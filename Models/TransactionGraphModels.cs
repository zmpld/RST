namespace RelationshipVisualizer.Models
{
    public class TransactionGraphData
    {
        public List<TransactionGraphNode> Nodes { get; set; } = new();
        public List<TransactionGraphEdge> Edges { get; set; } = new();
    }

    public class TransactionGraphNode
    {
        public string Id { get; set; } = null!;
        public string Label { get; set; } = null!;
        public string Group { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Shape { get; set; } = null!;

        public string? TransactionType { get; set; }
    }

    public class TransactionGraphEdge
    {
        public string Id { get; set; } = null!;
        public string From { get; set; } = null!;
        public string To { get; set; } = null!;
        public string Label { get; set; } = null!;
        public decimal RawAmount { get; set; }
    }
}
