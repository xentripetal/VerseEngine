namespace Verse.Render.View;

/// <summary>Add this component to a camera to disable *indirect mode*.</summary>
///
/// <remarks>
/// <para>
/// Indirect mode, automatically enabled on supported hardware, allows Bevy to
/// offload transform and cull operations to the GPU, reducing CPU overhead.
/// Doing this, however, reduces the amount of control that your app has over
/// instancing decisions. In certain circumstances, you may want to disable
/// indirect drawing so that your app can manually instance meshes as it sees
/// fit. See the `custom_shader_instancing` example.
/// </para>
/// <para>
/// The vast majority of applications will not need to use this component, as it
/// generally reduces rendering performance.
/// </para>
/// <para>
/// Note: This component should only be added when initially spawning a camera. Adding
/// or removing after spawn can result in unspecified behavior.
/// </para>
/// </remarks>
public struct NoIndirectDrawing;