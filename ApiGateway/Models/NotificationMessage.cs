using System.Text.Json.Serialization;

namespace ApiGateway.Models
{
    public class NotificationMessage
    {
        public string Type { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Data { get; set; }

        public const string MontageStopType = "montage_stop";
    }
}