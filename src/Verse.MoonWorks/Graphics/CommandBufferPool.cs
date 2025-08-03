using System.Collections.Concurrent;

namespace Verse.MoonWorks.Graphics;

internal class CommandBufferPool
{
	private readonly ConcurrentQueue<CommandBuffer> CommandBuffers = new ConcurrentQueue<CommandBuffer>();
	private readonly GraphicsDevice GraphicsDevice;

	public CommandBufferPool(GraphicsDevice graphicsDevice)
	{
		GraphicsDevice = graphicsDevice;
	}

	public CommandBuffer Obtain()
	{
		if (CommandBuffers.TryDequeue(out var commandBuffer)) {
			return commandBuffer;
		}
		return new CommandBuffer(GraphicsDevice);
	}

	public void Return(CommandBuffer commandBuffer)
	{
		commandBuffer.Handle = IntPtr.Zero;
		CommandBuffers.Enqueue(commandBuffer);
	}
}