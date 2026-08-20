namespace Momoka.Home.Level;

/// <summary>
/// 意图命令 + 显式逆操作：命令自带语义逆（如 <see cref="Commands.RemoveEntityCommand"/>
/// 的 Undo = 恢复到原位置 + 原宿主 + 原登记）。<see cref="Execute"/> / <see cref="Undo"/>
/// 双向产出 <see cref="ChangeSet"/>（Ui 据此回滚 / 前推渲染）。
/// </summary>
public interface IEditorCommand
{
    string Name { get; }

    /// <summary>非 null 且与栈顶命令相同 → 合并为一个历史项（拖拽：CoalesceKey = "Move#{id}"）。</summary>
    string? CoalesceKey { get; }

    bool Execute(EditorSession session, out ChangeSet changes);
    bool Undo(EditorSession session, out ChangeSet changes);
}

/// <summary>事务命令：持有子命令列表，Execute 依序执行、Undo 逆序回滚。</summary>
public interface ICompositeCommand : IEditorCommand
{
    IReadOnlyList<IEditorCommand> Children { get; }
}

/// <summary>
/// 事务基类（BuildWall / BuildOpening 使用）：子命令依序执行、逆序回滚；
/// 任一子命令失败 → 已执行子命令全部回滚，整体返回 false 且无残留
/// （ChangeSet 置空——墙未改、开口未落）。
/// </summary>
public abstract class CompositeCommand : ICompositeCommand
{
    public abstract string Name { get; }
    public string? CoalesceKey => null;
    public List<IEditorCommand> Children { get; } = new();
    IReadOnlyList<IEditorCommand> ICompositeCommand.Children => Children;

    public virtual bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var executed = new List<IEditorCommand>();
        try
        {
            foreach (var child in Children)
            {
                if (!child.Execute(session, out var childChanges))
                {
                    Rollback(session, executed);
                    changes = new ChangeSet();
                    return false;
                }
                executed.Add(child);
                changes.Merge(childChanges);
            }
            return true;
        }
        catch
        {
            Rollback(session, executed);
            changes = new ChangeSet();
            throw;
        }
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            if (!Children[i].Undo(session, out var childChanges))
                return false;
            changes.Merge(childChanges);
        }
        return true;
    }

    private static void Rollback(EditorSession session, List<IEditorCommand> executed)
    {
        for (var i = executed.Count - 1; i >= 0; i--)
            executed[i].Undo(session, out _);
    }
}
