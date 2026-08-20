using Momoka.Home.Level;
using Momoka.Home.Entities;
namespace Momoka.Home.Level.Commands;

/// <summary>删除：空间回落池（连带回落表面物件，实体保留于注册表）。</summary>
public sealed class RemoveEntityCommand : IEditorCommand
{
    private readonly Guid _entityId;

    public RemoveEntityCommand(Guid entityId) => _entityId = entityId;

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var unit = session.Layout;
        var entity = unit.Find(_entityId);
        if (entity is null)
            return false;

        var cascade = unit.CascadeOf(entity);
        if (!unit.Remove(entity))
            return false;
        foreach (var cascaded in cascade)
            changes.Removed(cascaded);
        return true;
    }
}
