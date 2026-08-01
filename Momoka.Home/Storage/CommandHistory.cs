using Momoka.Home.Models.Entities;

namespace Momoka.Home.Storage;

public class CommandHistory
{
    private readonly Stack<Editor.EditorCommand> _undo = new();
    private readonly Stack<Editor.EditorCommand> _redo = new();

    public void Execute(Editor.EditorCommand command, BlockCompositionEntity space)
    {
        command.Apply(space);
        _undo.Push(command);
        _redo.Clear();
    }

    public bool Undo(BlockCompositionEntity space)
    {
        if (!_undo.TryPop(out var cmd)) return false;
        cmd.Revert(space);
        _redo.Push(cmd);
        return true;
    }

    public bool Redo(BlockCompositionEntity space)
    {
        if (!_redo.TryPop(out var cmd)) return false;
        cmd.Apply(space);
        _undo.Push(cmd);
        return true;
    }
}
