using Momoka.Home;
using Momoka.Home.Editor;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Storage;

/// <summary>Undo/redo stack of <see cref="Editor.EditorCommand"/> over a <see cref="VoxelLayout{T}"/>.</summary>
public class CommandHistory
{
    private readonly Stack<Editor.EditorCommand> _undo = new();
    private readonly Stack<Editor.EditorCommand> _redo = new();

    public void Execute(Editor.EditorCommand command, VoxelLayout<Entity> layout)
    {
        command.Apply(layout);
        _undo.Push(command);
        _redo.Clear();
    }

    public bool Undo(VoxelLayout<Entity> layout)
    {
        if (!_undo.TryPop(out var cmd)) return false;
        cmd.Revert(layout);
        _redo.Push(cmd);
        return true;
    }

    public bool Redo(VoxelLayout<Entity> layout)
    {
        if (!_redo.TryPop(out var cmd)) return false;
        cmd.Apply(layout);
        _undo.Push(cmd);
        return true;
    }
}
