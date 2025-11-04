using System.Reflection;
using SDL3;
using Serilog;
using Verse.Assets;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Scheduling.Configs;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Graphics.Resources;
using Verse.MoonWorks.Storage;

namespace Verse.MoonWorks;

public struct MoonWorksPlugin(
	AppInfo appInfo,
	SDL.SDL_InitFlags initFlags = SDL.SDL_InitFlags.SDL_INIT_VIDEO | SDL.SDL_InitFlags.SDL_INIT_TIMER | SDL.SDL_InitFlags.SDL_INIT_GAMEPAD |
	                              SDL.SDL_InitFlags.SDL_INIT_HAPTIC
) : IPlugin
{

	public void Build(App app)
	{
		var sdlApp = new MoonWorksApp(app, appInfo, initFlags);
		app.AddMessage<SDL.SDL_Event>();
		var runner = new SDLRunner(sdlApp);
		app.SetRunner(runner.Run);
	}
}

public struct MoonWorksGraphicsPlugin : IPlugin
{
	public void Build(App app)
	{
		app.AddMessage<CloseWindowRequest>().
			InitAsset<Image>().
			InitAssetLoader<ImageLoader, Image, ImageSettings>().
			InsertResource(new WindowRegistry()).
			InitResource(new GraphicSettings {
				AvailableShaderFormats = ShaderFormat.MetalLib | ShaderFormat.MSL | ShaderFormat.SPIRV | ShaderFormat.DXBC | ShaderFormat.DXIL,
				DebugMode = true
			}).
			AddSchedulable(new MoonWorksSystems());
	}
}

public class GraphicSettings : IDefault<GraphicSettings>
{
	public ShaderFormat AvailableShaderFormats;
	public bool DebugMode;
	public static GraphicSettings Default() => new GraphicSettings {
		AvailableShaderFormats = ShaderFormat.MetalLib | ShaderFormat.MSL | ShaderFormat.SPIRV | ShaderFormat.DXBC | ShaderFormat.DXIL,
		DebugMode = true
	};
}

// TODO replace Window with a struct
public record struct WindowComponent(Window Window) { }

public class WindowRegistry
{
	protected Dictionary<uint, ulong> WindowToEntity = new Dictionary<uint, ulong>();
	protected Dictionary<ulong, uint> EntityToWindow = new Dictionary<ulong, uint>();

	public void RegisterWindow(Window window, ulong entityId)
	{
		var id = SDL.SDL_GetWindowID(window.Handle);
		RegisterWindow(id, entityId);
	}

	public void RegisterWindow(uint windowId, ulong entityId)
	{
		WindowToEntity[windowId] = entityId;
		EntityToWindow[entityId] = windowId;
	}

	public void UnregisterWindow(Window window)
	{
		var id = SDL.SDL_GetWindowID(window.Handle);
		UnregisterWindow(id);
	}

	public void UnregisterWindow(uint windowId)
	{
		var entity = WindowToEntity[windowId];
		WindowToEntity.Remove(windowId);
		EntityToWindow.Remove(entity);
	}

	public ulong EntityFromWindow(uint windowId)
	{
		return WindowToEntity[windowId];
	}

	public uint WindowFromEntity(ulong entityId)
	{
		return EntityToWindow[entityId];
	}
}

public partial class MoonWorksSystems
{
	[Schedule(Schedules.Startup)]
	public void InitGraphics(World world, in GraphicSettings settings, in TitleStorage storage)
	{
		world.InsertResource(new GraphicsDevice(storage, settings.AvailableShaderFormats, settings.DebugMode));
	}

	[Schedule(Schedules.First)]
	public void CreateWindows(Query<Data<WindowComponent>, Added<WindowComponent>> q, in GraphicsDevice graphics, in WindowRegistry registry)
	{
		foreach (var (e, window) in q) {
			if (!window.Ref.Window.Claimed) {
				if (!graphics.ClaimWindow(window.Mut.Window)) {
					Log.Warning("Failed to claim window {}", window.Ref.Window);
				} else {
					registry.RegisterWindow(window.Ref.Window, e.Ref.Id);
				}
			}
		}
	}

	[Schedule(Schedules.Last)]
	public void CloseWindows(World world, MessageReader<CloseWindowRequest> closes, ref readonly GraphicsDevice graphics, WindowRegistry registry)
	{
		foreach (var close in closes) {
			var entityId = registry.EntityFromWindow(close.WindowId);
			var entity = world.Entity(entityId);
			var window = entity.Get<WindowComponent>();
			graphics.UnclaimWindow(window.Window);
			entity.Unset<WindowComponent>();
			window.Window.Dispose();
			registry.UnregisterWindow(close.WindowId);
		}
	}
}