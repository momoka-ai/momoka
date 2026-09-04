namespace Momoka.Core.Packets;

/// <summary>
/// 封包服务（存根）：向客户端发送封包（同步/异步）。路由与序列化策略未定——
/// 待 PacketHandler 设计（进程 → 网关 → 客户端管线）完成后接入，当前留空。
/// </summary>
public sealed class PacketService
{
    /// <summary>发送封包（存根）。</summary>
    public void Send<T>(T packet)
        where T : Packet
    {
    }

    /// <summary>发送封包（异步，存根）。</summary>
    public void SendAsync<T>(T packet)
        where T : Packet
    {
    }
}
