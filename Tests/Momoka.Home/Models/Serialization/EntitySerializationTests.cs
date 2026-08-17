using Xunit;
using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
using Momoka.Home.Data.Json;
using Newtonsoft.Json;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// An <see cref="Entity"/> serializes directly via <see cref="Settings.JsonSerialization"/>:
/// key, position, volume, per-instance properties and components (incl.
/// placement surfaces) round-trip so a save is self-contained and loads
/// without templates.
/// </summary>
public class EntitySerializationTests
{
    [Fact]
    public void Entity_RoundTrips_FullState()
    {
        var entity = new Entity
        {
            Key = new Key("midea", "air_conditioner.ac_1523"),
            Pos = new Position(new Float3(20, 10, 30)),
            Volume = new Box3D { SizeX = 1, SizeY = 2, SizeZ = 1 },
        };
        entity.AddProperties(new Property[]
        {
            new BooleanProperty(Property.IsImmutable, true),
            new BooleanProperty(Property.IsOpen, false),
            new IntProperty("level", 3),
        });
        var surface = new GridLayout<bool>(new Int2(2, 2), new Int3(0, 1, 0)) { Direction = Int3.Up };
        surface[new Int2(0, 0)] = true;
        surface[new Int2(1, 1)] = true;
        entity.AddComponent(new PlacementLayoutSource { SourceId = "ac-1", Layout = surface });
        entity.AddComponent(new DataSource(DataSourceType.Temperature) { Value = 24.5f });
        entity.AddComponent(new CommandTarget { Commands = "[\"turn_on\",\"turn_off\"]" });

        var json = JsonConvert.SerializeObject(entity, Settings.JsonSerialization);
        var loaded = JsonConvert.DeserializeObject<Entity>(json, Settings.JsonSerialization)!;

        Assert.Equal(entity.Id, loaded.Id);
        Assert.Equal(entity.Key, loaded.Key);
        Assert.Equal(entity.Pos, loaded.Pos);

        var box = Assert.IsType<Box3D>(loaded.Volume);
        Assert.Equal(1, box.SizeX);

        Assert.Equal(3, loaded.Properties.Count);
        Assert.True(loaded.GetValue<bool>(Property.IsImmutable));
        Assert.False(loaded.GetValue<bool>(Property.IsOpen));
        Assert.Equal(3, loaded.GetValue<int>("level"));

        var surfaceLoaded = Assert.Single(loaded.GetComponents<PlacementLayoutSource>());
        Assert.Equal("ac-1", surfaceLoaded.SourceId);
        Assert.NotNull(surfaceLoaded.Layout);
        Assert.Equal(new Int2(2, 2), surfaceLoaded.Layout!.Size);
        Assert.Equal(Int3.Up, surfaceLoaded.Layout.Direction);
        Assert.True(surfaceLoaded.Layout[new Int2(0, 0)]);
        Assert.True(surfaceLoaded.Layout[new Int2(1, 1)]);
        Assert.False(surfaceLoaded.Layout[new Int2(0, 1)]);

        var sensor = Assert.Single(loaded.GetComponents<DataSource>());
        Assert.Equal(DataSourceType.Temperature, sensor.Type);
        Assert.Equal(24.5f, sensor.Value);

        var target = Assert.Single(loaded.GetComponents<CommandTarget>());
        Assert.Equal("[\"turn_on\",\"turn_off\"]", target.Commands);
    }

}
