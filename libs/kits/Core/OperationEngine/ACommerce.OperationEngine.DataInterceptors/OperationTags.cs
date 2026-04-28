namespace ACommerce.OperationEngine.DataInterceptors;

public static class OperationTags
{
    public const string DbAction = "db_action";
    public const string TargetEntity = "target_entity";

    public static class DbActions
    {
        public const string Create = "create";
        public const string Update = "update";
        public const string Delete = "delete";
        public const string Read = "read";
    }
}
