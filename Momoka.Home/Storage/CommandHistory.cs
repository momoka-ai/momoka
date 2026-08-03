using Momoka.Home;
using Momoka.Home.Editor;
using Momoka.Home.Layouts;
namespace Momoka.Home.Storage;

/// <summary>Undo/redo stack of <see cref="Editor.EditorCommand"/> over a <see cref="VoxelLayout3D"/>.</summary>
public class CommandHistory
{
    private readonly Stack<Editor.EditorCommand> _undo = new();
    private readonly Stack<Editor.EditorCommand> _redo = new();

    public void Execute(Editor.EditorCommand command, VoxelLayout3D layout)
    {
        command.Apply(layout);
        _undo.Push(command);
        _redo.Clear();
    }

    public bool Undo(VoxelLayout3D layout)
    {
        if (!_undo.TryPop(out var cmd)) return false;
        cmd.Revert(layout);
        _redo.Push(cmd);
        return true;
    }

    public bool Redo(VoxelLayout3D layout)
    {
        if (!_redo.TryPop(out var cmd)) return false;
        cmd.Apply(layout);
        _undo.Push(cmd);
        return true;
    }
}
