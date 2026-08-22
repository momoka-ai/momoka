using Momoka.Home.Runtime;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Levels.Entities.Components;
namespace Momoka.Home.Levels.Commands;

/// <summary>移动：改位置 + 宿主迁移（表面物随宿主同位移）。</summary>
public sealed class MoveEntityCommand : IEditorCommand
{
    private readonly Guid _entityId;
    private readonly Float3 _position;
    private readonly Guid? _hostId;

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
        var entity = unit.Find(_entityId);
        if (entity is null)
            return false;

        var host = _hostId is { } hostId
            ? unit.Find(hostId)?.GetComponent<PlacementLayoutSource>()
            : null;
        if (host is not null && entity.GetComponents<PlacementLayoutSource>().Contains(host))
            return false;

        if (!unit.Move(entity, new Position(_position), host))
            return false;
        foreach (var cascaded in unit.CascadeOf(entity))
            changes.Modified(cascaded);
        return true;
    }
}
