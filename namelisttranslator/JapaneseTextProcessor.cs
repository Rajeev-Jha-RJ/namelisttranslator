using System.Text;
using System.Text.RegularExpressions;

namespace CsvTranslator
{
    public class JapaneseTextProcessor
    {
        private static readonly Regex KanjiPattern = new Regex(@"[\u4e00-\u9faf]", RegexOptions.Compiled);

        public class ProcessedJapaneseText
        {
            public required string Original { get; set; }
            public required string Hiragana { get; set; }
            public required string Katakana { get; set; }
            public required string Romaji { get; set; }
            public required List<string> Segments { get; set; }
            public required string ReadingGuide { get; set; }
        }

        private static readonly Dictionary<char, char> HiraganaToKatakanaMap;
        private static readonly Dictionary<string, string> RomajiMap;

        static JapaneseTextProcessor()
        {
            // Initialize Hiragana to Katakana mapping
            HiraganaToKatakanaMap = new Dictionary<char, char>();
            for (char h = 'ぁ'; h <= 'ゖ'; h++)
            {
                HiraganaToKatakanaMap[h] = (char)(h + 0x60);
            }

            // Initialize Romaji mapping
            RomajiMap = new Dictionary<string, string>
            {
                {"あ", "a"}, {"い", "i"}, {"う", "u"}, {"え", "e"}, {"お", "o"},
                {"か", "ka"}, {"き", "ki"}, {"く", "ku"}, {"け", "ke"}, {"こ", "ko"},
                {"さ", "sa"}, {"し", "shi"}, {"す", "su"}, {"せ", "se"}, {"そ", "so"},
                {"た", "ta"}, {"ち", "chi"}, {"つ", "tsu"}, {"て", "te"}, {"と", "to"},
                {"な", "na"}, {"に", "ni"}, {"ぬ", "nu"}, {"ね", "ne"}, {"の", "no"},
                {"は", "ha"}, {"ひ", "hi"}, {"ふ", "fu"}, {"へ", "he"}, {"ほ", "ho"},
                {"ま", "ma"}, {"み", "mi"}, {"む", "mu"}, {"め", "me"}, {"も", "mo"},
                {"や", "ya"}, {"ゆ", "yu"}, {"よ", "yo"},
                {"ら", "ra"}, {"り", "ri"}, {"る", "ru"}, {"れ", "re"}, {"ろ", "ro"},
                {"わ", "wa"}, {"を", "wo"}, {"ん", "n"},
                // Dakuten
                {"が", "ga"}, {"ぎ", "gi"}, {"ぐ", "gu"}, {"げ", "ge"}, {"ご", "go"},
                {"ざ", "za"}, {"じ", "ji"}, {"ず", "zu"}, {"ぜ", "ze"}, {"ぞ", "zo"},
                {"だ", "da"}, {"ぢ", "ji"}, {"づ", "zu"}, {"で", "de"}, {"ど", "do"},
                {"ば", "ba"}, {"び", "bi"}, {"ぶ", "bu"}, {"べ", "be"}, {"ぼ", "bo"},
                // Handakuten
                {"ぱ", "pa"}, {"ぴ", "pi"}, {"ぷ", "pu"}, {"ぺ", "pe"}, {"ぽ", "po"},
                // Contracted sounds
                {"きょ", "kyo"}, {"きゅ", "kyu"}, {"きゃ", "kya"},
                {"しょ", "sho"}, {"しゅ", "shu"}, {"しゃ", "sha"},
                {"ちょ", "cho"}, {"ちゅ", "chu"}, {"ちゃ", "cha"},
                {"にょ", "nyo"}, {"にゅ", "nyu"}, {"にゃ", "nya"},
                {"ひょ", "hyo"}, {"ひゅ", "hyu"}, {"ひゃ", "hya"},
                {"みょ", "myo"}, {"みゅ", "myu"}, {"みゃ", "mya"},
                {"りょ", "ryo"}, {"りゅ", "ryu"}, {"りゃ", "rya"},
                {"ぎょ", "gyo"}, {"ぎゅ", "gyu"}, {"ぎゃ", "gya"},
                {"びょ", "byo"}, {"びゅ", "byu"}, {"びゃ", "bya"},
                {"ぴょ", "pyo"}, {"ぴゅ", "pyu"}, {"ぴゃ", "pya"}
            };
        }

        public ProcessedJapaneseText ProcessText(string input)
        {
            var result = new ProcessedJapaneseText
            {
                Original = input,
                Hiragana = ToHiragana(input),
                Katakana = ToKatakana(input),
                Romaji = ToRomaji(input),
                Segments = SegmentText(input),
                ReadingGuide = CreateReadingGuide(input)
            };

            return result;
        }

        public string ToHiragana(string input)
        {
            // Convert katakana to hiragana and preserve other characters
            var result = new StringBuilder();
            foreach (char c in input)
            {
                if (IsKatakana(c))
                {
                    // Convert Katakana to Hiragana by subtracting the Unicode offset
                    result.Append((char)(c - 0x60));
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        public string ToKatakana(string input)
        {
            var result = new StringBuilder();
            foreach (char c in input)
            {
                if (HiraganaToKatakanaMap.TryGetValue(c, out char katakana))
                {
                    result.Append(katakana);
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        public string ToRomaji(string input)
        {
            var result = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                bool found = false;
                // Try to match contracted sounds (two characters) first
                if (i < input.Length - 1)
                {
                    string pair = input.Substring(i, 2);
                    if (RomajiMap.TryGetValue(pair, out string? romajiPair) && romajiPair is not null)
                    {
                        result.Append(romajiPair);
                        i++;
                        found = true;
                        continue;
                    }
                }
                // If no contracted sound found, try single character
                string single = input[i].ToString();
                if (RomajiMap.TryGetValue(single, out string? romaji) && romaji is not null)
                {
                    result.Append(romaji);
                    found = true;
                }
                if (!found)
                {
                    result.Append(input[i]);
                }
            }
            return result.ToString();
        }

        public List<string> SegmentText(string input)
        {
            // Simple segmentation by spaces and punctuation
            return input.Split(new[] { ' ', '　', '。', '、', '！', '？' }, 
                             StringSplitOptions.RemoveEmptyEntries)
                       .ToList();
        }

        public string CreateReadingGuide(string input)
        {
            return $"{input} ({ToRomaji(input)})";
        }

        private bool IsHiragana(char c)
        {
            return c >= 0x3040 && c <= 0x309F;
        }

        private bool IsKatakana(char c)
        {
            return c >= 0x30A0 && c <= 0x30FF;
        }
    }

    public class JapaneseTransliterator
    {
    private readonly Dictionary<string, string> _phoneticMappings;

    public JapaneseTransliterator()
    {
        _phoneticMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Complete words and common names
            {"hotel", "ホテル"},
            {"albania", "アルバニア"},
            {"international", "インターナショナル"},
            {"restaurant", "レストラン"},
            {"resort", "リゾート"},
            
            // Consonant combinations (must come before single consonants)
            {"mak", "マク"},
            {"mac", "マク"},
            {"mc", "マク"},
            {"bh", "ブ"},
            {"gh", "グ"},
            {"kh", "ク"},
            {"ph", "フ"},
            {"sh", "シ"},
            {"ch", "チ"},
            {"th", "ス"},
            {"wh", "ウ"},
            {"ck", "ック"},
            {"ng", "ング"},
            {"mb", "ンブ"},
            
            // Common syllables
            {"ma", "マ"},
            {"mi", "ミ"},
            {"mu", "ム"},
            {"me", "メ"},
            {"mo", "モ"},
            {"ba", "バ"},
            {"bi", "ビ"},
            {"bu", "ブ"},
            {"be", "ベ"},
            {"bo", "ボ"},
            {"pa", "パ"},
            {"pi", "ピ"},
            {"pu", "プ"},
            {"pe", "ペ"},
            {"po", "ポ"},
            {"ta", "タ"},
            {"ti", "ティ"},
            {"tu", "トゥ"},
            {"te", "テ"},
            {"to", "ト"},
            {"da", "ダ"},
            {"di", "ディ"},
            {"du", "ドゥ"},
            {"de", "デ"},
            {"do", "ド"},
            {"ka", "カ"},
            {"ki", "キ"},
            {"ku", "ク"},
            {"ke", "ケ"},
            {"ko", "コ"},
            {"ga", "ガ"},
            {"gi", "ギ"},
            {"gu", "グ"},
            {"ge", "ゲ"},
            {"go", "ゴ"},
            {"sa", "サ"},
            {"si", "シ"},
            {"su", "ス"},
            {"se", "セ"},
            {"so", "ソ"},
            {"za", "ザ"},
            {"zi", "ジ"},
            {"zu", "ズ"},
            {"ze", "ゼ"},
            {"zo", "ゾ"},
            {"ra", "ラ"},
            {"ri", "リ"},
            {"ru", "ル"},
            {"re", "レ"},
            {"ro", "ロ"},
            {"wa", "ワ"},
            {"wi", "ウィ"},
            {"wu", "ウ"},
            {"we", "ウェ"},
            {"wo", "ヲ"},
            {"ya", "ヤ"},
            {"yu", "ユ"},
            {"yo", "ヨ"},
            {"ha", "ハ"},
            {"hi", "ヒ"},
            {"hu", "フ"},
            {"he", "ヘ"},
            {"ho", "ホ"},
            {"na", "ナ"},
            {"ni", "ニ"},
            {"nu", "ヌ"},
            {"ne", "ネ"},
            {"no", "ノ"},
            {"la", "ラ"},
            {"li", "リ"},
            {"lu", "ル"},
            {"le", "レ"},
            {"lo", "ロ"},

            // Single letters (used as fallback)
            {"a", "ア"},
            {"i", "イ"},
            {"u", "ウ"},
            {"e", "エ"},
            {"o", "オ"},
            {"n", "ン"},
            
            // Single consonants (with default vowel sound)
            {"b", "ブ"},
            {"c", "ク"},
            {"d", "ド"},
            {"f", "フ"},
            {"g", "グ"},
            {"h", "フ"},
            {"j", "ジ"},
            {"k", "ク"},
            {"l", "ル"},
            {"m", "ム"},
            {"p", "プ"},
            {"q", "ク"},
            {"r", "ル"},
            {"s", "ス"},
            {"t", "ト"},
            {"v", "ブ"},
            {"w", "ウ"},
            {"x", "クス"},
            {"y", "イ"},
            {"z", "ズ"}
        };

    }

    public string Transliterate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Split input into words for better word-by-word handling
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        foreach (var word in words)
        {
            // Try to match the complete word first
            if (_phoneticMappings.TryGetValue(word.ToLower(), out var completeWord))
            {
                result.Append(completeWord);
            }
            else
            {
                // Process word character by character if no complete match
                var i = 0;
                var processedWord = new StringBuilder();
                
                while (i < word.Length)
                {
                    bool matchFound = false;
                    
                    // Try to match longest possible substring
                    for (int len = Math.Min(4, word.Length - i); len > 0 && !matchFound; len--)
                    {
                        var substr = word.Substring(i, len).ToLower();
                        if (_phoneticMappings.TryGetValue(substr, out var kana))
                        {
                            processedWord.Append(kana);
                            i += len;
                            matchFound = true;
                        }
                    }

                    // If no match found, use single character mapping
                    if (!matchFound)
                    {
                        var c = word[i].ToString().ToLower();
                        processedWord.Append(_phoneticMappings.GetValueOrDefault(c, c));
                        i++;
                    }
                }

                result.Append(processedWord);
            }
            
            // Add space between words if not the last word
            if (word != words.Last())
            {
                result.Append("・");  // Using middle dot as word separator
            }
        }

        return result.ToString();
    }

    private string PreProcessInput(string input)
    {
        // Handle silent 'e' at the end of words
        if (input.EndsWith("e"))
        {
            input = input[..^1];
        }

        // Handle common English patterns before transliteration
        return input
            .Replace("ough", "オー")    // through -> スルー
            .Replace("ought", "オート")  // bought -> ボート
            .Replace("augh", "オー")     // taught -> トート
            .Replace("tion", "ション")   // action -> アクション
            .Replace("sion", "ション")   // vision -> ビジョン
            .Replace("cial", "シャル")   // special -> スペシャル
            .Replace("tial", "シャル");  // partial -> パーシャル
    }

    private string PostProcessResult(string input)
    {
        // Handle final 'n' sound
        var result = input.Replace("nン", "ン");

        // Handle repeated vowels and long sounds
        result = HandleVowelLengthening(result);

        return result;
    }

    private string HandleVowelLengthening(string input)
    {
        // Replace repeated vowels with long vowel mark
        var result = input
            .Replace("アア", "アー")
            .Replace("イイ", "イー")
            .Replace("ウウ", "ウー")
            .Replace("エエ", "エー")
            .Replace("オオ", "オー")
            // Handle long vowel sounds after specific characters
            .Replace("エイ", "エー")
            .Replace("オウ", "オー")
            // Handle special cases
            .Replace("ビィ", "ビー")
            .Replace("ティィ", "ティー")
            .Replace("ディィ", "ディー");

        // Handle long vowel sounds in names
        if (result.Length > 2)
        {
            // Add long vowel mark after certain endings
            if (result.EndsWith("イ") || result.EndsWith("ウ"))
            {
                result += "ー";
            }
        }

        return result;
        }
    }
}