using LearningTrainer.Services;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Services;

/// <summary>
/// Фоновый воркер: читает задачи из TranscriptionChannel,
/// проверяет кэш, при необходимости обращается к внешнему API,
/// записывает результат в кэш и обновляет слово в БД.
/// </summary>
public sealed class TranscriptionBackgroundService : BackgroundService
{
    private readonly TranscriptionChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TranscriptionBackgroundService> _logger;

    // Ограничиваем параллельные HTTP-запросы к внешнему API
    private readonly SemaphoreSlim _throttle = new(5, 5);

    public TranscriptionBackgroundService(
        TranscriptionChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<TranscriptionBackgroundService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TranscriptionBackgroundService started");

        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            // Не блокируем чтение канала — обрабатываем параллельно
            _ = ProcessAsync(request, stoppingToken);
        }

        _logger.LogInformation("TranscriptionBackgroundService stopped");
    }

    private async Task ProcessAsync(TranscriptionRequest request, CancellationToken ct)
    {
        await _throttle.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var dictService = scope.ServiceProvider.GetRequiredService<ExternalDictionaryService>();

            var wordLower = request.OriginalWord.Trim().ToLowerInvariant();

            // 1. Проверяем кэш
            var cached = await db.TranscriptionCache
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.WordLower == wordLower, ct);

            string? transcription;

            if (cached != null)
            {
                transcription = cached.Transcription;
                _logger.LogDebug("Cache hit for '{Word}'", wordLower);
            }
            else
            {
                // 2. Запрашиваем внешний API
                transcription = await dictService.GetTranscriptionAsync(request.OriginalWord);

                // 3. Сохраняем в кэш (даже null — чтобы не ходить повторно)
                db.TranscriptionCache.Add(new TranscriptionCache
                {
                    WordLower = wordLower,
                    Transcription = transcription,
                    CreatedAtUtc = DateTime.UtcNow
                });

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Другой воркер мог вставить запись параллельно — игнорируем дубликат
                    _logger.LogDebug("Duplicate cache entry for '{Word}', ignoring", wordLower);
                }
            }

            // 4. Обновляем слово
            if (transcription != null)
            {
                var word = await db.Words.FindAsync(new object[] { request.WordId }, ct);
                if (word != null && word.Transcription == null)
                {
                    word.Transcription = transcription;
                    await db.SaveChangesAsync(ct);
                    _logger.LogDebug("Transcription updated for Word {WordId}: {Transcription}",
                        request.WordId, transcription);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to process transcription for Word {WordId}", request.WordId);
        }
        finally
        {
            _throttle.Release();
        }
    }
}
