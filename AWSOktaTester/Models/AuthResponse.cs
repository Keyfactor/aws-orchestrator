using Newtonsoft.Json;

namespace AWSOktaTester.Models
{
    public class AuthResponse
    {
        [JsonProperty("token_type", NullValueHandling = NullValueHandling.Ignore)] public string TokenType { get; set; }
        [JsonProperty("expires_in", NullValueHandling = NullValueHandling.Ignore)] public int ExpiresIn { get; set; }
        [JsonProperty("access_token", NullValueHandling = NullValueHandling.Ignore)] public string AccessToken { get; set; }
        [JsonProperty("scope", NullValueHandling = NullValueHandling.Ignore)] public string Scope { get; set; }
    }
}
