using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// dense326 の Phase 0 と本測定（第326期）。
static partial class Dense326Diag
{
    // ---------------------------------------------------------------------------------
    // §2-1 段0 の Phase 0（直す前に調べる）と、直した後
    // ---------------------------------------------------------------------------------
    static void P0Stage0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §2-1 段0 の Phase 0 —— シガの怖気の判定のずれ");
        Console.WriteLine();
        Console.WriteLine("## 1. ポンの台 × ボス × seed 10（段0 前 ＝ 第325期の規定のシガ）: 怖気づいた手番の直前の鞭");
        Console.WriteLine();
        {
            var w = WaveOf("boss");
            var p = BattleEngine.Materialize(Apply(PonBoard(), Old), BattleContext.PlayerTeam);
            var r = BattleEngine.Run(p, w.Make(), 10, verbose: true);
            var t = r.TallyByUnit["shiga"];
            Console.WriteLine($"{(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・シガの手番 {t.TurnsTaken}・怖気 {t.WhipCowered}（うち当たる前の2倍が入った主目標 {t.Fear326CowerDoubled}・そのうち感電の2倍 {t.Fear326CowerDoubledShock}）。"
                + $"怖気の判定 {t.Fear326Judged} 回: 当たる前だけ動けない {t.Fear326PreOnly}・当たった後だけ動けない {t.Fear326PostOnly}・両方 {t.Fear326Both}。");
            Console.WriteLine();
            Console.WriteLine("ログの抜き書き（怖気の直前の手番の鞭・「二重に入る」「感電が弾けた（起点）」「萎縮」「怖気」の行）:");
            Console.WriteLine();
            Console.WriteLine("```");
            var lines = r.Log.Select(l => l.ToString()).ToList();
            int turn = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("--- ターン", StringComparison.Ordinal)) turn = int.Parse(new string(lines[i].Where(char.IsDigit).ToArray()));
                if (!lines[i].Contains("怖気づいた", StringComparison.Ordinal)) continue;
                int j = i;
                while (j > 0 && !(lines[j].StartsWith("  電気鞭のシガ →", StringComparison.Ordinal))) j--;
                Console.WriteLine($"T{turn}:");
                for (int k = j; k <= i; k++)
                    if (k == j || lines[k].Contains("二重に入る", StringComparison.Ordinal) || lines[k].Contains("感電が弾けた（起点）", StringComparison.Ordinal)
                        || lines[k].Contains("萎縮", StringComparison.Ordinal) || lines[k].Contains("腕が縮んだ", StringComparison.Ordinal) || lines[k].Contains("怖気", StringComparison.Ordinal)
                        || lines[k].Contains("組み付", StringComparison.Ordinal) || lines[k].Contains("痺れ", StringComparison.Ordinal))
                        Console.WriteLine(lines[k]);
            }
            Console.WriteLine("```");
        }
        Console.WriteLine();
        Console.WriteLine("## 2. 代表台（シガのいる台）× 10 波（seed 0..199・1戦あたり）: 段0 前 → 段0 の後");
        Console.WriteLine();
        Console.WriteLine("怖気 ＝ シガが怖気づいた回数（段0 前 → 後）。2倍の後 ＝ 段0 前の怖気のうち、主目標に当たる前の2倍が入っていた（動けない ／ 感電）回数 ＝ **段0 のずれ**。"
            + "前だけ ＝ 主目標が当たる前は動けず当たった後に動けた判定（指示書の見込みの形）・後だけ ＝ 当たる前は動けて当たった後に動けない（いまは怖気づかない・段0 でも同じ）。"
            + "手番 ＝ シガの手番数（失った手番を含まない）。勝率 ／ 倒しT は段0 前 → 後。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 倒しT | シガの手番 | 怖気 | 2倍の後（うち感電）| 前だけ ／ 後だけ ／ 両方 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        var tot = new Dictionary<string, (Z O, Z N)>();
        foreach (var (bn, bf) in Boards().Where(b => b.F.Occupied().Any(o => o.Def.Id == "shiga")))
            foreach (var w in Waves)
            {
                var o = Measure(Apply(bf, Old), w); var n = Measure(Apply(bf, Base), w);
                string key = w.Boss ? "ボス" : IsElite(w) ? "精鋭 ／ 九体" : "本編";
                if (!tot.ContainsKey(key)) tot[key] = (new Z(), new Z());
                tot[key].O.Merge(o); tot[key].N.Merge(n);
                Console.WriteLine($"| {bn} | {w.Name} | {F1(o.Win)} → {F1(n.Win)} | {Per(o.Base.WinT, o.Wins)} → {Per(n.Base.WinT, n.Wins)} | {Per(o.S.TurnsTaken, o.N)} → {Per(n.S.TurnsTaken, n.N)} | {Per(o.S.WhipCowered, o.N)} → {Per(n.S.WhipCowered, n.N)} | {Per(o.S.Fear326CowerDoubled, o.N)}（{Per(o.S.Fear326CowerDoubledShock, o.N)}） | {Per(o.S.Fear326PreOnly, o.N)} ／ {Per(o.S.Fear326PostOnly, o.N)} ／ {Per(o.S.Fear326Both, o.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("### 波の種類ごとの合計（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 怖気 段0 前 → 後 | 2倍の後（うち感電） | 段0 で怖気づかなかった | シガの手番 前 → 後 |");
        Console.WriteLine("|---|---|---|--:|---|");
        foreach (var (k, (o, n)) in tot)
            Console.WriteLine($"| {k} | {Per2(o.S.WhipCowered, o.N)} → {Per2(n.S.WhipCowered, n.N)} | {Per2(o.S.Fear326CowerDoubled, o.N)}（{Per2(o.S.Fear326CowerDoubledShock, o.N)}） | {Per2(n.S.Fear326DoubledSpared, n.N)} | {Per(o.S.TurnsTaken, o.N)} → {Per(n.S.TurnsTaken, n.N)} |");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 本体の Phase 0（規定 ＝ 段0 入り）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3-2 本体の Phase 0 —— 規定（E3 ＋ 段0）で、放電の受け手と濃さ（seed 0..199・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("## 1. ポンの台 × ボス: 勇者が浴びた放電の回数と出し手の内訳・そのときの勇者の濃さ・DR-a で増える量の見込み");
        Console.WriteLine();
        var pon = Measure(PonBoard(), WaveOf("boss"));
        var t = pon.T;
        Console.WriteLine($"勝率 {F1(pon.Win)}・倒しT {Per(pon.Base.WinT, pon.Wins)}・勇者の濃さの最大 {pon.HeroDensMax}。");
        Console.WriteLine();
        Console.WriteLine("放電 ＝ HP を減らす放電の本数（糸を伝った分を含む）。名目 ／ 減らした ＝ 1本あたり。受け手の濃さ ＝ 浴びたときの受け手の濃さの平均（DR-a の上乗せ ＝ 8 × これ）。DR-a の見込み ＝ 本数 × 8 ×（1 ＋ 受け手の濃さ）（出し手の濃さは消える）。");
        Console.WriteLine();
        Console.WriteLine("| 受け手 | 出し手 | 放電 | 名目 ／ 本 | 減らした ／ 本 | 受け手の濃さ（平均）| 濃さ ≧1 の本数 | 出し手の濃さ（平均）| DR-a の見込み（名目の合計）|");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        PrintDr(pon);
        Console.WriteLine();
        Console.WriteLine("勇者の濃さ（ターンの終わり・生きている戦の平均）と、勇者に入った与ダメ ／ 回復（1ターンあたり）:");
        Console.WriteLine();
        Console.WriteLine("| T | 生きている戦 | 濃さ | 与ダメ | 回復 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|");
        for (int k = 1; k <= 20; k++)
            if (pon.HeroAlive[k] > 0) Console.WriteLine($"| {k} | {pon.HeroAlive[k]} | {Per(pon.HeroDens[k], pon.HeroAlive[k])} | {Per0(pon.HeroDmg[k], pon.HeroAlive[k])} | {Per0(pon.HeroHeal[k], pon.HeroAlive[k])} |");
        Console.WriteLine();
        Console.WriteLine("勇者に入った与ダメの出どころ（1戦あたり）: " + string.Join("・", DmgSrc.Select((s, i) => $"{s} {Per0(pon.HeroDmgBySrc[i], pon.HeroN)}")));
        Console.WriteLine();
        Console.WriteLine("## 2. 糸玉（ポンの台 ／ 糸 ／ 光の盾 × ボス）");
        Console.WriteLine();
        Console.WriteLine("糸玉に届いた放電 ＝ HP の無い糸玉が受けた放電（受けるだけ・帯電していれば連鎖に積む）。糸玉が弾けた ＝ 糸玉の感電が弾けた延べ。糸玉の放電 駒へ ＝ 弾けた糸玉が隣の駒（勇者・召喚獣）へ出した放電の本数 ／ 減らした HP。"
            + "糸玉 → 勇者 ＝ 勇者が糸玉から浴びた放電（本数・名目 ／ 本・浴びたときの勇者の濃さ）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 糸玉に届いた放電 | 糸玉が弾けた | 糸玉の放電 駒へ（本 ／ 減らした） | 糸玉 → 勇者 本 ／ 名目 ／ 濃さ | DR-a の見込み（糸玉 → 勇者・名目の合計）|");
        Console.WriteLine("|---|--:|--:|--:|---|---|--:|");
        foreach (var (bn, f) in new[] { ("ポンの台", PonBoard()), ("糸", Playtest("試遊・感電 糸")), ("光の盾", Playtest("試遊・感電 光の盾")) })
        {
            var z = bn == "ポンの台" ? pon : Measure(f, WaveOf("boss"));
            int i = 1 * 4 + 1;
            Console.WriteLine($"| {bn} | {F1(z.Win)} | {Per(z.Base.G.SilkDisIn, z.N)} | {Per(z.Base.G.SilkPops, z.N)} | {Per(z.Base.G.SilkDisToUnit, z.N)} ／ {Per0(z.Base.G.SilkDealt, z.N)} | {Per(H(z.T.Dr326N, i), z.N)} ／ {Per(H(z.T.Dr326Nom, i), H(z.T.Dr326N, i))} ／ {Per(H(z.T.Dr326RecvDens, i), H(z.T.Dr326N, i))} | {Per0(8 * (H(z.T.Dr326N, i) + H(z.T.Dr326RecvDens, i)), z.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 3. 精鋭（近衛 ／ 大隊）・九体: 濃い敵が浴びる放電（代表台・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 倒しT | 敵が浴びた放電 | うち受け手の濃さ ≧1 | 受け手の濃さ（平均）| 名目 ／ 本 | DR-a の上乗せの見込み（名目） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (bn, f) in Boards().Where(b => b.F.Occupied().Any(o => o.Def.Id == "tou")))
            foreach (var w in Waves.Where(IsElite))
            {
                var z = Measure(f, w);
                long n = Sum(z.T.Dr326N, 1), dn = Sum(z.T.Dr326RecvDense, 1), rd = Sum(z.T.Dr326RecvDens, 1), nom = Sum(z.T.Dr326Nom, 1);
                Console.WriteLine($"| {bn} | {w.Name} | {F1(z.Win)} | {Per(z.Base.WinT, z.Wins)} | {Per(n, z.N)} | {Per(dn, z.N)} | {Per2(rd, n)} | {Per(nom, n)} | {Per0(8 * rd, z.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 4. 味方が浴びる放電と、そのときの味方の濃さ（トウのいる代表台・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("味方の放電 ＝ 味方が浴びた放電（本数・減らした HP）。濃さ ≧1 ＝ 浴びたとき受け手の味方に濃さがあった本数。DR-a の上乗せの見込み ＝ 8 × 受け手の濃さの和（名目）。反転 ＝ ベニの結界で回復になった放電。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 味方の放電 本 ／ 減らした | 濃さ ≧1 | 受け手の濃さ（平均）| DR-a の上乗せの見込み | 放電で倒れた味方 | 反転 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|");
        foreach (var (bn, f) in Boards().Where(b => b.F.Occupied().Any(o => o.Def.Id == "tou")))
            foreach (var w in Waves.Where(w => w.Boss || IsElite(w) || w.Name is "本編 第2波" or "本編 第5波"))
            {
                var z = Measure(f, w);
                long n = Sum(z.T.Dr326N, 0), dl = Sum(z.T.Dr326Dealt, 0), dn = Sum(z.T.Dr326RecvDense, 0), rd = Sum(z.T.Dr326RecvDens, 0);
                Console.WriteLine($"| {bn} | {w.Name} | {F1(z.Win)} | {Per(n, z.N)} ／ {Per0(dl, z.N)} | {Per(dn, z.N)} | {Per2(rd, n)} | {Per0(8 * rd, z.N)} | {Per2(z.Base.AllyDisDeaths, z.N)} | {Per(z.T.Dr326Inverted, z.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static void PrintDr(Z z)
    {
        for (int r = 0; r < 3; r++)
            for (int s = 0; s < 4; s++)
            {
                int i = r * 4 + s;
                long n = H(z.T.Dr326N, i);
                if (n == 0) continue;
                long rd = H(z.T.Dr326RecvDens, i);
                Console.WriteLine($"| {Recv[r]} | {Send[s]} | {Per(n, z.N)} | {Per(H(z.T.Dr326Nom, i), n)} | {Per(H(z.T.Dr326Dealt, i), n)} | {Per2(rd, n)} | {Per(H(z.T.Dr326RecvDense, i), z.N)} | {Per2(H(z.T.Dr326SendDens, i), n)} | {Per0(8 * (n + rd), z.N)} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // §5-1 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = new Dictionary<(string, string, string), Z>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v), w);
        var guza = new Dictionary<(string, string), int>();
        foreach (var (bn, bf) in boards.Where(b => b.F.Occupied().Any(o => o.Def.Id == "tou")))
            foreach (var w in Waves)
                if (vers.Any(v => res[(bn, v.Name, w.Name)].Wins * 2 >= Seeds))
                    guza[(bn, w.Name)] = WinsLite(S316.SwapId(bf, "tou", UnitCatalog.Guza), w);
        Console.WriteLine($"# {Tag} §5-1 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("規定 ＝ E3 ＋ 段0（いまの規定）。DR-b＋SA+ はシガも SA+（段0 入り・`ShigaSApF`）に替える。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307Seats(bf)}");
        Console.WriteLine();
        Console.WriteLine("## 1. 勝率（括弧は規定との差）と対照（トウ → グザ・どれかの版で 50% 以上のセルだけ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var (bn, _) in boards)
        {
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    double a = res[(bn, v.Name, w.Name)].Win, b0 = res[(bn, vers[0].Name, w.Name)].Win;
                    return v == vers[0] || a == b0 ? F1(a) : $"{F1(a)}（{Sg(a - b0)}）";
                })) + " |");
            if (guza.Keys.Any(k => k.Item1 == bn))
                Console.WriteLine($"| {bn} | 対照 グザ | " + string.Join(" | ", Waves.Select(w => guza.TryGetValue((bn, w.Name), out int d) ? F1(100.0 * d / Seeds) : "—")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 2. 倒しT ／ 負けT ／ トウの寿命（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w => WinT(res[(bn, v.Name, w.Name)]))) + " |");
        Console.WriteLine();
        Console.WriteLine("## 3. ボス: 1ターンに勇者へ入った与ダメと勇者の回復（生きている戦の平均）・回復を上回り始めたターン");
        Console.WriteLine();
        Console.WriteLine("セル ＝ 与ダメ ／ 回復（そのターンに勇者が生きている戦の平均）。勇者の回復は毎ターンの頭に「減った分まで・最大HP の 40%（1,200）まで」なので、与ダメが 1,200 に届くまでは全部戻される。"
            + "**上回り ＝ 1ターンの与ダメの平均が回復の上限 1,200 に初めて届いたターン**（生きている戦が 1/4 以上のターンだけ・届かなければ —）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 倒しT | T1 | T3 | T5 | T7 | T10 | 上回り | 勇者の濃さ T5 ／ T10 ／ 最大 |");
        Console.WriteLine("|---|---|--:|--:|---|---|---|---|---|---|---|");
        string Cell(Z z, int k) => z.HeroAlive[k] == 0 ? "—" : $"{Per0(z.HeroDmg[k], z.HeroAlive[k])} ／ {Per0(z.HeroHeal[k], z.HeroAlive[k])}";
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
            {
                var z = res[(bn, v.Name, "ボス")];
                Console.WriteLine($"| {bn} | {v.Name} | {F1(z.Win)} | {Per(z.Base.WinT, z.Wins)} | {Cell(z, 1)} | {Cell(z, 3)} | {Cell(z, 5)} | {Cell(z, 7)} | {Cell(z, 10)} | {Cross(z)} | {Per(z.HeroDens[5], z.HeroAlive[5])} ／ {Per(z.HeroDens[10], z.HeroAlive[10])} ／ {z.HeroDensMax} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 4. ボス: 勇者に入った与ダメの出どころ（1戦あたり）と、勇者が浴びた放電");
        Console.WriteLine();
        Console.WriteLine("放電（敵の受け手）＝ 勇者 ＋ 精鋭の敵が浴びた放電（ボス波では勇者だけ）: 本数 ／ 名目 ／ 本 ／ 減らした HP の合計。召喚獣 ／ 糸玉 ／ 糸 ／ ほか ＝ 出し手ごとの本数（名目 ／ 本）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", DmgSrc) + " | 放電 本 ／ 名目 ／ 減らした | 召喚獣 | 糸玉 | 糸 | ほか |");
        Console.WriteLine("|---|---|" + string.Concat(DmgSrc.Select(_ => "--:|")) + "---|---|---|---|---|");
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
            {
                var z = res[(bn, v.Name, "ボス")];
                long n = Sum(z.T.Dr326N, 1);
                string Sx(int s) { long c = H(z.T.Dr326N, 4 + s); return c == 0 ? "—" : $"{Per(c, z.N)}（{Per0(H(z.T.Dr326Nom, 4 + s), c)}）"; }
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", z.HeroDmgBySrc.Select(x => Per0(x, z.HeroN))) + $" | {Per(n, z.N)} ／ {Per0(Sum(z.T.Dr326Nom, 1), n)} ／ {Per0(Sum(z.T.Dr326Dealt, 1), z.N)} | {Sx(0)} | {Sx(1)} | {Sx(2)} | {Sx(3)} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 5. 波 × 版: 敵が浴びた放電・焼・カタの雷・シガ・糸玉・マイナス（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("敵の放電 ＝ 本 ／ 名目 ／ 本（喚ばれたもの・糸玉を除く敵）。焼 ＝ 濃い雷が弾けた本人を焼いた量（敵）。カタの雷 ＝ 雷の与ダメ。シガ ＝ 手番 ／ 怖気 ／ 与ダメ。糸玉 ＝ 糸玉に届いた放電 ／ 糸玉の放電が駒に与えた量。"
            + "味方の放電被弾 ＝ 味方が浴びた放電で減らした HP ／ 放電で倒れた味方 ／ 味方の焼。1回の最大 ＝ 名目の放電1本の最大（全戦）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 敵の放電 | 焼 | カタの雷 | シガ 手番 ／ 怖気 ／ 与ダメ | 糸玉 | 味方の放電被弾 ／ 倒れた ／ 焼 | 1回の最大 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|---|---|---|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var z = res[(bn, v.Name, w.Name)];
                    long n = Sum(z.T.Dr326N, 1);
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {F1(z.Win)} | {Per(n, z.N)} ／ {Per0(Sum(z.T.Dr326Nom, 1), n)} | {Per0(z.T.TdBurnFoe, z.N)} | {Per0(z.Base.K.ThunderDealt, z.N)} | {(z.ShigaN == 0 ? "—" : $"{Per(z.S.TurnsTaken, z.N)} ／ {Per(z.S.WhipCowered, z.N)} ／ {Per0(z.S.DamageToEnemy, z.N)}")} | {(z.Base.G.SilkDisIn == 0 ? "—" : $"{Per(z.Base.G.SilkDisIn, z.N)} ／ {Per0(z.Base.G.SilkDealt, z.N)}")} | {Per0(z.Base.AllyDisTaken, z.N)} ／ {Per2(z.Base.AllyDisDeaths, z.N)} ／ {Per0(z.T.TdBurnAlly, z.N)} | {Math.Max(z.T.Dr326Max, z.Base.DisMax)} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 6. 強すぎの兆し（§3-1: 1回の放電が 1,000 超 ／ 精鋭 ／ 九体の 1T 決着 5% 以上 ／ ボスの倒しT が 4T 未満）と格子を回す版（§5-3: ±10pt（ボスは ±5pt または倒しT ±3T））");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var hits = new List<string>(); var strong = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)]; var b0 = res[(bn, vers[0].Name, w.Name)];
                    double d = z.Win - b0.Win;
                    double tv = z.Wins > 0 ? (double)z.Base.WinT / z.Wins : 0, tb = b0.Wins > 0 ? (double)b0.Base.WinT / b0.Wins : 0;
                    double dt = z.Wins > 0 && b0.Wins > 0 ? tv - tb : 0;
                    bool hit = w.Boss ? Math.Abs(d) >= 5 || Math.Abs(dt) >= 3 : Math.Abs(d) >= 10;
                    if (hit) hits.Add($"{bn} × {w.Name}（{Sg(d)}pt{(w.Boss ? $"・倒しT {Sg(dt)}" : "")}）");
                    if (w.Boss && z.Wins > 0 && tv < 4) strong.Add($"{bn} × ボス 倒しT {F1(tv)}");
                    if (IsElite(w) && z.Base.OneT * 20 >= z.N) strong.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.Base.OneT, z.N)}%");
                    long mx = Math.Max(z.T.Dr326Max, z.Base.DisMax);
                    if (mx > 1000) strong.Add($"{bn} × {w.Name} 1回の放電 {mx}");
                }
            Console.WriteLine($"- **{v.Name}**: 強すぎの兆し {(strong.Count == 0 ? "なし" : string.Join("・", strong))}。格子 {(hits.Count == 0 ? "該当なし（回さない）" : $"{hits.Count} セル → 回す。" + string.Join("・", hits))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static string S307Seats(Formation f) => Som307Diag.Seats(f);

    // ---------------------------------------------------------------------------------
    // §5-2 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        double[,] Grid(Ver v)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, v);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }
        var b = Grid(vers[0]);
        Console.WriteLine($"# {Tag} §5-2 `compare` {rows.Length} 行 × 版（seed 0..{Seeds - 1}・基準は規定 ＝ E3 ＋ 段0）");
        Console.WriteLine();
        Console.WriteLine($"トウのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "tou")).Select(r => r.Name))}");
        Console.WriteLine();
        Console.WriteLine($"シガのいる行（DR-b＋SA+ で動きうる）: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "shiga")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), vers[vi].Shiga == vers[0].Shiga ? "tou" : "shiga");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5-3 格子（第324期と同じ作り: 固定枠 トウ ＋ カタ（ボスはさらにソム）・探索枠は残り・駒はいまの規定）
    // ---------------------------------------------------------------------------------
    static void Grid(string waveKey, Ver v)
    {
        var w = WaveOf(waveKey);
        var fixedU = w.Boss ? new[] { UnitCatalog.Tou, UnitCatalog.Kata, UnitCatalog.Som } : new[] { UnitCatalog.Tou, UnitCatalog.Kata };
        int free = 5 - fixedU.Length;
        UnitDef Now(UnitDef d) => UnitCatalog.All.FirstOrDefault(x => x.Id == d.Id) ?? d;
        var heal = S287.Heal.Select(d => d.Id).ToHashSet();
        var fixedIds = fixedU.Select(d => d.Id).ToHashSet();
        var pool = S287.Pool.Select(Now).Append(UnitCatalog.Shiga).Where(d => !fixedIds.Contains(d.Id)).GroupBy(d => d.Id).Select(g => g.First()).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var combos = new List<UnitDef[]>();
        void Rec(int start, List<UnitDef> cur)
        {
            if (cur.Count == free) { if (cur.Count(d => heal.Contains(d.Id)) <= 1) combos.Add(cur.ToArray()); return; }
            for (int i = start; i < pool.Length; i++) { cur.Add(pool[i]); Rec(i + 1, cur); cur.RemoveAt(cur.Count - 1); }
        }
        Rec(0, new List<UnitDef>());
        int step = free >= 3 ? 4 : 1;   // 第323〜324期と同じ間引き（近衛は 120 席のうち 30 席）
        var boards = new List<UnitDef[]>();
        foreach (var c in combos) boards.AddRange(B283.Perms(fixedU.Concat(c).ToArray()).Where((_, i) => i % step == 0));
        const int cut = 20;
        var res = new (int B, int V, int G, long Tb, long Tv)?[boards.Count];
        (int W, long T) Wins(Formation f, int n)
        {
            int c = 0; long t = 0;
            for (int sd = 0; sd < n; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                if (r.PlayerWon) { c++; t += r.Turns; }
            }
            return (c, t);
        }
        Parallel.For(0, boards.Count, i =>
        {
            var f0 = B283.Seat(boards[i]);
            var fv = Apply(f0, v);
            if (Wins(f0, cut).W < 5 && Wins(fv, cut).W < 5) return;
            var (wb, tb) = Wins(f0, Seeds); var (wv, tv) = Wins(fv, Seeds);
            int g = wb * 2 >= Seeds || wv * 2 >= Seeds ? Wins(S316.SwapId(f0, "tou", UnitCatalog.Guza), Seeds).W : -1;
            res[i] = (wb, wv, g, tb, tv);
        });
        var all = res.Where(r => r is not null).Select(r => r!.Value).ToList();
        bool Half(int x) => x * 2 >= Seeds;
        bool Need(int x, int g) => Half(x) && g >= 0 && g * 2 <= x;
        Console.WriteLine($"# {Tag} §5-3 格子 —— {w.Name} × 規定 ／ {v.Name}（固定枠 {string.Join(" ＋ ", fixedU.Select(Short))}・探索枠{free}（候補 {pool.Length} 枚: {string.Join("・", pool.Select(Short))}・ヒーラーは 1 枚まで）{combos.Count} 組・席 {120 / step} ＝ {boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{cut - 1}（どちらかで 5 勝以上・{all.Count:N0} 台）→ seed 0..{Seeds - 1}。**トウが要る台** ＝ 50% 以上 かつ トウ → グザで半分以下。所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
        Console.WriteLine();
        long sb = all.Sum(r => (long)r.B), sv = all.Sum(r => (long)r.V);
        Console.WriteLine($"| 段 | 規定 | {v.Name} |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 勝ちの延べ（届いた量） | {sb:N0} | {sv:N0} |");
        Console.WriteLine($"| 勝率 ≧ 50% | {all.Count(r => Half(r.B)):N0} | {all.Count(r => Half(r.V)):N0} |");
        Console.WriteLine($"| **トウが要る** | {all.Count(r => Need(r.B, r.G)):N0} | {all.Count(r => Need(r.V, r.G)):N0} |");
        Console.WriteLine($"| 勝率 ≧ 90% | {all.Count(r => r.B * 10 >= Seeds * 9):N0} | {all.Count(r => r.V * 10 >= Seeds * 9):N0} |");
        Console.WriteLine($"| 倒しT（勝ちの平均） | {Per(all.Sum(r => r.Tb), sb)} | {Per(all.Sum(r => r.Tv), sv)} |");
        Console.WriteLine($"| 版 − 規定 の平均（pt） | — | {(all.Count == 0 ? 0 : all.Average(r => 100.0 * (r.V - r.B) / Seeds)):+0.0;-0.0} |");
        Console.WriteLine($"| 版が +10pt 以上 ／ −10pt 以下 | — | {all.Count(r => r.V - r.B >= Seeds / 10):N0} ／ {all.Count(r => r.V - r.B <= -Seeds / 10):N0} |");
    }

    // ---------------------------------------------------------------------------------
    // §6 台本: ポンの台の放電の並び（受け手の濃さで重くなる）
    // ---------------------------------------------------------------------------------
    static void Memo(string wave, int seed, Ver v)
    {
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(Apply(PonBoard(), v), BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var x in r.Events)   // 戦の途中で湧いた駒（召喚獣・糸玉）
        {
            if (x.Kind == BattleEventKind.Summon && x.TargetId is int si && x.Text == UnitCatalog.Fodder.Name) names[si] = "召喚獣";
            if (x.Kind == BattleEventKind.SilkBall && x.TargetId is int bi && x.Text == SilkBallLabels.Place) names[bi] = "糸玉";
        }
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# dense326 memo —— ポンの台 × {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("`Discharge`（Amount ＝ 名目の量）・召喚獣 ／ 糸玉の `Damage` と 100 以上の `Damage`・`ShockSpent`・濃さ ／ 怖気の `ShockGauge`。最初の 160 件。");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1, lines = 0;
        foreach (var x in r.Events)
        {
            if (x.Kind is not (BattleEventKind.Discharge or BattleEventKind.ShockSpent or BattleEventKind.Damage or BattleEventKind.ShockGauge)) continue;
            if (x.Kind == BattleEventKind.ShockGauge && x.Text is not (ShockGaugeLabels.Dense or ShockGaugeLabels.DenseFlow or ShockGaugeLabels.DenseMove or ShockGaugeLabels.DensePop or ShockGaugeLabels.DenseBurn or ShockGaugeLabels.Cower)) continue;
            if (x.Kind == BattleEventKind.Damage && !(x.ActorId is int a && names.TryGetValue(a, out var an) && an is "召喚獣" or "糸玉" || x.Amount >= 100)) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.ReceiverDensity is int rd ? $"  ReceiverDensity {rd}" : "")}");
            if (++lines >= 160) { Console.WriteLine("…（以下略）"); break; }
        }
        Console.WriteLine("```");
    }
}
