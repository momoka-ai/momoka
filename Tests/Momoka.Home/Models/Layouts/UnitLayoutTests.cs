using Xunit;
using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// Checks the unit layout as the fully-3D multi-layer spatial root: entities
/// live in the single root space; placement surfaces come from the per-layer
/// floor plans' partition faces plus every entity's VoxelLayoutSource
/// component; the whole space composes upward via IVoxelGeometry3D.
/// </summary>
public class UnitLayoutTests
{
    private sealed class VoxelLayoutSourceEntity : Entity<Int3>
    {
        public VoxelLayoutSourceEntity()
        {
            Volume = new Box3D();
            AddComponent(new VoxelLayoutSource { Layouts = { new VoxelLayout2D(new Int2(2, 2)) } });
        }
    }

    [Fact]
    public void Entities_ReflectsTheRootSpace()
    {
        var unit = new UnitLayout();
        var wall = new Wall();
        unit.Layout.BuildAt(wall, new Int3(2, 0, 0));

        var registered = Assert.Single(unit.Entities);
        Assert.Equal(wall, registered);
    }

    [Fact]
    public void Floors_HoldsOnePlanPerLayer()
    {
        var unit = new UnitLayout();
        unit.Floors.Add(new FloorPlanLayout());
        unit.Floors.Add(new FloorPlanLayout());

        Assert.Equal(2, unit.Floors.Count);
    }

    [Fact]
    public void Surfaces_IncludesPlanPartitionFaces()
    {
        var unit = new UnitLayout();
        var wall = new Wall();
        var plan = new FloorPlanLayout();
        plan.Build(new Int2(2, 0), new Int2(7, 0), wall);
        unit.Floors.Add(plan);
        unit.Layout.BuildAt(wall, new Int3(2, 0, 0));

        // E–W wall → south + north faces (no floor/ceiling planes in a unit layout)
        Assert.Equal(2, unit.Surfaces.Count());
    }

    [Fact]
    public void Surfaces_IncludesVoxelLayoutSourceEntities()
    {
        var unit = new UnitLayout();
        var source = new VoxelLayoutSourceEntity();
        unit.Layout.BuildAt(source, new Int3(2, 0, 0));

        var surface = source.GetComponent<VoxelLayoutSource>()!.Layouts.Single();
        Assert.Contains(surface, unit.Surfaces);
    }

    [Fact]
    public void Surfaces_Empty_WhenNothingDeclaresSurfaces()
    {
        var unit = new UnitLayout();
        Assert.Empty(unit.Surfaces);
    }

    [Fact]
    public void PlaceAt_And_DestroyAt_ComposeIntoAParent()
    {
        var unit = new UnitLayout();
        var wall = new Wall();
        var plan = new FloorPlanLayout();
        plan.Build(new Int2(2, 0), new Int2(7, 0), wall);
        unit.Floors.Add(plan);
        unit.Layout.BuildAt(wall, new Int3(2, 0, 0));

        var parent = new VoxelLayout<Entity<Int3>>();
        unit.PlaceAt(parent, new Int3(0, 30, 0));
        Assert.True(parent.HasEntity(new Int3(2, 30, 0)));
        Assert.True(parent.HasEntity(new Int3(7, 30, 0)));

        unit.DestroyAt(parent, new Int3(0, 30, 0));
        Assert.False(parent.HasEntity(new Int3(2, 30, 0)));
    }

    [Fact]
    public void Cells3D_AreRootAbsolute()
    {
        var unit = new UnitLayout();
        var wall = new Wall();
        var plan = new FloorPlanLayout();
        plan.Build(new Int2(2, 0), new Int2(7, 0), wall);
        unit.Floors.Add(plan);
        unit.Layout.BuildAt(wall, new Int3(2, 0, 0));

        var cells = unit.Cells3D().ToList();
        Assert.Contains(new Int3(2, 0, 0), cells);
        Assert.Contains(new Int3(7, 0, 0), cells);
    }
}
