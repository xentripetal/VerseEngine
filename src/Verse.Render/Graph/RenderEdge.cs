using Verse.ECS;
using Verse.ECS.Datastructures;

namespace Verse.Render.Graph;

/// <summary>
/// An edge which connects two <see cref="IRenderNode"/> in a <see cref="RenderGraph"/>
/// </summary>
///
/// <remarks>
/// <para>They are used to describe the ordering of the nodes</para>
/// <para>Edges are added via the <see cref="RenderGraph.AddNodeEdge"/> and <see cref="RenderGraph.AddSlotEdge"/> methods.</para>
/// <para>
/// The former simply states the <see cref="RenderEdge.OutputNode"/> has to run before the <see cref="RenderEdge.InputNode"/>, while the latter connects
/// an output slot of the <see cref="RenderEdge.OutputNode"/> with an input slot of the <see cref="RenderEdge.InputNode"/> at the specified indices.
/// </para>
/// <para>Notice: This uses a nomenclature of the output being the source and the input being the destination. This is copied from bevy_render.</para>
/// </remarks>
public readonly record struct RenderEdge(IRenderLabel OutputNode, IRenderLabel InputNode, int OutputIndex = -1, int InputIndex = -1)
{
	/// <summary>
	/// An edge describing to ordering of both nodes (<see cref="OutputNode"/> before <see cref="InputNode"/>)
	/// </summary>
	public static RenderEdge NodeEdge(IRenderLabel outputNode, IRenderLabel inputNode)
	{
		return new (outputNode, inputNode);
	}
	/// <summary>
	/// An edge describing the ordering of both nodes and connecting the output slot at the <see cref="OutputIndex"/> of the <see cref="OutputNode"/>
	/// with the slot at the <see cref="InputIndex"/> of the <see cref="InputNode"/>
	/// </summary>
	public static RenderEdge SlotEdge(IRenderLabel outputNode, int outputIndex, IRenderLabel inputNode, int inputIndex)
	{
		return new (outputNode, inputNode, outputIndex, inputIndex);
	}

	public bool IsSlotEdge => OutputIndex > -1 || InputIndex > -1;
	public bool IsNodeEdge => OutputIndex < 0 && InputIndex < 0;
}

public readonly struct RenderEdges(IRenderLabel label)
{
	public readonly IRenderLabel Label = label;
	readonly OrderedSet<RenderEdge> _inputEdges = new OrderedSet<RenderEdge>();
	readonly OrderedSet<RenderEdge> _outputEdges = new OrderedSet<RenderEdge>();

	public IReadOnlyOrderedSet<RenderEdge> InputEdges => _inputEdges;
	public IReadOnlyOrderedSet<RenderEdge> OutputEdges => _outputEdges;

	/// <summary>
	/// Adds the specified <paramref name="edge"/> to the collection of input edges, if it does not already exist.
	/// </summary>
	/// <param name="edge">The <see cref="RenderEdge"/> to be added to the input edges.</param>
	/// <returns>True if the specified <paramref name="edge"/> was successfully added; otherwise, false.</returns>
	public bool AddInputEdge(RenderEdge edge)
	{
		if (HasInputEdge(edge)) return false;
		_inputEdges.Add(edge);
		return true;
	}

	/// <summary>
	/// Removes the specified <paramref name="edge"/> from the collection of input edges, if it exists.
	/// </summary>
	/// <param name="edge">The <see cref="RenderEdge"/> to be removed from the input edges.</param>
	/// <returns>True if the specified <paramref name="edge"/> was successfully removed; otherwise, false.</returns>
	public bool RemoveInputEdge(RenderEdge edge)
	{
		return _inputEdges.Remove(edge);
	}

	/// <summary>
	/// Adds the specified <paramref name="edge"/> to the collection of output edges, if it does not already exist.
	/// </summary>
	/// <param name="edge">The <see cref="RenderEdge"/> to be added to the output edges.</param>
	/// <returns>True if the specified <paramref name="edge"/> was successfully added; otherwise, false.</returns>
	public bool AddOutputEdge(RenderEdge edge)
	{
		if (HasOutputEdge(edge)) return false;
		_outputEdges.Add(edge);
		return true;
	}

	/// <summary>
	/// Removes the specified <paramref name="edge"/> from the collection of output edges.
	/// </summary>
	/// <param name="edge">The <see cref="RenderEdge"/> to be removed from the output edges.</param>
	/// <returns>True if the specified <paramref name="edge"/> was successfully removed; otherwise, false.</returns>
	public bool RemoveOutputEdge(RenderEdge edge)
	{
		return _outputEdges.Remove(edge);
	}

	/// <summary>
	/// Determines whether the specified <paramref name="edge"/> exists in the collection of input edges.
	/// </summary>
	/// <param name="edge">The <see cref="RenderEdge"/> to check for existence in the input edges.</param>
	/// <returns>True if the specified <paramref name="edge"/> exists in the input edges; otherwise, false.</returns>
	public bool HasInputEdge(RenderEdge edge)
	{
		return _inputEdges.Any(e => e == edge);
	}

	/// <summary>
	/// Determines whether the specified <paramref name="edge"/> exists in the collection of output edges.
	/// </summary>
	/// <param name="edge">The <see cref="RenderEdge"/> to check for existence in the output edges.</param>
	/// <returns>True if the specified <paramref name="edge"/> exists in the output edges; otherwise, false.</returns>
	public bool HasOutputEdge(RenderEdge edge)
	{
		return _outputEdges.Any(e => e == edge);
	}

	/// <summary>
	/// Searches the <see cref="InputEdges"/> for a <see cref="RenderEdge.SlotEdge"/> which has the given <paramref name="index"/> as its <see cref="RenderEdge.InputIndex"/>
	/// </summary>
	/// <param name="index">Slot index to search for</param>
	/// <param name="found">True if found</param>
	/// <returns>Matching render edge if found</returns>
	public RenderEdge? TryGetInputSlotEdge(int index, out bool found)
	{
		found = false;
		foreach (var edge in _inputEdges) {
			if (edge.IsSlotEdge && edge.InputIndex == index) {
				return edge;
			}
		}
		return null;
	}

	/// <summary>
	/// Searches the <see cref="OutputEdges"/> for a <see cref="RenderEdge.SlotEdge"/> which has the given <paramref name="index"/> as its <see cref="RenderEdge.OutputIndex"/>
	/// </summary>
	/// <param name="index">Slot index to search for</param>
	/// <param name="found">True if found</param>
	/// <returns>Matching render edge if found</returns>
	public RenderEdge? TryGetOutputSlotEdge(int index, out bool found)
	{
		found = false;
		foreach (var edge in _outputEdges) {
			if (edge.IsSlotEdge && edge.OutputIndex == index) {
				return edge;
			}
		}
		return null;
	}
}