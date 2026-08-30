namespace Momoka.Core.Commands.Arguments;

/// <summary>
/// 字符串数组参数：解析类 JSON 数组字面量（单个 token，<c>[a, b, c]</c> 逗号分隔并去首尾空白，
/// <c>[]</c> 为空数组；无方括号的单词视为单元素数组）。值内空格由引号控制
/// （如 <c>"[a, b, c]"</c>）。固定元数——数组就是这一个 token，多余 token 一律不匹配。
/// </summary>
public sealed class StringArrayArgument : Argument<string[]>
{
    public StringArrayArgument(string id)
        : base(id)
    {
    }

    /// <inheritdoc />
    public override bool TryParse(string input, out string[] value)
    {
        string body = input.Length >= 2 && input.StartsWith('[') && input.EndsWith(']')
            ? input[1..^1]
            : input;

        value = body.Length == 0
            ? []
            : [.. body.Split(',').Select(item => item.Trim())];
        return true;
    }
}
