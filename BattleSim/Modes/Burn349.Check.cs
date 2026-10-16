using BattleCore;
using static Common;
using I = Inv348Diag;

// burn349 の自己検査（第349期・指示書 §9）。戦の台本と帳簿で版の規則を確かめる（盤面は動かさない）。
static partial class Burn349Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static (BattleResult R, List<UnitState> P, List<UnitState> E) Fight(Ver v, I.W w, int seed, bool verbose = true) => I.Raw(Apply(Pon, v), w, seed, verbose: verbose);
    static int Inst(List<UnitState> p, string id) => p.First(u => u.Def.Id == id).InstanceId;

    /// <summary>火の変換の見出しの直後の回復（同じ受け手）を名目と突き合わせる: 回数と、回復が名目の半分（切り捨て）を超えた回数。</summary>
    static void ConvPairs(BattleResult r, ref int over, ref int pairs)
    {
        var ev = r.Events;
        for (int i = 0; i + 1 < ev.Count; i++)
        {
            var x = ev[i];
            if (x.Kind != BattleEventKind.FireArmor || x.Text != FireArmorLabels.Convert) continue;
            var y = ev[i + 1];
            if (y.Kind != BattleEventKind.Heal || y.TargetId != x.TargetId) continue;
            pairs++;
            if (y.Amount > x.Amount / 2) over++;
        }
    }

    static void Check()
    {
        Console.WriteLine($"# {Tag} burn349 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        Ver B = V("base"), Fv = V("f"), G = V("g"), A = V("a"), D = V("d"), FD = V("fd");
        I.W[] ws = { I.Boss, I.Guard, I.Bat, I.StrongWaves[0], I.StrongWaves[1], I.StrongWaves[3] };

        // ---- 段0 ----
        {
            var (r, _, _) = I.Raw(Pon, I.Boss, 7);
            Expect("段0: 試遊・燃焼 ポンの台（ムド・ボルグ・ホタ・ガルド・ヒヨ）× ボス × seed 7 が 4ターン・生存5体", r.PlayerWon && r.Turns == 4 && r.PlayerSurvivors == 5, $"{(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
            bool def = Pon.Occupied().All(o => ReferenceEquals(o.Def, UnitCatalog.Mudo) || ReferenceEquals(o.Def, UnitCatalog.Borg) || ReferenceEquals(o.Def, UnitCatalog.Hota) || ReferenceEquals(o.Def, UnitCatalog.Gald) || ReferenceEquals(o.Def, UnitCatalog.Hiyo))
                && Seats(Pon) == "ムド・ボルグ・ホタ・ガルド・ヒヨ" && !CompareBuilds().Any(c => c.Name == PonRow) && !Presets.Playtest.Any(c => c.Name == PonRow);
            Expect("段0: ポンの台は規定の駒・`Presets.Versions` に置き、`compare` ／ `Playtest` には入れない", def, Seats(Pon));
        }
        // ---- 版の定義 ----
        {
            bool f = UnitCatalog.BorgBNf.Traits.Contains(TraitId.FireArmor) && UnitCatalog.BorgBNf.Traits.Contains(TraitId.FireArmorNoGuard) && !UnitCatalog.BorgBNf.Traits.Contains(TraitId.FireWardAll)
                && UnitCatalog.HiyoBNhalf.Traits.Contains(TraitId.FireConvertHalf) && !UnitCatalog.HiyoBNhalf.Traits.Contains(TraitId.FireConvert)
                && Array.IndexOf(UnitCatalog.HiyoBNhalf.Traits.ToArray(), TraitId.FireConvertHalf) == Array.IndexOf(UnitCatalog.Hiyo.Traits.ToArray(), TraitId.FireConvert);
            bool g = UnitCatalog.BorgBNg.Traits.Contains(TraitId.FireArmor) && !UnitCatalog.BorgBNg.Traits.Contains(TraitId.FireArmorNoGuard) && !UnitCatalog.BorgBNg.Traits.Contains(TraitId.FireWardAll);
            bool a = UnitCatalog.BorgBNa.Traits.Contains(TraitId.BurnHitNoMend) && UnitCatalog.HotaBNa.Traits.SequenceEqual(UnitCatalog.Hota.Traits) && UnitCatalog.HiyoBNa.Traits.SequenceEqual(UnitCatalog.Hiyo.Traits);
            bool d = UnitCatalog.BorgBNd.Traits.SequenceEqual(UnitCatalog.Borg.Traits.Append(TraitId.BurnHitSplitOnce)) && UnitCatalog.BorgBNfd.Traits.SequenceEqual(UnitCatalog.BorgBNf.Traits.Append(TraitId.BurnHitSplitOnce));
            bool def = !UnitCatalog.Borg.Traits.Any(t => t is TraitId.FireArmorNoGuard or TraitId.BurnHitNoMend or TraitId.BurnHitSplitOnce) && UnitCatalog.Hiyo.Traits.Contains(TraitId.FireConvert)
                && !UnitCatalog.All.Concat(UnitCatalog.Retired).Any(u => u.Traits.Any(t => t is TraitId.FireArmorNoGuard or TraitId.BurnHitNoMend));
            Expect("版の定義: BN-f ＝ 火の鎧は残して半減だけ外す（`FireArmorNoGuard`）＋ 盾の配りを外す ＋ ヒヨの変換を半分（同じ席）／ BN-g ＝ 盾の配りだけ外す ／ BN-a ＝ `BurnHitNoMend`（ホタ ／ ヒヨは文面だけ）／ BN-d ＝ `BurnHitSplitOnce` を足すだけ", f && g && a && d, $"f {f}・g {g}・a {a}・d {d}");
            Expect("規定の駒は変わらない（新しい札の保持者は 52 枚 ＋ 退役に 0 枚・規定のヒヨは全量の変換）", def);
            string[] want = { "版・BN-f 燃焼", "版・BN-g 燃焼", "版・BN-a 燃焼", "版・BN-d 燃焼", "版・BN-f＋d 燃焼" };
            var vrow = Presets.Versions.Where(r => r.Name.StartsWith("版・", StringComparison.Ordinal)).ToArray();
            bool rows = vrow.Select(r => r.Name).SequenceEqual(want) && Presets.Versions.Take(3).Select(r => r.Name).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", PonRow })
                && vrow.Zip(Vers.Skip(1)).All(z => ReferenceEquals(z.First.F[1], z.Second.Borg) && ReferenceEquals(z.First.F[2], z.Second.Hota) && ReferenceEquals(z.First.F[4], z.Second.Hiyo) && ReferenceEquals(z.First.F[0], UnitCatalog.Mudo) && ReferenceEquals(z.First.F[3], UnitCatalog.Gald));
            Expect("試遊の台: 「試遊・感電 ポンの台」「試遊・ミサ 書き手なし」「試遊・燃焼 ポンの台」の後に「版・BN-f ／ BN-g ／ BN-a ／ BN-d ／ BN-f＋d 燃焼」（並びはポンの台・3枚が版）・「版・FL」は外した", rows, string.Join("・", Presets.Versions.Select(r => r.Name)));
        }
        // ---- BN-f ／ BN-g: 半減 ----
        {
            long guardB = 0, wardB = 0, guardF = 0, wardF = 0, guardG = 0, wardG = 0;
            long nomF = 0, healF = 0, halfF = 0, nomB = 0, healB = 0; int overF = 0, pairsF = 0, overB = 0, pairsB = 0;
            foreach (var w in ws) for (int s = 0; s < 5; s++)
            {
                foreach (var (v, set) in new (Ver, Action<BattleResult, List<UnitState>>)[]
                {
                    (B, (r, p) => { ConvPairs(r, ref overB, ref pairsB); guardB += r.TallyByUnit["borg"].FireArmorGuardHits; wardB += r.TallyByUnit["borg"].FireWardHits; nomB += r.TallyByUnit["hiyo"].FireConvTickNominal + r.TallyByUnit["hiyo"].FireConvSplashNominal; healB += r.TallyByUnit["hiyo"].FireConvTickHealed + r.TallyByUnit["hiyo"].FireConvSplashHealed; }),
                    (Fv, (r, p) =>
                    {
                        guardF += r.TallyByUnit["borg"].FireArmorGuardHits; wardF += r.TallyByUnit["borg"].FireWardHits;
                        nomF += r.TallyByUnit["hiyo"].FireConvTickNominal + r.TallyByUnit["hiyo"].FireConvSplashNominal; healF += r.TallyByUnit["hiyo"].FireConvTickHealed + r.TallyByUnit["hiyo"].FireConvSplashHealed;
                        halfF += r.Events.Where(x => x.Kind == BattleEventKind.FireArmor && x.Text == FireArmorLabels.Convert).Sum(x => (long)(x.Amount / 2));
                        ConvPairs(r, ref overF, ref pairsF);
                    }),
                    (G, (r, p) => { guardG += r.TallyByUnit["borg"].FireArmorGuardHits; wardG += r.TallyByUnit["borg"].FireWardHits; }),
                })
                {
                    var (r, p, _) = Fight(v, w, s);
                    set(r, p);
                }
            }
            Expect("BN-f: 燃えているボルグ ／ 味方の被ダメが半分にならない（火の鎧の半減 0 回・盾の配り 0 回・規定ではどちらも起きる）", guardF == 0 && wardF == 0 && guardB > 0 && wardB > 0, $"規定 鎧 {guardB}・配り {wardB} → BN-f {guardF} ／ {wardF}");
            Expect("BN-f: ヒヨの火の変換の回復は1回ごとに名目の半分（切り捨て）以下（変換の直後の回復を名目と突き合わせる・規定では半分を超える回がある）",
                overF == 0 && pairsF > 0 && overB > 0 && healF <= halfF, $"BN-f 半分を超えた {overF} ／ {pairsF} 回（癒えた {healF} ≤ Σ⌊名目/2⌋ {halfF}）・規定 半分を超えた {overB} ／ {pairsB} 回"); 
            Expect("BN-g: 盾の配りだけが外れ、ボルグ自身の火の鎧は働く", wardG == 0 && guardG > 0, $"鎧 {guardG}・配り {wardG}");
        }
        // ---- BN-a: 被弾の燃焼では癒えない ----
        {
            long deniedA = 0, convDeniedA = 0, mendA = 0, deniedB = 0, mendB = 0; int linesA = 0;
            foreach (var w in ws) for (int s = 0; s < 5; s++)
            {
                var (ra, _, _) = Fight(A, w, s);
                deniedA += ra.TallyByUnit.Values.Sum(t => t.Bn349MendDenied); convDeniedA += ra.TallyByUnit.Values.Sum(t => t.Bn349ConvDenied);
                mendA += ra.TallyByUnit["borg"].FireMendHealed + ra.TallyByUnit["hota"].FireMendHealed;
                linesA += ra.Log.Count(l => l.ToString().Contains("叩かれて燃え上がった火では癒えない"));
                var (rb, _, _) = Fight(B, w, s);
                deniedB += rb.TallyByUnit.Values.Sum(t => t.Bn349MendDenied + t.Bn349ConvDenied);
                mendB += rb.TallyByUnit["borg"].FireMendHealed + rb.TallyByUnit["hota"].FireMendHealed;
            }
            Expect("BN-a: 被弾の燃焼の刻みでは火の癒し ／ 火の変換が回復しない（拒んだ名目 > 0・ログに出る・規定は 0）", deniedA > 0 && convDeniedA > 0 && linesA > 0 && deniedB == 0, $"癒し {deniedA}・変換 {convDeniedA}・ログ {linesA} 行");
            Expect("BN-a: ターン頭の刻みなどでは今どおり火の癒しで回復する（ボルグ ／ ホタの火の癒し > 0）", mendA > 0 && mendB >= mendA, $"規定 {mendB} → BN-a {mendA}");
            // 被弾の燃焼の中の火の癒しは 0（刻みの台本: 火の癒しの見出しは `ActorId` ＝ 自分・被弾の燃焼の刻みは一撃の主を持つ `Status` を出さない）
            var (r1, p1, _) = Fight(A, I.StrongWaves[0], 0);
            var (r0, p0, _) = Fight(B, I.StrongWaves[0], 0);
            Expect("BN-a: 同じ戦（強 第2波 × seed 0）で拒んだ名目 ＝ 規定の被弾の燃焼で癒えた分の名目の出どころ（規定 > 0 ／ BN-a は拒んだ分だけ減る）",
                r1.TallyByUnit.Values.Sum(t => t.Bn349MendDenied + t.Bn349ConvDenied) > 0, $"BN-a 拒んだ {r1.TallyByUnit.Values.Sum(t => t.Bn349MendDenied + t.Bn349ConvDenied)}");
        }
        // ---- BN-d: 1回の攻撃に1度 ----
        {
            int multiD = 0, multiB = 0; long rainD = 0, rainB = 0;
            foreach (var (v, set) in new (Ver, bool)[] { (D, true), (B, false) })
                foreach (var w in ws) for (int s = 0; s < 5; s++)
                {
                    var (r, p, e) = Fight(v, w, s);
                    var at = Attribute(r, p, e);
                    if (set) rainD += at.HitBurnN[0]; else rainB += at.HitBurnN[0];
                    // 手番（枠）ごとに同じ一撃の主 → 同じ敵への被弾の燃焼の数
                    var bounds = r.Hands.Select(h => (h.EventStart, h.EventEnd, h.ActorId)).ToList();
                    foreach (var (a0, a1, actor) in bounds)
                    {
                        var c = new Dictionary<(int, int), int>();
                        for (int i = a0; i < Math.Min(a1, r.Events.Count); i++)
                        {
                            var x = r.Events[i];
                            if (x.Kind == BattleEventKind.Status && x.Text == "燃焼" && x.ActorId == actor && x.TargetId is int t && (x.TickIndex ?? 1) == 1) c[(actor, t)] = c.GetValueOrDefault((actor, t)) + 1;
                        }
                        int m = c.Values.Count(n => n > 1);
                        if (set) multiD += m; else multiB += m;
                    }
                }
            Expect("BN-d: 手番の一振りの連撃 ／ 火の雨の2発目以降に被弾の燃焼が乗らない（火の雨に乗った被弾の燃焼 0・自分の手番の枠で同じ敵が2回以上 0・規定ではどちらも起きる）。手番の外の攻撃は1回ずつが枠（第255期の札の形・ムドの連撃は1発ずつ）",
                rainD == 0 && rainB > 0 && multiD == 0 && multiB > 0, $"火の雨 規定 {rainB} → BN-d {rainD}・2回以上 規定 {multiB} → BN-d {multiD}");
        }
        // ---- verbose ／ PickOne ----
        {
            int diff = 0, n = 0;
            foreach (var v in Vers) foreach (var w in ws) for (int s = 0; s < 4; s++)
            {
                var (a, _, _) = Fight(v, w, s, verbose: true); var (z, _, _) = Fight(v, w, s, verbose: false);
                n++; if (a.PlayerWon != z.PlayerWon || a.Turns != z.Turns || a.PlayerSurvivors != z.PlayerSurvivors) diff++;
            }
            Expect("verbose の有無で勝敗 ／ T ／ 生存が同じ（版 6 × 波 6 × seed 0..3）", diff == 0, $"{n} 戦・差 {diff}");
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 1609b692:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Burn349*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第348期（1609b692）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
