using System.Threading.Channels;
using FluentResults;
using Verse.Core;
using Verse.ECS;
using Verse.MoonWorks.Graphics;
using Verse.Tracing;

namespace Verse.Render.Graph;

/// <summary>
/// Executes render graphs by running nodes in topological order
/// </summary>
public partial class RenderGraphRunner
{

	[Schedule(RenderSchedules.Render)]
	[InSet<RenderSets>(RenderSets.Render)]
	public static void Render(World world, RenderGraph graph, GraphicsDevice device)
	{
		// todo resource scoping
		graph.Update(world);
		var res = Run(graph, device, world);
		if (res.IsFailed) {
			throw new InvalidOperationException(res.ToString());
		}
		// Bevy calls Present for every window, but SDL doesn't have the concept
	}

	public static Result Run(RenderGraph graph, GraphicsDevice device, World world)
	{
		var renderContext = new RenderContext(device);
		var result = RunGraph(graph, null, renderContext, world, [], null);
		if (result.IsFailed) {
			return result;
		}
		// TODO bevy has a callback here for screenshots and such. Check if we should do something similar
		var buffers = renderContext.Finish();
		var fences = new Fence[buffers.Count];
		for (int i = 0; i < buffers.Count; i++) {
			fences[i] = device.SubmitAndAcquireFence(buffers[i]);
		}
		device.WaitForFences(true, fences);
		for (int i = 0; i < fences.Length; i++) {
			device.ReleaseFence(fences[i]);
		}
		Profiler.EmitFrameMark();
		return Result.Ok();
	}

	// todo lots of memory optimizations here to make this close to zero-alloc. For now ported
	// it directly as is from bevy.

	/// <summary>
	/// Runs the <see cref="RenderGraph"/> and all its sub-graphs sequentially, making sure that
	/// all nodes run in the correct order (a node only runs when all its dependencies have run).
	/// </summary>
	private static Result RunGraph(
		RenderGraph graph, ISubgraphLabel? subgraph, RenderContext renderContext, World world, List<SlotValue> inputs, ulong? viewEntity)
	{
		var allNodeOutputs = new Dictionary<IRenderLabel, List<SlotValue>>();
		var queue = new LinkedList<NodeState>();

		// Queue up any nodes without inputs as they can be ran immediately.
		foreach (var node in graph) {
			if (node.InputSlots.Count == 0) {
				queue.AddLast(node);
			}
		}

		// Pass inputs to the graph
		var inputNode = graph.GetInputNode();
		if (inputNode != null) {
			if (inputs.Count != inputNode.InputSlots.Count) {
				return Result.Fail($"graph {subgraph} could not be run because it expected {inputNode.InputSlots.Count} inputs but got {inputs.Count} inputs");
			}
			for (var i = 0; i < inputNode.InputSlots.Count; i++) {
				if (inputNode.InputSlots[i].Type != inputs[i].Type) {
					return Result.Fail(
						$"graph {subgraph} could not be run because input slot {i} is of type {inputNode.InputSlots[i].Type} but got {inputs[i].Type}");
				}
			}
			allNodeOutputs[inputNode.Label] = inputs;
			foreach (var (_, inNode) in graph.IterNodeOutputs(inputNode.Label)) {
				queue.AddFirst(inNode);
			}
		}

	handle_node:
		while (queue.Count > 0) {
			var nodeState = queue.Last!.Value;
			queue.RemoveLast();
			if (allNodeOutputs.ContainsKey(nodeState.Label)) {
				continue;
			}
			var indicesAndInputs = new List<(int, SlotValue)>(4);
			// check if all dependencies have finished running
			foreach (var (edge, depNode) in graph.IterNodeInputs(nodeState.Label)) {
				if (edge.IsSlotEdge) {
					if (allNodeOutputs.TryGetValue(edge.InputNode, out var outputs)) {
						indicesAndInputs.Add((edge.InputIndex, outputs[edge.OutputIndex]));
					} else {
						queue.AddFirst(nodeState);
						goto handle_node;
					}
				} else {
					if (!allNodeOutputs.ContainsKey(edge.InputNode)) {
						queue.AddFirst(nodeState);
						goto handle_node;
					}
				}
			}
			indicesAndInputs.Sort((a, b) => a.Item1 - b.Item1);
			var nodeInputs = new List<SlotValue>(indicesAndInputs.Count);
			foreach (var (idx, value) in indicesAndInputs) {
				nodeInputs.Add(value);
			}

			if (nodeInputs.Count != nodeState.Node.InputSlots.Count) {
				return Result.Fail(
					$"graph {subgraph} could not be run because node {nodeState.Label} expected {nodeState.Node.InputSlots.Count} inputs but got {inputs.Count} inputs");
			}

			var nodeOutputs = new List<SlotValue?>();
			// Run the node
			{
				var ctx = new RenderGraphContext(graph, nodeState, inputs, nodeOutputs);
				if (viewEntity != null) {
					ctx.SetViewEntity(viewEntity.Value);
				}
				nodeState.Node.Run(ctx, renderContext, world);
				foreach (var subgraphCmd in ctx.GetSubGraphCommands()) {
					var subgraphToRun = graph.GetSubGraph(subgraphCmd.SubgraphLabel);
					if (subgraphToRun == null) {
						throw new InvalidOperationException($"Subgraph {subgraphCmd.SubgraphLabel} not found");
					}
					RunGraph(subgraphToRun, subgraphCmd.SubgraphLabel, renderContext, world, subgraphCmd.Inputs, subgraphCmd.ViewEntity);
				}
			}

			// process the outputs
			var values = new List<SlotValue>(nodeOutputs.Count);
			for (int i = 0; i < nodeOutputs.Count; i++) {
				var value = nodeOutputs[i];
				if (value.HasValue) {
					values.Add(value.Value);
				} else {
					return Result.Fail($"graph {subgraph} could not be run because node {nodeState.Label} output {i} was null");
				}
			}
			allNodeOutputs[nodeState.Label] = values;
			foreach (var (_, outNode) in graph.IterNodeOutputs(nodeState.Label)) {
				queue.AddFirst(outNode);
			}
		}
		return Result.Ok();
	}
}