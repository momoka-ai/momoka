using System.Text.Json;
using Xunit;
using Momoka.Home.Data.Json;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// Direct deserialization of each property kind via the STJ
/// <see cref="KindTypeResolver"/> polymorphism: "type" resolves the concrete
/// class, "key"/"value"/"values" bind in one pass.
/// </summary>
public class JsonPropertyConverterTests
{
    private static readonly JsonSerializerOptions Settings = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        TypeInfoResolver = new KindTypeResolver(),
    };

    private static T RoundTrip<T>(T property) where T : Property =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(property, Settings), Settings)!;

    [Fact]
    public void Boolean_RoundTrips()
    {
        var prop = RoundTrip(new BooleanProperty("on", true) { Value = true });
        Assert.Equal("on", prop.Name);
        Assert.True(prop.Value);
    }

    [Fact]
    public void Boolean_Unset_DefaultsToDefaultValue()
    {
        var prop = new BooleanProperty("on", true);
        Assert.True(prop.Value);
        Assert.True(prop.BoxedValue is true);

        prop.Value = false;
        Assert.False(prop.Value);
        Assert.False(prop.BoxedValue is true);
    }

    [Fact]
    public void Int_RoundTrips()
    {
        var prop = RoundTrip(new IntProperty("level", 0) { Value = 3 });
        Assert.Equal(3, prop.Value);
    }

    [Fact]
    public void Float_RoundTrips()
    {
        var prop = RoundTrip(new FloatProperty("temp", 0f) { Value = 24.5f });
        Assert.Equal(24.5f, prop.Value);
    }

    [Fact]
    public void String_RoundTrips()
    {
        var prop = RoundTrip(new StringProperty("series", "") { Value = "AC-1" });
        Assert.Equal("AC-1", prop.Value);
    }

    [Fact]
    public void Literal_RoundTripsWithValidValues()
    {
        var values = new List<string> { "a", "b", "c" };
        var prop = RoundTrip(new LiteralProperty("mode", values) { Value = "b" });
        Assert.Equal("b", prop.Value);
        Assert.Equal(values, prop.ValidValues);
    }

    [Fact]
    public void Literal_RejectsValueOutsideSet()
    {
        var prop = new LiteralProperty("mode", new List<string> { "a", "b" });
        Assert.Throws<ArgumentException>(() => prop.Value = "c");
    }

    [Fact]
    public void Literal_ConfigWithInvalidValue_Throws()
    {
        var json = """{ "type": "literals", "key": "mode", "values": ["a", "b"], "value": "c" }""";
        // OnDeserialized 的跨字段校验异常由 STJ 原样传播。
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<Property>(json, Settings));
    }

    [Fact]
    public void Boolean_DeserializesFromConfigJson()
    {
        var prop = JsonSerializer.Deserialize<BooleanProperty>("""{ "type": "boolean", "key": "clean_mode", "value": true }""", Settings);
        Assert.Equal("clean_mode", prop!.Name);
        Assert.True(prop.Value);
    }
}
