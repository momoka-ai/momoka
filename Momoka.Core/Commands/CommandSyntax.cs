using Momoka.Core.Commands.Arguments;
using Momoka.Core.Commands.Parsing;

namespace Momoka.Core.Commands;

/// <summary>
/// 指令语法：一组 <see cref="Argument"/> + 一个执行器。输入 token 序列匹配参数表成功 →
/// 调用执行器并注入解析值；参数 id 唯一性与可选参数尾随（有缺省值即可选）在构造时校验（fail-fast）。
/// 可选性由本语法依 <see cref="Argument.DefaultValue"/> 控制：尾部可选参数可省略并注入缺省值。
/// </summary>
public sealed class CommandSyntax
{
    /// <summary>创建语法（可选参数必须全部尾随；参数 id 不得重复）。</summary>
    public CommandSyntax(
        CommandExecutor executor,
        IEnumerable<Argument> arguments)
    {
        Executor = executor ?? throw new ArgumentNullException(nameof(executor));
        ArgumentNullException.ThrowIfNull(arguments);

        List<Argument> list = [.. arguments];
        Validate(list);
        Arguments = list;
    }

    /// <summary>匹配成功时调用的执行器。</summary>
    public CommandExecutor Executor { get; }

    /// <summary>该语法的参数表（顺序即匹配顺序）。</summary>
    public IReadOnlyList<Argument> Arguments { get; }

    /// <summary>
    /// 从迷你语言格式构建语法（<c>&lt;必需&gt; [可选]</c>，<c>...&gt; 后缀变长</c>）：
    /// 位置参数 → <see cref="StringArgument"/> / <see cref="StringArrayArgument"/>，
    /// 可选参数以空串缺省标记。token 非法 → fail-fast。
    /// </summary>
    internal static CommandSyntax FromFormat(string format, CommandExecutor executor)
    {
        List<Argument> arguments = [.. CommandParser.Tokenize(format).Select(token => token switch
        {
            _ when token.Length >= 2 && token[0] == '<' && token[^1] == '>' => Positional(token[1..^1], required: true),
            _ when token.Length >= 2 && token[0] == '[' && token[^1] == ']' => Positional(token[1..^1], required: false),
            _ => throw new CommandSyntaxException($"Invalid syntax token '{token}'."),
        })];

        return new CommandSyntax(executor, arguments);
    }

    /// <summary>
    /// 尝试把输入 token 序列匹配到本语法（固定元数：token 数必须落在
    /// [必需参数数, 槽位总数] 区间，每个槽位恰好消费一个 token）：成功产出携带类型化值表与
    /// 原始文本表的 <see cref="CommandQueryResult"/>（<see cref="CommandQueryResult.Matched"/>），
    /// 返回 true；失败返回 false（供 <see cref="CommandParser.Query"/> 尝试下一条语法）。
    /// 尾部可选参数（有缺省值）缺失时注入缺省值。
    /// 终端无标志参数：任何 <c>--</c> 前缀 token 视为未知语法。
    /// </summary>
    internal bool TryMatch(string[] tokens, out CommandQueryResult result)
    {
        Dictionary<string, object?> values = new(StringComparer.Ordinal);
        Dictionary<string, string?> raw = new(StringComparer.Ordinal);

        int required = Arguments.Count(argument => argument.DefaultValue is null);
        if (tokens.Length < required || tokens.Length > Arguments.Count)
        {
            result = CommandQueryResult.NoMatch;
            return false;
        }

        for (int index = 0; index < tokens.Length; index++)
        {
            Argument argument = Arguments[index];
            string token = tokens[index];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                result = CommandQueryResult.NoMatch;
                return false;
            }

            ArgumentQueryResult parsed = argument.Parse(token);
            if (!parsed.Matched)
            {
                result = CommandQueryResult.NoMatch;
                return false;
            }

            values[argument.Id] = parsed.Value;
            raw[argument.Id] = token;
        }

        foreach (Argument argument in Arguments.Skip(tokens.Length).Where(argument => argument.DefaultValue is not null))
        {
            values[argument.Id] = argument.DefaultValue;
        }

        result = CommandQueryResult.Hit(this, values, raw);
        return true;
    }

    private static Argument Positional(string inner, bool required)
    {
        bool variadic = inner.EndsWith("...", StringComparison.Ordinal);
        inner = variadic ? inner[..^3] : inner;
        if (string.IsNullOrWhiteSpace(inner) || inner.Any(char.IsWhiteSpace))
        {
            throw new CommandSyntaxException($"Invalid positional argument '{inner}'.");
        }

        Argument argument = variadic ? new StringArrayArgument(inner) : new StringArgument(inner);
        return required ? argument : argument.WithDefaultValue(string.Empty);
    }

    private static void Validate(IReadOnlyList<Argument> arguments)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (arguments.Any(argument => !ids.Add(argument.Id)))
        {
            throw new IllegalCommandStructureException("Duplicate argument id in syntax.");
        }

        // 可选参数（有缺省值）必须全部尾随
        bool optional = false;
        foreach (Argument argument in arguments)
        {
            if (argument.DefaultValue is not null)
            {
                optional = true;
            }
            else if (optional)
            {
                throw new IllegalCommandStructureException(
                    "Required argument follows an optional one; optional arguments must be trailing.");
            }
        }
    }
}
