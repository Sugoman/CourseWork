using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text.Json.Serialization;
using LearningTrainer.Context; 
using LearningTrainerShared.Models;

namespace LearningAPI.Features.Dictionaries.Queries.GetDictionaries
{
    public class GetDictionariesHandler : IRequestHandler<GetDictionariesQuery, List<Dictionary>>
    {
        private readonly ApiDbContext _context; 
        private readonly IDistributedCache _cache; 

        public GetDictionariesHandler(ApiDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<List<Dictionary>> Handle(GetDictionariesQuery request, CancellationToken cancellationToken)
        {
            // 1. Ключ для кэша: уникальный для каждого юзера
            string cacheKey = $"dictionaries:user:{request.UserId}";

            // 2. Проверяем Redis
            var cachedJson = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedJson))
            {
                // Нашли! Десериализуем и отдаем
                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.Preserve,
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<List<Dictionary>>(cachedJson, options);
            }

            // 3. Если в Redis пусто — идем в БД (твоя старая логика)

            // Сначала ищем учителя (как у тебя было в контроллере)
            var currentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            var teacherId = currentUser?.UserId; // UserId здесь - это ID учителя (странный нейминг в БД, но ок)

            // Запрос данных
            var dictionaries = await _context.Dictionaries
                .Where(d => d.UserId == request.UserId || (teacherId != null && d.UserId == teacherId))
                .Include(d => d.Words) // Важно подгрузить слова!
                .AsNoTracking() // Для чтения это быстрее
                .ToListAsync(cancellationToken);

            // 4. Сохраняем в Redis на 5 минут
            var saveOptions = new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
            var jsonToSave = JsonSerializer.Serialize(dictionaries, saveOptions);

            await _cache.SetStringAsync(cacheKey, jsonToSave, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }, cancellationToken);

            return dictionaries;
        }
    }
}