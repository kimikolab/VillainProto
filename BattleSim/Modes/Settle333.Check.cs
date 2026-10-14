using BattleCore;
using static Common;
using T323 = Tou323Diag;

// settle333 の自己検査（第333期）。光の雨は `SparkTrait.Rain` を直に呼ぶ（`veil332 check` と同じ盤）。
static partial class Settle333Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Pal = new()
    {
        Id = "pal333", Name = "木偶の味方", MaxHp = 100, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly UnitDef Dummy = new()
    {
        Id = "dummy333", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# settle333 check —— 第333期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef S = UnitCatalog.Som, O = UnitCatalog.SomH332;

        // ---- 定義と固定の口
        {
            bool som = S.Traits.SequenceEqual(O.Traits.Append(TraitId.SparkVeilCap)) && S.Traits.SequenceEqual(UnitCatalog.SomLVc332.Traits) && S.PlusText == UnitCatalog.SomLVc332.PlusText
                && S.PlusText.Contains(UnitCatalog.SomVeilCapClause, StringComparison.Ordinal) && S.MinusText == O.MinusText && S.Id == O.Id && S.MaxHp == O.MaxHp && S.Attack == O.Attack && S.Speed == O.Speed
                && O.Traits.Contains(TraitId.SparkVeil) && !O.Traits.Contains(TraitId.SparkVeilCap) && O.Traits.SequenceEqual(UnitCatalog.SomH319.Traits.Append(TraitId.BeastThrowBackfire));
            bool vers = UnitCatalog.SomLVc332.Traits.SequenceEqual(O.Traits.Append(TraitId.SparkVeilCap)) && UnitCatalog.SomLVd332.Traits.SequenceEqual(O.Traits.Append(TraitId.SparkVeilTemp))
                && UnitCatalog.SomBc.Traits.SequenceEqual(O.Traits.Append(TraitId.BeastChargeSpread)) && UnitCatalog.SomLVc332.Traits.Count(t => t == TraitId.SparkVeilCap) == 1;
            bool outside = new[] { O, UnitCatalog.SomLVc332, UnitCatalog.SomLVd332, UnitCatalog.SomBc }.All(d => !UnitCatalog.Everyone.Contains(d)) && UnitCatalog.All.Contains(S) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.All(d => (d == S || !d.Traits.Contains(TraitId.SparkVeilCap)) && !d.Traits.Contains(TraitId.SparkVeilTemp));
            Expect("定義: 規定のソム ＝ 第320〜332期の規定（`SomH332`・LV-a）＋ `SparkVeilCap`（札と文面は第332期の `SomLVc332` と同じ）・第332期の版 ／ 隣帯電は旧から（札は1枚ずつ）・旧と版は `Everyone` の外・`All` 52 枚",
                som && vers && outside, $"ソム {som}・版 {vers}・外 {outside}");
            bool pin = ReferenceEquals(PinS332(S), O) && ReferenceEquals(Pin332(Formation.Build(back3: S))[4], O)
                && new Func<UnitDef, UnitDef>[] { PinTK331, PinT328, PinTK327, PinT326, PinTKK324 }.All(p => ReferenceEquals(p(S), O))
                && ReferenceEquals(PinS319(O), UnitCatalog.SomH319) && ReferenceEquals(PinSS318(O), UnitCatalog.SomH318) && ReferenceEquals(PinS317(O), UnitCatalog.SomH317) && IsSomNow(O)
                && ReferenceEquals(Veil332Diag.Vers[0].Som, O) && Veil332Diag.SomRows().SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, S))
                && CompareBuildsH332().Concat(PlaytestH332).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, S));
            Expect("固定の口: `PinS332` ／ `Pin332` は規定のソムを `SomH332` へ・第325〜332期の口（`PinTK331` ／ `PinT328` ／ `PinTK327` ／ `PinT326`）と古い口の終端（`PinTKK324`）も・`PinS319` ／ `PinSS318` ／ `PinS317` ／ `IsSomNow` は `SomH332` も受ける・`veil332` の「規定」は `SomH332`", pin);
        }

        // ---- 規定のソムで衣が最大HPの4割まで（板と合算）・超えた分は捨てる・旧では上限なく積もる
        foreach (var (som, tag, cap) in new[] { (S, "規定（LV-c）", true), (O, "旧（SomH332・LV-a）", false) })
        {
            var ctx = T323.Ctx(Formation.Build(front1: Pal, back3: som), Formation.Build(front1: Dummy), out var p, out var e);
            var a = T323.Seat(p, 0); var s = T323.Seat(p, 4);
            a.SetCounter(StatusKeys.Armor, 30);   // ほかの書き手の破片（板など）
            SparkTrait.Rain(ctx, s, 10);           // 光 70 が満タンの味方に降る → 溢れ 70
            int armor1 = a.RawCounter(StatusKeys.Armor), veil1 = a.RawCounter(SparkTrait.VeilKey);
            SparkTrait.Rain(ctx, s, 10);
            int armor2 = a.RawCounter(StatusKeys.Armor);
            bool ok = cap ? armor1 == 40 && veil1 == 10 && armor2 == 40 && T323.Tal(ctx, "som").SparkVeilCapped > 0 : armor1 == 100 && veil1 == 70 && armor2 == 170 && T323.Tal(ctx, "som").SparkVeilCapped == 0;
            Expect($"{tag}: 破片 30 の満タンの味方（最大HP 100）に光 70 が2回降ると{(cap ? "破片は 40（最大HPの4割・板と合算）で止まり、超えた分は捨てる" : "上限なく積もる")}", ok, $"破片 {armor1} → {armor2}・衣 {veil1}・捨てた {T323.Tal(ctx, "som").SparkVeilCapped}");
        }

        // ---- 試遊の台
        {
            var v = Presets.Versions;
            var rows = Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).Concat(v).ToArray();
            // 第334期: `Versions` は期ごとに「版・」の台を入れ替えるので、1行目（ポンの台・規定の駒）だけを見る（後ろの行は「版・」で始まること）。見出しの文は第333期のまま。
            bool ok = v.Length >= 1 && v[0].Name == "試遊・感電 ポンの台" && v.Skip(1).All(r => r.Name.StartsWith("版・", StringComparison.Ordinal)) && v[0].F.Occupied().Select(o => o.Def).SequenceEqual(new[] { UnitCatalog.Kata, UnitCatalog.Shiga, UnitCatalog.Tou, UnitCatalog.Kugu, S })
                && Presets.Playtest.Length == 17 && rows.First(r => r.Name.Contains(v[0].Name, StringComparison.Ordinal)).Name == v[0].Name;
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」1台（規定の駒・ソムは LV-c）・第332期の「版・LV」の2台は外した・`Playtest` は 17 台のまま", ok);
        }

        // ---- 戦の中で: verbose の有無で勝敗 ／ 決着T が同じ（代表台 7 × ボス ／ 近衛 ／ 大隊 × seed 0..9・規定）
        {
            int diff = 0, n = 0;
            foreach (var (_, f) in Boards())
                foreach (var w in new[] { WaveOf("boss"), WaveOf("guard"), WaveOf("bat") })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        n++;
                        if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                    }
            Expect($"代表台 7 × ボス ／ 近衛 ／ 大隊 × seed 0..9（{n} 戦・規定）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 365881a:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第332期（365881a）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
