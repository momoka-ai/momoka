using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Momoka.Core.Services;

namespace Momoka.Core.Plugins;

/// <summary>
/// 服务注入器：[ServiceInjection] 注入 pass。仅对插件声明的服务提供者实例生效——
/// 遍历其 [ServiceInjection] 属性，按属性类型查 <see cref="Service{T}"/> 当前提供商并赋值；
/// 记录使用边到 <see cref="ServiceUsageGraph"/>（来源为插件实例时）。
/// 可空性 = 硬失败开关：非可空属性无提供商 → 抛 <see cref="InvalidOperationException"/>（fail-fast）；
/// 可空属性无提供商 → 留 null。反射（属性/闭式解析方法/记录属性）按类型缓存。
/// </summary>
public static class ServiceInjector
{
    private static readonly ConcurrentDictionary<Type, InjectionSlot[]> SlotsCache = new();

    /// <summary>注入 <paramref name="plugin"/> 声明的全部服务提供者；<paramref name="graph"/> 缺省时不记录使用边。</summary>
    public static void Inject(Plugin plugin, ServiceUsageGraph? graph = null)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        foreach (Plugin.ServiceProviderRegistration registration in plugin.ServiceProviders)
        {
            InjectProvider(plugin, registration.Provider, graph);
        }
    }

    private static void InjectProvider(Plugin plugin, object provider, ServiceUsageGraph? graph)
    {
        foreach (InjectionSlot slot in GetSlots(provider.GetType()))
        {
            object? registration = slot.ResolveRegistration.Invoke(null, null);
            if (registration is null)
            {
                if (slot.Required)
                {
                    throw new InvalidOperationException(
                        $"Service '{slot.ServiceType.Name}' required by '{provider.GetType().Name}.{slot.Property.Name}' " +
                        $"([ServiceInjection]) is not registered.");
                }

                continue;
            }

            object resolved = slot.ProviderProperty.GetValue(registration)!;
            object? source = slot.SourceProperty.GetValue(registration);
            slot.Property.SetValue(provider, resolved);

            if (graph is not null && source is Plugin sourcePlugin)
            {
                graph.Add(plugin, sourcePlugin);
            }
        }
    }

    private static InjectionSlot[] GetSlots(Type type)
        => SlotsCache.GetOrAdd(type, BuildSlots);

    private static InjectionSlot[] BuildSlots(Type type)
    {
        var slots = new List<InjectionSlot>();
        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<ServiceInjectionAttribute>() is null)
            {
                continue;
            }

            Type serviceType = property.PropertyType;
            if (serviceType.IsValueType)
            {
                throw new InvalidOperationException(
                    $"[ServiceInjection] property '{type.Name}.{property.Name}' must reference a class or interface service type.");
            }

            // 可空性判定：编译器对 T? 引用属性发射 [Nullable(2)]；缺省（无标记）视为必填。
            bool required = property.GetCustomAttribute<NullableAttribute>()?.NullableFlags.FirstOrDefault() != 2;
            Type registrationType = typeof(ServiceRegistration<>).MakeGenericType(serviceType);
            slots.Add(new InjectionSlot(
                serviceType,
                property,
                required,
                typeof(Service<>).MakeGenericType(serviceType)
                    .GetProperty(nameof(Service<object>.CurrentRegistration), BindingFlags.Public | BindingFlags.Static)!
                    .GetGetMethod()!,
                registrationType.GetProperty(nameof(ServiceRegistration<object>.Provider))!,
                registrationType.GetProperty(nameof(ServiceRegistration<object>.Source))!));
        }

        return slots.ToArray();
    }

    private sealed record InjectionSlot(
        Type ServiceType,
        PropertyInfo Property,
        bool Required,
        MethodInfo ResolveRegistration,
        PropertyInfo ProviderProperty,
        PropertyInfo SourceProperty);
}
