using FluentAssertions;
using Verse.ECS.Systems;

namespace Verse.ECS.Test;

public class MessagesTests : EcsTestBase
{

	[Fact]
	public void Messages_CreateReader_ShouldStartAtCurrentMessageCount()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });

		// Act
		var reader = messages.CreateReader();

		// Assert
		reader.IsEmpty.Should().BeTrue();
		reader.CurrentOffset.Should().Be(2);
	}

	[Fact]
	public void Messages_CreateReaderFrom_ShouldStartAtSpecifiedOffset()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		messages.Writer.Enqueue(new TestMessage { Value = 3 });

		// Act
		var reader = messages.CreateReaderFrom(1);

		// Assert
		reader.IsEmpty.Should().BeFalse();
		var readMessages = reader.Read().ToList();
		readMessages.Should().HaveCount(2);
		readMessages[0].Value.Should().Be(2);
		readMessages[1].Value.Should().Be(3);
	}

	[Fact]
	public void MessageWriter_Enqueue_ShouldAddMessage()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		var writer = messages.Writer;

		// Act
		writer.Enqueue(new TestMessage { Value = 42 });

		// Assert
		writer.IsEmpty.Should().BeFalse();
	}

	[Fact]
	public void MessageWriter_IsEmpty_ShouldReturnCorrectState()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		var writer = messages.Writer;

		// Assert - Initially empty
		writer.IsEmpty.Should().BeTrue();

		// Act - Add message
		writer.Enqueue(new TestMessage { Value = 1 });

		// Assert - Not empty
		writer.IsEmpty.Should().BeFalse();
	}

	[Fact]
	public void MessageReader_Read_ShouldAdvanceOffset()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		var reader = messages.CreateReaderFrom(0);

		// Act
		var firstRead = reader.Read().ToList();
		var secondRead = reader.Read().ToList();

		// Assert
		firstRead.Should().HaveCount(2);
		secondRead.Should().BeEmpty(); // All messages already read
		reader.CurrentOffset.Should().Be(2);
	}

	[Fact]
	public void MessageReader_Peek_ShouldNotAdvanceOffset()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		var reader = messages.CreateReaderFrom(0);
		var initialOffset = reader.CurrentOffset;

		// Act
		var firstPeek = reader.Peek().ToList();
		var secondPeek = reader.Peek().ToList();

		// Assert
		firstPeek.Should().HaveCount(2);
		secondPeek.Should().HaveCount(2);
		firstPeek.Should().BeEquivalentTo(secondPeek);
		reader.CurrentOffset.Should().Be(initialOffset);
	}

	[Fact]
	public void MessageReader_Values_ShouldNotAdvanceOffset()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		var reader = messages.CreateReaderFrom(0);
		var initialOffset = reader.CurrentOffset;

		// Act
		var values = reader.Values.ToList();

		// Assert
		values.Should().HaveCount(2);
		reader.CurrentOffset.Should().Be(initialOffset);
	}

	[Fact]
	public void MessageReader_ResetTo_ShouldSetOffset()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		messages.Writer.Enqueue(new TestMessage { Value = 3 });
		var reader = messages.CreateReaderFrom(0);
		reader.Read().ToList(); // Read all messages

		// Act
		reader.ResetTo(1);

		// Assert
		reader.CurrentOffset.Should().Be(1);
		var messages2 = reader.Read().ToList();
		messages2.Should().HaveCount(2);
		messages2[0].Value.Should().Be(2);
	}

	[Fact]
	public void MessageReader_IsEmpty_ShouldReflectReadState()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		var reader = messages.CreateReaderFrom(0);

		// Assert - Has unread messages
		reader.IsEmpty.Should().BeFalse();

		// Act - Read all messages
		reader.Read().ToList();

		// Assert - No more unread messages
		reader.IsEmpty.Should().BeTrue();
	}

	[Fact]
	public void MessageReader_GetEnumerator_ShouldIterateAndAdvanceOffset()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		var reader = messages.CreateReaderFrom(0);

		// Act
		var count = 0;
		foreach (var msg in reader)
		{
			count++;
		}

		// Assert
		count.Should().Be(2);
		reader.IsEmpty.Should().BeTrue();
	}

	[Fact]
	public void Messages_Clear_ShouldSwapFrames()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });

		// Act - First clear: current frame becomes last frame
		messages.Clear();
		messages.Writer.Enqueue(new TestMessage { Value = 3 });

		// Assert - Reader starting from 0 should see all messages (last frame + current frame)
		var reader = messages.CreateReaderFrom(0);
		var allMessages = reader.Read().ToList();
		allMessages.Should().HaveCount(3);
		allMessages[0].Value.Should().Be(1);
		allMessages[1].Value.Should().Be(2);
		allMessages[2].Value.Should().Be(3);

		// Act - Second clear: old last frame is discarded, current frame becomes last frame
		messages.Clear();

		// Assert - Reader starting from 0 can only see messages from offset 2 onwards
		var reader2 = messages.CreateReaderFrom(0);
		var remainingMessages = reader2.Read().ToList();
		remainingMessages.Should().HaveCount(1);
		remainingMessages[0].Value.Should().Be(3);
	}

	[Fact]
	public void Messages_Clear_ShouldUpdateBaseOffset()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });

		// Act - First clear
		messages.Clear();
		var baseOffsetAfterFirstClear = messages.BaseOffset;

		// Assert
		baseOffsetAfterFirstClear.Should().Be(0);

		// Act - Second clear (should discard first 2 messages)
		messages.Clear();
		var baseOffsetAfterSecondClear = messages.BaseOffset;

		// Assert
		baseOffsetAfterSecondClear.Should().Be(2);
	}

	[Fact]
	public void Messages_IndependentReaders_ShouldTrackSeparately()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		messages.Writer.Enqueue(new TestMessage { Value = 3 });

		var reader1 = messages.CreateReaderFrom(0);
		var reader2 = messages.CreateReaderFrom(1);

		// Act
		var reader1Messages = reader1.Read().ToList();
		var reader2Messages = reader2.Read().ToList();

		// Assert
		reader1Messages.Should().HaveCount(3);
		reader2Messages.Should().HaveCount(2);
		reader1.CurrentOffset.Should().Be(3);
		reader2.CurrentOffset.Should().Be(3);
	}

	[Fact]
	public void Messages_ReaderCatchUp_ShouldReadMissedMessages()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		var reader = messages.CreateReader(); // Starts at 0
		var initialOffset = reader.CurrentOffset;

		// Act - Add messages after reader creation
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		messages.Writer.Enqueue(new TestMessage { Value = 3 });

		// Assert - Reader should be able to read newly added messages
		reader.IsEmpty.Should().BeFalse();
		var missedMessages = reader.Read().ToList();
		missedMessages.Should().HaveCount(3);
	}

	[Fact]
	public void Messages_ReadAcrossFrames_ShouldAccessLastAndCurrentFrame()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		var reader = messages.CreateReaderFrom(0);

		// Act - Clear to move to next frame
		messages.Clear();
		messages.Writer.Enqueue(new TestMessage { Value = 3 });
		messages.Writer.Enqueue(new TestMessage { Value = 4 });

		// Assert - Reader should see both last frame and current frame
		var allMessages = reader.Read().ToList();
		allMessages.Should().HaveCount(4);
		allMessages[0].Value.Should().Be(1);
		allMessages[1].Value.Should().Be(2);
		allMessages[2].Value.Should().Be(3);
		allMessages[3].Value.Should().Be(4);
	}

	[Fact]
	public void Messages_ReadAfterTwoFrames_ShouldOnlyAccessAvailableMessages()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 }); // Frame 0
		messages.Clear(); // Move to Frame 1
		messages.Writer.Enqueue(new TestMessage { Value = 2 }); // Frame 1
		messages.Clear(); // Move to Frame 2, Frame 0 is now discarded
		messages.Writer.Enqueue(new TestMessage { Value = 3 }); // Frame 2

		// Act - Reader starting from 0 should only see messages from Frame 1 onwards
		var reader = messages.CreateReaderFrom(0);

		// Assert - Frame 0 messages are gone, only Frame 1 and 2 available
		var availableMessages = reader.Read().ToList();
		availableMessages.Should().HaveCount(2);
		availableMessages[0].Value.Should().Be(2);
		availableMessages[1].Value.Should().Be(3);
	}

	[Fact]
	public void Messages_FromWorld_ShouldReturnSameInstance()
	{
		// Act
		var messages1 = Messages<TestMessage>.FromWorld(World);
		var messages2 = Messages<TestMessage>.FromWorld(World);

		// Assert
		messages1.Should().BeSameAs(messages2);
	}

	[Fact]
	public void Messages_TotalMessageCount_ShouldIncludeBaseOffset()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		messages.Clear(); // BaseOffset remains 0
		messages.Writer.Enqueue(new TestMessage { Value = 3 });
		messages.Clear(); // BaseOffset becomes 2

		// Act
		messages.Writer.Enqueue(new TestMessage { Value = 4 });

		// Assert
		messages.TotalMessageCount.Should().Be(4); // BaseOffset(2) + lastFrame(1) + currentFrame(1)
	}

	[Fact]
	public void MessageReader_FromWorld_ShouldCreateNewReader()
	{
		// Arrange
		World.InsertResource(Messages<TestMessage>.FromWorld(World));

		// Act
		var reader1 = MessageReader<TestMessage>.FromWorld(World);
		var reader2 = MessageReader<TestMessage>.FromWorld(World);

		// Assert
		reader1.Should().NotBeSameAs(reader2);
	}

	[Fact]
	public void MessageWriter_FromWorld_ShouldReturnSameWriter()
	{
		// Arrange - Ensure the Messages resource exists
		Messages<TestMessage>.FromWorld(World);

		// Act
		var writer1 = MessageWriter<TestMessage>.FromWorld(World);
		var writer2 = MessageWriter<TestMessage>.FromWorld(World);

		// Assert
		writer1.Should().BeSameAs(writer2);
	}

	[Fact]
	public void Messages_MultipleMessageTypes_ShouldBeIndependent()
	{
		// Arrange
		var messages1 = Messages<TestMessage>.FromWorld(World);
		var messages2 = Messages<OtherMessage>.FromWorld(World);

		// Act
		messages1.Writer.Enqueue(new TestMessage { Value = 1 });
		messages2.Writer.Enqueue(new OtherMessage { Text = "Hello" });

		// Assert
		var reader1 = messages1.CreateReaderFrom(0);
		var reader2 = messages2.CreateReaderFrom(0);

		var msg1 = reader1.Read().ToList();
		var msg2 = reader2.Read().ToList();

		msg1.Should().HaveCount(1);
		msg2.Should().HaveCount(1);
		msg1[0].Value.Should().Be(1);
		msg2[0].Text.Should().Be("Hello");
	}

	[Fact]
	public void Messages_ReaderBeyondAvailable_ShouldReturnEmpty()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });

		// Act - Create reader starting beyond available messages
		var reader = messages.CreateReaderFrom(100);

		// Assert
		reader.IsEmpty.Should().BeTrue();
		var readMessages = reader.Read().ToList();
		readMessages.Should().BeEmpty();
	}

	[Fact]
	public void Messages_ReaderBeforeAvailable_ShouldClampToBaseOffset()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		messages.Writer.Enqueue(new TestMessage { Value = 2 });
		messages.Clear(); // Move to frame 1
		messages.Writer.Enqueue(new TestMessage { Value = 3 });
		messages.Clear(); // Move to frame 2, discard frame 0, BaseOffset = 2

		// Act - Create reader starting before base offset
		var reader = messages.CreateReaderFrom(0);

		// Assert - Should clamp to base offset and read from there
		var readMessages = reader.Read().ToList();
		readMessages.Should().HaveCount(1);
		readMessages[0].Value.Should().Be(3);
	}

	[Fact]
	public void Messages_ReaderResetToNegative_ShouldClampToZero()
	{
		// Arrange
		var messages = Messages<TestMessage>.FromWorld(World);
		messages.Writer.Enqueue(new TestMessage { Value = 1 });
		var reader = messages.CreateReaderFrom(0);

		// Act
		reader.ResetTo(-10);

		// Assert
		reader.CurrentOffset.Should().Be(0);
	}
}

// Test message types
public struct TestMessage
{
	public int Value { get; set; }
}

public struct OtherMessage
{
	public string Text { get; set; }
}
