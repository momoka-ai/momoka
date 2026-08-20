namespace Momoka.Home.Editing.Commands;

/// <summary>
/// 会话级撤销命令（委托 History）。**不经 <see cref="EditorSession.Execute"/> 提交**——
/// <see cref="ServerLevelData"/> 路由 undo 请求时直接调用其 <see cref="IEditorCommand.Execute"/>
/// （不走 session.Execute 的"记录历史"路径），故撤销本身不会被当作普通命令记录进历史。
/// </summary>
public sealed class UndoCommand : IEditorCommand
{
    public string Name => "Undo";
    public string? CoalesceKey => null;

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        var result = session.Undo();
        changes = result ?? new ChangeSet();
        return result is not null;
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        var result = session.Redo();
        changes = result ?? new ChangeSet();
        return result is not null;
    }
}

/// <summary>会话级重做命令（委托 History），路径同 <see cref="UndoCommand"/>。</summary>
public sealed class RedoCommand : IEditorCommand
{
    public string Name => "Redo";
    public string? CoalesceKey => null;

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        var result = session.Redo();
        changes = result ?? new ChangeSet();
        return result is not null;
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        var result = session.Undo();
        changes = result ?? new ChangeSet();
        return result is not null;
    }
}
