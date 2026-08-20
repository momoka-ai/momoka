using Momoka.Home.Level;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Level.Commands;

/// <summary>墙体的一段：绝对格坐标（含）..+Size（不含）。</summary>
public sealed class WallSegment
{
    public Int3 Origin { get; set; }
    public Int3 Size { get; set; }

    public WallSegment() { }
    public WallSegment(Int3 origin, Int3 size)
    {
        Origin = origin;
        Size = size;
    }
}

/// <summary>
/// 砌墙命令（创建即放置，无暂存态）：校验幅界 → 生成不可变墙实体（直墙 = Box3D，
/// 多段 = Composite3D）并挂一垂直放置面（供开洞 / 挂件宿主登记）→ 放入空间 →
/// 登记注册表 → 扩展 Bound。validate-then-apply——预检通过后无失败路径，整体原子。
/// </summary>
public sealed class BuildWallCommand : IEditorCommand
{
    private readonly IReadOnlyList<WallSegment> _segments;

    public BuildWallCommand(IEnumerable<WallSegment> segments) => _segments = segments.ToList();

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        if (_segments.Count == 0)
            return false;

        // 写格前校验幅界（超出则 setter 静默丢格——近边界编辑显式拒绝）
        foreach (var seg in _segments)
        {
            for (var x = seg.Origin.X; x < seg.Origin.X + seg.Size.X; x++)
                for (var y = seg.Origin.Y; y < seg.Origin.Y + seg.Size.Y; y++)
                    for (var z = seg.Origin.Z; z < seg.Origin.Z + seg.Size.Z; z++)
                        if (!LayoutHelpers.InWorldExtent(new Int3(x, y, z)))
                            return false;
        }

        var unit = session.Layout;
        var min = new Int3(
            _segments.Min(s => s.Origin.X),
            _segments.Min(s => s.Origin.Y),
            _segments.Min(s => s.Origin.Z));

        Volume volume = _segments.Count == 1
            ? new Box3D { SizeX = _segments[0].Size.X, SizeY = _segments[0].Size.Y, SizeZ = _segments[0].Size.Z }
            : new Composite3D
            {
                Children = _segments
                    .Select(s => new CompositeChild3D
                    {
                        Offset = s.Origin - min,
                        Shape = new Box3D { SizeX = s.Size.X, SizeY = s.Size.Y, SizeZ = s.Size.Z },
                    })
                    .ToList(),
            };

        var wall = new Entity { Key = new Key("wall"), Volume = volume };
        wall.AddProperty(new BooleanProperty(Property.IsImmutable, true));
        if (BuildWallSurface(_segments[0], unit.Voxels.Length) is { } surface)
            wall.AddComponent(surface);

        // 创建即放置：先入空间（失败无残留），成功后登记注册表
        if (!unit.Add(wall, new Position(min.ToFloat3() * unit.Voxels.Length)))
            return false;
        session.Data.Entities.Add(wall);
        LayoutHelpers.ExpandBoundToInclude(unit, min, volume);

        changes.Added(wall);
        return true;
    }

    /// <summary>
    /// 墙体外侧垂直放置面（法向水平 → RotationAlignment.Vertical）：供门 / 窗开洞
    /// 挂宿主（级联删除）与期望类别校验。精确面映射由渲染端按格生成，此处仅需
    /// 垂直朝向——斜向表面等更复杂姿态为描述预留。
    /// </summary>
    private static PlacementLayoutSource? BuildWallSurface(WallSegment seg, float length)
    {
        Int2 gridSize;
        Rotation face;
        if (seg.Size.X == 1)
        {
            gridSize = new Int2(seg.Size.Z, seg.Size.Y);
            face = Rotation.East;
        }
        else
        {
            gridSize = new Int2(seg.Size.X, seg.Size.Y);
            face = Rotation.South;
        }
        var grid = new GridLayout<bool>(gridSize);
        grid.Fill(true, Int2.Zero, gridSize);
        return new PlacementLayoutSource
        {
            Layout = grid,
            Transform = new Transform(seg.Origin.ToFloat3() * length, face),
        };
    }
}
