using Momoka.Home.Level;
using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
namespace Momoka.Home.Level.Commands;

/// <summary>放置：池 → 空间（根放置或表面附着）。</summary>
public sealed class PlaceEntityCommand : IEditorCommand
{
    private readonly Guid _entityId;
    private readonly Float3 _position;
    private readonly Guid? _hostId;

    public PlaceEntityCommand(Guid entityId, Float3 position, Guid? hostId = null)
    {
        _entityId = entityId;
        _position = position;
        _hostId = hostId;
    }

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var unit = session.Layout;
        var entity = session.Data.Entities.FirstOrDefault(e => e.Id == _entityId);
        if (entity is null)
            return false;
        if (unit.Entities.Contains(entity))
            return false;

        if (_hostId is { } hostId)
        {
            var host = unit.Find(hostId);
            var source = host?.GetComponent<PlacementLayoutSource>();
            if (host is null || source is null)
                return false;
            if (!unit.Add(entity, new Position(_position), source))
                return false;
        }
        else if (!unit.Add(entity, new Position(_position)))
        {
            return false;
        }

        changes.Added(entity);
        return true;
    }
}
