using Momoka.Home.Levels.Entities;
namespace Momoka.Home.Runtime.Protocol;

/// <summary>
/// 单实体增量（<c>layout_changed</c> 载荷）：<c>added</c> / <c>modified</c> 带完整实体
/// 载荷；<c>removed</c> 只带 <see cref="EntityId"/>（客户端凭本地 Registry 取旧值）。
/// </summary>
public sealed class EntityDelta
{
    public string Kind { get; set; } = "";
    public Guid? EntityId { get; set; }
    public Entity? Entity { get; set; }
}
