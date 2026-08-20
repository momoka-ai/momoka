using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Level.Commands;

/// <summary>
/// 删除命令：删除实体（复用 <c>UnitLayout.Remove(entity, cascade)</c>——连带回落
/// 其表面上的所有物件；删除门/窗开口后墙体的洞口**保留**（墙 Volume 已排洞，语义已定，
/// Ui 需提示）。Undo = 按原位置 + 原宿主 + 原登记逆序重放整个级联链。
/// </summary>
public sealed class RemoveEntityCommand : IEditorCommand
{
    private readonly Guid _entityId;

    public string Name => "Remove";
    public string? CoalesceKey => null;

    private Entity? _entity;
    private List<(Entity Entity, Float3 Position, PlacementLayoutSource? Host)> _inverse = new();

    public RemoveEntityCommand(Guid entityId) => _entityId = entityId;

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var unit = session.Layout;
        _entity = unit.Find(_entityId);
        if (_entity is null)
            return false;

        // 级联闭包（子先于父）逐个快照 原位置 + 原宿主——Undo 反转即"父先于子"重放
        _inverse = unit.CascadeOf(_entity)
            .Select(e => (e, e.Transform.Position, unit.FindHostEntity(e)))
            .ToList();
        if (!unit.Remove(_entity))
            return false;

        foreach (var (e, _, _) in _inverse)
            changes.Removed(e);
        return true;
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        if (_entity is null || _inverse.Count == 0)
            return false;
        var unit = session.Layout;
        for (var i = _inverse.Count - 1; i >= 0; i--)
        {
            var (e, pos, host) = _inverse[i];
            var ok = host is not null
                ? unit.Add(e, new Position(pos), host)
                : unit.Add(e, new Position(pos));
            if (!ok)
                return false;
            changes.Added(e);
        }
        return true;
    }
}
