using System.Text.RegularExpressions;
using Momoka.Home;
namespace Momoka.Home.Primitives;

public readonly record struct Key : IComparable<Key>
{
    private const string DefaultNamespace = "momoka";
    private const string ValidCharsPattern = @"^[a-z0-9_.-]+$";

    public string Namespace { get; }
    public string Path { get; }

    public Key(string ns, string path)
    {
        if (!Regex.IsMatch(ns, ValidCharsPattern))
            throw new ArgumentException($"Invalid namespace: '{ns}'", nameof(ns));
        if (!Regex.IsMatch(path, ValidCharsPattern))
            throw new ArgumentException($"Invalid path: '{path}'", nameof(path));

        Namespace = ns;
        Path = path;
    }

    public Key(string path) : this(DefaultNamespace, path) { }

    public static Key Parse(string input)
    {
        var colon = input.IndexOf(':');
        return colon >= 0
            ? new Key(input[..colon], input[(colon + 1)..])
            : new Key(DefaultNamespace, input);
    }

    public static bool TryParse(string input, out Key result)
    {
        try { result = Parse(input); return true; }
        catch (ArgumentException) { result = default; return false; }
    }

    public int CompareTo(Key other)
    {
        var ns = string.CompareOrdinal(Namespace, other.Namespace);
        return ns != 0 ? ns : string.CompareOrdinal(Path, other.Path);
    }

    public static bool operator <(Key a, Key b) => a.CompareTo(b) < 0;
    public static bool operator <=(Key a, Key b) => a.CompareTo(b) <= 0;
    public static bool operator >(Key a, Key b) => a.CompareTo(b) > 0;
    public static bool operator >=(Key a, Key b) => a.CompareTo(b) >= 0;

    public override string ToString() => $"{Namespace}:{Path}";

    public static implicit operator Key(string path) => new(path);
}
