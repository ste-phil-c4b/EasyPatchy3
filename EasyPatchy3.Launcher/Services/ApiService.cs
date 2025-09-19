using EasyPatchy3.Launcher.Models;
using System.Text.Json;

namespace EasyPatchy3.Launcher.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("EasyPatchyApi");
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<List<AppVersionDto>> GetAvailableVersionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/versions");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<AppVersionDto>>(json, _jsonOptions) ?? new List<AppVersionDto>();
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Failed to fetch available versions: {ex.Message}", ex);
        }
    }

    public async Task<Stream> DownloadVersionAsync(int versionId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/versions/{versionId}/download");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync();
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Failed to download version {versionId}: {ex.Message}", ex);
        }
    }

    public async Task<PatchDto?> GetPatchAsync(int sourceVersionId, int targetVersionId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/patches/{sourceVersionId}/{targetVersionId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PatchDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Failed to get patch from {sourceVersionId} to {targetVersionId}: {ex.Message}", ex);
        }
    }

    public async Task<Stream> DownloadPatchAsync(int patchId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/patches/{patchId}/download");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync();
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Failed to download patch {patchId}: {ex.Message}", ex);
        }
    }
}