using Verse.MoonWorks.Graphics;

namespace Verse.Render.Graph;

public struct ToBuffer
{
	public ToBuffer(Func<GraphicsDevice, CommandBuffer> fn)
	{
		_fn = (device) => Task.FromResult(fn(device));
	}

	public ToBuffer(CommandBuffer buffer)
	{
		_task = Task.FromResult(buffer);
	}

	public ToBuffer(Task<CommandBuffer> task)
	{
		_task = task;
	}

	public static implicit operator ToBuffer(CommandBuffer buffer) => new (buffer);
	public static implicit operator ToBuffer(Task<CommandBuffer> task) => new (task);
	public static implicit operator ToBuffer(Func<GraphicsDevice, CommandBuffer> fn) => new (fn);

	private Task<CommandBuffer> _task;
	private Func<GraphicsDevice, Task<CommandBuffer>> _fn;

	public Task<CommandBuffer> Get(GraphicsDevice device)
	{
		if (_task != null) {
			return _task;
		}

		var fn = _fn;
		return Task.Run(() => fn(device));
	}
}

/// <summary>
/// Context for SDL3 rendering operations
/// </summary>
public partial class RenderContext
{
	public RenderContext(GraphicsDevice device)
	{
		Device = device;
		Buffers = new ();
		CurrentBuffer = null;
	}
	public GraphicsDevice Device { get; protected set; }
	protected List<ToBuffer> Buffers;
	protected CommandBuffer? CurrentBuffer;



	public bool HasCommands() => CurrentBuffer != null || Buffers.Count > 0;

	/// <summary>
	/// Gets the current command buffer
	/// </summary>
	/// <returns></returns>
	public CommandBuffer GetCurrentBuffer()
	{
		if (CurrentBuffer == null) {
			CurrentBuffer = Device.AcquireCommandBuffer();
		}
		return CurrentBuffer;
	}

	/// <summary>
	/// Append a <see cref="CommandBuffer"/> to the command buffer queue.
	///
	/// If present, this will flush the <see cref="CurrentBuffer"/> into the queue before appending the provided buffer.
	/// </summary>
	/// <param name="buffer"></param>
	public void AddCommandBuffer(CommandBuffer buffer)
	{
		FlushCurrentBuffer();
		Buffers.Add(buffer);
	}

	/// <summary>
	/// Appends a task that creates a <see cref="CommandBuffer"/> to the command buffer queue.
	///
	/// If present, this will flush the <see cref="CurrentBuffer"/> into the queue before appending the provided buffer.
	/// </summary>
	/// <param name="task"></param>
	public void AddCommandBufferTask(Task<CommandBuffer> task)
	{
		FlushCurrentBuffer();
		Buffers.Add(task);
	}

	/// <summary>
	/// Appends a Func that creates a <see cref="CommandBuffer"/> to the command buffer queue.
	///
	/// If present, this will flush the <see cref="CurrentBuffer"/> into the queue before appending the provided buffer.
	/// </summary>
	/// <param name="fn"></param>
	public void AddCommandBufferFunc(Func<GraphicsDevice, CommandBuffer> fn)
	{
		FlushCurrentBuffer();
		Buffers.Add(fn);
	}

	/// <summary>
	/// Finalizes and returns the queue of <see cref="CommandBuffer"/>s.
	///
	/// This function will wait until all command buffer generation tasks are complete by running
	/// them in parallel (where supported).
	/// </summary>
	/// <returns></returns>
	public List<CommandBuffer> Finish()
	{
		FlushCurrentBuffer();
		var buffers = new List<CommandBuffer>(Buffers.Count);
		var tasks = Buffers.Select(x => x.Get(Device));
		Task.WaitAll(tasks);
		foreach (var task in tasks) {
			buffers.Add(task.Result);
		}
		Buffers.Clear();
		return buffers;
	}

	protected void FlushCurrentBuffer()
	{
		if (CurrentBuffer != null) {
			Buffers.Add(CurrentBuffer);
			CurrentBuffer = null;
		}
	}
}