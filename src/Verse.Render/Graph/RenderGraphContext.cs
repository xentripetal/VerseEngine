using Verse.MoonWorks.Graphics.Resources;
using Buffer = Verse.MoonWorks.Graphics.Resources.Buffer;

namespace Verse.Render.Graph;

/// <summary>
/// Context provided to render nodes during execution.
/// </summary>
///
/// <remarks>
/// <p>The slot input can be read from here, and the outputs must be written back to the context for passing them onto the next node</p>
/// <p>Subgraphs can be queud for running by adding a <see cref="RunSubGraph"/> command to the context.
/// After the node has finished running, the graph runner is responsible for executing the subgraphs</p>
/// </remarks>
public class RenderGraphContext
{
	public RenderGraphContext(RenderGraph graph, NodeState node, List<SlotValue> inputs, List<SlotValue?> outputs)
	{
		Graph = graph;
		Node = node;
		Inputs = inputs;
		Outputs = outputs;
		RunSubGraphs = new ();
		ViewEntity = null;

	}

	protected RenderGraph Graph;
	protected NodeState Node;
	protected List<SlotValue> Inputs;
	protected List<SlotValue?> Outputs;
	protected List<RunSubGraphCommand> RunSubGraphs;
	/// <summary>
	/// The entity associated with the render graph being executed.
	/// </summary>
	/// <remarks>
	/// This is optional because you aren't required to have a view entity for a node. e.g. compute shaders
	/// It should always be set when the <see cref="RenderGraph"/> is running on a View.
	/// </remarks>
	protected ulong? ViewEntity;

	public IReadOnlyList<SlotValue> GetInputs() => Inputs;

	public IReadOnlyList<SlotInfo> GetInputInfo() => Node.InputSlots;

	public IReadOnlyList<SlotValue?> GetOutputs() => Outputs;

	public IReadOnlyList<SlotInfo> GetOutputInfo() => Node.OutputSlots;

	/// <summary>
	/// Gets the input slot value by name.
	/// </summary>
	/// <param name="label">Slot to search for</param>
	/// <returns>The slot value</returns>
	/// <exception cref="ArgumentException">Thrown when slot not found</exception>
	public SlotValue GetInput(IIntoSlotLabel label)
	{
		var index = label.IntoSlotLabel().IndexOf(Node.InputSlots);
		if (index < 0) {
			throw new ArgumentException($"Input slot '{label}' not found");
		}
		return Inputs[index];
	}

	public Texture GetInputTexture(IIntoSlotLabel intoLabel)
	{
		return GetInput(intoLabel).GetTexture();
	}

	public Sampler GetInputSampler(IIntoSlotLabel intoLabel)
	{
		return GetInput(intoLabel).GetSampler();
	}

	public Buffer GetInputBuffer(IIntoSlotLabel intoLabel)
	{
		return GetInput(intoLabel).GetBuffer();
	}

	public ulong GetInputEntity(IIntoSlotLabel intoLabel)
	{
		return GetInput(intoLabel).GetEntity();
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="intoLabel"></param>
	/// <param name="intoValue"></param>
	/// <exception cref="ArgumentException">When the slot label is invalid</exception>
	public void SetOutput(IIntoSlotLabel intoLabel, IIntoSlotValue intoValue)
	{
		var index = intoLabel.IntoSlotLabel().IndexOf(Node.OutputSlots);
		if (index < 0) {
			throw new ArgumentException($"Output slot '{intoLabel}' not found");
		}
		var value = intoValue.IntoSlotValue();
		var slot = Node.OutputSlots[index];
		if (value.Type != slot.Type) {
			throw new ArgumentException($"Output slot '{intoLabel}' is not of type {slot.Type}");
		}
		Outputs[index] = value;
	}

	/// <summary>
	/// Gets the view entity associated with this render graph execution.
	/// </summary>
	/// <returns>The view entity ID</returns>
	/// <exception cref="InvalidOperationException">Thrown when no view entity is set</exception>
	public ulong GetViewEntity()
	{
		return ViewEntity ?? throw new InvalidOperationException("No view entity is set for this render graph context");
	}

	/// <summary>
	/// Gets the optional view entity associated with this render graph execution.
	/// </summary>
	/// <returns>The view entity ID, or null if none is set</returns>
	public ulong? GetOptionalViewEntity()
	{
		return ViewEntity;
	}

	/// <summary>
	/// Sets the view entity for this render graph execution.
	/// </summary>
	/// <param name="entity">The entity ID to set as the view entity</param>
	public void SetViewEntity(ulong entity)
	{
		ViewEntity = entity;
	}

	/// <summary>
	/// Queues a subgraph for execution with the specified inputs.
	/// </summary>
	/// <param name="label">label of the subgraph to run</param>
	/// <param name="inputs">Input values for the subgraph</param>
	/// <param name="viewEntity">Optional view entity for the subgraph</param>
	public void RunSubGraph(ISubgraphLabel label, List<SlotValue> inputs, ulong? viewEntity = null)
	{
		var subgraph = Graph.GetSubGraph(label);
		if (subgraph == null) {
			throw new ArgumentException($"Subgraph '{label}' not found");
		}
		var input = subgraph.GetInputNode();


		var command = new RunSubGraphCommand {
			SubgraphLabel = label,
			Inputs = inputs,
			ViewEntity = viewEntity ?? ViewEntity
		};
		RunSubGraphs.Add(command);
	}

	/// <summary>
	/// Gets all queued subgraph commands.
	/// </summary>
	/// <returns>Read-only list of subgraph commands</returns>
	public IReadOnlyList<RunSubGraphCommand> GetSubGraphCommands()
	{
		return RunSubGraphs.AsReadOnly();
	}

	/// <summary>
	/// Clears all queued subgraph commands.
	/// </summary>
	public void ClearSubGraphCommands()
	{
		RunSubGraphs.Clear();
	}
}

/// <summary>
/// A command that signals the <see cref="RenderGraphRunner"/> to run the sub-<see cref="RenderGraph"/> corresponding to <see cref="SubgraphLabel"/>
/// with the specified `inputs` next.
/// </summary>
public struct RunSubGraphCommand
{
	public ISubgraphLabel SubgraphLabel;
	public List<SlotValue> Inputs;
	/// <summary>
	/// Optional entity that will be used as the view for the subgraph.
	/// </summary>
	public ulong? ViewEntity;
}