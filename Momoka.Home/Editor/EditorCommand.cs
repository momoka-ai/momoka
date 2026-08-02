using Momoka.Home.Models.Entities;
using Momoka.Home.Primitives;

namespace Momoka.Home.Editor;

public abstract class EditorCommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;

    public abstract void Apply(BlockGridEntity space);
    public abstract void Revert(BlockGridEntity space);
}

public class MoveEntityCommand : EditorCommand
{
    private readonly Guid _entityId;
    private readonly Int3 _from;
    private readonly Int3 _to;

    public MoveEntityCommand(BlockEntity entity, Int3 from, Int3 to)
    {
        _entityId = entity.Id;
        _from = from;
        _to = to;
        Description = $"Move {entity.Key} from {from} to {to}";
    }

    public override void Apply(BlockGridEntity space)
    {
        var entity = space[_from];
        if (entity is null) return;
        space[_from] = null;
        space[_to] = entity;
    }

    public override void Revert(BlockGridEntity space)
    {
        var entity = space[_to];
        if (entity is null) return;
        space[_to] = null;
        space[_from] = entity;
    }
}
