using System.Diagnostics;
using Serilog;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;

namespace Verse.Render;

/// <summary>
/// Optional interface for extracting a value from the main world to the render world.
/// If not present on an extracted resource, the resource will be used directly.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IExtractResource<T>
{
	T Extract(T value);
}

public interface IExtractComponent<T>
{
	T? Extract(T value);
}


public class Extract<T> : ISystemParam, IFromWorld<Extract<T>>
	where T : ISystemParam, IFromWorld<T>
{
	public T Param;
	public void Init(ISystem system, World world)
	{
		var mainWorld = world.Resource<MainWorld>();
		Param = T.FromWorld(mainWorld.world);
		Param.Init(system, world);
	}
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun)
	{
		var mainWorld = world.Resource<MainWorld>();
		Param.ValidateParam(meta, mainWorld.world, thisRun);
	}
	public static Extract<T> FromWorld(World world) => new Extract<T>();
}

public struct ExtractResourcePlugin<T> : IPlugin
{
	public void Build(App app)
	{
		var renderApp = app.GetSubApp(RenderApp.Name);
		if (renderApp != null) {
			var extract = ExtractResource;
			renderApp.AddSystems(RenderSchedules.Extract, FuncSystem.Of(extract));
		} else {
			Log.Warning("Render app not found, skipping ExtractResourcePlugin for {T}", typeof(T).Name);
		}
	}

	private static T Extract(T value)
	{
		if (value is IExtractResource<T> extract) {
			return extract.Extract(value);
		}
		return value;
	}

	public static void ExtractResource(Commands commands, Extract<OptionalRes<T>> mainWorldRes, OptionalResMut<T> renderWorldRes)
	{
		if (mainWorldRes.Param.HasValue) {
			if (renderWorldRes.HasValue) {
				if (mainWorldRes.Param.Ticks.IsChanged) {
					renderWorldRes.MutValue = Extract(mainWorldRes.Param.Value!);
				}
			} else {
#if DEBUG
				if (!mainWorldRes.Param.Ticks.IsAdded) {
					Log.Warning("Removing resource {} from render world not expected", typeof(T).Name);
				}
#endif
				commands.InsertResource(Extract(mainWorldRes.Param.Value!));
			}
		}
	}
}

public struct ExtractComponentPlugin<T> : IPlugin
{
	public bool OnlyExtractVisible;


	public void Build(App app) { }

	public static T? Extract(T value)
	{
		if (value is IExtractComponent<T> extract) {
			return extract.Extract(value);
		}
		return value;
	}

	public static void ExtractComponents(Commands commands, Local<int> previousLength, Extract<Query<Data<RenderEntity, T>>> query)
	{
		foreach (var (entity, queryItem) in query.Param) {
			var extracted = Extract(queryItem.Ref);
			if (extracted != null) {
				commands.Entity(entity.Ref.EntityId).Set<T>(extracted);
			} else {
				commands.Entity(entity.Ref.EntityId).Unset<T>();
			}
		}
		
	}
}