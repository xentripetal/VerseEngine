using QuikGraph;
using QuikGraph.Graphviz;
using QuikGraph.Graphviz.Dot;
using Verse.ECS.Scheduling;
using Verse.ECS.Scheduling.Graph;

namespace ProjectVerse.Utility;

public static class ScheduleRender
{
	public static string RenderScheduleHierarchy(Schedule schedule)
	{
		return schedule.Graph.GetHierarchy().ToGraphviz(AlgoFor(schedule));
	}
	public static string RenderScheduleDependency(Schedule schedule)
	{
		return schedule.Graph.GetDependencyGraph().ToGraphviz(AlgoFor(schedule));
	}

	private static Action<GraphvizAlgorithm<NodeId, Edge<NodeId>>> AlgoFor(Schedule schedule)
	{
		return algorithm => {
			algorithm.CommonEdgeFormat.ToolTip = "Edge tooltip";
			algorithm.FormatVertex += (sender, args) => {
				args.VertexFormat.Label = schedule.Graph.GetNodeName(args.Vertex);
				if (args.Vertex.IsSet) {
					args.VertexFormat.Shape = GraphvizVertexShape.Diamond;
				}
			};
		};
	}

}