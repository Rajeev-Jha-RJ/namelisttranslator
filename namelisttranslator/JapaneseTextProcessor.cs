using System.Text;
using System.Text.RegularExpressions;

namespace CsvTranslator
{
    public class JapaneseTextProcessor
    {
        private static readonly Regex KanjiPattern = new Regex(@"[\u4e00-\u9faf]", RegexOptions.Compiled);

        public class ProcessedJapaneseText
        {
            public string Original { get; set; }
            public string Hiragana { get; set; }
            public string Katakana { get; set; }
            public string Romaji { get; set; }
            public List<string> Segments { get; set; }
            public string ReadingGuide { get; set; }
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
                    if (RomajiMap.TryGetValue(pair, out string romajiPair))
                    {
                        result.Append(romajiPair);
                        i++;
                        found = true;
                        continue;
                    }
                }
                // If no contracted sound found, try single character
                string single = input[i].ToString();
                if (RomajiMap.TryGetValue(single, out string romaji))
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
            // Common English word endings
            {"worth", "ワース"},
            {"ville", "ビル"},
            {"field", "フィールド"},
            {"bridge", "ブリッジ"},
            {"burgh", "バラ"},
            {"water", "ウォーター"},
            {"berry", "ベリー"},
            {"shire", "シャー"},
            {"stone", "ストーン"},
            {"leigh", "リー"},
            {"port", "ポート"},
            {"side", "サイド"},
            {"wood", "ウッド"},
            {"land", "ランド"},
            {"gate", "ゲート"},
            {"ford", "フォード"},
            {"view", "ビュー"},
            {"hill", "ヒル"},
            {"son", "ソン"},
            {"ton", "トン"},

            // Common letter patterns (longest first)
            {"ough", "オー"},    // through -> スルー
            {"ought", "オート"},  // bought -> ボート
            {"augh", "オー"},     // taught -> トート
            {"tion", "ション"},   // action -> アクション
            {"sion", "ション"},   // vision -> ビジョン
            {"cial", "シャル"},   // special -> スペシャル
            {"tial", "シャル"},   // partial -> パーシャル

            // Vowel combinations
            {"ee", "イー"},
            {"oo", "ウー"},
            {"ai", "エイ"},
            {"ay", "エイ"},
            {"ie", "アイ"},
            {"oa", "オー"},
            {"au", "オー"},
            {"ou", "アウ"},
            {"ea", "イー"},
            {"ey", "エイ"},
            {"igh", "アイ"},
            {"ow", "オー"},
            {"ew", "ュー"},

            // Complex consonant combinations
            {"str", "ストr"},
            {"spr", "スプr"},
            {"scr", "スクr"},
            {"spl", "スプl"},
            {"thr", "スr"},
            {"chr", "クr"},
            {"dge", "ッジ"},

            // Special syllable combinations
            {"tha", "サ"},
            {"thi", "シ"},
            {"thu", "ス"},
            {"the", "ゼ"},
            {"tho", "ソ"},
            {"cha", "チャ"},
            {"chi", "チ"},
            {"chu", "チュ"},
            {"che", "チェ"},
            {"cho", "チョ"},
            {"sha", "シャ"},
            {"shi", "シ"},
            {"shu", "シュ"},
            {"she", "シェ"},
            {"sho", "ショ"},
            {"kya", "キャ"},
            {"kyu", "キュ"},
            {"kyo", "キョ"},
            {"gya", "ギャ"},
            {"gyu", "ギュ"},
            {"gyo", "ギョ"},
            {"nya", "ニャ"},
            {"nyu", "ニュ"},
            {"nyo", "ニョ"},
            {"hya", "ヒャ"},
            {"hyu", "ヒュ"},
            {"hyo", "ヒョ"},
            {"mya", "ミャ"},
            {"myu", "ミュ"},
            {"myo", "ミョ"},
            {"rya", "リャ"},
            {"ryu", "リュ"},
            {"ryo", "リョ"},
            {"bya", "ビャ"},
            {"byu", "ビュ"},
            {"byo", "ビョ"},
            {"pya", "ピャ"},
            {"pyu", "ピュ"},
            {"pyo", "ピョ"},

            // Basic consonant + vowel combinations
            {"ka", "カ"}, {"ki", "キ"}, {"ku", "ク"}, {"ke", "ケ"}, {"ko", "コ"},
            {"sa", "サ"}, {"si", "シ"}, {"su", "ス"}, {"se", "セ"}, {"so", "ソ"},
            {"ta", "タ"}, {"te", "テ"}, {"to", "ト"},
            {"na", "ナ"}, {"ni", "ニ"}, {"nu", "ヌ"}, {"ne", "ネ"}, {"no", "ノ"},
            {"ha", "ハ"}, {"hi", "ヒ"}, {"fu", "フ"}, {"he", "ヘ"}, {"ho", "ホ"},
            {"ma", "マ"}, {"mi", "ミ"}, {"mu", "ム"}, {"me", "メ"}, {"mo", "モ"},
            {"ya", "ヤ"}, {"yu", "ユ"}, {"yo", "ヨ"},
            {"ra", "ラ"}, {"ri", "リ"}, {"ru", "ル"}, {"re", "レ"}, {"ro", "ロ"},
            {"wa", "ワ"}, {"wo", "ヲ"},

            // Double consonants (geminate)
            {"kk", "ッk"},
            {"tt", "ッt"},
            {"ss", "ッs"},
            {"pp", "ッp"},
            {"bb", "ッb"},
            {"dd", "ッd"},
            {"ff", "ッf"},
            {"gg", "ッg"},
            {"mm", "ッm"},
            {"rr", "ッr"},
            {"ll", "ッl"},

            // Single vowels
            {"a", "ア"},
            {"i", "イ"},
            {"u", "ウ"},
            {"e", "エ"},
            {"o", "オ"},
            {"n", "ン"},

            // Foreign sound combinations
            {"va", "ヴァ"}, {"vi", "ヴィ"}, {"vu", "ヴ"}, {"ve", "ヴェ"}, {"vo", "ヴォ"},
            {"fa", "ファ"}, {"fi", "フィ"}, {"fe", "フェ"}, {"fo", "フォ"},
            {"qa", "クァ"}, {"qi", "クィ"}, {"qe", "クェ"}, {"qo", "クォ"},

            // Extended katakana
            {"ye", "イェ"},
            {"wi", "ウィ"},
            {"we", "ウェ"}
        };
    }

    public string Transliterate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Pre-process the input for better handling of English patterns
        var processedInput = PreProcessInput(input);
        
        var result = new StringBuilder();
        var lowerInput = processedInput.ToLower();
        int i = 0;

        while (i < lowerInput.Length)
        {
            bool matchFound = false;
            
            // Try to match longest possible substring first
            for (int j = Math.Min(8, lowerInput.Length - i); j > 0 && !matchFound; j--)
            {
                if (i + j <= lowerInput.Length)
                {
                    var substr = lowerInput.Substring(i, j);
                    if (_phoneticMappings.TryGetValue(substr, out string? kana))
                    {
                        result.Append(kana);
                        i += j;
                        matchFound = true;
                    }
                }
            }

            // Handle unmatched characters
            if (!matchFound)
            {
                char c = lowerInput[i];
                if ("bcdfghjklmnpqrstvwxyz".Contains(c))
                {
                    // Add a vowel sound for lone consonants
                    if (i + 1 >= lowerInput.Length || "bcdfghjklmnpqrstvwxyz".Contains(lowerInput[i + 1]))
                    {
                        result.Append(_phoneticMappings.GetValueOrDefault(c + "u", c.ToString()));
                    }
                    else
                    {
                        result.Append(c);
                    }
                }
                else
                {
                    result.Append(_phoneticMappings.GetValueOrDefault(c.ToString(), c.ToString()));
                }
                i++;
            }
        }

        // Post-process the result
        var finalResult = PostProcessResult(result.ToString());
        
        return finalResult;
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