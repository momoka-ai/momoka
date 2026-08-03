using Momoka.Home.Primitives;

using Momoka.Home;
namespace Momoka.Home.Editor;

public abstract class EditorCommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;

    public abstract void Apply(VoxelGridEntity space);
    public abstract void Revert(VoxelGridEntity space);
}

public class MoveEntityCommand : EditorCommand
{
    private readonly Guid _entityId;
    private readonly Int3 _from;
    private readonly Int3 _to;

    public MoveEntityCommand(VoxelEntity entity, Int3 from, Int3 to)
    {
        _entityId = entity.Id;
        _from = from;
        _to = to;
        Description = $"Move {entity.Key} from {from} to {to}";
    }

    public override void Apply(VoxelGridEntity space)
    {
        var entity = space[_from];
        if (entity is null) return;
        space[_from] = null;
        space[_to] = entity;
    }

    public override void Revert(VoxelGridEntity space)
    {
        var entity = space[_to];
        if (entity is null) return;
        space[_to] = null;
        space[_from] = entity;
    }
}
