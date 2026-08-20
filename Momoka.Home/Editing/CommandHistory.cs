namespace Momoka.Home.Editing;

/// <summary>
/// 命令历史：撤销栈 + 重做栈。同 <see cref="IEditorCommand.CoalesceKey"/> 且位于栈顶的命令合并为
/// 一个历史项（<see cref="CoalescedCommand"/>）——拖拽一次 = 一个历史项，
/// 一次撤销回到拖拽前。
/// </summary>
public sealed class CommandHistory
{
    private readonly List<IEditorCommand> _undo = new();
    private readonly List<IEditorCommand> _redo = new();

    public int Count => _undo.Count;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>撤销栈（栈顶 = 最近执行），仅供检查 / 测试。</summary>
    public IReadOnlyList<IEditorCommand> UndoStack => _undo;

    /// <summary>
    /// 记录一条已执行命令。CoalesceKey 与栈顶一致 → 合并进栈顶命令；
    /// 否则入栈并清空重做栈。
    /// </summary>
    public void Record(IEditorCommand command)
    {
        if (command.CoalesceKey is { } key && _undo.Count > 0 && Matches(_undo[^1], key))
        {
            _undo[^1] = CoalescedCommand.Append(_undo[^1], command);
            _redo.Clear();
            return;
        }
        _undo.Add(command);
        _redo.Clear();
    }

    /// <summary>撤销栈顶命令，返回其变更集（无操作可撤销时返回 null）。</summary>
    public ChangeSet? Undo(EditorSession session)
    {
        if (_undo.Count == 0)
            return null;
        var command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        if (command.Undo(session, out var changes))
        {
            _redo.Add(command);
            return changes;
        }
        _undo.Add(command);
        return null;
    }

    /// <summary>重做最近撤销的命令，返回其变更集（无操作可重做时返回 null）。</summary>
    public ChangeSet? Redo(EditorSession session)
    {
        if (_redo.Count == 0)
            return null;
        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        if (command.Execute(session, out var changes))
        {
            _undo.Add(command);
            return changes;
        }
        _redo.Add(command);
        return null;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private static bool Matches(IEditorCommand command, string key) =>
        (command as CoalescedCommand)?.CoalesceKey == key || command.CoalesceKey == key;
}

/// <summary>
/// 合并命令：同一 CoalesceKey 连续命令的容器（历史内部使用）。Execute 依序执行
/// 子命令（仅由重做路径触发——合并时子命令早已各自执行过）、Undo 逆序回滚
/// （一次撤销回到拖拽前）。子命令的逆操作数据即合并的"逆操作追加"。
/// </summary>
public sealed class CoalescedCommand : ICompositeCommand
{
    private readonly List<IEditorCommand> _children = new();

    public IReadOnlyList<IEditorCommand> Children => _children;
    public string Name => _children.Count > 0 ? _children[0].Name : "Coalesced";
    public string? CoalesceKey { get; private set; }

    private CoalescedCommand(IEditorCommand first)
    {
        _children.Add(first);
        CoalesceKey = first.CoalesceKey;
    }

    /// <summary>把 <paramref name="next"/> 追加到栈顶命令上（栈顶已是合并命令则直续，否则先包装）。</summary>
    public static CoalescedCommand Append(IEditorCommand top, IEditorCommand next)
    {
        var coalesced = top as CoalescedCommand ?? new CoalescedCommand(top);
        coalesced._children.Add(next);
        return coalesced;
    }

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        foreach (var child in _children)
        {
            if (!child.Execute(session, out var childChanges))
                return false;
            changes.Merge(childChanges);
        }
        return true;
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (!_children[i].Undo(session, out var childChanges))
                return false;
            changes.Merge(childChanges);
        }
        return true;
    }
}
