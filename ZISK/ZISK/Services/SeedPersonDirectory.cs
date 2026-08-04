namespace ZISK.Services;

/// <summary>
/// Fictional contact details for the seeded demo accounts, keyed by e-mail.
/// <para>
/// Everything in here is invented. The phone numbers use the Slovak mobile format but are not
/// assigned to anyone, the addresses are plausible Žilina-region streets, and the birth numbers
/// are generated so they are structurally correct yet deliberately fail the modulo-11 check that
/// every real Slovak birth number issued after 1953 satisfies. The demo banner says as much to
/// visitors; this comment is the same statement for anyone reading the source.
/// </para>
/// <para>
/// Members of one household intentionally share an address, so the parent/child links in the demo
/// look like a real club register rather than randomly generated rows.
/// </para>
/// </summary>
internal static class SeedPersonDirectory
{
    /// <param name="Serial">
    /// The four digits after the slash in the birth number. Unique per person and spaced ten apart,
    /// which is what lets <see cref="BuildRodneCislo"/> nudge a value by one without ever colliding
    /// with somebody else's number.
    /// </param>
    internal sealed record SeedPerson(string Phone, string Address, bool IsFemale, int Serial);

    // Household addresses, named so it is obvious which people are meant to live together.
    private const string NovakHome  = "Rosinská 14, 010 08 Žilina";
    private const string HorakHome  = "Framborská 27, 010 01 Žilina";
    private const string BlahaHome  = "Veľká Okružná 44, 010 01 Žilina";
    private const string KralHome   = "Hlinská 6, 010 01 Žilina";
    private const string SimonHome  = "Bratislavská 18, 010 01 Žilina";
    private const string BalogHome  = "Školská 12, 013 01 Teplička nad Váhom";
    private const string OravecHome = "Hlavná 33, 013 03 Varín";
    private const string MalyHome   = "Komenského 41, 010 01 Žilina";
    private const string VargaHome  = "Nanterská 15, 010 08 Žilina";
    private const string CiernyHome = "Slnečné námestie 7, 010 15 Žilina";
    private const string HolubHome  = "Sadová 5, 014 01 Bytča";

    private static readonly Dictionary<string, SeedPerson> ByEmail = new(StringComparer.OrdinalIgnoreCase)
    {
        // Základné účty
        ["admin@zisk.sk"]                = new("+421 905 214 336", "Vysokoškolákov 8, 010 08 Žilina", false, 1001),
        ["trener@zisk.sk"]               = new("+421 903 447 121", "Hálkova 22, 010 01 Žilina",       false, 1011),
        ["rodic@zisk.sk"]                = new("+421 907 512 908", NovakHome,                          false, 1021),
        ["dieta@zisk.sk"]                = new("+421 948 330 517", NovakHome,                          false, 1031),

        // Tréneri
        ["rastislav.horvath@zisk.sk"]    = new("+421 905 661 042", "Predmestská 51, 010 01 Žilina",   false, 1041),
        ["tomas.balaz@zisk.sk"]          = new("+421 918 274 630", "Kysucká 9, 010 01 Žilina",        false, 1051),
        ["jan.minac@zisk.sk"]            = new("+421 949 118 725", "Nám. A. Hlinku 3, 015 01 Rajec",  false, 1061),

        // Rodičia
        ["jana.novakova@zisk.sk"]        = new("+421 911 604 238", NovakHome,  true,  1071),
        ["milan.horak@zisk.sk"]          = new("+421 902 375 419", HorakHome,  false, 1081),
        ["andrea.blahova@zisk.sk"]       = new("+421 940 223 861", BlahaHome,  true,  1091),
        ["lukas.kral@zisk.sk"]           = new("+421 908 517 302", KralHome,   false, 1101),
        ["monika.simonova@zisk.sk"]      = new("+421 917 845 260", SimonHome,  true,  1111),
        ["juraj.balog@zisk.sk"]          = new("+421 903 926 174", BalogHome,  false, 1121),
        ["zuzana.oravec@zisk.sk"]        = new("+421 950 341 687", OravecHome, true,  1131),
        ["eva.mala@zisk.sk"]             = new("+421 905 738 210", MalyHome,   true,  1141),
        ["ivan.varga@zisk.sk"]           = new("+421 907 260 493", VargaHome,  false, 1151),
        ["marta.cierna@zisk.sk"]         = new("+421 944 512 078", CiernyHome, true,  1161),
        ["vladimir.holub@zisk.sk"]       = new("+421 918 603 945", HolubHome,  false, 1171),

        // Športovci
        ["lukas.maly@zisk.sk"]           = new("+421 949 802 316", MalyHome,   false, 1181),
        ["martin.horak@zisk.sk"]         = new("+421 902 148 570", HorakHome,  false, 1191),
        ["jakub.blaha@zisk.sk"]          = new("+421 911 375 924", BlahaHome,  false, 1201),
        ["adam.kral@zisk.sk"]            = new("+421 908 264 731", KralHome,   false, 1211),
        ["michal.simon@zisk.sk"]         = new("+421 917 590 148", SimonHome,  false, 1221),
        ["juraj.balog.jr@zisk.sk"]       = new("+421 903 471 265", BalogHome,  false, 1231),
        ["richard.varga@zisk.sk"]        = new("+421 940 837 512", VargaHome,  false, 1241),

        // Deti
        ["petra.horakova@zisk.sk"]       = new("+421 902 619 483", HorakHome,  true,  1251),
        ["klara.oravec@zisk.sk"]         = new("+421 950 726 184", OravecHome, true,  1261),
        ["filip.cerny@zisk.sk"]          = new("+421 944 108 357", CiernyHome, false, 1271),
        ["zuzana.kralova@zisk.sk"]       = new("+421 908 935 240", KralHome,   true,  1281),
        ["samuel.novak@zisk.sk"]         = new("+421 948 217 609", NovakHome,  false, 1291),
        ["ema.holubova@zisk.sk"]         = new("+421 918 462 073", HolubHome,  true,  1301),
        ["ondrej.maly@zisk.sk"]          = new("+421 949 350 861", MalyHome,   false, 1311),
        ["nina.blahova@zisk.sk"]         = new("+421 911 084 596", BlahaHome,  true,  1321)
    };

    /// <summary>
    /// Returns the demo details for a seeded account, or null for any other account. The null case
    /// is what keeps a real production admin (and anything a visitor creates) free of made-up data.
    /// </summary>
    public static SeedPerson? Find(string email) =>
        ByEmail.GetValueOrDefault(email);

    /// <summary>
    /// Builds a birth number in the YYMMDD/NNNN shape the app validates against, with the +50 month
    /// offset Slovak birth numbers use for women. The modulo-11 check is deliberately broken: when a
    /// serial happens to produce a number that would pass it, the serial is bumped by one so it does
    /// not. The result therefore reads as a birth number in the UI but cannot be mistaken for a real
    /// person's identifier.
    /// </summary>
    public static string BuildRodneCislo(DateOnly dateOfBirth, bool isFemale, int serial)
    {
        var month = dateOfBirth.Month + (isFemale ? 50 : 0);
        var prefix = $"{dateOfBirth.Year % 100:D2}{month:D2}{dateOfBirth.Day:D2}";

        if (long.Parse(prefix + serial.ToString("D4")) % 11 == 0)
            serial += 1;

        return $"{prefix}/{serial:D4}";
    }
}
