using System.Text;
using System.Text.RegularExpressions;

namespace TG.Payroll.Web.Services;

public static class BanglaBijoyConverter
{
    private static readonly Dictionary<string, string> MultiCharMap = new()
    {
        // Honorifics and common prefixes
        { "‡gvt", "মোঃ" },
        { "†gvt", "মোঃ" },
        { "‡gvQvt", "মোছাঃ" },
        { "†gvQvt", "মোছাঃ" },
        { "wmwbt", "সিনিঃ" },
        { "Rywbt", "জুনিঃ" },
        { "mnt", "সহঃ" },
        { "kÖx", "শ্রী" },
        { "kÖxgwZ", "শ্রীমতী" },
        { "kÖxgङ", "শ্রীমতী" },
        { "W.", "ড." },

        // 3+ character complex clusters
        { "Avn‡¤§`", "আহাম্মদ" },
        { "Avn‡g`", "আহমেদ" },
        { "Bmjvg", "ইসলাম" },
        { "ingvb", "রহমান" },
        { "†nv‡mb", "হোসেন" },
        { "‡nv‡mb", "হোসেন" },
        { "Avkivdzj", "আশরাফুল" },
        { "bRiæj", "নজরুল" },
        { "†gvdv¾j", "মোফাজ্জল" },
        { "weRq", "বিজয়" },
        { "DËg", "উত্তম" },
        { "Avwbmyi", "আনিসুর" },
        { "gwbi", "মনির" },
        { "mwn`", "সহিদ" },
        { "wgqv", "মিয়া" },
        { "†Qv‡jgvb", "ছোলেমান" },
        { "†kL", "শেখ" },
        { "Kzgvi", "কুমার" },
        { "†Nvl", "ঘোষ" },
        { "†eMg", "বেগম" },
        { "Kvwigv", "কারিমা" },
        { "bvRgyj", "নাজমুল" },
        { "Avjx", "আলী" },
        { "Lvb", "খান" },
        { "Av³vi", "আক্তার" },
        { "LvZzb", "খাতুন" },
        { "ivbx", "রানী" },
        { "P›`ª", "চন্দ্র" },
        { "evey", "বাবু" },
        { "nvmb", "হাসান" },
        { "†PŠayix", "চৌধুরী" },
        { "Avjg", "আলম" },
        { "wkK`vi", "শিকদার" },
        { "gÛj", "মন্ডল" },
        { "nvIjv`vi", "হাওলাদার" },
        { "cvUvIqvix", "পাটোয়ারী" },

        // 2-character conjuncts and clusters
        { "¯ú¨", "স্প্য" },
        { "¯ú", "স্প" },
        { "¯Í", "স্ত" },
        { "¯’", "স্থ" },
        { "¯œ", "স্ন" },
        { "¯¢", "স্ফ" },
        { "¯^", "স্ব" },
        { "¯§", "স্ম" },
        { "¯¿", "স্র" },
        { "m¨", "স্য" },
        { "kÖ", "শ্র" },
        { "kø", "শ্ল" },
        { "k^", "শ্ব" },
        { "k¥", "শ্ম" },
        { "nª", "হ্র" },
        { "nœ", "হ্ন" },
        { "n¥", "হ্ম" },
        { "n¬", "হ্ল" },
        { "nŸ", "হ্ব" },
        { "¤ú", "ম্প" },
        { "¤c", "ম্প" },
        { "¤§", "ম্ম" },
        { "¤^", "ম্ব" },
        { "¤¢", "ম্ভ" },
        { "bœ", "ন্ন" },
        { "b¥", "ন্ম" },
        { "eœ", "ব্ন" },
        { "eÜ", "বন্ধ" },
        { "eª", "ব্র" },
        { "Wª", "ড্র" },
        { "Uª", "ট্র" },
        { "Gg", "এম" },
        { "Iq", "ওয়" },
        { "Av", "আ" },
        { "Bq", "ইয়" },
        { "e¨", "ব্য" },
        { "g¨", "ম্য" },
        { "d¨", "ফ্য" },
        { "K¬", "ক্ল" },
        { "Mø", "গ্ল" },
        { "cø", "প্ল" },
        { "K«", "ক্র" },
        { "M«", "গ্র" },
        { "c«", "প্র" },
        { "f«", "ভ্র" },
        { "Î", "ত্র" },
        { "²", "ক্ষ" },
        { "³", "ক্ত" },
        { "·", "ক্স" },
        // Garment & Industry Designations and Keywords
        { "wìs", "ল্ডিং" },
        { "wì", "ল্ড" },
        // Conjuncts with prefix i-kar (w)
        { "Kw¤úDUvi", "কম্পিউটার" },
        { "Gw›Uª", "এন্ট্রি" },
        { "G›Uª", "এন্ট্র" },
        { "w›Uª", "ন্ট্রি" },
        { "w›U", "ন্টি" },
        { "›Uª", "ন্ট্র" },
        { "›U", "ন্ট" },
        { "w¤ú", "ম্পি" },
        { "w¯ú", "স্পি" },
        { "w¯Í", "স্তি" },
        { "w¯’", "স্থি" },
        { "wbœ", "ন্নি" },
        { "wUª", "ট্রি" },
        { "wWª", "ড্রি" },
        { "wc«", "প্রি" },
        { "wK«", "ক্রি" },
        { "wM«", "গ্রি" },
        { "weª", "ব্রি" },
        { "GgeªqWvix", "এমব্রয়ডারী" },
        { "Ggeª", "এমব্র" },
        { "‡dvwìs", "ফোল্ডিং" },
        { "wjdU", "লিফট" },
        { "wdUvi", "ফিটার" },
        { "wPjvi", "চিলার" },
        { "‡UKwbwkqvb", "টেকনিশিয়ান" },
        { "B‡jKwUªwkqvb", "ইলেকট্রিশিয়ান" },
        { "wkqvb", "শিয়ান" },
        { "Acv‡iUi", "অপারেটর" },
        { "‡Rbv‡iUi", "জেনারেটর" },
        { "†Wwjfvix", "ডেলিভারী" },
        { "†njcvi", "হেলপার" },
        { "mycvifvBRvi", "সুপারভাইজার" },
        { "g¨vb", "ম্যান" },
        { "e¨vK", "ব্যাক" },
        { "mvBRvi", "সাইজার" },
        { "‡gwW‡Kj", "মেডিকেল" },
        { "Awdmvi", "অফিসার" },
        { "PvBì‡Kqvi", "চাইল্ডকেয়ার" },
        { "PvBì", "চাইল্ড" },
        { "Mf‡b©m", "গভর্নেন্স" },
        { "Bqvb©", "ইয়ার্ন" },
        { "WvBs", "ডাইং" },
        { "‡KvqvwjwU", "কোয়ালিটি" },
        { "†PKvi", "চেকার" },
        { "WvUv", "ডাটা" },
        { "‡nW", "হেড" },
        { "gv÷vi", "মাস্টার" },
        { "g¨v‡bRvi", "ম্যানেজার" },
        { "KvwUs", "কাটিং" },
        { "myBs", "সুইং" },
        { "wdwbwks", "ফিনিশিং" },
        { "K¬xbvi", "ক্লিনার" },
        { "b©m", "র্নেন্স" },
        { "b©", "র্ন" },
        { "q©", "র্য" },
        { "U©", "র্ট" },
        { "W©", "র্ড" },
        { "m©", "র্স" },
        { "d©", "র্ফ" },
        { "c©", "র্প" },
        { "j©", "র্ল" },
        { "g©", "র্ম" },
        { "K©", "র্ক" },
        { "M©", "র্গ" },
        { "P©", "র্চ" }
    };

    private static readonly Dictionary<string, string> GlyphsMap = new()
    {
        { "¼", "ঙ্ক" },
        { "½", "ঙ্গ" },
        { "¾", "জ্জ" },
        { "¿", "র্জ" },
        { "À", "জ্ঞ" },
        { "Á", "জ্ঞ" },
        { "Â", "ঞ্চ" },
        { "Ã", "ঞ্ছ" },
        { "Ä", "ঞ্জ" },
        { "Å", "ট্" },
        { "Æ", "ট্ট" },
        { "Ç", "ড্ড" },
        { "È", "ণ্ট" },
        { "É", "ণ্ঠ" },
        { "Ê", "ণ্ড" },
        { "Ë", "ত্ত" },
        { "Ì", "ত্থ" },
        { "Í", "্ত" },
        { "Î", "ত্র" },
        { "Ï", "থ্" },
        { "Ð", "দ্দ" },
        { "Ñ", "দ্ধ" },
        { "Ò", "দ্ব" },
        { "Ó", "দ্ম" },
        { "Ô", "ধ্" },
        { "Õ", "ধ্ন" },
        { "Ö", "্র" },
        { "×", "দ্ধ" },
        { "Ø", "ন্ট" },
        { "Ù", "ন্ড" },
        { "Ú", "ন্ত" },
        { "Û", "ন্দ" },
        { "Ü", "ন্ধ" },
        { "Ý", "ন্ম" },
        { "Þ", "প্ট" },
        { "ß", "প্ত" },
        { "à", "প্ন" },
        { "á", "প্প" },
        { "â", "প্স" },
        { "ã", "ব্দ" },
        { "ä", "ব্ধ" },
        { "å", "ব্ব" },
        { "æ", "রু" },
        { "ç", "রূ" },
        { "è", "ল্ক" },
        { "é", "ল্গ" },
        { "ê", "ল্ট" },
        { "ë", "ল্ড" },
        { "ì", "ল্ড" },
        { "í", "ল্ফ" },
        { "î", "ল্ব" },
        { "ï", "ল্ম" },
        { "ð", "ল্ল" },
        { "ñ", "ষ্প" },
        { "ò", "ষ্ফ" },
        { "ó", "ষ্ট" },
        { "ô", "ষ্ঠ" },
        { "õ", "ষ্ণ" },
        { "ö", "ষ্ম" },
        { "÷", "স্ক" },
        { "ø", "স্খ" },
        { "ù", "স্ট" },
        { "ú", "স্প" },
        { "û", "স্ফ" },
        { "ü", "হৃ" },
        { "ý", "হু" },
        { "þ", "হ্ন" },
        { "ÿ", "হ্ম" }
    };

    private static readonly Dictionary<char, string> SingleCharMap = new()
    {
        { 'A', "অ" },
        { 'B', "ই" },
        { 'C', "ঈ" },
        { 'D', "উ" },
        { 'E', "ঊ" },
        { 'F', "ঋ" },
        { 'G', "এ" },
        { 'H', "ঐ" },
        { 'I', "ও" },
        { 'J', "ঔ" },
        { 'K', "ক" },
        { 'L', "খ" },
        { 'M', "গ" },
        { 'N', "ঘ" },
        { 'O', "ঙ" },
        { 'P', "চ" },
        { 'Q', "ছ" },
        { 'R', "জ" },
        { 'S', "ঝ" },
        { 'T', "ঞ" },
        { 'U', "ট" },
        { 'V', "ঠ" },
        { 'W', "ড" },
        { 'X', "ঢ" },
        { 'Y', "ণ" },
        { 'Z', "ত" },
        { '_', "থ" },
        { '`', "দ" },
        { 'a', "ধ" },
        { 'b', "ন" },
        { 'c', "প" },
        { 'd', "ফ" },
        { 'e', "ব" },
        { 'f', "ভ" },
        { 'g', "ম" },
        { 'h', "য" },
        { 'i', "র" },
        { 'j', "ল" },
        { 'k', "শ" },
        { 'l', "ষ" },
        { 'm', "স" },
        { 'n', "হ" },
        { 'o', "ড়" },
        { 'p', "ঢ়" },
        { 'q', "য়" },
        { 'r', "ৎ" },
        { 's', "ং" },
        { 't', "ঃ" },
        { 'u', "ঁ" },
        { 'v', "া" },
        { 'w', "ি" },
        { 'x', "ী" },
        { 'y', "ু" },
        { '~', "ূ" },
        { '„', "ৃ" },
        { '†', "ে" },
        { '‡', "ে" },
        { 'ˆ', "ৈ" },
        { 'Š', "ৌ" },
        { '¨', "্য" },
        { 'ª', "্র" },
        { '©', "র্" },
        { '&', "্" },
        { '|', "।" },
        { '0', "০" },
        { '1', "১" },
        { '2', "২" },
        { '3', "৩" },
        { '4', "৪" },
        { '5', "৫" },
        { '6', "৬" },
        { '7', "৭" },
        { '8', "৮" },
        { '9', "৯" }
    };

    public static bool IsUnicodeBangla(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        return input.Any(c => c >= 0x0980 && c <= 0x09FF);
    }

    public static bool LooksLikeBijoyAnsi(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (IsUnicodeBangla(input)) return false;
        return true;
    }

    public static string ConvertToUnicode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var text = input;

        // If already containing full Unicode, return directly
        if (IsUnicodeBangla(text))
        {
            return text;
        }

        // 1. Multi-character replacements (longest match first)
        foreach (var (k, v) in MultiCharMap.OrderByDescending(x => x.Key.Length))
        {
            text = text.Replace(k, v);
        }

        // 2. Glyph replacements (longest match first)
        foreach (var (k, v) in GlyphsMap.OrderByDescending(x => x.Key.Length))
        {
            text = text.Replace(k, v);
        }

        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Re-order prefix vowel signs: E-kar (w / † / ‡ / ˆ) in SutonnyMJ appears BEFORE the consonant
            if (c is 'w' or '†' or '‡' or 'ˆ')
            {
                var vowel = c switch
                {
                    'w' => "ি",
                    '†' => "ে",
                    '‡' => "ে",
                    'ˆ' => "ৈ",
                    _ => ""
                };

                // Look ahead for the consonant or cluster
                int nextIdx = i + 1;
                if (nextIdx < text.Length)
                {
                    char nextC = text[nextIdx];
                    if (SingleCharMap.TryGetValue(nextC, out var nextMapped))
                    {
                        sb.Append(nextMapped);
                        // Check if there's a hasanta or conjunct or ya-phala or ra-phala
                        while (nextIdx + 1 < text.Length && text[nextIdx + 1] is '¨' or 'ª' or '&')
                        {
                            char modifier = text[nextIdx + 1];
                            if (SingleCharMap.TryGetValue(modifier, out var modMapped))
                            {
                                sb.Append(modMapped);
                            }
                            nextIdx++;
                        }
                        sb.Append(vowel);
                        i = nextIdx;
                        continue;
                    }
                    else if (nextC >= 0x0980 && nextC <= 0x09FF)
                    {
                        sb.Append(nextC);
                        sb.Append(vowel);
                        i = nextIdx;
                        continue;
                    }
                }
                sb.Append(vowel);
                continue;
            }

            // Standard character mapping
            if (SingleCharMap.TryGetValue(c, out var mapped))
            {
                sb.Append(mapped);
            }
            else
            {
                sb.Append(c);
            }
        }

        var res = sb.ToString();

        // 3. Fix compound vowel signs:
        //    ে + া = ো (O-kar)
        //    ে + ৌ = ৌ (OU-kar)
        res = res.Replace("ো", "ো")
                 .Replace("ে া", "ো")
                 .Replace("ৌ", "ৌ")
                 .Replace("ে ৗ", "ৌ")
                 .Replace("্য া", "্যা")
                 .Replace("্যv", "্যা")
                 .Replace("্যv", "্যা")
                 .Replace("্য া", "্যা")
                 .Replace("্যা", "্যা");

        // 4. Clean up duplicate hasantas or orphan characters
        res = Regex.Replace(res, @"্+", "্");

        return res.Trim();
    }
}
