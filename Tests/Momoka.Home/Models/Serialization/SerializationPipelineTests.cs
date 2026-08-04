using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// Config pipeline: JSON config file → typed EntityTemplate (key from path,
/// "class" resolved against the registry with inheritance + merge) → entity.
/// </summary>
public class SerializationPipelineTests
{
    private static readonly string[] AiModeValues = ["disabled", "skyscreen_mode", "no_direct_wind_mode", "fast_cooling_mode"];

    private const string ConfigJson = """
    {
        "class": "entity.appliance.air_conditioner",
        "shape": { "kind": "box", "size": { "x": 1, "y": 2, "z": 1 } },
        "properties": [
            { "key": "ai_mode", "type": "literals", "values": ["disabled", "skyscreen_mode", "no_direct_wind_mode", "fast_cooling_mode"], "value": "disabled" },
            { "key": "clean_mode", "type": "boolean" },
            { "key": "texture", "type": "texture", "value": "texture.midea.air_conditioner.ac_1523" }
        ],
        "components": [ "" ]
    }
    """;

    [Fact]
    public void Load_ConfigFile_BuildsTypedEntity()
    {
        var path = WriteTempConfig("midea", "air_conditioner.ac_1523.json", ConfigJson);
        var loader = new EntityConfigLoader();

        var entity = loader.Load(path);

        var templateEntity = Assert.IsType<TemplateEntity>(entity);
        Assert.Equal(new Key("midea", "air_conditioner.ac_1523"), templateEntity.Key);
        Assert.Equal("entity.appliance.air_conditioner", templateEntity.Template.Class);

        var box = Assert.IsType<BoxShape>(templateEntity.Shape);
        Assert.Equal(1, box.SizeX);
        Assert.Equal(2, box.SizeY);
        Assert.Equal(1, box.SizeZ);

        Assert.Equal("disabled", templateEntity.GetValue("ai_mode"));
        Assert.False(templateEntity.GetValue("clean_mode") is true);
        Assert.Equal("texture.midea.air_conditioner.ac_1523", templateEntity.GetValue("texture"));
    }

    [Fact]
    public void LiteralsProperty_CarriesClosedValueSet()
    {
        var path = WriteTempConfig("midea", "air_conditioner.ac_1523.json", ConfigJson);
        var loader = new EntityConfigLoader();

        var entity = (TemplateEntity)loader.Load(path);
        var schema = entity.GetSchema();

        var aiMode = schema.Single(s => (string)s["name"]! == "ai_mode");
        Assert.Equal(AiModeValues, aiMode["validValues"]);
    }

    [Fact]
    public void Load_InheritsParentTemplate_AndMergesProperties()
    {
        var loader = new EntityConfigLoader();
        // Pre-register a base type: a generic air conditioner carrying a shared property.
        var baseTemplate = new EntityTemplate
        {
            Key = new Key("entity", "appliance.air_conditioner"),
            Class = "voxelentity",
            Properties = new List<Property>
            {
                new BooleanProperty("power", new Key("entity.appliance.air_conditioner"))
            }
        };
        loader.Registry.Register("entity.appliance.air_conditioner", baseTemplate);

        var childPath = WriteTempConfig("midea", "air_conditioner.ac_1523.json", ConfigJson);
        var entity = (TemplateEntity)loader.Load(childPath);

        // Child's own properties load, and the parent's "power" property is inherited.
        Assert.Equal("disabled", entity.GetValue("ai_mode"));
        Assert.False(entity.GetValue("power") is true);
        Assert.Contains(entity.Template.Properties!, p => p.Name == "power");
    }

    [Fact]
    public void LoadedTemplate_IsRegisteredForFurtherInheritance()
    {
        var loader = new EntityConfigLoader();
        var path = WriteTempConfig("brand", "series.json", """
        {
            "class": "voxelentity",
            "properties": [ { "key": "series", "type": "string", "value": "AC-1" } ]
        }
        """);

        loader.LoadTemplate(path);

        var registered = loader.Registry.Resolve("brand:series");
        Assert.NotNull(registered);
        Assert.Equal("voxelentity", registered!.Class);
    }

    [Fact]
    public void KeyFromPath_DerivesNamespaceAndPath()
    {
        var key = EntityConfigLoader.KeyFromPath("/data/midea/air_conditioner.ac_1523.json");
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
