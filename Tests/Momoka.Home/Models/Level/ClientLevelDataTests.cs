using Momoka.Home.Runtime;
using Xunit;
using Momoka.Home;
using Momoka.Home.Levels;
using Momoka.Home.Runtime.Protocol;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Level;

/// <summary>客户端镜像：快照应用、增量应用、网格重建与服务器一致、脏区块推导、事件缺口重同步。</summary>
public class ClientLevelDataTests
{
    private static Entity Box(Guid id, string key, Int3 size, Float3 position) => new()
    {
        Id = id,
        Key = new Key(key),
        Volume = new Box3D { SizeX = size.X, SizeY = size.Y, SizeZ = size.Z },
        Transform = new Transform(position, Rotation.Identity),
    };

    private static SnapshotEvent Snapshot(Entity floor, params Entity[] placed)
    {
        var all = new[] { floor }.Concat(placed).ToArray();
        return new SnapshotEvent
        {
            Type = "house",
            Entities = all,
            PlacedEntityIds = all.Select(e => e.Id).ToArray(),
            TemplateVersion = "1",
            Version = 1,
        };
    }

    [Fact]
    public void ApplySnapshot_RebuildsGrid_AndQueries()
    {
        var client = new ClientLevelData();
        var floor = Box(Guid.NewGuid(), "floor", new Int3(5, 1, 5), new Float3(0, 0, 0));
        var table = Box(Guid.NewGuid(), "table", new Int3(1, 1, 1), new Float3(0, 10, 0));
        client.ApplySnapshot(Snapshot(floor, table));

        Assert.Equal(2, client.Placed.Count);
        Assert.Equal(2, client.Registry.Count);
        Assert.Same(floor, client.Grid[new Int3(0, 0, 0)]);
        Assert.Same(table, client.Grid[new Int3(0, 1, 0)]);
        Assert.Null(client.Grid[new Int3(0, 2, 0)]);
        Assert.Equal(1u, client.Version);

        // 只读查询面（IVoxelSource<Entity> 扩展）
        var hit = client.IsCollidedVolume(new Position(new Float3(0, 10, 0)), new Box3D { SizeX = 1, SizeY = 1, SizeZ = 1 });
        Assert.NotNull(hit);
        Assert.Same(table, hit!.Value.Hit);
    }

    [Fact]
    public void Apply_Incremental_AddedModifiedRemoved()
    {
        var client = new ClientLevelData();
        var floor = Box(Guid.NewGuid(), "floor", new Int3(5, 1, 5), new Float3(0, 0, 0));
        client.ApplySnapshot(Snapshot(floor));

        // added：放置桌子
        var tableId = Guid.NewGuid();
        var table = Box(tableId, "table", new Int3(1, 1, 1), new Float3(0, 10, 0));
        var dirtyAdded = client.Apply(new[] { new EntityDelta { Kind = "added", EntityId = tableId, Entity = table } }, 2);
        Assert.Same(table, client.Grid[new Int3(0, 1, 0)]);
        Assert.Contains(dirtyAdded, c => c == new Int2(0, 0));

        // modified：移动桌子（新载荷，同 Id）
        var moved = Box(tableId, "table", new Int3(1, 1, 1), new Float3(10, 10, 0));
        client.Apply(new[] { new EntityDelta { Kind = "modified", EntityId = tableId, Entity = moved } }, 3);
        Assert.Null(client.Grid[new Int3(0, 1, 0)]);
        Assert.Same(moved, client.Grid[new Int3(1, 1, 0)]);
        Assert.Same(moved, client.Registry[tableId]);

        // removed：移除桌子（只带 id）
        client.Apply(new[] { new EntityDelta { Kind = "removed", EntityId = tableId } }, 4);
        Assert.Null(client.Grid[new Int3(1, 1, 0)]);
        Assert.DoesNotContain(client.Placed, e => e.Id == tableId);
        Assert.False(client.Registry.ContainsKey(tableId));
        Assert.Equal(4u, client.Version);
    }

    [Fact]
    public void NeedsResync_DetectsGap()
    {
        var client = new ClientLevelData();
        var floor = Box(Guid.NewGuid(), "floor", new Int3(5, 1, 5), new Float3(0, 0, 0));
        client.ApplySnapshot(Snapshot(floor));

        Assert.False(client.NeedsResync(2));
        Assert.True(client.NeedsResync(3)); // 跳号 → 重同步
    }

    [Fact]
    public void RebuildGrid_MatchesServerOccupancy()
    {
        var client = new ClientLevelData();
        var floor = Box(Guid.NewGuid(), "floor", new Int3(5, 1, 5), new Float3(0, 0, 0));
        var table = Box(Guid.NewGuid(), "table", new Int3(2, 2, 2), new Float3(30, 10, 30));
        client.ApplySnapshot(Snapshot(floor, table));

        client.RebuildGrid();
        Assert.Same(table, client.Grid[new Int3(3, 1, 3)]);
        Assert.Same(table, client.Grid[new Int3(4, 2, 4)]);
        Assert.Same(floor, client.Grid[new Int3(2, 0, 2)]);
    }
}
