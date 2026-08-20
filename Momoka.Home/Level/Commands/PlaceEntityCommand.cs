using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Level.Commands;

/// <summary>
/// 放置命令：从"未放置池"（<see cref="LevelData.Entities"/>）按 EntityId 取出实体，
/// 根放置（无 HostId）或表面附着（HostId + 期望类别校验，复用
/// <c>UnitLayout.Add(entity, position, source)</c>）。Undo = Remove（非级联即无
/// 附着物）+ 回池（实体仍留在 LevelData.Entities）。
/// </summary>
public sealed class PlaceEntityCommand : IEditorCommand
{
    private readonly Guid _entityId;
    private readonly Float3 _position;
    private readonly Guid? _hostId;

    public string Name => "Place";
    public string? CoalesceKey => null;

    private Entity? _entity;
    private PlacementLayoutSource? _host;

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
        _entity = session.Data.Entities.FirstOrDefault(e => e.Id == _entityId);
        if (_entity is null)
            return false;
        if (unit.Entities.Contains(_entity))
            return false; // 已放置（宿主即自身 / 重复放置）

        if (_hostId is { } hostId)
        {
            var host = unit.Find(hostId);
            var source = host?.GetComponent<PlacementLayoutSource>();
            if (host is null || source is null)
                return false;
            _host = source;
            if (!unit.Add(_entity, new Position(_position), source))
                return false;
        }
        else
        {
            if (!unit.Add(_entity, new Position(_position)))
                return false;
        }

        changes.Added(_entity);
        return true;
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        if (_entity is null || !session.Layout.Remove(_entity))
            return false;
        changes.Removed(_entity);
        return true;
    }
}
