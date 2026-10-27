using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh405 check —— 第405期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh405Diag
{
    internal const string HeadSha = "27d71da6";   // 第404期の HEAD（この期の作業を始めたときの BattleCore ／ BattleSim・9f8ee640 は DemoApp だけ）
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh405 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef hagi = UnitCatalog.Hagi, old = UnitCatalog.HagiH404, r50 = UnitCatalog.HagiReapR50;
        var boards = Boards();

        // ---- 段0 ----
        {
            bool cards = hagi.Traits.SequenceEqual(r50.Traits) && hagi.PlusText == r50.PlusText && hagi.MinusText == r50.MinusText && hagi != r50 && UnitCatalog.All.Contains(hagi) && hagi.Id == "hagi"
                && SameUnitName(hagi.Name, old.Name) && hagi.MaxHp == old.MaxHp && hagi.Attack == old.Attack && hagi.Speed == old.Speed
                && hagi.Traits.Contains(TraitId.HagiLineCap50) && hagi.Traits.Contains(TraitId.HagiReapAfterHit) && hagi.Traits[^1] == TraitId.HagiSpillFixed
                && hagi.PlusText.StartsWith("大鎌で斬り、線を切った敵はその一振りで刈り取る", StringComparison.Ordinal) && hagi.PlusText.Contains("合わせて最大 5 割", StringComparison.Ordinal);
            bool oldOk = old.Traits.SequenceEqual(UnitCatalog.HagiSingleT1.Traits) && old.PlusText == UnitCatalog.HagiSingleT1.PlusText && !UnitCatalog.Everyone.Contains(old) && !UnitCatalog.Everyone.Contains(r50);
            bool pins = PinH404(hagi) == old && PinH404(old) == old && PinH403(hagi) == UnitCatalog.HagiH403 && PinH403(old) == UnitCatalog.HagiH403 && PinH402(old) == UnitCatalog.HagiH402 && PinH401(old) == UnitCatalog.HagiH401
                && PinT399(hagi) == UnitCatalog.HagiH401 && PinH391(old) == UnitCatalog.HagiH391 && PinH389(old) == UnitCatalog.HagiH389 && Pin404(Presets.HagiCarry401())[3] == old && Pin404(Presets.HagiTwin403())[3] == old;
            bool v404 = new[] { UnitCatalog.HagiReapL45, UnitCatalog.HagiReapR50, UnitCatalog.HagiReapR45, UnitCatalog.HagiBigA404 }
                .All(x => x.Traits.Take(old.Traits.Count - 1).SequenceEqual(old.Traits.SkipLast(1)) && x.PlusText.Length > 0)
                && UnitCatalog.HagiBigA405.Traits.Take(hagi.Traits.Count - 1).SequenceEqual(hagi.Traits.SkipLast(1)) && UnitCatalog.HagiBigA405.Traits.Contains(TraitId.HagiBigSoul) && !UnitCatalog.Everyone.Contains(UnitCatalog.HagiBigA405);
            Expect("段0: 規定のハギ ＝ 第404期の R50 と同じ札と文面（別の定義・`All` の中・名前 ／ 数値は据え置き）・旧 `HagiH404` ＝ T1（`All` ／ `Retired` の外）・固定の口（`PinH404` ／ `Pin404`・`PinH403` ／ `PinH402` ／ `PinH401` ／ `PinT399` ／ `PinH391` ／ `PinH389` は旧も古い旧へ）・第404期の版の駒は旧から作ったまま・`HagiBigA405` は規定 ＋ A",
                cards && oldOk && pins && v404, $"札 {cards}・旧 {oldOk}・口 {pins}・第404期の版 {v404}");
        }
        {
            long diff = 0, cells = 0;
            foreach (var bd in boards.Where(b => b.Name != "版・大物 ハギ"))   // 大物の台は規定 ＋ A なので比べない
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(bd.F, w, sd).R, I.Raw(ColBoard(bd, r50), w, sd).R)) diff++;
                    }
            Expect("段0: 規定のハギは第404期の R50 と同じ動き（5 台 × 4 波 × seed 0..9・版・大物 ハギは規定 ＋ A なので除く）", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- 規定の compare（docs と一致・第404期の表から動いたのはハギのいる行だけ・R50 の列と同じ）----
        {
            var map = S390.BalanceMap(S390.GitShow("cf2661f7:docs/balance.md"));   // 第411期: 第405〜410期の表（compare は第406〜410期に動いていない）と、カーズの2行を旧の行名 ＋ 旧のカリに戻した行で比べる
            var head = S390.BalanceMap(S390.GitShow($"{HeadSha}:docs/balance.md"));
            var rows = CompareBuildsH410(); var g = CGrid(rows, CompareSeeds);
            var rcol = CGrid(rows.Select(r => (r.Name, F: I.Swap(Pin404(r.F), "hagi", r50))).ToArray(), CompareSeeds);
            int diff = 0, miss = 0, noHagiMoved = 0, notR = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var c)) { miss++; continue; }
                for (int w = 0; w < 5; w++)
                {
                    if (c[w].TrimEnd('%') != F1(g[ri, w])) diff++;
                    if (g[ri, w] != rcol[ri, w]) notR++;
                    if (!Has(rows[ri].F, "hagi") && head.TryGetValue(rows[ri].Name, out var h0) && h0[w] != c[w]) noHagiMoved++;
                }
            }
            Expect($"規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致・第404期の R50 の列と同じ・ハギのいない行は第404期（{HeadSha}）の表と同じ", diff == 0 && miss == 0 && noHagiMoved == 0 && notR == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}・R50 の列と違うセル {notR}・ハギのいない行で動いたセル {noHagiMoved}");
        }

        // ---- 出来事: 足した札が台本に出る・回数が帳簿と合う ----
        {
            var notes = new List<string>(); bool ok = true;
            var cnt = new Dictionary<string, long>();
            long reapE = 0, reapT = 0, chaseE = 0, chaseT = 0, afterE = 0, afterT = 0, colE = 0, colT = 0, twinE = 0, twinT = 0, encE = 0, encT = 0, purE = 0, purT = 0, markE = 0, markT = 0;
            long wgtE = 0, wgtT = 0, wgtN = 0, wgtNT = 0, hvyE = 0, hvyT = 0, hvyN = 0, hvyNT = 0, soulBad = 0, battles = 0;
            foreach (var bd in boards)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        battles++;
                        var (r, id) = One(bd.F, w, sd);
                        int hid = id.First(kv => kv.Value == "hagi").Key;
                        foreach (var x in r.Events)
                        {
                            if (x.Kind != BattleEventKind.Reap) continue;
                            string key = x.Text + (x.Text == ReapLabels.Reap ? $"・{x.Slot}" : x.Text == ReapLabels.Stage ? $"・{x.Amount}" : x.Text == ReapLabels.Twin ? (x.Amount == 1 ? "・刈り" : "・斬撃") : x.Text == ReapLabels.Soul ? (x.Amount < 0 ? "・薄れ" : $"・{x.Slot}") : "");
                            cnt[key] = cnt.GetValueOrDefault(key) + 1;
                        }
                        var ev = r.Events.Where(x => x.Kind == BattleEventKind.Reap && x.ActorId == hid).ToList();
                        reapE += ev.Count(x => x.Text == ReapLabels.Reap && x.Slot is ReapLabels.KindTurn or ReapLabels.KindTwin);
                        chaseE += ev.Count(x => x.Text == ReapLabels.Reap && x.Slot == ReapLabels.KindChase);
                        afterE += ev.Count(x => x.Text == ReapLabels.Reap && x.Slot is ReapLabels.KindAfter or ReapLabels.KindAfterChase);
                        colE += ev.Count(x => x.Text == ReapLabels.Reap && x.Slot == ReapLabels.KindColumn);
                        twinE += ev.Count(x => x.Text == ReapLabels.Twin); encE += ev.Count(x => x.Text == ReapLabels.Encore); purE += ev.Count(x => x.Text == ReapLabels.Pursue); markE += ev.Count(x => x.Text == ReapLabels.Mark);
                        wgtE += ev.Where(x => x.Text == ReapLabels.Weight).Sum(x => (long)x.Amount); wgtN += ev.Count(x => x.Text == ReapLabels.Weight);
                        hvyE += ev.Where(x => x.Text == ReapLabels.Heavy).Sum(x => (long)x.Amount); hvyN += ev.Count(x => x.Text == ReapLabels.Heavy);
                        // 魂: 増減を足し合わせた値が負にならず、最大が帳簿の最大と同じ・「魂」の後の値（StatusRemaining）と合う
                        int soul = 0, peak = 0;
                        foreach (var x in ev.Where(x => x.Text == ReapLabels.Soul)) { soul += x.Amount; if (soul < 0 || x.StatusRemaining != soul) soulBad++; peak = Math.Max(peak, soul); }
                        if (r.TallyByUnit.TryGetValue("hagi", out var t))
                        {
                            reapT += t.R388Reaps; chaseT += t.R391ChaseReaps; afterT += t.R404AfterReaps; colT += t.R389ColumnUnits + t.R391ChaseColumnUnits;
                            twinT += t.R403Twins; encT += t.R388Encores; purT += t.R388Pursuits; markT += t.R389MarksPlaced;
                            wgtT += t.R401MulAdd; wgtNT += t.R401MulHits; hvyT += t.R401MarkAdd; hvyNT += t.R401MarkHits;
                            if (peak != t.R389SoulPeak) soulBad++;
                        }
                    }
            // 「二度斬りの 2 発目の刈り取り」（刈る 6 ／ 二度斬る・刈り）は、規定（R50）では 1 発目の当たった後に同じ線で刈るので事実上出ない（1 発目が外れたときだけ）——出ないことを確かめる
            string[] need = { $"{ReapLabels.Reap}・{ReapLabels.KindTurn}", $"{ReapLabels.Reap}・{ReapLabels.KindChase}", $"{ReapLabels.Reap}・{ReapLabels.KindAfter}", $"{ReapLabels.Reap}・{ReapLabels.KindAfterChase}",
                $"{ReapLabels.Twin}・斬撃", ReapLabels.Encore, ReapLabels.Pursue, ReapLabels.Mark, ReapLabels.Line, $"{ReapLabels.Soul}・0", $"{ReapLabels.Soul}・50", $"{ReapLabels.Soul}・{ReapLabels.SoulAlly}", $"{ReapLabels.Soul}・薄れ",
                $"{ReapLabels.Stage}・1", $"{ReapLabels.Stage}・2", $"{ReapLabels.Stage}・3", ReapLabels.Weight, ReapLabels.Heavy };
            var missing = need.Where(k => cnt.GetValueOrDefault(k) == 0).ToList();
            ok &= missing.Count == 0; notes.Add(missing.Count == 0 ? "どの札も出た（列ごとは台しだい）" : "出ない: " + string.Join("・", missing));
            long twinReap = cnt.GetValueOrDefault($"{ReapLabels.Reap}・{ReapLabels.KindTwin}") + cnt.GetValueOrDefault($"{ReapLabels.Twin}・刈り");
            notes.Add($"二度斬りの 2 発目の刈り取り {twinReap}（規定では 1 発目の当たった後に刈るので事実上 0）");
            bool eq = reapE == reapT && chaseE == chaseT && afterE == afterT && colE == colT && twinE == twinT && encE == encT && purE == purT && markE == markT && wgtE == wgtT && wgtN == wgtNT && hvyE == hvyT && hvyN == hvyNT && reapE > 0 && afterE > 0;
            ok &= eq && soulBad == 0;
            notes.Add($"帳簿と同じ回数（刈る 振る前 {reapE} ／ {reapT}・追撃 {chaseE} ／ {chaseT}・当たった後 {afterE} ／ {afterT}・列ごと {colE} ／ {colT}・二度斬る {twinE} ／ {twinT}・再行動 {encE} ／ {encT}・追撃 {purE} ／ {purT}・印 {markE} ／ {markT}・段の重み {wgtE}（{wgtN} 回）／ {wgtT}（{wgtNT}）・印の重み {hvyE}（{hvyN}）／ {hvyT}（{hvyNT}）・魂の帳尻の外れ {soulBad}）{(eq && soulBad == 0 ? "" : " ×")}");
            notes.Add(string.Join("・", cnt.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}")));
            Expect($"出来事: 足した札（`Reap` 刈る（種類）／ 二度斬る ／ 再行動 ／ 追撃 ／ 印 ／ 線 ／ 魂 ／ 段 ／ 段の重み ／ 印の重み）が台本に出る・回数が帳簿と合う（6 台 × 4 波 × seed 0..9・{battles} 戦）", ok, string.Join("・", notes));
        }
        {
            // 並び: 刈る → 直後にその敵への Damage（HpAfter 0）／ 二度斬る → 次のハギの Attack か刈る（同じ的）／ 再行動 → 次のハギの振り ／ 追撃（刈り）→ 刈る ／ 印 → 直前はその敵への Damage ／ 段 → 直後に線 × 生きている敵
            var notes = new List<string>(); bool ok = true;
            int reaps = 0, badReap = 0, twins = 0, badTwin = 0, encs = 0, badEnc = 0, purs = 0, badPur = 0, marks = 0, badMark = 0, stages = 0, badStage = 0;
            foreach (var bd in boards)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        var (r, id, foe) = OneF(bd.F, w, sd);
                        var ev = r.Events;
                        for (int i = 0; i < ev.Count; i++)
                        {
                            var x = ev[i];
                            if (x.Kind != BattleEventKind.Reap) continue;
                            BattleEvent? Next(Func<BattleEvent, bool> p) { for (int j = i + 1; j < ev.Count; j++) if (p(ev[j])) return ev[j]; return null; }
                            if (x.Text == ReapLabels.Reap)
                            {
                                reaps++;
                                var d = Next(y => y.Kind == BattleEventKind.Damage || (y.Kind == BattleEventKind.Reap && y.Text == ReapLabels.Reap));
                                if (d is null || d.Kind != BattleEventKind.Damage || d.TargetId != x.TargetId || d.ActorId != x.ActorId || d.HpAfter != 0 || d.Amount != x.Amount) badReap++;
                            }
                            else if (x.Text == ReapLabels.Twin)
                            {
                                twins++;
                                var n = Next(y => y.ActorId == x.ActorId && (y.Kind == BattleEventKind.Attack || (y.Kind == BattleEventKind.Reap && y.Text == ReapLabels.Reap)));
                                bool good = n is not null && n.TargetId == x.TargetId && (x.Amount == 1 ? n.Kind == BattleEventKind.Reap && n.Slot == ReapLabels.KindTwin : n.Kind == BattleEventKind.Attack);
                                if (!good) badTwin++;
                            }
                            else if (x.Text == ReapLabels.Encore)
                            {
                                encs++;
                                var n = Next(y => y.ActorId == x.ActorId && (y.Kind == BattleEventKind.Attack || (y.Kind == BattleEventKind.Reap && y.Text is ReapLabels.Reap or ReapLabels.Weight or ReapLabels.Heavy)));
                                if (n is null) badEnc++;
                            }
                            else if (x.Text == ReapLabels.Pursue)
                            {
                                purs++;
                                var n = Next(y => y.ActorId == x.ActorId && (y.Kind == BattleEventKind.Attack || (y.Kind == BattleEventKind.Reap && y.Text == ReapLabels.Reap)));
                                bool good = n is not null && n.TargetId == x.TargetId && (x.StatusRemaining == 1 ? n.Kind == BattleEventKind.Reap && n.Slot == ReapLabels.KindChase : n.Kind == BattleEventKind.Attack || (n.Kind == BattleEventKind.Reap && n.Slot == ReapLabels.KindChase));
                                if (!good) badPur++;
                            }
                            else if (x.Text == ReapLabels.Mark)
                            {
                                marks++;
                                var d = Next(y => y.Kind == BattleEventKind.Damage);   // 印は、その一撃の Damage の直前に出る（`ApplyDamageBody` の HP を引いた後・台本の Damage を積む前）
                                if (d is null || d.TargetId != x.TargetId || d.ActorId != x.ActorId) badMark++;
                            }
                            else if (x.Text == ReapLabels.Stage)
                            {
                                stages++;
                                int n = 0; for (int j = i + 1; j < ev.Count && ev[j].Kind == BattleEventKind.Reap && ev[j].Text == ReapLabels.Line; j++) n++;
                                var dead = r.Events.Take(i + 1).Where(y => y.Kind == BattleEventKind.Death && y.TargetId is int t && foe.Contains(t)).Select(y => y.TargetId!.Value).ToHashSet();
                                int alive = foe.Count - dead.Count;   // 最初に立っていた敵のうち生きている数（召喚は数えない・いれば線は増える側）
                                if (n == 0 && alive > 0) badStage++;   // 敵が全滅した瞬間（最後の死で段が上がる）は線が 0 本でよい
                            }
                        }
                    }
            ok = badReap == 0 && badTwin == 0 && badEnc == 0 && badPur == 0 && badMark == 0 && reaps > 0 && twins > 0 && purs > 0 && marks > 0 && stages > 0;
            notes.Add($"刈る → Damage（同じ的・HpAfter 0・同じ量）{reaps} 件・外れ {badReap}");
            notes.Add($"二度斬る → 次のハギの振り（刈りなら「刈る」6・斬撃なら Attack・同じ的）{twins} 件・外れ {badTwin}");
            notes.Add($"再行動 → 次のハギの振り {encs} 件・外れ {badEnc}");
            notes.Add($"追撃 → Attack か「刈る」2（同じ的）{purs} 件・外れ {badPur}");
            notes.Add($"印 → 直後のハギの Damage（同じ的・印はその一撃の Damage の直前）{marks} 件・外れ {badMark}");
            notes.Add($"段 → 線（生きている敵ごと）{stages} 件・敵が生きているのに線が 0 本 {badStage}");
            ok &= badStage == 0;
            Expect("出来事の並び（6 台 × ボス ／ 近衛 ／ 大隊 × seed 0..4）", ok, string.Join("・", notes));
        }

        // ---- 盤面を変えない: verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var bd in boards)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5, I.Nine })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(bd.F, w, sd).R;
                        if (!Same(r0, I.Raw(bd.F, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(bd.F, w, sd).R)) det++;
                    }
            foreach (var row in CompareBuilds().Where(r => Has(r.F, "hagi")))
                foreach (var w in EnemyCatalog.Stages)
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        var r0 = BattleEngine.Run(row.F, w.Enemy, sd, verbose: false);
                        if (!Same(r0, BattleEngine.Run(row.F, w.Enemy, sd, verbose: true))) diff++;
                    }
            Expect("盤面を変えない: verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（6 台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 ／ 九体 ＋ `compare` のハギの 5 行 × 5 波 × seed 0..9）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- ボスの名前 ／ 種類で分岐していない ----
        {
            var hits = new List<string>();
            foreach (var f in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
                foreach (var l in File.ReadAllLines(f))
                {
                    if (!(l.Contains("第405期", StringComparison.Ordinal) || l.Contains("EmitReap", StringComparison.Ordinal) || l.Contains("ReapLabels", StringComparison.Ordinal))) continue;
                    string code = l.Contains("//") ? l[..l.IndexOf("//", StringComparison.Ordinal)] : l;
                    if (code.TrimStart().StartsWith("///", StringComparison.Ordinal)) continue;
                    if (code.Contains("Boss", StringComparison.Ordinal) || code.Contains("勇者", StringComparison.Ordinal) || code.Contains("Hero", StringComparison.Ordinal) || code.Contains("Def.Id", StringComparison.Ordinal) || code.Contains("Def.Name", StringComparison.Ordinal)) hits.Add(l.Trim());
                }
            Expect("ボスの名前 ／ 種類で分岐していない（第405期の出来事を積む行に Boss ／ 勇者 ／ Hero ／ Def.Id ／ Def.Name が無い）", hits.Count == 0, hits.Count == 0 ? "" : string.Join(" ／ ", hits.Take(3)));
        }

        // ---- 試遊の台 ----
        {
            var vrows = VersionsH407();   // 第410期: 第408期から末尾に足したカリ ／ カーズの台を外した一覧（＝ 第405〜407期の一覧）で見る
            bool now = !vrows.Any(r => r.Name == "版・刈り続け ハギ") && vrows.First(r => r.Name == "版・大物 ハギ").F[1] == UnitCatalog.HagiBigA405
                && vrows.First(r => r.Name == "試遊・ハギ キャリー").F[3] == hagi && vrows.First(r => r.Name == "試遊・ハギ 哀歌").F[3] == hagi && vrows.First(r => r.Name == "試遊・ハギ 台B").F[3] == hagi
                && !vrows.SelectMany(r => r.F.Occupied()).Any(o => o.Def == UnitCatalog.HagiReapR45 || o.Def == UnitCatalog.HagiReapR50 || o.Def == UnitCatalog.HagiReapL45 || o.Def == UnitCatalog.HagiBigA404 || o.Def == old);
            var h404 = VersionsH404();
            bool past = h404[^1].Name == "版・刈り続け ハギ" && h404[^1].F[3] == UnitCatalog.HagiReapR45 && h404.First(r => r.Name == "版・大物 ハギ").F[1] == UnitCatalog.HagiBigA404
                && h404.SelectMany(r => r.F.Occupied()).All(o => o.Def != hagi) && h404.Count(r => r.F.Occupied().Any(o => o.Def == old)) >= 4 && h404.Length == vrows.Length + 1 && VersionsH403().Length == h404.Length - 1;
            bool inside = !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).SelectMany(r => r.F.Occupied()).Any(o => o.Def == old || o.Def == r50 || o.Def == UnitCatalog.HagiReapR45);
            Expect("試遊の台: 「版・刈り続け ハギ」を外した・「版・大物 ハギ」は規定 ＋ A（`HagiBigA405`）・ハギの台は規定のハギ・第404期の一覧は `VersionsH404`（ハギは旧・大物は第404期の A・末尾に刈り続け × R45）・旧 ／ 版は器具の中だけ",
                now && past && inside, $"今 {now}・第404期 {past}・中だけ {inside}・{string.Join("、", vrows.TakeLast(3).Select(r => r.Name))}");
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool Row(TraitId t, string pat) => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(HcPlusL,\s*""[^""]*{pat}""");
            var miss = new[] { TraitId.HagiLineCap50, TraitId.HagiReapAfterHit }.Where(t => !Row(t, "第404期[^\"]*第405期から規定（ハギ）")).Concat(new[] { TraitId.HagiLineCap45 }.Where(t => !Row(t, "第404期[^\"]*保持者 0 枚"))).ToList();
            Expect("`checkup` の札の分類表: 規定になった R50 の札 2 枚（線の上限 5 割 ／ 斬った直後に刈る）は「第405期から規定（ハギ）」・線の上限 45% は保持者 0 枚（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1c = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（第404期（{HeadSha}）と同じ数）", e0 == e1 && t0 == t1c && r0 == r1 && re0 == re1 && e0 > 0, $"PickOne engine {e0} → {e1}・traits {t0} → {t1c}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
