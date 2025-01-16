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

    public enum ProperNounHandling
    {
        Transliterate,
        Translate,
        Both
    }

    public class TranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;    
        private readonly string _apiUrl;
        private readonly JapaneseTextProcessor _japaneseProcessor;
        private readonly JapaneseTransliterator _transliterator;
        
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
            _transliterator = new JapaneseTransliterator();
        }

        public async Task ValidateApiKeyAsync()
        {
            try
            {
                // First validate the key format
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    throw new ArgumentException("API key cannot be empty");
                }

                if (!_apiKey.Contains("-"))
                {
                    throw new ArgumentException("Invalid API key format. Key should contain hyphens (-)");
                }

                // Validate free API key format
                if (_apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase))
                {
                    var keyParts = _apiKey.Split(':')[0].Split('-');
                    if (keyParts.Length != 5 || keyParts.Any(part => part.Length != 4))
                    {
                        throw new ArgumentException("Invalid Free API key format. Expected format: xxxx-xxxx-xxxx-xxxx-xxxx:fx");
                    }
                }

                // Try to check quota first
                try
                {
                    await CheckQuotaAsync();
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception($"Failed to validate API key: {ex.Message}. Please verify your API key is correct and you have internet connectivity.");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to check quota: {ex.Message}");
                }

                // Try a test translation
                try
                {
                    await TranslateTextAsync("test", TranslationType.Japanese, false,ProperNounHandling.Translate);
                    Console.WriteLine("API key validation successful!");
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception($"Translation test failed: {ex.Message}. Please verify your API key has translation permissions.");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Translation test failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAPI key validation failed: {ex.Message}");
                throw;
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
            bool isProperNoun = false,
            int skipRows = 0,
            int outputBatchSize = 1000,
            int processingBatchSize = 50,
            bool includeJapaneseExtras = false,
            int delayBetweenBatchesMs = 1000,
            ProperNounHandling properNounHandling = ProperNounHandling.Transliterate)
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
                                    if (isProperNoun && properNounHandling == ProperNounHandling.Both &&
                                        (translationType is TranslationType.Japanese or TranslationType.Hiragana or TranslationType.Katakana))
                                    {
                                        // For "Both" option, create two separate columns
                                        var transliterated = _transliterator.Transliterate(originalText);
                                        var translated = await PerformTranslation(originalText, translationType);

                                        // Add as two separate columns
                                        ((IDictionary<string, object>)record)[$"{columnName}_Transliterated"] = transliterated;
                                        ((IDictionary<string, object>)record)[$"{columnName}_Translated"] = translated;

                                        // Handle Japanese extras if needed
                                        if (includeJapaneseExtras && translationType == TranslationType.Japanese)
                                        {
                                            var processedText = _japaneseProcessor.ProcessText(translated);
                                            ((IDictionary<string, object>)record)[$"{columnName}_Romaji"] = processedText.Romaji;
                                            ((IDictionary<string, object>)record)[$"{columnName}_ReadingGuide"] = processedText.ReadingGuide;
                                            ((IDictionary<string, object>)record)[$"{columnName}_Segments"] = string.Join(" | ", processedText.Segments);
                                        }
                                    }
                                    else
                                    {
                                        // Normal processing for non-proper nouns or other handling options
                                        var (translated, _) = await TranslateTextAsync(
                                            originalText, 
                                            translationType, 
                                            isProperNoun,
                                            properNounHandling);

                                        ((IDictionary<string, object>)record)[$"{columnName}{_columnSuffixes[translationType]}"] = translated;

                                        if (includeJapaneseExtras && translationType == TranslationType.Japanese)
                                        {
                                            var processedText = _japaneseProcessor.ProcessText(translated);
                                            ((IDictionary<string, object>)record)[$"{columnName}_Romaji"] = processedText.Romaji;
                                            ((IDictionary<string, object>)record)[$"{columnName}_ReadingGuide"] = processedText.ReadingGuide;
                                            ((IDictionary<string, object>)record)[$"{columnName}_Segments"] = string.Join(" | ", processedText.Segments);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    string columnSuffix = (isProperNoun && properNounHandling == ProperNounHandling.Both) 
                                        ? "_Translated" 
                                        : _columnSuffixes[translationType];
                                        
                                    ((IDictionary<string, object>)record)[$"{columnName}{columnSuffix}"] = $"ERROR: {ex.Message}";
                                    errorRows.Add((skipRows + processedCount + 1, $"Translation error for {translationType}: {ex.Message}"));
                                    errorCount++;
                                }
                            }
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
                            string directoryPath = Path.GetDirectoryName(outputFile) ?? ".";
                            string batchFilePath = Path.Combine(directoryPath, batchFileName);

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
                string errorLogPath = Path.Combine(Path.GetDirectoryName(outputFile) ?? ".", errorLogFile);
                await File.WriteAllLinesAsync(errorLogPath, 
                    errorRows.Select(e => $"Row {e.RowNumber}: {e.Error}"));
                Console.WriteLine($"\nError details written to: {errorLogFile}");
            }

            Console.WriteLine("\nProcessing completed!");
            Console.WriteLine($"Created {fileCounter - 1} batch files");
            Console.WriteLine($"Total errors: {errorCount}");
        }

        private async Task<(string translated, string? transliterated)> TranslateTextAsync(
            string text, 
            TranslationType translationType, 
            bool isProperNoun,
            ProperNounHandling properNounHandling)
        {
            if (isProperNoun && 
                (translationType is TranslationType.Japanese or TranslationType.Hiragana or TranslationType.Katakana))
            {
                switch (properNounHandling)
                {
                    case ProperNounHandling.Transliterate:
                        return (_transliterator.Transliterate(text), null);
                        
                    case ProperNounHandling.Translate:
                        return (await PerformTranslation(text, translationType), null);
                        
                    case ProperNounHandling.Both:
                        var transliterated = _transliterator.Transliterate(text);
                        var translated = await PerformTranslation(text, translationType);
                        return (translated, transliterated);
                        
                    default:
                        return (_transliterator.Transliterate(text), null);
                }
            }

            // For non-proper nouns or non-Japanese translations
            return (await PerformTranslation(text, translationType), null);
        }

        private async Task<string> PerformTranslation(string text, TranslationType translationType)
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
                return doc.RootElement
                    .GetProperty("translations")[0]
                    .GetProperty("text")
                    .GetString() ?? string.Empty;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"API Request Error: {ex.Message}");
                throw;
            }
        }

        private void ValidateColumn(CsvReader csv, string columnName)
        {
            try
            {
                csv.Read();
                csv.ReadHeader();
                
                // Get all headers and check if the column exists (case-insensitive)
                var headers = csv.HeaderRecord ?? Array.Empty<string>();
                var hasColumn = headers.Any(h => h.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                
                if (!hasColumn)
                {
                    throw new ArgumentException($"Column '{columnName}' not found in CSV file. Available columns: {string.Join(", ", headers)}");
                }

                // Check for duplicate headers
                var duplicates = headers
                    .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                if (duplicates.Any())
                {
                    Console.WriteLine($"Warning: Found duplicate column headers: {string.Join(", ", duplicates)}");
                    Console.WriteLine("This may cause issues with the translation process.");
                }
            }
            catch (HeaderValidationException ex)
            {
                throw new Exception($"Error validating CSV headers: {ex.Message}");
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new Exception($"Error reading CSV file: {ex.Message}");
            }
        }
    }
}