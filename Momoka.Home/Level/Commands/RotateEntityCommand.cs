using Momoka.Home.Entities;
using Momoka.Home.Primitives;
namespace Momoka.Home.Level.Commands;

/// <summary>
/// 旋转命令：改 Transform.Rotation（三轴欧拉增量，度）。体素占位轴对齐不变
/// （旋转只影响渲染 / 表面姿态），故无需重栅格化。<see cref="CoalesceKey"/> =
/// <c>Rotate#{id}</c>（连续旋转帧合并为一个历史项）。
/// </summary>
public sealed class RotateEntityCommand : IEditorCommand
{
    private readonly Guid _entityId;
    private readonly Float3 _delta;

    public string Name => "Rotate";
    public string? CoalesceKey => $"Rotate#{_entityId}";

    private Entity? _entity;

    public RotateEntityCommand(Guid entityId, Float3 delta)
    {
        _entityId = entityId;
        _delta = delta;
    }

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        _entity = session.Layout.Find(_entityId);
        if (_entity is null)
            return false;
        var r = _entity.Transform.Rotation;
        _entity.Transform = _entity.Transform with
        {
            Rotation = new Rotation(r.Yaw + _delta.X, r.Pitch + _delta.Y, r.Roll + _delta.Z),
        };
        changes.Modified(_entity);
        return true;
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        if (_entity is null)
            return false;
        var r = _entity.Transform.Rotation;
        _entity.Transform = _entity.Transform with
        {
            Rotation = new Rotation(r.Yaw - _delta.X, r.Pitch - _delta.Y, r.Roll - _delta.Z),
        };
        changes.Modified(_entity);
        return true;
    }
}
