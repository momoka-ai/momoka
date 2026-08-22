using Momoka.Home.Runtime;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Primitives;
namespace Momoka.Home.Levels.Commands;

/// <summary>旋转：改 Transform.Rotation（三轴欧拉增量，度）。体素占位轴对齐不变
/// （旋转只影响渲染 / 表面姿态），故无需重栅格化。</summary>
public sealed class RotateEntityCommand : IEditorCommand
{
    private readonly Guid _entityId;
    private readonly Float3 _delta;

    public RotateEntityCommand(Guid entityId, Float3 delta)
    {
        _entityId = entityId;
        _delta = delta;
    }

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var entity = session.Layout.Find(_entityId);
        if (entity is null)
            return false;
        var r = entity.Transform.Rotation;
        entity.Transform = entity.Transform with
        {
            Rotation = new Rotation(r.Yaw + _delta.X, r.Pitch + _delta.Y, r.Roll + _delta.Z),
        };
        changes.Modified(entity);
        return true;
    }
}
