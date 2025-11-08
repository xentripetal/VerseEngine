using Verse.ECS.Systems;

namespace Verse.ECS;

public sealed class Messages<T> : IEventParam, IFromWorld<Messages<T>>
	where T : notnull
{
	private List<T> lastFrameMessages = new();
	private List<T> currentFrameMessages = new();
	private int baseOffset; // Absolute offset where lastFrameMessages starts

	internal Messages()
	{
		Writer = new MessageWriter<T>(this);
	}

	public MessageWriter<T> Writer { get; }

	/// <summary>
	/// Gets the absolute offset where the last frame messages start.
	/// Messages before this offset are no longer available.
	/// </summary>
	public int BaseOffset => baseOffset;

	/// <summary>
	/// Gets the total number of messages including all available messages.
	/// </summary>
	public int TotalMessageCount => baseOffset + lastFrameMessages.Count + currentFrameMessages.Count;

	/// <summary>
	/// Creates a new independent message reader starting at the current message count.
	/// </summary>
	public MessageReader<T> CreateReader()
	{
		return new MessageReader<T>(this, TotalMessageCount);
	}

	/// <summary>
	/// Creates a new independent message reader starting at the specified absolute offset.
	/// </summary>
	public MessageReader<T> CreateReaderFrom(int absoluteOffset)
	{
		return new MessageReader<T>(this, absoluteOffset);
	}

	internal void Enqueue(T message)
	{
		currentFrameMessages.Add(message);
	}

	/// <summary>
	/// Gets a message at the specified absolute offset, or null if out of range.
	/// </summary>
	internal T? GetAt(int absoluteOffset)
	{
		// Before all available messages
		if (absoluteOffset < baseOffset)
			return default;

		var localOffset = absoluteOffset - baseOffset;

		// In last frame messages
		if (localOffset < lastFrameMessages.Count)
			return lastFrameMessages[localOffset];

		// In current frame messages
		var currentOffset = localOffset - lastFrameMessages.Count;
		if (currentOffset < currentFrameMessages.Count)
			return currentFrameMessages[currentOffset];

		// Beyond available messages
		return default;
	}

	internal IEnumerable<T> PeekFrom(int absoluteOffset)
	{
		// Clamp to valid range
		if (absoluteOffset < baseOffset)
			absoluteOffset = baseOffset;

		var localOffset = absoluteOffset - baseOffset;

		// Read from last frame messages
		for (int i = Math.Max(0, localOffset); i < lastFrameMessages.Count; i++)
		{
			yield return lastFrameMessages[i];
		}

		// Read from current frame messages
		var currentStart = Math.Max(0, localOffset - lastFrameMessages.Count);
		for (int i = currentStart; i < currentFrameMessages.Count; i++)
		{
			yield return currentFrameMessages[i];
		}
	}

	internal int MessageCount => lastFrameMessages.Count + currentFrameMessages.Count;

	public void Clear()
	{
		// Swap lists: old last frame is discarded, current frame becomes last frame
		var temp = lastFrameMessages;
		lastFrameMessages = currentFrameMessages;
		currentFrameMessages = temp;

		// Update base offset to point to the start of new last frame
		baseOffset += temp.Count; // Add the count of messages we're discarding

		// Clear the list that will become the new current frame
		currentFrameMessages.Clear();
	}

	public static Messages<T> FromWorld(World world)
	{
		var existing = world.GetResource<Messages<T>>();
		if (existing != null)
			return existing;
		var messages = new Messages<T>();
		world.InsertResource(messages);
		world.EventRegistry.Register(messages);
		return messages;
	}
}

public sealed class MessageWriter<T> : ISystemParam, IFromWorld<MessageWriter<T>>
	where T : notnull
{
	private readonly Messages<T> messagesResource;

	internal MessageWriter(Messages<T> messages)
	{
		messagesResource = messages;
	}

	public bool IsEmpty
		=> messagesResource.MessageCount == 0;

	public void Clear()
		=> messagesResource.Clear();

	public void Enqueue(T ev)
		=> messagesResource.Enqueue(ev);

	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredWrite(world.ResourceId<Messages<T>>()!.Value);
	}
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun) { }

	public static MessageWriter<T> FromWorld(World world)
	{
		return world.Resource<Messages<T>>().Writer;
	}
}

public sealed class MessageReader<T> : ISystemParam, IFromWorld<MessageReader<T>>
	where T : notnull
{
	private readonly Messages<T> messagesResource;
	private int absoluteOffset;

	internal MessageReader(Messages<T> messages, int startAbsoluteOffset = 0)
	{
		messagesResource = messages;
		absoluteOffset = startAbsoluteOffset;
	}

	public bool IsEmpty
		=> absoluteOffset >= messagesResource.TotalMessageCount;

	/// <summary>
	/// Gets all unread messages without advancing the read offset.
	/// </summary>
	public IEnumerable<T> Values => messagesResource.PeekFrom(absoluteOffset);

	/// <summary>
	/// Reads all unread messages and advances the read offset.
	/// </summary>
	public IEnumerable<T> Read()
	{
		// Clamp to base offset if we're trying to read from before available messages
		if (absoluteOffset < messagesResource.BaseOffset)
			absoluteOffset = messagesResource.BaseOffset;

		var endOffset = messagesResource.TotalMessageCount;
		while (absoluteOffset < endOffset)
		{
			var message = messagesResource.GetAt(absoluteOffset);
			absoluteOffset++;

			// GetAt returns default for invalid offsets, but we've clamped so this shouldn't happen
			// However, if T is a struct with default values, we still want to yield it
			// So we only skip if we're somehow outside the valid range (which we shouldn't be)
			yield return message!;
		}
	}

	/// <summary>
	/// Peeks at unread messages without advancing the read offset.
	/// </summary>
	public IEnumerable<T> Peek()
	{
		return messagesResource.PeekFrom(absoluteOffset);
	}

	/// <summary>
	/// Resets the reader to a specific absolute offset.
	/// </summary>
	public void ResetTo(int absoluteOffsetValue)
	{
		absoluteOffset = Math.Max(0, absoluteOffsetValue);
	}

	/// <summary>
	/// Gets the current absolute read offset.
	/// </summary>
	public int CurrentOffset => absoluteOffset;

	public void Clear()
		=> messagesResource.Clear();

	public IEnumerator<T> GetEnumerator()
		=> Read().GetEnumerator();

	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredRead(world.ResourceId<Messages<T>>()!.Value);
	}
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun) { }

	/// <summary>
	/// Creates a new independent message reader for the given world.
	/// Each call creates a new reader instance starting at the current message count.
	/// </summary>
	public static MessageReader<T> FromWorld(World world)
	{
		return world.Resource<Messages<T>>().CreateReader();
	}
}
