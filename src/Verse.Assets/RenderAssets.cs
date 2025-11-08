namespace Verse.Assets;

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