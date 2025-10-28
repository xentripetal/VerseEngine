using FluentAssertions;
using Verse.ECS.Datastructures;

namespace Verse.Core.Test;

public class IndexDictionaryTests
{
    [Fact]
    public void Constructor_Default_ShouldCreateEmptyDictionary()
    {
        // Act
        var dict = new IndexDictionary<string, int>();

        // Assert
        dict.Count.Should().Be(0);
        dict.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithCapacity_ShouldCreateEmptyDictionary()
    {
        // Act
        var dict = new IndexDictionary<string, int>(10);

        // Assert
        dict.Count.Should().Be(0);
        dict.Should().BeEmpty();
    }

    [Fact]
    public void Add_NewKeyValue_ShouldAddToEnd()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();

        // Act
        dict.Add("key1", 1);
        dict.Add("key2", 2);

        // Assert
        dict.Count.Should().Be(2);
        dict.ContainsKey("key1").Should().BeTrue();
        dict.ContainsKey("key2").Should().BeTrue();
        dict["key1"].Should().Be(1);
        dict["key2"].Should().Be(2);
    }

    [Fact]
    public void Add_DuplicateKey_ShouldThrowArgumentException()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 1);

        // Act & Assert
        var act = () => dict.Add("key1", 2);
        act.Should().Throw<ArgumentException>()
           .WithMessage("An item with the same key has already been added. Key: key1");
    }

    [Fact]
    public void Indexer_SetExistingKey_ShouldUpdateValue()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 1);

        // Act
        dict["key1"] = 10;

        // Assert
        dict["key1"].Should().Be(10);
        dict.Count.Should().Be(1);
    }

    [Fact]
    public void Indexer_SetNewKey_ShouldAddEntry()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();

        // Act
        dict["key1"] = 1;

        // Assert
        dict["key1"].Should().Be(1);
        dict.Count.Should().Be(1);
        dict.ContainsKey("key1").Should().BeTrue();
    }

    [Fact]
    public void Indexer_GetNonExistentKey_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();

        // Act & Assert
        var act = () => dict["nonexistent"];
        act.Should().Throw<KeyNotFoundException>()
           .WithMessage("The given key 'nonexistent' was not present in the dictionary.");
    }

    [Fact]
    public void IndexedAccess_GetByIndex_ShouldReturnCorrectValue()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("first", 1);
        dict.Add("second", 2);
        dict.Add("third", 3);

        // Act & Assert
        dict[0].Should().Be(1);
        dict[1].Should().Be(2);
        dict[2].Should().Be(3);
    }

    [Fact]
    public void IndexedAccess_SetByIndex_ShouldUpdateValue()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("first", 1);
        dict.Add("second", 2);

        // Act
        dict[0] = 10;
        dict[1] = 20;

        // Assert
        dict[0].Should().Be(10);
        dict[1].Should().Be(20);
        dict["first"].Should().Be(10);
        dict["second"].Should().Be(20);
    }

    [Fact]
    public void IndexedAccess_OutOfRange_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 1);

        // Act & Assert
        var getAct = () => dict[1];
        getAct.Should().Throw<ArgumentOutOfRangeException>();

        var setAct = () => dict[1] = 10;
        setAct.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Remove_ExistingKey_ShouldRemoveAndShiftIndices()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("first", 1);
        dict.Add("second", 2);
        dict.Add("third", 3);

        // Act
        var removed = dict.Remove("second");

        // Assert
        removed.Should().BeTrue();
        dict.Count.Should().Be(2);
        dict.ContainsKey("second").Should().BeFalse();
        dict[0].Should().Be(1); // "first"
        dict[1].Should().Be(3); // "third" shifted down
        dict.GetIndex("first").Should().Be(0);
        dict.GetIndex("third").Should().Be(1);
    }

    [Fact]
    public void Remove_NonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 1);

        // Act
        var removed = dict.Remove("nonexistent");

        // Assert
        removed.Should().BeFalse();
        dict.Count.Should().Be(1);
    }

    [Fact]
    public void SwapRemove_ExistingKey_ShouldRemoveAndSwapLastElement()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("first", 1);
        dict.Add("second", 2);
        dict.Add("third", 3);

        // Act
        var removed = dict.SwapRemove("first");

        // Assert
        removed.Should().Be(true);
        dict.Count.Should().Be(2);
        dict.ContainsKey("first").Should().BeFalse();
        dict[0].Should().Be(3); // "third" moved to index 0
        dict[1].Should().Be(2); // "second" stays at index 1
        dict.GetIndex("third").Should().Be(0);
        dict.GetIndex("second").Should().Be(1);
    }

    [Fact]
    public void SwapRemove_LastElement_ShouldJustRemove()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("first", 1);
        dict.Add("second", 2);

        // Act
        var removed = dict.SwapRemove("second");

        // Assert
        removed.Should().Be(true);
        dict.Count.Should().Be(1);
        dict[0].Should().Be(1);
        dict.GetIndex("first").Should().Be(0);
    }

    [Fact]
    public void SwapRemove_NonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 1);

        // Act
        var removed = dict.SwapRemove("nonexistent");

        // Assert
        removed.Should().Be(false);
        dict.Count.Should().Be(1);
    }

    [Fact]
    public void TryGetValue_ExistingKey_ShouldReturnTrueAndValue()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 42);

        // Act
        var found = dict.TryGetValue("key1", out var value);

        // Assert
        found.Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public void TryGetValue_NonExistentKey_ShouldReturnFalseAndDefault()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();

        // Act
        var found = dict.TryGetValue("nonexistent", out var value);

        // Assert
        found.Should().BeFalse();
        value.Should().Be(0); // default(int)
    }

    [Fact]
    public void GetIndex_ExistingKey_ShouldReturnCorrectIndex()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("first", 1);
        dict.Add("second", 2);
        dict.Add("third", 3);

        // Act & Assert
        dict.GetIndex("first").Should().Be(0);
        dict.GetIndex("second").Should().Be(1);
        dict.GetIndex("third").Should().Be(2);
    }

    [Fact]
    public void GetIndex_NonExistentKey_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();

        // Act & Assert
        var act = () => dict.GetIndex("nonexistent");
        act.Should().Throw<KeyNotFoundException>()
           .WithMessage("The given key 'nonexistent' was not present in the dictionary.");
    }

    [Fact]
    public void TryGetIndex_ExistingKey_ShouldReturnTrueAndIndex()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 1);

        // Act
        var found = dict.TryGetIndex("key1", out var index);

        // Assert
        found.Should().BeTrue();
        index.Should().Be(0);
    }

    [Fact]
    public void TryGetIndex_NonExistentKey_ShouldReturnFalseAndNegativeOne()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();

        // Act
        var found = dict.TryGetIndex("nonexistent", out var index);

        // Assert
        found.Should().BeFalse();
        index.Should().Be(0); // out parameter gets default value
    }

    [Fact]
    public void GetByIndex_ValidIndex_ShouldReturnKeyValuePair()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 42);
        dict.Add("key2", 84);

        // Act
        var kvp0 = dict.GetByIndex(0);
        var kvp1 = dict.GetByIndex(1);

        // Assert
        kvp0.Key.Should().Be("key1");
        kvp0.Value.Should().Be(42);
        kvp1.Key.Should().Be("key2");
        kvp1.Value.Should().Be(84);
    }

    [Fact]
    public void GetKeyByIndex_ValidIndex_ShouldReturnKey()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 42);
        dict.Add("key2", 84);

        // Act & Assert
        dict.GetKeyByIndex(0).Should().Be("key1");
        dict.GetKeyByIndex(1).Should().Be("key2");
    }

    [Fact]
    public void GetValueByIndex_ValidIndex_ShouldReturnValue()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 42);
        dict.Add("key2", 84);

        // Act & Assert
        dict.GetValueByIndex(0).Should().Be(42);
        dict.GetValueByIndex(1).Should().Be(84);
    }

    [Fact]
    public void GetFull_ExistingKey_ShouldReturnKeyValueAndIndex()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("first", 1);
        dict.Add("second", 2);

        // Act
        var (key, value, index) = dict.GetFull("second");

        // Assert
        key.Should().Be("second");
        value.Should().Be(2);
        index.Should().Be(1);
    }

    [Fact]
    public void GetFull_NonExistentKey_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();

        // Act & Assert
        var act = () => dict.GetFull("nonexistent");
        act.Should().Throw<KeyNotFoundException>();
    }


    [Fact]
    public void Enumeration_ShouldPreserveInsertionOrder()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("third", 3);
        dict.Add("first", 1);
        dict.Add("second", 2);

        // Act
        var items = dict.ToList();

        // Assert
        items.Should().HaveCount(3);
        items[0].Should().Be(new KeyValuePair<string, int>("third", 3));
        items[1].Should().Be(new KeyValuePair<string, int>("first", 1));
        items[2].Should().Be(new KeyValuePair<string, int>("second", 2));
    }

    [Fact]
    public void Enumeration_AfterRemoval_ShouldMaintainOrder()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("first", 1);
        dict.Add("second", 2);
        dict.Add("third", 3);
        dict.Add("fourth", 4);

        // Act
        dict.Remove("second"); // Remove middle element

        // Assert
        var items = dict.ToList();
        items.Should().HaveCount(3);
        items[0].Should().Be(new KeyValuePair<string, int>("first", 1));
        items[1].Should().Be(new KeyValuePair<string, int>("third", 3));
        items[2].Should().Be(new KeyValuePair<string, int>("fourth", 4));
    }

    [Fact]
    public void Keys_ShouldReturnKeysInInsertionOrder()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("c", 3);
        dict.Add("a", 1);
        dict.Add("b", 2);

        // Act
        var keys = dict.Keys.ToList();

        // Assert
        keys.Should().Equal("c", "a", "b");
    }

    [Fact]
    public void Values_ShouldReturnValuesInInsertionOrder()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("c", 3);
        dict.Add("a", 1);
        dict.Add("b", 2);

        // Act
        var values = dict.Values.ToList();

        // Assert
        values.Should().Equal(3, 1, 2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 1);
        dict.Add("key2", 2);

        // Act
        dict.Clear();

        // Assert
        dict.Count.Should().Be(0);
        dict.Should().BeEmpty();
        dict.ContainsKey("key1").Should().BeFalse();
        dict.ContainsKey("key2").Should().BeFalse();
    }

    [Fact]
    public void Contains_KeyValuePair_ExistingPair_ShouldReturnTrue()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 42);
        var kvp = new KeyValuePair<string, int>("key1", 42);

        // Act
        var contains = dict.Contains(kvp);

        // Assert
        contains.Should().BeTrue();
    }

    [Fact]
    public void Contains_KeyValuePair_WrongValue_ShouldReturnFalse()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 42);
        var kvp = new KeyValuePair<string, int>("key1", 84);

        // Act
        var contains = dict.Contains(kvp);

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public void CopyTo_ValidArray_ShouldCopyInOrder()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("first", 1);
        dict.Add("second", 2);
        dict.Add("third", 3);

        var array = new KeyValuePair<string, int>[5];

        // Act
        dict.CopyTo(array, 1);

        // Assert
        array[0].Should().Be(default(KeyValuePair<string, int>));
        array[1].Should().Be(new KeyValuePair<string, int>("first", 1));
        array[2].Should().Be(new KeyValuePair<string, int>("second", 2));
        array[3].Should().Be(new KeyValuePair<string, int>("third", 3));
        array[4].Should().Be(default(KeyValuePair<string, int>));
    }

    [Fact]
    public void CopyTo_NullArray_ShouldThrowArgumentNullException()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 1);

        // Act & Assert
        var act = () => dict.CopyTo(null!, 0);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CopyTo_InsufficientSpace_ShouldThrowArgumentException()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();
        dict.Add("key1", 1);
        dict.Add("key2", 2);

        var array = new KeyValuePair<string, int>[1];

        // Act & Assert
        var act = () => dict.CopyTo(array, 0);
        act.Should().Throw<ArgumentException>()
           .WithMessage("The number of elements in the source IndexDictionary is greater than the available space from arrayIndex to the end of the destination array.");
    }

    [Fact]
    public void IsReadOnly_ShouldReturnFalse()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();

        // Act & Assert
        dict.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void ComplexScenario_MultipleOperations_ShouldMaintainConsistency()
    {
        // Arrange
        var dict = new IndexDictionary<string, int>();

        // Act: Build up dictionary
        dict.Add("a", 1);
        dict.Add("b", 2);
        dict.Add("c", 3);
        dict.Add("d", 4);
        dict.Add("e", 5);

        // Verify initial state
        dict.ToList().Should().Equal(
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("c", 3),
            new KeyValuePair<string, int>("d", 4),
            new KeyValuePair<string, int>("e", 5)
        );

        // Remove middle element
        dict.Remove("c");
        dict.ToList().Should().Equal(
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("d", 4),
            new KeyValuePair<string, int>("e", 5)
        );

        // SwapRemove first element
        dict.SwapRemove("a");
        dict.ToList().Should().Equal(
            new KeyValuePair<string, int>("e", 5), // "e" moved to index 0
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("d", 4)
        );

        // Update values
        dict["b"] = 20;
        dict["new"] = 100;

        // Final verification
        dict.Count.Should().Be(4);
        dict.GetIndex("e").Should().Be(0);
        dict.GetIndex("b").Should().Be(1);
        dict.GetIndex("d").Should().Be(2);
        dict.GetIndex("new").Should().Be(3);

        dict[0].Should().Be(5);  // "e"
        dict[1].Should().Be(20); // "b" updated
        dict[2].Should().Be(4);  // "d"
        dict[3].Should().Be(100);// "new"
    }
}