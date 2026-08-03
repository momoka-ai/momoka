using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
using Momoka.Home.Shapes;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// End-to-end config pipeline: a config JSON file → typed entity. Key is derived
/// from the file path (folder = namespace, filename = key path), the "typename"
/// resolves through the factory registry (with the TemplateEntity default), and
/// shape + properties are filled from the content table.
/// </summary>
public class SerializationPipelineTests
{
    private const string ConfigJson = """
    {
        "typename": "entity.appliance.air_conditioner",
        "version": 1,
        "content": {
            "shape": { "kind": "box", "size": { "x": 1, "y": 2, "z": 1 } },
            "properties": [
                { "key": "ai_mode", "type": "literals", "values": ["disabled", "skyscreen_mode", "no_direct_wind_mode", "fast_cooling_mode"], "value": "disabled" },
                { "key": "clean_mode", "type": "boolean" },
                { "key": "texture", "type": "texture", "value": "texture.midea.air_conditioner.ac_1523" }
            ],
            "components": [ "" ]
        }
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
        Assert.Equal("entity.appliance.air_conditioner", templateEntity.Template.TypeName);

        var box = Assert.IsType<BoxShape>(templateEntity.Shape);
        Assert.Equal(1, box.SizeX);
        Assert.Equal(2, box.SizeY);
        Assert.Equal(1, box.SizeZ);

        Assert.Equal("disabled", templateEntity.GetValue("ai_mode"));
        Assert.False((bool)templateEntity.GetValue("clean_mode"));
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
        Assert.Equal(
            new[] { "disabled", "skyscreen_mode", "no_direct_wind_mode", "fast_cooling_mode" },
            aiMode["validValues"]);
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
