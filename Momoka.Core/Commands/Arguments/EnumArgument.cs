namespace Momoka.Core.Commands.Arguments;

/// <summary>枚举参数：按枚举名匹配（忽略大小写）。</summary>
public sealed class EnumArgument<TEnum> : Argument<TEnum>
    where TEnum : struct, Enum
{
    public EnumArgument(string id)
        : base(id)
    {
    }

    /// <inheritdoc />
    public override bool TryParse(string input, out TEnum value) =>
        Enum.TryParse(input, ignoreCase: true, out value);
}
