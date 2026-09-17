using BattleCore;
using static Common;

// =====================================================================================
// derive モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "derive")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 derive
// =====================================================================================

static class DeriveDiag
{
// derive モード: **手で写している表を、機械に照合させる**（第94期）。
//
// 第84期（カドを含む行数 8 → 11）・第85期（`isFriendlyFire` の呼び出し口 6 → 10）・
// 第90期（`Where` が `Foe` → `Any`）・第92期（`Supplies` の欠落2・過剰1）・
// 第93期（「`GatherRule` / `IgniteRule` は既定 off」が誤り。両方 on）と、
// **手で写した表が5期連続でずれた。** 指示書を書く側が毎回リポジトリを読み直しても防げていない。
//
//     実装から derive できるものは、手で写すと必ずずれる。
//
//     dotnet run --project BattleSim -c Release 0 derive rules > docs/rules.md   # §1 (T1)
//     dotnet run --project BattleSim -c Release 0 derive scan                    # §2 (T2)
//     dotnet run --project BattleSim -c Release 0 derive check                   # §5 自己検査
//
// **盤面は1ビットも動かない。** 観測の印（`BattleContext.Mark` / `Probe`）は既定 null で、
// どの規則からも読まれない（`derive check` が `compare` 305 セルで確かめる）。
public static void Run(string[] args, int stageIndex)
{
    string focusId = args.Length > 1 ? args[1] : "";

    string dvMode = args.Length > 2 ? args[2] : "";

    // リポジトリ直下を探す（`audit` と同じ walk-up）。
    string? dvRoot = Directory.GetCurrentDirectory();
    while (dvRoot != null && !File.Exists(Path.Combine(dvRoot, "docs", "balance.md")))
        dvRoot = Path.GetDirectoryName(dvRoot);

    // ---------------------------------------------------------------------------------
    // 共通: ノブの一覧を**実装から**取る（reflection）。手で書いた項目は1つも無い。
    // ---------------------------------------------------------------------------------
    var dvAsm = typeof(BattleContext).Assembly;

    // `BattleEngine.Run(Formation, Formation, ...)` の引数がノブの正本。
    var dvRun = typeof(BattleEngine).GetMethods()
        .Where(m => m.Name == "Run")
        .OrderByDescending(m => m.GetParameters().Length)
        .First(m => m.GetParameters()[0].ParameterType == typeof(Formation));

    // 型 → 既定値（`public static T Default`、無ければ `public static readonly T ...`）。
    string? DvDefaultOf(Type t)
    {
        Type bare = Nullable.GetUnderlyingType(t) ?? t;
        var pi = bare.GetProperty("Default", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (pi is not null && pi.PropertyType == bare) return pi.GetValue(null)?.ToString();
        var fi = bare.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                     .FirstOrDefault(f => f.FieldType == bare);
        return fi?.GetValue(null)?.ToString();
    }

    // `default(T)` と一致するか。**判定ではなく手がかり**（表の脚注）。
    bool DvIsBare(Type t)
    {
        Type bare = Nullable.GetUnderlyingType(t) ?? t;
        if (!bare.IsValueType) return false;
        string? d = DvDefaultOf(bare);
        return d is not null && d == Activator.CreateInstance(bare)?.ToString();
    }

    // ---------------------------------------------------------------------------------
    // 共通: 診断名と期を**ソースと design/ から derive** する。
    // ---------------------------------------------------------------------------------
    string dvProgram = "";
    var dvModes = new List<(string Name, int Start)>();
    if (dvRoot is not null)
    {
        string pp = Path.Combine(dvRoot, "BattleSim", "Program.cs");
        if (File.Exists(pp))
        {
            dvProgram = File.ReadAllText(pp);
            const string needle = "focusId == \"";
            for (int i = dvProgram.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = dvProgram.IndexOf(needle, i + 1, StringComparison.Ordinal))
            {
                int q = dvProgram.IndexOf('"', i + needle.Length);
                if (q < 0) break;
                string nm = dvProgram.Substring(i + needle.Length, q - i - needle.Length);
                // コメントの中の `focusId == "..."` を落とす（モード名は英小文字と数字だけ）。
                if (nm.Length > 0 && nm.All(ch => char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch)))
                    dvModes.Add((nm, i));
            }
            dvModes = dvModes.OrderBy(m => m.Start).ToList();
        }
    }

    // 第118期。**診断が `Program.cs` の外にあっても索けるようにする。**
    // top-level statements は全部で1つのメソッドなので、大きい診断は別ファイルの static クラスに置く
    // （`BattleSim/Tank.cs` が最初）。その場合 `focusId == "..."` の区間には振り分けの数行しか無く、
    // **規則の名前が本文に現れないので `測った診断` が「不明」になる**（実際にこれで 1 件出た）。
    //
    // 付け方は「**その区間が名前を挙げているクラスを宣言しているファイル**を、その区間に足す」。
    // クラス名で結ぶので、ファイル名にもモード名にも依存しない。
    var dvExtra = new Dictionary<string, string>(StringComparer.Ordinal);
    if (dvRoot is not null && dvModes.Count > 0)
    {
        foreach (string f in Directory.GetFiles(Path.Combine(dvRoot, "BattleSim"), "*.cs"))
        {
            if (Path.GetFileName(f) == "Program.cs") continue;
            string txt = File.ReadAllText(f);
            foreach (System.Text.RegularExpressions.Match cm in
                     System.Text.RegularExpressions.Regex.Matches(txt, @"(?:static|sealed) class (\w+)"))
            {
                string cls = cm.Groups[1].Value;
                for (int i = 0; i < dvModes.Count; i++)
                {
                    int a = dvModes[i].Start;
                    int b = i + 1 < dvModes.Count ? dvModes[i + 1].Start : dvProgram.Length;
                    if (dvProgram.IndexOf(cls + ".", a, b - a, StringComparison.Ordinal) < 0) continue;
                    dvExtra[dvModes[i].Name] = dvExtra.TryGetValue(dvModes[i].Name, out string? had)
                        ? had + txt : txt;
                    break;
                }
            }
        }
    }

    // その名前が現れる診断（`focusId == "..."` の区間で切る）。
    string[] DvDiagnostics(string name)
    {
        if (dvProgram.Length == 0 || dvModes.Count == 0) return Array.Empty<string>();
        var hit = new List<string>();
        for (int i = 0; i < dvModes.Count; i++)
        {
            int a = dvModes[i].Start;
            int b = i + 1 < dvModes.Count ? dvModes[i + 1].Start : dvProgram.Length;
            if (dvProgram.IndexOf(name, a, b - a, StringComparison.Ordinal) >= 0
                || (dvExtra.TryGetValue(dvModes[i].Name, out string? ex)
                    && ex.Contains(name, StringComparison.Ordinal))) hit.Add(dvModes[i].Name);
        }
        return hit.Distinct().ToArray();
    }

    // design/PHASEnn_*.md のうち、その名前が現れるものの期番号。
    var dvDesign = new List<(int Phase, string File, string Text)>();
    if (dvRoot is not null && Directory.Exists(Path.Combine(dvRoot, "design")))
    {
        foreach (string f in Directory.GetFiles(Path.Combine(dvRoot, "design"), "PHASE*.md"))
        {
            string bn = Path.GetFileNameWithoutExtension(f);
            string num = new string(bn.Substring(5).TakeWhile(char.IsDigit).ToArray());
            if (num.Length == 0) continue;
            dvDesign.Add((int.Parse(num), Path.GetFileName(f), File.ReadAllText(f)));
        }
        dvDesign = dvDesign.OrderBy(x => x.Phase).ToList();
    }
    // 第112期 (T3) 段2: 走査対象を `CLAUDE.md` ＋ `design/LESSONS_*.md` に広げる。
    // 各期の実測と経緯を `LESSONS_*.md` へ出した（第112期 (T2)）ので、`CLAUDE.md` だけを見ると
    // **型名が散文にしか出ていない 15 本のノブの ○ が消える**——移動で消えたわけではないのに落ちる。
    // **段1（生成器を触らずに再生成して消えた ○ を列挙する）を先にやること**——
    // 段2 だけをやると、移動中に記述ごと消えたノブがあっても ○ が付いたままで気づけない。
    string dvClaude = "";
    if (dvRoot is not null)
    {
        if (File.Exists(Path.Combine(dvRoot, "CLAUDE.md")))
            dvClaude = File.ReadAllText(Path.Combine(dvRoot, "CLAUDE.md"));
        string dvDesignDir = Path.Combine(dvRoot, "design");
        if (Directory.Exists(dvDesignDir))
            foreach (string f in Directory.GetFiles(dvDesignDir, "LESSONS_*.md").OrderBy(x => x, StringComparer.Ordinal))
                dvClaude += "\n" + File.ReadAllText(f);
    }

    int[] DvPhases(string name)
        => dvDesign.Where(d => d.Text.Contains(name, StringComparison.Ordinal))
                   .Select(d => d.Phase).Distinct().OrderBy(x => x).ToArray();

    // 「初出 第nn期」。全部並べると 20 期を超えて読めない。
    string DvPhaseCell(string name)
    {
        int[] ph = DvPhases(name);
        // **初出だけを出す。**末尾は出さない——その期の報告書が型名に触れるだけで動くので、
        // 「いつ作られたか」だけが安定した情報になる（第94期に実際に踏んだ）。
        //
        // **第124期 段0: 「（k 期）」を落とした。** k は `design/PHASE*.md` のうちその型名が
        // 現れるファイルの期番号の異なり数なので、**この診断の出力を `design/` に置くと k が増える**
        // ——走査対象が走査する側の成果物を含む自己参照で、症状は「走査が空」ではなく
        // **「毎期 `docs/rules.md` に差分が出る」**（第123期 §9-5 で `SoakRule` が 9／正 10 になった）。
        // **k が動くのは報告書を書いたからであって、ノブの既定が動いたからではない。**
        // 規約 (G8) の必須3「触っていないノブの既定が動いていない」を `docs/rules.md` の差分で示す、が
        // **k のせいで常に偽になる**ので、検算の側を守るために出力から外す。
        // **初出（`ph[0]`）は動かない**——後から足される報告書の期番号は必ず既出より大きい。
        //
        // **第132期 段0-b: 「〜」も落とした。** 第124期は「（k 期）」だけを落としたが、
        // **「〜」に同じ自己参照が1ビットぶん残っていた**——「初出だけ」と書いてあるのに
        // `ph.Length` を見ているので、**指示書がそのノブの型名を挙げただけで
        // 「第nn期」→「第nn期〜」に変わる**（第131期に実測。第130期に初出のノブが「第130期〜」に変わった）。
        // **この註では型名を1つも書かない**——`測った診断` の列は本文まるごとで結ぶので、
        // ここに型名を書くと**そのノブの利用者に `derive` が付く**（この期に実際に踏んだ）。
        // **常に1行動く検算は、検算として死んでいる**——規約 (G8) の必須3
        // （触っていないノブの既定が動いていない）を `docs/rules.md` 丸ごとの差分で示す、が
        // 指示書が既存のノブに言及した期には必ず偽になり、毎期「札のせいか自己参照か」を
        // 人力で切り分けることになる。**部分的に塞ぐと残りに気づけない**（第124期の積み残し）。
        return ph.Length == 0 ? "**不明**" : $"第{ph[0]}期";
    }

    // =================================================================================
    // §1 (T1) ノブの一覧を生成物にする。
    // =================================================================================
    if (dvMode == "rules")
    {
        Console.WriteLine("# ノブ一覧");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 derive rules > docs/rules.md` の出力。手で編集しない。");
        Console.WriteLine();
        Console.WriteLine("**この表は全部が実装から derive されている**（第94期 (T1)）。"
                          + "型名・引数名・既定値は reflection、`測った診断` は `BattleSim/Program.cs` の "
                          + "`focusId == \"...\"` の区間（**別ファイルの診断はそこが名前を挙げているクラスで結ぶ**）、"
                          + "`初出` は `design/PHASE*.md` の本文からそれぞれ引いた（**初出の期だけ。範囲は出さない**——第132期 段0-b）。"
                          + "**手で書いた項目は1つも無い。**");
        Console.WriteLine();
        Console.WriteLine("> **以後の指示書は既定値をここから引くこと。手で写さない。**");
        Console.WriteLine("> 第93期は「`GatherRule` / `IgniteRule` は既定 off」と書いて測り始めたが、"
                          + "**どちらも第89・90期に採用済みで既定 on** だった。");
        Console.WriteLine();

        Console.WriteLine("## 1. `BattleEngine.Run` の引数（ノブの正本）");
        Console.WriteLine();
        Console.WriteLine("| # | 引数名 | 型 | 既定値（実装） | `= default(T)` | 測った診断 | 初出（design/） | CLAUDE.md / LESSONS |");
        Console.WriteLine("|--:|---|---|---|:-:|---|---|:-:|");
        var dvPars = dvRun.GetParameters().Skip(3).ToArray();   // player / enemy / seed を除く
        int dvNo = 0;
        foreach (var pi in dvPars)
        {
            Type bare = Nullable.GetUnderlyingType(pi.ParameterType) ?? pi.ParameterType;
            if (bare == typeof(bool)) continue;                  // verbose
            if (bare == typeof(CounterProbe)) continue;          // 第94期 (T2) の観測子。ノブではない
            dvNo++;
            string def = DvDefaultOf(bare) ?? "**不明**";
            string[] diag = DvDiagnostics(bare.Name);
            Console.WriteLine($"| {dvNo} | `{pi.Name}` | `{bare.Name}` | `{def}` | {(DvIsBare(bare) ? "○" : "")} "
                + $"| {(diag.Length == 0 ? "**不明**" : string.Join(" / ", diag.Select(d => $"`{d}`")))} "
                + $"| {DvPhaseCell(bare.Name)} "
                + $"| {(dvClaude.Contains(bare.Name, StringComparison.Ordinal) ? "○" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"引数 {dvNo} 本（`verbose` と観測子を除く）。");
        Console.WriteLine();
        Console.WriteLine("**`= default(T)` は「その規則が既定で何もしない」の機械的な手がかりであって、判定ではない。**");
        Console.WriteLine("採否そのものは**既定値の列**を読むこと——`ThornRule { Wound = None }` は残置、"
                          + "`SoakRule { Poison = True, Burn = False }` は毒側だけ採用、という具合に既定値が全部を語る。");
        Console.WriteLine();

        Console.WriteLine("## 2. `Default` を持つ型の全数（`Run` の引数に出ないものを含む）");
        Console.WriteLine();
        Console.WriteLine("| 型 | 既定値 | `Run` の引数 | 測った診断 | 初出（design/） |");
        Console.WriteLine("|---|---|:-:|---|---|");
        var dvArgTypes = dvPars.Select(p => Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType).ToHashSet();
        int dvTypeN = 0;
        foreach (Type t in dvAsm.GetTypes().Where(x => x.IsPublic && !x.IsEnum)
                                 .OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            string? def = DvDefaultOf(t);
            if (def is null) continue;
            dvTypeN++;
            string[] diag = DvDiagnostics(t.Name);
            Console.WriteLine($"| `{t.Name}` | `{def}` | {(dvArgTypes.Contains(t) ? "○" : "")} "
                + $"| {(diag.Length == 0 ? "**不明**" : string.Join(" / ", diag.Select(d => $"`{d}`")))} "
                + $"| {DvPhaseCell(t.Name)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"{dvTypeN} 型。");
        Console.WriteLine();

        Console.WriteLine("## 3. 規則が使う列挙型");
        Console.WriteLine();
        Console.WriteLine("**`default(T)` は必ず値 0 の要素**なので、この並びが上の `= default(T)` 列の意味を決めている。");
        Console.WriteLine();
        Console.WriteLine("| 列挙型 | 値（0 から） |");
        Console.WriteLine("|---|---|");
        var dvEnums = new List<Type>();
        foreach (Type t in dvAsm.GetTypes().Where(x => x.IsPublic && !x.IsEnum))
        {
            if (DvDefaultOf(t) is null) continue;
            foreach (Type m in t.GetProperties().Select(p => p.PropertyType)
                                .Concat(t.GetFields().Select(f => f.FieldType)))
                if (m.IsEnum && m.Assembly == dvAsm && !dvEnums.Contains(m)) dvEnums.Add(m);
        }
        foreach (Type e in dvEnums.OrderBy(x => x.Name, StringComparer.Ordinal))
            Console.WriteLine($"| `{e.Name}` | "
                + $"{string.Join(" / ", Enum.GetNames(e).Select(n => $"`{n}`={Convert.ToInt64(Enum.Parse(e, n))}"))} |");
        Console.WriteLine();
        Console.WriteLine($"{dvEnums.Count} 型。");
        return;
    }

    // =================================================================================
    // §2 (T2) 走らせて観測し、手で作った表と突き合わせる。
    // =================================================================================
    var dvStages = EnemyCatalog.Stages;

    if (dvMode.Length == 0 || dvMode == "scan")
    {
        // 観測の帳簿。**(特性, キー, 場所, 向き) の組ごとに回数と総量を持つ。**
        var dvObs = new Dictionary<(TraitId T, int Key, int W, int Dir), (long N, long Amt)>();
        var dvBlade = new Dictionary<TraitId, long>();                     // 味方の刃（第85期の呼び出し口）
        long dvBladeEngine = 0;                                            // 印が立たない中継（巨躯・分かち）
        var dvDeep = new Dictionary<(TraitId T, int W, int Dir), long>();  // 深手（11 キーに無い）
        long dvEvents = 0;

        // StatusKeys / CarryKeys の名前 → CarryKeys の添字。深手だけ -1、私有キーは -2。
        int DvKeyOf(string key) => key switch
        {
            StatusKeys.Poison => UnitTally.CarryPoison,
            StatusKeys.Burn => UnitTally.CarryBurn,
            StatusKeys.Stun => UnitTally.CarryStun,
            StatusKeys.Marked => UnitTally.CarryMark,
            StatusKeys.Armor => UnitTally.CarryArmor,
            StatusKeys.Wound => UnitTally.CarryWound,
            StatusKeys.IdleTurn => UnitTally.CarryIdle,
            StatusKeys.Deep => -1,
            // **未知（特性の私有キー）は -2。深手の -1 と混ぜない**
            // ——混ぜると `goadTarget` などが深手の表に化ける（この期に実際に踏んだ）。
            _ => Array.IndexOf(UnitTally.CarryKeys, key) is int i && i >= 0 ? i : -2
        };

        const int DvSelf = 0, DvAlly = 1, DvFoe = 2;
        string[] dvWhereName = { "自分", "味方", "敵" };

        void DvProbe(TraitId trait, UnitState owner, UnitState target, string key, int delta)
        {
            dvEvents++;
            if (key == BattleContext.FriendlyBladeKey)
            {
                dvBlade[trait] = dvBlade.TryGetValue(trait, out long b) ? b + 1 : 1;
                return;
            }
            if (key == BattleContext.FriendlyBladeEngineKey) { dvBladeEngine++; return; }
            int k = DvKeyOf(key);
            if (k < -1) return;                                   // 特性の私有キー（goadTarget など）
            int w = ReferenceEquals(owner, target) ? DvSelf
                  : owner.TeamId == target.TeamId ? DvAlly : DvFoe;
            int dir = delta == 0 ? 0 : delta > 0 ? 1 : -1;
            if (k == -1)
            {
                var dk = (trait, w, dir);
                dvDeep[dk] = dvDeep.TryGetValue(dk, out long dd) ? dd + 1 : 1;
                return;
            }
            var ky = (trait, k, w, dir);
            var cur = dvObs.TryGetValue(ky, out var v) ? v : (0L, 0L);
            dvObs[ky] = (cur.Item1 + 1, cur.Item2 + Math.Abs((long)delta));
        }

        // 走らせる台。**`compare` 61 行 ＋ 交差帯 12 行**（指示書 §2-2）。
        const int DvSeeds = 20;
        var dvRows = CompareBuilds().Concat(CrossBuilds()).ToArray();

        var dvSw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var row in dvRows)
            foreach (var st in dvStages)
                for (int seed = 0; seed < DvSeeds; seed++)
                    BattleEngine.Run(row.F, st.Enemy, seed, verbose: false, probe: DvProbe);
        dvSw.Stop();
        double dvBattles = dvRows.Length * dvStages.Count * DvSeeds;

        // 台に居る体数（未観測の読み分けに使う。延べ）。
        var dvBodies = new Dictionary<TraitId, int>();
        void DvCount(UnitDef d)
        {
            foreach (TraitId t in d.Traits)
                dvBodies[t] = dvBodies.TryGetValue(t, out int c) ? c + 1 : 1;
        }
        foreach (var row in dvRows) foreach (var (_, d) in row.F.Occupied()) DvCount(d);
        foreach (var st in dvStages) foreach (var (_, d) in st.Enemy.Occupied()) DvCount(d);

        // 観測を「供給 / 消費 / 読み」に畳む。
        var dvSup = new Dictionary<(TraitId, int, int), (long N, long Amt)>();
        var dvDrain = new Dictionary<(TraitId, int, int), (long N, long Amt)>();
        var dvRead = new Dictionary<(TraitId, int, int), long>();
        foreach (var kv in dvObs)
        {
            var k = (kv.Key.T, kv.Key.Key, kv.Key.W);
            if (kv.Key.Dir > 0) dvSup[k] = kv.Value;
            else if (kv.Key.Dir < 0) dvDrain[k] = kv.Value;
            else dvRead[k] = kv.Value.N;
        }
        // 中継の判定は**キー単位**（場所をまたいで移すので）。
        var dvSupByKey = new Dictionary<(TraitId, int), long>();
        var dvDrainByKey = new Dictionary<(TraitId, int), long>();
        foreach (var kv in dvSup)
        {
            var k = (kv.Key.Item1, kv.Key.Item2);
            dvSupByKey[k] = (dvSupByKey.TryGetValue(k, out long a) ? a : 0) + kv.Value.Amt;
        }
        foreach (var kv in dvDrain)
        {
            var k = (kv.Key.Item1, kv.Key.Item2);
            dvDrainByKey[k] = (dvDrainByKey.TryGetValue(k, out long a) ? a : 0) + kv.Value.Amt;
        }
        bool DvIsRelay(TraitId t, int key)
            => dvDrainByKey.TryGetValue((t, key), out long d) && d > 0
               && (!dvSupByKey.TryGetValue((t, key), out long s) || s <= d);

        // 観測できるキー（指示書 §2-2 の範囲）。
        var dvReadable = new[] { UnitTally.CarryPoison, UnitTally.CarryBurn, UnitTally.CarryStun,
                                 UnitTally.CarryMark, UnitTally.CarryArmor, UnitTally.CarryWound,
                                 UnitTally.CarryIdle }.ToHashSet();
        var dvSuppliable = Enumerable.Range(0, UnitTally.CarryKeys.Length)
                                     .Where(i => i != UnitTally.CarryHit).ToHashSet();

        // 第74期に「キーを持たせない」と決めて切り出したマイナス4枚と、キーを持たない2枚。
        // **表の空欄が意図的**なので、欠落とは呼ばない（`TraitKeyMap` の doc）。
        var dvIntentional = new[] { TraitId.ThinBlade, TraitId.Overreach, TraitId.Await,
                                    TraitId.Seal, TraitId.Stoic, TraitId.Executioner }.ToHashSet();

        string DvK(int k) => UnitTally.CarryKeys[k];

        bool DvCovered((int Key, TraitEntryMap.Where W)[] tab, int key, int w)
            => tab.Any(e => e.Key == key && (e.W == TraitEntryMap.Where.Any || (int)e.W == w));

        (long N, long Amt) dvScaleNote = (0, 0);   // 鱗の破片（doc に明記された意図的な除外）
        var missSup = new List<(TraitId T, int K, int W, long N, long Amt)>();
        var missRead = new List<(TraitId T, int K, int W, long N)>();
        var overSup = new List<(TraitId T, int K, string W, long S, long D)>();
        var unobsSup = new List<(TraitId T, int K, string W)>();
        var unobsRead = new List<(TraitId T, int K, string W)>();
        var oosRead = new List<(TraitId T, int K, string W)>();

        // (1) 観測されたが表に無い（＝欠落）
        foreach (var kv in dvSup.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2).ThenBy(x => x.Key.Item3))
        {
            (TraitId t, int k, int w) = kv.Key;
            if (!dvSuppliable.Contains(k) || dvIntentional.Contains(t)) continue;
            if (DvIsRelay(t, k)) continue;                       // 中継は供給ではない（下の (3) で出す）
            // **鱗の破片だけは doc に明記された意図的な除外**（`TraitEntryMap.Supplies` の doc）。
            // 載せると自分の読み `(破片, 自分)` を打ち消して入口が 1 → 0 になり、第78期の定義が変わる。
            if (t == TraitId.Scale && k == UnitTally.CarryArmor) { dvScaleNote = kv.Value; continue; }
            var tab = TraitEntryMap.Supplies.TryGetValue(t, out var ss)
                ? ss : Array.Empty<(int, TraitEntryMap.Where)>();
            if (!DvCovered(tab, k, w)) missSup.Add((t, k, w, kv.Value.N, kv.Value.Amt));
        }
        foreach (var kv in dvRead.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2).ThenBy(x => x.Key.Item3))
        {
            (TraitId t, int k, int w) = kv.Key;
            if (!dvReadable.Contains(k) || dvIntentional.Contains(t)) continue;
            var tab = TraitEntryMap.Reads.TryGetValue(t, out var rr)
                ? rr : Array.Empty<(int, TraitEntryMap.Where)>();
            if (!DvCovered(tab, k, w)) missRead.Add((t, k, w, kv.Value));
        }

        // (2) 表にあるが観測されなかった（＝未観測。誤りとは呼ばない）
        foreach (var kv in TraitEntryMap.Supplies.OrderBy(x => x.Key))
            foreach ((int k, TraitEntryMap.Where w) in kv.Value)
            {
                if (!dvSuppliable.Contains(k)) continue;
                bool seen = dvSup.Keys.Any(o => o.Item1 == kv.Key && o.Item2 == k
                                                && (w == TraitEntryMap.Where.Any || (int)w == o.Item3));
                if (!seen) unobsSup.Add((kv.Key, k, w.ToString()));
            }
        foreach (var kv in TraitEntryMap.Reads.OrderBy(x => x.Key))
            foreach ((int k, TraitEntryMap.Where w) in kv.Value)
            {
                if (!dvReadable.Contains(k)) { oosRead.Add((kv.Key, k, w.ToString())); continue; }
                bool seen = dvRead.Keys.Any(o => o.Item1 == kv.Key && o.Item2 == k
                                                 && (w == TraitEntryMap.Where.Any || (int)w == o.Item3));
                if (!seen) unobsRead.Add((kv.Key, k, w.ToString()));
            }

        // (3) 表にあるが「中継」と観測された（＝過剰）
        foreach (var kv in TraitEntryMap.Supplies.OrderBy(x => x.Key))
            foreach ((int k, TraitEntryMap.Where w) in kv.Value)
            {
                if (!dvSuppliable.Contains(k) || !DvIsRelay(kv.Key, k)) continue;
                overSup.Add((kv.Key, k, w.ToString(),
                             dvSupByKey.TryGetValue((kv.Key, k), out long sAmt) ? sAmt : 0,
                             dvDrainByKey.TryGetValue((kv.Key, k), out long dAmt) ? dAmt : 0));
            }

        // -----------------------------------------------------------------------------
        // 出力
        // -----------------------------------------------------------------------------
        Console.WriteLine("# 第94期 (T2) —— 手で写した表と、走らせて観測した事実の突き合わせ");
        Console.WriteLine();
        Console.WriteLine($"台: `compare` {CompareBuilds().Length} 行 ＋ 交差帯 {CrossBuilds().Length} 行 "
                          + $"× 全 {dvStages.Count} 波 × seed 0..{DvSeeds - 1} "
                          + $"＝ {dvBattles:N0} 戦。"
                          + $"観測 {dvEvents:N0} 件・所要 {dvSw.Elapsed.TotalSeconds:F1} 秒。");
        Console.WriteLine();
        Console.WriteLine("**観測できる範囲**（指示書 §2-2）:");
        Console.WriteLine();
        Console.WriteLine("| | 範囲 | 器具 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 読み | 毒・燃・痺・標・破片・傷・手番（＋深手） | `UnitState.Counter` |");
        Console.WriteLine("| 供給 | 11 キー全部（**被弾は engine と敵が供給する**ので比較から外す） | `BattleContext.NoteCarry` |");
        Console.WriteLine("| 消費 | 同上（減った分） | `UnitState.SetCounter` |");
        Console.WriteLine();
        Console.WriteLine("**強化・弱体・移動・被弾の「読み」は観測範囲外**——`AtkBonus` も `HasFallenBack` も"
                          + "カウンタではないので、`Counter` を1度も通らない。**未観測ではなく、測っていない。**");
        Console.WriteLine();

        Console.WriteLine("## 表B-1. 観測されたが表に無い —— **欠落（表の誤り）**");
        Console.WriteLine();
        Console.WriteLine("| 特性 | キー | 場所 | 向き | 回/戦 | 量/戦 |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        foreach (var m in missSup.OrderByDescending(x => x.Amt))
            Console.WriteLine($"| `{m.T}` | {DvK(m.K)} | {dvWhereName[m.W]} | **供給** "
                              + $"| {m.N / dvBattles:F2} | {m.Amt / dvBattles:F2} |");
        foreach (var m in missRead.OrderByDescending(x => x.N))
            Console.WriteLine($"| `{m.T}` | {DvK(m.K)} | {dvWhereName[m.W]} | 読み "
                              + $"| {m.N / dvBattles:F2} | — |");
        Console.WriteLine();
        Console.WriteLine($"**供給の欠落 {missSup.Count} 件 ／ 読みの欠落 {missRead.Count} 件。**");
        Console.WriteLine();
        Console.WriteLine($"**別扱い1件**: 鱗（`Scale`）の `(破片, 自分)` は {dvScaleNote.N / dvBattles:F2} 回/戦 "
                          + $"観測されるが、**`Supplies` に載せないことが doc に明記された意図的な除外**"
                          + "——載せると自分の読み `(破片, 自分)` を打ち消して入口が 1 → 0 になり、"
                          + "第78期の「入口」の定義が変わる。**ここだけは観測より doc の側を採る。**");
        Console.WriteLine();

        Console.WriteLine("## 表B-2. 表にあるが「中継」と観測された —— **過剰（表の誤り）**");
        Console.WriteLine();
        Console.WriteLine("**中継 ＝ 同じ特性が同じキーについて増と減の両方を書き、増の総量 ≤ 減の総量。**");
        Console.WriteLine("盤面の総量を増やさないので供給者ではない（第92期のガルドの傷）。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | キー | 表の場所 | 増/戦 | 減/戦 | 純増/戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|");
        foreach (var o in overSup)
            Console.WriteLine($"| `{o.T}` | {DvK(o.K)} | {o.W} | {o.S / dvBattles:F2} | {o.D / dvBattles:F2} "
                              + $"| **{(o.S - o.D) / dvBattles:F2}** |");
        Console.WriteLine();
        Console.WriteLine($"**過剰 {overSup.Count} 件。**");
        Console.WriteLine();

        Console.WriteLine("## 表B-3. 表にあるが観測されなかった —— **未観測（誤りとは呼ばない）**");
        Console.WriteLine();
        Console.WriteLine("**台に居る体数を併記する**（指示書 §2-3）。0 体なら「台に居ない」、"
                          + "1 体以上なら「居るのに発火していない」で意味が違う。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | キー | 場所 | 種別 | 台の体数 | 読み方 |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        foreach (var u in unobsSup)
        {
            int n = dvBodies.TryGetValue(u.T, out int c) ? c : 0;
            Console.WriteLine($"| `{u.T}` | {DvK(u.K)} | {u.W} | 供給 | {n} "
                              + $"| {(n == 0 ? "台に居ない" : "**居るのに発火していない**")} |");
        }
        foreach (var u in unobsRead)
        {
            int n = dvBodies.TryGetValue(u.T, out int c) ? c : 0;
            Console.WriteLine($"| `{u.T}` | {DvK(u.K)} | {u.W} | 読み | {n} "
                              + $"| {(n == 0 ? "台に居ない" : "**居るのに発火していない**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**未観測 {unobsSup.Count + unobsRead.Count} 件**"
                          + $"（供給 {unobsSup.Count} / 読み {unobsRead.Count}）。うち台に居ないもの "
                          + $"{unobsSup.Count(u => !dvBodies.ContainsKey(u.T)) + unobsRead.Count(u => !dvBodies.ContainsKey(u.T))} 件。");
        Console.WriteLine();

        Console.WriteLine("## 表B-4. 観測範囲外（読みが `Counter` を通らないキー）");
        Console.WriteLine();
        Console.WriteLine($"表の `Reads` のうち {oosRead.Count} 件が強化・弱体・移動・被弾で、"
                          + "**この期の器具では原理的に観測できない**（誤りとも未観測とも判定していない）。");
        Console.WriteLine();
        Console.WriteLine("| キー | 件数 |");
        Console.WriteLine("|---|--:|");
        foreach (var g in oosRead.GroupBy(x => x.K).OrderByDescending(g => g.Count()))
            Console.WriteLine($"| {DvK(g.Key)} | {g.Count()} |");
        Console.WriteLine();

        Console.WriteLine("## 表C. Q1 —— 第92期が見つけた既知の誤り");
        Console.WriteLine();
        Console.WriteLine("**Q1 の判定は (T3) で表を直す<u>前</u>のコミットで行う**（指示書 §2-4）"
                          + "——直した後は「出ない」が正しい答えなので、この表は"
                          + "**直した後は「もう出ないこと」を確かめる回帰の表**として読む。");
        Console.WriteLine();
        Console.WriteLine("| 既知の誤り | 種別 | いま出るか |");
        Console.WriteLine("|---|---|---|");
        bool q1a = missSup.Any(m => m.T == TraitId.Contagion && m.K == UnitTally.CarryPoison);
        bool q1b = missSup.Any(m => m.T == TraitId.Amplifier && m.K == UnitTally.CarryPoison);
        bool q1c = overSup.Any(o => o.T == TraitId.Guardian && o.K == UnitTally.CarryWound);
        Console.WriteLine($"| 疫み（ラウ・`Contagion`）が毒を置くのに `Supplies` に無い | 欠落 "
                          + $"| {(q1a ? "**出る（未修正）**" : "出ない（修正済み）")} |");
        Console.WriteLine($"| 澱み（ミオ・`Amplifier`）が毒を置くのに `Supplies` に無い | 欠落 "
                          + $"| {(q1b ? "**出る（未修正）**" : "出ない（修正済み）")} |");
        Console.WriteLine($"| ガルド（`Guardian`）の傷は**中継**であって供給ではない | 過剰 "
                          + $"| {(q1c ? "**出る（未修正）**" : "出ない（修正済み）")} |");
        Console.WriteLine();
        Console.WriteLine($"**いま出ているのは {(q1a ? 1 : 0) + (q1b ? 1 : 0) + (q1c ? 1 : 0)} / 3 件。**"
                          + "（コミット `0489513`＝(T3) の前では **3 / 3** だった。それが Q1 の答え）");
        Console.WriteLine();

        Console.WriteLine("## 表B-5. `TraitKeyMap` の突き合わせ");
        Console.WriteLine();
        Console.WriteLine("**観測したキー（読み ∪ 供給 ∪ 消費）と `TraitKeyMap.TraitKeys` の差。**"
                          + "被弾は engine と敵が供給するので外してある。");
        Console.WriteLine();
        var dvSeenKeys = new Dictionary<TraitId, HashSet<int>>();
        foreach (var kv in dvObs)
        {
            if (kv.Key.Key == UnitTally.CarryHit) continue;
            if (!dvSeenKeys.TryGetValue(kv.Key.T, out var hs)) dvSeenKeys[kv.Key.T] = hs = new HashSet<int>();
            hs.Add(kv.Key.Key);
        }
        Console.WriteLine("| 特性 | 観測したキー | 表のキー | 観測にあって表に無い |");
        Console.WriteLine("|---|---|---|---|");
        int dvKeyMiss = 0;
        foreach (var kv in dvSeenKeys.OrderBy(x => x.Key))
        {
            if (dvIntentional.Contains(kv.Key)) continue;
            var tab = (TraitKeyMap.TraitKeys.TryGetValue(kv.Key, out int[]? tk) ? tk : Array.Empty<int>()).ToHashSet();
            var extra = kv.Value.Where(k => !tab.Contains(k)).OrderBy(x => x).ToArray();
            if (extra.Length == 0) continue;
            dvKeyMiss++;
            Console.WriteLine($"| `{kv.Key}` | {string.Join(" ", kv.Value.OrderBy(x => x).Select(DvK))} "
                              + $"| {string.Join(" ", tab.OrderBy(x => x).Select(DvK))} "
                              + $"| **{string.Join(" ", extra.Select(DvK))}** |");
        }
        Console.WriteLine();
        Console.WriteLine($"**差のある特性 {dvKeyMiss} 枚。**（表の側にだけあるキーは、"
                          + "その特性が台で発火していないか観測範囲外なので出していない）");
        Console.WriteLine();

        Console.WriteLine("## 表B-6. 味方の刃（`isFriendlyFire`）の呼び出し口 —— **走らせて数える**");
        Console.WriteLine();
        Console.WriteLine("第85期に指示書の「6箇所」が実測 **10** とずれた表。**特性別に観測した。**");
        Console.WriteLine("**印が立っていない呼び出し（巨躯・分かちの中継）は最後の行にまとめてある**"
                          + "——engine の段は誰の特性でもないので、特性名を付けられない。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 回/戦 |");
        Console.WriteLine("|---|--:|");
        foreach (var kv in dvBlade.OrderByDescending(x => x.Value))
            Console.WriteLine($"| `{kv.Key}` | {kv.Value / dvBattles:F2} |");
        Console.WriteLine($"| **印なし（engine の中継）** | {dvBladeEngine / dvBattles:F2} |");
        Console.WriteLine();
        Console.WriteLine($"**特性に紐づく呼び出し口 {dvBlade.Count} 枚 ＋ engine の中継**"
                          + $"（延べ {(dvBlade.Values.Sum() + dvBladeEngine) / dvBattles:F2} 回/戦）。");
        Console.WriteLine();

        Console.WriteLine("## 表B-7. 深手（`StatusKeys.Deep`）");
        Console.WriteLine();
        Console.WriteLine("**11 キーに無い**（第93期・`DeepRule` は既定 `false` で残置）ので上の表には出ない。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 場所 | 向き | 回/戦 |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var kv in dvDeep.OrderByDescending(x => x.Value))
            Console.WriteLine($"| `{kv.Key.T}` | {dvWhereName[kv.Key.W]} "
                              + $"| {(kv.Key.Dir == 0 ? "読み" : kv.Key.Dir > 0 ? "供給" : "消費")} "
                              + $"| {kv.Value / dvBattles:F2} |");
        Console.WriteLine();
        return;
    }

    // =================================================================================
    // §5 自己検査
    // =================================================================================
    if (dvMode == "check")
    {
        Console.WriteLine("# 表E. 自己検査（第94期）");
        Console.WriteLine();

        // **コメントを落としてから grep する**（(d)(f) はコードの参照だけを見たい。
        // この診断の説明文そのものが `TraitEntryMap` と書いてあるので、素の全文だと必ず引っかかる）。
        var dvCoreLines = new List<(string File, int Line, string Text)>();
        if (dvRoot is not null)
            foreach (string f in Directory.GetFiles(Path.Combine(dvRoot, "BattleCore"), "*.cs"))
            {
                string[] ls = File.ReadAllLines(f);
                for (int i = 0; i < ls.Length; i++)
                    if (!ls[i].TrimStart().StartsWith("//")) dvCoreLines.Add((Path.GetFileName(f), i + 1, ls[i]));
            }
        string dvCore = string.Join("\n", dvCoreLines.Select(x => x.Text));

        // (a) compare 305 セル
        var dvCmp = CompareBuilds();
        var dvCells = new List<string>();
        foreach (var b in dvCmp)
        {
            var row = new List<string>();
            foreach (var st in dvStages)
            {
                int w = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                row.Add($"{w * 100.0 / 200:F1}%");
            }
            dvCells.Add($"| {b.Name} | {string.Join(" | ", row)} |");
        }
        int dvDiff = -1;
        string dvBal = dvRoot is not null ? Path.Combine(dvRoot, "docs", "balance.md") : "";
        if (File.Exists(dvBal))
        {
            var docLines = File.ReadAllLines(dvBal).Where(l => l.StartsWith("| ") && l.Contains('%')).ToArray();
            dvDiff = Math.Abs(docLines.Length - dvCells.Count);
            for (int i = 0; i < Math.Min(docLines.Length, dvCells.Count); i++)
                if (docLines[i].Trim() != dvCells[i].Trim()) dvDiff++;
        }
        Console.WriteLine($"- **(a)** `compare` {dvCmp.Length} 行 × {dvStages.Count} 波 "
                          + $"＝ {dvCmp.Length * dvStages.Count} セルを `docs/balance.md` と突き合わせ: "
                          + $"**ずれ {dvDiff} 行**{(dvDiff == 0 ? "（○）" : "（×）")}");

        // (b) 交差帯 12 行
        var dvX = CrossBuilds();
        int dvXDiff = -1;
        string dvCr = dvRoot is not null ? Path.Combine(dvRoot, "docs", "crossing.md") : "";
        if (File.Exists(dvCr))
        {
            string crText = File.ReadAllText(dvCr);
            dvXDiff = 0;
            foreach (var b in dvX)
            {
                var row = new List<string>();
                foreach (var st in dvStages)
                {
                    int w = 0;
                    for (int seed = 0; seed < 200; seed++)
                        if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                    row.Add($"{w * 100.0 / 200:F1}%");
                }
                if (!crText.Contains($"| {b.Name} | {string.Join(" | ", row)} |")) dvXDiff++;
            }
        }
        Console.WriteLine($"- **(b)** 交差帯 {dvX.Length} 行 × {dvStages.Count} 波を `docs/crossing.md` と突き合わせ: "
                          + $"**ずれ {dvXDiff} 行**{(dvXDiff == 0 ? "（○）" : "（×）")}");

        // (d) 印の参照箇所を**全部列挙する**（指示書 §5 (d)）。
        int dvBegins = dvCoreLines.Count(x => x.Text.Contains("BeginTrait("));
        int dvEnds = dvCoreLines.Count(x => x.Text.Contains("EndTrait("));
        var dvMarkLines = dvCoreLines
            .Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.Text, @"\bMark\b")).ToArray();
        Console.WriteLine($"- **(d)** 印を触るのは `BeginTrait(` {dvBegins} 箇所 ／ `EndTrait(` {dvEnds} 箇所 ／ "
                          + $"`Mark` の語 {dvMarkLines.Length} 箇所。**全部を下に列挙する**"
                          + "——記録（`NoteProbe*`）と印の上げ下げと観測子の呼び出し以外に無いことが (d) の中身。");
        Console.WriteLine();
        Console.WriteLine("| 場所 | 行 |");
        Console.WriteLine("|---|---|");
        foreach (var m in dvMarkLines)
            Console.WriteLine($"| `{m.File}:{m.Line}` | `{m.Text.Trim().Replace("|", @"\|")}` |");
        Console.WriteLine();
        Console.WriteLine("**`Traits.cs` の 4 行は別物**——`GoadRule.Mark`（第52期・駆り立てが標を付けるかのノブ）で、"
                          + "観測の印とは名前が同じだけ。**`BattleContext.Mark` を読むのは `BattleEngine.cs` の "
                          + "12 行だけで、そのすべてが記録か印の上げ下げである。**");
        Console.WriteLine();

        // (f) 表が BattleCore から参照されていないこと
        bool dvNoRef = !dvCore.Contains("TraitEntryMap") && !dvCore.Contains("TraitKeyMap")
                       && !dvCore.Contains("TraitHookMap");
        Console.WriteLine($"- **(f)** `TraitEntryMap` / `TraitKeyMap` / `TraitHookMap` が `BattleCore` から"
                          + $"参照されていない: {(dvNoRef ? "○" : "×")}"
                          + "（**表を直しても盤面は動かない**ことの根拠）");

        // (g) PickOne
        int dvPick = System.Text.RegularExpressions.Regex.Matches(dvCore, @"PickOne\(").Count;
        Console.WriteLine($"- **(g)** `BattleCore` の `PickOne(` 呼び出し {dvPick} 箇所"
                          + "（第94期は1つも足していない。`git diff` で確かめること）");
        Console.WriteLine();
        return;
    }

    Console.WriteLine("使い方: `derive rules` / `derive scan` / `derive check`");
    return;
}
}
