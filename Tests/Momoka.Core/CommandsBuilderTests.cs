using Xunit;
using Momoka.Core.Commands;
using Momoka.Core.Commands.Arguments;

namespace Momoka.Core.Tests;

/// <summary>Commands 构建器（Minestom 风格）：链式构建 / 类型化参数 / 字面量 / 数值区间 /
/// 可选缺省 / 多语法 / 默认执行器 / 子命令 / 结构校验。</summary>
public sealed class CommandsBuilderTests
{
    [Fact]
    public async Task Builder_Execute_PopulatesTypedContext()
    {
        var manager = new CommandManager();
        var target = ArgumentType.String("target");
        var amount = ArgumentType.Integer("amount");
        CommandContext? seen = null;
        var command = new CommandBuilder("set")
            .Aliases("s")
            .Description("set target amount")
            .Syntax((ctx, _) =>
            {
                seen = ctx;
                return Task.CompletedTask;
            }, target, amount)
            .Build();
        manager.Register(command);

        var result = await manager.ExecuteAsync("set lamp 42");

        Assert.Equal(CommandResult.Success, result);
        Assert.NotNull(seen);
        Assert.Equal("lamp", seen!.Get(target));
        Assert.Equal(42, seen.Get(amount));
        Assert.Equal(42, seen.Get<int>(amount));
        Assert.Equal(42, seen.Get<int>("amount"));
        Assert.Equal("42", seen.Get("amount"));
        Assert.True(seen.Contains("amount"));
        Assert.Equal("set", seen.Name);
        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("s lamp 1"));
    }

    [Fact]
    public async Task Builder_LiteralArgument_MatchesExactToken()
    {
        var manager = new CommandManager();
        var executed = 0;
        manager.Register(new CommandBuilder("perm")
            .Syntax((_, _) =>
            {
                executed++;
                return Task.CompletedTask;
            }, ArgumentType.Literal("set"), ArgumentType.Integer("level"))
            .Build());

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("perm set 3"));
        Assert.Equal(1, executed);
        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("perm get 3"));
        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("perm set x"));
        Assert.Equal(1, executed);
    }

    [Fact]
    public async Task Builder_IntegerRange_RejectsOutOfRange()
    {
        var manager = new CommandManager();
        var executed = 0;
        manager.Register(new CommandBuilder("heat")
            .Syntax((_, _) =>
            {
                executed++;
                return Task.CompletedTask;
            }, ArgumentType.Integer("power").Min(1).Max(5))
            .Build());

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("heat 3"));
        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("heat 7"));
        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("heat zero"));
        Assert.Equal(1, executed);
    }

    [Fact]
    public async Task Builder_BooleanAndEnumArguments()
    {
        var manager = new CommandManager();
        var toggle = ArgumentType.Boolean("toggle");
        var mode = ArgumentType.Enum<Mode>("mode");
        bool? toggleValue = null;
        Mode? modeValue = null;
        manager.Register(new CommandBuilder("light")
            .Syntax((ctx, _) =>
            {
                toggleValue = ctx.Get(toggle);
                modeValue = ctx.Get(mode);
                return Task.CompletedTask;
            }, toggle, mode)
            .Build());

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("light true On"));
        Assert.True(toggleValue);
        Assert.Equal(Mode.On, modeValue);

        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("light maybe On"));
        Assert.Equal(CommandResult.InvalidSyntax, await manager.ExecuteAsync("light true Auto"));
    }

    [Fact]
    public async Task Builder_OptionalArgument_AppliesDefault()
    {
        var manager = new CommandManager();
        int? value = null;
        manager.Register(new CommandBuilder("vol")
            .Syntax((ctx, _) =>
            {
                value = ctx.Get<int>("level");
                return Task.CompletedTask;
            }, ArgumentType.Integer("level").WithDefaultValue(50))
            .Build());

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("vol 30"));
        Assert.Equal(30, value);
        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("vol"));
        Assert.Equal(50, value);
    }

    [Fact]
    public async Task Builder_MultipleSyntaxes_DispatchesToMatching()
    {
        var manager = new CommandManager();
        var executed = new List<string>();
        manager.Register(new CommandBuilder("calc")
            .Syntax((ctx, _) =>
            {
                executed.Add("add:" + ctx.Get<int>("a") + ctx.Get<int>("b"));
                return Task.CompletedTask;
            }, ArgumentType.Literal("add"), ArgumentType.Integer("a"), ArgumentType.Integer("b"))
            .Syntax((ctx, _) =>
            {
                executed.Add("mul:" + ctx.Get<int>("a") * ctx.Get<int>("b"));
                return Task.CompletedTask;
            }, ArgumentType.Literal("mul"), ArgumentType.Integer("a"), ArgumentType.Integer("b"))
            .Build());

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("calc add 2 3"));
        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("calc mul 2 3"));

        Assert.Equal(new[] { "add:23", "mul:6" }, executed);
    }

    [Fact]
    public async Task Builder_DefaultExecutor_RunsWhenNoSyntaxMatches()
    {
        var manager = new CommandManager();
        var defaultCalls = 0;
        manager.Register(new CommandBuilder("list")
            .Syntax((_, _) => Task.CompletedTask, ArgumentType.String("filter"))
            .DefaultExecutor((_, _) =>
            {
                defaultCalls++;
                return Task.CompletedTask;
            })
            .Build());

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("list all"));
        Assert.Equal(0, defaultCalls);
        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("list"));
        Assert.Equal(1, defaultCalls);
        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("list a b"));
        Assert.Equal(2, defaultCalls);
    }

    [Fact]
    public async Task Builder_BooleanAndArrayArguments()
    {
        var manager = new CommandManager();
        var toggle = ArgumentType.Boolean("toggle");
        var text = ArgumentType.StringArray("text");
        bool? flag = null;
        string[]? items = null;
        manager.Register(new CommandBuilder("echo")
            .Syntax((ctx, _) =>
            {
                flag = ctx.Get(toggle);
                items = ctx.Get(text);
                return Task.CompletedTask;
            }, toggle, text)
            .Build());

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("echo true [a,b]"));
        Assert.True(flag);
        Assert.Equal(new[] { "a", "b" }, items);

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("echo false x"));
        Assert.False(flag);
        Assert.Equal(new[] { "x" }, items);
    }

    [Fact]
    public async Task Builder_Subcommand_Dispatches()
    {
        var manager = new CommandManager();
        string? created = null;
        var sub = new CommandBuilder("create")
            .Syntax((ctx, _) =>
            {
                created = ctx.Get<string>("name");
                return Task.CompletedTask;
            }, ArgumentType.String("name"))
            .Build();
        var parent = new CommandBuilder("user")
            .Subcommand(sub)
            .DefaultExecutor((_, _) => Task.CompletedTask)
            .Build();
        manager.Register(parent);

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("user create alice"));
        Assert.Equal("alice", created);

        Assert.Equal(CommandResult.Success, await manager.ExecuteAsync("user delete alice"));
    }

    [Fact]
    public void Builder_DuplicateArgumentId_Throws()
    {
        Assert.Throws<IllegalCommandStructureException>(() =>
            new CommandBuilder("x")
                .Syntax((_, _) => Task.CompletedTask, ArgumentType.String("id"), ArgumentType.Integer("id")));
    }

    [Fact]
    public void Builder_OptionalFollowedByRequired_Throws()
    {
        Assert.Throws<IllegalCommandStructureException>(() =>
            new CommandBuilder("x")
                .Syntax((_, _) => Task.CompletedTask,
                    ArgumentType.String("a").WithDefaultValue(string.Empty), ArgumentType.String("b")));
    }

    [Fact]
    public void Builder_InvalidFormat_Throws()
    {
        Assert.Throws<CommandSyntaxException>(() =>
            new CommandBuilder("x").Syntax("bad <token", (_, _) => Task.CompletedTask));
    }

    private enum Mode
    {
        Off,
        On,
    }
}
