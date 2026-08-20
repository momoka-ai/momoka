using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Editing.Protocol;

/// <summary>帧类型名（请求 / 事件共用一个注册表，仿 <c>JsonTypeNameRegistry</c> 思路）。</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FrameTypeAttribute : Attribute
{
    public string Name { get; }
    public FrameTypeAttribute(string name) => Name = name;
}

/// <summary>
/// 帧类型注册表：<see cref="FrameTypeAttribute"/> 标注的请求 / 事件类按 <c>type</c> 名
/// 判别。<see cref="Envelope.Type"/> + <see cref="Envelope.Payload"/>（JToken）→ 具体帧。
/// </summary>
public static class FrameRegistry
{
    private static readonly Dictionary<string, Type> Requests = new();
    private static readonly Dictionary<string, Type> Events = new();
    private static readonly Dictionary<Type, string> Names = new();

    static FrameRegistry()
    {
        foreach (var type in typeof(FrameRegistry).Assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(FrameTypeAttribute), false).FirstOrDefault() is not FrameTypeAttribute attr)
                continue;
            if (typeof(IRequestFrame).IsAssignableFrom(type))
                Requests[attr.Name] = type;
            if (typeof(IEventFrame).IsAssignableFrom(type))
                Events[attr.Name] = type;
            Names[type] = attr.Name;
        }
    }

    public static string NameOf(Type type) => Names[type];
    public static string NameOf<T>() where T : class => Names[typeof(T)];

    public static Type? GetRequestType(string typeName) =>
        Requests.GetValueOrDefault(typeName);

    public static Type? GetEventType(string typeName) =>
        Events.GetValueOrDefault(typeName);

    /// <summary>按 type 名 + JToken 载荷物化请求帧（未知 type → 抛异常）。</summary>
    public static IRequestFrame CreateRequest(string typeName, JToken? payload)
    {
        var type = GetRequestType(typeName)
            ?? throw new InvalidDataException($"Unknown request frame '{typeName}'.");
        if (payload is null)
            return (IRequestFrame)Activator.CreateInstance(type)!;
        return (IRequestFrame)payload.ToObject(type, JsonSerializer.Create(Settings.JsonSerialization))!;
    }

    /// <summary>按 type 名 + JToken 载荷物化事件帧（未知 type → 抛异常）。</summary>
    public static IEventFrame CreateEvent(string typeName, JToken? payload)
    {
        var type = GetEventType(typeName)
            ?? throw new InvalidDataException($"Unknown event frame '{typeName}'.");
        if (payload is null)
            return (IEventFrame)Activator.CreateInstance(type)!;
        return (IEventFrame)payload.ToObject(type, JsonSerializer.Create(Settings.JsonSerialization))!;
    }
}
