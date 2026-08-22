using Momoka.Home.Levels.Entities;
using Momoka.Home.Runtime.Protocol;
namespace Momoka.Core.Gateway;

/// <summary>
/// 服务端 → 客户端调用约定（<see cref="HomeService"/> 的客户端契约，供
/// <c>Clients.All.Xxx(...)</c> 强类型广播）。Ui 侧以
/// <c>connection.On(nameof(...))</c> 注册同名处理函数（HomeClient 待 Ui 语言定案后创建）。
/// 契约 DTO 复用 Momoka.Home 领域类型（实体 / 增量 / 帧载荷）。
/// </summary>
public interface IHomeClient
{
    /// <summary>新实体物化并登记进注册表（<c>create_entity</c> 批准后广播）。</summary>
    Task EntityCreated(Entity entity);

    /// <summary>布局变更事实通道（一切布局操作的广播，含全局版本号，客户端凭此更新镜像）。</summary>
    Task LayoutChanged(uint version, EntityDelta[] deltas);

    /// <summary>持久化完成（<c>save</c> 成功后的生命周期确认）。</summary>
    Task SaveCompleted();
}
