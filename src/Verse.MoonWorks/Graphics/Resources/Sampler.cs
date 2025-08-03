using SDL = Verse.MoonWorks.Graphics.SDL_GPU;

namespace Verse.MoonWorks.Graphics.Resources;

/// <summary>
///     Specifies how a texture will be sampled in a shader.
/// </summary>
public class Sampler : SDLGPUResource
{

	private Sampler(GraphicsDevice device) : base(device) { }
	protected override Action<IntPtr, IntPtr> ReleaseFunction => SDL.SDL_ReleaseGPUSampler;

	public static Sampler Create(
		GraphicsDevice device,
		in SamplerCreateInfo samplerCreateInfo
	)
	{
		var handle = SDL.SDL_CreateGPUSampler(
			device.Handle,
			samplerCreateInfo
		);

		if (handle == IntPtr.Zero) {
			Logger.LogError(lib.SDL3_CS.SDL3.SDL.SDL_GetError());
			return null;
		}

		return new Sampler(device) {
			Handle = handle,
			Name = lib.SDL3_CS.SDL3.SDL.SDL_GetStringProperty(samplerCreateInfo.Props, lib.SDL3_CS.SDL3.SDL.SDL_PROP_GPU_SAMPLER_CREATE_NAME_STRING, "Sampler")
		};
	}

	public static Sampler Create(
		GraphicsDevice device,
		string name,
		SamplerCreateInfo samplerCreateInfo
	)
	{
		var cleanProps = false;
		if (samplerCreateInfo.Props == 0) {
			samplerCreateInfo.Props = lib.SDL3_CS.SDL3.SDL.SDL_CreateProperties();
			cleanProps = true;
		}

		lib.SDL3_CS.SDL3.SDL.SDL_SetStringProperty(samplerCreateInfo.Props, lib.SDL3_CS.SDL3.SDL.SDL_PROP_GPU_SAMPLER_CREATE_NAME_STRING, name);

		var result = Create(device, samplerCreateInfo);

		if (cleanProps) {
			lib.SDL3_CS.SDL3.SDL.SDL_DestroyProperties(samplerCreateInfo.Props);
		}

		return result;
	}
}