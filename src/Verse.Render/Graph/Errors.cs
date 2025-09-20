using FluentResults;

namespace Verse.Render.Graph;

public abstract class RenderGraphError(string reason) : Error(reason) { }
public class EdgeDoesNotExistError(RenderEdge edge) : RenderGraphError($"Edge {edge} does not exist") { }
public class EdgeAlreadyExistsError(RenderEdge edge) : RenderGraphError($"Edge {edge} already exists") { }
public class InvalidOutputNodeSlotError(SlotLabel label) : RenderGraphError($"Output node slot does not exist {label}") { }
public class InvalidInputNodeSlotError(SlotLabel label) : RenderGraphError($"Input node slot does not exist {label}") { }

public class NodeSlotAlreadyOccupiedError(IRenderLabel node, int slot, IRenderLabel occupiedBy)
	: RenderGraphError($"node {node} input slot {slot} already occupied by {occupiedBy}") { }

public class MismatchedNodeSlotsError(IRenderLabel inNode, int inSlot, IRenderLabel outNode, int outSlot)
	: RenderGraphError($"attempted to connect output slot {outSlot} from node {outNode} to incompatible input slot {inSlot} from node {inNode}") { }