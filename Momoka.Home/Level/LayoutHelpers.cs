using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Level;

/// <summary>编辑层共用的模型辅助（世界幅界校验 / Bound 扩展）。</summary>
public static class LayoutHelpers
{
    /// <summary>格是否落在世界幅界内（超出则体素 setter 静默丢弃写入）。</summary>
    public static bool InWorldExtent(Int3 cell) => Bound.IsValid(cell.ToFloat3());

    /// <summary>
    /// 把体积占用格并入网格 Bound（编辑结构件后调用，保证 Region / 查询层可见新范围）。
    /// 空体积不改变 Bound。
    /// </summary>
    public static void ExpandBoundToInclude(UnitLayout unit, Int3 anchor, Volume volume)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var minZ = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        var maxZ = int.MinValue;
        var any = false;
        foreach (var cell in volume.Cells3D())
        {
            var p = anchor + cell;
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Z < minZ) minZ = p.Z;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Z > maxZ) maxZ = p.Z;
            any = true;
        }
        if (!any)
            return;
        var cells = Bound.FromCorners(new Int3(minX, minY, minZ).ToFloat3(), new Int3(maxX, maxY, maxZ).ToFloat3());
        var current = unit.Voxels.Bound;
        unit.Voxels.Bound = current.Valid ? current.Union(cells) : cells;
    }
}
