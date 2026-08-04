using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// EntityFactory dispatches by the template's type name to a registered
/// constructor and stamps the template key onto the produced entity.
/// </summary>
public class EntityFactoryTests
{
    private sealed class DummyEntity : VoxelEntity
    {
    }

    [Fact]
    public void Register_AndCreate_BuildsTypedEntityWithTemplateKey()
    {
        var factory = new EntityFactory();
        factory.Register<DummyEntity>("dummy");

        var template = new EntityTemplate(new Key("test", "dummy"), "dummy");

        var entity = Assert.IsType<DummyEntity>(factory.Create(template));
        Assert.Equal(new Key("test", "dummy"), entity.Key);
    }

    [Fact]
    public void Create_UnregisteredType_Throws()
    {
        var factory = new EntityFactory();
        var template = new EntityTemplate(new Key("test", "ghost"), "ghost");

        Assert.Throws<InvalidOperationException>(() => factory.Create(template));
        Assert.False(factory.TryCreate(template, out var entity));
        Assert.Null(entity);
    }

    [Fact]
    public void TryCreate_RegisteredType_ReturnsTrue()
    {
        var factory = new EntityFactory();
        factory.Register<DummyEntity>("dummy");

        var template = new EntityTemplate(new Key("dummy"), "dummy");

        Assert.True(factory.TryCreate(template, out var entity));
        Assert.IsType<DummyEntity>(entity);
        Assert.Equal(new Key("dummy"), entity.Key);
    }
}
