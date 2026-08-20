using Momoka.Home.Level.Protocol;
namespace Momoka.Home.Level;

/// <summary>Pub/Sub 订阅者：宿主按连接注册，接收服务器广播的事件帧（只读转发，禁止回写模型）。</summary>
public interface ISubscriber
{
    void OnFrame(Envelope envelope);
}

public sealed class LayoutChangedEventArgs : EventArgs
{
    public LayoutChangedEvent Event { get; }
    public LayoutChangedEventArgs(LayoutChangedEvent frame) => Event = frame;
}

public sealed class EntityCreatedEventArgs : EventArgs
{
    public EntityCreatedEvent Event { get; }
    public EntityCreatedEventArgs(EntityCreatedEvent frame) => Event = frame;
}

public sealed class SaveCompletedEventArgs : EventArgs
{
    public SaveCompletedEvent Event { get; }
    public SaveCompletedEventArgs(SaveCompletedEvent frame) => Event = frame;
}
