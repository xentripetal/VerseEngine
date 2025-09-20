using Verse.ECS;

namespace Verse.Render.Graph;

/// <summary>
/// Internal representation of a <see cref="IRenderNode"/>, with all data requried by <see cref="RenderGraph"/>
/// </summary>
/// <remarks>The <see cref="InputSlots"/> and <see cref="OutputSlots"/> are provided by the node.</remarks>
public class NodeState
{
	public readonly IRenderLabel Label;
	public readonly string TypeName;
	public IRenderNode Node { get; }
	public IReadOnlyList<SlotInfo> InputSlots;
	public IReadOnlyList<SlotInfo> OutputSlots;
	public RenderEdges Edges;


	private NodeState(IRenderLabel label, string typeName, IRenderNode node)
	{
		Label = label;
		Node = node;
		TypeName = typeName;
		InputSlots = node.InputSlots;
		OutputSlots = node.OutputSlots;
		Edges = new RenderEdges(label);
	}

	public static NodeState New<T>(IRenderLabel label, T node) where T : IRenderNode
	{
		return new NodeState(label, typeof(T).Name, node);
	}

	/// <summary>
	/// Retrieves the node cast to the specified type <typeparamref name="T"/>. Throws an exception if the node is not of the specified type.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public T GetNodeAs<T>() where T : IRenderNode
	{
		return (T)Node;
	}

	/// <summary>
	/// Validates that each input slot corresponds to an input edge.
	/// </summary>
	/// <returns>True if valid</returns>
	public bool ValidateInputSlots()
	{
		for (var i = 0; i < InputSlots.Count; i++) {
			Edges.TryGetInputSlotEdge(i, out var ok);
			if (!ok) {
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Validates that each output slot corresponds to an output edge.
	/// </summary>
	/// <returns>True if valid</returns>
	public bool ValidateOutputSlots()
	{
		for (int i = 0; i < OutputSlots.Count; i++) {
			Edges.TryGetOutputSlotEdge(i, out var ok);
			if (!ok) {
				return false;
			}
		}
		return true;
	}
}

/// <summary>
/// Interface for render graph nodes
/// </summary>
public interface IRenderNode
{
	/// <summary>
	/// Input slots this node requires
	/// </summary>
	IReadOnlyList<SlotInfo> InputSlots => [];

	/// <summary>
	/// Output slots this node produces
	/// </summary>
	IReadOnlyList<SlotInfo> OutputSlots => [];

	/// <summary>
	/// Updates the node before rendering
	/// </summary>
	void Update(World world) { }

	/// <summary>
	/// Executes the render node
	/// </summary>
	void Run(RenderGraphContext context, RenderContext renderContext, World world);
}

/// <summary>
/// A <see cref="IRenderNode"/> which acts as an entry point for a <see cref="RenderGraph"/> with custom inputs.
/// It has the same input and output slots and simply copies them over when run.
/// </summary>
/// <remarks>Can be referenced via <see cref="GraphInput"/></remarks>
/// <param name="slots">Standard inputs into the graph</param>
public readonly struct GraphInputNode(List<SlotInfo> slots) : IRenderNode
{
	public IReadOnlyList<SlotInfo> InputSlots => slots;
	public IReadOnlyList<SlotInfo> OutputSlots => slots;
	public void Run(RenderGraphContext context, RenderContext renderContext, World world)
	{
		var inputs = context.GetInputs();
		for (int i = 0; i < inputs.Count; i++) {
			context.SetOutput(SlotLabel.OfIndex(i), inputs[i]);
		}
	}
}

/// <summary>A <see cref="IRenderNode"/> without any inputs, outputs, or subgraphs, which does nothing when run.</summary>
/// <remarks>Used (as a label) to bundle multiple dependencies into one inside the <see cref="RenderGraph"/></remarks>
public struct EmptyNode : IRenderNode
{
	public void Run(RenderGraphContext context, RenderContext renderContext, World world)
	{
	}
}

/// <summary>
/// A <see cref="RenderGraph"/> <see cref="IRenderNode"/> that runs the configured subgraph once.
/// This makes it easier to insert sub-graph runs into a graph.
/// </summary>
/// <param name="label">Subgraph to run</param>
public struct RunGraphOnViewNode(ISubgraphLabel label) : IRenderNode
{
	public void Run(RenderGraphContext context, RenderContext renderContext, World world)
	{
		context.RunSubGraph(label, [], context.GetViewEntity());
	}
}

/// <summary>
/// This interface should be used instead of the <see cref="IRenderNode"/> interface when making a render node that runs on a view.
/// </summary>
/// <typeparam name="TData">Data accessed from the view entity. NOTE: This isn't actually used yet. Leaving as a stub for when we actually implement it.</typeparam>
/// <remarks>Intended to be used with <see cref="ViewRenderNodeRunner"/></remarks>
public interface IViewRenderNode<TData> : IRenderNode where TData : struct, IData<TData>, allows ref struct
{
	public void Run(RenderGraphContext context, RenderContext renderContext, World world, EntityView view);
	void IRenderNode.Run(RenderGraphContext context, RenderContext renderContext, World world)
	{
		Run(context, renderContext, world, world.Entity(context.GetViewEntity()));
	}
}

