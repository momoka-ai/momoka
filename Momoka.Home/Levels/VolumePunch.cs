using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
namespace Momoka.Home.Levels;

/// <summary>
/// 墙排洞：从墙体体积中扣除矩形洞口盒，输出与墙同锚点的分段体积
/// （直墙 = Box3D，多段 = Composite3D——左段 + 右段 + 过梁，无需新 Volume 子类）。
/// 洞盒必须完整落在墙体内，否则返回 null（事务拒绝）。
/// </summary>
public static class VolumePunch
{
    /// <summary>
    /// 从 <paramref name="wallVolume"/>（锚定在 <paramref name="wallAnchor"/> 格）扣除
    /// 绝对洞口盒 <paramref name="openingOrigin"/>（含）..+<paramref name="openingSize"/>（不含），
    /// 返回同锚点的分段体积。仅支持盒形墙 / 盒形子体积的分段墙。
    /// </summary>
    public static Volume? Punch(Volume wallVolume, Int3 wallAnchor, Int3 openingOrigin, Int3 openingSize)
    {
        var holeBox = new Box3D { SizeX = openingSize.X, SizeY = openingSize.Y, SizeZ = openingSize.Z };
        var holeCells = holeBox.Cells3D().Select(c => openingOrigin + c).ToHashSet();
        var wallCells = wallVolume.Cells3D().Select(c => wallAnchor + c).ToHashSet();
        if (holeCells.Any(c => !wallCells.Contains(c)))
            return null;

        var pieces = new List<(Int3 Origin, Int3 Size)>();
        switch (wallVolume)
        {
            case Box3D box:
                pieces.AddRange(Decompose(wallAnchor, new Int3(box.SizeX, box.SizeY, box.SizeZ), openingOrigin, openingSize));
                break;
            case Composite3D composite:
                foreach (var child in composite.Children)
                {
                    if (child.Shape is not Box3D childBox)
                        return null; // 仅支持盒形子体积
                    pieces.AddRange(Decompose(
                        wallAnchor + child.Offset,
                        new Int3(childBox.SizeX, childBox.SizeY, childBox.SizeZ),
                        openingOrigin,
                        openingSize));
                }
                break;
            default:
                return null;
        }

        var children = pieces
            .Where(p => p.Size.X > 0 && p.Size.Y > 0 && p.Size.Z > 0)
            .Select(p => new CompositeChild3D
            {
                Offset = p.Origin - wallAnchor,
                Shape = new Box3D { SizeX = p.Size.X, SizeY = p.Size.Y, SizeZ = p.Size.Z },
            })
            .ToList();
        if (children.Count == 0)
            return null;

        if (children.Count == 1 && children[0].Offset == Int3.Zero)
            return children[0].Shape;

        return new Composite3D { Children = children };
    }

    /// <summary>
    /// 盒 减 洞 的标准六向分解：洞盒先裁剪进盒内；输出覆盖 盒\洞 的互不重叠盒段
    /// （左 / 右 X 段 → 中 X 带的下 / 上 Y 段 → 中 X/Y 带的前 / 后 Z 段）。
    /// 洞与盒不相交时原样返回。
    /// </summary>
    private static IEnumerable<(Int3 Origin, Int3 Size)> Decompose(
        Int3 boxOrigin, Int3 boxSize, Int3 holeOrigin, Int3 holeSize)
    {
        var h0 = new Int3(
            Math.Max(boxOrigin.X, holeOrigin.X),
            Math.Max(boxOrigin.Y, holeOrigin.Y),
            Math.Max(boxOrigin.Z, holeOrigin.Z));
        var h1 = new Int3(
            Math.Min(boxOrigin.X + boxSize.X, holeOrigin.X + holeSize.X),
            Math.Min(boxOrigin.Y + boxSize.Y, holeOrigin.Y + holeSize.Y),
            Math.Min(boxOrigin.Z + boxSize.Z, holeOrigin.Z + holeSize.Z));
        if (h0.X >= h1.X || h0.Y >= h1.Y || h0.Z >= h1.Z)
        {
            yield return (boxOrigin, boxSize);
            yield break;
        }

        var end = boxOrigin + boxSize;

        if (boxOrigin.X < h0.X)
            yield return (boxOrigin, new Int3(h0.X - boxOrigin.X, boxSize.Y, boxSize.Z));
        if (h1.X < end.X)
            yield return (new Int3(h1.X, boxOrigin.Y, boxOrigin.Z), new Int3(end.X - h1.X, boxSize.Y, boxSize.Z));

        if (boxOrigin.Y < h0.Y)
            yield return (new Int3(h0.X, boxOrigin.Y, boxOrigin.Z), new Int3(h1.X - h0.X, h0.Y - boxOrigin.Y, boxSize.Z));
        if (h1.Y < end.Y)
            yield return (new Int3(h0.X, h1.Y, boxOrigin.Z), new Int3(h1.X - h0.X, end.Y - h1.Y, boxSize.Z));

        if (boxOrigin.Z < h0.Z)
            yield return (new Int3(h0.X, h0.Y, boxOrigin.Z), new Int3(h1.X - h0.X, h1.Y - h0.Y, h0.Z - boxOrigin.Z));
        if (h1.Z < end.Z)
            yield return (new Int3(h0.X, h0.Y, h1.Z), new Int3(h1.X - h0.X, h1.Y - h0.Y, end.Z - h1.Z));
    }
}
