using Momoka.Home.Levels;
namespace Momoka.Home.Runtime;

/// <summary>
/// 意图命令：校验 + 应用的原子单元。执行成功产出 <see cref="ChangeSet"/>（实体增改删，
/// 供广播与客户端镜像）；失败返回 false 且不产生任何变更（validate-then-apply，
/// 复合操作在首步预检后不再有失败路径）。撤销/重做不在命令上——历史归客户端本地
/// （记录操作参数 + 逆操作重发），服务器不记录、不重放。
/// </summary>
public interface IEditorCommand
{
    bool Execute(EditorSession session, out ChangeSet changes);
}
