using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
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

        public ImportController(ApiDbContext context, ILogger<ImportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Импортировать словарь из JSON файла
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

                    // Создать новый словарь
                    var dictionary = new Dictionary
                    {
                        Name = importData.Name ?? "Imported Dictionary",
                        Description = importData.Description,
                        LanguageFrom = importData.LanguageFrom ?? "English",
                        LanguageTo = importData.LanguageTo ?? "Russian",
                        UserId = userId,
                        Words = importData.Words?.Select(w => new Word
                        {
                            OriginalWord = w.Original,
                            Translation = w.Translation,
                            Transcription = w.PartOfSpeech,
                            Example = w.Example,
                            UserId = userId
                        }).ToList() ?? new List<Word>()
                    };

                    _context.Dictionaries.Add(dictionary);
                    await _context.SaveChangesAsync();

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
        /// Импортировать словарь из CSV файла
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
                        var word = new Word
                        {
                            OriginalWord = csv.GetField("Original") ?? "",
                            Translation = csv.GetField("Translation") ?? "",
                            Transcription = csv.GetField("Part of Speech"),
                            Example = csv.GetField("Example"),
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

                // Создать новый словарь
                var dictionary = new Dictionary
                {
                    Name = dictionaryName ?? "Imported Dictionary",
                    LanguageFrom = languageFrom ?? "English",
                    LanguageTo = languageTo ?? "Russian",
                    UserId = userId,
                    Words = words
                };

                _context.Dictionaries.Add(dictionary);
                await _context.SaveChangesAsync();

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
