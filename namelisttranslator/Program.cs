﻿using CsvHelper;
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

                Console.Write("Enter the path for the output CSV file: ");
                var outputFile = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(outputFile))
                {
                    throw new ArgumentException("Output file path cannot be empty.");
                }

                Console.WriteLine("\nOperation Summary:");
                Console.WriteLine($"Input File: {inputFile}");
                Console.WriteLine($"Column to Translate: {columnToTranslate}");
                Console.WriteLine($"Selected Translations: {string.Join(", ", selectedTypes)}");
                Console.WriteLine($"Include Japanese Extras: {includeJapaneseExtras}");
                Console.WriteLine($"Output File: {outputFile}");
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
                    includeJapaneseExtras: includeJapaneseExtras);

                // Final quota check
                await translator.CheckQuotaAsync();

                Console.WriteLine($"\nTranslation completed successfully!");
                Console.WriteLine($"Output saved to: {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}