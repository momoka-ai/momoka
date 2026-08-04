using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

/// <summary>
/// A wall segment: a straight line (see <see cref="LineShape"/>) with a
/// thickness. Its two faces are exposed as placement surfaces through a
/// <see cref="VoxelLayoutSource"/> component, refreshed after the line is anchored.
/// </summary>
public class Wall : Entity<Int3>
{
    public static readonly TextureProperty TEXTURE = new("texture", new Key("wall"));

    /// <summary>Wall height in cells — drives the face surface extent.</summary>
    public int Height { get; set; } = 3;

    public Wall()
    {
        Shape = new LineShape();
        AddProperty(TEXTURE);
        AddComponent(new VoxelLayoutSource());
    }

    /// <summary>
    /// The two face placement surfaces (front/back) of this wall, refreshed from
    /// the current geometry on access. Diagonal walls expose no faces.
    /// </summary>
    public IEnumerable<VoxelLayout2D> Layouts
    {
        get
        {
            RefreshSurfaces();
            return GetComponent<VoxelLayoutSource>()!.Layouts;
        }
    }

    /// <summary>Recomputes the face surfaces into the <see cref="VoxelLayoutSource"/> component.</summary>
    public void RefreshSurfaces()
    {
        var source = GetComponent<VoxelLayoutSource>()!;
        source.Layouts.Clear();
        source.Layouts.AddRange(ComputeFaces());
    }

    private List<VoxelLayout2D> ComputeFaces()
    {
        var line = (LineShape)Shape;
        var a = Coords + line.Start.Int3;
        var b = Coords + line.End.Int3;
        var length = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Z - a.Z));
        var thickness = Math.Max(1, line.Thickness);
        var faces = new List<VoxelLayout2D>(2);

        if (a.Z == b.Z)
        {
            // East–West wall → South (−Z) and North (+Z) faces
            var x0 = Math.Min(a.X, b.X);
            var z0 = Math.Min(a.Z, b.Z);
            faces.Add(new VoxelLayout2D(new Int2(length, Height), new Int3(x0, a.Y, z0)) { Direction = Int3.South });
            faces.Add(new VoxelLayout2D(new Int2(length, Height), new Int3(x0, a.Y, z0 + thickness)) { Direction = Int3.North });
        }
        else if (a.X == b.X)
        {
            // North–South wall → West (−X) and East (+X) faces
            var z0 = Math.Min(a.Z, b.Z);
            var x0 = Math.Min(a.X, b.X);
            faces.Add(new VoxelLayout2D(new Int2(Height, length), new Int3(x0, a.Y, z0)) { Direction = Int3.West });
            faces.Add(new VoxelLayout2D(new Int2(Height, length), new Int3(x0 + thickness, a.Y, z0)) { Direction = Int3.East });
        }
        // Diagonal walls: axis-aligned Direction cannot express the face normal — no faces.

        return faces;
    }
}
