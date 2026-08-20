using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Level.Commands;

/// <summary>
/// 开洞命令（事务）：(1) 墙排洞——墙 Volume 替换为分段体积（Composite3D：
/// 左段 + 右段 + 过梁，排除洞口盒）；(2) 放置门 / 窗实体（<c>is_open</c>，
/// 挂宿主墙——级联删除 / Region 连通随 <c>!is_open</c> 判定天然正确）。
/// Undo = 还原墙原 Volume + 移除开口。洞盒不完整落在墙体内 → 事务整体失败（无残留）。
/// </summary>
public sealed class BuildOpeningCommand : CompositeCommand
{
    private readonly Guid _wallEntityId;
    private readonly Int3 _openingOrigin;
    private readonly Int3 _openingSize;
    private readonly string _openingKey;
    private readonly bool _isOpen;
    private bool _built;

    public override string Name => "BuildOpening";

    public BuildOpeningCommand(Guid wallEntityId, Int3 openingOrigin, Int3 openingSize, string openingKey, bool isOpen = true)
    {
        _wallEntityId = wallEntityId;
        _openingOrigin = openingOrigin;
        _openingSize = openingSize;
        _openingKey = openingKey;
        _isOpen = isOpen;
    }

    public override bool Execute(EditorSession session, out ChangeSet changes)
    {
        // 子命令只在首次执行时构建一次——redo 复用同一开口实体（Id 稳定、逆操作可重放）
        if (!_built)
        {
            if (!BuildChildren(session))
            {
                changes = new ChangeSet();
                return false;
            }
            _built = true;
        }
        return base.Execute(session, out changes);
    }

    private bool BuildChildren(EditorSession session)
    {
        Children.Clear();
        var unit = session.Layout;
        var wall = unit.Find(_wallEntityId);
        if (wall is null || wall.Volume is null)
            return false;

        // 洞盒必须完整落在墙体内（含幅界校验）
        var anchor = unit.Voxels.GetAsRelative(wall.Transform.Position);
        foreach (var cell in new Box3D { SizeX = _openingSize.X, SizeY = _openingSize.Y, SizeZ = _openingSize.Z }.Cells3D())
        {
            var p = _openingOrigin + cell;
            if (!LayoutHelpers.InWorldExtent(p))
                return false;
        }

        var punched = VolumePunch.Punch(wall.Volume, anchor, _openingOrigin, _openingSize);
        if (punched is null || wall.GetComponent<PlacementLayoutSource>() is null)
            return false;

        Children.Add(new SetVolumeCommand(_wallEntityId, punched));

        var opening = new Entity
        {
            Key = new Key(_openingKey),
            Volume = new Box3D { SizeX = _openingSize.X, SizeY = _openingSize.Y, SizeZ = _openingSize.Z },
        };
        opening.AddProperty(
            new BooleanProperty(Property.IsImmutable, true),
            new BooleanProperty(Property.IsOpen, _isOpen),
            new EnumProperty<RotationAlignment>(Property.RotationAlignment, RotationAlignment.Vertical));
        Children.Add(new RegisterEntityCommand(opening));
        Children.Add(new PlaceEntityCommand(opening.Id, _openingOrigin.ToFloat3() * unit.Voxels.Length, _wallEntityId));
        return true;
    }
}
