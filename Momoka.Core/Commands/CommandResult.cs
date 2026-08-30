namespace Momoka.Core.Commands;

/// <summary>指令执行结果（对应 Minestom 的 <c>ExecutableCommand.Result</c>）。</summary>
public enum CommandResult
{
    /// <summary>执行成功。</summary>
    Success,

    /// <summary>未知指令（名称或别名未注册）。</summary>
    Unknown,

    /// <summary>语法错误（参数解析失败：缺必需参数 / 参数过多 / 未知 -- 语法 / 引号未闭合）。</summary>
    InvalidSyntax,

    /// <summary>执行器抛出未处理异常。</summary>
    ExecutorException,

    /// <summary>执行被取消（CancellationToken）。</summary>
    Cancelled,
}
