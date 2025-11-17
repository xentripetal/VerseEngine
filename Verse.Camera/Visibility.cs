namespace Verse.Camera;

/// <summary>
/// User indication of whether an entity is visible. Propagates down the entity hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// If an entity is hidden in this way, all [`Children`] (and all of their children and so on) who
/// are set to [`Inherited`](Self::Inherited) will also be hidden.
/// </para>
/// <para>
/// This is done by the `visibility_propagate_system` which uses the entity hierarchy and
/// `Visibility` to set the values of each entity's [`InheritedVisibility`] component.
/// </para>
/// </remarks>
/// <param name="Visible"></param>
public record struct ViewVisibility(bool Visible);

