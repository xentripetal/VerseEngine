using SDL3;
using Serilog;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Scheduling.Configs;
using Verse.MoonWorks.Graphics;
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
		app.AddEvent<SDL.SDL_Event>().
			AddEvent<CloseWindowRequest>().
			InitRes<GraphicsDevice>().
			InitRes(new GraphicSettings {
				AvailableShaderFormats = ShaderFormat.MetalLib | ShaderFormat.MSL | ShaderFormat.SPIRV | ShaderFormat.DXBC | ShaderFormat.DXIL,
				DebugMode = true
			}).AddSchedulable(new MoonWorksSystems());
		var sdlApp = new MoonWorksApp(app, appInfo, initFlags);
		var runner = new SDLRunner(sdlApp);
		app.SetRunner(runner.Run);
	}
}

public struct GraphicSettings
{
	public ShaderFormat AvailableShaderFormats;
	public bool DebugMode;
}

// TODO replace Window with a struct
public record struct WindowComponent(Window Window) { }

public class WindowRegistry
{
	public Dictionary<uint, ulong> WindowToEntity = new Dictionary<uint, ulong>();
}

public partial class MoonWorksSystems
{
	[Schedule(Schedules.First)]
	public void InitGraphics(Res<TitleStorage> storage, ResMut<GraphicsDevice> graphics, Res<GraphicSettings> settings)
	{
		// TODO break GraphicsDevice out into its own plugin
		graphics.Value ??= new GraphicsDevice(storage.Value, settings.Value.AvailableShaderFormats, settings.Value.DebugMode);
	}

	[Schedule(Schedules.First)]
	[After<Sets>(Sets.InitGraphics)]
	public void CreateWindows(Query<Data<WindowComponent>, Added<WindowComponent>> q, Res<GraphicsDevice> graphics, ResMut<WindowRegistry> registry)
	{
		if (!registry.HasValue) {
			registry.Value = new WindowRegistry();
		}
		foreach (var (e, window) in q) {
			if (!window.Ref.Window.Claimed) {
				if (!graphics.Value.ClaimWindow(window.Mut.Window)) {
					Log.Warning("Failed to claim window", window.Ref.Window);
				} else {
					registry.Value!.WindowToEntity[SDL.SDL_GetWindowID(window.Ref.Window.Handle)] = e.Ref.Id;
				}
			}
		}
	}

	[Schedule(Schedules.Last)]
	public void CloseWindows(EventReader<CloseWindowRequest> closes, Res<GraphicsDevice> graphics, World world, Res<WindowRegistry> registry)
	{
		foreach (var close in closes) {
			var entityId = registry.Value.WindowToEntity[close.WindowId];
			var entity = world.Entity(entityId);
			var window = entity.Get<WindowComponent>();
			entity.Unset<WindowComponent>();
			graphics.Value.UnclaimWindow(window.Window);
			window.Window.Dispose();
			registry.Value.WindowToEntity.Remove(close.WindowId);
		}
	}
}