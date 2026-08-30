using Xunit;
using Momoka.Core.Commands;

namespace Momoka.Core.Tests;

/// <summary>Commands：注册 / 别名 / 查重 / 解析（迷你语言）/ 执行结果映射 / 取消。</summary>
public sealed class CommandsTests
{
    [Fact]
    public void Register_GetCommand_ByNameAndAlias()
    {
        var manager = new CommandManager();
        var command = new TestCommand("greet", aliases: new[] { "hi", "hello" });

        manager.Register(command);

        Assert.Same(command, manager.GetCommand("greet"));
        Assert.Same(command, manager.GetCommand("hi"));
        Assert.Same(command, manager.GetCommand("HELLO"));
        Assert.Same(command, Assert.Single(manager.Commands));
    }

    [Fact]
    public void Register_DuplicateName_Throws()
    {
        var manager = new CommandManager();
        manager.Register(new TestCommand("greet"));

        Assert.Throws<InvalidOperationException>(() => manager.Register(new TestCommand("greet")));
    }

    [Fact]
    public void Register_DuplicateAlias_Throws()
    {
        var manager = new CommandManager();
        manager.Register(new TestCommand("greet", aliases: new[] { "hi" }));

        Assert.Throws<InvalidOperationException>(() => manager.Register(new TestCommand("bye", aliases: new[] { "hi" })));
    }

    [Fact]
    public void Register_SelfConflictingAliases_Throws()
    {
        var manager = new CommandManager();
        Assert.Throws<InvalidOperationException>(() =>
            manager.Register(new TestCommand("greet", aliases: new[] { "greet" })));
    }

    [Fact]
    public void Unregister_RemovesWholeCommand()
    {
        var manager = new CommandManager();
        manager.Register(new TestCommand("greet", aliases: new[] { "hi" }));

        Assert.True(manager.Unregister("hi"));
        Assert.Null(manager.GetCommand("greet"));
        Assert.False(manager.Unregister("greet"));
    }

    [Fact]
    public async Task Execute_UnknownCommand_ReturnsUnknown()
    {
        var manager = new CommandManager();

        Assert.Equal(CommandResult.Unknown, await manager.ExecuteAsync("nope", new[] { "x" }));
    }

    [Fact]
    public async Task Execute_ResolvesSyntax_PopulatesContext()
    {
        var manager = new CommandManager();
        var received = new List<CommandContext>();
        manager.Register(new TestCommand(
            "set",
            executor: (ctx, ct) =>
            {
                received.Add(ctx);
                return Task.CompletedTask;
            },
            syntax: "<target> [amount]"));

        var result = await manager.ExecuteAsync("set lamp 3");

        Assert.Equal(CommandResult.Success, result);
        var ctx = Assert.Single(received);
        Assert.Equal("set", ctx.Name);
        Assert.Equal("lamp", ctx.Get("target"));
        Assert.Equal("3", ctx.Get("amount"));
        Assert.Equal(3, ctx.Get<int>("amount"));
        Assert.True(ctx.Contains("amount"));
    }

    [Fact]
    public async Task Execute_MissingRequired_ReturnsInvalidSyntax()
    {
        var manager = new CommandManager();
        manager.Register(new TestCommand("set", syntax: "<target>"));

        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("set", Array.Empty<string>()));
    }

    [Fact]
    public async Task Execute_UnknownFlagLikeToken_ReturnsInvalidSyntax()
    {
        var manager = new CommandManager();
        manager.Register(new TestCommand("set", syntax: "<target>"));

        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("set a --bogus"));
    }

    [Fact]
    public async Task Execute_TooManyArguments_ReturnsInvalidSyntax()
    {
        var manager = new CommandManager();
        manager.Register(new TestCommand("set", syntax: "<target>"));

        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("set a b"));
    }

    [Fact]
    public async Task Execute_QuotedArgument_TokenizedAsOne()
    {
        var manager = new CommandManager();
        string? target = null;
        manager.Register(new TestCommand("say", executor: (ctx, _) =>
        {
            target = ctx.Get("message");
            return Task.CompletedTask;
        }, syntax: "<message>"));

        await manager.ExecuteAsync("say \"hello world\"");

        Assert.Equal("hello world", target);
    }

    [Fact]
    public async Task Execute_ArrayArgument_ParsesBracketedList()
    {
        var manager = new CommandManager();
        string[]? items = null;
        manager.Register(new TestCommand("note", executor: (ctx, _) =>
        {
            items = ctx.Get<string[]>("items");
            return Task.CompletedTask;
        }, syntax: "<items...>"));

        await manager.ExecuteAsync("note [a,b,c]");
        Assert.Equal(new[] { "a", "b", "c" }, items);

        await manager.ExecuteAsync("note \"[a, b, c]\"");
        Assert.Equal(new[] { "a", "b", "c" }, items);

        await manager.ExecuteAsync("note single");
        Assert.Equal(new[] { "single" }, items);

        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("note a b c"));
    }

    [Fact]
    public async Task Execute_EmptyLine_ThrowsCommandSyntaxException()
    {
        var manager = new CommandManager();

        await Assert.ThrowsAsync<CommandSyntaxException>(() => manager.ExecuteAsync("   "));
    }

    [Fact]
    public async Task Execute_ExecutorThrows_ReturnsExecutorException()
    {
        var manager = new CommandManager();
        manager.Register(new TestCommand("boom", executor: (_, _) => throw new InvalidOperationException("boom")));

        Assert.Equal(CommandResult.ExecutorException,
            await manager.ExecuteAsync("boom", Array.Empty<string>()));
    }

    [Fact]
    public async Task Execute_Cancelled_ReturnsCancelled()
    {
        var manager = new CommandManager();
        manager.Register(new TestCommand("slow", executor: (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Equal(CommandResult.Cancelled,
            await manager.ExecuteAsync("slow", Array.Empty<string>(), cts.Token));
    }

    /// <summary>测试指令：可配置执行器 / 语法 / 别名。</summary>
    private sealed class TestCommand : Command
    {
        private readonly string _syntax;
        private readonly string[] _aliases;

        public TestCommand(
            string name,
            CommandExecutor? executor = null,
            string syntax = "",
            string[]? aliases = null)
            : base(name, executor)
        {
            _syntax = syntax;
            _aliases = aliases ?? Array.Empty<string>();
        }

        public override string Syntax => _syntax;

        public override IReadOnlyList<string> Aliases => _aliases;
    }
}
