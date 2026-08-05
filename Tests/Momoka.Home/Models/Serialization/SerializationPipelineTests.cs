using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
using Momoka.Home.States;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// Config pipeline: JSON config file → typed EntityTemplate (key from path,
/// "extends" resolved as mixin composition) → Entity&lt;Int3&gt;.
/// </summary>
public class SerializationPipelineTests
{
    private static readonly string[] AiModeValues = ["disabled", "skyscreen_mode", "no_direct_wind_mode", "fast_cooling_mode"];

    private const string AcConfigJson = """
    {
        "shape": { "kind": "box", "size": { "x": 1, "y": 2, "z": 1 } },
        "properties": [
            { "key": "ai_mode", "type": "literals", "values": ["disabled", "skyscreen_mode", "no_direct_wind_mode", "fast_cooling_mode"], "value": "disabled" },
            { "key": "clean_mode", "type": "boolean" },
            { "key": "texture", "type": "texture", "value": "texture.midea.air_conditioner.ac_1523" }
        ]
    }
    """;

    private const string AcWithExtendsConfigJson = """
    {
        "extends": [ "entity.appliance.air_conditioner" ],
        "shape": { "kind": "box", "size": { "x": 1, "y": 2, "z": 1 } },
        "properties": [
            { "key": "ai_mode", "type": "literals", "values": ["disabled", "skyscreen_mode", "no_direct_wind_mode", "fast_cooling_mode"], "value": "disabled" }
        ]
    }
    """;

    [Fact]
    public void Load_ConfigFile_BuildsTypedEntity()
    {
        var path = WriteTempConfig("midea", "air_conditioner.ac_1523.json", AcConfigJson);
        var factory = new EntityTemplateFactory();

        var entity = factory.Load(path);

        var spatial = Assert.IsType<Entity<Int3>>(entity);
        Assert.Equal(new Key("midea", "air_conditioner.ac_1523"), spatial.Key);

        var box = Assert.IsType<Box3D>(spatial.Volume);
        Assert.Equal(1, box.SizeX);
        Assert.Equal(2, box.SizeY);
        Assert.Equal(1, box.SizeZ);

        Assert.Equal("disabled", spatial.GetValue("ai_mode"));
        Assert.False(spatial.GetValue("clean_mode") is true);
        Assert.Equal("texture.midea.air_conditioner.ac_1523", spatial.GetValue("texture"));
    }

    [Fact]
    public void LiteralsProperty_CarriesClosedValueSet()
    {
        var path = WriteTempConfig("midea", "air_conditioner.ac_1523.json", AcConfigJson);
        var factory = new EntityTemplateFactory();

        var entity = factory.Load(path);
        var schema = entity.GetSchema();

        var aiMode = schema.Single(s => (string)s["name"]! == "ai_mode");
        Assert.Equal(AiModeValues, aiMode["validValues"]);
    }

    [Fact]
    public void Extends_MergesMixinPropertiesAndShape()
    {
        var factory = new EntityTemplateFactory();
        factory.Register("entity.appliance.air_conditioner", new EntityTemplate
        {
            Key = new Key("entity", "appliance.air_conditioner"),
            Volume = new Box3D { SizeX = 3, SizeY = 2, SizeZ = 2 },
            Properties = new List<Property> { new BooleanProperty("power") }
        });

        var path = WriteTempConfig("midea", "air_conditioner.ac_1523.json", AcWithExtendsConfigJson);
        var entity = factory.Load(path);

        // mixin shape overridden by the child's own; the mixin's "power" property is inherited
        var box = Assert.IsType<Box3D>(entity.Volume);
        Assert.Equal(1, box.SizeX);
        Assert.Equal("disabled", entity.GetValue("ai_mode"));
        Assert.False(entity.GetValue("power") is true);
        Assert.Contains(entity.GetSchema(), s => (string)s["name"]! == "power");
    }

    [Fact]
    public void Extends_MultipleMixins_LaterOverridesEarlier()
    {
        var factory = new EntityTemplateFactory();
        factory.Register("a", new EntityTemplate
        {
            Key = new Key("a"),
            Volume = new Box3D { SizeX = 2, SizeY = 1, SizeZ = 1 },
            Properties = new List<Property> { new IntProperty("level", 1) }
        });
        factory.Register("b", new EntityTemplate
        {
            Key = new Key("b"),
            Properties = new List<Property> { new IntProperty("level", 2) }
        });

        var path = WriteTempConfig("brand", "thing.json", """
        {
            "extends": [ "a", "b" ],
            "properties": [ { "key": "level", "type": "int", "value": 3 } ]
        }
        """);
        var entity = factory.Load(path);

        // shape from the only shape-providing mixin; the child's own config overrides "level"
        Assert.IsType<Box3D>(entity.Volume);
        Assert.Equal(3, entity.GetValue("level"));
    }

    [Fact]
    public void Extends_UnknownMixin_Throws()
    {
        var factory = new EntityTemplateFactory();
        var path = WriteTempConfig("brand", "thing.json", """
        { "extends": [ "does.not.exist" ] }
        """);

        Assert.Throws<InvalidDataException>(() => factory.LoadTemplate(path));
    }

    [Fact]
    public void LoadedTemplate_IsRegisteredForFurtherComposition()
    {
        var factory = new EntityTemplateFactory();
        var path = WriteTempConfig("brand", "series.json", """
        {
            "properties": [ { "key": "series", "type": "string", "value": "AC-1" } ]
        }
        """);

        factory.LoadTemplate(path);

        var registered = factory.Resolve("brand:series");
        Assert.NotNull(registered);
        Assert.Equal("AC-1", registered!.Properties!.Single(p => p.Name == "series").Value);
    }

    [Fact]
    public void Save_RoundTripsTemplate()
    {
        var factory = new EntityTemplateFactory();
        var template = new EntityTemplate
        {
            Key = new Key("brand", "thing"),
            Volume = new Box3D { SizeX = 2, SizeY = 3, SizeZ = 4 },
            Properties = new List<Property> { new BooleanProperty("on", true) { Value = true } }
        };

        var path = Path.Combine(Path.GetTempPath(), "momoka_test", "roundtrip.json");
        factory.Save(path, template);
        var loaded = factory.LoadTemplate(path);

        // Key is path-derived (never serialized); content round-trips.
        Assert.Equal(EntityTemplateFactory.KeyFromPath(path), loaded.Key);
        var box = Assert.IsType<Box3D>(loaded.Volume);
        Assert.Equal(2, box.SizeX);
        Assert.True(loaded.Properties!.Single(p => p.Name == "on").Value is true);
    }

    [Fact]
    public void KeyFromPath_DerivesNamespaceAndPath()
    {
        var key = EntityTemplateFactory.KeyFromPath("/data/midea/air_conditioner.ac_1523.json");
        Assert.Equal(new Key("midea", "air_conditioner.ac_1523"), key);
    }

    private static string WriteTempConfig(string folder, string file, string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "momoka_test", folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, file);
        File.WriteAllText(path, json);
        return path;
    }
}
