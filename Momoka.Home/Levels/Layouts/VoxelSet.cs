using Momoka.Home.Primitives;
namespace Momoka.Home.Levels.Layouts;

/// <summary>
/// 可枚举自身所占体素格的元素契约：返回该元素在局部坐标系中占据的
/// 3D 格集合（相对宿主锚点，对齐网格）。体积形状（<c>Volume</c>）、
/// 占用网格等实现本接口；查询层（射线 / 碰撞 / 区块推导）只依赖
/// <see cref="GetVoxelSet"/>，不关心形状的具体几何。
/// </summary>
public interface IVoxelSet
{
    /// <summary>局部占用 3D 格（相对宿主 Coords，对齐网格）。</summary>
    IEnumerable<Int3> GetVoxelSet();
}
