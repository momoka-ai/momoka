using Momoka.Home.Primitives;
namespace Momoka.Home.Levels.Volumes;

/// <summary>
/// 2D 截面光栅化助手：为 <see cref="Extruded3D"/> 族的剖面生成格数据（Int2[]）。
/// 只产出数据、不构成几何类型——2D 不再有独立的形状类型体系。
/// </summary>
internal static class Rasterizer
{
    public static IEnumerable<Int2> FilledRect(int sizeX, int sizeZ)
    {
        for (var x = 0; x < sizeX; x++)
            for (var z = 0; z < sizeZ; z++)
                yield return new Int2(x, z);
    }

    /// <summary>填充圆截面：x²+z² ≤ r²。</summary>
    public static IEnumerable<Int2> FilledCircle(int radius)
    {
        var r = radius;
        for (var x = -r; x <= r; x++)
            for (var z = -r; z <= r; z++)
                if (x * x + z * z <= r * r)
                    yield return new Int2(x, z);
    }

    /// <summary>填充椭圆截面：(x/rx)² + (z/rz)² ≤ 1。</summary>
    public static IEnumerable<Int2> FilledEllipse(int radiusX, int radiusZ)
    {
        var a = radiusX;
        var b = radiusZ;
        for (var x = -a; x <= a; x++)
            for (var z = -b; z <= b; z++)
                if (x * x * b * b + z * z * a * a <= a * a * b * b)
                    yield return new Int2(x, z);
    }

    /// <summary>环形截面：inner² ≤ x²+z² ≤ outer²。</summary>
    public static IEnumerable<Int2> FilledRing(int innerRadius, int outerRadius)
    {
        var outer = outerRadius;
        var inner2 = innerRadius * innerRadius;
        for (var x = -outer; x <= outer; x++)
        {
            for (var z = -outer; z <= outer; z++)
            {
                var d2 = x * x + z * z;
                if (d2 >= inner2 && d2 <= outer * outer)
                    yield return new Int2(x, z);
            }
        }
    }

    /// <summary>任意（凸或凹）多边形截面：包围盒采样 + even-odd 点内判定，边界格含。</summary>
    public static IEnumerable<Int2> FilledPolygon(IEnumerable<Int2> vertices)
    {
        var list = vertices.ToList();
        if (list.Count < 3)
            yield break;

        var minX = list.Min(v => v.X);
        var maxX = list.Max(v => v.X);
        var minZ = list.Min(v => v.Z);
        var maxZ = list.Max(v => v.Z);

        for (var x = minX; x <= maxX; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                var cell = new Int2(x, z);
                if (ContainsCenter(list, cell))
                    yield return cell;
            }
        }
    }

    /// <summary>True if the cell's CENTER (x+0.5, z+0.5) lies inside the polygon.</summary>
    private static bool ContainsCenter(List<Int2> vertices, Int2 cell)
    {
        var px = cell.X + 0.5;
        var pz = cell.Z + 0.5;
        var inside = false;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            var a = vertices[i];
            var b = vertices[j];
            if ((a.Z > pz) != (b.Z > pz) &&
                px < (b.X - a.X) * (pz - a.Z) / (double)(b.Z - a.Z) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
