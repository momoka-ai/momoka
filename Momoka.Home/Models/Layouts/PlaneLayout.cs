using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Layouts;

/// <summary>
/// A large planar surface — a floor, a ceiling, a shelf unit — combining:
///  • the placement surface itself (the <see cref="VoxelLayout2D"/> base: which
///    cells objects may occupy on the plane),
///  • a planar <see cref="Subdivision{T}"/> of material/region faces covering
///    the plane (wood / tile / carpet zones),
///  • a stack of attachment <see cref="PlaneLayer"/>s at fixed heights along
///    <see cref="VoxelLayout2D.Direction"/> (raised platform layers above a
///    floor, hanging-fixture layers below a ceiling).
/// </summary>
public class PlaneLayout<T> : VoxelLayout2D, IVoxelLayout2DSource where T : class
{
    /// <summary>Material / region faces covering this plane.</summary>
    public Subdivision<T> Subdivision { get; } = new();

    private readonly List<PlaneLayer> _layers = new();

    /// <summary>Attachment layers, ordered by ascending height.</summary>
    public IReadOnlyList<PlaneLayer> Layers => _layers;

    public PlaneLayout(Int2 size, Int3? offset = null) : base(size, offset)
    {
    }

    /// <summary>
    /// All attachment surfaces of this plane: the plane itself, then each layer
    /// surface, ordered by height.
    /// </summary>
    public IReadOnlyList<VoxelLayout2D> Layouts
    {
        get
        {
            var surfaces = new List<VoxelLayout2D> { this };
            surfaces.AddRange(_layers.Select(l => l.Surface));
            return surfaces;
        }
    }

    /// <summary>
    /// Adds an attachment layer <paramref name="height"/> cells along
    /// <see cref="VoxelLayout2D.Direction"/> from the plane origin. Its surface
    /// shares the plane's extent and direction.
    /// </summary>
    public PlaneLayer AddLayer(int height)
    {
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Layer height must be positive.");
        if (LayerAt(height) is not null)
            throw new ArgumentException($"A layer at height {height} already exists.", nameof(height));

        var surface = new VoxelLayout2D(ChunkSize, Offset + Direction * height) { Direction = Direction };
        var layer = new PlaneLayer(height, surface);
        _layers.Add(layer);
        _layers.Sort((a, b) => a.Height.CompareTo(b.Height));
        return layer;
    }

    /// <summary>Returns the layer at the given height, or null.</summary>
    public PlaneLayer? LayerAt(int height) =>
        _layers.FirstOrDefault(l => l.Height == height);

    /// <summary>Removes the layer at the given height. False if absent.</summary>
    public bool RemoveLayer(int height)
    {
        var layer = LayerAt(height);
        return layer is not null && _layers.Remove(layer);
    }
}

/// <summary>
/// An attachment layer of a <see cref="PlaneLayout{T}"/>: a placement surface at
/// a fixed height along the plane's normal direction.
/// </summary>
public sealed class PlaneLayer
{
    /// <summary>Height in cells along the plane's Direction.</summary>
    public int Height { get; }

    /// <summary>This layer's placement surface (independent blocked cells).</summary>
    public VoxelLayout2D Surface { get; }

    internal PlaneLayer(int height, VoxelLayout2D surface)
    {
        Height = height;
        Surface = surface;
    }
}
