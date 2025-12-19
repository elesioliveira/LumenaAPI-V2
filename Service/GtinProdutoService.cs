using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

public interface IGtinProdutoService
{
    Task<string> ConsultarProdutoAsync(string gtin);
}

public sealed class GtinProdutoService : IGtinProdutoService
{
    private readonly HttpClient _httpClient;
    private readonly IGtinTokenService _tokenService;
    private readonly GtinApiOptions _options;

    public GtinProdutoService(
        HttpClient httpClient,
        IGtinTokenService tokenService,
        IOptions<GtinApiOptions> options)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _options = options.Value;
    }

    public async Task<string> ConsultarProdutoAsync(string gtin)
    {


        var token = await _tokenService.GetTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.BaseUrl}/{gtin}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
