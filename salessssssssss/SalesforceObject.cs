namespace salessssssssss
{
    public class SalesforceObject
    {
        public string Label { get; set; }
        public string ApiName { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string DeploymentStatus { get; set; }
        public int ChildRelationshipsCount { get; set; }
        public int Fields { get; set; }
        public int ValidationResults { get; set; }
        public int RecordTypeInfos { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedDate { get; set; } // Add this property
    }

}
