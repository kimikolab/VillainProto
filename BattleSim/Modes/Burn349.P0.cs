using BattleCore;
using static Common;
using I = Inv348Diag;

// burn349 §3-2 —— ポンの台の与ダメと守りの出どころ（台本を読む・盤面は1ビットも動かさない）。
static partial class Burn349Diag
{
    /// <summary>与ダメ（敵の HP を減らした一撃・`Damage` の行為者つき）の区分。</summary>
    internal static readonly string[] HitCats = { "焼き尽くす（本体）", "火の雨", "ホタの段 ／ 臨界", "残り火", "ボルグの爆炎", "ボルグのほか", "ムド", "ほかの駒" };
    /// <summary>被弾の燃焼（敵が受けた・`Status` 燃焼の行為者 ＝ 一撃の主）が乗った一撃の区分（名目）。</summary>
    internal static readonly string[] HitBurnCats = { "火の雨", "焼き尽くす（本体）", "ホタのほか", "ボルグの爆炎", "ボルグのほか", "ムドの連撃", "そのほか" };

    internal sealed class Src
    {
        public long N;
        public readonly long[] Hit = new long[8], HitBurn = new long[7], HitBurnN = new long[7];
        public long Tick, Burnouts, Blazes;
        // 守り
        public long ArmorSaved, WardSaved, BorgMend, HotaMend, BorgFeed, HiyoConvHealed;
        public readonly Dictionary<string, long> ConvTo = new(), Healed = new();
        public void Merge(Src o)
        {
            N += o.N; Tick += o.Tick; Burnouts += o.Burnouts; Blazes += o.Blazes;
            for (int i = 0; i < Hit.Length; i++) Hit[i] += o.Hit[i];
            for (int i = 0; i < HitBurn.Length; i++) { HitBurn[i] += o.HitBurn[i]; HitBurnN[i] += o.HitBurnN[i]; }
            ArmorSaved += o.ArmorSaved; WardSaved += o.WardSaved; BorgMend += o.BorgMend; HotaMend += o.HotaMend; BorgFeed += o.BorgFeed; HiyoConvHealed += o.HiyoConvHealed;
            foreach (var (k, v) in o.ConvTo) ConvTo[k] = ConvTo.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Healed) Healed[k] = Healed.GetValueOrDefault(k) + v;
        }
    }

    enum Mode { None, Stage, Burnout, Rain, Embers, Blaze }

    internal static Src Attribute(BattleResult r, List<UnitState> p, List<UnitState> e)
    {
        var s = new Src { N = 1 };
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team is int tm && tm != BattleContext.PlayerTeam) foes.Add(sid);
        int? Id(string id) => p.FirstOrDefault(u => u.Def.Id == id)?.InstanceId;
        int? hota = Id("hota"), borg = Id("borg"), hiyo = Id("hiyo"), mudo = Id("mudo");
        var names = p.ToDictionary(u => u.InstanceId, u => Short(u.Def));
        var starts = new Dictionary<int, List<int>>();
        foreach (var h in r.Hands) { if (!starts.TryGetValue(h.EventStart, out var l)) starts[h.EventStart] = l = new List<int>(); l.Add(h.ActorId); }
        var mode = new Dictionary<int, Mode>();
        Mode M(int? a) => a is int i && mode.TryGetValue(i, out var m) ? m : Mode.None;
        var ev = r.Events;
        for (int i = 0; i < ev.Count; i++)
        {
            if (starts.TryGetValue(i, out var st)) foreach (var a in st) mode[a] = Mode.None;
            var x = ev[i];
            if (x.Kind == BattleEventKind.FireLevel && x.ActorId is int fa)
            {
                if (fa == hota && x.TargetId == hota)
                {
                    if (x.Text == FireLevelLabels.Burnout) { mode[fa] = Mode.Burnout; s.Burnouts++; }
                    else if (x.Text is FireLevelLabels.Embers or FireLevelLabels.EmbersHit) mode[fa] = Mode.Embers;
                    else if (x.Text is FireLevelLabels.Stage or FireLevelLabels.Lance or FireLevelLabels.Critical) mode[fa] = Mode.Stage;
                }
                if (fa == hota && x.Text == FireLevelLabels.Rain) mode[fa] = Mode.Rain;
                if (fa == borg && x.Text is FireLevelLabels.Blaze or FireLevelLabels.BlazeSolo or FireLevelLabels.Unleash) { if (M(fa) != Mode.Blaze) s.Blazes++; mode[fa] = Mode.Blaze; }
                continue;
            }
            if (x.Kind == BattleEventKind.Damage && x.TargetId is int t && foes.Contains(t) && !x.Relayed)
            {
                if (x.ActorId is not int a) continue;   // 刻みは `Status` の側で数える
                int c = a == hota ? M(a) switch { Mode.Burnout => 0, Mode.Rain => 1, Mode.Embers => 3, _ => 2 }
                      : a == borg ? (M(a) == Mode.Blaze ? 4 : 5) : a == mudo ? 6 : 7;
                s.Hit[c] += x.Amount;
                continue;
            }
            if (x.Kind == BattleEventKind.Status && x.Text == "燃焼" && x.TargetId is int bt && foes.Contains(bt))
            {
                if (x.ActorId is not int a) { s.Tick += x.Amount; continue; }
                int c = a == hota ? M(a) switch { Mode.Rain => 0, Mode.Burnout => 1, _ => 2 }
                      : a == borg ? (M(a) == Mode.Blaze ? 3 : 4) : a == mudo ? 5 : 6;
                s.HitBurn[c] += x.Amount; s.HitBurnN[c]++;
                continue;
            }
            if (x.Kind == BattleEventKind.FireArmor && x.Text == FireArmorLabels.Convert && x.TargetId is int ct && names.TryGetValue(ct, out var cn))
                s.ConvTo[cn] = s.ConvTo.GetValueOrDefault(cn) + x.Amount;
        }
        long Xv(string id, Func<UnitTally, long> f) => r.TallyByUnit.TryGetValue(id, out var t) ? f(t) : 0;
        s.ArmorSaved = Xv("borg", t => t.FireArmorSaved); s.WardSaved = Xv("borg", t => t.FireWardSaved);
        s.BorgMend = Xv("borg", t => t.FireMendHealed); s.HotaMend = Xv("hota", t => t.FireMendHealed); s.BorgFeed = Xv("borg", t => t.FireFeedHealed);
        s.HiyoConvHealed = Xv("hiyo", t => t.FireConvTickHealed + t.FireConvSplashHealed);
        foreach (var u in p) if (r.TallyByUnit.TryGetValue(u.Def.Id, out var t)) s.Healed[Short(u.Def)] = s.Healed.GetValueOrDefault(Short(u.Def)) + t.Healed;
        return s;
    }

    internal static (I.St S, Src A) MeasureSrc(Formation f, I.W w, int n = Seeds)
    {
        var parts = new (I.St, Src)[n];
        Parallel.For(0, n, i => { var (r, p, e) = I.Raw(f, w, i, verbose: true); parts[i] = (I.Take(r, p, w), Attribute(r, p, e)); });
        var st = new I.St(); var a = new Src();
        foreach (var (x, y) in parts) { st.Add(x); a.Merge(y); }
        return (st, a);
    }

    internal static void PrintSrc(string title, IEnumerable<(string Name, I.St S, Src A)> rows)
    {
        var list = rows.ToList();
        Console.WriteLine($"#### {title} —— 与ダメの出どころ（敵の HP を減らした量 ／戦）");
        Console.WriteLine();
        Console.WriteLine("| 列 | 勝率・T | " + string.Join(" | ", HitCats) + " | 燃焼の刻み（名目） | 焼き尽くす ／ 爆炎（回） |");
        Console.WriteLine("|---|---|" + string.Concat(HitCats.Select(_ => "--:|")) + "--:|---|");
        foreach (var (n, s, a) in list)
            Console.WriteLine($"| {n} | {F1(s.Win)}・{s.WinTs}T | " + string.Join(" | ", a.Hit.Select(v => Per(v, a.N))) + $" | {Per(a.Tick, a.N)} | {Per2(a.Burnouts, a.N)} ／ {Per2(a.Blazes, a.N)} |");
        Console.WriteLine();
        Console.WriteLine($"#### {title} —— 被弾の燃焼（敵が受けた・名目 ／戦・（回））が乗った一撃");
        Console.WriteLine();
        Console.WriteLine("| 列 | 計 | " + string.Join(" | ", HitBurnCats) + " |");
        Console.WriteLine("|---|--:|" + string.Concat(HitBurnCats.Select(_ => "---|")));
        foreach (var (n, _, a) in list)
            Console.WriteLine($"| {n} | {Per(a.HitBurn.Sum(), a.N)} | " + string.Join(" | ", Enumerable.Range(0, HitBurnCats.Length).Select(i => $"{Per(a.HitBurn[i], a.N)}（{Per2(a.HitBurnN[i], a.N)}）")) + " |");
        Console.WriteLine();
        Console.WriteLine($"#### {title} —— 守りの出どころ（量 ／戦）");
        Console.WriteLine();
        Console.WriteLine("火の鎧 ＝ ボルグ自身の半減で切った量 ／ 盾の配り ＝ 燃えている味方の半減で切った量（保持者ボルグの側）／ 火の癒し ＝ 実際に戻った HP ／ 火の変換 ＝ ヒヨが癒した HP（受け手ごとは名目）／ 回復 計 ＝ 駒ごとの `Healed`（どこから来たかを問わない）。");
        Console.WriteLine();
        Console.WriteLine("| 列 | 倒れた ／戦 | 火の鎧 | 盾の配り | 火の癒し ボルグ ／ ホタ | 焼き返し | 火の変換（癒えた）・受け手ごとの名目 | 回復 計（駒ごと） |");
        Console.WriteLine("|---|--:|--:|--:|---|--:|---|---|");
        foreach (var (n, s, a) in list)
            Console.WriteLine($"| {n} | {Per2(s.AllyFell, s.N)} | {Per(a.ArmorSaved, a.N)} | {Per(a.WardSaved, a.N)} | {Per(a.BorgMend, a.N)} ／ {Per(a.HotaMend, a.N)} | {Per(a.BorgFeed, a.N)} | "
                + $"{Per(a.HiyoConvHealed, a.N)}・{string.Join("、", a.ConvTo.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, a.N)}"))} | {string.Join("、", a.Healed.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, a.N)}"))} |");
        Console.WriteLine();
    }

    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        I.W[] ws = new[] { I.Boss, I.Guard, I.Bat }.Concat(I.StrongWaves).ToArray();
        Console.WriteLine($"# {Tag} §3-2 Phase 0 —— {PonRow}（{Seats(Pon)}）の与ダメと守りの出どころ（規定・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("与ダメは `Damage` の出来事（行為者つき・中継の段は除く）、被弾の燃焼と刻みは `Status`（燃焼）の名目。区分はホタ ／ ボルグのその手番の見出し（焼き尽くす ／ 火の雨 ／ 残り火 ／ 段 ／ 爆炎）で切る。");
        Console.WriteLine();
        var rows = ws.Select(w => { var (s, a) = MeasureSrc(Pon, w); return (w.Name, s, a); }).ToList();
        PrintSrc("ポンの台（規定）", rows);
        Console.WriteLine("駒ごとの倒れた割合 ／ 生きていた T:");
        Console.WriteLine();
        var ids = Pon.Occupied().Select(o => o.Def).ToArray();
        Console.WriteLine("| 波 | " + string.Join(" | ", ids.Select(Short)) + " |");
        Console.WriteLine("|---|" + string.Concat(ids.Select(_ => "--:|")));
        foreach (var (n, s, _) in rows)
            Console.WriteLine($"| {n} | " + string.Join(" | ", ids.Select(d => $"{Pct(s.Deaths.GetValueOrDefault(d.Id), s.N)}% ／ {Per(s.AliveT.GetValueOrDefault(d.Id), s.N)}T")) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
