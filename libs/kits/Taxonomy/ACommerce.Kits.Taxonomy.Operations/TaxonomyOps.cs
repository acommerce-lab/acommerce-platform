using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Taxonomy.Operations;

public static class TaxonomyOps
{
    // قِراءَة
    public static readonly OperationType TreeGet     = new("taxonomy.tree.get");
    public static readonly OperationType PathGet     = new("taxonomy.path.get");
    public static readonly OperationType SubtreeGet  = new("taxonomy.subtree.get");

    // إدارَة (admin)
    public static readonly OperationType NodeCreate  = new("taxonomy.node.create");
    public static readonly OperationType NodeUpdate  = new("taxonomy.node.update");
    public static readonly OperationType NodeDelete  = new("taxonomy.node.delete");
    public static readonly OperationType NodeMove    = new("taxonomy.node.move");
}

public static class TaxonomyTagKeys
{
    public static readonly TagKey RootCode = new("taxonomy_root");
    public static readonly TagKey NodeCode = new("taxonomy_code");
    public static readonly TagKey NodeId   = new("taxonomy_node_id");
}
