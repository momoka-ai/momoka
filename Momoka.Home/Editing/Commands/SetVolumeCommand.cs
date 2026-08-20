using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Editing.Commands;

/// <summary>
/// 体积替换命令（事务内部使用）：重栅格化实体的新旧占用格并替换 <see cref="Entity.Volume"/>
/// （墙排洞的落点——洞口格从网格清除，之后放置的门窗不再与之碰撞）。
/// Undo = 还原原体积并重栅格化。ChangeSet 按旧 + 新格产脏块（实体级变更语义）。
/// </summary>
public sealed class SetVolumeCommand : IEditorCommand
{
    private readonly Guid _entityId;
    private readonly Volume _volume;

    public string Name => "SetVolume";
    public string? CoalesceKey => null;

    private Entity? _entity;
    private Volume? _oldVolume;

    public SetVolumeCommand(Guid entityId, Volume volume)
    {
        _entityId = entityId;
        _volume = volume;
    }

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var unit = session.Layout;
        _entity = unit.Find(_entityId);
        if (_entity is null || _entity.Volume is null)
            return false;

        _oldVolume = _entity.Volume;
        var anchor = unit.Voxels.GetAsRelative(_entity.Transform.Position);
        Clear(unit, _entity, anchor, _oldVolume);
        _entity.Volume = _volume;
        Write(unit, _entity, anchor, _volume);

        changes.Modified(_entity);
        return true;
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        if (_entity is null || _oldVolume is null)
            return false;
        var unit = session.Layout;
        var anchor = unit.Voxels.GetAsRelative(_entity.Transform.Position);
        Clear(unit, _entity, anchor, _entity.Volume);
        _entity.Volume = _oldVolume;
        Write(unit, _entity, anchor, _oldVolume);

        changes.Modified(_entity);
        return true;
    }

    private static void Clear(UnitLayout unit, Entity entity, Int3 anchor, Volume volume)
    {
        foreach (var cell in volume.Cells3D())
        {
            var pos = anchor + cell;
            if (unit.Voxels[pos] == entity)
                unit.Voxels[pos] = default;
        }
    }

    private static void Write(UnitLayout unit, Entity entity, Int3 anchor, Volume volume)
    {
        foreach (var cell in volume.Cells3D())
            unit.Voxels[anchor + cell] = entity;
    }
}
