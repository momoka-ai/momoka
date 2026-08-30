namespace Momoka.Core.Commands;

/// <summary>
/// 指令执行回调（对应 Minestom 的 <c>CommandExecutor</c>）：接收已解析的
/// <see cref="CommandContext"/> 并执行指令逻辑；异常由 <see cref="CommandManager"/> 捕获并映射为
/// <see cref="CommandResult.ExecutorException"/>。
/// </summary>
public delegate Task CommandExecutor(CommandContext context, CancellationToken cancellationToken = default);
