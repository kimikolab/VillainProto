using System.Text.RegularExpressions;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// firescale moved ／ phase0 —— 前段（規定化で動いた行）と Q0-1〜Q0-5。
static partial class FireScaleDiag
{
    // =================================================================================
    // 前段: 規定化で動いた行（受け入れ 1）
    // =================================================================================
    static Dictionary<string, double[]> ReadBalance(string path)
    {
        var res = new Dictionary<string, double[]>();
        foreach (var raw in File.ReadAllLines(path))
        {
            string line = raw.TrimEnd('\r');
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            var c = line.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            if (c.Length < 6) continue;
            var v = c.Skip(1).Take(5).Select(x => double.Parse(x.TrimEnd('%'))).ToArray();
            res[c[0]] = v;
        }
        return res;
    }

    static partial void Moved()
    {
        string oldPath = _args is { Length: > 3 } ? _args[3] : throw new ArgumentException("firescale moved <旧 balance.md>");
        string newPath = _args is { Length: > 4 } ? _args[4] : Path.Combine("docs", "balance.md");
        var o = ReadBalance(oldPath); var n = ReadBalance(newPath);
        var rows = CompareBuilds();
        bool Holds(Formation f) => f.Occupied().Any(x => x.Def.Id is "borg" or "hiyo");
        if (o.Count != rows.Length || n.Count != rows.Length) throw new InvalidOperationException($"行数が合わない（旧 {o.Count} ／ 新 {n.Count} ／ compare {rows.Length}）");
        Console.WriteLine($"# 第239期 前段 `firescale moved` —— 規定化（ボルグ A+D2・ヒヨ V1）で `compare` の動いた行");
        Console.WriteLine();
        int holders = rows.Count(r => Holds(r.F));
        int otherCells = 0, holderMovedRows = 0, holderCells = 0;
        Console.WriteLine("| 行 | 主判定 | " + string.Join(" | ", Enumerable.Range(1, 5).Select(w => $"第{w}波")) + " | 第2〜5波の平均の差 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", 6)));
        foreach (var (name, f) in rows)
        {
            var a = o[name]; var b = n[name];
            int diff = Enumerable.Range(0, 5).Count(i => a[i] != b[i]);
            if (!Holds(f)) { otherCells += diff; continue; }
            if (diff == 0) continue;
            holderMovedRows++; holderCells += diff;
            string cells = string.Join(" | ", Enumerable.Range(0, 5).Select(i => a[i] == b[i] ? $"{b[i]:F1}" : $"{a[i]:F1} → **{b[i]:F1}**"));
            double d = Enumerable.Range(1, 4).Average(i => b[i] - a[i]);
            Console.WriteLine($"| {name} | {(Baseline.PrimaryRows.Contains(name) ? "○" : "")} | {cells} | {d:+0.0;-0.0;0.0} |");
        }
        Console.WriteLine();
        var still = rows.Where(r => Holds(r.F) && Enumerable.Range(0, 5).All(i => o[r.Name][i] == n[r.Name][i])).Select(r => r.Name).ToList();
        Console.WriteLine($"- ボルグ・ヒヨのいる行 **{holders} 行**のうち動いたのは **{holderMovedRows} 行・{holderCells} セル**。動かなかった {still.Count} 行: {string.Join("、", still)}");
        Console.WriteLine($"- **ボルグ・ヒヨのいない行 {rows.Length - holders} 行で動いたセル: {otherCells}**（受け入れ 1 は 0）");
        Console.WriteLine();
        Console.WriteLine("| 分母 | 版 | " + string.Join(" | ", Enumerable.Range(1, 5).Select(w => $"第{w}波")) + " | 情報セル（第2〜5波 0<x<100） | 第五波 95% 超 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", 7)));
        foreach (var (lab, pick) in new (string, Func<string, bool>)[] { ("全61行", _ => true), ("主判定19行", nm => Baseline.PrimaryRows.Contains(nm)) })
            foreach (var (vn, tab) in new[] { ("旧", o), ("新", n) })
            {
                var sel = rows.Where(r => pick(r.Name)).Select(r => tab[r.Name]).ToList();
                string avg = string.Join(" | ", Enumerable.Range(0, 5).Select(i => sel.Average(x => x[i]).ToString("F1")));
                int info = sel.Sum(x => Enumerable.Range(1, 4).Count(i => x[i] > 0 && x[i] < 100));
                int hi = sel.Count(x => x[4] > 95);
                Console.WriteLine($"| {lab}（{sel.Count}） | {vn} | {avg} | {info} | {hi} |");
            }
    }

    // =================================================================================
    // Phase 0
    // =================================================================================
    static string FindRoot()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "BattleCore", "Traits.cs"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("BattleCore/Traits.cs が見つからない");
    }

    /// <summary>ソースを1行ずつ見て、その行を囲むメソッド名（直前のメソッド宣言）を返す。</summary>
    static string[] EnclosingMethods(string[] lines)
    {
        var res = new string[lines.Length];
        // クラスの直下（字下げ 4）の宣言だけ。修飾子の無い private メソッド（`void BurnTickOnce(`）も拾う。
        var decl = new Regex(@"^    (?:(?:public|private|internal|protected|static|override|virtual|sealed|readonly|partial)\s+)*(?:void|bool|int|long|string|[A-Z][\w.]*(?:<[^>]*>)?(?:\[\])?\??|\([^)]*\))\s+(\w+)\s*\(");
        string cur = "";
        for (int i = 0; i < lines.Length; i++)
        {
            var m = decl.Match(lines[i]);
            if (m.Success && !lines[i].Contains(" new(") && !lines[i].TrimStart().StartsWith("//")) cur = m.Groups[1].Value;
            res[i] = cur;
        }
        return res;
    }

    /// <summary>Traits.cs の行 → 囲むクラス名（`public (sealed )?class X`）。</summary>
    static string[] EnclosingClasses(string[] lines)
    {
        var res = new string[lines.Length];
        var decl = new Regex(@"^(?:public|internal)\s+(?:\w+\s+)*(?:class|record|struct|enum)\s+(\w+)");
        string cur = "";
        for (int i = 0; i < lines.Length; i++) { var m = decl.Match(lines[i]); if (m.Success) cur = m.Groups[1].Value; res[i] = cur; }
        return res;
    }

    /// <summary>クラス名 → 札。抽象クラスは具象の子の札をすべて（開戦の撒き O1〜O4 など）。</summary>
    static Dictionary<string, TraitId[]> ClassToTrait()
    {
        var res = new Dictionary<string, TraitId[]>();
        var all = typeof(Trait).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Trait))).ToList();
        TraitId? IdOf(Type t) => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null ? ((Trait)Activator.CreateInstance(t)!).Id : null;
        foreach (var t in all)
        {
            var ids = t.IsAbstract ? all.Where(c => c.IsSubclassOf(t)).Select(IdOf).OfType<TraitId>().ToArray()
                                   : IdOf(t) is TraitId one ? new[] { one } : Array.Empty<TraitId>();
            if (ids.Length > 0) res[t.Name] = ids;
        }
        return res;
    }

    internal static IEnumerable<UnitDef> EnemyDefs()
    {
        var fs = EnemyCatalog.Stages.Select(s => s.Enemy).Concat(EnemyCatalog.Pattern3Copies.Select(s => s.Enemy));
        foreach (var f in fs) foreach (var (_, d) in f.Occupied()) yield return d;
        foreach (var s in EnemyCatalog.TestStages) foreach (var (_, d) in s.Enemy.Occupied()) yield return d;
    }

    static string ReadKind(string line)
    {
        if (Regex.IsMatch(line, @"StatusKeys\.\w+,\s*StatusKeys\.") || Regex.IsMatch(line, @",\s*StatusKeys\.Burn\s*[,}]")) return "種類（一覧の1本）";
        if (line.Contains("n++") || line.Contains(".Count(")) return "数（燃えている駒の数）";
        if (Regex.IsMatch(line, @"StatusKeys\.Burn\)\s*(>|<=|>=\s*1|==\s*0)\s*0?") ) return "二値（燃えているか）";
        if (Regex.IsMatch(line, @"StatusKeys\.Burn\)\s*>=\s*\w")) return "残りターン（比べる）";
        return "残りターン（値）";
    }

    static bool IsWrite(string line) => line.Contains("SetCounter(StatusKeys.Burn") || line.Contains("EmitStatusGain(") || line.Contains("LabelOf(StatusKeys.Burn)");

    static bool IsBook(string method, string line)
        => Regex.IsMatch(method, @"^(Note|Emit|Close|Carry|Tally|Brittle|BurningForLink)") || line.Contains("Tally") || Regex.IsMatch(line, @"\bt\.\w+\+\+") || line.Contains("NoteCarry") || line.Contains("Text = string.Join");

    /// <summary>§3.1 の一覧（Traits.cs と BattleEngine.cs の走査から機械的に）。</summary>
    internal sealed record InvRow(string Where, string Name, TraitId[] Traits, string Writes, string Reads, bool Book);

    internal static List<InvRow> Inventory(out Dictionary<TraitId, string> tickMap)
    {
        string root = FindRoot();
        var tl = File.ReadAllLines(Path.Combine(root, "BattleCore", "Traits.cs"));
        var el = File.ReadAllLines(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
        var cls = EnclosingClasses(tl); var tm = EnclosingMethods(tl); var em = EnclosingMethods(el);
        var c2t = ClassToTrait();
        var rows = new List<InvRow>();
        var ign = new Regex(@"\bIgnite\(\s*([\w.]+)");
        // Traits.cs: クラスごと
        foreach (var g in Enumerable.Range(0, tl.Length).Where(i => !tl[i].TrimStart().StartsWith("//") && !tl[i].TrimStart().StartsWith("///") && (ign.IsMatch(tl[i]) || tl[i].Contains("StatusKeys.Burn")))
                     .GroupBy(i => cls[i]))
        {
            var ws = g.Where(i => ign.IsMatch(tl[i]) && !tl[i].Contains("void Ignite")).Select(i => $"{tm[i]}: {ign.Match(tl[i]).Groups[1].Value}").Distinct().ToList();
            var rs = g.Where(i => tl[i].Contains("StatusKeys.Burn") && !ign.IsMatch(tl[i]) && !IsWrite(tl[i])).Select(i => $"{tm[i]}: {ReadKind(tl[i])}").Distinct().ToList();
            var t = c2t.TryGetValue(g.Key, out var tt) ? tt : Array.Empty<TraitId>();
            rows.Add(new InvRow(t.Length > 0 ? "札" : "型", g.Key, t, string.Join("<br>", ws), string.Join("<br>", rs), false));
        }
        // BattleEngine.cs: メソッドごと（燃焼の刻みの外の読み手・書き手）
        foreach (var g in Enumerable.Range(0, el.Length).Where(i => !el[i].TrimStart().StartsWith("//") && !el[i].TrimStart().StartsWith("///") && ((ign.IsMatch(el[i]) && !el[i].Contains("void Ignite")) || el[i].Contains("StatusKeys.Burn")))
                     .GroupBy(i => em[i]))
        {
            var ws = g.Where(i => ign.IsMatch(el[i]) && !el[i].Contains("void Ignite")).Select(i => ign.Match(el[i]).Groups[1].Value).Distinct().ToList();
            var rs = g.Where(i => el[i].Contains("StatusKeys.Burn") && !ign.IsMatch(el[i]) && !IsWrite(el[i])).Select(i => ReadKind(el[i])).Distinct().ToList();
            bool book = g.All(i => IsBook(em[i], el[i]) || el[i].Contains("SetCounter(StatusKeys.Burn") || el[i].Contains("EmitStatusGain"));
            // そのメソッドが見る札（TraitId.X ／ _xxxHolders）
            int s0 = g.Min(), s1 = g.Max();
            int a = s0; while (a > 0 && em[a - 1] == g.Key) a--;
            int b = s1; while (b + 1 < el.Length && em[b + 1] == g.Key) b++;
            var ids = new SortedSet<string>();
            // 読み・書きの行とその前後1行に出る札だけ（メソッド全体だと被ダメの段の札が全部並んでしまう）
            foreach (int i0 in g) for (int i = Math.Max(a, i0 - 1); i <= Math.Min(b, i0 + 1); i++) foreach (Match m in Regex.Matches(el[i], @"TraitId\.(\w+)")) ids.Add(m.Groups[1].Value);
            // 札の代わりに窓口（保持者の一覧・規則）で分岐している行: `_fireWardHolders` ／ `_fireArmorLive` ／ `Ember.Brittle` など
            foreach (int i0 in g) foreach (Match m in Regex.Matches(el[i0], @"\b_(\w+?)(?:Holders|Live)\b|\b(Ember\.\w+|Soak\.\w+)")) ids.Add(m.Groups[1].Success && m.Groups[1].Value.Length > 0 ? "_" + m.Groups[1].Value : m.Groups[2].Value);
            // Ignite の中で残りターンを書き換える口（滲み則・燃えやすい板）
            if (g.Key == "Ignite")
                for (int i = a; i <= b; i++) if (Regex.IsMatch(el[i], @"^\s*turns\s*(\+?=)")) ids.Add("残りターン: " + el[i].Trim().Split("//")[0].Trim());
            rows.Add(new InvRow("engine", g.Key + (ids.Count > 0 ? "（" + string.Join("・", ids) + "）" : ""), Array.Empty<TraitId>(), string.Join("・", ws), string.Join("<br>", rs), book));
        }
        // 燃焼ダメージの扱い: 刻み・起爆・燃える巻き込みの本文が見る札（手書きは下の表の6行だけ・抜けは落とす）
        var handled = new Dictionary<string, string>
        {
            ["Pyre"] = "無効（本人・熾火）", ["FireArmor"] = "無効（本人・火の鎧）", ["FireMend"] = "回復（本人・火の癒し）",
            ["FireConvert"] = "回復（燃えている味方・ヒヨが生きている間）", ["FireConvertHalf"] = "半分の回復（同）", ["Inverse"] = "回復（ベニの隣・反転）",
            ["FireConvertDry"] = "（渇きの扱いだけ）", ["FireMendDry"] = "（渇きの扱いだけ）", ["Devour"] = "（起爆の毒の側・旧ベニ・保持者 0 枚）",
        };
        tickMap = new Dictionary<TraitId, string>();
        foreach (string meth in new[] { "BurnTickOnce", "DetonateOne", "FireSplashHit", "FireConvert", "FireproofArmor", "FireConvertHolder" })
        {
            var idx = Enumerable.Range(0, el.Length).Where(i => em[i] == meth).ToList();
            if (idx.Count == 0) throw new InvalidOperationException($"`{meth}` が見つからない（R034）");
            foreach (int i in idx)
                foreach (Match m in Regex.Matches(el[i], @"TraitId\.(\w+)"))
                {
                    string k = m.Groups[1].Value;
                    if (!handled.TryGetValue(k, out var lab)) throw new InvalidOperationException($"燃焼ダメージの扱いの表に無い札 `{k}`（{meth}・R260）");
                    tickMap[Enum.Parse<TraitId>(k)] = lab;
                }
        }
        if (rows.Count == 0) throw new InvalidOperationException("燃焼の書き手・読み手の走査が空（R034）");
        return rows;
    }

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第239期 `firescale phase0` —— Q0-1〜Q0-5");
        Console.WriteLine();

        // ---------------- Q0-1 §3.1 の一覧 ----------------
        var inv = Inventory(out var tickMap);
        var enemies = EnemyDefs().ToList();
        string Holders(TraitId[] t, IEnumerable<UnitDef> pool) => string.Join("・", pool.Where(u => u.Traits.Any(t.Contains)).Select(u => u.Name).Distinct());
        Console.WriteLine("## Q0-1 燃焼の書き手・読み手（`Traits.cs` と `BattleEngine.cs` の走査・手書き 0 行）");
        Console.WriteLine();
        Console.WriteLine("「書く」＝ `Ignite(` を呼ぶフックと、その第1引数（火を点ける相手）。「読む」＝ `StatusKeys.Burn` を読むフックと読み方（行の形から機械的に分けた）。");
        Console.WriteLine("「火勢」＝ 火勢を入れたとき意味が変わりうる読み手（計数だけの行を除いた読み手すべて。二値は △・数／残りターン／種類は ○）。");
        Console.WriteLine();
        Console.WriteLine("| 所在 | クラス・メソッド | 札 | 保持者（味方・規定） | 保持者（敵） | 書く | 読む | 燃焼ダメージの扱い | 火勢 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var r in inv.OrderBy(r => r.Where).ThenBy(r => r.Name))
        {
            string ally = Holders(r.Traits, UnitCatalog.All), foe = Holders(r.Traits, enemies);
            string tick = string.Join("・", r.Traits.Where(tickMap.ContainsKey).Select(x => tickMap[x]));
            string mark = r.Book || r.Reads == "" ? "" : r.Reads.Split("<br>").Any(x => !x.Contains("二値")) ? "○" : "△";
            Console.WriteLine($"| {r.Where} | {r.Name} | {string.Join("・", r.Traits)} | {ally} | {foe} | {r.Writes} | {r.Reads}{(r.Book ? "（計数）" : "")} | {tick} | {mark} |");
        }
        Console.WriteLine();
        Console.WriteLine("燃焼ダメージの扱い（刻み・起爆・燃える巻き込みの本文が見る札。**書いていない駒はダメージ**）: "
            + string.Join(" ／ ", tickMap.Select(kv => $"`{kv.Key}` {kv.Value}（{string.Join("・", UnitCatalog.All.Where(u => u.Traits.Contains(kv.Key)).Select(u => u.Name))}）")));
        Console.WriteLine();

        // ---------------- Q0-2 着火の数え方 ----------------
        Console.WriteLine("## Q0-2 着火 ＝ 台本の `StatusGain`（burn）1件か（影の火勢の入力）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 × 倍率 | `StatusGain` burn | 帳簿（点いた＋煽られた） | 一致 | 観測した書き手（`ActorId` の駒） |");
        Console.WriteLine("|---|---|---:|---:|---|---|");
        foreach (var (bn, bf) in Boards.Where(b => b.Name != "T3-1"))
        {
            var f = bf();
            foreach (int w in new[] { BA.MainWave, 3 })
            {
                long evs = 0, book = 0; var writers = new SortedSet<string>();
                for (int s = 0; s < 50; s++)
                {
                    var (r, p, e) = Fight(f, w, BA.Scales[0].Sc, s);
                    var nm = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Name);
                    foreach (var x in r.Events)
                        if (x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Burn)
                        { evs++; writers.Add(x.ActorId is int a && nm.TryGetValue(a, out var n) ? n : "（出どころなし・召喚）"); }
                    book += r.TallyByUnit.Values.Sum(t => (long)t.BurnLit + t.BurnRelit);
                }
                Console.WriteLine($"| {bn} | {BA.WaveNames[w]} × 200/200 × seed 0..49 | {evs} | {book} | {(evs == book ? "○" : "×")} | {string.Join("・", writers)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("- 帳簿の `BurnLit` / `BurnRelit` は `Ignite` の中でしか足されない。**台本の件数と一致すれば、影の火勢は `Ignite` の全件を見ている**");
        Console.WriteLine("- 数えないもの: リリの口移しで移る燃焼（`StatusTransfer`・着火ではない）。上の台にリリはいない");
        Console.WriteLine();

        // ---------------- Q0-3 手番の枠 ----------------
        Console.WriteLine("## Q0-3 手番の枠（`BattleResult.Hands`）");
        Console.WriteLine();
        {
            long hands = 0, taken = 0, nested = 0, verboseOff = 0;
            for (int s = 0; s < 50; s++)
            {
                var (r, p, e) = Fight(T3, BA.MainWave, BA.Scales[0].Sc, s);
                hands += r.Hands.Count; taken += r.TallyByUnit.Values.Sum(t => (long)t.TurnsTaken);
                var hs = r.Hands.OrderBy(h => h.EventStart).ToList();
                for (int i = 1; i < hs.Count; i++) if (hs[i].EventStart < hs[i - 1].EventEnd) nested++;
                var (r2, _, _) = Fight(T3, BA.MainWave, BA.Scales[0].Sc, s, verbose: false);
                verboseOff += r2.Hands.Count;
            }
            Console.WriteLine($"- T3 × 九/新兵 × 200/200 × seed 0..49: 枠 {hands} ／ 帳簿の `TurnsTaken` {taken}（{(hands == taken ? "一致" : "不一致")}）・入れ子（再行動）{nested}・verbose 偽の枠 {verboseOff}（0 のはず）");
        }
        Console.WriteLine();

        // ---------------- Q0-4 台と席 ----------------
        Console.WriteLine("## Q0-4 台と席");
        Console.WriteLine();
        var pk = PickSeat(T31Members);
        _t31 = pk.F;
        Console.WriteLine($"- **T3-1 の席**（規定・120 通り × 九/新兵 × seed {BA.PickSeed0}..{BA.PickSeed0 + BA.PickSeeds - 1}）: {BA.SeatsNamed(pk.F)}（200/200 全員生存 {pk.S.Sv}/{BA.PickSeeds}・400/300 {pk.Sv4}/{BA.PickSeeds}・落 {pk.S.Fell}・列挙 {pk.Rank + 1} 番目・同値 {pk.Tied} 通り）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席 | 九/新兵 200/200 全員生存 ／ 勝率 | 400/300 | 115/115 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (bn, bf) in Boards)
        {
            var f = bf();
            var cells = BA.Scales.Select(sc =>
            {
                int sv = 0, w = 0;
                for (int s = 0; s < BA.Seeds; s++) { var r = Fight(f, BA.MainWave, sc.Sc, s, verbose: false).R; if (r.PlayerWon) { w++; if (r.PlayerStarterFallen.Count == 0) sv++; } }
                return $"{100.0 * sv / BA.Seeds:F1} ／ {100.0 * w / BA.Seeds:F1}";
            });
            Console.WriteLine($"| {bn} | {BA.SeatsNamed(f)} | {string.Join(" | ", cells)} |");
        }
        Console.WriteLine();
        Console.WriteLine("- **T3 の 400/300 が第238期 A+D2+V1 の T3′-1（85.5）と一致すれば、規定の駒は第238期の版と同じ**");
        Console.WriteLine();

        // ---------------- Q0-5 予測の材料（影は数えない） ----------------
        Console.WriteLine("## Q0-5 予測の材料（九/新兵 × 200/200 × seed 0..49・影の火勢と絵はまだ数えない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 着火/戦 ボルグ ／ ホタ ／ ヒヨ ／ 相方 ／ 敵 | うち燃えている駒へ（点け直し）% | 手番/戦 ボルグ ／ ホタ ／ ヒヨ | 決着T |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (bn, bf) in Boards)
        {
            var f = bf();
            long n = 0, turns = 0; var ig = new long[5]; var rl = new long[5]; var hd = new long[3];
            for (int s = 0; s < 50; s++)
            {
                var (r, p, e) = Fight(f, BA.MainWave, BA.Scales[0].Sc, s);
                n++; turns += r.Turns;
                var info = p.Concat(e).ToDictionary(u => u.InstanceId, u => RoleIx(u.Def.Id, u.TeamId));
                // 帳簿（`Ignite` の中で足される・受け手の駒ごと）: 点いた ／ 煽られた（燃えている駒への点け直し）
                foreach (var (id, t) in r.TallyByUnit)
                {
                    var u = p.FirstOrDefault(x => x.Def.Id == id);
                    int ro = u is null ? 4 : RoleIx(id, u.TeamId);
                    ig[ro] += t.BurnLit + t.BurnRelit; rl[ro] += t.BurnRelit;
                }
                foreach (var h in r.Hands) if (info.TryGetValue(h.ActorId, out var ro) && ro <= 2) hd[ro]++;
            }
            Console.WriteLine($"| {bn} | {string.Join(" ／ ", ig.Select(x => ((double)x / n).ToString("F1")))} | {string.Join(" ／ ", Enumerable.Range(0, 5).Select(i => Pct(rl[i], ig[i])))} | {string.Join(" ／ ", hd.Select(x => ((double)x / n).ToString("F1")))} | {(double)turns / n:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
