using System.Text.Json.Serialization;

public sealed class GtinTokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}
