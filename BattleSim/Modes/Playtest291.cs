using BattleCore;
using static Common;

// =====================================================================================
// playtest291 —— 第291期「試遊の準備（感電軸・標軸）」。指示書は design/PHASE291_PLAYTEST_PREP_SPEC.md ／ 報告は design/PHASE291_PLAYTEST_PREP.md。
//
//     dotnet run --project BattleSim -c Release 0 playtest291 rates       # Phase 0 §6-3: 試遊の5台 × ボス規定形 ／ 近衛 ／ 大隊 × seed 0..49 の勝率（目安）
//     dotnet run --project BattleSim -c Release 0 playtest291 events      # 段2: 試遊の5台 × 試遊の波 × seed 0..49 の新しい種類の件数（札ごと）
//     dotnet run --project BattleSim -c Release 0 playtest291 memo <行名の一部> <boss|guard|bat> <seed> [上限]   # 段3: 台本の並びの例（新しい種類と前後）
//     dotnet run --project BattleSim -c Release 0 playtest291 check       # 自己検査
//
// 駒・札・数値・波は1つも変えない（器具だけ）。試遊の波と行は BattleCore の `EnemyCatalog.PlaytestStages` ／ `Presets.Playtest` を引くだけで写しを持たない。
// =====================================================================================
static class Playtest291Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "rates";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "rates": Rates(); return;
            case "events": Events(); return;
            case "memo": Memo(A(3, "糸"), A(4, "guard"), int.Parse(A(5, "0")), int.Parse(A(6, "60"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("playtest291: モードは rates / events / memo / check。"); return;
        }
    }

    static EnemyCatalog.PlaytestStage WaveOf(string n) => EnemyCatalog.PlaytestStages[n switch { "boss" => 0, "guard" => 1, "bat" => 2, _ => int.Parse(n) }];
    /// <summary>第296期: 試遊の行が8行になった——この器具は第291期の5行（先頭の5行）だけを読む（ヒサは `Make` の `Pin294` で旧の規定に固定される）。</summary>
    static (string Name, Formation F)[] Rows291 => Presets.Playtest.Take(5).Select(r => (r.Name, FvSwap(r.F, UnitCatalog.Doha, UnitCatalog.DohaD0))).ToArray();   // 第298期: ドハは旧（`DohaD0`）に固定
    static (List<UnitState> P, List<UnitState> E) Make(Formation f, EnemyCatalog.PlaytestStage w)
        => (BattleEngine.Materialize(Kugu292Diag.Pin294(FvSwap(f, UnitCatalog.Kata, UnitCatalog.KataKRb)), BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(w.Enemy, w.Scale));   // 第293期: カタは第291期の規定（KR-b）に固定・第294期: クグ ／ シガも（KG-b ／ SI-b）

    /// <summary>第291期に足した表示専用の種類。</summary>
    static readonly BattleEventKind[] NewKinds = { BattleEventKind.ShockGauge, BattleEventKind.Feather, BattleEventKind.Scar, BattleEventKind.MarkLayer };

    // ---------------------------------------------------------------------------------
    // Phase 0 §6-3: 試遊の目安
    // ---------------------------------------------------------------------------------
    static void Rates()
    {
        const int seeds = 50;
        var rows = Rows291;
        var waves = EnemyCatalog.PlaytestStages;
        Console.WriteLine("# 第291期 試遊の目安 —— 試遊の5台 × 試遊の波（seed 0..49・規定の駒）");
        Console.WriteLine();
        Console.WriteLine("勝率（倒しT ＝ 勝った戦の平均の決着ターン ／ 負けT ＝ 負けた戦の平均）。ボスはほぼ乱数を引かない（第283期 R378）——100% は1本の筋書きに近い。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 席（前1・前3・中央・後1・後3） | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "--:|")));
        foreach (var (name, f) in rows)
        {
            var cells = new string[waves.Count];
            for (int wi = 0; wi < waves.Count; wi++)
            {
                int wins = 0; long wt = 0, lt = 0;
                var res = new (bool W, int T)[seeds];
                Parallel.For(0, seeds, s =>
                {
                    var (p, e) = Make(f, waves[wi]);
                    var r = BattleEngine.Run(p, e, s, verbose: false);
                    res[s] = (r.PlayerWon, r.Turns);
                });
                foreach (var (w, t) in res) { if (w) { wins++; wt += t; } else lt += t; }
                cells[wi] = $"{100.0 * wins / seeds:F0}%（{(wins == 0 ? "—" : (wt / (double)wins).ToString("F1"))} ／ {(wins == seeds ? "—" : (lt / (double)(seeds - wins)).ToString("F1"))}）";
            }
            string seats = string.Join("・", Enumerable.Range(0, 5).Select(i => f[i]?.Name ?? "—"));
            Console.WriteLine($"| {name} | {seats} | {string.Join(" | ", cells)} |");
        }
    }

    // ---------------------------------------------------------------------------------
    // 段2: 新しい種類の件数
    // ---------------------------------------------------------------------------------
    static void Events()
    {
        const int seeds = 50;
        Console.WriteLine("# 第291期 表示専用の種類の件数 —— 試遊の5台 × 試遊の波（seed 0..49・1戦あたり）");
        Console.WriteLine();
        var labels = new SortedSet<string>(StringComparer.Ordinal);
        var table = new Dictionary<(string Row, string Wave), Dictionary<string, long>>();
        foreach (var (name, f) in Rows291)
            foreach (var w in EnemyCatalog.PlaytestStages)
            {
                var d = new Dictionary<string, long>();
                for (int s = 0; s < seeds; s++)
                {
                    var (p, e) = Make(f, w);
                    var r = BattleEngine.Run(p, e, s, verbose: true);
                    foreach (var ev in r.Events)
                    {
                        string? key = KeyOf(r, ev);
                        if (key is null) continue;
                        d[key] = d.GetValueOrDefault(key) + 1;
                        labels.Add(key);
                    }
                }
                table[(name, w.Name)] = d;
            }
        var cols = labels.ToArray();
        Console.WriteLine("| 行 | 波 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|---|" + string.Concat(cols.Select(_ => "--:|")));
        foreach (var ((row, wave), d) in table)
            Console.WriteLine($"| {row} | {wave} | " + string.Join(" | ", cols.Select(c => d.TryGetValue(c, out long n) ? (n / (double)seeds).ToString("F2") : "")) + " |");
    }

    /// <summary>件数の列の名前（新しい種類 ＝ 種類・札 ／ 既存の種類に足した印 ＝ 「粉・主目標」など）。それ以外は null。</summary>
    static string? KeyOf(BattleResult r, BattleEvent ev)
    {
        if (ev.Kind == BattleEventKind.ShockGauge) return "感電 " + ev.Text;
        if (ev.Kind == BattleEventKind.Feather) return "羽 " + ev.Text;
        if (ev.Kind == BattleEventKind.Scar) return "爪痕";
        if (ev.Kind == BattleEventKind.MarkLayer) return "標の層";
        if (ev.Kind == BattleEventKind.Discharge && ev.SourceTrait == TraitId.Thread) return ev.Text == ThreadLabels.Release ? "糸・ほどけ" : ev.PartnerId is null ? "糸 ①" : "糸 ②";
        if (ev.Kind == BattleEventKind.StatusGain && ev.Text == StatusKeys.Shock && ev.PowderRoute is { } pr) return "粉・" + pr;
        return null;
    }

    // ---------------------------------------------------------------------------------
    // 段3: 台本の並びの例
    // ---------------------------------------------------------------------------------
    static void Memo(string rowPart, string wave, int seed, int limit)
    {
        var (name, f) = Rows291.First(r => r.Name.Contains(rowPart, StringComparison.Ordinal));
        var w = WaveOf(wave);
        var (p, e) = Make(f, w);
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Name);
        foreach (var ev in r.Events.Where(x => x.Kind == BattleEventKind.Summon && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, $"#{ev.TargetId}");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# playtest291 memo —— {name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("新しい種類（と印を足した既存の種類）の前後1件ずつを出す。`…` は省いた区間。");
        Console.WriteLine();
        Console.WriteLine("| # | T | 種類 | Text | Actor | Target | Amount | Slot | Partner | Reaction | 他 |");
        Console.WriteLine("|--:|--:|---|---|---|---|--:|--:|---|---|---|");
        var evs = r.Events;
        var show = new SortedSet<int>();
        for (int i = 0; i < evs.Count; i++)
            if (KeyOf(r, evs[i]) is not null) { show.Add(i); if (i > 0) show.Add(i - 1); if (i + 1 < evs.Count) show.Add(i + 1); }
        int last = -1, shown = 0;
        foreach (int i in show)
        {
            if (shown >= limit) { Console.WriteLine($"| … | | （以下 {show.Count - shown} 件は省略） | | | | | | | | |"); break; }
            if (last >= 0 && i != last + 1) Console.WriteLine("| … | | | | | | | | | | |");
            var x = evs[i];
            string other = string.Join(" ", new[]
            {
                x.SourceTrait is { } st ? $"src={st}" : null, x.SpreadFromId is int sp ? $"spread={N(sp)}" : null,
                x.FriendlyFire ? "ff" : null, x.PowderRoute is { } pr ? $"粉={pr}" : null, x.StatusRemaining is int sr ? $"rem={sr}" : null,
                x.Kind is BattleEventKind.Damage or BattleEventKind.Scar ? $"hp={x.HpAfter}" : null,
            }.Where(s => s is not null));
            Console.WriteLine($"| {i} | {x.Turn} | {(KeyOf(r, x) is null ? x.Kind.ToString() : "**" + x.Kind + "**")} | {x.Text} | {N(x.ActorId)} | {N(x.TargetId)} | {x.Amount} | {x.Slot} | {N(x.PartnerId)} | {(x.Reaction ? "○" : "")} | {other} |");
            last = i; shown++;
        }
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fails;
    static void Expect(string label, bool ok, string detail = "")
    {
        if (!ok) _fails++;
        Console.WriteLine($"- {(ok ? "○" : "×")} {label}{(detail.Length > 0 ? $"（{detail}）" : "")}");
    }

    static void Check()
    {
        _fails = 0;
        Console.WriteLine("# 第291期 自己検査（playtest291 check）");
        Console.WriteLine();
        // (a) 規定のクグ ＝ KG-b
        Expect("(a) 第291期の規定のクグ ＝ KG-b（組み付き ＋ 糸 ＋ 導線）・`KuguKGb`（第294期から規定は KW-a）・旧の規定 `KuguKG0` は組み付きだけ（`All` ／ `Retired` に入っていない）・文面",
            // 第294期: 規定は KW-a になった——第291期の規定（KG-b）は `KuguKGb` に固定してあることを確かめる
            UnitCatalog.KuguKGb.Traits.SequenceEqual(new[] { TraitId.Grapple, TraitId.Thread, TraitId.ThreadCharge }) && UnitCatalog.Kugu.Traits.SequenceEqual(UnitCatalog.KuguKGb.Traits.Append(TraitId.WebCharge))
            && UnitCatalog.KuguKG0.Traits.SequenceEqual(new[] { TraitId.Grapple }) && !UnitCatalog.Everyone.Contains(UnitCatalog.KuguKG0) && !UnitCatalog.Everyone.Contains(UnitCatalog.KuguKGb)
            && UnitCatalog.KuguKG0.MaxHp == UnitCatalog.KuguKGb.MaxHp && UnitCatalog.KuguKG0.Attack == UnitCatalog.KuguKGb.Attack && UnitCatalog.KuguKG0.Speed == UnitCatalog.KuguKGb.Speed
            && UnitCatalog.KuguKGb.PlusText == UnitCatalog.KuguKG0.PlusText + "。組み付いた敵とは糸で繋がっている。自分に流れ込む電気は、糸を伝ってその敵へ流れ、浴びた敵は帯電する");
        // (b) 試遊の波
        var ps = EnemyCatalog.PlaytestStages;
        static bool SameWave(EnemyWave a, EnemyWave b) => Enumerable.Range(0, 9).All(i => ReferenceEquals(a[i], b[i]));
        Expect("(b) 試遊の波 ＝ ボス規定形（素の値）・近衛 ／ 大隊（`elite` と同じ定義・HP 1000% ／ 攻 300%）・`TestStages` は3本 ／ `Stages` は5本のまま",
            ps.Count == 3 && SameWave(ps[0].Enemy, EnemyCatalog.BossRegularWave) && ps[0].Scale == EnemyScaleRule.None
            && ReferenceEquals(ps[1].Enemy, EliteDiag.Five) && ReferenceEquals(ps[2].Enemy, EliteDiag.Nine) && ps[1].Scale == EliteDiag.Elite && ps[2].Scale == EliteDiag.Elite
            && EliteDiag.Elite == new EnemyScaleRule(1000, 300) && SameWave(EliteDiag.Five, EnemyCatalog.TestStages[2].Enemy) && SameWave(EliteDiag.Nine, EnemyCatalog.TestStages[1].Enemy)
            && EnemyCatalog.TestStages.Count == 3 && EnemyCatalog.Stages.Count == 5);
        // (c) 試遊の行
        var pl = Rows291;
        var compareNames = Presets.Compare.Select(r => r.Name).ToHashSet();
        Expect("(c) 試遊の行は5行・`Compare`（64 行）／ `Cross`（12 行）に入っていない・駒はすべて規定（`All`）・5枠が埋まっている・`試遊・標 道中` は `見境改 (ミサ×薙ぎ)` と同じ台",
            pl.Length == 5 && Presets.Compare.Length == 64 && Presets.Cross.Length == 12 && pl.All(r => !compareNames.Contains(r.Name) && !Presets.Cross.Any(c => c.Name == r.Name))
            && Presets.Playtest.Take(5).All(r => r.F.Occupied().Count() == 5 && r.F.Occupied().All(o => UnitCatalog.All.Contains(o.Def)))   // 第298期: 固定（`DohaD0`）の前の行で見る
            && pl[4].F.Occupied().SequenceEqual(Presets.Compare.First(c => c.Name == "見境改 (ミサ×薙ぎ)").F.Occupied()));

        // (d)〜(h) 台本: 試遊の台 × 試遊の波 ＋ `compare` 64 行 × 本編5波（seed は下）
        var battles = new List<(string Name, Func<(List<UnitState>, List<UnitState>)> Make, int Seed)>();
        foreach (var (name, f) in pl) foreach (var w in ps) for (int s = 0; s < 20; s++) { var ff = f; var ww = w; battles.Add(($"{name}×{w.Name}", () => Make(ff, ww), s)); }
        foreach (var (name, f) in Presets.Compare) for (int wi = 0; wi < EnemyCatalog.Stages.Count; wi++) for (int s = 0; s < 4; s++)
                { var ff = Kugu292Diag.Pin294(FvSwap(f, UnitCatalog.Kata, UnitCatalog.KataKRb));   /* 第293期: カタは第291期の規定（KR-b）に固定・第294期: クグ ／ シガも */ int wj = wi; battles.Add(($"{name}×第{wi + 1}波", () => (BattleEngine.Materialize(ff, BattleContext.PlayerTeam), BattleEngine.Materialize(EnemyCatalog.Stages[wj].Enemy, BattleContext.EnemyTeam, EnemyScaleRule.Default)), s)); }
        long diff = 0, holderLess = 0, interruptBad = 0, volleyBad = 0, scarBad = 0, layerBad = 0, powderBad = 0, threadBad = 0, gaugeBad = 0;
        var kindsSeen = new Dictionary<string, long>();
        var lockObj = new object();
        Parallel.For(0, battles.Count, bi =>
        {
            var b = battles[bi];
            var (p0, e0) = b.Make();
            var r0 = BattleEngine.Run(p0, e0, b.Seed, verbose: false);
            var (p, e) = b.Make();
            var r = BattleEngine.Run(p, e, b.Seed, verbose: true);
            long d = 0, hl = 0, ib = 0, vb = 0, sb = 0, lb = 0, pb = 0, tb = 0, gb = 0;
            // (d) 表示専用: verbose の有無で勝敗・決着T・生存者の HP が同じ
            if (r0.PlayerWon != r.PlayerWon || r0.Turns != r.Turns || !p0.Select(u => u.Hp).SequenceEqual(p.Select(u => u.Hp)) || !e0.Select(u => u.Hp).SequenceEqual(e.Select(u => u.Hp))) d++;
            // (e) 保持者がいない戦では1件も出ない
            var all = p.Concat(e).ToList();
            bool Has(TraitId t) => all.Any(u => u.Def.Traits.Contains(t) || u.Traits.Any(x => x.Id == t));
            var byId = all.ToDictionary(u => u.InstanceId);
            var evs = r.Events;
            for (int i = 0; i < evs.Count; i++)
            {
                var x = evs[i];
                if (KeyOf(r, x) is string key) lock (lockObj) kindsSeen[key] = kindsSeen.GetValueOrDefault(key) + 1;
                switch (x.Kind)
                {
                    case BattleEventKind.ShockGauge:
                        bool holder = x.Text switch
                        {
                            ShockGaugeLabels.ChargeGain or ShockGaugeLabels.ChargeSpent or ShockGaugeLabels.ChargeDrained => Has(TraitId.StoredCharge),
                            ShockGaugeLabels.Cloud or ShockGaugeLabels.CloudStrike => Has(TraitId.Thundercloud),
                            ShockGaugeLabels.Interrupt => Has(TraitId.ShockWhipFlurry) || Has(TraitId.ShockWhipBolt),
                            ShockGaugeLabels.Cower => Has(TraitId.Scourge),
                            _ => false,
                        };
                        if (!holder) hl++;
                        if (x.Text is ShockGaugeLabels.ChargeGain && !(x.Amount == x.Slot + 1 && x.Amount <= StoredChargeTrait.Cap)) gb++;
                        if (x.Text is ShockGaugeLabels.ChargeSpent && x.Amount != x.Slot - 1) gb++;
                        if (x.Text is ShockGaugeLabels.ChargeDrained && x.Amount != 0) gb++;
                        if (x.Text is ShockGaugeLabels.Cloud && !(x.Amount > x.Slot && x.Amount <= ThundercloudTrait.KeepCap)) gb++;
                        // 割り込みの見出しの次の `Attack` はシガの手番外の一振り（Reaction）で、的は見出しの Target
                        if (x.Text is ShockGaugeLabels.Interrupt)
                        {
                            var nextAtk = evs.Skip(i + 1).FirstOrDefault(y => y.Kind == BattleEventKind.Attack);
                            if (nextAtk is null || nextAtk.ActorId != x.ActorId || !nextAtk.Reaction || nextAtk.TargetId != x.TargetId) ib++;
                        }
                        break;
                    case BattleEventKind.Feather:
                        if (!Has(TraitId.Feathers)) hl++;
                        if (x.Text is FeatherLabels.Volley)
                        {
                            int shots = 0;
                            for (int j = i + 1; j < evs.Count; j++)
                            {
                                var y = evs[j];
                                if (y.Kind == BattleEventKind.Feather && y.ActorId == x.ActorId)
                                {
                                    if (y.Text is FeatherLabels.Volley) break;
                                    if (y.Text is FeatherLabels.Chase or FeatherLabels.Flow or FeatherLabels.Spray) { shots++; if (y.Slot != shots || y.Amount != x.Amount) vb++; }
                                }
                                if (y.Kind == BattleEventKind.TurnStart) break;
                            }
                            if (shots == 0 || shots > x.Amount) vb++;
                        }
                        break;
                    case BattleEventKind.Scar:
                        if (!Has(TraitId.RuptureScar)) hl++;
                        if (x.Amount <= 0 || x.Slot < 1) sb++;
                        break;
                    case BattleEventKind.MarkLayer:
                        if (!Has(TraitId.Rupture)) hl++;
                        if (x.Amount != x.Slot + 1) lb++;
                        break;
                    case BattleEventKind.StatusGain when x.PowderRoute is { } route:
                        if (!Has(TraitId.ChargedPowder) || x.Text != StatusKeys.Shock) pb++;
                        else if (x.ActorId is not int tid || !byId.TryGetValue(tid, out var tou) || x.TargetId is not int gid || !byId.TryGetValue(gid, out var got)) pb++;
                        else if (route == PowderRoute.Leak ? got.TeamId != tou.TeamId || !x.FriendlyFire : got.TeamId == tou.TeamId || x.FriendlyFire) pb++;
                        else if (route == PowderRoute.Spread && x.SpreadFromId is null) pb++;
                        break;
                    case BattleEventKind.Discharge when x.SourceTrait == TraitId.Thread:
                        if (!Has(TraitId.Thread)) tb++;
                        break;
                }
            }
            if (d + hl + ib + vb + sb + lb + pb + tb + gb > 0)
                lock (lockObj) { diff += d; holderLess += hl; interruptBad += ib; volleyBad += vb; scarBad += sb; layerBad += lb; powderBad += pb; threadBad += tb; gaugeBad += gb; }
        });
        Expect("(d) 表示専用: verbose の有無で勝敗・決着T・全員の HP が同じ", diff == 0, $"{battles.Count} 戦中 {diff} 戦");
        Expect("(e) 保持者のいない戦では新しい種類が1件も出ない（蓄電 ＝ 蓄電の札・雷雲 ＝ 雷雲の札・割り込み ＝ 振りまくる ／ 雷霆の割り込み・怖気 ＝ 責め苦・羽 ＝ 羽・爪痕 ＝ 爪痕・標の層 ＝ 炸裂・粉の印 ＝ 粉・糸 ＝ 糸）",
            holderLess == 0 && powderBad == 0 && threadBad == 0, $"{holderLess} ／ 粉 {powderBad} ／ 糸 {threadBad}");
        Expect("(f) 蓄電 ／ 雷雲の値の向き（増えた ＝ 前 ＋ 1・上限以下 ／ 使った ＝ 前 − 1 ／ 使い果たした ＝ 0 ／ 雷雲 ＝ 前より多く上限 8 以下）", gaugeBad == 0, $"{gaugeBad} 件");
        Expect("(g) 割り込みの見出しの次の `Attack` はシガの手番外（`Reaction`）で的が見出しと同じ", interruptBad == 0, $"{interruptBad} 件");
        Expect("(h) 連射の見出しの後の1発ごとの札（追う ／ 流れた ／ 乱射）が 1, 2, … と並び、数が連射の発数以下", volleyBad == 0, $"{volleyBad} 件");
        Expect("(i) 爪痕は減った量 > 0・新しい最大HP ≧ 1 ／ 標の層は 新しい層 ＝ 前の層 ＋ 1", scarBad == 0 && layerBad == 0, $"{scarBad} ／ {layerBad}");
        string[] want = { "感電 " + ShockGaugeLabels.ChargeGain, "感電 " + ShockGaugeLabels.ChargeSpent, "感電 " + ShockGaugeLabels.Cloud, "感電 " + ShockGaugeLabels.CloudStrike,
                          "感電 " + ShockGaugeLabels.Interrupt, "羽 " + FeatherLabels.Gain, "羽 " + FeatherLabels.Volley, "羽 " + FeatherLabels.Chase, "爪痕", "標の層", "糸 ②", "粉・" + PowderRoute.Main, "粉・" + PowderRoute.Spread };
        var missing = want.Where(k => !kindsSeen.ContainsKey(k)).ToArray();
        Expect("(j) 試遊の台で主な札が実際に出る（空の札が無い）", missing.Length == 0, missing.Length == 0 ? string.Join(" ／ ", kindsSeen.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}")) : "出ない: " + string.Join("・", missing));
        // (k) 新しく足した口は乱数を引かない（BattleContext の Emit 口の本体に Roll ／ PickOne が無い）
        string en = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "BattleCore", "BattleEngine.cs"));
        int j0 = en.IndexOf("// 第291期 —— 表示専用の口", StringComparison.Ordinal), j1 = en.IndexOf("// 第291期 —— 表示専用の口（ここまで）", StringComparison.Ordinal);
        string body = j0 >= 0 && j1 > j0 ? en[j0..j1] : "";
        Expect("(k) 表示専用の口（`EmitShockGauge` ほか）は乱数を引かない・盤面を書かない（`Roll` ／ `PickOne` ／ `SetCounter` ／ `Hp =` が無い）", body.Length > 0 && !body.Contains("Roll(") && !body.Contains("PickOne") && !body.Contains("SetCounter") && !body.Contains("Hp ="), $"{body.Length} 字");
        Console.WriteLine();
        Console.WriteLine(_fails == 0 ? "すべて ○" : $"× が {_fails} 件");
        if (_fails > 0) Environment.ExitCode = 1;
    }
}
