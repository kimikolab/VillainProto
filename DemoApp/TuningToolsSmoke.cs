using BattleCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================================
// 試遊を減らす道具の門（第466期 §5・`--tuning-tools-smoke`・指示書 design/PHASE466_TUNING_TOOLS_SPEC.md）
//
// **画面を1つも作らない。** `Chapter1` ／ `PlayLog` ／ `FoeWeight` ／ `Map11Session` と器具の方針（`Map12ChangeSmoke.Greedy`）だけを回す。
// 写しは `.tmp/p466/smoke/plays` に書く（`design/plays` は汚さない）。
//
//   (a) 1 章を器具で 1 回通すと、3 ステージ × 始め・1 刻目の前・終わりの写しが出る。読み直すと持ち越しと同じ中身
//   (b) 1-2 ／ 1-3 の始めの写しから始めると、1-1 から通して入ったときと同じ seed なら 1 刻ずつ一致する
//   (c) 写しの版で始める ／ 今の版で始める、の切替が効く（敵の倍率が替わる）
//   (d) `--foe-weight` が 1-2 ・ 1-3 の表を出す。第462期 §3 の「ハギ × 勇者」の行を再現する
//   (e) 手で写した第461〜464期の写しが読める（推定の注つき）
//   (f) 規則は 1 行も変わっていない——写しを書いても書かなくても通しは同じ（単発・画面・`--chapter1-*` の門は回帰で見る）
// =====================================================================================

public static class TuningToolsSmoke
{
    private const int RetryCap = 6;

    private static string Finger(Map11State st)
    {
        string U(UnitState u) => $"{u.Def.Id}{u.Def.MaxHp}:{u.Slot}:{u.Hp}{(u.Relic is { } r ? "◆" + r : "")}";
        string squads = string.Join(" | ", st.Squads.Select(q => $"{q.Pos}{(q.Lost ? "x" : "")}[{string.Join(",", q.Units?.Select(U) ?? Enumerable.Empty<string>())}]"));
        string nodes = string.Join(",", st.Nodes.SelectMany(r => r).Concat(st.Spawns).Select(n => $"{n.Pos}:{n.HpNow}{(n.Cleared ? "c" : "")}"));
        string hero = st.Hero is { } h ? $"{h.Pos}:{h.HpNow}{(h.Cleared ? "c" : "")}" : "-";
        var log = Map11Session.BattleLog;
        string last = log.Count == 0 ? "-" : string.Join(";", log.Select(l => $"{l.Squad}>{l.Foe}{(l.Won ? "W" : "L")}{l.Alive}/{l.HpPercent:F1}"));
        return $"T{st.Turn} G{st.Gold} cap{st.SquadCap} 隊[{squads}] 敵[{nodes}] 勇者[{hero}] 手札[{string.Join(",", st.Bench.Select(U))}]"
               + $" 候補[{string.Join(",", st.Offers.Select(d => d.Id))}] 札[{string.Join(",", st.RelicHand)}] 戦闘{log.Count} {last.GetHashCode():x8}";
    }

    private sealed class Run1
    {
        public Dictionary<(int Stage, int Attempt), List<string>> Turns { get; } = new();
        public string?[] StartText { get; } = new string?[3];
        public string?[] EntryText { get; } = new string?[3];
        public string?[] EndText { get; } = new string?[3];
        public int[] Battles { get; } = new int[3];
        public string[] Result { get; } = new string[3];
        public bool Through { get; set; }
        public string? Dir { get; set; }
    }

    /// <summary>器具の方針で、`Map11Session` の戦闘の口（画面と同じ・戦闘の記録が残る）を通して 1 ステージ回す。</summary>
    private static void PlayStage(Map11State st, List<string> turns)
    {
        bool last = Chapter1.Index == Chapter1.Stages.Count - 1;
        Map11Session.TakeOpening();   // 画面の「進める」と同じ口（1 刻目の前の写しがここで出る）
        turns.Add(Finger(st));
        Map12ChangeSmoke.Greedy(st.Seed, st.Stage, guard: true, split: true, state: st, siegeRest: true,
            heroPlan: last ? Map12ChangeSmoke.HeroPlan.Flee : Map12ChangeSmoke.HeroPlan.None, watch: s => turns.Add(Finger(s)));
    }

    /// <summary>1 章を器具で通す（負ければ同じステージをやり直す・1 ステージ <see cref="RetryCap"/> 回まで）。</summary>
    private static Run1 PlayChapter(int seed, ChapterRule rule)
    {
        var run = new Run1();
        Chapter1.Begin(seed, rule);
        run.Dir = PlayLog.ChapterDir;
        for (int guard = 0; guard < 3 * (RetryCap + 1) && Chapter1.Active && !Chapter1.Over; guard++)
        {
            Map11State st = Map11Session.State!;
            int i = Chapter1.Index;
            var startPack = st.TakeCarry();
            run.StartText[i] = PlayLog.PackText(startPack with { Gold = startPack.Gold - (st.Stage.Gold.On ? st.Stage.Gold.Start : 0) });
            run.EntryText[i] = Chapter1.Records[i].Entry is { } e ? PlayLog.PackText(e) : null;
            var turns = new List<string>();
            run.Turns[(i, Chapter1.Attempt)] = turns;
            PlayStage(st, turns);
            Chapter1.Settle(st, Map11Session.RelicWorked, Map11Session.BattleLog);
            run.EndText[i] = Chapter1.Records[i].EndPack is { } p ? PlayLog.PackText(p) : null;
            run.Battles[i] = Map11Session.BattleLog.Count;
            run.Result[i] = Chapter1.Records[i].Result;
            if (!st.Won)
            {
                if (Chapter1.Records[i].Retries >= RetryCap) break;
                Chapter1.Retry();
            }
            else if (i == Chapter1.Stages.Count - 1) { run.Through = true; break; }
            else Chapter1.Next();
        }
        return run;
    }

    /// <summary>戦闘の口を `Map11Session` に通す（`Map11Main.PlayChapterForCapture` と同じ・戦闘の記録と働いた札が残る）。</summary>
    private static void FightThroughSession(Map11State st, int squad, Map11State.Node node, bool intercept)
    {
        bool begun = intercept ? Map11Session.BeginIntercept(squad, node) : Map11Session.BeginBattle(squad, node);
        if (!begun) return;
        BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: true);
        CampaignSession.ClearCarriedBattle();
        Map11Session.CompleteBattle(r.PlayerWon, r);
    }

    public static bool Run(Action<string> write, string demoDir, bool quick = false)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        string repo = Path.GetFullPath(Path.Combine(demoDir, ".."));
        string tmp = Path.Combine(repo, ".tmp", "p466", "smoke");
        if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true);
        string plays = Path.Combine(tmp, "plays");
        string realPlays = PlayLog.DefaultRoot(demoDir);
        ChapterRule rule = ChapterRule.V465;   // 第467期: 第465期の 1 章に固定（遊ぶ 1 章は投資のある第467期・(c) の「今の版」だけが `Chapter1.Default` を読む）
        ChapterRule nowRule = ChapterRule.V468;   // 第469期: (c) の「今の版」も第468期に固定（遊ぶ 1 章は始めの 6 枚のある第469期）
        write($"  遊ぶ 1 章 {rule.Name}・写しの置き場（門） {plays}・手で写した写し {PlayLog.ManualDir(realPlays)}");

        Map12ChangeSmoke.FightHook = FightThroughSession;
        PlayLog.Disable();
        try
        {
            // ---- (f) の前半: 写しを書かない通し ----
            Run1? bare = null;
            int seed = -1;
            for (int c = 4660; c < 4680 && bare is null; c++)
            {
                Run1 r = PlayChapter(c, rule);
                if (r.Through) { bare = r; seed = c; }
            }
            Req(bare is not null && bare.Dir is null, $"(f0) 写しを書かない起動では 1 章が写しのフォルダを持たない（器具で通せた 1 章の seed {seed}）");
            if (bare is null) return false;

            // ---- (a) 同じ 1 章を、写しを書いて通す ----
            PlayLog.Enable(plays);
            Run1 run = PlayChapter(seed, rule);
            string dir = run.Dir ?? "";
            write($"  (a) 1 章の seed {seed}: {string.Join(" ／ ", Enumerable.Range(0, 3).Select(i => $"{Chapter1.Stages[i].Id} {run.Result[i]}（やり直し {Chapter1.Records[i].Retries}・戦闘 {run.Battles[i]}）"))}・写し {dir}");
            bool okA = run.Through && Directory.Exists(dir);
            var stagesW = Chapter1.StagesFor(rule);
            for (int i = 0; i < 3 && okA; i++)
            {
                string id = stagesW[i].Id;
                var files = new[] { "start", "first", "end" }.Select(k => Path.Combine(dir, $"{id}_{k}.json")).ToList();
                bool exist = files.All(File.Exists);
                if (!exist) { write($"      × {id}: 写しが無い（{string.Join("・", files.Where(f => !File.Exists(f)).Select(Path.GetFileName))}）"); okA = false; continue; }
                PlayLog.Snap s0 = PlayLog.Read(files[0]), s1 = PlayLog.Read(files[1]), s2 = PlayLog.Read(files[2]);
                string got0 = PlayLog.PackText(PlayLog.ToCarry(s0.Pack!, stagesW[i].Stars));
                string want0 = i == 0 ? run.StartText[0]! : run.EntryText[i]!;
                string got2 = PlayLog.PackText(PlayLog.ToCarry(s2.Pack!, stagesW[i].Stars));
                bool m0 = got0 == want0 && s0.Entry == (i > 0) && (i == 0 || run.StartText[i] == want0);
                bool m2 = got2 == run.EndText[i] && s2.Battles?.Count == run.Battles[i] && s2.Result == run.Result[i];
                bool m1 = s1.Pack is not null && s1.Entry && s1.Kind == "first";
                bool head = s0.ChapterSeed == seed && s0.StageIndex == i && s0.Rule == rule.Key && s0.StageSeed == Chapter1.StageSeed(seed, i, s0.Attempt);
                write($"      {id}: 始め（{(s0.Entry ? "持ち越し" : "配り")}・配り {s0.Attempt + 1} 回目・seed {s0.StageSeed}）一致 {m0} ／ 1 刻目の前 {(m1 ? "あり" : "なし")}（隊 {s1.Pack?.Squads.Count(q => q.Count > 0)}・駒 {s1.Pack?.Bench.Count + s1.Pack?.Squads.Sum(q => q.Count)}）"
                      + $" ／ 終わり 一致 {m2}（戦闘 {s2.Battles?.Count}・{s2.Result}）・見出し {head}");
                if (!m0) write($"        want {want0}\n        got  {got0}");
                if (!m2) write($"        want {run.EndText[i]}\n        got  {got2}");
                okA &= m0 && m1 && m2 && head;
            }
            Req(okA, "(a) 1 章を器具で 1 回通すと 3 ステージ × 始め・1 刻目の前・終わりの写しが出る。読み直すと持ち越しと同じ中身（第461期の門 (c)(d) の並べ方 ＋ ☆）・終わりの写しに戦闘の記録");

            // ---- (f) 写しを書いても書かなくても通しは同じ ----
            bool same = bare.Turns.Count == run.Turns.Count && bare.Turns.All(kv => run.Turns.TryGetValue(kv.Key, out var t) && t.SequenceEqual(kv.Value));
            write($"  (f) 写しを書かない通しと書く通し: 配り {bare.Turns.Count} ／ {run.Turns.Count}・刻 {bare.Turns.Sum(kv => kv.Value.Count)} ／ {run.Turns.Sum(kv => kv.Value.Count)}・すべて一致 {same}");
            Req(same && rule.Name == ChapterRule.V465.Name,
                "(f) 規則は 1 行も変わっていない——写しを書いても書かなくても 1 章の通しは 1 刻ずつ同じ・遊ぶ 1 章は第465期のまま（単発・画面・`--chapter1-*` の門・`compare` ／ `docs/` は回帰で見る）");

            // ---- (b) 写しから始めると 1 刻ずつ一致する ----
            bool okB = true;
            for (int i = 1; i < 3; i++)
            {
                string id = stagesW[i].Id;
                PlayLog.Snap snap = PlayLog.Read(Path.Combine(dir, $"{id}_start.json"));
                var want = run.Turns[(i, snap.Attempt)];
                Map11Session.PickChapterFrom(snap, snapRule: true);
                Map11State st = Map11Session.State!;
                var turns = new List<string>();
                bool where = Chapter1.Index == i && Chapter1.Attempt == snap.Attempt && st.Seed == snap.StageSeed && Chapter1.Rule.Name == rule.Name && st.CarriedIn;
                PlayStage(st, turns);
                int firstDiff = Enumerable.Range(0, Math.Min(turns.Count, want.Count)).FirstOrDefault(k => turns[k] != want[k], -1);
                bool eq = turns.SequenceEqual(want);
                write($"  (b) {id} の始めの写しから: ステージ {Chapter1.CurrentStage.Id}・配り {Chapter1.Attempt + 1} 回目・seed {st.Seed}・持ち越し {st.CarriedIn}・刻 {turns.Count} ／ {want.Count}（1-1 から通したとき）・1 刻ずつ一致 {eq}{(firstDiff >= 0 ? $"（{firstDiff} 刻目で違う）" : "")}"
                      + $"・{(st.Won ? "勝ち" : "負け")} ／ {(Chapter1.Records[i].Stage.Id == id ? "" : "?")}写しの前のステージ {string.Join("・", Chapter1.Records.Take(i).Select(r => r.Result))}");
                if (firstDiff >= 0) write($"        want {want[firstDiff]}\n        got  {turns[firstDiff]}");
                okB &= where && eq && Chapter1.Records.Take(i).All(r => r.Skipped);
            }
            Req(okB, "(b) `--map-chapter-from=<1-2 ／ 1-3 の始めの写し>` で始めると、1 章を 1-1 から通して入ったときと同じ seed なら 1 刻ずつ一致する");

            // ---- (e) 手で写した写し ----
            string manual = PlayLog.ManualDir(realPlays);
            var mans = Directory.Exists(manual) ? Directory.EnumerateFiles(manual, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList() : new List<string>();
            bool okE = mans.Count >= 9;
            foreach (string f in mans)
            {
                try
                {
                    PlayLog.Snap s = PlayLog.Read(f);
                    ChapterRule own = ChapterRule.ByKey(s.Rule) ?? throw new InvalidDataException($"版が無い {s.Rule}");
                    var mine = PlayLog.ToCarry(s.Pack!, Chapter1.StagesFor(own)[s.StageIndex].Stars);
                    var now = PlayLog.ToCarry(s.Pack!, Chapter1.StagesFor(rule)[s.StageIndex].Stars);
                    bool seedOk = Chapter1.StageSeed(s.ChapterSeed, s.StageIndex, s.Attempt) == s.StageSeed;
                    int stars1 = now.Stars?.Count(kv => kv.Value.Star == 1) ?? 0, stars3 = now.Stars?.Count(kv => kv.Value.Star == 3) ?? 0;
                    write($"  (e) {Path.GetFileName(f)}: {s.Label}・駒 {mine.Units}・seed {s.StageSeed}（1 章の seed から {(seedOk ? "一致" : "×")}）・今の版で ☆1 {stars1} ／ ☆3 {stars3}・推定 {s.Estimated.Count} 件");
                    okE &= s.Estimated.Count > 0 && s.Notes.Count > 0 && seedOk && mine.Units == now.Units && mine.Units > 0 && s.Entry;
                }
                catch (Exception ex) { write($"  (e) × {Path.GetFileName(f)}: {ex.Message}"); okE = false; }
            }
            Req(okE, $"(e) 手で写した第461〜464期の写し {mans.Count} 本が読める（推定の注つき・ステージの seed が 1 章の seed から出る・今の版でも写しの版でも駒が立つ）");

            // ---- (c) 写しの版 ／ 今の版 ----
            {
                PlayLog.Snap? s461 = mans.Select(PlayLog.Read).FirstOrDefault(s => s.ChapterSeed == 21323 && s.Stage == "1-3");
                PlayLog.Snap? s464 = mans.Select(PlayLog.Read).FirstOrDefault(s => s.ChapterSeed == 186678 && s.Stage == "1-3");
                bool okC = s461 is not null && s464 is not null;
                if (okC)
                {
                    var parts = new List<string>();
                    foreach (var (s, wantRule) in new[] { (s461!, ChapterRule.V461), (s464!, ChapterRule.V464) })
                    {
                        Map11Session.PickChapterFrom(s, snapRule: true);
                        MapStage a = Map11Session.State!.Stage;
                        bool snapOk = Chapter1.Rule.Name == wantRule.Name && a.FoeScale == wantRule.ScaleOf(2);
                        int heroA = a.Hero.Unit!.MaxHp;
                        string starA = Map11Session.State.Stage.Stars.On ? $"☆{Map11Session.State.Stars.Values.Count(v => v.Star == 3)}枚が☆3" : "☆なし";
                        Map11Session.PickChapterFrom(s, snapRule: false, now: nowRule);
                        MapStage b = Map11Session.State!.Stage;
                        bool nowOk = Chapter1.Rule.Name == nowRule.Name && b.FoeScale == nowRule.ScaleOf(2);
                        int heroB = b.Hero.Unit!.MaxHp;
                        okC &= snapOk && nowOk && a.FoeScale != b.FoeScale && heroA != heroB;
                        parts.Add($"{s.ChapterSeed} の 1-3: 写しの版 {wantRule.Key} {a.FoeScale.Text}（勇者 HP {heroA}・{starA}）／ 今の版 {nowRule.Key} {b.FoeScale.Text}{(b.FoeScale.HeroMends ? "" : "・勇者の回復なし")}（勇者 HP {heroB}・☆{Map11Session.State.Stars.Values.Count(v => v.Star == 3)}枚が☆3）");
                    }
                    foreach (string p in parts) write($"  (c) {p}");
                }
                Req(okC, "(c) 写しの版で始める ／ 今の版で始める、の切替が効く（敵の倍率・勇者の HP が替わる・既定は今の版）");
            }
            Chapter1.End();

            // ---- (d) 敵の重さの表 ----
            {
                // 第462期 §3: ポンの 1-3 の第2隊（第461期の写し・ハギあり ／ ハギ抜き）× 作戦マップの勇者 ×1.0 ／ ×1.3 ／ ×1.6（回復あり）× 20 seed・レリックなし
                var squads = FoeWeight.SquadsOf(PlayLog.List(manual), "1-3", realPlays).Where(q => q.Source.Contains("21323") && q.No == 2).ToList();
                bool okD = squads.Count == 1 && squads[0].Pieces.Any(p => p.Base.Id == UnitCatalog.Hagi.Id);
                var expect = new[] { ("ハギあり", 100, 3.0, 0.00), ("ハギあり", 130, 3.0, 0.00), ("ハギあり", 160, 4.0, 0.00),
                                     ("ハギ抜き", 100, 5.0, 0.00), ("ハギ抜き", 130, 6.0, 0.15), ("ハギ抜き", 160, 7.0, 1.20) };
                if (okD)
                {
                    FoeWeight.Squad with462 = squads[0];
                    FoeWeight.Squad without = with462 with { Pieces = with462.Pieces.Where(p => p.Base.Id != UnitCatalog.Hagi.Id).ToList() };
                    write("  (d) 第462期 §3 の再現（第461期の写しの 1-3 第2隊・レリックなし・勇者は満タンから・seed 0〜19）: 隊 | 勇者 | 勝ち | 決着のターン（中央値） | 失った枚数 | 第462期の報告");
                    foreach (var (name, pct, turns, lost) in expect)
                    {
                        var sc = new MapFoeScale(pct, pct);
                        MapStage s = FoeWeight.StageAt(2, sc);
                        FoeWeight.Foe hero = FoeWeight.FoesOf(s).Single(f => f.Kind == FoeWeight.Kind.Hero);
                        var q = name == "ハギあり" ? with462 : without;
                        var fights = Enumerable.Range(0, 20).Select(seed => FoeWeight.Run(FoeWeight.Players(q, 0, StarRule.V464, relics: false), FoeWeight.Enemies(hero, s), seed)).ToList();
                        var cell = new FoeWeight.Cell(q, 0, hero, sc, fights);
                        bool hit = cell.Wins == 20 && Math.Abs(cell.MedTurns - turns) < 1e-9 && Math.Abs(cell.Lost - lost) < 0.005;
                        okD &= hit;
                        write($"      {name} | {sc.Text}（HP {s.Hero.Unit!.MaxHp}・回復 {sc.HeroMend(BossMendTrait.Fixed300)}） | {cell.Wins} ／ 20 | {cell.MedTurns:0.#} | {cell.Lost:F2} | 20 ／ 20・{turns:0.#}・{lost:F2} {(hit ? "○" : "×")}");
                    }
                }
                // 表そのもの（手で写した写し ＋ この門の器具の写し・出力は .tmp）
                string outDir = Path.Combine(tmp, "weights");
                var args = new List<string> { "--foe-weight", $"--plays={realPlays}", $"--out={outDir}", quick ? "--stage=1-2" : "--stage=1-2,1-3", $"--rule={rule.Key}" };   // 第467期: 第465期の版に固定
                if (quick) args.Add("--stars=as");
                bool ran = FoeWeight.Main(write, args, demoDir);
                var outs = Directory.Exists(outDir) ? Directory.GetFiles(outDir, "*.md") : Array.Empty<string>();
                bool tables = outs.Length == (quick ? 1 : 2) && outs.All(f => File.ReadAllText(f).Contains("## 帯から外れた組") && File.ReadAllText(f).Contains("## 勇者に 1 隊ずつ順に当てる"));
                write($"  (d) 表: {string.Join("・", outs.Select(Path.GetFileName))}");
                Req(okD && ran && tables, "(d) `--foe-weight` が 1-2 ・ 1-3 の表を出す（帯から外れた組 ・ 勇者の通しつき）。第462期 §3 の「ハギ × 勇者」の行（同じ隊・同じ倍率・同じ seed）を再現する");
            }
        }
        finally
        {
            Map12ChangeSmoke.FightHook = null;
            PlayLog.Disable();
            Chapter1.End();
        }
        return ok;
    }
}
