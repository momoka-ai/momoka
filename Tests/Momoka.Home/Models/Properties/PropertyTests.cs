using Xunit;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Tests.Models.Properties;

/// <summary>
/// Property 基类与 6 种子类型的行为：默认值 / 赋值 / BoxedValue 契约、
/// Clone 的按实例独立性、Literal 封闭值集校验、Enum 词表与 schema 输出。
/// </summary>
public class PropertyTests
{
    private static readonly string[] Modes = { "a", "b" };
    private static readonly string[] TestModeNames = { "Off", "Low", "High" };

    [Fact]
    public void ValueType_ReflectsStorageType()
    {
        Assert.Equal(typeof(bool), new BooleanProperty("on").ValueType);
        Assert.Equal(typeof(int), new IntProperty("level").ValueType);
        Assert.Equal(typeof(float), new FloatProperty("temp").ValueType);
        Assert.Equal(typeof(string), new StringProperty("series").ValueType);
    }

    [Fact]
    public void UnsetValue_SeedsTheDefault()
    {
        Assert.True(new BooleanProperty("on", true).Value);
        Assert.Equal(5, new IntProperty("level", 5).Value);
        Assert.Equal("AC-1", new StringProperty("series", "AC-1").Value);
    }

    [Fact]
    public void Value_And_BoxedValue_StayInSync()
    {
        var prop = new IntProperty("level", 0);
        prop.Value = 42;

        Assert.Equal(42, prop.BoxedValue);
        prop.BoxedValue = 7;
        Assert.Equal(7, prop.Value);
    }

    [Fact]
    public void BoxedValue_Null_ResetsToDefault()
    {
        var prop = new StringProperty("series", "default");
        prop.Value = "overridden";

        prop.BoxedValue = null;

        Assert.Equal("default", prop.Value);
        Assert.Equal("default", prop.GetUnsetValue());
    }

    [Fact]
    public void BoxedValue_WrongType_Throws()
    {
        var prop = new IntProperty("level", 0);

        Assert.Throws<InvalidCastException>(() => prop.BoxedValue = "not an int");
    }

    [Fact]
    public void GetUnsetValue_ReturnsTheSeededDefault()
    {
        Assert.Equal(true, new BooleanProperty("on", true).GetUnsetValue());
        Assert.Equal(3, new IntProperty("level", 3).GetUnsetValue());
        Assert.Equal(24.5f, new FloatProperty("temp", 24.5f).GetUnsetValue());
    }

    [Fact]
    public void IsValidValue_AcceptsNull_And_CorrectTypeOnly()
    {
        var prop = new IntProperty("level", 0);

        Assert.True(prop.IsValidValue(null));
        Assert.True(prop.IsValidValue(5));
        Assert.False(prop.IsValidValue("5"));
    }

    [Fact]
    public void Clone_CopiesDefinition_AndValue()
    {
        var original = new IntProperty("level", 0) { Value = 9, IsReadOnly = true };
        var clone = (IntProperty)original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal("level", clone.Name);
        Assert.Equal(0, clone.UnsetValue);
        Assert.Equal(9, clone.Value);
        Assert.True(clone.IsReadOnly);
    }

    [Fact]
    public void Clone_IsPerInstance_ModifyingCloneDoesNotTouchOriginal()
    {
        var original = new IntProperty("level", 0) { Value = 1 };
        var clone = (IntProperty)original.Clone();

        clone.Value = 2;

        Assert.Equal(1, original.Value);
        Assert.Equal(2, clone.Value);
    }

    // ── LiteralProperty：封闭值集 ────────────────────────

    [Fact]
    public void Literal_AssignsValueFromClosedSet()
    {
        var prop = new LiteralProperty("mode", Modes);
        prop.Value = "b";

        Assert.Equal("b", prop.Value);
    }

    [Fact]
    public void Literal_RejectsValueOutsideSet()
    {
        var prop = new LiteralProperty("mode", Modes);

        Assert.Throws<ArgumentException>(() => prop.Value = "c");
    }

    [Fact]
    public void Literal_OnConfigLoaded_ValidatesCurrentValue()
    {
        // BoxedValue 不经 Value setter 校验，可构造"值不在封闭集"的非法状态
        var prop = new LiteralProperty("mode", Modes);
        prop.BoxedValue = "c";

        Assert.Throws<ArgumentException>(() => prop.OnConfigLoaded());
    }

    [Fact]
    public void Literal_Clone_PreservesValidValues()
    {
        var original = new LiteralProperty("mode", Modes) { Value = "b" };
        var clone = (LiteralProperty)original.Clone();

        Assert.Equal(Modes, clone.ValidValues);
        Assert.Equal("b", clone.Value);
        Assert.Throws<ArgumentException>(() => clone.Value = "z");
    }

    // ── EnumProperty ─────────────────────────────────────

    private enum TestMode { Off, Low, High }

    [Fact]
    public void Enum_GetValidValues_ListsEnumNames()
    {
        var prop = EnumProperty.Create<TestMode>("mode", TestMode.Off);

        Assert.Equal(TestModeNames, prop.GetValidValues());
    }

    [Fact]
    public void Enum_Value_StoresTypedEnum()
    {
        var prop = EnumProperty.Create<TestMode>("mode", TestMode.Off);
        prop.BoxedValue = TestMode.High;

        Assert.Equal(TestMode.High, prop.BoxedValue);
    }

    // ── ToSchema ─────────────────────────────────────────

    [Fact]
    public void ToSchema_ContainsIdentityAndDefault()
    {
        var schema = new IntProperty("level", 3).ToSchema();

        Assert.Equal("level", schema["name"]);
        Assert.Equal("integer", schema["type"]);
        Assert.Equal(3, schema["default"]);
        Assert.False((bool)schema["isReadOnly"]!);
    }

    [Fact]
    public void ToSchema_IncludesDescription_WhenSet()
    {
        var schema = new BooleanProperty("on", false, "Power switch").ToSchema();

        Assert.Equal("Power switch", schema["description"]);
    }

    [Fact]
    public void ToSchema_IncludesValidValues_ForLiteral()
    {
        var schema = new LiteralProperty("mode", Modes).ToSchema();

        Assert.Equal("literals", schema["type"]);
        Assert.Equal(Modes, schema["validValues"]);
    }

    [Fact]
    public void ToSchema_TypeNames_MatchConfigVocabulary()
    {
        Assert.Equal("boolean", new BooleanProperty("on").ToSchema()["type"]);
        Assert.Equal("integer", new IntProperty("level").ToSchema()["type"]);
        Assert.Equal("number", new FloatProperty("temp").ToSchema()["type"]);
        Assert.Equal("string", new StringProperty("series").ToSchema()["type"]);
        Assert.Equal("enum", EnumProperty.Create<TestMode>("mode", TestMode.Off).ToSchema()["type"]);
    }
}
