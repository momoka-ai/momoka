using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Momoka.Home;
using Momoka.Home.Level;
using Momoka.Home.Level.Protocol;
using Momoka.Home.Primitives;
using Momoka.Home.Entities;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Tests.Models.Level;

/// <summary>JSON 协议：FrameRegistry 判别、Envelope 帧往返、请求 / 事件载荷形状。</summary>
public class ProtocolJsonTests
{
    private static Envelope RoundTrip(Envelope envelope) =>
        JsonConvert.DeserializeObject<Envelope>(JsonConvert.SerializeObject(envelope, Settings.JsonSerialization), Settings.JsonSerialization)!;

    [Fact]
    public void FrameRegistry_ResolvesRequest_FromTypeAndPayload()
    {
        var request = new PlaceEntityRequest
        {
            EntityId = Guid.NewGuid(),
            Position = new Float3(50, 10, 40),
            HostId = Guid.NewGuid(),
        };
        var envelope = Frames.RequestFrame("place_entity", 1, "req-1", request);

        var back = Assert.IsType<PlaceEntityRequest>(FrameRegistry.CreateRequest(envelope.Type, envelope.Payload));
        Assert.Equal(request.EntityId, back.EntityId);
        Assert.Equal(new Float3(50, 10, 40), back.Position);
        Assert.Equal(request.HostId, back.HostId);
    }

    [Fact]
    public void FrameRegistry_AllTypesDiscriminate()
    {
        var requests = new[] { "create_entity", "place_entity", "remove_entity", "move_entity", "rotate_entity", "set_property", "set_texture", "build_wall", "build_opening", "undo", "redo", "begin_edit", "end_edit", "save", "get_snapshot" };
        foreach (var type in requests)
        {
            Assert.NotNull(FrameRegistry.GetRequestType(type));
            Assert.NotNull(FrameRegistry.CreateRequest(type, null));
        }

        var events = new[] { "entity_created", "layout_changed", "save_completed", "snapshot", "error" };
        foreach (var type in events)
            Assert.NotNull(FrameRegistry.GetEventType(type));

        Assert.Null(FrameRegistry.GetRequestType("bogus"));
        Assert.Null(FrameRegistry.GetEventType("bogus"));
        Assert.Throws<InvalidDataException>(() => FrameRegistry.CreateRequest("bogus", null));
    }

    [Fact]
    public void Envelope_RoundTrips_Request()
    {
        var request = new MoveEntityRequest { EntityId = Guid.NewGuid(), Position = new Float3(10, 20, 30) };
        var envelope = Frames.RequestFrame("move_entity", 7, "req-1", request);

        var back = RoundTrip(envelope);
        Assert.Equal(Frames.Version, back.ProtocolVersion);
        Assert.Equal(7u, back.Seq);
        Assert.Equal("move_entity", back.Type);
        Assert.Equal("req-1", back.RequestId);
        Assert.Equal("json", back.PayloadFormat);

        var moved = Assert.IsType<MoveEntityRequest>(FrameRegistry.CreateRequest(back.Type, back.Payload));
        Assert.Equal(request.EntityId, moved.EntityId);
        Assert.Equal(new Float3(10, 20, 30), moved.Position);
    }

    [Fact]
    public void LayoutChangedEvent_RoundTrips_Delta()
    {
        var box = Scenes.Box("box", 1, 1, 1);
        var addedId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var evt = new LayoutChangedEvent
        {
            Version = 5,
            EntityDelta = new[]
            {
                new EntityDelta { Kind = "added", EntityId = addedId, Entity = box },
                new EntityDelta { Kind = "removed", EntityId = removedId },
            },
        };
        var envelope = Frames.EventFrame("layout_changed", 3, "req-2", evt);

        var back = RoundTrip(envelope);
        var parsed = Assert.IsType<LayoutChangedEvent>(FrameRegistry.CreateEvent(back.Type, back.Payload));
        Assert.Equal(5u, parsed.Version);
        Assert.Equal(2, parsed.EntityDelta.Length);
        Assert.Equal("added", parsed.EntityDelta[0].Kind);
        Assert.Equal(addedId, parsed.EntityDelta[0].EntityId);
        Assert.NotNull(parsed.EntityDelta[0].Entity);
        Assert.Equal("removed", parsed.EntityDelta[1].Kind);
        Assert.Equal(removedId, parsed.EntityDelta[1].EntityId);
        Assert.Null(parsed.EntityDelta[1].Entity);
    }

    [Fact]
    public void SnapshotEvent_RoundTrips_WithCatalog()
    {
        var entity = Scenes.Box("box", 1, 1, 1);
        var snapshot = new SnapshotEvent
        {
            Type = "condo",
            Entities = new[] { entity },
            PlacedEntityIds = new[] { entity.Id },
            TemplateCatalog = new[]
            {
                new TemplateCatalogEntry
                {
                    Key = "momoka:table",
                    Volume = new Geometry.Box3D(),
                    Properties = new List<Property>(),
                    Components = new List<string>(),
                },
            },
            TemplateVersion = "1",
            Version = 3,
        };
        var envelope = Frames.EventFrame("snapshot", 1, null, snapshot);

        var back = RoundTrip(envelope);
        var parsed = Assert.IsType<SnapshotEvent>(FrameRegistry.CreateEvent(back.Type, back.Payload));
        Assert.Equal("condo", parsed.Type);
        
        Assert.Single(parsed.Entities);
        Assert.Equal(entity.Id, parsed.Entities[0].Id);
        Assert.Equal("momoka:table", Assert.Single(parsed.TemplateCatalog).Key);
        Assert.Equal(3u, parsed.Version);
    }

    [Fact]
    public void SetProperty_Value_IsRawJsonScalar()
    {
        var request = new SetPropertyRequest
        {
            EntityId = Guid.NewGuid(),
            Name = "is_open",
            Value = JToken.FromObject(true),
        };
        var envelope = Frames.RequestFrame("set_property", 1, "req", request);
        var json = JsonConvert.SerializeObject(envelope, Settings.JsonSerialization);
        Assert.Contains("\"value\": true", json);

        var parsed = Assert.IsType<SetPropertyRequest>(FrameRegistry.CreateRequest("set_property", RoundTrip(envelope).Payload));
        Assert.Equal(JTokenType.Boolean, parsed.Value!.Type);
        Assert.True(parsed.Value.Value<bool>());
    }

    [Fact]
    public void Topics_Map_Frames()
    {
        Assert.Equal(Topics.Layout, Topics.Of(new LayoutChangedEvent()));
        Assert.Equal(Topics.Entities, Topics.Of(new EntityCreatedEvent()));
        Assert.Equal(Topics.Lifecycle, Topics.Of(new SaveCompletedEvent()));
        Assert.Equal(Topics.Lifecycle, Topics.Of(new ErrorEvent()));
    }
}
