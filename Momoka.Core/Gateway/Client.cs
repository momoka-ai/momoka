namespace Momoka.Core;

/// <summary>
/// 线上客户端（设备级身份，C/S 的 Client）：手机、电脑、中控屏等物件的一次连接。
/// <see cref="ClientId"/> 是设备地址（稳定，网关按它寻址）；<see cref="ConnectionId"/>
/// 是 SignalR 网络层令牌（重连即变，仅作当前可达路径）。"谁在使用该设备"由后续
/// Profile 模型承载，由客户端自行发包上报。
/// </summary>
public sealed record Client(string ClientId, string Role, DateTimeOffset ConnectedAt, string ConnectionId);
