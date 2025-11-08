using Serilog;
using Verse.Assets;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;

namespace Verse.Render;


public struct PrepareAssetResult<TRenderAsset>
{
	public PrepareAssetResult(TRenderAsset asset)
	{
		Asset = asset;
		RetryNextFrame = false;
		Exception = null;
	}

	public PrepareAssetResult(Exception exception, bool retry = false)
	{
		Exception = exception;
		RetryNextFrame = retry;
	}
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
	/// Prepares the asset for the GPU by transforming it into a <see cref="IRenderAsset{TRenderAsset, TSourceAsset, TParam}"/>
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
	public ExtractedAssets()
	{
		Extracted = [];
		Removed = [];
		Modified = [];
		Added = [];
	}
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

public struct RenderAssetPlugin<TRender, TSource, TParam> : IPlugin
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
	[Schedule(RenderSchedules.Extract)]
	public static void ExtractRenderAsset(Commands commands, MainWorld mainWorld, Local<int> offset)
	{
		var assets = mainWorld.world.Resource<Assets<TSource>>();
		var assetMessages = mainWorld.world.Resource<Messages<AssetEvent<TSource>>>();
		// TODO Make sure this strategy for offsets actually works
		var events = assetMessages.CreateReaderFrom(offset.Value);

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
		
		offset.Value = events.CurrentOffset;
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

		foreach (var removed in assets.Removed) {
			TRender.UnloadAsset(removed, param);
		}
		assets.Removed.Clear();

		foreach (var (id, extractedAsset) in assets.Extracted) {
			// We remove previous to ensure that if we are updating the asset, then any users will not see the old asset
			// after a new asset is extracted. Even if the new asset is not yet ready, or we are out of bytes
			renderAssets.Assets.Remove(id, out var previousAsset);
			var res = TRender.PrepareAsset(extractedAsset, id, param, previousAsset);
			if (res.Asset != null) {
				renderAssets.Assets[id] = res.Asset;
			} else if (res.RetryNextFrame) {
				prepareNextFrame.Assets.Add((id, extractedAsset));
			} else {
				Log.Error(res.Exception, "{AssetType} asset preparation failed for asset ID {AssetId}", typeof(TRender), id);
			}
		}
	}
}