using Momoka.Home.Entities;
namespace Momoka.Home.Entities;

/// <summary>
/// 实体间宿主关系查询：谁附着在谁的放置表面上（<see cref="PlacementLayoutSource"/>）。
/// 关系由放置操作（<c>UnitLayout.Add(entity, position, source)</c>）产生并随
/// <c>Remove</c> 消解——本接口只暴露**查询侧**：消费方（编辑器删除确认框、
/// 命令层 undo、删除反登记）面向接口编程，不依赖具体空间根实现。
/// 与 <see cref="IEntitySource"/>（实体列表来源）正交：提供实体的源未必管理宿主关系。
/// </summary>
public interface IEntityRelationSource
{
    /// <summary>物件的宿主表面（其附着所在表面）；根物件（无宿主）返回 null。</summary>
    PlacementLayoutSource? FindHostEntity(Entity entity);
}
