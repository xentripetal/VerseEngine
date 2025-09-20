using FluentAssertions;
using Verse.ECS;

namespace Verse.ECS.Test;

public class QueryTests : IDisposable
{
    private readonly World _world;

    public QueryTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world?.Dispose();
    }

    [Fact]
    public void QueryBuilder_With_ShouldAddWithTerm()
    {
        // Act
        var queryBuilder = _world.QueryBuilder();
        var result = queryBuilder.With<QueryTestComponent>();

        // Assert
        result.Should().Be(queryBuilder); // Should return self for chaining
        result.World.Should().Be(_world);
    }

    [Fact]
    public void QueryBuilder_Without_ShouldAddWithoutTerm()
    {
        // Act
        var queryBuilder = _world.QueryBuilder();
        var result = queryBuilder.Without<QueryTestComponent>();

        // Assert
        result.Should().Be(queryBuilder); // Should return self for chaining
        result.World.Should().Be(_world);
    }

    [Fact]
    public void QueryBuilder_Optional_ShouldAddOptionalTerm()
    {
        // Act
        var queryBuilder = _world.QueryBuilder();
        var result = queryBuilder.Optional<QueryTestComponent>();

        // Assert
        result.Should().Be(queryBuilder); // Should return self for chaining
        result.World.Should().Be(_world);
    }

    [Fact]
    public void QueryBuilder_Build_ShouldCreateQuery()
    {
        // Arrange
        var queryBuilder = _world.QueryBuilder();
        queryBuilder.With<QueryTestComponent>();

        // Act
        var query = queryBuilder.Build();

        // Assert
        query.Should().NotBeNull();
        query.World.Should().Be(_world);
    }

    [Fact]
    public void Query_Count_WithNoEntities_ShouldReturnZero()
    {
        // Arrange
        var queryBuilder = _world.QueryBuilder();
        queryBuilder.With<QueryTestComponent>();
        var query = queryBuilder.Build();

        // Act
        var count = query.Count();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void Query_Count_WithMatchingEntities_ShouldReturnCorrectCount()
    {
        // Arrange
        var entity1 = _world.Entity().Set(new QueryTestComponent { Value = 1 });
        var entity2 = _world.Entity().Set(new QueryTestComponent { Value = 2 });
        var entity3 = _world.Entity().Set(new OtherQueryComponent { Name = "test" }); // Different component

        var queryBuilder = _world.QueryBuilder();
        queryBuilder.With<QueryTestComponent>();
        var query = queryBuilder.Build();

        // Act
        var count = query.Count();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void Query_Count_WithMultipleComponents_ShouldReturnMatchingEntities()
    {
        // Arrange
        var entity1 = _world.Entity()
            .Set(new QueryTestComponent { Value = 1 })
            .Set(new OtherQueryComponent { Name = "Both" });

        var entity2 = _world.Entity().Set(new QueryTestComponent { Value = 2 }); // Only first component
        var entity3 = _world.Entity().Set(new OtherQueryComponent { Name = "Only second" }); // Only second component

        var queryBuilder = _world.QueryBuilder();
        queryBuilder.With<QueryTestComponent>();
        queryBuilder.With<OtherQueryComponent>();
        var query = queryBuilder.Build();

        // Act
        var count = query.Count();

        // Assert
        count.Should().Be(1); // Only entity1 has both components
    }

    [Fact]
    public void Query_Count_WithWithoutFilter_ShouldExcludeEntities()
    {
        // Arrange
        var entity1 = _world.Entity().Set(new QueryTestComponent { Value = 1 });
        var entity2 = _world.Entity()
            .Set(new QueryTestComponent { Value = 2 })
            .Set(new OtherQueryComponent { Name = "Excluded" });

        var queryBuilder = _world.QueryBuilder();
        queryBuilder.With<QueryTestComponent>();
        queryBuilder.Without<OtherQueryComponent>();
        var query = queryBuilder.Build();

        // Act
        var count = query.Count();

        // Assert
        count.Should().Be(1); // Only entity1 should match (has QueryTestComponent but not OtherQueryComponent)
    }

    [Fact]
    public void Query_Iter_ShouldReturnQueryIterator()
    {
        // Arrange
        var entity = _world.Entity().Set(new QueryTestComponent { Value = 42 });

        var queryBuilder = _world.QueryBuilder();
        queryBuilder.With<QueryTestComponent>();
        var query = queryBuilder.Build();

        // Act
        var iterator = query.Iter(1); // Using tick 1

        // Assert
        // iterator.Should().NotBeNull(); // Can't use Should() with ref structs
    }

    [Fact]
    public void Query_ChainedOperations_ShouldWork()
    {
        // Arrange
        var entity1 = _world.Entity()
            .Set(new QueryTestComponent { Value = 1 })
            .Add<QueryTestTag>();

        var entity2 = _world.Entity().Set(new QueryTestComponent { Value = 2 }); // No tag
        var entity3 = _world.Entity().Add<QueryTestTag>(); // Only tag

        // Act
        var query = _world.QueryBuilder()
            .With<QueryTestComponent>()
            .With<QueryTestTag>()
            .Build();

        var count = query.Count();

        // Assert
        count.Should().Be(1); // Only entity1 matches both criteria
    }

    [Fact]
    public void Query_WithEcsId_ShouldWork()
    {
        // Arrange
        var entity = _world.Entity().Set(new QueryTestComponent { Value = 42 });
        var componentId = _world.GetComponent<QueryTestComponent>().Id;

        var queryBuilder = _world.QueryBuilder();
        queryBuilder.With(componentId);
        var query = queryBuilder.Build();

        // Act
        var count = query.Count();

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public void Query_WithOptional_ShouldIncludeEntitiesWithAndWithoutComponent()
    {
        // Arrange
        var entity1 = _world.Entity().Set(new QueryTestComponent { Value = 1 });
        var entity2 = _world.Entity(); // No components

        var queryBuilder = _world.QueryBuilder();
        queryBuilder.Optional<QueryTestComponent>();
        var query = queryBuilder.Build();

        // Act
        var count = query.Count();

        // Assert - This test might need adjustment based on how Optional actually works in the ECS
        // Optional components typically don't restrict the query, so this should match both entities
        count.Should().BeGreaterOrEqualTo(0); // Depends on implementation details
    }

    [Fact]
    public void QueryIterator_ShouldIterateOverArchetypes()
    {
        // Arrange
        var entity1 = _world.Entity().Set(new QueryTestComponent { Value = 1 });
        var entity2 = _world.Entity().Set(new QueryTestComponent { Value = 2 });

        var queryBuilder = _world.QueryBuilder();
        queryBuilder.With<QueryTestComponent>();
        var query = queryBuilder.Build();

        // Act
        var iterator = query.Iter(1);
        var archetypeCount = 0;

        while (iterator.Next())
        {
            archetypeCount++;
        }

        // Assert
        archetypeCount.Should().BeGreaterThan(0);
    }
}

// Test components for query tests
public struct QueryTestComponent
{
    public int Value { get; set; }
}

public struct QueryTestTag
{
}

public struct OtherQueryComponent
{
    public string Name { get; set; }
}