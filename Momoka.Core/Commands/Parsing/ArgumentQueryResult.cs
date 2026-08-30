namespace Momoka.Core.Commands.Parsing;

/// <summary>单个参数解析结果：成功产出类型化值，失败含原因。</summary>
public readonly record struct ArgumentQueryResult(bool Matched, object? Value, string? Error)
{
    /// <summary>解析成功（<paramref name="value"/> 为类型化值）。</summary>
    public static ArgumentQueryResult Success(object? value) => new(true, value, null);

    /// <summary>解析失败（<paramref name="error"/> 为原因描述）。</summary>
    public static ArgumentQueryResult Failure(string error) => new(false, null, error);
}
