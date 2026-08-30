using System.Text;
using Momoka.Core.Commands;

namespace Momoka.Core.Commands.Parsing;

/// <summary>
/// 指令解析器（对应 Minestom 的 <c>CommandParser</c>）：词法层把整行拆为命令名 + 参数数组
/// （<see cref="ParseLine"/>，引号分词）；语法层 <see cref="Query"/> 依声明序尝试命令的全部语法，
/// 命中产出 <see cref="CommandQueryResult"/>。
/// </summary>
public static class CommandParser
{
    /// <summary>把整行指令拆为命令名 + 参数数组（空行 / 引号未闭合抛 <see cref="CommandSyntaxException"/>）。</summary>
    public static (string Name, string[] Arguments) ParseLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        string[] tokens = Tokenize(line);
        return tokens.Length == 0
            ? throw new CommandSyntaxException("Command line is empty.")
            : (tokens[0], tokens[1..]);
    }

    /// <summary>
    /// 依声明序尝试语法列表匹配输入参数：命中返回携带该语法与解析值的结果；
    /// 全部失败返回 <see cref="CommandQueryResult.NoMatch"/>。
    /// </summary>
    public static CommandQueryResult Query(IReadOnlyList<CommandSyntax> syntaxes, string[] args)
    {
        ArgumentNullException.ThrowIfNull(syntaxes);
        ArgumentNullException.ThrowIfNull(args);

        foreach (CommandSyntax syntax in syntaxes)
        {
            if (syntax.TryMatch(args, out CommandQueryResult result))
            {
                return result;
            }
        }

        return CommandQueryResult.NoMatch;
    }

    /// <summary>把文本分词（支持单 / 双引号；引号未闭合抛 <see cref="CommandSyntaxException"/>）。</summary>
    internal static string[] Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        foreach (char c in text)
        {
            if (quote is { } activeQuote)
            {
                if (c == activeQuote)
                {
                    quote = null;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (quote is not null)
        {
            throw new CommandSyntaxException("Unterminated quoted string.");
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens.ToArray();
    }
}
