namespace Momoka.Core.Events;

/// <summary>
/// 阻断门控（Before 语义）：事件类型实现本接口即表示可被其它插件否决。
/// 订阅者在处理事件时置 <c>IsCancelled = true</c> 即表达否决；<see cref="EventHub.Publish{TEvent}"/>
/// 检测到已取消即停止后续分发（fail-fast），发布方在返回后检查本标志决定提交/回滚。
/// 实现例：<c>public sealed record PlaceBefore(...) : ICancellable { public bool IsCancelled { get; set; } }</c>
/// </summary>
public interface ICancellable
{
    /// <summary>是否已被否决。</summary>
    bool IsCancelled { get; set; }
}
