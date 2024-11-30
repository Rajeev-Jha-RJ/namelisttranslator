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
}