using System.Collections;
using FluentResults;
using Verse.ECS;

namespace Verse.Render.Graph;

/// <summary>
/// The label for <see cref="GraphInputNode"/> of a graph. Used to connect other nodes to it.
/// </summary>
public struct GraphInput : IRenderLabel
{
	public bool Equals(IRenderLabel? other) => other is GraphInput;
	public override bool Equals(object? obj) => obj is GraphInput;
	public override int GetHashCode() => typeof(GraphInput).GetHashCode();
	public static GraphInput Label() => new GraphInput();
}

/// <summary>
/// The render graph configures modular and re-usable render logic.
/// It is a retained structure that cannot be modified while being executed.
/// </summary>
public class RenderGraph : IEnumerable<NodeState>
{
	private readonly Dictionary<IRenderLabel, NodeState> _nodes = new ();
	private readonly Dictionary<ISubgraphLabel, RenderGraph> _subGraphs = new ();

	/// <summary>
	/// Updates all nodes and subgraphs of the render graph. Should be called before executing this graph.
	/// </summary>
	/// <param name="world"></param>
	public void Update(World world)
	{
		foreach (var node in _nodes.Values) {
			node.Node.Update(world);
		}
		foreach (var subGraph in _subGraphs.Values) {
			subGraph.Update(world);
		}
	}

	/// <summary>
	/// Creates a <see cref="GraphInputNode"/> with the specified splots if not already present
	/// </summary>
	/// <param name="inputs"></param>
	/// <exception cref="InvalidOperationException"></exception>
	public void SetInput(List<SlotInfo> inputs)
	{
		if (_nodes.ContainsKey(GraphInput.Label())) {
			throw new InvalidOperationException("Input node already exists");
		}
		AddNode(GraphInput.Label(), new GraphInputNode(inputs));
	}



	/// <summary>
	/// Returns the <see cref="NodeState"/> of the input node for this graph. 
	/// </summary>
	/// <returns>The input node or null if not present</returns>
	public NodeState? GetInputNode()
	{
		return _nodes!.GetValueOrDefault(GraphInput.Label(), null);
	}

	/// <summary>
	/// Returns the <see cref="NodeState"/> of the input node for this graph.
	/// </summary>
	/// <exception cref="KeyNotFoundException">If there is no input node</exception>
	/// <returns></returns>
	public NodeState InputNode()
	{
		return _nodes[GraphInput.Label()];
	}

	/// <summary>
	/// Adds a node to the render graph. If the node already exists, it is replaced.
	/// </summary>
	public void AddNode<T>(IRenderLabel label, T node) where T : IRenderNode
	{
		var nodeState = NodeState.New(label, node);
		_nodes[label] = nodeState;
	}
	/// <summary>
	/// Adds edges based on the order of the given <paramref name="edges"/> collection.
	/// </summary>
	/// <remarks>Defining an edge that already exists is not considered an error with this api. It simply won't create a new edge</remarks>
	/// <param name="edges"></param>
	/// <exception cref="InvalidOperationException"></exception>
	public void AddNodeEdges(ICollection<IRenderLabel> edges)
	{
		for (var i = 0; i < edges.Count() - 1; i += 2) {
			var result = TryAddNodeEdge(edges.ElementAt(i), edges.ElementAt(i + 1));
			// Ignore already existing edges as they are easily produced by this api
			if (result.IsFailed && !result.HasError<EdgeAlreadyExistsError>()) {
				throw new InvalidOperationException($"Failed to add edge {edges.ElementAt(i)} -> {edges.ElementAt(i + 1)}");
			}
		}
	}

	/// <summary>
	/// Removes the node with the label from the graph. If the label does not exist, nothing happens.
	/// </summary>
	/// <param name="label"></param>
	public void RemoveNode(IRenderLabel label)
	{
		if (_nodes.Remove(label, out var nodeState)) {
			// Remove all edges from other nodes to this one. Note that as we're removing this node, we don't need to remove its input edges
			foreach (var edge in nodeState.Edges.InputEdges) {
				GetNodeState(edge.OutputNode)?.Edges.RemoveOutputEdge(edge);
			}
			foreach (var edge in nodeState.Edges.OutputEdges) {
				GetNodeState(edge.InputNode)?.Edges.RemoveInputEdge(edge);
			}
		}
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="label"></param>
	/// <returns></returns>
	public NodeState? GetNodeState(IRenderLabel label)
	{
		return _nodes.GetValueOrDefault(label);
	}


	/// <summary>
	/// Gets a node from the render graph
	/// </summary>
	public T? GetNode<T>(IRenderLabel label) where T : class, IRenderNode
	{
		if (_nodes.TryGetValue(label, out var nodeState)) {
			return nodeState.Node as T;
		}
		return null;
	}

	/// <summary>
	/// Adds the <see cref="RenderEdge.SlotEdge"/> to the graph. This guarantees that the outputNode is run before the inputNode and also connects the two slots.
	/// </summary>
	/// <remarks>Fails if any invalid <see cref="IRenderLabel"/>s or <see cref="SlotLabel"/>s are given</remarks>
	/// <param name="outputNode"></param>
	/// <param name="outputSlot"></param>
	/// <param name="inputNode"></param>
	/// <param name="inputSlot"></param>
	/// <returns></returns>
	/// <seealso cref="AddSlotEdge"/>
	public Result TryAddSlotEdge(IRenderLabel outputNode, IIntoSlotLabel outputSlot, IRenderLabel inputNode, IIntoSlotLabel inputSlot)
	{
		var outSlot = outputSlot.IntoSlotLabel();
		var inSlot = inputSlot.IntoSlotLabel();
		var outNodeState = GetNodeState(outputNode);
		if (outNodeState == null)
			return Result.Fail(new InvalidOutputNodeSlotError(outSlot));
		var inNodeState = GetNodeState(inputNode);
		if (inNodeState == null)
			return Result.Fail(new InvalidInputNodeSlotError(inSlot));

		var outIndex = outSlot.IndexOf(outNodeState.OutputSlots);
		var inIndex = inSlot.IndexOf(inNodeState.InputSlots);
		if (outIndex == -1)
			return Result.Fail(new InvalidOutputNodeSlotError(outSlot));
		if (inIndex == -1)
			return Result.Fail(new InvalidInputNodeSlotError(inSlot));

		var edge = RenderEdge.SlotEdge(outputNode, outIndex, inputNode, inIndex);
		if (ValidateEdge(edge, false) is { IsFailed: true } result) return result;
		outNodeState.Edges.AddOutputEdge(edge);
		inNodeState.Edges.AddInputEdge(edge);
		return Result.Ok();
	}

	public void AddSlotEdge(IRenderLabel outputNode, IIntoSlotLabel outputSlot, IRenderLabel inputNode, IIntoSlotLabel inputSlot)
	{
		if (TryAddSlotEdge(outputNode, outputSlot, inputNode, inputSlot) is { IsFailed: true } result) {
			throw new InvalidOperationException(result.ToString());
		}
	}

	public Result RemoveSlotEdge(IRenderLabel outputNode, IIntoSlotLabel outputSlot, IRenderLabel inputNode, IIntoSlotLabel inputSlot)
	{
		var outSlot = outputSlot.IntoSlotLabel();
		var inSlot = inputSlot.IntoSlotLabel();
		var outNodeState = GetNodeState(outputNode);
		if (outNodeState == null)
			return Result.Fail(new InvalidOutputNodeSlotError(outSlot));
		var inNodeState = GetNodeState(inputNode);
		if (inNodeState == null)
			return Result.Fail(new InvalidInputNodeSlotError(inSlot));

		var outIndex = outSlot.IndexOf(outNodeState.OutputSlots);
		var inIndex = inSlot.IndexOf(inNodeState.InputSlots);
		if (outIndex == -1)
			return Result.Fail(new InvalidOutputNodeSlotError(outSlot));
		if (inIndex == -1)
			return Result.Fail(new InvalidInputNodeSlotError(inSlot));

		var edge = RenderEdge.SlotEdge(outputNode, outIndex, inputNode, inIndex);
		if (ValidateEdge(edge, true) is { IsFailed: true } result) return result;
		outNodeState.Edges.RemoveOutputEdge(edge);
		inNodeState.Edges.RemoveInputEdge(edge);
		return Result.Ok();

	}

	public Result TryAddNodeEdge(IRenderLabel outputNode, IRenderLabel inputNode)
	{
		var edge = RenderEdge.NodeEdge(outputNode, inputNode);
		if (ValidateEdge(edge, false) is { IsFailed: true } result) return result;
		GetNodeState(outputNode)!.Edges.AddOutputEdge(edge);
		GetNodeState(inputNode)!.Edges.AddInputEdge(edge);
		return Result.Ok();
	}

	public void AddNodeEdge(IRenderLabel outputNode, IRenderLabel inputNode)
	{
		if (TryAddNodeEdge(outputNode, inputNode) is { IsFailed: true } result) {
			throw new InvalidOperationException(result.ToString());
		}
	}

	public Result RemoveNodeEdge(IRenderLabel outputNode, IRenderLabel inputNode)
	{
		var edge = RenderEdge.NodeEdge(outputNode, inputNode);
		if (ValidateEdge(edge, true) is { IsFailed: true } result) {
			return result;
		}
		GetNodeState(outputNode)!.Edges.RemoveOutputEdge(edge);
		GetNodeState(inputNode)!.Edges.RemoveInputEdge(edge);
		return Result.Ok();
	}

	/// <summary>
	/// Verifies that the edge existence is as expected and checks that slot edges are connected correctly.
	/// </summary>
	/// <param name="edge"></param>
	/// <param name="shouldExist"></param>
	/// <returns></returns>
	public Result ValidateEdge(RenderEdge edge, bool shouldExist)
	{
		if (shouldExist && !HasEdge(edge)) {
			return Result.Fail(new EdgeDoesNotExistError((edge)));
		}
		if (!shouldExist && HasEdge(edge)) {
			return Result.Fail(new EdgeAlreadyExistsError(edge));
		}

		if (edge.IsSlotEdge) {
			var outNodeState = GetNodeState(edge.OutputNode)!;
			var inNodeState = GetNodeState(edge.InputNode)!;
			if (outNodeState.OutputSlots.Count <= edge.OutputIndex) {
				return Result.Fail(new InvalidOutputNodeSlotError(SlotLabel.OfIndex(edge.OutputIndex)));
			}
			var outSlot = outNodeState.OutputSlots[edge.OutputIndex];

			if (inNodeState.InputSlots.Count <= edge.InputIndex) {
				return Result.Fail(new InvalidInputNodeSlotError(SlotLabel.OfIndex(edge.InputIndex)));
			}
			var inSlot = inNodeState.InputSlots[edge.InputIndex];


			if (!shouldExist) {
				foreach (var inEdge in inNodeState.Edges.InputEdges) {
					if (inEdge.IsSlotEdge && inEdge.InputIndex == edge.InputIndex) {
						return Result.Fail(new NodeSlotAlreadyOccupiedError(inNodeState.Label, inEdge.InputIndex, inEdge.OutputNode));
					}
				}
			}

			if (outSlot.Type != inSlot.Type) {
				return Result.Fail(new MismatchedNodeSlotsError(inNodeState.Label, edge.InputIndex, outNodeState.Label, edge.OutputIndex));
			}

		}
		return Result.Ok();
	}

	/// <summary>
	/// Checks whether the edge already exists in the graph
	/// </summary>
	/// <returns></returns>
	public bool HasEdge(RenderEdge edge)
	{
		var outNode = GetNodeState(edge.OutputNode);
		var inNode = GetNodeState(edge.InputNode);
		if (outNode == null || inNode == null) {
			return false;
		}
		return outNode.Edges.OutputEdges.Contains(edge) && inNode.Edges.InputEdges.Contains(edge);
	}


	/// <summary>
	/// Gets all nodes in topological order for execution
	/// </summary>
	public IEnumerable<NodeState> GetExecutionOrder()
	{
		var visited = new HashSet<IRenderLabel>();
		var visiting = new HashSet<IRenderLabel>();
		var result = new List<NodeState>();

		foreach (var node in _nodes.Keys) {
			if (!visited.Contains(node)) {
				TopologicalSort(node, visited, visiting, result);
			}
		}

		return result;
	}

	private void TopologicalSort(IRenderLabel nodeLabel, HashSet<IRenderLabel> visited, HashSet<IRenderLabel> visiting, List<NodeState> result)
	{
		if (visiting.Contains(nodeLabel))
			throw new InvalidOperationException($"Circular dependency detected at node {nodeLabel}");

		if (visited.Contains(nodeLabel))
			return;

		visiting.Add(nodeLabel);

		if (_nodes.TryGetValue(nodeLabel, out var nodeState)) {
			foreach (var edge in nodeState.Edges.InputEdges) {
				TopologicalSort(edge.OutputNode, visited, visiting, result);
			}
		}

		visiting.Remove(nodeLabel);
		visited.Add(nodeLabel);

		if (nodeState != null) {
			result.Add(nodeState);
		}
	}

	public void AddSubGraph(ISubgraphLabel label, RenderGraph subGraph)
	{
		_subGraphs[label] = subGraph;
	}

	public void RemoveSubGraph(ISubgraphLabel label)
	{
		_subGraphs.Remove(label);
	}

	public RenderGraph? GetSubGraph(ISubgraphLabel label)
	{
		_subGraphs.TryGetValue(label, out var subGraph);
		return subGraph;
	}

	public RenderGraph SubGraph(ISubgraphLabel label)
	{
		if (_subGraphs.TryGetValue(label, out var subGraph)) {
			return subGraph;
		}
		throw new InvalidOperationException($"Subgraph {label} does not exist");
	}

	/// <summary>
	/// Iterate over a tuple of the output edges and the corresponding input nodes
	/// for the node referenced by the label.
	/// </summary>
	/// <param name="label"></param>
	/// <returns></returns>
	public IEnumerable<(RenderEdge, NodeState)> IterNodeOutputs(IRenderLabel label)
	{
		var node = GetNodeState(label);
		if (node == null) {
			yield break;
		}
		foreach (var edge in node.Edges.OutputEdges) {
			yield return (edge, GetNodeState(edge.InputNode)!);
		}
	}

	/// <summary>
	/// Iterate over a tuple of the input edges and the corresponding output nodes
	/// for the node referenced by the label.
	/// </summary>
	/// <param name="label"></param>
	/// <returns></returns>
	public IEnumerable<(RenderEdge, NodeState)> IterNodeInputs(IRenderLabel label)
	{
		var node = GetNodeState(label);
		if (node == null) {
			yield break;
		}
		foreach (var edge in node.Edges.InputEdges) {
			yield return (edge, GetNodeState(edge.OutputNode)!);
		}
	}

	public IEnumerator<NodeState> GetEnumerator() => _nodes.Values.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// A strongly-typed class of labels used to identify a <see cref="IRenderNode"/> in a <see cref="RenderGraph"/>
/// </summary>
public interface IRenderLabel : IEquatable<IRenderLabel> { }

/// <summary>
/// A strongly-typed class of labels used to identify a subgraph for <see cref="RenderGraph"/>
/// </summary>
public interface ISubgraphLabel : ILabel
{
}

public static class EnumExtensions
{
	public static IRenderLabel AsRenderLabel(this Enum value) => new EnumRenderLabel(value);
}

[Label<ISubgraphLabel>]
public partial struct ExampleLabel { }

public readonly struct EnumRenderLabel(Enum value) : IRenderLabel
{
	public readonly Enum Value = value;
	public bool Equals(EnumRenderLabel other) => other.Value.Equals(Value);
	public bool Equals(IRenderLabel? other) => other is EnumRenderLabel otherEnum && Equals(otherEnum);
	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		return obj.GetType() == GetType() && Equals((EnumRenderLabel)obj);
	}
	public override int GetHashCode() => Value.GetHashCode();
}