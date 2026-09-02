namespace Momoka.Core.Services;

/// <summary>
/// 服务注入标记：标注在服务提供者类（经 <c>Plugin.AddService</c> 注册的实例）的属性上，
/// 宿主注入 pass 按属性类型从 <see cref="Service{T}"/> 解析并赋值。
/// 可空性即硬失败开关：<c>T?</c> 可空属性在无服务实例时留 null 不炸；
/// <c>T</c> 非可空属性无服务实例 → 注入 pass fail-fast 抛 <see cref="InvalidOperationException"/>。
/// 仅服务提供者可携带本标记；Command / EventHandler / PacketHandler 由 Core 管理，不参与注入。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ServiceInjectionAttribute : Attribute
{
}
