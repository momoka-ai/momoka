namespace Momoka.Core.Commands;

/// <summary>指令语法错误（迷你语言解析失败：缺必需参数 / 未知标志 / 参数过多 / 引号未闭合 / 语法声明非法）。</summary>
public sealed class CommandSyntaxException : Exception
{
    public CommandSyntaxException(string message)
        : base(message)
    {
    }
}
