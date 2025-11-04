using FluentResults;
using Serilog;
using Verse.Assets;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;

namespace Verse.Render;

/// <summary>
/// Defines where an asset will be used
/// </summary>
/// <remarks>
/// If an asset is set to the `RenderWorld` but not the `MainWorld`, the asset will be
/// unloaded from the asset server once it's been extracted and prepared in the render world.
///
/// Unloading the asset saves on memory, as for most cases it is no longer necessary to keep
/// it in RAM once it's been uploaded to the GPU's VRAM. However, this means you can no longer
/// access the asset from the CPU (via the `Assets{T}` resource) once unloaded (without re-loading it).
///
/// If you never need access to the asset from the CPU past the first frame it's loaded on,
/// or only need very infrequent access, then set this to `RenderWorld`. Otherwise, set this to
/// `RenderWorld | MainWorld`.
///
/// If you have an asset that doesn't need to end up in the render world, like an Image
/// that will be decoded into another Image asset, use `MainWorld` only.
/// </remarks>
[Flags]
public enum RenderAssetUsage
{
	MainWorld = 1 << 0,
	RenderWorld = 1 << 1,
}

public struct PrepareAssetResult<TRenderAsset>
{
	public TRenderAsset? Asset;
	public bool RetryNextFrame;
	public Exception? Exception;
}

public interface IRenderAsset<TRenderAsset, TSourceAsset, TParam>
	where TSourceAsset : IAsset
	where TRenderAsset : IRenderAsset<TRenderAsset, TSourceAsset, TParam>
	where TParam : ISystemParam, IFromWorld<TParam>
{
	/// <summary>
	/// Prepares the asset for the GPU by transforming it into a <see cref="IRenderAsset{TRenderAsset, TSourceAsset}"/>
	/// </summary>
	static abstract PrepareAssetResult<TRenderAsset> PrepareAsset(TSourceAsset asset, AssetId<TSourceAsset> assetId, TParam param, TRenderAsset? previousAsset);

	/// <summary>
	/// Called whenever the <paramref name="TSourceAsset"/> has been removed.
	/// </summary>
	/// <remarks>You can implement this method if you need to access ECS data in order to perform cleanup tasks.</remarks>
	static virtual void UnloadAsset(AssetId<TSourceAsset> assetId, TParam param) { }

	/// <summary>
	/// Size of the data the asset will upload to the gpu. Specicfying a return value will allow the asset toe be throttled
	/// </summary>
	/// <param name="asset">The asset to be uploaded</param>
	/// <returns></returns>
	static virtual ulong ByteLength(TSourceAsset asset)
	{
		return 0;
	}
	/// <summary>
	/// Whether ot not to unload the asset after extracting it to the render world
	/// </summary>
	/// <returns>Render usage flags</returns>
	static virtual RenderAssetUsage GetUsage(TSourceAsset asset)
	{
		return RenderAssetUsage.MainWorld | RenderAssetUsage.RenderWorld;
	}
}

public class ExtractedAssets<TRender, TSource, TParam>
	where TRender : IRenderAsset<TRender, TSource, TParam>
	where TSource : IAsset
	where TParam : ISystemParam, IFromWorld<TParam>
{
	public ExtractedAssets(
		List<(AssetId<TSource>, TSource)> extracted, HashSet<AssetId<TSource>> removed, HashSet<AssetId<TSource>> modified, HashSet<AssetId<TSource>> added)
	{
		Extracted = extracted;
		Removed = removed;
		Modified = modified;
		Added = added;
	}
	/// <summary>
	/// The assets extracted this frame
	/// </summary>
	public List<(AssetId<TSource>, TSource)> Extracted;
	/// <summary>
	/// The IDs of assets that were removed in this frame
	/// </summary>
	public HashSet<AssetId<TSource>> Removed;
	/// <summary>
	/// The IDs of assets that were modified this frame
	/// </summary>
	public HashSet<AssetId<TSource>> Modified;
	/// <summary>
	/// The IDs of assets that were added to this frame
	/// </summary>
	public HashSet<AssetId<TSource>> Added;
}

/// <summary>
/// Stores all GPU representations of assets as long as they exist
/// </summary>
public class RenderAssets<TRender, TSource, TParam>
	where TRender : IRenderAsset<TRender, TSource, TParam>
	where TSource : IAsset
	where TParam : ISystemParam, IFromWorld<TParam>
{
	/// <summary>
	/// The prepared render assets
	/// </summary>
	public Dictionary<AssetId<TSource>, TRender> Assets = new Dictionary<AssetId<TSource>, TRender>();
}

/// <summary>
/// All assets that should be prepared next frame.
/// </summary>
/// <typeparam name="TRender"></typeparam>
/// <typeparam name="TSource"></typeparam>
public class PrepareNextFrameAssets<TRender, TSource, TParam>
	where TRender : IRenderAsset<TRender, TSource, TParam>
	where TSource : IAsset
	where TParam : ISystemParam, IFromWorld<TParam>
{
	public List<(AssetId<TSource>, TSource)> Assets = new List<(AssetId<TSource>, TSource)>();
}

/// <summary>
/// The system set during which we extract modified assets to the render world
/// </summary>
public class AssetExtractionSets : StaticSystemSet { }

public struct RenderAssetPlugin<TRender, TSource, TParam>
	where TRender : IRenderAsset<TRender, TSource, TParam>
	where TSource : IAsset
	where TParam : ISystemParam, IFromWorld<TParam>
{

	public void Build(App app)
	{
		var render = app.GetSubApp(RenderApp.Name);
		if (render != null) {
			render.InitResource<ExtractedAssets<TRender, TSource, TParam>>().
				InitResource<RenderAssets<TRender, TSource, TParam>>().
				InitResource<PrepareNextFrameAssets<TRender, TSource, TParam>>().
				AddSchedulable<RenderAssetSystems<TRender, TSource, TParam>>();
		}
	}
}

public partial class RenderAssetSystems<TRender, TSource, TParam>
	where TRender : IRenderAsset<TRender, TSource, TParam>
	where TSource : IAsset
	where TParam : ISystemParam, IFromWorld<TParam>
{

	/// <summary>
	/// This system extracts all created or modified assets of the corresponding type into the render world
	/// </summary>
	/// <param name="commands"></param>
	/// <param name="mainWorld"></param>
	[Schedule(RenderSchedules.Extract)]
	public static void ExtractRenderAsset(Commands commands, MainWorld mainWorld)
	{
		var assets = mainWorld.world.GetResource<Assets<TSource>>();
		var assetMessages = mainWorld.world.GetResource<Messages<AssetEvent<TSource>>>();
		// TODO support offset
		var events = assetMessages.ReaderFrom(0);

		var needsExtracting = new HashSet<AssetId<TSource>>();
		var removed = new HashSet<AssetId<TSource>>();
		var modified = new HashSet<AssetId<TSource>>();

		foreach (var evt in events) {
			switch (evt.Type) {

				case AssetEventType.Added:
					needsExtracting.Add(evt.Id);
					break;
				case AssetEventType.Modified:
					needsExtracting.Add(evt.Id);
					modified.Add(evt.Id);
					break;
				case AssetEventType.Removed:
					// We don't care that the asset was removed from Assets<T> in the main world.
					// An asset is only removed from RenderAssets<T> when its last handle is dropped (AssetEvent::Unused).
					break;
				case AssetEventType.Unused:
					needsExtracting.Remove(evt.Id);
					modified.Remove(evt.Id);
					removed.Add(evt.Id);
					break;
				case AssetEventType.LoadedWithDependencies:
					// todo handle this - Even bevy doesnt do anything with this
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
		var extractedAssets = new List<(AssetId<TSource>, TSource)>();
		var added = new HashSet<AssetId<TSource>>();
		foreach (var id in needsExtracting) {
			if (assets.TryGet(id, out var asset)) {
				var usage = TRender.GetUsage(asset!);
				if (usage.HasFlag(RenderAssetUsage.RenderWorld)) {
					if (usage == RenderAssetUsage.RenderWorld) {
						assets.Remove(id);
					}
					extractedAssets.Add((id, asset!));
					added.Add(id);
				}
			}
		}
		var extracted = new ExtractedAssets<TRender, TSource, TParam>(extractedAssets, removed, modified, added);
		commands.InsertResource(extracted);
	}

	[Schedule(RenderSchedules.Render)]
	[InSet<RenderSets>(RenderSets.Prepare)]
	public static void PrepareAssets(
		ExtractedAssets<TRender, TSource, TParam> assets, RenderAssets<TRender, TSource, TParam> renderAssets,
		PrepareNextFrameAssets<TRender, TSource, TParam> prepareNextFrame,
		TParam param)
	{

		var nextFrame = prepareNextFrame.Assets;
		prepareNextFrame.Assets = [];
		foreach (var (id, extractedAsset)in nextFrame) {
			if (assets.Removed.Contains(id) || assets.Added.Contains(id)) {
				// Skip previous frame assets that have been removed or updated
				continue;
			}
			renderAssets.Assets.TryGetValue(id, out var previousAsset);
			var res = TRender.PrepareAsset(extractedAsset, id, param, previousAsset);
			if (res.Asset != null) {
				renderAssets.Assets[id] = res.Asset!;
			} else if (res.RetryNextFrame) {
				prepareNextFrame.Assets.Add((id, extractedAsset));
			} else {
				Log.Error(res.Exception, "{AssetType} asset preparation failed for asset ID {AssetId}", typeof(TRender), id);
			}
		}
	}
}