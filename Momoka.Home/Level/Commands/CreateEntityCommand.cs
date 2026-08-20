using Momoka.Home.Entities;
namespace Momoka.Home.Level.Commands;

/// <summary>
/// 从模板物化实体并登记进未放置池（<see cref="LevelData.Entities"/>）。
/// 客户端无"凭空创建"能力——实体只能由服务器从配置文件模板实例化
/// （协议帧 <c>create_entity</c>）。记录进历史（撤销 = 从池移除）。
/// 不产出实体变更——池登记非布局变更（协议走独立 <c>entity_created</c> 帧）。
/// </summary>
public sealed class CreateEntityCommand : IEditorCommand
{
    private readonly string _templateKey;
    private readonly string? _templateVersion;
    private readonly EntityTemplateFactory _templates;

    public string Name => "CreateEntity";
    public string? CoalesceKey => null;

    private Entity? _entity;
    private RegisterEntityCommand? _register;

    public CreateEntityCommand(string templateKey, string? templateVersion, EntityTemplateFactory templates)
    {
        _templateKey = templateKey;
        _templateVersion = templateVersion;
        _templates = templates;
    }

    /// <summary>本次执行创建的实体（路由层取用发送 <c>entity_created</c> 帧）。</summary>
    public Entity? CreatedEntity => _entity;

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        var template = _templates.Resolve(_templateKey);
        if (template is null)
        {
            changes = new ChangeSet();
            return false;
        }
        if (_templateVersion is not null && _templateVersion != _templates.Version)
        {
            changes = new ChangeSet();
            return false; // 目录版本过期
        }
        _entity = _templates.Create(template);
        _register = new RegisterEntityCommand(_entity);
        return _register.Execute(session, out changes);
    }

    public bool Undo(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        return _register is not null && _register.Undo(session, out _);
    }
}
