using Microsoft.AspNetCore.SignalR;
using Momoka.Home.Level.Protocol;
namespace Momoka.Core.Gateway;

/// <summary>
/// Home 网关 Hub（客户端 → 服务端操作面）。当前全部为存根，函数实现待定稿后逐个落地：
/// 委托 Momoka.Home 模块（模型操作 / <c>ServerLevelData</c>）执行并广播
/// <see cref="IHomeClient"/> 事件。撤销/重做不在此列——历史由客户端本地保存，
/// 撤销即重发逆操作请求。
/// </summary>
/// <remarks>Hub 为 transient——不得在类字段存状态；编辑 token 等会话状态放注入的单例服务。</remarks>
public class HomeService : Hub<IHomeClient>
{
    /// <summary>全量同步：注册表 + 放置列表 + 模板目录 + 全局版本号。</summary>
    public Task<Result> GetSnapshot() => throw new NotImplementedException();

    /// <summary>从模板物化实体并登记进注册表（未放置池）。</summary>
    public Task<Result> CreateEntity(CreateEntityRequest request) => throw new NotImplementedException();

    /// <summary>放置：池 → 空间（根放置或表面附着）。</summary>
    public Task<Result> PlaceEntity(PlaceEntityRequest request) => throw new NotImplementedException();

    /// <summary>删除：空间回落池（家具）；不可变结构件即销毁（拆除语义）。</summary>
    public Task<Result> RemoveEntity(RemoveEntityRequest request) => throw new NotImplementedException();

    /// <summary>移动：改位置 + 宿主迁移（级联随移）。</summary>
    public Task<Result> MoveEntity(MoveEntityRequest request) => throw new NotImplementedException();

    /// <summary>旋转：三轴欧拉增量（体素占位不变）。</summary>
    public Task<Result> RotateEntity(RotateEntityRequest request) => throw new NotImplementedException();

    /// <summary>设置属性值（清除用 null；入值按属性类型强转）。</summary>
    public Task<Result> SetProperty(SetPropertyRequest request) => throw new NotImplementedException();

    /// <summary>重涂贴图（<c>Property.Texture</c>，模板外补建）。</summary>
    public Task<Result> SetTexture(SetTextureRequest request) => throw new NotImplementedException();

    /// <summary>砌墙：图模型墙体（GraphLine3D），创建即放置，无暂存态。</summary>
    public Task<Result> BuildWall(BuildWallRequest request) => throw new NotImplementedException();

    /// <summary>开洞：墙体排洞 + 放置门/窗开口。</summary>
    public Task<Result> BuildOpening(BuildOpeningRequest request) => throw new NotImplementedException();

    /// <summary>获取编辑 token（单写者互斥）。</summary>
    public Task<Result> BeginEdit() => throw new NotImplementedException();

    /// <summary>释放编辑 token。</summary>
    public Task<Result> EndEdit() => throw new NotImplementedException();

    /// <summary>持久化到存档。</summary>
    public Task<Result> Save() => throw new NotImplementedException();
}
