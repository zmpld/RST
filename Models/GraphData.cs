namespace RelationshipVisualizer.Models
{
    public class GraphData
    {
        public List<Node> Nodes { get; set; } = new();
        public List<Edge> Edges { get; set; } = new();
    }
}