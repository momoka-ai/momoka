namespace Momoka.Core.Commands.Arguments;

/// <summary>字符串参数：单个 token → string（值内空格由输入引号控制）。</summary>
public sealed class StringArgument : Argument<string>
{
    public StringArgument(string id)
        : base(id)
    {
    }

    /// <inheritdoc />
    public override bool TryParse(string input, out string value)
    {
        value = input;
        return true;
    }
}
