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
/// 砌墙命令（事务）：创建不可变墙实体，Volume 由格段生成（直墙 = Box3D，多段 =
/// Composite3D），并挂一垂直放置面（供开洞 / 挂件宿主登记）。Undo = 移除墙（连带其
/// 开洞——门/窗挂在其表面）。
/// </summary>
public sealed class BuildWallCommand : CompositeCommand
{
    private readonly IReadOnlyList<WallSegment> _segments;
    private bool _built;
    private Int3 _anchor;
    private Volume? _volume;

    public override string Name => "BuildWall";

    public BuildWallCommand(IEnumerable<WallSegment> segments) => _segments = segments.ToList();

    public override bool Execute(EditorSession session, out ChangeSet changes)
    {
        // 子命令只在首次执行时构建一次——redo 复用同一实体（Id 稳定、逆操作可重放）
        if (!_built)
        {
            if (!BuildChildren(session))
            {
                changes = new ChangeSet();
                return false;
            }
            _built = true;
        }

        if (!base.Execute(session, out changes))
            return false;

        LayoutHelpers.ExpandBoundToInclude(session.Layout, _anchor, _volume!);
        return true;
    }

    private bool BuildChildren(EditorSession session)
    {
        Children.Clear();
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

        _volume = _segments.Count == 1
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
        _anchor = min;

        var wall = new Entity { Key = new Key("wall"), Volume = _volume };
        wall.AddProperty(new BooleanProperty(Property.IsImmutable, true));
        if (BuildWallSurface(_segments[0], unit.Voxels.Length) is { } surface)
            wall.AddComponent(surface);

        Children.Add(new RegisterEntityCommand(wall));
        Children.Add(new PlaceEntityCommand(wall.Id, min.ToFloat3() * unit.Voxels.Length));
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
