namespace Momoka.Core.Commands.Arguments;

/// <summary>浮点参数：接受十进制浮点（可选 min/max 闭区间）。</summary>
public sealed class DoubleArgument : Argument<double>
{
    private double? _min;
    private double? _max;

    public DoubleArgument(string id)
        : base(id)
    {
    }

    /// <summary>最小值（含）；设置后超出区间拒绝。</summary>
    public DoubleArgument Min(double min)
    {
        _min = min;
        return this;
    }

    /// <summary>最大值（含）；设置后超出区间拒绝。</summary>
    public DoubleArgument Max(double max)
    {
        _max = max;
        return this;
    }

    /// <inheritdoc />
    public override bool TryParse(string input, out double value) =>
        double.TryParse(input, out value)
        && (!_min.HasValue || value >= _min.Value)
        && (!_max.HasValue || value <= _max.Value);
}
