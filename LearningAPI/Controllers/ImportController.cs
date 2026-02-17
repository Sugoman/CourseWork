using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningAPI.Services;
using System.Text.Json;
using CsvHelper;
using System.Globalization;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/dictionaries/import")]
    [Authorize]
    public class ImportController : BaseApiController
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<ImportController> _logger;
        private readonly TranscriptionChannel _transcriptionChannel;

        private static readonly HashSet<string> OriginalAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            "original", "word", "originalword", "original_word", "term", "source", "english", "front", "question"
        };

        private static readonly HashSet<string> TranslationAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            "translation", "translate", "meaning", "definition", "target", "russian", "back", "answer", "перевод"
        };

        private static readonly HashSet<string> TranscriptionAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            "transcription", "pronunciation", "phonetic", "ipa", "partofspeech", "part_of_speech", "pos"
        };

        private static readonly HashSet<string> ExampleAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            "example", "sentence", "usage", "context", "sample", "example_sentence", "пример"
        };

        public ImportController(ApiDbContext context, ILogger<ImportController> logger, TranscriptionChannel transcriptionChannel)
        {
            _context = context;
            _logger = logger;
            _transcriptionChannel = transcriptionChannel;
        }

        /// <summary>
        /// Импортирует словарь из JSON файла
        /// </summary>
        [HttpPost("json")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportFromJson(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "No file provided" });
                }

                if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "File must be JSON format" });
                }

                var userId = GetUserId();

                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    var content = await reader.ReadToEndAsync();
                    var importData = JsonSerializer.Deserialize<ImportDictionaryData>(content);

                    if (importData == null)
                    {
                        return BadRequest(new { message = "Invalid JSON format" });
                    }

                    // Создаём новый словарь
                    var dictionary = new Dictionary
                    {
                        Name = importData.Name ?? "Imported Dictionary",
                        Description = importData.Description ?? "",
                        LanguageFrom = importData.LanguageFrom ?? "English",
                        LanguageTo = importData.LanguageTo ?? "Russian",
                        UserId = userId,
                        Words = importData.Words?.Select(w => new Word
                        {
                            OriginalWord = w.Original,
                            Translation = w.Translation,
                            Transcription = w.PartOfSpeech,
                            Example = w.Example ?? "",
                            UserId = userId
                        }).ToList() ?? new List<Word>()
                    };

                    _context.Dictionaries.Add(dictionary);
                    await _context.SaveChangesAsync();

                    // Запрашиваем транскрипцию только для слов, у которых она не указана
                    foreach (var word in dictionary.Words.Where(w => string.IsNullOrWhiteSpace(w.Transcription)))
                    {
                        await _transcriptionChannel.Writer.WriteAsync(
                            new TranscriptionRequest(word.Id, word.OriginalWord));
                    }

                    _logger.LogInformation("Dictionary imported from JSON by user {UserId}, DictionaryId={DictionaryId}", userId, dictionary.Id);

                    return Ok(new
                    {
                        message = "Dictionary imported successfully",
                        dictionaryId = dictionary.Id,
                        name = dictionary.Name,
                        wordCount = dictionary.Words.Count
                    });
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON format during import");
                return BadRequest(new { message = "Invalid JSON format", error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing dictionary from JSON");
                return StatusCode(500, new { message = "Error importing dictionary" });
            }
        }

        /// <summary>
        /// Импортирует словарь из JSON файла произвольной структуры.
        /// Поддерживает плоский массив объектов и объект с вложенным массивом слов.
        /// Автоматически определяет маппинг полей.
        /// </summary>
        [HttpPost("json/auto")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportFromJsonAuto(
            IFormFile file,
            [FromForm] string? dictionaryName = null,
            [FromForm] string? languageFrom = null,
            [FromForm] string? languageTo = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "No file provided" });
                }

                if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "File must be JSON format" });
                }

                var userId = GetUserId();

                using var reader = new StreamReader(file.OpenReadStream());
                var content = await reader.ReadToEndAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var resolvedName = dictionaryName;
                var resolvedLangFrom = languageFrom;
                var resolvedLangTo = languageTo;

                JsonElement itemsElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    itemsElement = root;
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (resolvedName == null && TryGetStringProperty(root, "name", out var n))
                        resolvedName = n;
                    if (resolvedLangFrom == null && TryGetStringProperty(root, "languagefrom", out var lf))
                        resolvedLangFrom = lf;
                    if (resolvedLangTo == null && TryGetStringProperty(root, "languageto", out var lt))
                        resolvedLangTo = lt;

                    // Ищем массив слов внутри объекта
                    itemsElement = FindWordsArray(root);
                }
                else
                {
                    return BadRequest(new { message = "JSON must be an array or object" });
                }

                if (itemsElement.ValueKind != JsonValueKind.Array)
                {
                    return BadRequest(new { message = "Could not find an array of words in the JSON" });
                }

                // Определяем маппинг полей по первому элементу
                var mapping = DetectFieldMapping(itemsElement);

                if (mapping.OriginalKey == null || mapping.TranslationKey == null)
                {
                    return BadRequest(new
                    {
                        message = "Could not auto-detect required fields (original word and translation). " +
                                  "Make sure the JSON contains properties with recognizable names."
                    });
                }

                var words = new List<Word>();
                foreach (var item in itemsElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var original = GetStringValue(item, mapping.OriginalKey);
                    var translation = GetStringValue(item, mapping.TranslationKey);

                    if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(translation))
                        continue;

                    words.Add(new Word
                    {
                        OriginalWord = original,
                        Translation = translation,
                        Transcription = mapping.TranscriptionKey != null ? GetStringValue(item, mapping.TranscriptionKey) : null,
                        Example = mapping.ExampleKey != null ? GetStringValue(item, mapping.ExampleKey) ?? "" : "",
                        UserId = userId
                    });
                }

                if (!words.Any())
                {
                    return BadRequest(new { message = "No valid words found in the file" });
                }

                var fallbackName = Path.GetFileNameWithoutExtension(file.FileName);

                var dictionary = new Dictionary
                {
                    Name = resolvedName ?? fallbackName ?? "Imported Dictionary",
                    Description = "",
                    LanguageFrom = resolvedLangFrom ?? "English",
                    LanguageTo = resolvedLangTo ?? "Russian",
                    UserId = userId,
                    Words = words
                };

                _context.Dictionaries.Add(dictionary);
                await _context.SaveChangesAsync();

                // Запрашиваем транскрипцию только для слов, у которых она не указана
                foreach (var word in dictionary.Words.Where(w => string.IsNullOrWhiteSpace(w.Transcription)))
                {
                    await _transcriptionChannel.Writer.WriteAsync(
                        new TranscriptionRequest(word.Id, word.OriginalWord));
                }

                _logger.LogInformation(
                    "Dictionary auto-imported from JSON by user {UserId}, DictionaryId={DictionaryId}, WordCount={WordCount}, Mapping=[{Original}->{Translation}]",
                    userId, dictionary.Id, words.Count, mapping.OriginalKey, mapping.TranslationKey);

                return Ok(new
                {
                    message = "Dictionary imported successfully",
                    dictionaryId = dictionary.Id,
                    name = dictionary.Name,
                    wordCount = dictionary.Words.Count,
                    detectedMapping = new
                    {
                        original = mapping.OriginalKey,
                        translation = mapping.TranslationKey,
                        transcription = mapping.TranscriptionKey,
                        example = mapping.ExampleKey
                    }
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON format during auto-import");
                return BadRequest(new { message = "Invalid JSON format", error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-importing dictionary from JSON");
                return StatusCode(500, new { message = "Error importing dictionary" });
            }
        }

        private static FieldMapping DetectFieldMapping(JsonElement arrayElement)
        {
            var mapping = new FieldMapping();

            // Собираем все ключи из первых нескольких элементов
            var keys = new HashSet<string>();
            var count = 0;
            foreach (var item in arrayElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                foreach (var prop in item.EnumerateObject())
                    keys.Add(prop.Name);
                if (++count >= 3) break;
            }

            foreach (var key in keys)
            {
                var normalized = key.Replace(" ", "").Replace("_", "").Replace("-", "");

                if (mapping.OriginalKey == null && OriginalAliases.Contains(normalized))
                    mapping.OriginalKey = key;
                else if (mapping.TranslationKey == null && TranslationAliases.Contains(normalized))
                    mapping.TranslationKey = key;
                else if (mapping.TranscriptionKey == null && TranscriptionAliases.Contains(normalized))
                    mapping.TranscriptionKey = key;
                else if (mapping.ExampleKey == null && ExampleAliases.Contains(normalized))
                    mapping.ExampleKey = key;
            }

            // Если не нашли по алиасам — пытаемся угадать по позиции (первые два строковых поля)
            if (mapping.OriginalKey == null || mapping.TranslationKey == null)
            {
                var stringKeys = new List<string>();
                foreach (var item in arrayElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    foreach (var prop in item.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String && !stringKeys.Contains(prop.Name))
                            stringKeys.Add(prop.Name);
                    }
                    break;
                }

                if (mapping.OriginalKey == null && stringKeys.Count >= 1)
                    mapping.OriginalKey = stringKeys[0];
                if (mapping.TranslationKey == null && stringKeys.Count >= 2)
                    mapping.TranslationKey = stringKeys[1];
            }

            return mapping;
        }

        private static JsonElement FindWordsArray(JsonElement obj)
        {
            // Ищем свойство-массив с подходящим именем
            var arrayAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "words", "items", "data", "entries", "vocabulary", "list", "cards"
            };

            // Сначала ищем по известным именам
            foreach (var prop in obj.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array && arrayAliases.Contains(prop.Name))
                    return prop.Value;
            }

            // Если не нашли — берём первое свойство-массив
            foreach (var prop in obj.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    return prop.Value;
            }

            return default;
        }

        private static bool TryGetStringProperty(JsonElement obj, string nameNormalized, out string value)
        {
            value = null!;
            foreach (var prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name.Replace(" ", "").Replace("_", ""),
                    nameNormalized, StringComparison.OrdinalIgnoreCase)
                    && prop.Value.ValueKind == JsonValueKind.String)
                {
                    value = prop.Value.GetString()!;
                    return true;
                }
            }
            return false;
        }

        private static string? GetStringValue(JsonElement obj, string key)
        {
            if (obj.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
            return null;
        }

        private class FieldMapping
        {
            public string? OriginalKey { get; set; }
            public string? TranslationKey { get; set; }
            public string? TranscriptionKey { get; set; }
            public string? ExampleKey { get; set; }
        }

        /// <summary>
        /// ������������� ������� �� CSV �����
        /// </summary>
        [HttpPost("csv")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportFromCsv(IFormFile file, [FromForm] string dictionaryName, [FromForm] string languageFrom, [FromForm] string languageTo)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "No file provided" });
                }

                if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "File must be CSV format" });
                }

                var userId = GetUserId();
                var words = new List<Word>();

                using (var reader = new StreamReader(file.OpenReadStream()))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Read();
                    csv.ReadHeader();

                    while (csv.Read())
                    {
                        string? transcription = null;
                        string? example = null;
                        
                        try { transcription = csv.GetField("Part of Speech"); } catch { }
                        try { example = csv.GetField("Example"); } catch { }
                        
                        var word = new Word
                        {
                            OriginalWord = csv.GetField("Original") ?? "",
                            Translation = csv.GetField("Translation") ?? "",
                            Transcription = transcription,
                            Example = example ?? "",
                            UserId = userId
                        };

                        if (!string.IsNullOrEmpty(word.OriginalWord) && !string.IsNullOrEmpty(word.Translation))
                        {
                            words.Add(word);
                        }
                    }
                }

                if (!words.Any())
                {
                    return BadRequest(new { message = "No valid words found in CSV" });
                }

                // ������� ����� �������
                var dictionary = new Dictionary
                {
                    Name = dictionaryName ?? "Imported Dictionary",
                    Description = "",
                    LanguageFrom = languageFrom ?? "English",
                    LanguageTo = languageTo ?? "Russian",
                    UserId = userId,
                    Words = words
                };

                _context.Dictionaries.Add(dictionary);
                await _context.SaveChangesAsync();

                // Запрашиваем транскрипцию только для слов, у которых она не указана
                foreach (var word in dictionary.Words.Where(w => string.IsNullOrWhiteSpace(w.Transcription)))
                {
                    await _transcriptionChannel.Writer.WriteAsync(
                        new TranscriptionRequest(word.Id, word.OriginalWord));
                }

                _logger.LogInformation("Dictionary imported from CSV by user {UserId}, DictionaryId={DictionaryId}, WordCount={WordCount}", userId, dictionary.Id, words.Count);

                return Ok(new
                {
                    message = "Dictionary imported successfully",
                    dictionaryId = dictionary.Id,
                    name = dictionary.Name,
                    wordCount = dictionary.Words.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing dictionary from CSV");
                return StatusCode(500, new { message = "Error importing dictionary", error = ex.Message });
            }
        }

        public class ImportDictionaryData
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string LanguageFrom { get; set; }
            public string LanguageTo { get; set; }
            public List<ImportWordData> Words { get; set; }
        }

        public class ImportWordData
        {
            public string Original { get; set; }
            public string Translation { get; set; }
            public string PartOfSpeech { get; set; }
            public string Example { get; set; }
        }
    }
}
