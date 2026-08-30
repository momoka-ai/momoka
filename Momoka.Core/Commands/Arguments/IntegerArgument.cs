namespace Momoka.Core.Commands.Arguments;

/// <summary>整数参数：接受十进制整数（可选 min/max 闭区间）。</summary>
public sealed class IntegerArgument : Argument<int>
{
    private int? _min;
    private int? _max;

    public IntegerArgument(string id)
        : base(id)
    {
    }

    /// <summary>最小值（含）；设置后超出区间拒绝。</summary>
    public IntegerArgument Min(int min)
    {
        _min = min;
        return this;
    }

    /// <summary>最大值（含）；设置后超出区间拒绝。</summary>
    public IntegerArgument Max(int max)
    {
        _max = max;
        return this;
    }

    /// <inheritdoc />
    public override bool TryParse(string input, out int value) =>
        int.TryParse(input, out value)
        && (!_min.HasValue || value >= _min.Value)
        && (!_max.HasValue || value <= _max.Value);
}
