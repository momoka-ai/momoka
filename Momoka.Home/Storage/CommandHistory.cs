
using Momoka.Home;
namespace Momoka.Home.Storage;

public class CommandHistory
{
    private readonly Stack<Editor.EditorCommand> _undo = new();
    private readonly Stack<Editor.EditorCommand> _redo = new();

    public void Execute(Editor.EditorCommand command, VoxelGridEntity space)
    {
        command.Apply(space);
        _undo.Push(command);
        _redo.Clear();
    }

    public bool Undo(VoxelGridEntity space)
    {
        if (!_undo.TryPop(out var cmd)) return false;
        cmd.Revert(space);
        _redo.Push(cmd);
        return true;
    }

    public bool Redo(VoxelGridEntity space)
    {
        if (!_redo.TryPop(out var cmd)) return false;
        cmd.Apply(space);
        _undo.Push(cmd);
        return true;
    }
}
