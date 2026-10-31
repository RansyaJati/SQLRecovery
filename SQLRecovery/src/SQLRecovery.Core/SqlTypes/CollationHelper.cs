using System.Text;

namespace SQLRecovery.Core.SqlTypes;

/// <summary>
/// Maps SQL Server collation ids to .NET encodings so that legacy (non-Unicode) varchar/char
/// data is decoded with the right code page.
/// </summary>
public static class CollationHelper
{
    private static bool _providerRegistered;

    private static readonly Dictionary<int, int> SortIdToCodePage = new()
    {
        // SQL collations encode a sort id in bits 20-27.
        [30] = 437, [31] = 437, [32] = 437, [33] = 437, [34] = 437,
        [40] = 850, [41] = 850, [42] = 850, [43] = 850, [44] = 850,
        [49] = 1252, [50] = 1252, [51] = 1252, [52] = 1252, [53] = 1252, [54] = 1252,
        [55] = 850, [56] = 850, [57] = 850, [58] = 850, [59] = 850, [60] = 850, [61] = 850,
        [80] = 1250, [81] = 1250, [82] = 1250, [83] = 1250, [84] = 1250, [85] = 1250,
        [86] = 1250, [87] = 1250, [88] = 1250, [89] = 1250, [90] = 1250, [91] = 1250,
        [92] = 1250, [93] = 1250, [94] = 1250, [95] = 1250, [96] = 1250,
        [104] = 1251, [105] = 1251, [106] = 1251, [107] = 1251, [108] = 1251,
        [112] = 1253, [113] = 1253, [114] = 1253, [115] = 1253, [116] = 1253,
        [117] = 1253, [118] = 1253, [119] = 1253, [120] = 1253, [121] = 1253,
        [122] = 1253, [123] = 1253, [124] = 1253,
        [128] = 1254, [129] = 1254, [130] = 1254,
        [136] = 1255, [137] = 1255, [138] = 1255,
        [144] = 1256, [145] = 1256, [146] = 1256,
        [152] = 1257, [153] = 1257, [154] = 1257, [155] = 1257, [156] = 1257,
        [157] = 1257, [158] = 1257, [159] = 1257, [160] = 1257,
        [183] = 1252, [184] = 1252, [185] = 1252, [186] = 1252
    };

    private static readonly Dictionary<int, int> LcidToCodePage = new()
    {
        [0x0409] = 1252, // en-US
        [0x0809] = 1252, // en-GB
        [0x0407] = 1252, // de-DE
        [0x040C] = 1252, // fr-FR
        [0x0410] = 1252, // it-IT
        [0x0413] = 1252, // nl-NL
        [0x0416] = 1252, // pt-BR
        [0x0C0A] = 1252, // es-ES modern
        [0x040A] = 1252, // es-ES traditional
        [0x0415] = 1250, // pl-PL
        [0x0405] = 1250, // cs-CZ
        [0x040E] = 1250, // hu-HU
        [0x0418] = 1250, // ro-RO
        [0x0419] = 1251, // ru-RU
        [0x0422] = 1251, // uk-UA
        [0x0408] = 1253, // el-GR
        [0x041F] = 1254, // tr-TR
        [0x040D] = 1255, // he-IL
        [0x0401] = 1256, // ar-SA
        [0x0425] = 1257, // et-EE
        [0x0414] = 1252, // nb-NO
        [0x0406] = 1252, // da-DK
        [0x041D] = 1252, // sv-SE
        [0x040B] = 1252, // fi-FI
        [0x0411] = 932,  // ja-JP
        [0x0804] = 936,  // zh-CN
        [0x0404] = 950,  // zh-TW
        [0x0412] = 949,  // ko-KR
        [0x041E] = 874,  // th-TH
        [0x042A] = 1258  // vi-VN
    };

    public static Encoding GetEncoding(int collationId)
    {
        EnsureProvider();

        int codePage = ResolveCodePage(collationId);

        try
        {
            return Encoding.GetEncoding(codePage);
        }
        catch (NotSupportedException)
        {
            return Encoding.Latin1;
        }
    }

    private static int ResolveCodePage(int collationId)
    {
        if (collationId == 0)
            return 1252;

        // Bit 28: 1 = Windows collation, 0 = SQL collation.
        bool isWindowsCollation = (collationId & 0x10000000) != 0;

        if (isWindowsCollation)
        {
            int lcid = collationId & 0xFFFFF;
            return LcidToCodePage.TryGetValue(lcid, out var cp) ? cp : 1252;
        }

        int sortId = (collationId >> 20) & 0xFF;
        return SortIdToCodePage.TryGetValue(sortId, out var sortCp) ? sortCp : 1252;
    }

    private static void EnsureProvider()
    {
        if (_providerRegistered)
            return;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _providerRegistered = true;
    }
}
