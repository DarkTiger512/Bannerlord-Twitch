using BLTAdoptAHero.Util;
using Newtonsoft.Json;

static class EnchantmentTests
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Enchantment: " + message);
    }

    static void Throws(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        catch (InvalidOperationException) { return; }
        throw new Exception("Expected enchantment validation failure.");
    }

    public static void Run()
    {
        int[] costs = { 100000, 200000, 300000, 400000, 500000 };
        int[] failures = { 0, 15, 20, 25, 30 };
        int[] gains = { 5, 2, 5 };
        EnchantmentPolicy.Validate(costs, failures, gains);
        Throws(() => EnchantmentPolicy.Validate(new[] { -1, 2, 3, 4, 5 }, failures, gains));
        Throws(() => EnchantmentPolicy.Validate(costs, new[] { 1, 15, 20, 25, 30 }, gains));
        Throws(() => EnchantmentPolicy.Validate(costs, new[] { 0, 101, 20, 25, 30 }, gains));
        Throws(() => EnchantmentPolicy.Validate(costs, failures, new[] { 0, 2, 5 }));
        Check(EnchantmentPolicy.TryParse(null, out var index, out var stat) && index == 0 && stat == null, "list");
        Check(EnchantmentPolicy.TryParse(" #2 DAMAGE ", out index, out stat) && index == 2 && stat == EnchantmentStat.Damage, "purchase parsing");
        Check(EnchantmentPolicy.TryParse("3", out index, out stat) && index == 3 && stat == null, "preview parsing");
        foreach (var args in new[] { "0", "-1", "##2", "2 0", "2 armor", "2 damage extra", "999999999999999" })
            Check(!EnchantmentPolicy.TryParse(args, out _, out _), "reject " + args);

        var history = EnchantmentPolicy.Roll(null, EnchantmentStat.Damage, 5, 100, 0, out _, out var success);
        Check(success && history.Count == 1, "legacy/null history and guaranteed +1");
        var saved = JsonConvert.DeserializeObject<List<EnchantmentEntry>>(JsonConvert.SerializeObject(history));
        Check(saved[0].Gain == 5 && saved[0].Stat == EnchantmentStat.Damage, "history round-trip");
        var failed = EnchantmentPolicy.Roll(saved, EnchantmentStat.Speed, 100, 15, 14, out var change, out success);
        Check(!success && failed.Count == 0 && change.Gain == 5 && change.Stat == EnchantmentStat.Damage,
            "failure reverses historical stat/gain rather than current selection/config");
        Check(saved.Count == 1, "roll must not mutate saved history");
        history = EnchantmentPolicy.Roll(failed, EnchantmentStat.Speed, 2, 100, 0, out _, out success);
        Check(success && history.Count == 1, "return to +0 guarantees +1 again");
        history = EnchantmentPolicy.Roll(history, EnchantmentStat.Damage, 5, 15, 15, out _, out success);
        Check(success && history.Count == 2, "exact failure boundary succeeds");
        for (int level = 2; level < 5; level++)
        {
            Check(EnchantmentPolicy.AttemptError(level, costs[level], costs[level], false, false, false) == null, "exact funds");
            history = EnchantmentPolicy.Roll(history, EnchantmentStat.Damage, 5, failures[level], 99, out _, out success);
            Check(success && history.Count == level + 1, "level transition");
        }
        Throws(() => EnchantmentPolicy.Roll(history, EnchantmentStat.Damage, 5, 0, 99, out _, out _));
        Check(EnchantmentPolicy.AttemptError(5, 999999, 1, false, false, false) != null, "cap");
        Check(EnchantmentPolicy.AttemptError(0, 99999, 100000, false, false, false) != null, "insufficient funds");
        Check(EnchantmentPolicy.AttemptError(0, 100000, 100000, true, false, false) != null, "mission");
        Check(EnchantmentPolicy.AttemptError(0, 100000, 100000, false, true, false) != null, "prisoner");
        Check(EnchantmentPolicy.AttemptError(0, 100000, 100000, false, false, true) != null, "auction");

        int damage = 40, gold = 200000, spent = 7;
        Throws(() => EnchantmentPolicy.Commit(() => { damage = 45; throw new InvalidOperationException(); },
            () => gold -= 100000, () => damage = 40, () => { gold = 200000; spent = 7; }));
        Check(damage == 40 && gold == 200000 && spent == 7, "partial item application rollback");
        Throws(() => EnchantmentPolicy.Commit(() => damage = 45,
            () => { gold -= 100000; spent += 100000; throw new InvalidOperationException(); },
            () => damage = 40, () => { gold = 200000; spent = 7; }));
        Check(damage == 40 && gold == 200000 && spent == 7, "charge rollback");
        EnchantmentPolicy.Commit(() => damage = 35, () => { gold -= 100000; spent += 100000; }, () => damage = 40, () => gold = 200000);
        Check(damage == 35 && gold == 100000 && spent == 100007, "legitimate downgrade charged once");
        Console.WriteLine("Enchantment policy tests passed.");
    }
}
