using Xunit;
using Momoka.Home.Algorithms;
using Momoka.Home.Entities;
using Momoka.Home.Properties;
namespace Momoka.Home.Tests.Models.Algorithms;

/// <summary>
/// 阻挡档位判定（<see cref="OcclusionExtensions.Blocks{T}"/>）：四档位各自
/// 的选择性阻挡语义，以及空格（null）不构成阻挡。
/// </summary>
public class OcclusionTests
{
    private static Entity Box(string path) => new() { Key = path };

    private static Entity Immutable(string path)
    {
        var entity = Box(path);
        entity.AddProperties(new[] { new BooleanProperty(Property.IsImmutable, true) });
        return entity;
    }

    private static Entity Transparent(string path)
    {
        var entity = Box(path);
        entity.AddProperties(new[] { new BooleanProperty(Property.IsTransparent, true) });
        return entity;
    }

    [Fact]
    public void None_NeverBlocks()
    {
        Assert.False(Occlusion.None.Blocks(Immutable("wall")));
        Assert.False(Occlusion.None.Blocks(Box("chair")));
        Assert.False(Occlusion.None.Blocks((Entity?)null));
    }

    [Fact]
    public void OnlyImmutable_BlocksImmovableOnly()
    {
        Assert.True(Occlusion.OnlyImmutable.Blocks(Immutable("wall")));
        Assert.False(Occlusion.OnlyImmutable.Blocks(Box("chair")));
        Assert.False(Occlusion.OnlyImmutable.Blocks(Transparent("glass")));
    }

    [Fact]
    public void OnlyNonTransparent_BlocksOpaqueOnly()
    {
        Assert.True(Occlusion.OnlyNonTransparent.Blocks(Box("wall")));        // 默认不透明
        Assert.True(Occlusion.OnlyNonTransparent.Blocks(Immutable("wall")));
        Assert.False(Occlusion.OnlyNonTransparent.Blocks(Transparent("glass")));
    }

    [Fact]
    public void Everything_AlwaysBlocks()
    {
        Assert.True(Occlusion.Everything.Blocks(Immutable("wall")));
        Assert.True(Occlusion.Everything.Blocks(Box("chair")));
        Assert.True(Occlusion.Everything.Blocks(Transparent("glass")));
    }

    [Fact]
    public void NullValue_NeverBlocks_ForAnyLevel()
    {
        Assert.False(Occlusion.None.Blocks((Entity?)null));
        Assert.False(Occlusion.OnlyImmutable.Blocks((Entity?)null));
        Assert.False(Occlusion.OnlyNonTransparent.Blocks((Entity?)null));
        Assert.False(Occlusion.Everything.Blocks((Entity?)null));
    }
}
