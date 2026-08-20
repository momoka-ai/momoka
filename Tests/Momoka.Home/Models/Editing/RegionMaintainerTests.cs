using Xunit;
using Momoka.Home;
using Momoka.Home.Editing;
using Momoka.Home.Editing.Commands;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Entities;
namespace Momoka.Home.Tests.Models.Editing;

/// <summary>Region 增量维护：结构变更前后未受影响 Region 的 Id 稳定、AffectedRegions 正确、
/// 非结构变更不重建。</summary>
public class RegionMaintainerTests
{
    [Fact]
    public void UnaffectedRegion_KeepsId_AcrossStructuralChange()
    {
        var (session, _) = Scenes.TwoRoomScene();
        var before = session.Regions.Regions!;
        var leftBefore = before.At(2, 5, 2)!;
        var rightBefore = before.At(7, 5, 2)!;
        Assert.NotEqual(leftBefore.Id, rightBefore.Id);

        // 右室内再砌一堵墙（x=7）→ 右室分裂，左室不受影响
        var changes = session.Execute(new BuildWallCommand(new[] { new WallSegment(new Int3(7, 1, 1), new Int3(1, 29, 8)) }));
        Assert.NotNull(changes);

        var after = session.Regions.Regions!;
        Assert.Equal(leftBefore.Id, after.At(2, 5, 2)!.Id); // 左室 Id 稳定
        Assert.DoesNotContain(leftBefore.Id, changes!.AffectedRegions); // 左室不在受影响集
        Assert.Contains(rightBefore.Id, changes.AffectedRegions); // 右室受影响

        Assert.Equal(3, DistinctCount(after));
        Assert.NotEqual(after.At(6, 5, 2)!.Id, after.At(8, 5, 2)!.Id); // 右室分裂为两区
    }

    [Fact]
    public void DoorOpen_Merges_DoorClose_RestoresIds()
    {
        var (session, midWall) = Scenes.TwoRoomScene();
        session.Execute(new BuildOpeningCommand(midWall, new Int3(5, 1, 4), new Int3(1, 20, 2), "door", isOpen: false));
        var closed = session.Regions.Regions!;
        var leftId = closed.At(2, 5, 2)!.Id;
        var rightId = closed.At(7, 5, 2)!.Id;

        // 开门 → 两室合并为一个 Region
        var door = session.Layout.Entities.Single(e => e.Key == new Key("door"));
        var openChanges = session.Execute(new SetPropertyCommand(door.Id, Property.IsOpen, true));
        var opened = session.Regions.Regions!;
        Assert.Single(Distinct(opened));
        Assert.Contains(openChanges!.AffectedRegions, id => id == leftId || id == rightId);

        // 关门 → 分回两室，Id 恢复
        var closeChanges = session.Execute(new SetPropertyCommand(door.Id, Property.IsOpen, false));
        var reclosed = session.Regions.Regions!;
        Assert.Equal(2, DistinctCount(reclosed));
        Assert.Equal(leftId, reclosed.At(2, 5, 2)!.Id);
        Assert.Equal(rightId, reclosed.At(7, 5, 2)!.Id);
        Assert.Contains(closeChanges!.AffectedRegions, id => id == leftId || id == rightId);
    }

    [Fact]
    public void NonStructuralChange_DoesNotRebuild()
    {
        var (session, _) = Scenes.TwoRoomScene();
        var before = session.Regions.Regions!;

        // 家具（非结构）移动 → Region 层不变（同一 Region 引用）
        var sofa = Scenes.Box("sofa", 1, 1, 1);
        var sofaId = Scenes.Place(session, sofa, new Float3(10, 10, 10));
        var moveChanges = session.Execute(new MoveEntityCommand(sofaId, new Float3(20, 10, 10)));
        Assert.NotNull(moveChanges);

        Assert.Same(before, session.Regions.Regions);
        Assert.Empty(moveChanges!.AffectedRegions);
        Assert.Equal(before.At(2, 5, 2), session.Regions.Regions!.At(2, 5, 2));
    }

    [Fact]
    public void RebuildRegions_AfterOpen_AssignsBaselineIds()
    {
        var (session, _) = Scenes.TwoRoomScene();
        var regions = session.Regions.Regions!;
        Assert.Equal(2, DistinctCount(regions));
        Assert.All(Distinct(regions), r => Assert.True(r.Id >= 1));
    }

    private static int DistinctCount(ColumnLayout<Region> regions) => Distinct(regions).Count();

    private static IEnumerable<Region> Distinct(ColumnLayout<Region> regions) =>
        regions.Cells().Select(c => c.Value).Distinct();
}
