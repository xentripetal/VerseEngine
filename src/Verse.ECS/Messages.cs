using Verse.ECS.Systems;

namespace Verse.ECS;

public sealed class Messages<T> : IEventParam, IFromWorld<Messages<T>>
	where T : notnull
{
	private readonly List<T> messages = new();
	private int lastFrameStart;
	private int currentFrameStart;

	internal Messages()
	{
		Writer = new MessageWriter<T>(this);
		Reader = new MessageReader<T>(this);
	}

	public MessageWriter<T> Writer { get; }
	public MessageReader<T> Reader { get; }

	internal void Enqueue(T message)
	{
		messages.Add(message);
	}

	internal IEnumerable<T> ReadFrom(MessageReader<T> reader)
	{
		while (reader.offset < messages.Count)
		{
			yield return messages[reader.offset++];
		}
	}

	internal IEnumerable<T> PeekFrom(int offset)
	{
		for (int i = offset; i < messages.Count; i++)
		{
			yield return messages[i];
		}
	}

	internal int MessageCount => messages.Count;

	public MessageReader<T> ReaderFrom(int offset) => new MessageReader<T>(this, offset);

	public void Clear()
	{
		// Clean up messages from before last frame (keeping last frame + current frame)
		if (lastFrameStart > 0)
		{
			messages.RemoveRange(0, lastFrameStart);

			// Adjust offsets after removal
			Reader.AdjustOffset(-lastFrameStart);
			currentFrameStart -= lastFrameStart;
			lastFrameStart = 0;
		}

		// Move frame boundary forward
		lastFrameStart = currentFrameStart;
		currentFrameStart = messages.Count;
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
	internal int offset;

	internal MessageReader(Messages<T> messages, int startOffset = 0)
	{
		messagesResource = messages;
		offset = startOffset;
	}

	public bool IsEmpty
		=> offset >= messagesResource.MessageCount;

	/// <summary>
	/// Gets all unread messages without advancing the read offset.
	/// </summary>
	public IEnumerable<T> Values => messagesResource.PeekFrom(offset);

	/// <summary>
	/// Reads all unread messages and advances the read offset.
	/// </summary>
	public IEnumerable<T> Read()
	{
		return messagesResource.ReadFrom(this);
	}

	/// <summary>
	/// Peeks at unread messages without advancing the read offset.
	/// </summary>
	public IEnumerable<T> Peek()
	{
		return messagesResource.PeekFrom(offset);
	}

	/// <summary>
	/// Resets the reader to a specific offset.
	/// </summary>
	public void ResetTo(int offsetValue)
	{
		offset = Math.Max(0, offsetValue);
	}

	/// <summary>
	/// Gets the current read offset.
	/// </summary>
	public int CurrentOffset => offset;

	internal void AdjustOffset(int delta)
	{
		offset = Math.Max(0, offset + delta);
	}

	public void Clear()
		=> messagesResource.Clear();

	public IEnumerator<T> GetEnumerator()
		=> Read().GetEnumerator();

	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredRead(world.ResourceId<Messages<T>>()!.Value);
	}
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun) { }
	public static MessageReader<T> FromWorld(World world)
	{
		return world.Resource<Messages<T>>().Reader;
	}
}
