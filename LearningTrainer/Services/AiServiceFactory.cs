using LearningTrainerShared.Models.Features.Ai;
using Microsoft.Extensions.Configuration;
using System.Net.Http;

namespace LearningTrainer.Services;

/// <summary>
/// Единая фабрика для создания IAiTranslationService.
/// Устраняет дублирование CreateAiService() в трёх ViewModel-ах.
/// </summary>
public static class AiServiceFactory
{
    private static readonly object _lock = new();
    private static IAiTranslationService? _instance;

    /// <summary>
    /// Возвращает singleton-экземпляр IAiTranslationService (AiTranslationWithFallback).
    /// BaseUrl читается из appsettings.json → AiService:BaseUrl.
    /// </summary>
    public static IAiTranslationService Create()
    {
        if (_instance != null)
            return _instance;

        lock (_lock)
        {
            if (_instance != null)
                return _instance;

            var baseUrl = "http://85.217.170.223:5200";
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true)
#if DEBUG
                    .AddJsonFile("appsettings.Development.json", optional: true)
#endif
                    .Build();

                var configUrl = config["AiService:BaseUrl"];
                if (!string.IsNullOrWhiteSpace(configUrl))
                    baseUrl = configUrl;
            }
            catch { }

            var ai = new AiTranslationHttpService(baseUrl);
            var translationFallback = new TranslationService();
            var exampleFallback = new ExternalDictionaryService(new HttpClient());
            _instance = new AiTranslationWithFallback(ai, translationFallback, exampleFallback);
            return _instance;
        }
    }
}
