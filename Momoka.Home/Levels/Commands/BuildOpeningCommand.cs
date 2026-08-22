using Momoka.Home.Runtime;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Levels.Commands;

/// <summary>
/// 开洞命令（事务）：(1) 预检——洞盒完整落在墙体内（VolumePunch 可分段）+ 幅界
/// 校验 + 开口格仅被墙占用；(2) 墙排洞——墙 Volume 替换为分段体积（Composite3D：
/// 左段 + 右段 + 过梁，排除洞口盒）；(3) 放置门 / 窗实体（is_open，挂宿主墙——
/// 级联删除）。validate-then-apply——预检通过后各步骤无失败路径，整体原子。
/// </summary>
public sealed class BuildOpeningCommand : IEditorCommand
{
    private readonly Guid _wallEntityId;
    private readonly Int3 _openingOrigin;
    private readonly Int3 _openingSize;
    private readonly string _openingKey;
    private readonly bool _isOpen;

    public BuildOpeningCommand(Guid wallEntityId, Int3 openingOrigin, Int3 openingSize, string openingKey, bool isOpen = true)
    {
        _wallEntityId = wallEntityId;
        _openingOrigin = openingOrigin;
        _openingSize = openingSize;
        _openingKey = openingKey;
        _isOpen = isOpen;
    }

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var unit = session.Layout;
        var wall = unit.Find(_wallEntityId);
        if (wall is null || wall.Volume is null)
            return false;

        // 洞盒必须完整落在墙体内（含幅界校验）
        var openingVolume = new Box3D { SizeX = _openingSize.X, SizeY = _openingSize.Y, SizeZ = _openingSize.Z };
        foreach (var cell in openingVolume.Cells3D())
        {
            var p = _openingOrigin + cell;
            if (!LayoutHelpers.InWorldExtent(p))
                return false;
        }

        var punched = VolumePunch.Punch(wall.Volume, unit.Voxels.GetAsRelative(wall.Transform.Position), _openingOrigin, _openingSize);
        var surface = wall.GetComponent<PlacementLayoutSource>();
        if (punched is null || surface is null)
            return false;

        // 预检：开口格仅允许被墙占用（排洞后清空——保证放置无碰撞）
        foreach (var cell in openingVolume.Cells3D())
        {
            var occupant = unit.Voxels[_openingOrigin + cell];
            if (occupant is not null && occupant != wall)
                return false;
        }

        // 排洞
        if (!new SetVolumeCommand(_wallEntityId, punched).Execute(session, out var volumeChanges))
            return false;
        changes.Merge(volumeChanges);

        // 开口实体：占洞口格，挂宿主墙
        var opening = new Entity
        {
            Key = new Key(_openingKey),
            Volume = openingVolume,
        };
        opening.AddProperty(
            new BooleanProperty(Property.IsImmutable, true),
            new BooleanProperty(Property.IsOpen, _isOpen),
            new EnumProperty<RotationAlignment>(Property.RotationAlignment, RotationAlignment.Vertical));

        if (!unit.Add(opening, new Position(_openingOrigin.ToFloat3() * unit.Voxels.Length), surface))
            return false;
        session.Data.Entities.Add(opening);

        changes.Added(opening);
        return true;
    }
}
