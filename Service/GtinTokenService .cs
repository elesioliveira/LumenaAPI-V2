using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

public interface IGtinTokenService
{
    Task<string> GetTokenAsync();
    void ClearTokenCache();
}

public sealed class GtinTokenService : IGtinTokenService
{
    private const string CacheKey = "GTIN_API_TOKEN";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly GtinApiOptions _options;

    public GtinTokenService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<GtinApiOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<string> GetTokenAsync()
    {
        if (_cache.TryGetValue(CacheKey, out string token))
            return token;

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", _options.BasicAuth);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        request.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        using var response = await _httpClient.SendAsync(request);

        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(raw);

        var tokenResponse =
            JsonSerializer.Deserialize<GtinTokenResponse>(raw)
            ?? throw new InvalidOperationException("Resposta inválida da API GTIN");

        if (string.IsNullOrWhiteSpace(tokenResponse.Token))
            throw new InvalidOperationException($"Token vazio retornado: {raw}");

        // cache por 55 minutos (token expira em 1h)
        _cache.Set(
            CacheKey,
            tokenResponse.Token,
            TimeSpan.FromMinutes(55));

        return tokenResponse.Token;
    }

    public void ClearTokenCache()
    {
        _cache.Remove(CacheKey);
    }


}
