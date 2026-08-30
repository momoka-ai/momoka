using System.Globalization;
using Momoka.Core.Commands.Arguments;

namespace Momoka.Core.Commands;

/// <summary>
/// 单次指令调用上下文：实际调用名（本名 / 别名 / 子命令名）、原始参数与解析后的
/// 类型化值（<see cref="Arguments"/>：参数 id → 类型化值）与原始文本表（<see cref="Get(string)"/>）。
/// 类型化取值经 <see cref="Get{T}(string)"/> / <see cref="Get(Argument)"/>。终端模型不含发起者抽象
/// （无 sender / 角色 / 权限——权限鉴权归宿主，输出通道由执行器自行捕获）。
/// </summary>
public sealed class CommandContext
{
    private readonly IReadOnlyDictionary<string, string?> _rawValues;

    /// <summary>创建调用上下文（由 <see cref="CommandManager"/> 在匹配成功后构造）。</summary>
    public CommandContext(
        string name,
        IReadOnlyList<string> rawArguments,
        IReadOnlyDictionary<string, object?> arguments,
        IReadOnlyDictionary<string, string?>? rawValues = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        RawArguments = rawArguments ?? throw new ArgumentNullException(nameof(rawArguments));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        _rawValues = rawValues ?? new Dictionary<string, string?>(StringComparer.Ordinal);
    }

    /// <summary>实际调用名（本名 / 别名 / 子命令名，未归一化）。</summary>
    public string Name { get; }

    /// <summary>原始参数（命令名之后的分词结果）。</summary>
    public IReadOnlyList<string> RawArguments { get; }

    /// <summary>解析后的类型化值表：参数 id → 类型化值（字符串参数为 string，整数为 int 等）。</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; }

    /// <summary>是否含指定参数。</summary>
    public bool Contains(string name) => Arguments.ContainsKey(name);

    /// <summary>读取参数原始文本（原始 token）；未提供返回 null。</summary>
    public string? Get(string name) =>
        _rawValues.TryGetValue(name, out string? raw)
            ? raw
            : Arguments.TryGetValue(name, out object? value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : null;

    /// <summary>尝试读取参数原始文本；未提供返回 false。</summary>
    public bool TryGet(string name, out string? value)
    {
        value = Get(name);
        return value is not null;
    }

    /// <summary>读取并转换参数值（缺失抛 <see cref="InvalidOperationException"/>，转换失败抛 <see cref="FormatException"/>）。</summary>
    public T Get<T>(string name)
    {
        if (!Arguments.TryGetValue(name, out object? raw) || raw is null)
        {
            throw new InvalidOperationException($"Command argument '{name}' was not provided.");
        }

        return ConvertValue<T>(raw, name);
    }

    /// <summary>读取参数值（未提供返回 null）。</summary>
    public object? Get(Argument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return Arguments.TryGetValue(argument.Id, out object? value) ? value : null;
    }

    /// <summary>读取并转换参数值（缺失抛 <see cref="InvalidOperationException"/>，转换失败抛 <see cref="FormatException"/>）。</summary>
    public T Get<T>(Argument<T> argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return Get<T>(argument.Id);
    }

    private static T ConvertValue<T>(object? raw, string name)
    {
        if (raw is T typed)
        {
            return typed;
        }

        Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        object? converted;
        if (target.IsEnum)
        {
            try
            {
                converted = Enum.Parse(target, Convert.ToString(raw, CultureInfo.InvariantCulture)!, ignoreCase: true);
            }
            catch (Exception ex)
            {
                throw new FormatException(
                    $"Command argument '{name}' has value '{raw}' which is not a valid '{target.Name}'.", ex);
            }
        }
        else
        {
            try
            {
                converted = Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new FormatException(
                    $"Command argument '{name}' has value '{raw}' which cannot be converted to '{target.Name}'.", ex);
            }
        }

        return (T)converted!;
    }
}
