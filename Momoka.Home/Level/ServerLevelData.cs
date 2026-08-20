using Momoka.Home.Data;
using Momoka.Home.Data.Sqlite;
using Momoka.Home.Level.Commands;
using Momoka.Home.Level.Protocol;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Level;

/// <summary>
/// 权威服务器模型（常驻）：包装 <see cref="EditorSession"/>（命令执行 / 撤销 /
/// ChangeSet 全在后者）；本类型即权威数据（继承 <see cref="LevelData"/>：
/// 类型 + 布局 + 全实体注册表）。负责模板工厂、装载校验、类型化操作入口
/// （网关 <c>HomeService</c> 直接委托）、全局变更版本号、编辑 token、
/// 变更事件（网关转发到 <c>IHomeClient</c>）、Sqlite 持久化（Entities + Voxels）。
/// Home 保持零网络零外部依赖。
/// </summary>
/// <remarks>
/// 并发模型：<c>_gate</c> 把模型操作串行化（模型非线程安全——编辑 token 只保证
/// 单写者，不隔离读/写竞态）。事件回调（<see cref="LayoutChanged"/> /
/// <see cref="EntityCreated"/> / <see cref="SaveCompleted"/>）一律在锁外触发——
/// 锁内只计算结果与事件载荷，避免订阅者（网关广播）拖住全局串行锁。
/// </remarks>
public sealed class ServerLevelData : LevelData
{
    public EditorSession Session { get; }
    public EntityTemplateFactory Templates { get; }
    public string TemplateVersion => Templates.Version;

    /// <summary>全局变更版本号：每次成功布局变更 +1，随 <see cref="LayoutChanged"/> 广播。</summary>
    public uint Version { get; private set; }

    /// <summary>布局变更（版本 + 实体增量；网关 1:1 转发 <c>IHomeClient.LayoutChanged</c>，锁外触发）。</summary>
    public event Action<uint, EntityDelta[]>? LayoutChanged;

    /// <summary>新实体登记进注册表（网关 1:1 转发 <c>IHomeClient.EntityCreated</c>，锁外触发）。</summary>
    public event Action<Entity>? EntityCreated;

    /// <summary>持久化完成（网关 1:1 转发 <c>IHomeClient.SaveCompleted</c>，锁外触发）。</summary>
    public event Action? SaveCompleted;

    private readonly object _gate = new();
    private SqliteStore? _store;
    private string? _editorConnectionId;

    public ServerLevelData() : this(new EntityTemplateFactory()) { }

    public ServerLevelData(EntityTemplateFactory templates)
    {
        Session = new EditorSession(this); // 权威数据 = 自身（基类 Layout / Entities / Type）
        Templates = templates;
        // 生成时创建隐藏 Home 实体（档案：无 Volume、不进空间、无任何增删渠道；
        // unit_type 承载 LevelData.Type 的持久化真相——由 SqliteStore 同步）
        if (Entities.All(e => e.Key != HomeKey))
        {
            var home = new Entity { Key = HomeKey };
            home.AddProperty(new EnumProperty<UnitType>(Property.UnitType, UnitType.Estate));
            home.AddProperty(new StringProperty(Property.Address, ""));
            Entities.Add(home);
        }
    }

    // ── 装载 / 持久化（Sqlite 单源：Entities + Voxels）────────

    /// <summary>
    /// 装载：SqliteStore（entities + chunks）→ 放置关系重建 → Type 从 Home 实体
    /// 还原 → Validate（Bound 派生修复）。硬错误 → 拒绝装载（抛异常）。
    /// </summary>
    public void Load(SqliteStore store)
    {
        _store = store;

        lock (_gate)
        {
            var data = store.Load() ?? throw new InvalidDataException("No level data in store.");
            // Type 已由 SqliteStore 从 Home 实体还原；就地替换自身基类载荷，
            // 保持 EditorSession.Data 与自身（继承的 LevelData）同一实例
            ReplaceWith(data);
            Session.Adopt(this);
            Session.Layout.RestorePlacementFromGrid();

            var report = Validate();
            if (!report.IsValid)
                throw new InvalidDataException(
                    $"Level load validation failed: {string.Join("; ", report.HardErrors)}");
        }
    }

    /// <summary>
    /// 持久化：Sqlite 事务（Entities + chunks）。持久化成功（或失败）决定 Result；
    /// <see cref="SaveCompleted"/> 在锁外触发，订阅者异常不回滚持久化结果。
    /// </summary>
    public Result Save()
    {
        uint version;
        lock (_gate)
        {
            try
            {
                SaveInternal();
                version = Version;
            }
            catch
            {
                return Result.Fail("save_failed");
            }
        }
        SaveCompleted?.Invoke();
        return Result.Success(version);
    }

    private void SaveInternal()
    {
        if (_store is null)
            throw new InvalidOperationException("ServerLevelData not loaded — no persistence configured.");
        // Bound 为派生量，由持久化层同步（以实体占用范围为准）；Type 由 SqliteStore 同步到 Home 实体
        var extent = ComputeEntityExtent(Layout);
        Layout.Voxels.Bound = extent;
        _store.Save(this);
    }

    // ── 校验 ────────────────────────────────────────────────

    /// <summary>
    /// 装载校验：硬错误（拒绝装载）与可修复警告（自动修复 + 记录）。每次装载后调用。
    /// </summary>
    public ValidationReport Validate()
    {
        lock (_gate)
        {
            var report = new ValidationReport();
            var layout = Layout;

            // 隐藏 Home 实体：恰好一个（生成时创建；无增删渠道，仅作档案 + Type 持久化）
            var homes = Entities.Where(e => e.Key == HomeKey).ToList();
            if (homes.Count != 1)
                report.HardErrors.Add($"home entity count is {homes.Count} (expected exactly 1)");

            var ids = new HashSet<Guid>();
            foreach (var entity in Entities)
            {
                if (!ids.Add(entity.Id))
                    report.HardErrors.Add($"duplicate entity id '{entity.Id}'");
                if (string.IsNullOrEmpty(entity.Key.Path))
                    report.HardErrors.Add($"entity '{entity.Id}' has empty key");
            }

            foreach (var placed in layout.Entities)
                if (!Entities.Contains(placed))
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

            // 可修复：Bound 与实体占用范围不一致 → 以实体范围重建（派生量，无独立持久化）
            var extent = ComputeEntityExtent(layout);
            if (extent.Valid && layout.Voxels.Bound != extent)
            {
                report.Warnings.Add("bound rebuilt from entity extents");
                layout.Voxels.Bound = extent;
            }
            else if (!extent.Valid && layout.Voxels.Bound.Valid)
            {
                report.Warnings.Add("bound cleared (no placed entities)");
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

    // ── 类型化操作入口（网关 HomeService 直接委托）────────────

    /// <summary>全量同步：注册表 + 放置列表 + 模板目录 + 全局版本号。</summary>
    public Result GetSnapshot()
    {
        lock (_gate)
        {
            var snapshot = BuildSnapshot();
            return Result.WithPayload(JToken.FromObject(snapshot, JsonSerializer.Create(Settings.JsonSerialization)));
        }
    }

    /// <summary>从模板物化实体并登记进注册表（未放置池）。不产出布局变更。</summary>
    public Result CreateEntity(CreateEntityRequest request, string connectionId) => Handle(connectionId, () =>
    {
        var template = Templates.Resolve(request.TemplateKey);
        if (template is null)
            return new Outcome(Result.Fail("template_not_found"), null);
        if (request.TemplateVersion is not null && request.TemplateVersion != Templates.Version)
            return new Outcome(Result.Fail("stale_template_version"), null);

        var command = new CreateEntityCommand(request.TemplateKey, request.TemplateVersion, Templates);
        if (Session.Execute(command) is null)
            return new Outcome(Result.Fail("create_failed"), null);
        var created = command.CreatedEntity!;
        return new Outcome(
            Result.WithPayload(JToken.FromObject(created, JsonSerializer.Create(Settings.JsonSerialization))),
            () => EntityCreated?.Invoke(created));
    });

    /// <summary>放置：池 → 空间（根放置或表面附着）。</summary>
    public Result PlaceEntity(PlaceEntityRequest request, string connectionId) => Handle(connectionId, () =>
    {
        if (Entities.All(e => e.Id != request.EntityId))
            return new Outcome(Result.Fail("entity_not_found"), null);
        if (Session.Layout.Entities.Any(e => e.Id == request.EntityId))
            return new Outcome(Result.Fail("already_placed"), null);
        if (request.HostId is { } hostId && Session.Layout.Find(hostId) is null)
            return new Outcome(Result.Fail("host_not_found"), null);
        return Execute(new PlaceEntityCommand(request.EntityId, request.Position, request.HostId));
    });

    /// <summary>删除：空间回落池（级联回落表面物件）。</summary>
    public Result RemoveEntity(RemoveEntityRequest request, string connectionId) => Handle(connectionId, () =>
    {
        if (Session.Layout.Find(request.EntityId) is null)
            return new Outcome(Result.Fail("entity_not_found"), null);
        return Execute(new RemoveEntityCommand(request.EntityId));
    });

    /// <summary>移动：改位置 + 宿主迁移（级联随移）。</summary>
    public Result MoveEntity(MoveEntityRequest request, string connectionId) => Handle(connectionId, () =>
    {
        if (Session.Layout.Find(request.EntityId) is null)
            return new Outcome(Result.Fail("entity_not_found"), null);
        if (request.HostId is { } moveHost && Session.Layout.Find(moveHost) is null)
            return new Outcome(Result.Fail("host_not_found"), null);
        return Execute(new MoveEntityCommand(request.EntityId, request.Position, request.HostId));
    });

    /// <summary>旋转：三轴欧拉增量（体素占位不变）。</summary>
    public Result RotateEntity(RotateEntityRequest request, string connectionId) => Handle(connectionId, () =>
    {
        if (Session.Layout.Find(request.EntityId) is null)
            return new Outcome(Result.Fail("entity_not_found"), null);
        return Execute(new RotateEntityCommand(request.EntityId, new Float3(request.YawDelta, request.PitchDelta, request.RollDelta)));
    });

    /// <summary>设置 / 清除属性值（属性不存在 → 失败）。</summary>
    public Result SetProperty(SetPropertyRequest request, string connectionId) => Handle(connectionId, () =>
    {
        if (Session.Layout.Find(request.EntityId) is null)
            return new Outcome(Result.Fail("entity_not_found"), null);
        return Execute(new SetPropertyCommand(request.EntityId, request.Name, request.Value?.ToObject<object>()));
    });

    /// <summary>重涂贴图（Property.Texture，模板外补建）。</summary>
    public Result SetTexture(SetTextureRequest request, string connectionId) => Handle(connectionId, () =>
    {
        if (Session.Layout.Find(request.EntityId) is null)
            return new Outcome(Result.Fail("entity_not_found"), null);
        return Execute(new SetPropertyCommand(request.EntityId, Property.Texture, request.TextureKey, createIfMissing: true));
    });

    /// <summary>砌墙（创建即放置，无暂存态）。</summary>
    public Result BuildWall(BuildWallRequest request, string connectionId) => Handle(connectionId, () =>
        Execute(new BuildWallCommand(request.Segments)));

    /// <summary>开洞：墙排洞 + 放置门/窗开口。</summary>
    public Result BuildOpening(BuildOpeningRequest request, string connectionId) => Handle(connectionId, () =>
    {
        if (Session.Layout.Find(request.WallEntityId) is null)
            return new Outcome(Result.Fail("wall_not_found"), null);
        return Execute(new BuildOpeningCommand(request.WallEntityId, request.OpeningOrigin, request.OpeningSize, request.OpeningKey, request.IsOpen));
    });

    /// <summary>获取编辑 token（单写者互斥：仅持有者可 mutate）。</summary>
    public Result BeginEdit(string connectionId)
    {
        lock (_gate)
        {
            if (_editorConnectionId is not null && _editorConnectionId != connectionId)
                return Result.Fail("edit_token_held");
            _editorConnectionId = connectionId;
            return Result.Success(Version);
        }
    }

    /// <summary>释放编辑 token。</summary>
    public Result EndEdit(string connectionId)
    {
        lock (_gate)
        {
            if (_editorConnectionId != connectionId)
                return Result.Fail("no_edit_token");
            _editorConnectionId = null;
            return Result.Success(Version);
        }
    }

    private bool HasEditToken(string connectionId) =>
        _editorConnectionId is null || _editorConnectionId == connectionId;

    /// <summary>
    /// 锁内路由：编辑 token 校验 → 锁内执行并收集事件，锁外触发 <see cref="Outcome.Notify"/>。
    /// </summary>
    private Result Handle(string connectionId, Func<Outcome> mutate)
    {
        Outcome outcome;
        lock (_gate)
        {
            if (!HasEditToken(connectionId))
                return Result.Fail("no_edit_token");
            outcome = mutate();
        }
        outcome.Notify?.Invoke();
        return outcome.Result;
    }

    private Outcome Execute(IEditorCommand command)
    {
        var changes = Session.Execute(command);
        if (changes is null)
            return new Outcome(Result.Fail("invalid_operation"), null);
        return Commit(changes);
    }

    /// <summary>提交：版本 +1 → 组装变更事件（锁内），通知在锁外由 <see cref="Handle"/> 触发。</summary>
    private Outcome Commit(ChangeSet changes)
    {
        var version = ++Version;
        Action? notify = null;
        if (changes.Changes.Count > 0)
        {
            var deltas = ToDelta(changes);
            notify = () => LayoutChanged?.Invoke(version, deltas);
        }
        return new Outcome(Result.Success(version), notify);
    }

    // ── 事件载荷 ───────────────────────────────────────────

    private static EntityDelta[] ToDelta(ChangeSet changes) => changes.Changes.Select(c => c.Kind switch
    {
        EntityChangeKind.Added => new EntityDelta { Kind = "added", EntityId = c.Entity.Id, Entity = c.Entity },
        EntityChangeKind.Removed => new EntityDelta { Kind = "removed", EntityId = c.Entity.Id },
        _ => new EntityDelta { Kind = "modified", EntityId = c.Entity.Id, Entity = c.Entity },
    }).ToArray();

    private SnapshotEvent BuildSnapshot()
    {
        var placed = Layout.Entities.Select(e => e.Id).ToArray();
        return new SnapshotEvent
        {
            Type = Type.ToString(),
            Entities = Entities.ToArray(),
            PlacedEntityIds = placed,
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
}

/// <summary>锁内执行的结果 + 锁外通知（事件触发延迟到 <see cref="ServerLevelData"/> 的锁释放后）。</summary>
internal readonly record struct Outcome(Result Result, Action? Notify);

/// <summary>装载校验报告：硬错误（拒绝装载）与可修复警告（自动修复 + 记录）。</summary>
public sealed class ValidationReport
{
    public List<string> HardErrors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsValid => HardErrors.Count == 0;
}
