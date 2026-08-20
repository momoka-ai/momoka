using Momoka.Home.Data;
using Momoka.Home.Data.Sqlite;
using Momoka.Home.Editing.Commands;
using Momoka.Home.Editing.Protocol;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Editing;

/// <summary>
/// 权威服务器模型（常驻）：包装 <see cref="EditorSession"/>（命令执行 / 撤销 / Region
/// 增量 / ChangeSet 全在后者），新增职责 = 模板工厂、装载校验、请求路由、全局变更版本号、
/// 请求串行锁、编辑 token、Pub/Sub 事件广播、三持久化源组合（Sqlite + 体素 + Region）。
/// Home 保持零网络零外部依赖——WS 监听 / fan-out / 鉴权属宿主（Core/Stage daemon），
/// 此处只暴露 <see cref="HandleRequest"/> → Result + 事件。
/// </summary>
public sealed class ServerLevelData
{
    public EditorSession Session { get; }
    public EntityTemplateFactory Templates { get; }
    public string TemplateVersion => Templates.Version;

    /// <summary>全局变更版本号：每次成功布局变更 +1，随 <c>layout_changed</c> 事件广播。</summary>
    public uint Version { get; private set; }

    public event EventHandler<LayoutChangedEventArgs>? LayoutChanged;
    public event EventHandler<EntityCreatedEventArgs>? EntityCreated;
    public event EventHandler<SaveCompletedEventArgs>? SaveCompleted;

    private readonly object _gate = new();
    private readonly Dictionary<string, List<ISubscriber>> _subscribers = new();
    private SqliteStore? _store;
    private string _chunkDir = "";
    private string _regionsPath = "";
    private string? _editorConnectionId;
    private uint _seq;

    public ServerLevelData() : this(new EditorSession(new Residence()), new EntityTemplateFactory()) { }

    public ServerLevelData(EditorSession session, EntityTemplateFactory templates)
    {
        Session = session;
        Templates = templates;
    }

    // ── Pub/Sub（宿主按连接订阅；Home 内事件分发锁外）──────

    public void Subscribe(string topic, ISubscriber subscriber)
    {
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(topic, out var list))
                _subscribers[topic] = list = new List<ISubscriber>();
            list.Add(subscriber);
        }
    }

    public void Unsubscribe(string topic, ISubscriber subscriber)
    {
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(topic, out var list))
                return;
            list.Remove(subscriber);
            if (list.Count == 0)
                _subscribers.Remove(topic);
        }
    }

    // ── 装载 / 持久化（三源组合）────────────────────────────

    /// <summary>
    /// 装载：SqliteStore（residence + entities）→ 体素块（LayoutChunkCodec，恢复
    /// Voxels.Bound）→ 区域层（RegionsCodec，保留持久化 Id/Name 经
    /// <see cref="RegionMaintainer.Adopt"/> 注入）→ 放置关系重建 → Validate。
    /// 硬错误 → 拒绝装载（抛异常）。
    /// </summary>
    public void Load(SqliteStore store, string chunkDir, string regionsPath)
    {
        _store = store;
        _chunkDir = chunkDir;
        _regionsPath = regionsPath;

        lock (_gate)
        {
            var residence = store.Load() ?? throw new InvalidDataException("No residence in store.");
            Session.Adopt(residence);

            var loaded = LayoutChunkCodec.Load(chunkDir, residence.Entities);
            Session.Layout.Voxels = loaded.Grid;
            Session.Layout.Voxels.Bound = residence.Bound;
            Session.Layout.RestorePlacementFromGrid();

            var regions = RegionsCodec.Load(loaded.RegionColumns, regionsPath);
            Session.Regions.Adopt(regions);

            var report = Validate();
            if (!report.IsValid)
                throw new InvalidDataException(
                    $"Residence load validation failed: {string.Join("; ", report.HardErrors)}");
        }
    }

    /// <summary>持久化：Sqlite（residence + entities）+ 体素块 + Regions 文件。失败返回 false。</summary>
    public bool Save()
    {
        lock (_gate)
        {
            try
            {
                SaveInternal();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private void SaveInternal()
    {
        if (_store is null)
            throw new InvalidOperationException("ServerLevelData not loaded — no persistence configured.");
        // Bound 为持久化元数据，由持久化层同步（以实体占用范围为准）
        var extent = ComputeEntityExtent(Session.Layout);
        Session.Residence.Bound = extent;
        Session.Layout.Voxels.Bound = extent;
        _store.Save(Session.Residence);
        LayoutChunkCodec.Save(Session.Layout.Voxels, Session.Regions.Regions, _chunkDir);
        RegionsCodec.Save(Session.Regions.Regions ?? new ColumnLayout<Region>(_ => false), _regionsPath);
    }

    // ── 校验 ────────────────────────────────────────────────

    /// <summary>
    /// 装载校验：硬错误（拒绝装载）与可修复警告（自动修复 + 记录）。
    /// 每次装载后调用；请求时复验为轻量局部检查（见 HandleRequest 路由）。
    /// </summary>
    public ValidationReport Validate()
    {
        lock (_gate)
        {
            var report = new ValidationReport();
            var residence = Session.Residence;
            var layout = Session.Layout;

            if (string.IsNullOrWhiteSpace(residence.Name))
                report.HardErrors.Add("residence name is empty");

            var ids = new HashSet<Guid>();
            foreach (var entity in residence.Entities)
            {
                if (!ids.Add(entity.Id))
                    report.HardErrors.Add($"duplicate entity id '{entity.Id}'");
                if (string.IsNullOrEmpty(entity.Key.Path))
                    report.HardErrors.Add($"entity '{entity.Id}' has empty key");
            }

            foreach (var placed in layout.Entities)
                if (!residence.Entities.Contains(placed))
                    report.HardErrors.Add($"placed entity '{placed.Id}' is not in the registry");

            for (var i = 0; i < layout.Entities.Count; i++)
                for (var j = i + 1; j < layout.Entities.Count; j++)
                {
                    var a = layout.Entities[i];
                    var b = layout.Entities[j];
                    var anchorA = layout.Voxels.GetAsRelative(a.Transform.Position);
                    var anchorB = layout.Voxels.GetAsRelative(b.Transform.Position);
                    if (a.Volume is not null && b.Volume is not null &&
                        a.Volume.Intersects(anchorA, b.Volume, anchorB))
                        report.HardErrors.Add($"entities '{a.Id}' and '{b.Id}' overlap");
                }

            try
            {
                Region.BuildLayout(layout);
            }
            catch (Exception ex)
            {
                report.HardErrors.Add($"region build failed: {ex.Message}");
            }

            // 可修复：Bound 与实体占用范围不一致 → 以实体范围重建
            var extent = ComputeEntityExtent(layout);
            if (extent.Valid && residence.Bound != extent)
            {
                report.Warnings.Add("residence bound rebuilt from entity extents");
                residence.Bound = extent;
                layout.Voxels.Bound = extent;
            }
            else if (!extent.Valid && residence.Bound.Valid)
            {
                report.Warnings.Add("residence bound cleared (no placed entities)");
                residence.Bound = Bound.UnsetValue;
                layout.Voxels.Bound = Bound.UnsetValue;
            }

            return report;
        }
    }

    private static Bound ComputeEntityExtent(UnitLayout layout)
    {
        var any = false;
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var maxZ = float.MinValue;
        foreach (var entity in layout.Entities)
        {
            if (entity.Volume is null)
                continue;
            var anchor = layout.Voxels.GetAsRelative(entity.Transform.Position);
            foreach (var cell in entity.Volume.Cells3D())
            {
                var p = layout.Voxels.GetAsAbsolute(anchor + cell);
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
                any = true;
            }
        }
        return any
            ? Bound.FromCorners(new Float3(minX, minY, minZ), new Float3(maxX, maxY, maxZ))
            : Bound.UnsetValue;
    }

    // ── 请求处理（锁内路由 + 锁外分发）──────────────────────

    /// <summary>
    /// 请求入口：锁内路由（产生 Result 与事件清单），锁外分发事件。
    /// <paramref name="connectionId"/> 供编辑 token（BeginEdit 后仅其持有者可 mutate）。
    /// </summary>
    public Result HandleRequest(Envelope envelope, string connectionId)
    {
        IRequestFrame request;
        try
        {
            request = FrameRegistry.CreateRequest(envelope.Type, envelope.Payload);
        }
        catch
        {
            return Result.Fail("unknown_request");
        }

        var pending = new List<(IEventFrame Frame, string? RequestId)>();
        Result result;
        lock (_gate)
        {
            result = Apply(request, connectionId, pending);
        }

        if (!result.Ok)
            pending.Add((new ErrorEvent { RequestId = envelope.RequestId, ErrorCode = result.ErrorCode ?? "error" }, envelope.RequestId));

        foreach (var (frame, requestId) in pending)
            Dispatch(frame, requestId ?? envelope.RequestId);
        return result;
    }

    private Result Apply(IRequestFrame request, string connectionId, List<(IEventFrame, string?)> pending)
    {
        switch (request)
        {
            case GetSnapshotRequest:
                return GetSnapshot();
            case BeginEditRequest:
                return BeginEdit(connectionId);
            case EndEditRequest:
                return EndEdit(connectionId);
            case SaveRequest:
                return Save(pending);
            case CreateEntityRequest create:
                return CreateEntity(create, connectionId, pending);
            case UndoRequest:
                return UndoRedo(s => s.Undo(), "nothing_to_undo", connectionId, pending);
            case RedoRequest:
                return UndoRedo(s => s.Redo(), "nothing_to_redo", connectionId, pending);
            default:
                return Mutate(request, connectionId, pending);
        }
    }

    private Result Mutate(IRequestFrame request, string connectionId, List<(IEventFrame, string?)> pending)
    {
        if (!HasEditToken(connectionId))
            return Result.Fail("no_edit_token");

        IEditorCommand? command = null;
        string? notFound = null;
        switch (request)
        {
            case PlaceEntityRequest r:
                if (Session.Residence.Entities.All(e => e.Id != r.EntityId))
                    notFound = "entity_not_found";
                else if (Session.Layout.Entities.Any(e => e.Id == r.EntityId))
                    return Result.Fail("already_placed");
                else if (r.HostId is { } hostId && Session.Layout.Find(hostId) is null)
                    return Result.Fail("host_not_found");
                else
                    command = new PlaceEntityCommand(r.EntityId, r.Position, r.HostId);
                break;
            case RemoveEntityRequest r:
                if (Session.Layout.Find(r.EntityId) is null)
                    notFound = "entity_not_found";
                else
                    command = new RemoveEntityCommand(r.EntityId);
                break;
            case MoveEntityRequest r:
                if (Session.Layout.Find(r.EntityId) is null)
                    notFound = "entity_not_found";
                else if (r.HostId is { } moveHost && Session.Layout.Find(moveHost) is null)
                    return Result.Fail("host_not_found");
                else
                    command = new MoveEntityCommand(r.EntityId, r.Position, r.HostId);
                break;
            case RotateEntityRequest r:
                if (Session.Layout.Find(r.EntityId) is null)
                    notFound = "entity_not_found";
                else
                    command = new RotateEntityCommand(r.EntityId, new Float3(r.YawDelta, r.PitchDelta, r.RollDelta));
                break;
            case SetPropertyRequest r:
                if (Session.Layout.Find(r.EntityId) is null)
                    notFound = "entity_not_found";
                else
                    command = new SetPropertyCommand(r.EntityId, r.Name, r.Value?.ToObject<object>());
                break;
            case SetTextureRequest r:
                if (Session.Layout.Find(r.EntityId) is null)
                    notFound = "entity_not_found";
                else
                    command = new SetPropertyCommand(r.EntityId, Property.Texture, r.TextureKey, createIfMissing: true);
                break;
            case BuildWallRequest r:
                command = new BuildWallCommand(r.Segments);
                break;
            case BuildOpeningRequest r:
                if (Session.Layout.Find(r.WallEntityId) is null)
                    notFound = "wall_not_found";
                else
                    command = new BuildOpeningCommand(r.WallEntityId, r.OpeningOrigin, r.OpeningSize, r.OpeningKey, r.IsOpen);
                break;
        }

        if (notFound is not null)
            return Result.Fail(notFound);
        if (command is null)
            return Result.Fail("invalid_operation");

        var changes = Session.Execute(command);
        if (changes is null)
            return Result.Fail("invalid_operation");

        Version++;
        if (changes.Changes.Count > 0)
            pending.Add((new LayoutChangedEvent { Version = Version, EntityDelta = ToDelta(changes) }, null));
        return Result.Success(Version);
    }

    private Result UndoRedo(Func<EditorSession, ChangeSet?> op, string emptyError, string connectionId, List<(IEventFrame, string?)> pending)
    {
        if (!HasEditToken(connectionId))
            return Result.Fail("no_edit_token");
        var changes = op(Session);
        if (changes is null)
            return Result.Fail(emptyError);
        Version++;
        if (changes.Changes.Count > 0)
            pending.Add((new LayoutChangedEvent { Version = Version, EntityDelta = ToDelta(changes) }, null));
        return Result.Success(Version);
    }

    private Result CreateEntity(CreateEntityRequest r, string connectionId, List<(IEventFrame, string?)> pending)
    {
        if (!HasEditToken(connectionId))
            return Result.Fail("no_edit_token");
        var template = Templates.Resolve(r.TemplateKey);
        if (template is null)
            return Result.Fail("template_not_found");
        if (r.TemplateVersion is not null && r.TemplateVersion != Templates.Version)
            return Result.Fail("stale_template_version");

        var command = new CreateEntityCommand(r.TemplateKey, r.TemplateVersion, Templates);
        if (Session.Execute(command) is null)
            return Result.Fail("create_failed");
        var created = command.CreatedEntity!;
        pending.Add((new EntityCreatedEvent { Entity = created }, null));
        return Result.WithPayload(JToken.FromObject(created, JsonSerializer.Create(Settings.JsonSerialization)));
    }

    private Result GetSnapshot()
    {
        var snapshot = BuildSnapshot();
        return Result.WithPayload(JToken.FromObject(snapshot, JsonSerializer.Create(Settings.JsonSerialization)));
    }

    private Result Save(List<(IEventFrame, string?)> pending)
    {
        try
        {
            SaveInternal();
            pending.Add((new SaveCompletedEvent(), null));
            return Result.Success(Version);
        }
        catch
        {
            return Result.Fail("save_failed");
        }
    }

    private Result BeginEdit(string connectionId)
    {
        if (_editorConnectionId is not null && _editorConnectionId != connectionId)
            return Result.Fail("edit_token_held");
        _editorConnectionId = connectionId;
        return Result.Success(Version);
    }

    private Result EndEdit(string connectionId)
    {
        if (_editorConnectionId != connectionId)
            return Result.Fail("no_edit_token");
        _editorConnectionId = null;
        return Result.Success(Version);
    }

    private bool HasEditToken(string connectionId) =>
        _editorConnectionId is null || _editorConnectionId == connectionId;

    // ── 帧 / 事件 ───────────────────────────────────────────

    private static EntityDelta[] ToDelta(ChangeSet changes) => changes.Changes.Select(c => c.Kind switch
    {
        EntityChangeKind.Added => new EntityDelta { Kind = "added", EntityId = c.Entity.Id, Entity = c.Entity },
        EntityChangeKind.Removed => new EntityDelta { Kind = "removed", EntityId = c.Entity.Id },
        _ => new EntityDelta { Kind = "modified", EntityId = c.Entity.Id, Entity = c.Entity },
    }).ToArray();

    private SnapshotEvent BuildSnapshot()
    {
        var placed = Session.Layout.Entities.Select(e => e.Id).ToArray();
        return new SnapshotEvent
        {
            ResidenceMeta = new ResidenceMeta
            {
                Name = Session.Residence.Name,
                Address = Session.Residence.Address,
                Type = Session.Residence.Type.ToString(),
                Bound = Session.Residence.Bound,
            },
            Entities = Session.Residence.Entities.ToArray(),
            PlacedEntityIds = placed,
            RegionPayload = BuildRegionPayload(Session.Regions.Regions),
            TemplateCatalog = Templates.All.Select(t => new TemplateCatalogEntry
            {
                Key = t.Key.ToString(),
                Volume = t.Volume,
                Properties = t.Properties ?? new List<Property>(),
                Components = t.Components ?? new List<string>(),
            }).ToArray(),
            TemplateVersion = Templates.Version,
            Version = Version,
        };
    }

    private static RegionPayload BuildRegionPayload(ColumnLayout<Region>? regions)
    {
        var payload = new RegionPayload();
        if (regions is null)
            return payload;
        var seen = new HashSet<int>();
        foreach (var (_, region) in regions.Cells())
            if (seen.Add(region.Id))
                payload.Regions.Add(new RegionInfo { Id = region.Id, Name = region.Name });
        return payload;
    }

    private void Dispatch(IEventFrame frame, string? requestId)
    {
        var envelope = Frames.EventFrame(FrameRegistry.NameOf(frame.GetType()), _seq++, requestId, frame);
        var topic = Topics.Of(frame);
        List<ISubscriber>? subscribers;
        lock (_gate)
        {
            _subscribers.TryGetValue(topic, out subscribers);
        }
        if (subscribers is not null)
            foreach (var subscriber in subscribers.ToList())
                subscriber.OnFrame(envelope);

        switch (frame)
        {
            case LayoutChangedEvent layoutChanged:
                LayoutChanged?.Invoke(this, new LayoutChangedEventArgs(layoutChanged));
                break;
            case EntityCreatedEvent created:
                EntityCreated?.Invoke(this, new EntityCreatedEventArgs(created));
                break;
            case SaveCompletedEvent saved:
                SaveCompleted?.Invoke(this, new SaveCompletedEventArgs(saved));
                break;
        }
    }
}

/// <summary>装载校验报告：硬错误（拒绝装载）与可修复警告（自动修复 + 记录）。</summary>
public sealed class ValidationReport
{
    public List<string> HardErrors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsValid => HardErrors.Count == 0;
}
