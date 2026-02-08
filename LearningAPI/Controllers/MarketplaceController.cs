using LearningAPI.Extensions;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System.Text.Json;

namespace LearningAPI.Controllers;

/// <summary>
/// Контроллер для маркетплейса - публичного обмена словарями и правилами
/// </summary>
[ApiController]
[Route("api/marketplace")]
public class MarketplaceController : BaseApiController
{
    private readonly ApiDbContext _context;
    private readonly ILogger<MarketplaceController> _logger;
    private readonly IDistributedCache _cache;

    public MarketplaceController(ApiDbContext context, ILogger<MarketplaceController> logger, IDistributedCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    #region Public Dictionaries

    /// <summary>
    /// Получить список публичных словарей с пагинацией и фильтрацией
    /// </summary>
    [HttpGet("dictionaries")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicDictionaries(
        [FromQuery] string? search,
        [FromQuery] string? languageFrom,
        [FromQuery] string? languageTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 9)
    {
        var cacheKey = $"marketplace:dicts:{search}:{languageFrom}:{languageTo}:{page}:{pageSize}";
        var cached = await _cache.TryGetStringAsync(cacheKey);
        if (cached != null)
        {
            return Content(cached, "application/json");
        }

        var query = _context.Dictionaries
            .Include(d => d.User)
            .Include(d => d.Words)
            .Where(d => d.IsPublished);

        // Фильтрация
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d => 
                d.Name.Contains(search) || 
                d.Description.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(languageFrom))
        {
            query = query.Where(d => d.LanguageFrom == languageFrom);
        }

        if (!string.IsNullOrWhiteSpace(languageTo))
        {
            query = query.Where(d => d.LanguageTo == languageTo);
        }

        // Подсчёт
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Пагинация
        var items = await query
            .OrderByDescending(d => d.Rating)
            .ThenByDescending(d => d.DownloadCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DictionaryListItemDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description ?? "",
                LanguageFrom = d.LanguageFrom ?? "",
                LanguageTo = d.LanguageTo ?? "",
                WordCount = d.Words.Count,
                AuthorName = d.User != null ? d.User.Login : "Unknown",
                Rating = d.Rating,
                Downloads = d.DownloadCount
            })
            .ToListAsync();

        var result = new PagedResultDto<DictionaryListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            TotalPages = totalPages,
            CurrentPage = page
        };

        var resultJson = JsonSerializer.Serialize(result);
        await _cache.TrySetStringAsync(cacheKey, resultJson, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3)
        });

        return Ok(result);
    }

    /// <summary>
    /// Получить детальную информацию о словаре
    /// </summary>
    [HttpGet("dictionaries/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDictionaryDetails(int id)
    {
        var detailCacheKey = $"marketplace:dict:{id}";
        var cachedDetail = await _cache.TryGetStringAsync(detailCacheKey);
        if (cachedDetail != null)
        {
            return Content(cachedDetail, "application/json");
        }

        var dictionary = await _context.Dictionaries
            .Include(d => d.User)
            .Include(d => d.Words)
            .Where(d => d.Id == id && d.IsPublished)
            .FirstOrDefaultAsync();

        if (dictionary == null)
            return NotFound();

        var authorContentCount = await _context.Dictionaries
            .Where(d => d.UserId == dictionary.UserId && d.IsPublished)
            .CountAsync();

        var previewWords = dictionary.Words
            .Take(10)
            .Select(w => new WordPreviewDto
            {
                Term = w.OriginalWord,
                Translation = w.Translation
            })
            .ToList();

        var detailResult = new DictionaryDetailsDto
        {
            Id = dictionary.Id,
            Name = dictionary.Name,
            Description = dictionary.Description ?? "",
            LanguageFrom = dictionary.LanguageFrom ?? "",
            LanguageTo = dictionary.LanguageTo ?? "",
            WordCount = dictionary.Words.Count,
            AuthorName = dictionary.User?.Login ?? "Unknown",
            Rating = dictionary.Rating,
            RatingCount = dictionary.RatingCount,
            Downloads = dictionary.DownloadCount,
            AuthorContentCount = authorContentCount,
            PreviewWords = previewWords
        };

        var detailJson = JsonSerializer.Serialize(detailResult);
        await _cache.TrySetStringAsync(detailCacheKey, detailJson, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });

        return Ok(detailResult);
    }

    /// <summary>
    /// Скачать словарь
    /// </summary>
    [HttpPost("dictionaries/{id}/download")]
    [Authorize]
    public async Task<IActionResult> DownloadDictionary(int id)
    {
        var userId = GetUserId();
        
        var originalDict = await _context.Dictionaries
            .Include(d => d.Words)
            .FirstOrDefaultAsync(d => d.Id == id && d.IsPublished);

        if (originalDict == null)
            return NotFound();

        // Создаём копию словаря для пользователя
        var newDict = new Dictionary
        {
            UserId = userId,
            Name = originalDict.Name,
            Description = $"[Скачано] {originalDict.Description}",
            LanguageFrom = originalDict.LanguageFrom,
            LanguageTo = originalDict.LanguageTo,
            SourceDictionaryId = originalDict.Id
        };

        _context.Dictionaries.Add(newDict);
        await _context.SaveChangesAsync();

        // Копируем слова
        foreach (var word in originalDict.Words)
        {
            var newWord = new Word
            {
                DictionaryId = newDict.Id,
                UserId = userId,
                OriginalWord = word.OriginalWord,
                Translation = word.Translation,
                Transcription = word.Transcription,
                Example = word.Example
            };
            _context.Words.Add(newWord);
        }

        // Увеличиваем счётчик скачиваний
        originalDict.DownloadCount++;

        // Записываем историю скачивания
        _context.Downloads.Add(new Download
        {
            UserId = userId,
            ContentType = "Dictionary",
            ContentId = id,
            DownloadedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _cache.TryRemoveAsync($"marketplace:dict:{id}");

        return Ok(new { Message = "Словарь успешно скачан", NewDictionaryId = newDict.Id });
    }

    #endregion

    #region Public Rules

    /// <summary>
    /// Получить список публичных правил с пагинацией и фильтрацией
    /// </summary>
    [HttpGet("rules")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicRules(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int difficulty = 0,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 8)
    {
        var cacheKey = $"marketplace:rules:{search}:{category}:{difficulty}:{page}:{pageSize}";
        var cached = await _cache.TryGetStringAsync(cacheKey);
        if (cached != null)
        {
            return Content(cached, "application/json");
        }

        var query = _context.Rules
            .Include(r => r.User)
            .Where(r => r.IsPublished);

        // Фильтрация
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r => 
                r.Title.Contains(search) || 
                r.Description.Contains(search) ||
                r.MarkdownContent.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(r => r.Category == category);
        }

        if (difficulty > 0)
        {
            query = query.Where(r => r.DifficultyLevel == difficulty);
        }

        // Подсчёт
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Пагинация
        var items = await query
            .OrderByDescending(r => r.Rating)
            .ThenByDescending(r => r.DownloadCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RuleListItemDto
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description ?? "",
                Category = r.Category ?? "",
                DifficultyLevel = r.DifficultyLevel,
                AuthorName = r.User != null ? r.User.Login : "Unknown",
                Rating = r.Rating,
                Downloads = r.DownloadCount,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        var rulesResult = new PagedResultDto<RuleListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            TotalPages = totalPages,
            CurrentPage = page
        };

        var rulesJson = JsonSerializer.Serialize(rulesResult);
        await _cache.TrySetStringAsync(cacheKey, rulesJson, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3)
        });

        return Ok(rulesResult);
    }

    /// <summary>
    /// Получить детальную информацию о правиле
    /// </summary>
    [HttpGet("rules/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRuleDetails(int id)
    {
        var ruleCacheKey = $"marketplace:rule:{id}";
        var cachedRule = await _cache.TryGetStringAsync(ruleCacheKey);
        if (cachedRule != null)
        {
            return Content(cachedRule, "application/json");
        }

        var rule = await _context.Rules
            .Include(r => r.User)
            .Where(r => r.Id == id && r.IsPublished)
            .FirstOrDefaultAsync();

        if (rule == null)
            return NotFound();

        var authorContentCount = await _context.Rules
            .Where(r => r.UserId == rule.UserId && r.IsPublished)
            .CountAsync();

        var htmlContent = ConvertMarkdownToHtml(rule.MarkdownContent);

        var ruleDetail = new RuleDetailsDto
        {
            Id = rule.Id,
            Title = rule.Title,
            Description = rule.Description ?? "",
            Category = rule.Category ?? "",
            DifficultyLevel = rule.DifficultyLevel,
            AuthorName = rule.User?.Login ?? "Unknown",
            Rating = rule.Rating,
            RatingCount = rule.RatingCount,
            Downloads = rule.DownloadCount,
            AuthorContentCount = authorContentCount,
            HtmlContent = htmlContent,
            CreatedAt = rule.CreatedAt
        };

        var ruleJson = JsonSerializer.Serialize(ruleDetail);
        await _cache.TrySetStringAsync(ruleCacheKey, ruleJson, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });

        return Ok(ruleDetail);
    }

    /// <summary>
    /// Скачать правило (добавить в свой аккаунт)
    /// </summary>
    [HttpPost("rules/{id}/download")]
    [Authorize]
    public async Task<IActionResult> DownloadRule(int id)
    {
        var userId = GetUserId();
        
        var originalRule = await _context.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.IsPublished);

        if (originalRule == null)
            return NotFound();

        // Создаём копию правила для пользователя
        var newRule = new Rule
        {
            UserId = userId,
            Title = originalRule.Title,
            Description = $"[Скачано] {originalRule.Description}",
            MarkdownContent = originalRule.MarkdownContent,
            Category = originalRule.Category,
            DifficultyLevel = originalRule.DifficultyLevel,
            SourceRuleId = originalRule.Id
        };

        _context.Rules.Add(newRule);

        // Увеличиваем счётчик скачиваний
        originalRule.DownloadCount++;

        // Записываем историю скачивания
        _context.Downloads.Add(new Download
        {
            UserId = userId,
            ContentType = "Rule",
            ContentId = id,
            DownloadedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _cache.TryRemoveAsync($"marketplace:rule:{id}");

        return Ok(new { Message = "Правило успешно скачано", NewRuleId = newRule.Id });
    }

    /// <summary>
    /// Получить похожие правила
    /// </summary>
    [HttpGet("rules/{id}/related")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRelatedRules(int id, [FromQuery] string category)
    {
        var related = await _context.Rules
            .Include(r => r.User)
            .Where(r => r.Id != id && r.Category == category && r.IsPublished)
            .OrderByDescending(r => r.Rating)
            .Take(5)
            .Select(r => new RuleListItemDto
            {
                Id = r.Id,
                Title = r.Title,
                Rating = r.Rating
            })
            .ToListAsync();

        return Ok(related);
    }

    #endregion

    #region Comments

    [HttpGet("dictionaries/{id}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDictionaryComments(int id)
    {
        var comments = await _context.Comments
            .Include(c => c.User)
            .Where(c => c.ContentType == "Dictionary" && c.ContentId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentItemDto
            {
                Id = c.Id,
                AuthorName = c.User != null ? c.User.Login : "Anonymous",
                Rating = c.Rating,
                Text = c.Text,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(comments);
    }

    [HttpPost("dictionaries/{id}/comments")]
    [Authorize]
    public async Task<IActionResult> AddDictionaryComment(int id, [FromBody] AddCommentRequest request)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000)
            return BadRequest(new { Message = "Текст комментария должен быть от 1 до 2000 символов" });

        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest(new { Message = "Оценка должна быть от 1 до 5" });

        // Экранируем текст комментария для защиты от XSS
        request.Text = System.Net.WebUtility.HtmlEncode(request.Text);

        // Check if user already reviewed this dictionary
        var existingReview = await _context.Comments
            .AnyAsync(c => c.UserId == userId && c.ContentType == "Dictionary" && c.ContentId == id);

        if (existingReview)
        {
            return BadRequest(new { Message = "Вы уже оставили отзыв на этот словарь" });
        }

        var comment = new Comment
        {
            UserId = userId,
            ContentType = "Dictionary",
            ContentId = id,
            Rating = request.Rating,
            Text = request.Text,
            CreatedAt = DateTime.UtcNow
        };

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Обновляем средний рейтинг словаря после сохранения комментария
        await UpdateDictionaryRating(id);
        await _context.SaveChangesAsync();

        await _cache.TryRemoveAsync($"marketplace:dict:{id}");

        return Ok(new { Message = "Комментарий добавлен" });
    }

    [HttpGet("dictionaries/{id}/has-reviewed")]
    [Authorize]
    public async Task<IActionResult> HasReviewedDictionary(int id)
    {
        var userId = GetUserId();
        var hasReviewed = await _context.Comments
            .AnyAsync(c => c.UserId == userId && c.ContentType == "Dictionary" && c.ContentId == id);

        return Ok(new { HasReviewed = hasReviewed });
    }

    [HttpGet("rules/{id}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRuleComments(int id)
    {
        var comments = await _context.Comments
            .Include(c => c.User)
            .Where(c => c.ContentType == "Rule" && c.ContentId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentItemDto
            {
                Id = c.Id,
                AuthorName = c.User != null ? c.User.Login : "Anonymous",
                Rating = c.Rating,
                Text = c.Text,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(comments);
    }

    [HttpPost("rules/{id}/comments")]
    [Authorize]
    public async Task<IActionResult> AddRuleComment(int id, [FromBody] AddCommentRequest request)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000)
            return BadRequest(new { Message = "Текст комментария должен быть от 1 до 2000 символов" });

        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest(new { Message = "Оценка должна быть от 1 до 5" });

        // Экранируем текст комментария для защиты от XSS
        request.Text = System.Net.WebUtility.HtmlEncode(request.Text);

        // Check if user already reviewed this rule
        var existingReview = await _context.Comments
            .AnyAsync(c => c.UserId == userId && c.ContentType == "Rule" && c.ContentId == id);

        if (existingReview)
        {
            return BadRequest(new { Message = "Вы уже оставили отзыв на это правило" });
        }

        var comment = new Comment
        {
            UserId = userId,
            ContentType = "Rule",
            ContentId = id,
            Rating = request.Rating,
            Text = request.Text,
            CreatedAt = DateTime.UtcNow
        };

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Обновляем средний рейтинг правила после сохранения комментария
        await UpdateRuleRating(id);
        await _context.SaveChangesAsync();

        await _cache.TryRemoveAsync($"marketplace:rule:{id}");

        return Ok(new { Message = "Комментарий добавлен" });
    }

    [HttpGet("rules/{id}/has-reviewed")]
    [Authorize]
    public async Task<IActionResult> HasReviewedRule(int id)
    {
        var userId = GetUserId();
        var hasReviewed = await _context.Comments
            .AnyAsync(c => c.UserId == userId && c.ContentType == "Rule" && c.ContentId == id);

        return Ok(new { HasReviewed = hasReviewed });
    }

    #endregion

    #region My Content

    [HttpGet("my/dictionaries")]
    [Authorize]
    public async Task<IActionResult> GetMyDictionaries()
    {
        var userId = GetUserId();

        var dictionaries = await _context.Dictionaries
            .Include(d => d.Words)
            .Where(d => d.UserId == userId)
            .Select(d => new MyDictionaryItemDto
            {
                Id = d.Id,
                Name = d.Name,
                WordCount = d.Words.Count,
                IsPublished = d.IsPublished,
                Rating = d.Rating,
                Downloads = d.DownloadCount
            })
            .ToListAsync();

        return Ok(dictionaries);
    }

    [HttpGet("my/rules")]
    [Authorize]
    public async Task<IActionResult> GetMyRules()
    {
        var userId = GetUserId();

        var rules = await _context.Rules
            .Where(r => r.UserId == userId)
            .Select(r => new MyRuleItemDto
            {
                Id = r.Id,
                Title = r.Title,
                Category = r.Category ?? "",
                IsPublished = r.IsPublished,
                Rating = r.Rating,
                Downloads = r.DownloadCount
            })
            .ToListAsync();

        return Ok(rules);
    }

    [HttpGet("my/downloads")]
    [Authorize]
    public async Task<IActionResult> GetMyDownloads()
    {
        var userId = GetUserId();

        var downloads = await _context.Downloads
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.DownloadedAt)
            .Take(50)
            .ToListAsync();

        var result = new List<DownloadedItemDto>();

        foreach (var download in downloads)
        {
            string title = "";
            string authorName = "";

            if (download.ContentType == "Dictionary")
            {
                var dict = await _context.Dictionaries
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.Id == download.ContentId);
                title = dict?.Name ?? "Удалено";
                authorName = dict?.User?.Login ?? "Unknown";
            }
            else if (download.ContentType == "Rule")
            {
                var rule = await _context.Rules
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Id == download.ContentId);
                title = rule?.Title ?? "Удалено";
                authorName = rule?.User?.Login ?? "Unknown";
            }

            result.Add(new DownloadedItemDto
            {
                Id = download.ContentId,
                Type = download.ContentType,
                Title = title,
                AuthorName = authorName,
                DownloadedAt = download.DownloadedAt
            });
        }

        return Ok(result);
    }

    [HttpPost("dictionaries/{id}/publish")]
    [Authorize]
    public async Task<IActionResult> PublishDictionary(int id)
    {
        var userId = GetUserId();
        var dictionary = await _context.Dictionaries
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

        if (dictionary == null)
            return NotFound();

        dictionary.IsPublished = true;
        await _context.SaveChangesAsync();

        await _cache.TryRemoveAsync($"marketplace:dict:{id}");
        await InvalidateDictionaryListCacheAsync();

        return Ok(new { Message = "Словарь опубликован" });
    }

    [HttpPost("dictionaries/{id}/unpublish")]
    [Authorize]
    public async Task<IActionResult> UnpublishDictionary(int id)
    {
        var userId = GetUserId();
        var dictionary = await _context.Dictionaries
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

        if (dictionary == null)
            return NotFound();

        dictionary.IsPublished = false;
        await _context.SaveChangesAsync();

        await _cache.TryRemoveAsync($"marketplace:dict:{id}");
        await InvalidateDictionaryListCacheAsync();

        return Ok(new { Message = "Словарь снят с публикации" });
    }

    [HttpPost("rules/{id}/publish")]
    [Authorize]
    public async Task<IActionResult> PublishRule(int id)
    {
        var userId = GetUserId();
        var rule = await _context.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (rule == null)
            return NotFound();

        rule.IsPublished = true;
        await _context.SaveChangesAsync();

        await _cache.TryRemoveAsync($"marketplace:rule:{id}");
        await InvalidateRuleListCacheAsync();

        return Ok(new { Message = "Правило опубликовано" });
    }

    [HttpPost("rules/{id}/unpublish")]
    [Authorize]
    public async Task<IActionResult> UnpublishRule(int id)
    {
        var userId = GetUserId();
        var rule = await _context.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (rule == null)
            return NotFound();

        rule.IsPublished = false;
        await _context.SaveChangesAsync();

        await _cache.TryRemoveAsync($"marketplace:rule:{id}");
        await InvalidateRuleListCacheAsync();

        return Ok(new { Message = "Правило снято с публикации" });
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Инвалидирует кэш списка словарей (первые несколько страниц без фильтров)
    /// </summary>
    private async Task InvalidateDictionaryListCacheAsync()
    {
        // Инвалидируем первые страницы без фильтров (самые частые запросы)
        var pageSizes = new[] { 9, 12, 20 };
        foreach (var pageSize in pageSizes)
        {
            for (int page = 1; page <= 3; page++)
            {
                await _cache.TryRemoveAsync($"marketplace:dicts::::{page}:{pageSize}");
            }
        }
    }

    /// <summary>
    /// Инвалидирует кэш списка правил (первые несколько страниц без фильтров)
    /// </summary>
    private async Task InvalidateRuleListCacheAsync()
    {
        // Инвалидируем первые страницы без фильтров (самые частые запросы)
        var pageSizes = new[] { 8, 12, 20 };
        foreach (var pageSize in pageSizes)
        {
            for (int page = 1; page <= 3; page++)
            {
                await _cache.TryRemoveAsync($"marketplace:rules:::0:{page}:{pageSize}");
            }
        }
    }

    private async Task UpdateDictionaryRating(int dictionaryId)
    {
        var ratings = await _context.Comments
            .Where(c => c.ContentType == "Dictionary" && c.ContentId == dictionaryId)
            .ToListAsync();

        if (ratings.Any())
        {
            var dict = await _context.Dictionaries.FindAsync(dictionaryId);
            if (dict != null)
            {
                dict.Rating = ratings.Average(c => c.Rating);
                dict.RatingCount = ratings.Count;
            }
        }
    }

    private async Task UpdateRuleRating(int ruleId)
    {
        var ratings = await _context.Comments
            .Where(c => c.ContentType == "Rule" && c.ContentId == ruleId)
            .ToListAsync();

        if (ratings.Any())
        {
            var rule = await _context.Rules.FindAsync(ruleId);
            if (rule != null)
            {
                rule.Rating = ratings.Average(c => c.Rating);
                rule.RatingCount = ratings.Count;
            }
        }
    }

    private static string ConvertMarkdownToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        // Санитизация: экранируем HTML-теги в пользовательском вводе для защиты от XSS
        var html = System.Net.WebUtility.HtmlEncode(markdown)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        // Заголовки
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^### (.+)$", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^## (.+)$", "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"^# (.+)$", "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Жирный и курсив
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\*(.+?)\*", "<em>$1</em>");

        // Код
        html = System.Text.RegularExpressions.Regex.Replace(html, @"`(.+?)`", "<code>$1</code>");

        // Параграфы
        var paragraphs = html.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        html = string.Join("", paragraphs.Select(p => 
            p.StartsWith("<h") ? p : $"<p>{p.Replace("\n", "<br/>")}</p>"));

        // Финальная санитизация: оставляем только безопасные теги (allowlist)
        return SanitizeHtmlAllowlist(html);
    }

    /// <summary>
    /// Allowlist-санитизатор: пропускает только безопасные теги без атрибутов.
    /// Корректно обрабатывает кавычки внутри атрибутов (не ломается на > в onerror и т.д.).
    /// </summary>
    private static string SanitizeHtmlAllowlist(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        var allowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "h1", "h2", "h3", "h4", "h5", "h6",
            "p", "br", "hr",
            "strong", "b", "em", "i", "u",
            "code", "pre",
            "ul", "ol", "li",
            "blockquote",
            "table", "thead", "tbody", "tr", "th", "td"
        };

        var result = new System.Text.StringBuilder(html.Length);
        int i = 0;

        while (i < html.Length)
        {
            if (html[i] == '<')
            {
                int tagEnd = FindTagEnd(html, i);
                if (tagEnd == -1)
                {
                    result.Append("&lt;");
                    i++;
                    continue;
                }

                var inner = html.AsSpan(i + 1, tagEnd - i - 1).Trim();
                bool isClosing = inner.Length > 0 && inner[0] == '/';
                if (isClosing) inner = inner.Slice(1).TrimStart();

                int nameEnd = 0;
                while (nameEnd < inner.Length &&
                       !char.IsWhiteSpace(inner[nameEnd]) &&
                       inner[nameEnd] != '/' && inner[nameEnd] != '>')
                    nameEnd++;

                var tagName = inner.Slice(0, nameEnd).ToString();

                if (allowedTags.Contains(tagName))
                {
                    bool selfClose = tagName is "br" or "hr";
                    result.Append(isClosing ? $"</{tagName}>" : selfClose ? $"<{tagName}/>" : $"<{tagName}>");
                }

                i = tagEnd + 1;
            }
            else
            {
                result.Append(html[i]);
                i++;
            }
        }

        return result.ToString();
    }

    private static int FindTagEnd(string html, int openBracket)
    {
        bool inDouble = false, inSingle = false;
        for (int i = openBracket + 1; i < html.Length; i++)
        {
            char c = html[i];
            if (inDouble) { if (c == '"') inDouble = false; }
            else if (inSingle) { if (c == '\'') inSingle = false; }
            else if (c == '"') inDouble = true;
            else if (c == '\'') inSingle = true;
            else if (c == '>') return i;
        }
        return -1;
    }

    #endregion
}

#region DTOs

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
}

public class DictionaryListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LanguageFrom { get; set; } = "";
    public string LanguageTo { get; set; } = "";
    public int WordCount { get; set; }
    public string AuthorName { get; set; } = "";
    public double Rating { get; set; }
    public int Downloads { get; set; }
}

public class DictionaryDetailsDto : DictionaryListItemDto
{
    public int RatingCount { get; set; }
    public int AuthorContentCount { get; set; }
    public List<WordPreviewDto> PreviewWords { get; set; } = new();
}

public class WordPreviewDto
{
    public string Term { get; set; } = "";
    public string Translation { get; set; } = "";
}

public class RuleListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public int DifficultyLevel { get; set; }
    public string AuthorName { get; set; } = "";
    public double Rating { get; set; }
    public int Downloads { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RuleDetailsDto : RuleListItemDto
{
    public string HtmlContent { get; set; } = "";
    public int RatingCount { get; set; }
    public int AuthorContentCount { get; set; }
}

public class CommentItemDto
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = "";
    public int Rating { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class AddCommentRequest
{
    public int Rating { get; set; }
    public string Text { get; set; } = "";
}

public class MyDictionaryItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int WordCount { get; set; }
    public bool IsPublished { get; set; }
    public double Rating { get; set; }
    public int Downloads { get; set; }
}

public class MyRuleItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsPublished { get; set; }
    public double Rating { get; set; }
    public int Downloads { get; set; }
}

public class DownloadedItemDto
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public DateTime DownloadedAt { get; set; }
}

#endregion
