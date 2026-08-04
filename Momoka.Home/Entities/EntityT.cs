using Momoka.Home.Shapes;
namespace Momoka.Home.Entities;

/// <summary>
/// A spatial entity with coordinates of type <typeparamref name="T"/>. The three
/// built-ins are <see cref="Int2"/> (tiles/materials), <see cref="Int3"/> (voxel
/// content — the config-template type), and <see cref="Float3"/> (continuous
/// living/moving objects, never rasterized). <see cref="Shape"/> carries the
/// body's geometry: meaningful for Int2/Int3, left null for Float3.
/// </summary>
public class Entity<T> : Entity where T : struct
{
    public T Coords { get; set; }

    public Shape Shape { get; set; } = null!;
}
