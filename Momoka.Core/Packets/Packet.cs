namespace Momoka.Core.Packets;

/// <summary>
/// 封包基类（存根）：承载进程 → 客户端（网关）的出站消息载荷。与事件同构：
/// 类型 = 运行时具体类型，路由/序列化由 PacketService 按类型完成。
/// </summary>
public abstract record class Packet
{
}
