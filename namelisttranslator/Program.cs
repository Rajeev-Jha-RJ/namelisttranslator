using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace CsvTranslator
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Enter your DeepL API key:");
                Console.WriteLine("- For Free API: Should end with ':fx' (format: xxxx-xxxx-xxxx-xxxx-xxxx:fx)");
                Console.WriteLine("- For Pro API: No specific prefix/suffix required");
                Console.Write("\nAPI Key: ");
                
                var apiKey = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new ArgumentException("API key cannot be empty.");
                }

                var translator = new TranslationService(apiKey);
                await translator.ValidateApiKeyAsync();

                
                string inputFile;
                do
                {
                    Console.Write("\nEnter the path to your input CSV file: ");
                    inputFile = Console.ReadLine()?.Trim() ?? "";
                    
                    if (!File.Exists(inputFile))
                    {
                        Console.WriteLine("File not found. Please enter a valid file path.");
                    }
                } while (!File.Exists(inputFile));

                var headers = File.ReadLines(inputFile).First().Split(',');
                Console.WriteLine("\nAvailable columns:");
                for (int i = 0; i < headers.Length; i++)
                {
                    Console.WriteLine($"{i + 1}. {headers[i]}");
                }

                Console.Write("\nEnter the name of the column to translate: ");
                var columnToTranslate = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(columnToTranslate))
                {
                    throw new ArgumentException("Column name cannot be empty.");
                }

                Console.Write("\nIs this column a proper noun (names, places, etc.)? (y/n): ");
                bool isProperNoun = Console.ReadLine()?.Trim().ToLower() == "y";

                ProperNounHandling properNounHandling = ProperNounHandling.Transliterate; // Default value
                if (isProperNoun)
                {
                    Console.WriteLine("\nHow would you like to handle proper nouns?");
                    Console.WriteLine("1. Transliterate only (convert to Japanese characters)");
                    Console.WriteLine("2. Translate only (standard translation)");
                    Console.WriteLine("3. Both (create two columns)");
                    Console.Write("\nEnter your choice (1-3): ");

                    if (int.TryParse(Console.ReadLine()?.Trim(), out int choice) && choice >= 1 && choice <= 3)
                    {
                        properNounHandling = (ProperNounHandling)(choice - 1);
                    }
                }

                Console.WriteLine("\nSelect translation types (comma-separated numbers):");
                Console.WriteLine("1. Japanese");
                Console.WriteLine("2. Hiragana");
                Console.WriteLine("3. Katakana");
                Console.WriteLine("4. Brazilian Portuguese");
                Console.WriteLine("5. Colombian Spanish");
                Console.Write("\nEnter your choices (e.g., 1,3,4): ");

                var selectedTypes = new List<TranslationType>();
                var choices = Console.ReadLine()?.Split(',');
                if (choices != null)
                {
                    foreach (var choice in choices)
                    {
                        if (int.TryParse(choice.Trim(), out int typeNum) && typeNum >= 1 && typeNum <= 5)
                        {
                            selectedTypes.Add((TranslationType)(typeNum - 1));
                        }
                    }
                }

                if (!selectedTypes.Any())
                {
                    throw new ArgumentException("No valid translation types selected.");
                }

                bool includeJapaneseExtras = false;
                if (selectedTypes.Contains(TranslationType.Japanese))
                {
                    Console.Write("\nWould you like to include additional Japanese processing (romaji, reading guide, text segments)? (y/n): ");
                    includeJapaneseExtras = Console.ReadLine()?.Trim().ToLower() == "y";
                }

                Console.Write("\nEnter number of rows to skip (0 for none): ");
                int skipRows = 0;
                if (!int.TryParse(Console.ReadLine()?.Trim(), out skipRows) || skipRows < 0)
                {
                    skipRows = 0;
                }

                Console.Write("\nEnter number of records per output file (default 1000): ");
                int outputBatchSize = 1000;
                if (!int.TryParse(Console.ReadLine()?.Trim(), out outputBatchSize) || outputBatchSize <= 0)
                {
                    outputBatchSize = 1000;
                }

                Console.Write("\nEnter processing batch size (number of records to process at once, default 50): ");
                int processingBatchSize = 50;
                if (!int.TryParse(Console.ReadLine()?.Trim(), out processingBatchSize) || processingBatchSize <= 0)
                {
                    processingBatchSize = 50;
                }

                Console.Write("Enter the path for the output CSV file: ");
                var outputFile = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(outputFile))
                {
                    throw new ArgumentException("Output file path cannot be empty.");
                }

                Console.WriteLine("\nOperation Summary:");
                Console.WriteLine($"Input File: {inputFile}");
                Console.WriteLine($"Column to Translate: {columnToTranslate}");
                Console.WriteLine($"Is Proper Noun: {isProperNoun}");
                if (isProperNoun)
                {
                    Console.WriteLine($"Proper Noun Handling: {properNounHandling}");
                }
                Console.WriteLine($"Selected Translations: {string.Join(", ", selectedTypes)}");
                Console.WriteLine($"Include Japanese Extras: {includeJapaneseExtras}");
                Console.WriteLine($"Skip Rows: {skipRows:N0} rows");
                Console.WriteLine($"Output Batch Size: {outputBatchSize:N0} records per file");
                Console.WriteLine($"Processing Batch Size: {processingBatchSize:N0} records at once");
                Console.WriteLine($"Output File Base: {outputFile}");
                Console.Write("\nProceed with translation? (y/n): ");
                
                if (Console.ReadLine()?.Trim().ToLower() != "y")
                {
                    Console.WriteLine("Operation cancelled by user.");
                    return;
                }

                Console.WriteLine("\nStarting translation...");
                await translator.TranslateCsvColumnAsync(
                    inputFile: inputFile,
                    outputFile: outputFile,
                    columnName: columnToTranslate,
                    translationTypes: selectedTypes,
                    isProperNoun: isProperNoun,
                    skipRows: skipRows,
                    outputBatchSize: outputBatchSize,
                    processingBatchSize: processingBatchSize,
                    includeJapaneseExtras: includeJapaneseExtras,   
                    properNounHandling: properNounHandling);
                
                // Final quota check
                await translator.CheckQuotaAsync();

                Console.WriteLine($"\nTranslation completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            //Console.ReadKey();
        }
    }
}