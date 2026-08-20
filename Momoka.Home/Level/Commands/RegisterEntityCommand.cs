using Momoka.Home.Entities;
namespace Momoka.Home.Level.Commands;

/// <summary>
/// 登记命令（事务内部使用）：把新创建的实体注册进 <see cref="LevelData.Entities"/>
/// （"未放置池"），为后续 <see cref="PlaceEntityCommand"/> 提供解析来源。
/// Undo = 移出池。不产出实体变更（未放置不入体素空间）。
/// </summary>
public sealed class RegisterEntityCommand : IEditorCommand
{
    private readonly Entity _entity;

    public string Name => "Register";
    public string? CoalesceKey => null;

    public RegisterEntityCommand(Entity entity) => _entity = entity;

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        if (session.Data.Entities.Contains(_entity))
            return false;
        session.Data.Entities.Add(_entity);
        return true;
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        return session.Data.Entities.Remove(_entity);
    }
}
