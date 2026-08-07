using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Primitives;
namespace Momoka.Home.Layouts;

/// <summary>
/// A chunked 3D voxel space whose cells reference the <see cref="Entity{Int3}"/>
/// occupying them — the successor of <c>VoxelLayout3D</c>. Storage is
/// Minecraft-style: XZ chunks (<see cref="VoxelChunk{T}"/>) keyed by a packed
/// long, each a column of <see cref="VoxelChunkSection{T}"/> (16×16×16 paletted
/// sections) along the height axis. Sections are created lazily, so empty bands
/// cost nothing and adding height is just appending a section — the chunk
/// structure never needs recomputing.
/// </summary>
public class VoxelLayout<T> where T : Entity
{
    /// <summary>Section edge length — a power of two, so chunk math is shift/mask.</summary>
    public const int SectionSize = 16;

    private readonly Dictionary<long, VoxelChunk<T>> _chunks = new();

    /// <summary>All entities held by this space, kept in sync with the cell grid.</summary>
    public List<T> Entities { get; } = new();

    /// <summary>Inclusive footprint of the space (optional, set by the owner).</summary>
    public Bound Bound { get; set; } = Bound.Empty;

    /// <summary>The entity at the given position, or null.</summary>
    public T? this[Int3 coords]
    {
        get
        {
            var key = ChunkKey(coords);
            return _chunks.TryGetValue(key, out var chunk) ? chunk[ChunkLocal(coords)] : null;
        }
        set
        {
            var key = ChunkKey(coords);
            if (!_chunks.TryGetValue(key, out var chunk))
            {
                chunk = new VoxelChunk<T>(new Int2(coords.X >> 4, coords.Z >> 4));
                _chunks[key] = chunk;
            }
            chunk[ChunkLocal(coords)] = value;
        }
    }

    /// <summary>Removes all chunk storage (keeps <see cref="Entities"/>).</summary>
    public void Clear() => _chunks.Clear();

    /// <summary>True if an entity occupies the given position.</summary>
    public bool HasEntity(Int3 pos) => this[pos] is not null;

    /// <summary>
    /// True if placing <paramref name="entity"/> at <paramref name="cs"/> would
    /// collide: the anchor or any of its (local) shape voxels lands on an
    /// occupied cell.
    /// </summary>
    public bool IsEntityCollided(T entity, Int3 cs)
    {
        if (HasEntity(cs))
            return true;

        foreach (var cell in entity.Volume.Cells3D())
        {
            if (HasEntity(cs + cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True if placing <paramref name="src"/> at <paramref name="cs"/> intersects
    /// the specific <paramref name="dest"/> entity (dest voxels vs src voxels).
    /// </summary>
    public bool IsEntityCollided(T dest, T src, Int3 cs)
    {
        var destCells = dest.Volume.Cells3D()
            .Select(v => dest.Coords + v)
            .ToHashSet();
        return src.Volume.Cells3D().Any(v => destCells.Contains(cs + v));
    }

    /// <summary>
    /// Builds (places) the entity at <paramref name="cs"/>: writes EVERY one of
    /// its shape voxels into the grid and registers it. False if collided.
    /// </summary>
    public bool BuildAt(T entity, Int3 cs)
    {
        if (IsEntityCollided(entity, cs))
            return false;

        entity.Coords = cs;
        foreach (var cell in entity.Volume.Cells3D())
        {
            this[cs + cell] = entity;
        }
        Entities.Add(entity);
        return true;
    }

    /// <summary>
    /// Undoes <see cref="BuildAt"/> at the entity's registered position:
    /// removes the entity whose Coords equals <paramref name="pos"/>.
    /// </summary>
    public bool DestroyAt(Int3 pos)
    {
        var entity = Entities.FirstOrDefault(e => e.Coords == pos);
        return entity is not null && Remove(entity);
    }

    /// <summary>Removes the entity covering the given target cell.</summary>
    public bool DestroyTarget(Int3 target)
    {
        if (this[target] is not T entity)
            return false;
        return Remove(entity);
    }

    /// <summary>
    /// Copies another layout's occupancy into this space, offset by
    /// <paramref name="offset"/> (a child container at its position in the parent).
    /// Cells keep referencing the leaf entities; the same entity may live in both
    /// the child's and the parent's <see cref="Entities"/> list (upward composition).
    /// </summary>
    public void MergeFrom(VoxelLayout<T> child, Int3 offset)
    {
        foreach (var entity in child.Entities)
        {
            Entities.Add(entity);
            foreach (var cell in entity.Volume.Cells3D())
            {
                this[offset + entity.Coords + cell] = entity;
            }
        }
    }

    /// <summary>Undoes <see cref="MergeFrom"/>: removes the child's entities and clears their cells.</summary>
    public void RemoveFrom(VoxelLayout<T> child, Int3 offset)
    {
        foreach (var entity in child.Entities)
        {
            Entities.Remove(entity);
            foreach (var cell in entity.Volume.Cells3D())
            {
                var pos = offset + entity.Coords + cell;
                if (this[pos] == entity)
                    this[pos] = null;
            }
        }
    }

    /// <summary>
    /// Clears the current storage and re-rasterizes every held entity into
    /// the grid — a forced flush/refresh after direct low-level cell writes.
    /// </summary>
    public void Rebuild()
    {
        Clear();
        foreach (var entity in Entities)
        {
            foreach (var cell in entity.Volume.Cells3D())
            {
                this[entity.Coords + cell] = entity;
            }
        }
    }

    private bool Remove(T entity)
    {
        if (!Entities.Remove(entity))
            return false;

        foreach (var cell in entity.Volume.Cells3D())
        {
            var pos = entity.Coords + cell;
            if (this[pos] == entity)
                this[pos] = null;
        }
        return true;
    }

    /// <summary>
    /// Returns all entities whose shape intersects the axis-aligned box
    /// <paramref name="min"/>–<paramref name="max"/> (inclusive). Drag-select.
    /// </summary>
    public List<T> GetEntitiesInBound(Int2 min, Int2 max)
    {
        var result = new List<T>();
        foreach (var entity in Entities)
        {
            foreach (var loc in entity.Volume.Cells3D())
            {
                var p = (entity.Coords + loc).Xz;
                if (p.X >= min.X && p.X <= max.X && p.Z >= min.Z && p.Z <= max.Z)
                {
                    result.Add(entity);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>All entities assignable to the specified type.</summary>
    public List<TEntity> GetEntitiesOfType<TEntity>() where TEntity : T =>
        Entities.OfType<TEntity>().ToList();

    /// <summary>The entity at the given position, or null.</summary>
    public T? GetEntityAtPoint(Int3 pos) => this[pos];

    /// <summary>Nearest entity by expanding spiral search, or null.</summary>
    public T? GetEntityAtNearest(Int3 pos)
    {
        for (var radius = 0; radius < 1000; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                        continue;

                    var candidate = this[new Int3(pos.X + dx, pos.Y, pos.Z + dz)];
                    if (candidate is not null)
                        return candidate;
                }
            }
        }
        return null;
    }

    /// <summary>Finds an entity by its unique Id across this space.</summary>
    public Entity? FindEntity(Guid id) =>
        Entities.FirstOrDefault(e => e.Id == id);

    // ── Chunk math (SectionSize = 16, power of two) ────────────────────

    private static long ChunkKey(Int3 coords) =>
        ((long)(coords.X >> 4) << 32) | (uint)(coords.Z >> 4);

    private static Int3 ChunkLocal(Int3 coords) =>
        new(coords.X & 15, coords.Y, coords.Z & 15);
}

/// <summary>
/// An XZ chunk column of a <see cref="VoxelLayout{T}"/>: the footprint is fixed
/// at 16×16 cells; the height axis is a growable array of
/// <see cref="VoxelChunkSection{T}"/> (one per 16 cells of height). Sections are
/// created on first write, so a column's height grows by appending — never by
/// recomputing existing sections.
/// </summary>
public class VoxelChunk<T> where T : Entity
{
    private VoxelChunkSection<T>[] _sections = Array.Empty<VoxelChunkSection<T>>();

    /// <summary>Chunk column index in the XZ plane.</summary>
    public Int2 Index { get; }

    /// <summary>Sections of this column, low to high (may contain null gaps).</summary>
    public IReadOnlyList<VoxelChunkSection<T>?> Sections => _sections;

    public VoxelChunk(Int2 index) => Index = index;

    /// <summary>
    /// Chunk-local cell access: x/z in [0,16), y any (the column height).
    /// </summary>
    public T? this[Int3 local]
    {
        get
        {
            var s = local.Y >> 4;
            if (s >= _sections.Length)
                return null;
            var section = _sections[s];
            return section?[new Int3(local.X, local.Y & 15, local.Z)];
        }
        set
        {
            var s = local.Y >> 4;
            if (_sections.Length <= s)
                Array.Resize(ref _sections, s + 1);

            var section = _sections[s];
            if (section is null)
            {
                section = new VoxelChunkSection<T>();
                _sections[s] = section;
            }
            section[new Int3(local.X, local.Y & 15, local.Z)] = value;
        }
    }
}

/// <summary>
/// A 16×16×16 paletted section of a <see cref="VoxelChunk{T}"/> column: the
/// atomic storage unit, backed by a <see cref="PalettedContainer{Int3, T}"/>
/// with a chunk strategy. Coordinates are section-local.
/// </summary>
public class VoxelChunkSection<T> where T : Entity
{
    /// <summary>Paletted cell storage of this 16×16×16 section.</summary>
    public PalettedContainer<Int3, T> Data { get; } = new(
        new Palette<T>.Int3ChunkStrategy(
            new Int3(VoxelLayout<T>.SectionSize, VoxelLayout<T>.SectionSize, VoxelLayout<T>.SectionSize),
            initialBits: 4));

    /// <summary>Section-local cell access (all components in [0,16)).</summary>
    public T? this[Int3 local]
    {
        get => Data[local];
        set => Data[local] = value;
    }
}
