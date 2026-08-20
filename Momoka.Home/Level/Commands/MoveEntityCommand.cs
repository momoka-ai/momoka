using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Level.Commands;

/// <summary>
/// 移动命令：改 Transform.Position，重栅格化旧/新格，宿主登记迁移
/// （旧宿主 → 新宿主，森林不变量）；其表面上的物件随宿主同位移（相对附着保持）。
/// <see cref="CoalesceKey"/> = <c>Move#{id}</c>——连续拖拽帧合并为一个历史项，
/// 一次撤销回到拖拽前。
/// </summary>
public sealed class MoveEntityCommand : IEditorCommand
{
    private readonly Guid _entityId;
    private readonly Float3 _position;
    private readonly Guid? _hostId;

    public string Name => "Move";
    public string? CoalesceKey => $"Move#{_entityId}";

    private Entity? _entity;
    private Float3 _beforePosition;
    private PlacementLayoutSource? _beforeHost;

    public MoveEntityCommand(Guid entityId, Float3 position, Guid? hostId = null)
    {
        _entityId = entityId;
        _position = position;
        _hostId = hostId;
    }

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var unit = session.Layout;
        _entity = unit.Find(_entityId);
        if (_entity is null)
            return false;

        var host = _hostId is { } hid ? unit.Find(hid)?.GetComponent<PlacementLayoutSource>() : null;
        if (host is not null && _entity.GetComponents<PlacementLayoutSource>().Contains(host))
            return false; // 宿主即自身

        _beforePosition = _entity.Transform.Position;
        _beforeHost = unit.FindHostEntity(_entity);

        if (!unit.Move(_entity, new Position(_position), host))
            return false;

        foreach (var e in unit.CascadeOf(_entity))
            changes.Modified(e);
        return true;
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        if (_entity is null)
            return false;
        var unit = session.Layout;
        if (!unit.Move(_entity, new Position(_beforePosition), _beforeHost))
            return false;
        foreach (var e in unit.CascadeOf(_entity))
            changes.Modified(e);
        return true;
    }
}
