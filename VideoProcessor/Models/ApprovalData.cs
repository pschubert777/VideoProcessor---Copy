using Azure;
using Azure.Data.Tables;

namespace VideoProcessor.Models
{
    public class ApprovalData : ITableEntity
    {
        public const string TableName = nameof(ApprovalData);
        public const string ApprovalPartitionKey = "Approval";

        public string PartitionKey { get; set; } = ApprovalPartitionKey;
        public string RowKey { get; set; }
        public string OrchestrationId { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
