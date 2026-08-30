using Momoka.Core.Commands.Parsing;

namespace Momoka.Core.Commands;

/// <summary>
/// 指令管理器（注册表 + 执行器）：按名称 / 别名注册与查找指令，解析输入并派发执行。
/// 线程安全；注册重复名称 / 别名 fail-fast。执行失败不抛异常，映射为 <see cref="CommandResult"/>。
/// 派发顺序：命令级条件 → 子命令 → 语法匹配（依声明序）→ 默认执行器 → InvalidSyntax。
/// </summary>
public sealed class CommandManager
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Command> _commands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册指令（名称与别名全部参与查重；重复或冲突抛 <see cref="InvalidOperationException"/>）。</summary>
    public void Register(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);

        string[] names = [.. command.Aliases.Prepend(command.Name)];
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
        {
            throw new InvalidOperationException($"Command '{command.Name}' declares duplicate names or aliases.");
        }

        lock (_gate)
        {
            if (names.Any(_commands.ContainsKey))
            {
                throw new InvalidOperationException($"Command or alias '{command.Name}' is already registered.");
            }

            foreach (string name in names)
            {
                _commands.Add(name, command);
            }
        }
    }

    /// <summary>注销指令（按名称或别名，整条命令连同其余别名一并移除）。</summary>
    public bool Unregister(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
        {
            if (!_commands.TryGetValue(name, out Command? command))
            {
                return false;
            }

            foreach (string candidate in command.Aliases.Prepend(command.Name))
            {
                _commands.Remove(candidate);
            }
        }

        return true;
    }

    /// <summary>按名称或别名查找指令；未找到返回 null。</summary>
    public Command? GetCommand(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            return _commands.TryGetValue(name, out Command? command) ? command : null;
        }
    }

    /// <summary>已注册指令快照（去重，按注册顺序）。</summary>
    public IReadOnlyCollection<Command> Commands
    {
        get
        {
            lock (_gate)
            {
                return _commands.Values.Distinct().ToList();
            }
        }
    }

    /// <summary>解析整行（命令名 + 参数）并执行；空行 / 引号未闭合抛 <see cref="CommandSyntaxException"/>。</summary>
    public Task<CommandResult> ExecuteAsync(
        string line,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);

        (string name, string[] args) = CommandParser.ParseLine(line);
        return ExecuteAsync(name, args, cancellationToken);
    }

    /// <summary>按命令名 + 参数数组执行（解析失败不抛异常，返回 <see cref="CommandResult.InvalidSyntax"/>）。</summary>
    public Task<CommandResult> ExecuteAsync(
        string commandName,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(args);

        Command? command = GetCommand(commandName);
        if (command is null)
        {
            return Task.FromResult(CommandResult.Unknown);
        }

        return ExecuteAsync(command, commandName, args, cancellationToken);
    }

    private static async Task<CommandResult> ExecuteAsync(
        Command command,
        string invokedName,
        string[] args,
        CancellationToken cancellationToken)
    {
        if (command.Subcommands.Count > 0 && args.Length > 0)
        {
            Command? subcommand = command.Subcommands
                .FirstOrDefault(c => c.Name == args[0] || c.Aliases.Contains(args[0], StringComparer.OrdinalIgnoreCase));
            if (subcommand is not null)
            {
                return await ExecuteAsync(subcommand, args[0], args[1..], cancellationToken).ConfigureAwait(false);
            }
        }

        IReadOnlyList<CommandSyntax> syntaxes = command.Syntaxes;
        if (syntaxes.Count > 0)
        {
            CommandQueryResult result = CommandParser.Query(syntaxes, args);
            if (result.Matched)
            {
                var context = new CommandContext(invokedName, args, result.Arguments, result.RawArguments);
                return await RunAsync(result.Syntax!.Executor, context, cancellationToken).ConfigureAwait(false);
            }

            if (command.DefaultExecutor is not null)
            {
                var context = new CommandContext(
                    invokedName, args, new Dictionary<string, object?>(StringComparer.Ordinal));
                return await RunAsync(command.DefaultExecutor, context, cancellationToken).ConfigureAwait(false);
            }

            return CommandResult.InvalidSyntax;
        }

        var direct = new CommandContext(
            invokedName, args, new Dictionary<string, object?>(StringComparer.Ordinal));
        return await RunAsync(command.DefaultExecutor ?? ((ctx, ct) => command.ExecuteAsync(ctx, ct)), direct, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<CommandResult> RunAsync(
        CommandExecutor executor,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await executor(context, cancellationToken).ConfigureAwait(false);
            return CommandResult.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommandResult.Cancelled;
        }
        catch (Exception)
        {
            return CommandResult.ExecutorException;
        }
    }
}
