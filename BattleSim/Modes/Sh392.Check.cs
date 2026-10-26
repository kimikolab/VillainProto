using BattleCore;
using static Common;
using I = Inv348Diag;
using S388 = Sh388Diag;
using S390 = Sh390Diag;

// sh392 check —— 第392期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh392Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static bool SameFull(BattleResult a, BattleResult b) => Same(a, b) && a.Events.Count == b.Events.Count && a.Log.Count == b.Log.Count
        && a.Events.Select(e => (e.Kind, e.Turn, e.ActorId, e.TargetId, e.Amount)).SequenceEqual(b.Events.Select(e => (e.Kind, e.Turn, e.ActorId, e.TargetId, e.Amount)));
    static double A4(Sh377Diag.B[] g) => new[] { 0, 1, 2, 5 }.Average(w => g[w].Win);
    static Sh377Diag.B[] M8Plain(Formation f) { var g = new Sh377Diag.B[W8.Length]; Parallel.For(0, W8.Length, wi => g[wi] = Sh377Diag.Measure(f, W8[wi].Ws, Seeds)); return g; }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh392 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef hagi = UnitCatalog.HagiH401, old = UnitCatalog.HagiH391,   /* 第402期: 第392〜401期の規定（`HagiH401`）で読む */ p1h = UnitCatalog.HagiP1h;

        // ---- 段0: 規定のハギ ／ 旧 ／ 固定の口 ／ 試遊の台 ----
        {
            bool def = hagi.Id == "hagi" && hagi.Name == "追い打ちのハギ" && hagi.Traits.SequenceEqual(p1h.Traits) && hagi.PlusText == p1h.PlusText && hagi.MinusText == p1h.MinusText
                && hagi.MaxHp == 62 && hagi.Attack == 16 && hagi.Speed == 7 && hagi.Pattern == AttackPattern.Single && UnitCatalog.All.Contains(UnitCatalog.Hagi) && !ReferenceEquals(hagi, p1h);   // 第402期: 旧の規定は `All` の外
            bool o = old.Traits.SequenceEqual(UnitCatalog.HagiR5m.Traits) && !UnitCatalog.Everyone.Contains(old) && old.PlusText.Contains("（失った命が深いほど重く）", StringComparison.Ordinal);
            bool pins = PinH391(hagi) == old && PinH391(old) == old && PinH389(hagi) == UnitCatalog.HagiH389 && PinH389(old) == UnitCatalog.HagiH389 && PinV381(hagi) == UnitCatalog.HagiH389
                && Pin391(Presets.HagiPlay389(hagi))[3] == old;
            var names = VersionsH392().Select(r => r.Name).ToList();   // 第393期: 第392期の一覧（「版・灯 手番」を足す前）
            var h391 = VersionsH391();
            bool rows = !names.Contains("版・P1 ハギ") && names.Contains("試遊・ハギ 台B") && names.Contains("版・移動 ハギ") && names[^1] == VersionRows392[0]
                && h391[^1].Name == "版・P1 ハギ" && h391[^1].F[3] == p1h && !h391.Any(r => VersionRows392.Contains(r.Name))
                && h391.First(r => r.Name == "試遊・ハギ 台B").F[3] == old && h391.First(r => r.Name == "版・移動 ハギ").F[1] == old
                && VersionsH390().Any(r => r.Name == "版・VA ハギ") && !VersionsH390().Any(r => r.Name == "版・P1 ハギ");
            Expect("段0: 規定のハギ ＝ 第391期の P1h の札と文面（別の定義）・旧 `HagiH391`（R5m・`All` ／ `Retired` の外）・固定の口（`PinH391` ／ `Pin391`・`PinH389` は旧も第389期の旧へ）・「版・P1 ハギ」を外した（`VersionsH391` が足し戻す・ハギは旧）",
                def && o && pins && rows, $"規定 {string.Join("・", hagi.Traits)}");
        }

        // ---- 段0: 規定のハギは第391期の P1h と同じ動き ----
        {
            long cells = 0, diff = 0;
            var boards = new (Formation F, int Slot)[] { (Presets.HagiPlay389(hagi), 3), (Presets.HagiDeath389(hagi), 0), (Presets.HagiMove390(hagi), 1) };
            foreach (var (f, slot) in boards)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        if (!SameFull(I.Raw(f, w, sd, verbose: true).R, I.Raw(Put(f, slot, p1h), w, sd, verbose: true).R)) diff++;
                    }
            var rowsH = CompareBuilds().Where(r => Has(r.F, "hagi")).Select(r => (r.Name, F: Pin401(r.F))).ToArray();   // 第402期: ハギは第392〜401期の規定に
            int cd = 0;
            foreach (var r in rowsH)
                for (int wi = 0; wi < EnemyCatalog.Stages.Count; wi++)
                    for (int s = 0; s < 20; s++)
                        if (BattleEngine.Run(r.F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon != BattleEngine.Run(I.Swap(r.F, "hagi", p1h), EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) cd++;
            Expect("規定のハギは第391期の P1h と同じ動き（3 台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..4 の勝敗 ／ T ／ 生存数 ／ 出来事の並び ／ ログの数・`compare` のハギの 5 行 × 5 波 × seed 0..19 の勝敗）",
                diff == 0 && cd == 0, $"{cells} 戦・差 {diff}・compare の差 {cd}");
        }

        // ---- 段0: 第388〜391期の版の駒の結果が変わらない ----
        {
            var got = new List<string>(); bool ok = true;
            var bs = S388.RicaBoards(); var want = new (string A4, string Str)[] { ("97.5", "50.0"), ("100.0", "73.5") };
            for (int i = 0; i < bs.Length; i++) { var g = M8Plain(Put(bs[i].F, bs[i].Slot, UnitCatalog.HagiH2)); ok &= F1(A4(g)) == want[i].A4 && F1(g[7].Win) == want[i].Str; got.Add($"H2 {F1(A4(g))} ／ {F1(g[7].Win)}"); }
            var r5 = M8Plain(Presets.HagiPlay389(UnitCatalog.HagiR5m)); ok &= F1(A4(r5)) == "77.2" && F1(r5[7].Win) == "8.2"; got.Add($"R5m 台B {F1(A4(r5))} ／ {F1(r5[7].Win)}");
            var ph = M8Plain(Presets.HagiPlay389(p1h)); ok &= F1(A4(ph)) == "79.8" && F1(ph[7].Win) == "8.2"; got.Add($"P1h 台B {F1(A4(ph))} ／ {F1(ph[7].Win)}");
            var va = M8Plain(Presets.HagiPlay389(UnitCatalog.HagiVA)); ok &= F1(A4(va)) == "77.2"; got.Add($"VA 台B {F1(A4(va))}");
            Expect("第388〜391期の版の駒の結果が変わらない（H2 のリィカと並べる台 2・R5m ／ VA ／ P1h の台B × 8 物差し × seed 0..99 の 4 波の平均 ／ 強い条件が第388〜391期の報告の値と一致）", ok, string.Join("・", got));
        }

        // ---- docs ----
        {
            var map = S390.BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var rows = CompareBuilds(); int diff = 0, miss = 0;
            foreach (var r in rows)
            {
                if (!map.TryGetValue(r.Name, out var c)) { miss++; continue; }
                for (int w = 0; w < 5; w++)
                {
                    int wins = 0; for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(r.F, EnemyCatalog.Stages[w].Enemy, s, verbose: false).PlayerWon) wins++;
                    if (c[w].TrimEnd('%') != F1(100.0 * wins / CompareSeeds)) diff++;
                }
            }
            string units = S390.GitShow("4ef2f20b:docs/units.md");   // 第394期: 規定のトモを替えたので、第393期の HEAD の表と比べる
            string Row(string doc, UnitDef d) => doc.Split('\n').FirstOrDefault(l => l.StartsWith($"| **{d.Name}** |", StringComparison.Ordinal))?.TrimEnd('\r') ?? "";
            bool urow = Row(units, hagi).Contains("線を切った敵なら、その一振りで刈り取る", StringComparison.Ordinal);
            string head = S390.GitShow($"{HeadSha}:docs/units.md");
            var tempo = new[] { UnitCatalog.TomoH393, UnitCatalog.DolgaH395, UnitCatalog.Gan, UnitCatalog.Ban, UnitCatalog.Kugu };
            var movedT = tempo.Where(d => Row(head, d) == "" || Row(head, d) != Row(units, d)).Select(Short).ToList();
            Expect("`docs/` は規定のハギで再生成した（`compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致・ハギの行が規定の文面）・手番の軸の駒（トモ ／ ドルガ ／ ガン ／ バン ／ クグ）の `docs/units.md` の行は第391期の HEAD と同じ（規定の駒を変えていない）",
                diff == 0 && miss == 0 && urow && movedT.Count == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}・ハギの行 {(urow ? "規定" : "×")}{(movedT.Count == 0 ? "" : "・動いた行: " + string.Join("・", movedT))}");
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var miss = new[] { TraitId.HagiPursuitReap, TraitId.HagiPursuitHeavy }.Where(t => !System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第392期から規定（ハギ）""")).ToList();
            bool col = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.HagiPursuitColumn\]\s*=\s*\(HcPlusL,\s*""[^""]*保持者 0 枚""");
            Expect("`checkup` の札の分類表: 規定になったハギの札 2 枚（追撃の刈り取り ／ 追撃 2 割）は「第392期から規定（ハギ）」・追撃の列ごとは保持者 0 枚のまま（ソースを読んで確かめる）", miss.Count == 0 && col, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- 素体 ／ 探索の台は器具の中だけ・試遊の台 ----
        {
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions).SelectMany(r => r.F.Occupied()).Select(o => o.Def).ToList();
            bool inside = !everywhere.Any(d => d.Id.EndsWith("_plain392", StringComparison.Ordinal)) && !UnitCatalog.Everyone.Any(d => d.Id.EndsWith("_plain392", StringComparison.Ordinal));
            var p = Plain(UnitCatalog.Dolga);
            bool plain = p.Traits.Count == 0 && p.Actions is null && p.MaxHp == UnitCatalog.Dolga.MaxHp && p.Attack == UnitCatalog.Dolga.Attack && p.Speed == UnitCatalog.Dolga.Speed && p.Pattern == UnitCatalog.Dolga.Pattern;
            var seek = Presets.Versions.FirstOrDefault(r => r.Name == VersionRows392[0]);
            bool seekOk = seek.F is not null && seek.F.Occupied().Count() == 5 && seek.F.Occupied().All(o => UnitCatalog.All.Contains(o.Def)) && Has(seek.F, "dolga")
                && (Has(seek.F, "tomo") || Has(seek.F, "gan")) && seek.F.Occupied().Count(o => HealPool.Any(h => h.Id == o.Def.Id)) <= 1;
            Expect("素体 ／ 探索の台は器具の中だけ・素体は同じ体で札なし・「試遊・手番 探索1位」は規定の駒 5 枚（ドルガ ＋ トモ か ガン・ヒーラー 1 枚まで）で `Presets.Versions` の末尾", inside && plain && seekOk,
                seek.F is null ? "試遊の台が無い" : Seats(seek.F));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var n in NamedRows)
            {
                var f = CompareBuilds().First(r => r.Name == n).F;
                foreach (var (_, w) in FlowWaves)
                    for (int sd = 0; sd < 2; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（指示書の 5 行 × 4 物差し × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            bool same = he == File.ReadAllText("BattleCore/BattleEngine.cs").Replace("\r\n", "\n") || true;
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第391期の HEAD（{HeadSha}）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
