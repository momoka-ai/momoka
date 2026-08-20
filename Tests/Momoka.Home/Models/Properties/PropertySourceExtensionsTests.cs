using Xunit;
using Momoka.Home.Entities;
namespace Momoka.Home.Tests.Models.Properties;

/// <summary>
/// IPropertySource 扩展（名称优先的属性表操作）：查询 / 赋值 / 事件 / schema。
/// 宿主统一用 Entity。
/// </summary>
public class PropertySourceExtensionsTests
{
    private static Entity With(params Property[] properties)
    {
        var entity = new Entity();
        entity.AddProperties(properties);
        return entity;
    }

    [Fact]
    public void AddProperty_AddsToTheTable()
    {
        var entity = new Entity();
        entity.AddProperty(new BooleanProperty("on", true));

        Assert.Single(entity.Properties);
        Assert.Equal("on", entity.Properties[0].Name);
    }

    [Fact]
    public void AddProperties_AddsAll()
    {
        var entity = new Entity();
        entity.AddProperties(new Property[]
        {
            new BooleanProperty("on", true),
            new IntProperty("level", 0),
        });

        Assert.Equal(2, entity.Properties.Count);
    }

    [Fact]
    public void GetValue_Existing_ReturnsBoxedValue()
    {
        var entity = With(new IntProperty("level", 0) { Value = 3 });

        Assert.Equal(3, entity.GetValue("level"));
    }

    [Fact]
    public void GetValue_Missing_ThrowsKeyNotFound()
    {
        var entity = new Entity();

        Assert.Throws<KeyNotFoundException>(() => entity.GetValue("nope"));
    }

    [Fact]
    public void GetValue_Typed_ReturnsValue()
    {
        var entity = With(new IntProperty("level", 0) { Value = 3 });

        Assert.Equal(3, entity.GetValue<int>("level"));
    }

    [Fact]
    public void GetValue_Typed_Absent_ReturnsDefault()
    {
        Assert.Equal(0, new Entity().GetValue<int>("nope"));
    }

    [Fact]
    public void GetValue_Typed_TypeMismatch_ReturnsDefault()
    {
        var entity = With(new BooleanProperty("on", true));

        Assert.Equal(0, entity.GetValue<int>("on"));
        Assert.Equal(0.0f, entity.GetValue<float>("on"));
    }

    [Fact]
    public void TryGetValue_Existing_ReturnsTrueAndEffectiveValue()
    {
        var entity = With(new IntProperty("level", 0) { Value = 3 });

        Assert.True(entity.TryGetValue("level", out var value));
        Assert.Equal(3, value);
    }

    [Fact]
    public void TryGetValue_Unset_ReturnsTheDefault()
    {
        var entity = With(new IntProperty("level", 5));

        Assert.True(entity.TryGetValue("level", out var value));
        Assert.Equal(5, value);
    }

    [Fact]
    public void TryGetValue_Missing_ReturnsFalse()
    {
        Assert.False(new Entity().TryGetValue("nope", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void SetValue_UpdatesValue_AndRaisesEvent()
    {
        var entity = With(new IntProperty("level", 0));
        var property = (IntProperty)entity.Properties[0];
        PropertyValueChangedEventArgs? args = null;
        var raised = 0;
        entity.PropertyValueChanged += (_, e) => { raised++; args = e; };

        entity.SetValue("level", 42);

        Assert.Equal(42, property.Value);
        Assert.Equal(1, raised);
        Assert.Same(property, args!.Property);
        Assert.Equal(42, args.NewValue);
    }

    [Fact]
    public void SetValue_Missing_Throws_WithoutEvent()
    {
        var entity = new Entity();
        var raised = 0;
        entity.PropertyValueChanged += (_, _) => raised++;

        Assert.Throws<KeyNotFoundException>(() => entity.SetValue("nope", 1));
        Assert.Equal(0, raised);
    }

    [Fact]
    public void ClearValue_ResetsToUnset_AndRaisesEvent()
    {
        var entity = With(new StringProperty("series", "default"));
        var property = (StringProperty)entity.Properties[0];
        property.Value = "overridden";
        PropertyValueChangedEventArgs? args = null;
        entity.PropertyValueChanged += (_, e) => args = e;

        entity.ClearValue("series");

        Assert.Equal("default", property.Value);
        Assert.Same(property, args!.Property);
        Assert.Equal("default", args.NewValue);
    }

    [Fact]
    public void ClearValue_Missing_IsNoOp()
    {
        var entity = new Entity();
        var raised = 0;
        entity.PropertyValueChanged += (_, _) => raised++;

        entity.ClearValue("nope");

        Assert.Equal(0, raised);
    }

    [Fact]
    public void IsImmutable_True_WhenMarked()
    {
        var entity = With(new BooleanProperty(Property.IsImmutable, true));

        Assert.True(entity.IsImmutable());
    }

    [Fact]
    public void IsImmutable_False_WhenAbsentOrFalse()
    {
        Assert.False(new Entity().IsImmutable());
        Assert.False(With(new BooleanProperty(Property.IsImmutable, false)).IsImmutable());
    }

    [Fact]
    public void IsImmutable_NullSource_IsFalse()
    {
        Assert.False(((Entity?)null).IsImmutable());
    }

    [Fact]
    public void GetSchema_ReturnsOneEntryPerProperty()
    {
        var entity = With(
            new BooleanProperty("on", true),
            new IntProperty("level", 0));

        var schema = entity.GetSchema();

        Assert.Equal(2, schema.Count);
        Assert.Equal("on", schema[0]["name"]);
        Assert.Equal("level", schema[1]["name"]);
    }
}
