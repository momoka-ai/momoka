using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Editor;

/// <summary>
/// A reversible edit operation over a <see cref="VoxelLayout3D"/> occupancy
/// container. Commands operate on the layout directly (composition), not on the
/// entity shell.
/// </summary>
public abstract class EditorCommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;

    public abstract void Apply(VoxelLayout3D layout);
    public abstract void Revert(VoxelLayout3D layout);
}

public class MoveEntityCommand : EditorCommand
{
    private readonly Guid _entityId;
    private readonly Int3 _from;
    private readonly Int3 _to;

    public MoveEntityCommand(Entity<Int3> entity, Int3 from, Int3 to)
    {
        _entityId = entity.Id;
        _from = from;
        _to = to;
        Description = $"Move {entity.Key} from {from} to {to}";
    }

    public override void Apply(VoxelLayout3D layout)
    {
        var entity = layout[_from];
        if (entity is null || entity.Id != _entityId) return;
        layout[_from] = null;
        layout[_to] = entity;
    }

    public override void Revert(VoxelLayout3D layout)
    {
        var entity = layout[_to];
        if (entity is null || entity.Id != _entityId) return;
        layout[_to] = null;
        layout[_from] = entity;
    }
}
