using LearningTrainerShared.Models.KnowledgeTreeDto;
using System.Net.Http.Json;

namespace LearningTrainerWeb.Services;

public interface IKnowledgeTreeApiService
{
    Task<KnowledgeTreeState?> GetTreeStateAsync();
    Task<PourXpResponse?> PourXpAsync(long amount);
    Task<List<TreeSkinInfo>> GetSkinsAsync();
    Task<bool> ChangeSkinAsync(int skinId);
}

public class KnowledgeTreeApiService : IKnowledgeTreeApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly ILogger<KnowledgeTreeApiService> _logger;

    public KnowledgeTreeApiService(HttpClient httpClient, AuthTokenProvider tokenProvider, ILogger<KnowledgeTreeApiService> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    private async Task ApplyAuthAsync()
        => await _tokenProvider.EnsureValidTokenAsync(_httpClient);

    public async Task<KnowledgeTreeState?> GetTreeStateAsync()
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.GetAsync("api/knowledge-tree/state");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<KnowledgeTreeState>();

            _logger.LogWarning("GetTreeState failed: {Status}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tree state");
            return null;
        }
    }

    public async Task<List<TreeSkinInfo>> GetSkinsAsync()
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.GetAsync("api/knowledge-tree/skins");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<TreeSkinInfo>>() ?? [];

            _logger.LogWarning("GetSkins failed: {Status}", response.StatusCode);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tree skins");
            return [];
        }
    }

    public async Task<PourXpResponse?> PourXpAsync(long amount)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.PostAsJsonAsync("api/knowledge-tree/pour-xp", new PourXpRequest { Amount = amount });
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<PourXpResponse>();

            _logger.LogWarning("PourXp failed: {Status}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pouring XP");
            return null;
        }
    }

    public async Task<bool> ChangeSkinAsync(int skinId)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.PutAsJsonAsync("api/knowledge-tree/skin", new { SkinId = skinId });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing tree skin");
            return false;
        }
    }
}
