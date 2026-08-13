using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// Palette direct serialization: Palette&lt;T&gt; JSON payloads (values, or entity
/// Guid references) and PackedBitStorage byte BLOBs — the palette_json / bits
/// / data pieces the Sqlite voxel layer will store as chunk_sections columns.
/// </summary>
public class PaletteSerializationTests
{
    private static string Compact(string json) =>
        JToken.Parse(json).ToString(Formatting.None);

    [Fact]
    public void Palette_NonEntity_SerializesToValueArray()
    {
        var palette = new Palette<int>();
        palette.IdFor(5);
        palette.IdFor(7);

        var json = Compact(JsonConvert.SerializeObject(palette, Settings.JsonSerialization));

        Assert.Equal("[5,7]", json); // id 0 空槽被跳过
    }

    [Fact]
    public void Palette_Entity_SerializesToGuidArray()
    {
        var wall = new Entity { Key = "wall" };
        var floor = new Entity { Key = "floor" };
        var palette = new Palette<Entity>();
        palette.IdFor(wall);
        palette.IdFor(floor);

        var json = Compact(JsonConvert.SerializeObject(palette, Settings.JsonSerialization));

        // 与 LayoutChunkCodec 相同的 Guid 引用约定：载荷不依赖实体表顺序
        Assert.Equal($"[{JsonConvert.ToString(wall.Id)},{JsonConvert.ToString(floor.Id)}]", json);
    }

    [Fact]
    public void Palette_Deserialize_ThrowsNotSupported()
    {
        // Entity 引用需实体表解析，读由存储层用 Palette.FromValues 完成
        Assert.Throws<NotSupportedException>(() =>
            JsonConvert.DeserializeObject<Palette<int>>("[1,2]"));
    }

    [Fact]
    public void PackedBitStorage_ToBytesFromBytes_RoundTrips()
    {
        var storage = new PackedBitStorage(4096, 4); // 16³ section，4 位宽
        storage.Set(0, 1);
        storage.Set(1234, 5);
        storage.Set(4095, 2);

        var restored = PackedBitStorage.FromBytes(storage.Size, storage.Bits, storage.ToBytes());

        Assert.Equal(storage.Bits, restored.Bits);
        Assert.Equal(storage.Size, restored.Size);
        for (var i = 0; i < storage.Size; i++)
            Assert.Equal(storage.Get(i), restored.Get(i));
    }

    [Fact]
    public void PackedBitStorage_ToBytes_MatchesChunkCodecWordLayout()
    {
        var storage = new PackedBitStorage(4096, 4);
        storage.Set(100, 3);
        storage.Set(2000, 7);

        // LayoutChunkCodec 用 BinaryWriter.Write(ulong) 逐词写——little-endian，与 ToBytes 同布局
        using var expected = new MemoryStream();
        using var writer = new BinaryWriter(expected);
        foreach (var word in storage.Data)
            writer.Write(word);

        Assert.Equal(expected.ToArray(), storage.ToBytes());
    }

    [Fact]
    public void PalettedContainer_ThreeColumnPayload_RoundTrips()
    {
        var size = new Int3(16, 16, 16);
        var wall = new Entity { Key = "wall" };
        var floor = new Entity { Key = "floor" };
        var container = new PalettedContainer<Int3, Entity>(
            new Palette<Entity>.Int3ChunkStrategy(size, 4));
        container[new Int3(0, 0, 0)] = wall;
        container[new Int3(1, 2, 3)] = floor;
        container[new Int3(15, 15, 15)] = wall;

        // 拆成三列载荷：palette_json + bits + data BLOB
        var paletteJson = Compact(JsonConvert.SerializeObject(container.Palette, Settings.JsonSerialization));
        var bits = container.Storage.Bits;
        var data = container.Storage.ToBytes();

        // 存储层恢复：palette 的 Guid 引用解析到实体表
        var ids = JsonConvert.DeserializeObject<Guid[]>(paletteJson)!;
        var byId = new[] { wall, floor }.ToDictionary(e => e.Id);
        var restored = new PalettedContainer<Int3, Entity>(
            new Palette<Entity>.Int3ChunkStrategy(size, 4),
            Palette<Entity>.FromValues(ids.Select(id => byId[id]).ToList()),
            PackedBitStorage.FromBytes(container.Storage.Size, bits, data));

        Assert.Same(wall, restored[new Int3(0, 0, 0)]);
        Assert.Same(floor, restored[new Int3(1, 2, 3)]);
        Assert.Same(wall, restored[new Int3(15, 15, 15)]);
        Assert.Null(restored[new Int3(2, 2, 2)]);
    }
}
