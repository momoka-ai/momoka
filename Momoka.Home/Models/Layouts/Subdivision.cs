using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Layouts;

/// <summary>
/// A planar subdivision: a straight-line planar graph (inherits
/// <see cref="Graph2D{TEntity}"/>) plus face enumeration. Nodes are junctions,
/// edges are segments (diagonal allowed), and the bounded faces are the
/// enclosed regions (rooms). Edges may share endpoints but must not cross.
///
/// Faces are computed on demand by a half-edge traversal: from each directed
/// half-edge, walk the edge immediately after the reverse direction in
/// counter-clockwise order around each vertex. Bounded faces come out
/// counter-clockwise; the outer face comes out clockwise.
///
/// Known limitation: a region enclosing an inner loop (an island, e.g. a
/// freestanding counter) yields two separate face cycles rather than one face
/// with a hole.
/// </summary>
public class Subdivision<TEntity> : Graph2D<TEntity> where TEntity : class
{
    /// <summary>
    /// A face (region) of a planar subdivision: a closed cycle of vertices
    /// forming a polygon. Bounded interior faces are traversed counter-clockwise
    /// (positive signed area); the unbounded outer face is clockwise (negative).
    /// The vertex list maps directly to a region polygon boundary and supports
    /// diagonal edges.
    /// </summary>
    public class Face
    {
        public IReadOnlyList<Int2> Vertices { get; }

        public int Count => Vertices.Count;

        public Face(IReadOnlyList<Int2> vertices)
        {
            Vertices = vertices;
            SignedArea = ComputeSignedArea(vertices);
        }

        /// <summary>Positive = counter-clockwise (bounded interior face); negative = clockwise (outer).</summary>
        public double SignedArea { get; }

        public double Area => Math.Abs(SignedArea);

        public bool IsOuter => SignedArea < 0;

        /// <summary>
        /// Optional content entity bound to this face (e.g. a material marker for
        /// floor/ceiling surfaces). Persisted via <see cref="Subdivision{TEntity}.AssignEntity"/>.
        /// </summary>
        public TEntity? Entity { get; set; }

        /// <summary>Point-in-polygon via ray casting (same rule as a region boundary).</summary>
        public bool Contains(Int2 point)
        {
            var inside = false;
            for (int i = 0, j = Vertices.Count - 1; i < Vertices.Count; j = i++)
            {
                var a = Vertices[i];
                var b = Vertices[j];
                if ((a.Z > point.Z) != (b.Z > point.Z) &&
                    point.X < (b.X - a.X) * (point.Z - a.Z) / (double)(b.Z - a.Z) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>True if the two faces share at least one boundary edge.</summary>
        public bool SharesEdge(Face other)
        {
            for (var i = 0; i < Vertices.Count; i++)
            {
                var u = Vertices[i];
                var v = Vertices[(i + 1) % Vertices.Count];
                for (var j = 0; j < other.Vertices.Count; j++)
                {
                    var ou = other.Vertices[j];
                    var ov = other.Vertices[(j + 1) % other.Vertices.Count];
                    if ((u == ou && v == ov) || (u == ov && v == ou))
                        return true;
                }
            }
            return false;
        }

        private static double ComputeSignedArea(IReadOnlyList<Int2> pts)
        {
            var sum = 0.0;
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                sum += (double)a.X * b.Z - (double)b.X * a.Z;
            }
            return sum / 2;
        }

        public override string ToString() =>
            $"{nameof(Face)}[{string.Join(" → ", Vertices)}]";
    }

    private readonly Dictionary<HashSet<Int2>, TEntity?> _faceEntities = new();

    /// <summary>Binds a content entity (e.g. a material marker) to a face. Survives recomputation.</summary>
    public void AssignEntity(Face face, TEntity? entity) =>
        _faceEntities[new HashSet<Int2>(face.Vertices)] = entity;

    /// <summary>Returns the content entity bound to the given face, or default.</summary>
    public TEntity? EntityOf(Face face) =>
        _faceEntities.TryGetValue(new HashSet<Int2>(face.Vertices), out var entity)
            ? entity
            : default;

    // ── Face enumeration ─────────────────────────────────

    /// <summary>Enumerates all faces (bounded rooms + outer face).</summary>
    public List<Face> ComputeFaces()
    {
        var faces = new List<Face>();
        var visited = new HashSet<(Int2 From, Int2 To)>();

        foreach (var edge in Edges)
        {
            TraceFace(edge.A.Coords, edge.B.Coords, visited, faces);
            TraceFace(edge.B.Coords, edge.A.Coords, visited, faces);
        }
        return faces;
    }

    public List<Face> Faces => ComputeFaces();

    /// <summary>Only the bounded (counter-clockwise) faces — the rooms.</summary>
    public List<Face> BoundedFaces => ComputeFaces().Where(f => !f.IsOuter).ToList();

    /// <summary>Returns the first bounded face containing the point, or null.</summary>
    public Face? FaceAt(Int2 point) =>
        BoundedFaces.FirstOrDefault(f => f.Contains(point));

    /// <summary>Faces sharing at least one boundary edge with the given face.</summary>
    public List<Face> AdjacentFaces(Face face) =>
        BoundedFaces.Where(f => !ReferenceEquals(f, face) && f.SharesEdge(face)).ToList();

    /// <summary>
    /// Removes the wall edge(s) shared by two adjacent faces, merging them into
    /// one. Faces are recomputed on demand, so the merge takes effect on the
    /// next <see cref="ComputeFaces"/> call. Returns false if not adjacent.
    /// </summary>
    public bool Merge(Face a, Face b)
    {
        var shared = Edges
            .Where(e => IsEdgeOfFace(e, a) && IsEdgeOfFace(e, b))
            .ToList();
        if (shared.Count == 0)
            return false;
        foreach (var edge in shared)
            Edges.Remove(edge);
        return true;
    }

    private void TraceFace(Int2 startFrom, Int2 startTo, HashSet<(Int2, Int2)> visited, List<Face> faces)
    {
        if (!visited.Add((startFrom, startTo)))
            return;

        var vertices = new List<Int2>();
        var from = startFrom;
        var to = startTo;

        while (true)
        {
            vertices.Add(from);

            var next = NextHalfEdge(from, to);
            if (next is null)
                return; // open chain — not a closed face

            from = to;
            to = next.Value;

            if (from == startFrom && to == startTo)
                break;

            if (!visited.Add((from, to)))
                return; // would only happen for an invalid (crossing) embedding
        }

        if (vertices.Count >= 3)
        {
            var face = new Face(vertices);
            _faceEntities.TryGetValue(new HashSet<Int2>(vertices), out var entity);
            face.Entity = entity;
            faces.Add(face);
        }
    }

    private Int2? NextHalfEdge(Int2 from, Int2 to)
    {
        if (!_nodes.TryGetValue(to, out var node))
            return null;

        var reverse = from - to; // direction from `to` back to `from`
        var candidates = GetNeighbors(node)
            .Where(n => n.Coords != from)
            .ToList();
        if (candidates.Count == 0)
            return null;

        return candidates
            .OrderByDescending(n => CcwAngle(reverse, n.Coords - to))
            .First()
            .Coords;
    }

    private static bool IsEdgeOfFace(Edge e, Face f)
    {
        for (var i = 0; i < f.Vertices.Count; i++)
        {
            var u = f.Vertices[i];
            var v = f.Vertices[(i + 1) % f.Vertices.Count];
            if ((e.A.Coords == u && e.B.Coords == v) || (e.A.Coords == v && e.B.Coords == u))
                return true;
        }
        return false;
    }
}
