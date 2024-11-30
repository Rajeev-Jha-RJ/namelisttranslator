using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text.Json;

namespace CsvTranslator
{
    public enum TranslationType
    {
        Japanese,
        Hiragana,
        Katakana,
        BrazilianPortuguese,
        ColombianSpanish
    }

    public class TranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiUrl;
        private readonly JapaneseTextProcessor _japaneseProcessor;

        private readonly Dictionary<TranslationType, string> _targetLanguages = new()
        {
            { TranslationType.Japanese, "JA" },
            { TranslationType.Hiragana, "JA" },
            { TranslationType.Katakana, "JA" },
            { TranslationType.BrazilianPortuguese, "PT-BR" },
            { TranslationType.ColombianSpanish, "ES" }
        };

        private readonly Dictionary<TranslationType, string> _columnSuffixes = new()
        {
            { TranslationType.Japanese, "_Japanese" },
            { TranslationType.Hiragana, "_Hiragana" },
            { TranslationType.Katakana, "_Katakana" },
            { TranslationType.BrazilianPortuguese, "_Portuguese_BR" },
            { TranslationType.ColombianSpanish, "_Spanish_CO" }
        };

        public TranslationService(string apiKey)
        {
            _apiKey = apiKey;
            bool isFreeKey = apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase);
            _apiUrl = isFreeKey 
                ? "https://api-free.deepl.com/v2/translate"
                : "https://api.deepl.com/v2/translate";

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {apiKey}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MultiLanguageCsvTranslator/1.0.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            
            _japaneseProcessor = new JapaneseTextProcessor();
        }

        public async Task ValidateApiKeyAsync()
        {
            if (!_apiKey.Contains("-"))
            {
                throw new ArgumentException("Invalid API key format. Key should contain hyphens (-)");
            }

            if (_apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase))
            {
                var keyParts = _apiKey.Split(':')[0].Split('-');
                if (keyParts.Length != 5 || keyParts.Any(part => part.Length != 4))
                {
                    throw new ArgumentException("Invalid Free API key format. Expected format: xxxx-xxxx-xxxx-xxxx-xxxx:fx");
                }
            }

            try
            {
                await CheckQuotaAsync();
                await TranslateTextAsync("test", TranslationType.Japanese);
                Console.WriteLine("API key validation successful!");
            }
            catch (Exception ex)
            {
                throw new Exception($"API key validation failed: {ex.Message}");
            }
        }

        public async Task CheckQuotaAsync()
        {
            var usageUrl = _apiUrl.Replace("/translate", "/usage");
            try
            {
                var response = await _httpClient.GetAsync(usageUrl);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                
                var characterCount = doc.RootElement.GetProperty("character_count").GetInt64();
                var characterLimit = doc.RootElement.GetProperty("character_limit").GetInt64();
                
                Console.WriteLine($"\nAPI Usage: {characterCount:N0} / {characterLimit:N0} characters");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not check quota: {ex.Message}");
            }
        }

        public async Task TranslateCsvColumnAsync(
            string inputFile,
            string outputFile,
            string columnName,
            List<TranslationType> translationTypes,
            bool includeJapaneseExtras = false,
            int batchSize = 50,
            int delayBetweenBatchesMs = 1000)
        {
            var records = new List<dynamic>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            };

            // Read CSV and validate column
            using (var reader = new StreamReader(inputFile))
            using (var csv = new CsvReader(reader, config))
            {
                ValidateColumn(csv, columnName);
                records = csv.GetRecords<dynamic>().ToList();
            }

            Console.WriteLine($"Found {records.Count} records to process...");
            Console.WriteLine("Progress: [" + new string('-', 50) + "]");
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.Write("Progress: [");

            // Process records in batches
            var processedCount = 0;
            var progressBarPosition = Console.CursorTop;
            var progressBarLeft = Console.CursorLeft;

            for (int i = 0; i < records.Count; i += batchSize)
            {
                var batch = records.Skip(i).Take(batchSize);
                foreach (var record in batch)
                {
                    var originalText = ((IDictionary<string, object>)record)[columnName]?.ToString();
                    if (!string.IsNullOrEmpty(originalText))
                    {
                        foreach (var translationType in translationTypes)
                        {
                            var translatedText = await TranslateTextAsync(originalText, translationType);
                            ((IDictionary<string, object>)record)[$"{columnName}{_columnSuffixes[translationType]}"] = translatedText;

                            // Add extra Japanese processing for Japanese translations
                            if (includeJapaneseExtras && translationType == TranslationType.Japanese)
                            {
                                var processedText = _japaneseProcessor.ProcessText(translatedText);
                                ((IDictionary<string, object>)record)[$"{columnName}_Romaji"] = processedText.Romaji;
                                ((IDictionary<string, object>)record)[$"{columnName}_ReadingGuide"] = processedText.ReadingGuide;
                                ((IDictionary<string, object>)record)[$"{columnName}_Segments"] = string.Join(" | ", processedText.Segments);
                            }
                        }
                    }

                    processedCount++;

                    // Update progress bar every 10 records
                    if (processedCount % 10 == 0 || processedCount == records.Count)
                    {
                        var percentComplete = (int)((double)processedCount / records.Count * 50);
                        Console.SetCursorPosition(progressBarLeft, progressBarPosition);
                        Console.Write(new string('█', percentComplete));
                        Console.SetCursorPosition(progressBarLeft + 50, progressBarPosition);
                        Console.Write("] ");
                        Console.Write($"{(double)processedCount / records.Count:P0}");
                        
                        // Show records processed
                        Console.SetCursorPosition(0, progressBarPosition + 1);
                        Console.Write($"Records processed: {processedCount}/{records.Count}   ");
                    }
                }

                if (processedCount % 500 == 0)
                {
                    Console.SetCursorPosition(0, progressBarPosition + 2);
                    await CheckQuotaAsync();
                }

                if (i + batchSize < records.Count)
                {
                    await Task.Delay(delayBetweenBatchesMs);
                }
            }

            Console.WriteLine("\n");

            // Write to output CSV
            using (var writer = new StreamWriter(outputFile))
            using (var csv = new CsvWriter(writer, config))
            {
                csv.WriteRecords(records);
            }
        }

        private async Task<string> TranslateTextAsync(string text, TranslationType translationType)
        {
            var targetLang = _targetLanguages[translationType];
            
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("target_lang", targetLang),
                new KeyValuePair<string, string>("source_lang", "EN"),
                new KeyValuePair<string, string>("preserve_formatting", "1")
            });

            try
            {
                var response = await _httpClient.PostAsync(_apiUrl, content);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Authentication failed: {errorContent}");
                }
                
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var translatedText = doc.RootElement
                    .GetProperty("translations")[0]
                    .GetProperty("text")
                    .GetString() ?? string.Empty;

                // Process Japanese text based on translation type
                if (translationType == TranslationType.Hiragana)
                {
                    return _japaneseProcessor.ToHiragana(translatedText);
                }
                else if (translationType == TranslationType.Katakana)
                {
                    return _japaneseProcessor.ToKatakana(translatedText);
                }

                return translatedText;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"API Request Error: {ex.Message}");
                throw;
            }
        }

        private void ValidateColumn(CsvReader csv, string columnName)
        {
            csv.Read();
            csv.ReadHeader();
            var hasColumn = csv.HeaderRecord!.Any(h => h.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            
            if (!hasColumn)
            {
                throw new ArgumentException($"Column '{columnName}' not found in CSV file.");
            }
        }
    }
}