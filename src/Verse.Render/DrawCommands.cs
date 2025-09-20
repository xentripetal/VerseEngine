using System.Numerics;
using Verse.Core;
using Verse.Render.Graph;

namespace Verse.Render;

/// <summary>
/// Represents a renderable item in a render phase
/// </summary>
public interface IPhaseItem
{
    /// <summary>
    /// The entity associated with this phase item
    /// </summary>
    int EntityId { get; }
    
    /// <summary>
    /// The draw function to use for rendering this item
    /// </summary>
    Type DrawFunction { get; }
    
    /// <summary>
    /// Distance from camera for sorting
    /// </summary>
    float Distance { get; }
}

/// <summary>
/// Base implementation of a phase item
/// </summary>
public record PhaseItem(int EntityId, Type DrawFunction, float Distance = 0.0f) : IPhaseItem;

/// <summary>
/// Represents a render phase that sorts items by distance
/// </summary>
/// <typeparam name="T">The type of phase items</typeparam>
public class SortedRenderPhase<T> where T : IPhaseItem
{
    private readonly List<T> _items = new();
    
    /// <summary>
    /// Adds an item to this render phase
    /// </summary>
    public void Add(T item)
    {
        _items.Add(item);
    }
    
    /// <summary>
    /// Clears all items from this phase
    /// </summary>
    public void Clear()
    {
        _items.Clear();
    }
    
    /// <summary>
    /// Sorts all items by distance (back to front for transparency)
    /// </summary>
    public void Sort()
    {
        _items.Sort((a, b) => b.Distance.CompareTo(a.Distance));
    }
    
    /// <summary>
    /// Gets all items in the phase
    /// </summary>
    public IReadOnlyList<T> Items => _items;
    
    /// <summary>
    /// Renders all items in this phase using their draw functions
    /// </summary>
    public void Render(RenderContext renderContext, int viewEntity)
    {
        foreach (var item in _items)
        {
            // Get the draw function and execute it
            if (DrawFunctionRegistry.TryGetDrawFunction(item.DrawFunction, out var drawFunction))
            {
                drawFunction.Draw(renderContext, viewEntity, item);
            }
        }
    }
}

/// <summary>
/// Represents a render phase that bins items for efficient batching
/// </summary>
/// <typeparam name="T">The type of phase items</typeparam>
/// <typeparam name="TBinKey">The type used for binning items</typeparam>
public class BinnedRenderPhase<T, TBinKey> where T : IPhaseItem where TBinKey : notnull
{
    private readonly Dictionary<TBinKey, List<T>> _bins = new();
    
    /// <summary>
    /// Adds an item to the appropriate bin
    /// </summary>
    public void Add(TBinKey binKey, T item)
    {
        if (!_bins.TryGetValue(binKey, out var bin))
        {
            bin = new List<T>();
            _bins[binKey] = bin;
        }
        bin.Add(item);
    }
    
    /// <summary>
    /// Clears all bins
    /// </summary>
    public void Clear()
    {
        foreach (var bin in _bins.Values)
        {
            bin.Clear();
        }
    }
    
    /// <summary>
    /// Gets all bins
    /// </summary>
    public IReadOnlyDictionary<TBinKey, List<T>> Bins => _bins;
    
    /// <summary>
    /// Renders all items in all bins
    /// </summary>
    public void Render(RenderContext renderContext, int viewEntity)
    {
        foreach (var (binKey, items) in _bins)
        {
            // Sort bins for consistent rendering order
            foreach (var item in items.OrderBy(i => i.EntityId))
            {
                if (DrawFunctionRegistry.TryGetDrawFunction(item.DrawFunction, out var drawFunction))
                {
                    drawFunction.Draw(renderContext, viewEntity, item);
                }
            }
        }
    }
}

/// <summary>
/// Interface for draw functions that can render phase items
/// </summary>
public interface IDrawFunction
{
    /// <summary>
    /// Draws a phase item
    /// </summary>
    void Draw(RenderContext renderContext, int viewEntity, IPhaseItem item);
}

/// <summary>
/// Registry for draw functions
/// </summary>
public static class DrawFunctionRegistry
{
    private static readonly Dictionary<Type, IDrawFunction> _drawFunctions = new();
    
    /// <summary>
    /// Registers a draw function
    /// </summary>
    public static void Register<T>(IDrawFunction drawFunction) where T : IDrawFunction
    {
        _drawFunctions[typeof(T)] = drawFunction;
    }
    
    /// <summary>
    /// Tries to get a draw function by type
    /// </summary>
    public static bool TryGetDrawFunction(Type type, out IDrawFunction drawFunction)
    {
        return _drawFunctions.TryGetValue(type, out drawFunction!);
    }
    
    /// <summary>
    /// Gets a draw function by type, throws if not found
    /// </summary>
    public static IDrawFunction GetDrawFunction(Type type)
    {
        if (_drawFunctions.TryGetValue(type, out var drawFunction))
        {
            return drawFunction;
        }
        throw new InvalidOperationException($"Draw function {type.Name} not registered");
    }
    
    /// <summary>
    /// Clears all registered draw functions (for testing)
    /// </summary>
    public static void Clear()
    {
        _drawFunctions.Clear();
    }
}

/// <summary>
/// Common render phases used in the engine
/// </summary>
public static class RenderPhases
{
    /// <summary>
    /// Opaque geometry rendered front to back
    /// </summary>
    public const string Opaque3d = "Opaque3d";
    
    /// <summary>
    /// Alpha-masked geometry  
    /// </summary>
    public const string AlphaMask3d = "AlphaMask3d";
    
    /// <summary>
    /// Transparent geometry rendered back to front
    /// </summary>
    public const string Transparent3d = "Transparent3d";
    
    /// <summary>
    /// 2D sprites and UI
    /// </summary>
    public const string Transparent2d = "Transparent2d";
}

/// <summary>
/// Example phase item for 3D meshes
/// </summary>
public record Mesh3dPhaseItem(
    int EntityId,
    Type DrawFunction,
    float Distance,
    int MaterialId,
    int MeshId,
    Matrix4x4 Transform
) : PhaseItem(EntityId, DrawFunction, Distance);

/// <summary>
/// Example binning key for 3D meshes (groups by material and mesh for batching)
/// </summary>
public record Mesh3dBinKey(int MaterialId, int MeshId);

/// <summary>
/// Example draw function for 3D meshes
/// </summary>
public class DrawMesh3d : IDrawFunction
{
    public void Draw(RenderContext renderContext, int viewEntity, IPhaseItem item)
    {
        if (item is Mesh3dPhaseItem meshItem)
        {
            // Placeholder for SDL3 mesh rendering
            // Would set up vertex buffers, shaders, uniforms, etc.
            // renderContext.SdlRenderer.DrawMesh(meshItem.MeshId, meshItem.Transform);
        }
    }
}

/// <summary>
/// Example draw function for UI elements
/// </summary>
public class DrawUi : IDrawFunction
{
    public void Draw(RenderContext renderContext, int viewEntity, IPhaseItem item)
    {
        // Placeholder for SDL3 UI rendering
        // Would render quads, text, etc.
    }
}