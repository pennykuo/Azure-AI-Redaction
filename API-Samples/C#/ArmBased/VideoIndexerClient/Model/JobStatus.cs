using System.Text.Json.Serialization;

namespace VideoIndexingARMAccounts.VideoIndexerClient.Model;

public class JobStatus
{
    [JsonPropertyName("creationTime")]
    public string CreationTime { get; set; }
    [JsonPropertyName("lastUpdateTime")]
    public string LastUpdateTime { get; set; }
    [JsonPropertyName("progress")]
    public int Progress { get; set; }
    [JsonPropertyName("jobType")]
    public string JobType { get; set; }
    [JsonPropertyName("state")]
    public string State { get; set; }
}