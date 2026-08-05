using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
namespace Momoka.Home.Tests.Models.Serialization;

public class MideaVerifyTests
{
    [Fact]
    public void Loads_NewFormat()
    {
        var factory = new EntityTemplateFactory();
        factory.Register("entity.appliance.air_conditioner", new EntityTemplate
        {
            Key = new Key("entity", "appliance.air_conditioner"),
            Volume = new Box3D { SizeX = 2, SizeY = 2, SizeZ = 2 }
        });

        var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "Documentation", "midea", "air_conditioner.ac_1523.json");
        Assert.True(File.Exists(path), $"file not found: {path}");

        var entity = factory.Load(path);

        var box = Assert.IsType<Box3D>(entity.Volume);
        Assert.Equal(1, box.SizeX);
        Assert.Equal(2, box.SizeY);
        Assert.Equal(1, box.SizeZ);
        Assert.Equal("disabled", entity.GetValue("ai_mode"));
        Assert.Equal("texture.midea.air_conditioner.ac_1523", entity.GetValue("texture"));
        Assert.False(entity.GetValue("clean_mode") is true);
    }
}
