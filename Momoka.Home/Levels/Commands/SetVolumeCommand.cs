using Momoka.Home.Runtime;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
namespace Momoka.Home.Levels.Commands;

/// <summary>
/// 体积替换命令（结构命令内部使用）：重栅格化实体的新旧占用格并替换
/// <see cref="Entity.Volume"/>（墙排洞的落点——洞口格从网格清除，之后放置的
/// 门窗不再与之碰撞）。
/// </summary>
public sealed class SetVolumeCommand : IEditorCommand
{
    private readonly Guid _entityId;
    private readonly Volume _volume;

    public SetVolumeCommand(Guid entityId, Volume volume)
    {
        _entityId = entityId;
        _volume = volume;
    }

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var unit = session.Layout;
        var entity = unit.Find(_entityId);
        if (entity is null || entity.Volume is null)
            return false;

        var anchor = unit.Voxels.GetAsRelative(entity.Transform.Position);
        Clear(unit, entity, anchor, entity.Volume);
        entity.Volume = _volume;
        Write(unit, entity, anchor, _volume);

        changes.Modified(entity);
        return true;
    }

    private static void Clear(LevelLayout unit, Entity entity, Int3 anchor, Volume volume)
    {
        foreach (var cell in volume.GetVoxelSet())
        {
            var pos = anchor + cell;
            if (unit.Voxels[pos] == entity)
                unit.Voxels[pos] = default;
        }
    }

    private static void Write(LevelLayout unit, Entity entity, Int3 anchor, Volume volume)
    {
        foreach (var cell in volume.GetVoxelSet())
            unit.Voxels[anchor + cell] = entity;
    }
}
