namespace Momoka.Core.Commands;

/// <summary>指令语法构建非法（fail-fast）：同一语法内参数 id 重复 / 可选参数后跟必需参数 / 参数 id 非法。</summary>
public sealed class IllegalCommandStructureException : Exception
{
    public IllegalCommandStructureException(string message)
        : base(message)
    {
    }
}
