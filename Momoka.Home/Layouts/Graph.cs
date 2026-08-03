using Momoka.Home.Primitives;

namespace Momoka.Home;

public class Graph<T, TCoords> where T : class where TCoords : notnull
{
    protected readonly Dictionary<TCoords, Node> _nodes = new();
    public IEnumerable<Node> Nodes => _nodes.Values;
    public List<Edge> Edges { get; } = new();

    public Node AddNode(TCoords coords)
    {
        if (_nodes.TryGetValue(coords, out var existing))
            return existing;

        var node = new Node(coords);
        _nodes[coords] = node;
        return node;
    }

    public Node? TryGetNode(TCoords coords) =>
        _nodes.TryGetValue(coords, out var node) ? node : null;

    public bool RemoveNode(TCoords coords)
    {
        if (!_nodes.TryGetValue(coords, out var node))
            return false;

        Edges.RemoveAll(e => e.Has(node));
        _nodes.Remove(coords);
        return true;
    }

    public Edge AddEdge(Node from, Node to, T? entity = null)
    {
        var edge = new Edge(from, to, entity);
        Edges.Add(edge);
        return edge;
    }

    public Edge AddEdge(TCoords from, TCoords to, T? entity = null) =>
        AddEdge(AddNode(from), AddNode(to), entity);

    public bool RemoveEdge(Edge edge) => Edges.Remove(edge);

    public IEnumerable<Edge> GetEdges(Node node) =>
        Edges.Where(e => e.Has(node));

    public IEnumerable<Node> GetNeighbors(Node node) =>
        GetEdges(node)
            .Select(e => e.Exclude(node))
            .Distinct();

    protected Edge? FindEdge(Node a, Node b)
    {
        foreach (var edge in Edges)
        {
            if ((edge.A == a && edge.B == b) || (edge.A == b && edge.B == a))
                return edge;
        }
        return null;
    }

    public static Graph<T, TCoords> operator +(Graph<T, TCoords> g, TCoords coords)
    {
        g.AddNode(coords);
        return g;
    }

    public static Graph<T, TCoords> operator -(Graph<T, TCoords> g, TCoords coords)
    {
        g.RemoveNode(coords);
        return g;
    }

    public static Graph<T, TCoords> operator +(Graph<T, TCoords> g, (TCoords from, TCoords to) edge)
    {
        g.AddEdge(edge.from, edge.to);
        return g;
    }

    public readonly record struct Node(TCoords Coords);

    public readonly record struct Edge(Node A, Node B, T? Entity)
    {
        public Node Exclude(Node node) => (A == node) ? B : A;

        public bool Has(Node node) => (A == node) || (B == node);
    }
}

/// <summary>
/// 2D graph (XZ plane). The exterior boundary ring is only meaningful here:
/// face traversal (sharpest-left-turn walk keeping the interior on the left)
/// follows concave corners correctly. Empty if the graph has no closed loop.
/// </summary>
public class Graph2D<TEntity> : Graph<TEntity, Int2> where TEntity : class
{
    public List<Edge> Bounds
    {
        get
        {
            var result = new List<Edge>();
            if (_nodes.Count == 0)
                return result;

            // Start at the leftmost-bottommost node.
            var start = _nodes.Values
                .OrderBy(n => n.Coords.X)
                .ThenBy(n => n.Coords.Z)
                .First();

            // Walk north from the leftmost corner: the exterior lies on the left.
            var next = GetNeighbors(start)
                .OrderByDescending(n => n.Coords.Z)
                .FirstOrDefault();
            if (next == default)
                return result;

            var current = start;

            while (true)
            {
                var edge = FindEdge(current, next);
                if (edge is null) break;
                result.Add(edge.Value);

                if (next == start)
                    break;

                var incoming = next.Coords - current.Coords;
                var candidates = GetNeighbors(next)
                    .Where(n => n != current)
                    .ToList();
                if (candidates.Count == 0)
                    break;

                // Sharpest left turn = smallest counterclockwise angle from the incoming direction.
                var chosen = candidates
                    .OrderBy(n => CcwAngle(incoming, n.Coords - next.Coords))
                    .First();

                current = next;
                next = chosen;
            }

            return result;
        }
    }

    protected static double CcwAngle(Int2 from, Int2 to)
    {
        var cross = (double)from.X * to.Z - (double)from.Z * to.X;
        var dot = (double)from.X * to.X + (double)from.Z * to.Z;
        var angle = Math.Atan2(cross, dot);
        return angle < 0 ? angle + 2 * Math.PI : angle;
    }
}

/// <summary>
/// 3D graph for networks spanning a volume (pipes, wiring, conduit runs).
/// No exterior boundary ring — enclosing a volume is not expressible as a ring.
/// </summary>
public class Graph3D<TEntity> : Graph<TEntity, Int3> where TEntity : class
{
}
