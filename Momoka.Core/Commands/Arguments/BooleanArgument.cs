namespace Momoka.Core.Commands.Arguments;

/// <summary>布尔参数：接受 <c>true</c> / <c>false</c>（忽略大小写）。</summary>
public sealed class BooleanArgument : Argument<bool>
{
    public BooleanArgument(string id)
        : base(id)
    {
    }

    /// <inheritdoc />
    public override bool TryParse(string input, out bool value) =>
        bool.TryParse(input, out value);
}
