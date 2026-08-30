using Momoka.Core.Commands.Arguments;

namespace Momoka.Core.Commands;

/// <summary>
/// 指令定义抽象基类：名称（全局唯一）+ 别名 + 描述 + 语法（<see cref="Syntaxes"/>，类型化
/// 参数表或迷你语言格式）+ 默认执行器 + 子命令。既可直接子类化（重写 <see cref="ExecuteAsync"/> / 经
/// <see cref="AddSyntax(CommandExecutor, Argument[])"/> 声明语法），也可用
/// <see cref="CommandBuilder"/> 链式构建。
/// </summary>
public abstract class Command
{
    private readonly List<CommandSyntax> _syntaxes = new();
    private readonly List<Command> _subcommands = new();
    private IReadOnlyList<CommandSyntax>? _derivedSyntaxes;

    /// <summary>创建指令（无执行器，须重写 <see cref="ExecuteAsync"/> 或声明语法）。</summary>
    protected Command(string name)
        : this(name, null)
    {
    }

    /// <summary>创建指令并绑定组合执行器（缺省 <see cref="ExecuteAsync"/> 委托给它）。</summary>
    protected Command(string name, CommandExecutor? executor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Executor = executor;
    }

    /// <summary>指令名（全局唯一，注册时与全部别名一起查重）。</summary>
    public string Name { get; }

    /// <summary>别名（默认空）。</summary>
    public virtual IReadOnlyList<string> Aliases => Array.Empty<string>();

    /// <summary>可读描述（供 help 展示）。</summary>
    public virtual string Description => string.Empty;

    /// <summary>迷你语言语法声明（如 <c>"&lt;target&gt; [amount]"</c>，空 = 无参数）；
    /// 未显式 <see cref="AddSyntax(CommandExecutor, Argument[])"/> 时由 <see cref="Syntaxes"/> 惰性解析。</summary>
    public virtual string Syntax => string.Empty;

    /// <summary>组合执行器（可空；缺省 <see cref="ExecuteAsync"/> 委托给它，字符串语法亦以其为执行器）。</summary>
    public CommandExecutor? Executor { get; }

    /// <summary>默认执行器（所有语法都不匹配时调用；缺省 null）。</summary>
    public virtual CommandExecutor? DefaultExecutor => null;

    /// <summary>
    /// 该指令全部语法（匹配顺序即声明顺序）：显式声明的优先；否则若设置了 <see cref="Syntax"/>
    /// 字符串则惰性解析为单条语法（执行器 = <see cref="Executor"/> 或 <see cref="ExecuteAsync"/>）。
    /// </summary>
    public virtual IReadOnlyList<CommandSyntax> Syntaxes
    {
        get
        {
            if (_syntaxes.Count > 0)
            {
                return _syntaxes;
            }

            if (string.IsNullOrWhiteSpace(Syntax))
            {
                return Array.Empty<CommandSyntax>();
            }

            return _derivedSyntaxes ??= new[]
            {
                CommandSyntax.FromFormat(Syntax, Executor ?? ((context, ct) => ExecuteAsync(context, ct))),
            };
        }
    }

    /// <summary>子命令（执行 <c>name &lt;sub&gt; …</c> 时分派给对应子命令）。</summary>
    public virtual IReadOnlyList<Command> Subcommands => _subcommands;

    /// <summary>执行指令（缺省实现委托给 <see cref="Executor"/>，无执行器则为空操作；
    /// 设置了语法时由 <see cref="CommandManager"/> 按语法分派，不再调用本方法）。</summary>
    public virtual Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
        => Executor is null ? Task.CompletedTask : Executor(context, cancellationToken);

    /// <summary>声明一条语法（类型化参数表 + 执行器）。</summary>
    protected void AddSyntax(CommandExecutor executor, params Argument[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _syntaxes.Add(new CommandSyntax(executor, arguments));
    }

    /// <summary>声明一条语法（迷你语言格式）。</summary>
    protected void AddSyntax(string format, CommandExecutor executor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        _syntaxes.Add(CommandSyntax.FromFormat(format, executor));
    }

    /// <summary>添加子命令。</summary>
    protected void AddSubcommand(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _subcommands.Add(command);
    }
}
