using Momoka.Core.Commands.Arguments;

namespace Momoka.Core.Commands;

/// <summary>
/// 指令构建器（链式，对应 Minestom 的 <c>builder.Command</c> 构建方式）：设置名称 / 别名 / 描述 /
/// 默认执行器 / 多条语法（<see cref="Argument"/> 或迷你语言格式）/ 子命令，
/// <see cref="Build"/> 产出不可变的 <see cref="Command"/> 实例供 <see cref="CommandManager"/> 注册。
/// </summary>
public sealed class CommandBuilder
{
    private readonly string _name;
    private readonly List<string> _aliases = new();
    private readonly List<CommandSyntax> _syntaxes = new();
    private readonly List<Command> _subcommands = new();
    private string _description = string.Empty;
    private CommandExecutor? _defaultExecutor;

    /// <summary>创建指令构建器（名称非空，注册时全名 / 别名统一查重）。</summary>
    public CommandBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    /// <summary>添加别名。</summary>
    public CommandBuilder Alias(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        _aliases.Add(alias);
        return this;
    }

    /// <summary>批量添加别名。</summary>
    public CommandBuilder Aliases(params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        foreach (string alias in aliases)
        {
            Alias(alias);
        }

        return this;
    }

    /// <summary>设置描述（供 help 展示）。</summary>
    public CommandBuilder Description(string description)
    {
        _description = description ?? string.Empty;
        return this;
    }

    /// <summary>设置默认执行器（所有语法都不匹配时调用）。</summary>
    public CommandBuilder DefaultExecutor(CommandExecutor executor)
    {
        _defaultExecutor = executor ?? throw new ArgumentNullException(nameof(executor));
        return this;
    }

    /// <summary>添加一条语法（类型化参数表 + 执行器）。</summary>
    public CommandBuilder Syntax(CommandExecutor executor, params Argument[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _syntaxes.Add(new CommandSyntax(executor, arguments));
        return this;
    }

    /// <summary>添加一条语法（迷你语言格式，如 <c>"&lt;target&gt; [amount]"</c>）。</summary>
    public CommandBuilder Syntax(string format, CommandExecutor executor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        _syntaxes.Add(CommandSyntax.FromFormat(format, executor));
        return this;
    }

    /// <summary>添加子命令（执行 <c>name &lt;sub&gt; …</c> 时分派给它）。</summary>
    public CommandBuilder Subcommand(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _subcommands.Add(command);
        return this;
    }

    /// <summary>构建指令实例（配置即冻结；后续调用 Create 的返回互不影响）。</summary>
    public Command Build() => new BuiltCommand(this);

    /// <summary>构建器产出的指令：透传构建器全部配置。</summary>
    private sealed class BuiltCommand : Command
    {
        private readonly CommandBuilder _builder;

        public BuiltCommand(CommandBuilder builder)
            : base(builder._name)
        {
            _builder = builder;
        }

        public override IReadOnlyList<string> Aliases => _builder._aliases;

        public override string Description => _builder._description;

        public override CommandExecutor? DefaultExecutor => _builder._defaultExecutor;

        public override IReadOnlyList<CommandSyntax> Syntaxes => _builder._syntaxes;

        public override IReadOnlyList<Command> Subcommands => _builder._subcommands;
    }
}
