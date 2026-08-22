using Momoka.Home.Runtime;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Entities;
namespace Momoka.Home.Levels.Commands;

/// <summary>
/// 从模板物化实体并登记进未放置池（<see cref="LevelData.Entities"/>）。
/// 客户端无"凭空创建"能力——实体只能由服务器从配置文件模板实例化。
/// 不产出实体变更——池登记非布局变更（走独立 entity_created 事件）。
/// </summary>
public sealed class CreateEntityCommand : IEditorCommand
{
    private readonly string _templateKey;
    private readonly string? _templateVersion;
    private readonly EntityTemplateFactory _templates;

    private Entity? _entity;

    public CreateEntityCommand(string templateKey, string? templateVersion, EntityTemplateFactory templates)
    {
        _templateKey = templateKey;
        _templateVersion = templateVersion;
        _templates = templates;
    }

    /// <summary>本次执行创建的实体（路由层取用发送 entity_created 事件）。</summary>
    public Entity? CreatedEntity => _entity;

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var template = _templates.Resolve(_templateKey);
        if (template is null)
            return false;
        if (_templateVersion is not null && _templateVersion != _templates.Version)
            return false; // 目录版本过期
        _entity = _templates.Create(template);
        session.Data.Entities.Add(_entity);
        return true;
    }
}
