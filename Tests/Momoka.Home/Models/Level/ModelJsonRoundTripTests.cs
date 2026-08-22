using System.Text.Json;
using Xunit;
using Momoka.Home.Data;
using Momoka.Home;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels;
using Momoka.Home.Runtime.Protocol;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Tests.Models.Level;

/// <summary>
/// 模型类型在 <see cref="Settings.JsonSerialization"/> 下的 JSON 往返——协议叶载荷
/// 直接复用模型类型（路线 B）的前提。JSON 协议 + STJ 注册表多态，无需独立 DTO。
/// </summary>
public class ModelJsonRoundTripTests
{
    private static T RoundTrip<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Settings.JsonSerialization), Settings.JsonSerialization)!;

    [Fact]
    public void Entity_RoundTrips_FullPayload()
    {
        var floor = Scenes.Floor(5, 1, 5);
        floor.Transform = new Transform(new Float3(50, 0, 50), new Rotation(30, 0, 0));
        floor.AddProperty(new StringProperty(Property.Texture, ""));
        floor.SetValue(Property.Texture, "momoka:wood");

        var back = RoundTrip(floor);

        Assert.Equal(floor.Id, back.Id);
        Assert.Equal(floor.Key, back.Key);
        Assert.Equal(new Float3(50, 0, 50), back.Transform.Position);
        Assert.Equal(30, back.Transform.Rotation.Yaw);
        Assert.Equal("momoka:wood", back.GetValue<string>(Property.Texture));
        Assert.True(back.IsImmutable());

        var surface = Assert.IsType<PlacementLayoutSource>(Assert.Single(back.Components));
        Assert.Equal(new Int2(5, 5), surface.Layout.Size);
        Assert.True(surface.Layout[new Int2(3, 3)]);
        Assert.Equal(Rotation.Up, surface.Transform.Rotation);
    }

    private static readonly string[] AcFanValues = { "ac", "fan" };

    [Fact]
    public void Entity_RoundTrips_EnumLiteralAndComposite()
    {
        var door = Scenes.Box("door", 1, 20, 2);
        door.AddProperty(
            new BooleanProperty(Property.IsImmutable, true),
            new BooleanProperty(Property.IsOpen, true),
            EnumProperty.Create(Property.RotationAlignment, RotationAlignment.Vertical),
            new LiteralProperty("device_type", AcFanValues, "ac"));
        var doorBack = RoundTrip(door);
        Assert.Equal(RotationAlignment.Vertical, doorBack.GetValue<RotationAlignment>(Property.RotationAlignment));
        Assert.Equal("ac", doorBack.GetValue<string>("device_type"));

        var wall = Scenes.Box("wall", 1, 29, 8);
        var punched = VolumePunch.Punch(wall.Volume, Int3.Zero, new Int3(0, 1, 4), new Int3(1, 20, 2))!;
        wall.Volume = punched;
        var wallBack = RoundTrip(wall);
        Assert.Equal(4, Assert.IsType<Composite>(wallBack.Volume).Children.Count);
    }

    [Fact]
    public void Key_And_Transform_RoundTrip()
    {
        var key = new Key("momoka", "washing_machine");
        Assert.Equal(key, RoundTrip(key));
        var transform = new Transform(new Float3(50, 10, 40), new Rotation(90, 0, 0));
        var back = RoundTrip(transform);
        Assert.Equal(new Float3(50, 10, 40), back.Position);
        Assert.Equal(90, back.Rotation.Yaw);
    }

    [Fact]
    public void Component_Types_RoundTrip()
    {
        var target = new CommandTarget { SourceId = "ac-1" };
        var data = new DataSource { SourceId = "sensor-1" };
        Assert.Equal("ac-1", RoundTrip(target).SourceId);
        Assert.Equal("sensor-1", RoundTrip(data).SourceId);
    }

    [Fact]
    public void TemplateCatalogEntry_RoundTrips_WithKey()
    {
        var entry = new TemplateCatalogEntry
        {
            Key = "momoka:table",
            Volume = new Box { SizeX = 2, SizeY = 1, SizeZ = 2 },
            Properties = new List<Property> { new BooleanProperty(Property.IsImmutable, true) },
            Components = new List<string> { "placement_layout" },
        };
        var back = RoundTrip(entry);
        Assert.Equal("momoka:table", back.Key);
        var volume = Assert.IsType<Box>(back.Volume);
        Assert.Equal(2, volume.SizeX);
        Assert.Equal(1, volume.SizeY);
        Assert.Equal(2, volume.SizeZ);
        Assert.Equal("placement_layout", Assert.Single(back.Components));
    }
}
