using Verse.ECS;
using Verse.Render.Graph.RenderPhase;
using Verse.Render.View;

namespace Verse.Render.Tests;

/// <summary>
/// Tests for the render phase system.
/// </summary>
public class RenderPhaseTests
{
    /// <summary>
    /// Test binned render phase basic functionality.
    /// </summary>
    [Fact]
    public void BinnedRenderPhase_BasicFunctionality_Works()
    {
        // Arrange
        var phase = new BinnedRenderPhase<TestBinnedPhaseItem>();
        var batchSetKey = new TestBatchSetKey(true);
        var binKey = new TestBinKey(1);
        var entity = new EntityView(null!, 123);
        var mainEntity = new MainEntity(456);
        var uniformIndex = new InputUniformIndex(10);

        // Act
        phase.Add(batchSetKey, binKey, entity, mainEntity, uniformIndex, BinnedRenderPhaseType.BatchableMesh);

        // Assert
        Assert.False(phase.IsEmpty);
        Assert.Single(phase.BatchableMeshes);
        Assert.True(phase.BatchableMeshes.ContainsKey((batchSetKey, binKey)));

        var bin = phase.BatchableMeshes[(batchSetKey, binKey)];
        Assert.Single(bin.Entities);
        Assert.True(bin.Entities.ContainsKey(mainEntity));
        Assert.Equal(uniformIndex, bin.Entities[mainEntity]);
    }

    /// <summary>
    /// Test that prepare for new frame clears the phase.
    /// </summary>
    [Fact]
    public void BinnedRenderPhase_PrepareForNewFrame_ClearsPhase()
    {
        // Arrange
        var phase = new BinnedRenderPhase<TestBinnedPhaseItem>();
        var batchSetKey = new TestBatchSetKey(true);
        var binKey = new TestBinKey(1);
        var entity = new EntityView(null!, 123);
        var mainEntity = new MainEntity(456);
        var uniformIndex = new InputUniformIndex(10);

        phase.Add(batchSetKey, binKey, entity, mainEntity, uniformIndex, BinnedRenderPhaseType.BatchableMesh);
        Assert.False(phase.IsEmpty);

        // Act
        phase.PrepareForNewFrame();

        // Assert
        Assert.True(phase.IsEmpty);
        Assert.Empty(phase.BatchableMeshes);
        Assert.Empty(phase.MultidrawableMeshes);
        Assert.Empty(phase.UnbatchableMeshes);
        Assert.Empty(phase.NonMeshItems);
    }

    /// <summary>
    /// Test view binned render phases functionality.
    /// </summary>
    [Fact]
    public void ViewBinnedRenderPhases_PrepareForNewFrame_Works()
    {
        // Arrange
        var viewPhases = new ViewBinnedRenderPhases<TestBinnedPhaseItem>();
        var viewEntity = new RetainedViewEntity(new MainEntity(1), new MainEntity(2), 0);

        // Act
        viewPhases.PrepareForNewFrame(viewEntity);

        // Assert
        Assert.Single(viewPhases);
        Assert.True(viewPhases.ContainsKey(viewEntity));
        Assert.True(viewPhases[viewEntity].IsEmpty);
    }

    /// <summary>
    /// Test sorted render phase basic functionality.
    /// </summary>
    [Fact]
    public void SortedRenderPhase_BasicFunctionality_Works()
    {
        // Arrange
        var phase = new SortedRenderPhase<TestSortedPhaseItem>();
        var entity1 = new EntityView(null!, 123);
        var entity2 = new EntityView(null!, 124);
        var mainEntity1 = new MainEntity(456);
        var mainEntity2 = new MainEntity(457);

        var item1 = new TestSortedPhaseItem(entity1, mainEntity1, 2.0f);
        var item2 = new TestSortedPhaseItem(entity2, mainEntity2, 1.0f);

        // Act
        phase.Add(item1);
        phase.Add(item2);

        // Assert
        Assert.Equal(2, phase.Count);
        Assert.False(phase.IsEmpty);

        // Test sorting
        phase.Sort();
        Assert.Equal(item2, phase.Items[0]); // Lower sort key should come first
        Assert.Equal(item1, phase.Items[1]);
    }

    /// <summary>
    /// Test view sorted render phases functionality.
    /// </summary>
    [Fact]
    public void ViewSortedRenderPhases_InsertOrClear_Works()
    {
        // Arrange
        var viewPhases = new ViewSortedRenderPhases<TestSortedPhaseItem>();
        var viewEntity = new RetainedViewEntity(new MainEntity(1), new MainEntity(2), 0);
        var entity = new EntityView(null!, 123);
        var mainEntity = new MainEntity(456);
        var item = new TestSortedPhaseItem(entity, mainEntity, 1.0f);

        // Act - First call creates new phase
        viewPhases.InsertOrClear(viewEntity);
        viewPhases[viewEntity].Add(item);

        Assert.Single(viewPhases);
        Assert.False(viewPhases[viewEntity].IsEmpty);

        // Act - Second call clears existing phase
        viewPhases.InsertOrClear(viewEntity);

        // Assert
        Assert.Single(viewPhases);
        Assert.True(viewPhases[viewEntity].IsEmpty);
    }

    /// <summary>
    /// Test render phase extensions utility methods.
    /// </summary>
    [Fact]
    public void RenderPhaseExtensions_GetMeshPhaseType_ReturnsCorrectTypes()
    {
        // Act & Assert
        Assert.Equal(BinnedRenderPhaseType.MultidrawableMesh,
            RenderPhaseExtensions.GetMeshPhaseType(batchable: true, supportsMultiDraw: true));

        Assert.Equal(BinnedRenderPhaseType.BatchableMesh,
            RenderPhaseExtensions.GetMeshPhaseType(batchable: true, supportsMultiDraw: false));

        Assert.Equal(BinnedRenderPhaseType.UnbatchableMesh,
            RenderPhaseExtensions.GetMeshPhaseType(batchable: false, supportsMultiDraw: true));

        Assert.Equal(BinnedRenderPhaseType.UnbatchableMesh,
            RenderPhaseExtensions.GetMeshPhaseType(batchable: false, supportsMultiDraw: false));
    }

    /// <summary>
    /// Test phase item extra index creation methods.
    /// </summary>
    [Fact]
    public void PhaseItemExtraIndex_CreationMethods_Work()
    {
        // Act & Assert
        var none = PhaseItemExtraIndex.MaybeDynamicOffset(null);
        Assert.IsType<PhaseItemExtraIndex.None>(none);

        var dynamicOffset = PhaseItemExtraIndex.MaybeDynamicOffset(42);
        Assert.IsType<PhaseItemExtraIndex.DynamicOffset>(dynamicOffset);
        var offset = (PhaseItemExtraIndex.DynamicOffset)dynamicOffset;
        Assert.Equal(42u, offset.Offset);

        var indirectNone = PhaseItemExtraIndex.MaybeIndirectParametersIndex(null);
        Assert.IsType<PhaseItemExtraIndex.None>(indirectNone);

        var indirect = PhaseItemExtraIndex.MaybeIndirectParametersIndex(5);
        Assert.IsType<PhaseItemExtraIndex.IndirectParametersIndex>(indirect);
        var indirectParams = (PhaseItemExtraIndex.IndirectParametersIndex)indirect;
        Assert.Equal(new Range(5, 6), indirectParams.Range);
    }
}

/// <summary>
/// Test implementation of IBinnedPhaseItem for unit tests.
/// </summary>
internal record TestBinnedPhaseItem(
    EntityView Entity,
    MainEntity MainEntity,
    string DrawFunction,
    Range BatchRange,
    PhaseItemExtraIndex ExtraIndex,
    IPhaseItemBatchSetKey BatchSetKey,
    IComparable BinKey) : IBinnedPhaseItem
{
    ref Range IPhaseItem.BatchRange => ref Unsafe.AsRef(in BatchRange);
}

/// <summary>
/// Test implementation of ISortedPhaseItem for unit tests.
/// </summary>
internal record TestSortedPhaseItem(
    EntityView Entity,
    MainEntity MainEntity,
    float SortValue) : ISortedPhaseItem
{
    public string DrawFunction => "test_draw";
    public Range BatchRange { get; } = new(0, 1);
    public PhaseItemExtraIndex ExtraIndex { get; } = new PhaseItemExtraIndex.None();
    public IComparable SortKey => SortValue;
    public bool Indexed => false;

    ref Range IPhaseItem.BatchRange => ref Unsafe.AsRef(in BatchRange);
}

/// <summary>
/// Test implementation of IPhaseItemBatchSetKey for unit tests.
/// </summary>
internal record TestBatchSetKey(bool Indexed) : IPhaseItemBatchSetKey
{
    public int CompareTo(IPhaseItemBatchSetKey? other) =>
        other is TestBatchSetKey key ? Indexed.CompareTo(key.Indexed) : 1;

    public bool Equals(IPhaseItemBatchSetKey? other) =>
        other is TestBatchSetKey key && Indexed == key.Indexed;

    public override int GetHashCode() => Indexed.GetHashCode();
}

/// <summary>
/// Test implementation of IComparable for bin keys.
/// </summary>
internal record TestBinKey(int Value) : IComparable
{
    public int CompareTo(object? obj) =>
        obj is TestBinKey other ? Value.CompareTo(other.Value) : 1;
}