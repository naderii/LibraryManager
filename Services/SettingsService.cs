using LibraryManager.Data;
using LibraryManager.Models;

namespace LibraryManager.Services;

public static class SettingsService
{
    static Dictionary<string, string> cache = new();

    public static void Seed()
    {
        using var d = new LibraryDbContext();
        var defs = new Dictionary<string, string>
        {
            { "LibraryName", "کتابخانه من" },
            { "MembershipFeeAmount", "20000" },
            { "FineAmountPerDay", "500" },
            { "LoanPeriodDays", "7" },
            { "MaxActiveLoansPerMember", "2" },
            { "LastBackupDate", "" }
        };
        foreach (var x in defs)
            if (!d.Settings.Any(s => s.Key == x.Key))
                d.Settings.Add(new Setting { Key = x.Key, Value = x.Value });
        d.SaveChanges();
        cache.Clear();
    }

    public static string Get(string key, string fallback = "0")
    {
        if (cache.TryGetValue(key, out var v)) return v;
        using var d = new LibraryDbContext();
        v = d.Settings.FirstOrDefault(x => x.Key == key)?.Value ?? fallback;
        cache[key] = v;
        return v;
    }

    public static string LibraryName => Get("LibraryName", "کتابخانه من");
    public static decimal Fee => decimal.Parse(Get("MembershipFeeAmount", "20000"));
    public static decimal FinePerDay => decimal.Parse(Get("FineAmountPerDay", "500"));
    public static int LoanDays => int.Parse(Get("LoanPeriodDays", "7"));
    public static int MaxLoans => int.Parse(Get("MaxActiveLoansPerMember", "2"));

    public static void Save(string key, string value)
    {
        using var d = new LibraryDbContext();
        var s = d.Settings.FirstOrDefault(x => x.Key == key);
        if (s == null) d.Settings.Add(new Setting { Key = key, Value = value });
        else s.Value = value;
        d.SaveChanges();
        cache[key] = value;
    }
}
