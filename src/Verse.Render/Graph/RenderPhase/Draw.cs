using Verse.ECS;
using Verse.MoonWorks.Graphics;

namespace Verse.Render.Graph.RenderPhase;

public record struct DrawFunctionId(uint Id);

public interface IDrawFunction<T> where T : IPhaseItem
{
	public void Prepare(World world) {}
	public void Draw(World world, RenderPass pass, EntityView view, ref T item);
}

public class DrawFunctions<T> where T : IPhaseItem
{
	public List<IDrawFunction<T>> Functions = new();
	public Dictionary<Type, int> Indices = new();

	public void Prepare(World world)
	{
		foreach (var function in Functions)
			function.Prepare(world);
	}
	
	/// <summary>
	/// Adds a draw function and returns its index.
	/// </summary>
	/// <param name="function"></param>
	/// <typeparam name="TFunction"></typeparam>
	public int Add<TFunction>(TFunction function) where TFunction : IDrawFunction<T>
	{
		return AddWith<TFunction>(function);
	}
	
	/// <summary>
	/// Adds a draw function with a lookup type and returns its index.
	/// </summary>
	/// <param name="draw"></param>
	/// <typeparam name="TLookup"></typeparam>
	public int AddWith<TLookup>(IDrawFunction<T> draw) 
	{
		if (Indices.ContainsKey(typeof(TLookup))) {
			return Indices[typeof(TLookup)];
		}
		var index = Functions.Count;
		Functions.Add(draw);
		Indices[typeof(TLookup)] = index;
		return index;
	}
	
	public int? GetId<TLookup>() 
	{
		return Indices.TryGetValue(typeof(TLookup), out var id) ? id : null;
	}
	
	public int Id<TLookup>() 
	{
		return Indices[typeof(TLookup)];
	}

	public IDrawFunction<T>? Get(int index)
	{
		if (index >= Functions.Count) return null;
		return Functions[index];
	} 
}