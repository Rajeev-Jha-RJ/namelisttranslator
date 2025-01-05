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
                await TranslateTextAsync("test", TranslationType.Japanese,true);
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
            bool isProperNoun = false,  // New paramete
            int skipRows = 0,
            int outputBatchSize = 1000,
            int processingBatchSize = 50,
            bool includeJapaneseExtras = false,
            int delayBetweenBatchesMs = 1000)
        {
            var records = new List<dynamic>();
            var errorRows = new List<(int RowNumber, string Error)>();
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

            // Skip the specified number of rows
            if (skipRows > 0)
            {
                if (skipRows >= records.Count)
                {
                    throw new ArgumentException($"Skip rows value ({skipRows}) is greater than or equal to the total number of records ({records.Count})");
                }
                records = records.Skip(skipRows).ToList();
                Console.WriteLine($"Skipped {skipRows} rows. Processing remaining {records.Count} records...");
            }

            Console.WriteLine($"Found {records.Count} records to process...");
            Console.WriteLine($"Will create new output file every {outputBatchSize} records");
            Console.WriteLine("Progress: [" + new string('-', 50) + "]");
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.Write("Progress: [");

            var processedCount = 0;
            var errorCount = 0;
            var progressBarPosition = Console.CursorTop;
            var progressBarLeft = Console.CursorLeft;
            var currentBatchRecords = new List<dynamic>();
            var fileCounter = 1;

            for (int i = 0; i < records.Count; i += processingBatchSize)
            {
                var batch = records.Skip(i).Take(processingBatchSize);
                foreach (var record in batch)
                {
                    try
                    {
                        var originalText = ((IDictionary<string, object>)record)[columnName]?.ToString();
                        if (!string.IsNullOrEmpty(originalText))
                        {
                            foreach (var translationType in translationTypes)
                            {
                                try
                                {
                                    var translatedText = await TranslateTextAsync(originalText, translationType, isProperNoun);
                                    ((IDictionary<string, object>)record)[$"{columnName}{_columnSuffixes[translationType]}"] = translatedText;

                                    // Add proper noun indicator in the output
                                    if (isProperNoun)
                                    {
                                        ((IDictionary<string, object>)record)[$"{columnName}_IsProperNoun"] = "true";
                                    }

                                    if (includeJapaneseExtras && translationType == TranslationType.Japanese)
                                    {
                                        var processedText = _japaneseProcessor.ProcessText(translatedText);
                                        ((IDictionary<string, object>)record)[$"{columnName}_Romaji"] = processedText.Romaji;
                                        ((IDictionary<string, object>)record)[$"{columnName}_ReadingGuide"] = processedText.ReadingGuide;
                                        ((IDictionary<string, object>)record)[$"{columnName}_Segments"] = string.Join(" | ", processedText.Segments);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Error handling remains the same
                                    ((IDictionary<string, object>)record)[$"{columnName}{_columnSuffixes[translationType]}"] = $"ERROR: {ex.Message}";
                                    errorRows.Add((skipRows + processedCount + 1, $"Translation error for {translationType}: {ex.Message}"));
                                    errorCount++;
                                }
                            }
                            /* old code
                            foreach (var translationType in translationTypes)
                            {
                                try
                                {
                                    var translatedText = await TranslateTextAsync(originalText, translationType);
                                    ((IDictionary<string, object>)record)[$"{columnName}{_columnSuffixes[translationType]}"] = translatedText;

                                    if (includeJapaneseExtras && translationType == TranslationType.Japanese)
                                    {
                                        var processedText = _japaneseProcessor.ProcessText(translatedText);
                                        ((IDictionary<string, object>)record)[$"{columnName}_Romaji"] = processedText.Romaji;
                                        ((IDictionary<string, object>)record)[$"{columnName}_ReadingGuide"] = processedText.ReadingGuide;
                                        ((IDictionary<string, object>)record)[$"{columnName}_Segments"] = string.Join(" | ", processedText.Segments);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Add error marker for this translation type
                                    ((IDictionary<string, object>)record)[$"{columnName}{_columnSuffixes[translationType]}"] = $"ERROR: {ex.Message}";
                                    errorRows.Add((skipRows + processedCount + 1, $"Translation error for {translationType}: {ex.Message}"));
                                    errorCount++;
                                }
                            }
                            */
                        }

                        currentBatchRecords.Add(record);
                    }
                    catch (Exception ex)
                    {
                        errorRows.Add((skipRows + processedCount + 1, $"Row processing error: {ex.Message}"));
                        errorCount++;
                        Console.SetCursorPosition(0, progressBarPosition + 4);
                        Console.WriteLine($"Error in row {skipRows + processedCount + 1}: {ex.Message}");
                        continue; // Skip to next record
                    }

                    processedCount++;

                    // Write batch file when reaching outputBatchSize
                    if (currentBatchRecords.Count >= outputBatchSize || processedCount == records.Count)
                    {
                        try
                        {
                            string batchFileName = Path.GetFileNameWithoutExtension(outputFile) + 
                                                 $"_batch{fileCounter}" +
                                                 Path.GetExtension(outputFile);
                            string batchFilePath = Path.Combine(Path.GetDirectoryName(outputFile), batchFileName);

                            using (var writer = new StreamWriter(batchFilePath))
                            using (var csv = new CsvWriter(writer, config))
                            {
                                csv.WriteRecords(currentBatchRecords);
                            }

                            Console.SetCursorPosition(0, progressBarPosition + 2);
                            Console.WriteLine($"Wrote batch file: {batchFileName} ({currentBatchRecords.Count} records)");
                            currentBatchRecords.Clear();
                            fileCounter++;
                        }
                        catch (Exception ex)
                        {
                            Console.SetCursorPosition(0, progressBarPosition + 4);
                            Console.WriteLine($"Error writing batch file: {ex.Message}");
                        }
                    }

                    // Update progress bar every 10 records
                    if (processedCount % 10 == 0 || processedCount == records.Count)
                    {
                        var percentComplete = (int)((double)processedCount / records.Count * 50);
                        Console.SetCursorPosition(progressBarLeft, progressBarPosition);
                        Console.Write(new string('█', percentComplete));
                        Console.SetCursorPosition(progressBarLeft + 50, progressBarPosition);
                        Console.Write("] ");
                        Console.Write($"{(double)processedCount / records.Count:P0}");
                        
                        Console.SetCursorPosition(0, progressBarPosition + 1);
                        Console.Write($"Records processed: {processedCount}/{records.Count} (Skipped: {skipRows}, Errors: {errorCount})   ");
                    }

                    if (processedCount % 500 == 0)
                    {
                        Console.SetCursorPosition(0, progressBarPosition + 3);
                        await CheckQuotaAsync();
                    }
                }

                if (i + processingBatchSize < records.Count)
                {
                    await Task.Delay(delayBetweenBatchesMs);
                }
            }

            // Write error log if there were any errors
            if (errorRows.Any())
            {
                string errorLogFile = Path.GetFileNameWithoutExtension(outputFile) + "_errors.log";
                string errorLogPath = Path.Combine(Path.GetDirectoryName(outputFile), errorLogFile);
                await File.WriteAllLinesAsync(errorLogPath, 
                    errorRows.Select(e => $"Row {e.RowNumber}: {e.Error}"));
                Console.WriteLine($"\nError details written to: {errorLogFile}");
            }

            Console.WriteLine("\nProcessing completed!");
            Console.WriteLine($"Created {fileCounter - 1} batch files");
            Console.WriteLine($"Total errors: {errorCount}");
        }

        private async Task<string> TranslateTextAsync(string text, TranslationType translationType, bool isProperNoun)
        {
            // For proper nouns, directly transliterate for Japanese-related translations
            if (isProperNoun && (translationType == TranslationType.Japanese || 
                                translationType == TranslationType.Hiragana || 
                                translationType == TranslationType.Katakana))
            {
                switch (translationType)
                {
                    case TranslationType.Japanese:
                        return _japaneseProcessor.ToKatakana(text);  // Default to Katakana for proper nouns
                    case TranslationType.Hiragana:
                        return _japaneseProcessor.ToHiragana(text);
                    case TranslationType.Katakana:
                        return _japaneseProcessor.ToKatakana(text);
                    default:
                        return text;
                }
            }

            // For non-Japanese languages or non-proper nouns, use regular translation
            var targetLang = _targetLanguages[translationType];
            
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("target_lang", targetLang),
                new KeyValuePair<string, string>("source_lang", "EN"),
                new KeyValuePair<string, string>("preserve_formatting", "1"),
                // Add formality preference for proper nouns
                new KeyValuePair<string, string>("formality", isProperNoun ? "more" : "default")
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

                return translatedText;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"API Request Error: {ex.Message}");
                throw;
            }
        }

        /* old method
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
        */

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