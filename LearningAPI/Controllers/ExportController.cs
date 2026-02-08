using CsvHelper;
using LearningTrainer.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/dictionaries/export")]
    [Authorize]
    public class ExportController : BaseApiController
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<ExportController> _logger;

        public ExportController(ApiDbContext context, ILogger<ExportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// �������������� ������� � JSON
        /// </summary>
        [HttpGet("{dictionaryId}/json")]
        public async Task<IActionResult> ExportAsJson(int dictionaryId)
        {
            try
            {
                var userId = GetUserId();

                var dictionary = await _context.Dictionaries
                    .Include(d => d.Words)
                    .FirstOrDefaultAsync(d => d.Id == dictionaryId && d.UserId == userId);

                if (dictionary == null)
                {
                    return NotFound();
                }

                var exportData = new DictionaryExportData
                {
                    Name = dictionary.Name,
                    Description = dictionary.Description,
                    LanguageFrom = dictionary.LanguageFrom,
                    LanguageTo = dictionary.LanguageTo,
                    ExportDate = DateTime.UtcNow,
                    Words = dictionary.Words.Select(w => new WordExportData
                    {
                        Original = w.OriginalWord,
                        Translation = w.Translation,
                        PartOfSpeech = w.Transcription,
                        Example = w.Example
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                _logger.LogInformation("Dictionary {DictionaryId} exported as JSON by user {UserId}", dictionaryId, userId);

                return File(
                    Encoding.UTF8.GetBytes(json),
                    "application/json",
                    $"{dictionary.Name}_export_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting dictionary {DictionaryId} as JSON", dictionaryId);
                return StatusCode(500, new { message = "Error exporting dictionary" });
            }
        }

        /// <summary>
        /// �������������� ������� � CSV
        /// </summary>
        [HttpGet("{dictionaryId}/csv")]
        public async Task<IActionResult> ExportAsCsv(int dictionaryId)
        {
            try
            {
                var userId = GetUserId();

                var dictionary = await _context.Dictionaries
                    .Include(d => d.Words)
                    .FirstOrDefaultAsync(d => d.Id == dictionaryId && d.UserId == userId);

                if (dictionary == null)
                {
                    return NotFound();
                }

                using (var writer = new StringWriter())
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    // �������� ���������
                    csv.WriteField("Original");
                    csv.WriteField("Translation");
                    csv.WriteField("Part of Speech");
                    csv.WriteField("Example");
                    csv.NextRecord();

                    // �������� �����
                    foreach (var word in dictionary.Words)
                    {
                        csv.WriteField(word.OriginalWord);
                        csv.WriteField(word.Translation);
                        csv.WriteField(word.Transcription ?? "");
                        csv.WriteField(word.Example ?? "");
                        csv.NextRecord();
                    }

                    _logger.LogInformation("Dictionary {DictionaryId} exported as CSV by user {UserId}", dictionaryId, userId);

                    var csvContent = writer.ToString();
                    var preamble = Encoding.UTF8.GetPreamble();
                    var csvBytes = Encoding.UTF8.GetBytes(csvContent);
                    var result = new byte[preamble.Length + csvBytes.Length];
                    preamble.CopyTo(result, 0);
                    csvBytes.CopyTo(result, preamble.Length);

                    return File(
                        result,
                        "text/csv",
                        $"{dictionary.Name}_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting dictionary {DictionaryId} as CSV", dictionaryId);
                return StatusCode(500, new { message = "Error exporting dictionary" });
            }
        }

        /// <summary>
        /// �������������� ��� ������� � ����� ZIP ������
        /// </summary>
        [HttpGet("all/zip")]
        public async Task<IActionResult> ExportAllAsZip()
        {
            try
            {
                var userId = GetUserId();

                var dictionaries = await _context.Dictionaries
                    .Include(d => d.Words)
                    .Where(d => d.UserId == userId)
                    .ToListAsync();

                if (!dictionaries.Any())
                {
                    return NotFound(new { message = "No dictionaries to export" });
                }

                using var memoryStream = new MemoryStream();

                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    foreach (var dictionary in dictionaries)
                    {
                        var exportData = new DictionaryExportData
                        {
                            Name = dictionary.Name,
                            Description = dictionary.Description,
                            LanguageFrom = dictionary.LanguageFrom,
                            LanguageTo = dictionary.LanguageTo,
                            ExportDate = DateTime.UtcNow,
                            Words = dictionary.Words.Select(w => new WordExportData
                            {
                                Original = w.OriginalWord,
                                Translation = w.Translation,
                                PartOfSpeech = w.Transcription,
                                Example = w.Example
                            }).ToList()
                        };

                        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });

                        var entry = archive.CreateEntry($"{dictionary.Name}.json");
                        using (var entryStream = entry.Open())
                        {
                            await entryStream.WriteAsync(Encoding.UTF8.GetBytes(json));
                        }
                    }
                } // archive.Dispose() writes the ZIP central directory here

                _logger.LogInformation("All dictionaries exported as ZIP by user {UserId}", userId);

                return File(
                    memoryStream.ToArray(),
                    "application/zip",
                    $"dictionaries_export_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting all dictionaries as ZIP");
                return StatusCode(500, new { message = "Error exporting dictionaries" });
            }
        }

        public class DictionaryExportData
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string LanguageFrom { get; set; }
            public string LanguageTo { get; set; }
            public DateTime ExportDate { get; set; }
            public List<WordExportData> Words { get; set; } = new();
        }

        public class WordExportData
        {
            public string Original { get; set; }
            public string Translation { get; set; }
            public string PartOfSpeech { get; set; }
            public string Example { get; set; }
        }
    }
}
