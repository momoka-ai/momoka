using Momoka.Core.Commands;

namespace Momoka.Core.Commands.Parsing;

/// <summary>
/// 指令解析结果：成功命中某条语法（携带该语法 + 类型化值与原始文本表），否则未匹配。
/// 由 <see cref="CommandParser.Query"/> 依声明序匹配产出；<see cref="CommandManager"/> 据
/// <see cref="Matched"/> 决定执行对应语法的执行器或落入默认执行器 / InvalidSyntax。
/// </summary>
public sealed class CommandQueryResult
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyValues =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string?> EmptyRaw =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    private CommandQueryResult(
        CommandSyntax? syntax,
        IReadOnlyDictionary<string, object?> arguments,
        IReadOnlyDictionary<string, string?> rawArguments)
    {
        Syntax = syntax;
        Arguments = arguments;
        RawArguments = rawArguments;
    }

    /// <summary>是否命中某条语法。</summary>
    public bool Matched => Syntax is not null;

    /// <summary>命中的语法（未匹配为 null）。</summary>
    public CommandSyntax? Syntax { get; }

    /// <summary>解析出的类型化值表（参数 id → 类型化值）。</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; }

    /// <summary>解析出的原始文本表（参数 id → 原始 token）。</summary>
    public IReadOnlyDictionary<string, string?> RawArguments { get; }

    /// <summary>命中结果（携带命中的语法与解析值）。</summary>
    public static CommandQueryResult Hit(
        CommandSyntax syntax,
        IReadOnlyDictionary<string, object?> arguments,
        IReadOnlyDictionary<string, string?> rawArguments)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(rawArguments);
        return new CommandQueryResult(syntax, arguments, rawArguments);
    }

    /// <summary>未命中结果（空值表）。</summary>
    public static CommandQueryResult NoMatch { get; } = new(null, EmptyValues, EmptyRaw);
}
