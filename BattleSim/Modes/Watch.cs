using BattleCore;
using static Common;

// =====================================================================================
// watch モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "watch")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 watch
// =====================================================================================

static class WatchDiag
{
// watch モード: **見る前に「どの行のどの波のどの seed を見るか」を出す地図**（第123期）。
//
// この期は採否の判定を1つも持たない。主判定・拒否権・ノイズ床・2×2 はこのモードに存在しない。
// 出すのは「観察の対象」と「観察しても画面に出得ないもの（盲点表）」の2つだけで、
// 観察そのものはポンが `design/PHASE123_WATCH_LOG.md` に書く。
//
// **戦闘は回すが盤面は1ビットも動かさない**——`compare` と同じ `BattleEngine.Run` を呼ぶだけで、
// 規則も既定も1つも触っていない（受け入れ条件: `docs/balance.md` と 305 セル 0 件）。
//
//     dotnet run --project BattleSim -c Release 0 watch phase0    # 盲点表（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 watch           # 主表（候補行 × 波 × 代表 seed）
//     dotnet run --project BattleSim -c Release 0 watch cast [行名の部分一致]   # 台本の出演内訳
public static void Run(string[] args, int stageIndex)
{
    string waMode = args.Length > 2 ? args[2] : "";
    const int WatchSeeds = 200;

    // ------------------------------------------------------------------
    // 候補行 —— 手で並べない。`Baseline.PrimaryRows`（19行・コード）と
    // 「第110〜122期に勝率が動いたと記録されている行」（下）の**和集合**。
    // ------------------------------------------------------------------
    //
    // 出どころは `design/PHASE110_*.md` 〜 `PHASE122_*.md` の報告書（Q0-9）。
    // **報告書は手書き文書なので、ここは「引いた結果」を置くしかない**——そのかわり
    // 行名は `Presets` と**完全一致で照合**し、一致しなかったものを件数つきで出す。
    //
    // 採否が盤面を動かした期だけが載る（第112・115・117〜121期は 0 行:
    // 第115期は「盤面を1ビットも動かしていない」・第120/121期は測定だけ・第117〜119期は不採用）。
    // **第111期に落とした `死軸×ホタ (ゾト×熾)` は載せない**——`Presets` から消えた行なので観察できない。
    var waMoved = new (string Name, string Phase)[]
    {
        ("灯×薙ぎ (トモ×ドルガ)",        "第111期に差し替えで新設／第114期に3セル＋席"),
        ("傷×被弾 (カド×ノミ×ノノ)",     "第111期に交差帯の終端を差し替え／第122期 −0.1"),
        ("惨禍×被弾強化",                 "第113期に席／第122期 −0.3"),
        ("澱み喰い (グザ×ヴィオ)",        "第113期に席"),
        ("縛め収入型 (クグ×バン×ガン)",   "第116期 +15.5（第四波 +43.0）"),
        ("溜め改 (クグ×バン×ガン)",       "第116期 第五波 +0.5"),
        ("追撃×毒 (ハギ×グザ)",           "第122期 +20.0（第2波 +58.5）"),
        ("毒→被弾強化 (グザ×ムド)",       "第122期 +1.1"),
        ("反撃改2 (ガン×カド)",           "第122期 +0.3"),
        ("反撃 (ヒサ×カド)",              "第122期 −0.3"),
        ("死軸×ヒヨ (ゾト×火選り)",       "第122期 −0.4"),
        ("耐久 (ガルド×ノノ)",            "第122期 −0.8"),
        ("反撃改 (ドハ×カド)",            "第122期 −1.0"),
    };

    var waAll = CompareBuilds().Concat(CrossBuilds()).ToArray();
    var waByName = new Dictionary<string, Formation>(StringComparer.Ordinal);
    foreach (var (n, f) in waAll) waByName[n] = f;

    var waMissing = new List<string>();
    var waNames = new List<string>();
    foreach (string n in Baseline.PrimaryRows)
    {
        if (!waByName.ContainsKey(n)) { waMissing.Add(n); continue; }
        if (!waNames.Contains(n, StringComparer.Ordinal)) waNames.Add(n);
    }
    int waPrimaryCount = waNames.Count;
    int waExtra = 0;
    foreach (var (n, _) in waMoved)
    {
        if (!waByName.ContainsKey(n)) { waMissing.Add(n); continue; }
        if (waNames.Contains(n, StringComparer.Ordinal)) continue;
        waNames.Add(n); waExtra++;
    }

    // ------------------------------------------------------------------
    // 軸コメント —— `Baseline.PrimaryRows` の行末コメントを**実装（Program.cs）から**引く。
    // 走査が空なら止める（第117期: 引けなかったときと「該当なし」の区別が付かない）。
    // ------------------------------------------------------------------
    string? waRoot = Directory.GetCurrentDirectory();
    while (waRoot != null && !File.Exists(Path.Combine(waRoot, "docs", "balance.md")))
        waRoot = Path.GetDirectoryName(waRoot);
    var waAxis = new Dictionary<string, string>(StringComparer.Ordinal);
    string waAxisSource = "—";
    if (waRoot != null && File.Exists(Path.Combine(waRoot, "BattleSim", "Program.cs")))
    {
        string src = File.ReadAllText(Path.Combine(waRoot, "BattleSim", "Program.cs"));
        // **アンカーは連結で組む。** 走査対象がこのファイル自身なので、検索文字列を
        // そのまま書くと**自分のコードが最初に当たる**（実際に1度踏んだ）。
        // 第117期「実装から引く分類は、引けなかったときに『該当なし』と区別が付かない」の
        // 自己参照の版で、症状は「走査が空」ではなく「別の場所を読む」。
        string waAnchor = "static class " + "Baseline";
        int a0 = src.IndexOf(waAnchor, StringComparison.Ordinal);
        int a = a0 < 0 ? -1 : src.IndexOf("PrimaryRows" + " =", a0, StringComparison.Ordinal);
        int b = a < 0 ? -1 : src.IndexOf("\n    };", a, StringComparison.Ordinal);
        if (a >= 0 && b > a)
        {
            foreach (string line in src.Substring(a, b - a).Split('\n'))
            {
                int q1 = line.IndexOf('"');
                if (q1 < 0) continue;
                int q2 = line.IndexOf('"', q1 + 1);
                int cm = line.IndexOf("//", StringComparison.Ordinal);
                if (q2 < 0 || cm < q2) continue;
                string nm = line.Substring(q1 + 1, q2 - q1 - 1);
                string ax = line.Substring(cm + 2).Trim();
                int par = ax.IndexOf('（');
                if (par > 0) ax = ax.Substring(0, par);
                int star = ax.IndexOf("**", StringComparison.Ordinal);
                if (star > 0) ax = ax.Substring(0, star);
                waAxis[nm] = ax.Trim();
            }
        }
        waAxisSource = $"BattleSim/Program.cs の `PrimaryRows` 行末コメント {waAxis.Count} 件";
    }
    if (waAxis.Count == 0)
    {
        Console.WriteLine("**走査が空**: `Baseline.PrimaryRows` の軸コメントを1件も引けなかった");
        Console.WriteLine("（リポジトリの外から叩いている可能性がある。第117期の形なのでここで止める）。");
        return;
    }

    string WaAxisOf(string name) => waAxis.TryGetValue(name, out string? v) && v.Length > 0 ? v : "追加";

    // ------------------------------------------------------------------
    // 盲点表 —— `watch phase0`。**戦闘0回。**
    // ------------------------------------------------------------------
    if (waMode == "phase0")
    {
        Console.WriteLine("# 盲点表 —— 画面に出得ないもの（第123期・`watch phase0`）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 watch phase0` の出力。手で編集しない。**戦闘0回。**");
        Console.WriteLine();
        Console.WriteLine("> **この表に載っているものは、画面で見えなくても駒の設計の問題ではない。**");
        Console.WriteLine("> 窓口が無い通貨は、駒が正しく働いていても出来事として1件も残らない。");
        Console.WriteLine();

        // (1) StatusKeys.All × StatusGain の窓口。呼び出し元を BattleEngine.cs から機械で拾う。
        var waGain = new List<string>();
        string wePath = waRoot == null ? "" : Path.Combine(waRoot, "BattleCore", "BattleEngine.cs");
        if (wePath.Length > 0 && File.Exists(wePath))
        {
            string we = File.ReadAllText(wePath);
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(
                         we, "EmitStatusGain\\(\\s*[A-Za-z_]\\w*\\s*,\\s*([A-Za-z_][\\w\\.]*)"))
                waGain.Add(m.Groups[1].Value);
        }
        if (waGain.Count == 0)
        {
            Console.WriteLine("**走査が空**: `EmitStatusGain` の呼び出し元を1件も引けなかった。ここで止める。");
            return;
        }
        var waGainKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string g in waGain)
            waGainKeys.Add(g.StartsWith("StatusKeys.", StringComparison.Ordinal) ? g.Substring("StatusKeys.".Length) : g);
        string[] waEventKindNames = Enum.GetNames(typeof(BattleEventKind));
        var waEventKinds = waEventKindNames.ToHashSet(StringComparer.Ordinal);

        Console.WriteLine("## 1. `StatusKeys.All` × 「付いた瞬間」の窓口（`StatusGain`）");
        Console.WriteLine();
        Console.WriteLine($"`StatusKeys.All` {StatusKeys.All.Length} キー ／ `EmitStatusGain` の呼び出し元 {waGain.Count} 箇所"
                          + $"（うち `StatusKeys.*` でないもの {waGain.Count(g => !g.StartsWith("StatusKeys.", StringComparison.Ordinal))} 件）。");
        Console.WriteLine();
        Console.WriteLine("| キー | 表示名 | 付いた瞬間（`StatusGain`） | 専用の種類 | 残量（`StatusSnapshot`） |");
        Console.WriteLine("|---|---|:-:|:-:|:-:|");
        int waNoGate = 0, waOwnKind = 0;
        var waKeyHasGate = new Dictionary<string, bool>(StringComparer.Ordinal);
        var waConstName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in StatusKeys.All)
        {
            // 定数名を引き当てる（`StatusKeys.Poison` の "Poison"）。値ではなく名前で照合する。
            string cname = typeof(StatusKeys).GetFields()
                .Where(fi => fi.IsLiteral && fi.FieldType == typeof(string) && (string?)fi.GetRawConstantValue() == key)
                .Select(fi => fi.Name).FirstOrDefault() ?? key;
            waConstName[key] = cname;
            bool gate = waGainKeys.Contains(cname);
            waKeyHasGate[key] = gate;
            if (!gate) waNoGate++;
            // 第147期に足した列。**`StatusGain` を持たないキーでも、専用の
            // `BattleEventKind` を持てば「付いた瞬間」は画面に出る**——転倒（第145期）・
            // 痺れ（第146期 段0）・混乱（第147期）がそれで、この列が無かったせいで
            // 3期ぶんの成果が盲点表では × のままだった。
            // **照合はキーの定数名と列挙の名前**（だから種類の名前をキーに揃えてある）。
            bool own = waEventKinds.Contains(cname);
            if (own) waOwnKind++;
            // 残量は `StatusLabels` が `StatusKeys.All` から作られるので全キー ○。
            Console.WriteLine($"| `{cname}` | {StatusKeys.LabelOf(key)} | {(gate ? "○" : "**×**")}"
                              + $" | {(own ? "○" : "**×**")} | ○ |");
        }
        Console.WriteLine();
        Console.WriteLine($"**窓口を持たないキー {waNoGate} / {StatusKeys.All.Length}"
                          + $"（うち専用の種類を持つ {waOwnKind}）。**"
                          + " 残量はターン頭に全キーぶん写る（`StatusLabels` は `StatusKeys.All` から作られる）ので、"
                          + "**「いま乗っている」は見えるが「いま書かれた」は見えない**。");
        Console.WriteLine();
        Console.WriteLine("> **「窓口」と「画面に出るか」は別の問い**（第147期 Q0-4）。`StatusGain` は"
                          + " 「engine の窓口を持つ4通貨だけが出す」を明文で持つので、窓口の無いキーは"
                          + " **専用の `BattleEventKind` を足す**ことで画面に出す——"
                          + "その3件をこの表で数えていなかったため、第147期の指示書は"
                          + "**「盲点表から1行減る」という到達不能な受け入れ条件**を書いていた。");
        Console.WriteLine();
        var waOrphan = waGainKeys.Where(k => !waConstName.Values.Contains(k, StringComparer.Ordinal)).ToArray();
        Console.WriteLine($"`StatusKeys.All` に無い窓口 {waOrphan.Length} 件: "
                          + (waOrphan.Length == 0 ? "—" : string.Join(" / ", waOrphan.Select(k => $"`{k}`")))
                          + "（なまり＝`BattleContext.DullKey`。`AtkBonus` は状態異常のカウンタではないので `All` に無い）。");
        Console.WriteLine();

        // (1-b) 「誰がやったか」が台本に載る種類・載らない種類。
        //
        // **通貨の窓口とは別の盲点**——回復・蘇生・召喚・移動は出来事としては出るのに
        // `ActorId` を持たないので、**画面では「起きたこと」は見えるが「誰がやったか」は引けない。**
        // 走査は `BattleEngine.cs` の `Emit` の初期化子から機械で引く（0件なら止める）。
        Console.WriteLine("## 1-b. 出来事の種類 × 「誰がやったか」（`ActorId`）");
        Console.WriteLine();
        var waActorOf = new Dictionary<string, bool>(StringComparer.Ordinal);
        int waEmitSites = 0;
        {
            string we = File.ReadAllText(wePath);
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(we, "Kind = BattleEventKind\\.(\\w+)"))
            {
                waEmitSites++;
                string kind = m.Groups[1].Value;
                // 初期化子の終わりまで（`});` か、次の `Kind = ` まで）を見る。
                int from = m.Index;
                int end = we.IndexOf("});", from, StringComparison.Ordinal);
                int nxt = we.IndexOf("Kind = BattleEventKind.", from + 1, StringComparison.Ordinal);
                if (end < 0) end = we.Length;
                if (nxt >= 0 && nxt < end) end = nxt;
                bool hasActor = we.IndexOf("ActorId", from, end - from, StringComparison.Ordinal) >= 0;
                waActorOf[kind] = waActorOf.GetValueOrDefault(kind) || hasActor;
            }
        }
        if (waEmitSites == 0)
        {
            Console.WriteLine("**走査が空**: `Kind = BattleEventKind.` の初期化子を1件も引けなかった。ここで止める。");
            return;
        }
        Console.WriteLine($"`BattleEventKind` {waEventKindNames.Length} 種 ／ `BattleEngine.cs` の初期化子 {waEmitSites} 箇所。");
        Console.WriteLine();
        Console.WriteLine("| 種類 | `ActorId` | 意味 |");
        Console.WriteLine("|---|:-:|---|");
        int waNoActor = 0;
        foreach (string k in waEventKindNames)
        {
            bool has = waActorOf.GetValueOrDefault(k);
            // `EmitTurnStart` / `EmitCharge` / `EmitSkill` / `EmitStatusGain` / `EmitStatusSnapshot`
            // は別メソッドなので上の走査に入らない。ここは初期化子に現れる種類だけを見る。
            if (!waActorOf.ContainsKey(k)) { Console.WriteLine($"| `{k}` | — | 初期化子が `Emit*` の中（走査対象外） |"); continue; }
            if (!has) waNoActor++;
            Console.WriteLine($"| `{k}` | {(has ? "○" : "**×**")} | {(has ? "誰がやったかが引ける" : "**起きたことは見えるが、誰がやったかは引けない**")} |");
        }
        Console.WriteLine();
        // **文も走査から組む**（第124期）。第123期は「回復・蘇生・召喚・移動がここに落ちる」と
        // 手で書いていたので、段2 で `ActorId` を足した瞬間に**表は直るが文が嘘になる**
        // ——第122期「規則を降ろすと、その規則を前提に書かれた説明文が静かに嘘になる」の生成物版。
        string[] waNoActorKinds = waEventKindNames
            .Where(k => waActorOf.ContainsKey(k) && !waActorOf[k]).ToArray();
        Console.WriteLine($"**`ActorId` を持たない種類 {waNoActor} 件**"
                          + (waNoActorKinds.Length == 0
                             ? "。**初期化子に現れるすべての種類が書き手を載せている。**"
                             : $": {string.Join(" / ", waNoActorKinds.Select(k => $"`{k}`"))}"
                               + "——**起きたことが画面に出ても「誰の仕業か」の線が引けない。**"));
        Console.WriteLine();
        Console.WriteLine("> **`Death` は `ActorId` を持つが、それは撃破した駒**（＝普通の一振りの結果）で、");
        Console.WriteLine("> 特性の痕跡ではない。`watch cast` の「特性由来」は `Death` を別に数える。");
        Console.WriteLine();

        // (2) 候補行に出る味方駒 × 通貨。
        Console.WriteLine("## 2. 候補行に出る味方駒 × 扱う通貨");
        Console.WriteLine();
        Console.WriteLine($"候補行 {waNames.Count} 行（主判定 {waPrimaryCount} ＋ 追加 {waExtra}）に登場する味方駒。");
        Console.WriteLine("通貨は `TraitKeyMap.KeysOf`（`derive scan` が観測と突き合わせている表）から引く。");
        Console.WriteLine();

        bool waDull = waGainKeys.Contains("DullKey");

        // 第124期 段2: 窓口が**書き手**を受け取るかを reflection で引く。
        // **手で書くと段2 のような変更で表だけ直って文が嘘になる**（第122期の則の生成物版）。
        // 判定は「`by` という名前の引数を持つか」——`UnitState` の数で見ると、
        // 元から2体を受け取る窓口（将来の `Transfer` 等）と区別が付かない。
        static bool WaTakesWriter(string method)
            => typeof(BattleContext).GetMethod(method)?.GetParameters()
                   .Any(x => x.Name == "by") == true;
        string waDullVia = waDull
            ? (WaTakesWriter("Dull")
               ? "`StatusGain`（`DullKey`。**書き手も載る**）"
               : "`StatusGain`（`DullKey`。**writer が null なので札は出るが線は出ない**）")
            : "**×** —";
        (string Key, string Via, bool Seen)[] waCarryGate =
        {
            ("強化", "**×** —（`AtkBonus` の窓口は `Whet` だが `StatusGain` を呼ばない。受け手の `StatSnapshot` に数字として出るだけ）", false),
            ("弱体", waDullVia, waDull),
            ("毒",   waKeyHasGate[StatusKeys.Poison]   ? "`StatusGain`" : "**×** —", waKeyHasGate[StatusKeys.Poison]),
            ("燃",   waKeyHasGate[StatusKeys.Burn]     ? "`StatusGain`" : "**×** —", waKeyHasGate[StatusKeys.Burn]),
            ("痺",   waKeyHasGate[StatusKeys.Stun]     ? "`StatusGain`" : "**×** —", waKeyHasGate[StatusKeys.Stun]),
            ("標",   waKeyHasGate[StatusKeys.Marked]   ? "`StatusGain`" : "**×** —", waKeyHasGate[StatusKeys.Marked]),
            ("破片", waKeyHasGate[StatusKeys.Armor]    ? "`StatusGain`" : "**×** —", waKeyHasGate[StatusKeys.Armor]),
            ("傷",   waKeyHasGate[StatusKeys.Wound]    ? "`StatusGain`" : "**×** —", waKeyHasGate[StatusKeys.Wound]),
            ("手番", waKeyHasGate[StatusKeys.IdleTurn] ? "`StatusGain`" : "**×** —", waKeyHasGate[StatusKeys.IdleTurn]),
            ("被弾", waEventKinds.Contains("Damage") ? "`Damage`" : "**×** —", waEventKinds.Contains("Damage")),
            ("移動", waEventKinds.Contains("Move")
                       ? (WaTakesWriter("SwapSlots") ? "`Move`（**書き手も載る**）" : "`Move`")
                       : "**×** —", waEventKinds.Contains("Move")),
        };
        if (waCarryGate.Length != UnitTally.CarryKeys.Length)
        {
            Console.WriteLine($"**通貨の数が合わない**（表 {waCarryGate.Length} / `UnitTally.CarryKeys` {UnitTally.CarryKeys.Length}）。ここで止める。");
            return;
        }
        for (int i = 0; i < waCarryGate.Length; i++)
            if (!string.Equals(waCarryGate[i].Key, UnitTally.CarryKeys[i], StringComparison.Ordinal))
            {
                Console.WriteLine($"**通貨の並びが `UnitTally.CarryKeys` と違う**（{i}: {waCarryGate[i].Key} / {UnitTally.CarryKeys[i]}）。ここで止める。");
                return;
            }

        Console.WriteLine("| # | 通貨 | 画面に出る窓口 |");
        Console.WriteLine("|--:|---|---|");
        for (int i = 0; i < waCarryGate.Length; i++)
            Console.WriteLine($"| {i} | {waCarryGate[i].Key} | {waCarryGate[i].Via} |");
        Console.WriteLine();
        Console.WriteLine("**`StatusKeys.All` にあって `UnitTally.CarryKeys` に無いキー: "
                          + $"{StatusKeys.LabelOf(StatusKeys.Deep)} / {StatusKeys.LabelOf(StatusKeys.Curse)}**"
                          + "（第68期の11本は当時の7キーから作られていて、以後に増えた2つが入っていない）。");
        Console.WriteLine();

        var waUnits = new List<UnitDef>();
        foreach (string n in waNames)
            foreach (var x in waByName[n].Occupied())
                if (!waUnits.Any(u => u.Id == x.Def.Id)) waUnits.Add(x.Def);
        Console.WriteLine($"候補行に出る味方駒 **{waUnits.Count} 体**（`UnitCatalog` の Id で数えた実体数）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 扱う通貨 | 画面に出る通貨 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        var waBlind = new List<UnitDef>();
        foreach (UnitDef d in waUnits.OrderBy(u => u.Id, StringComparer.Ordinal))
        {
            int[] keys = TraitKeyMap.KeysOf(d);
            string mine = keys.Length == 0 ? "**—（キーを1本も持たない）**" : string.Join("/", keys.Select(k => UnitTally.CarryKeys[k]));
            var seen = keys.Where(k => waCarryGate[k].Seen).ToArray();
            string seenS = seen.Length == 0 ? "**なし**" : string.Join("/", seen.Select(k => UnitTally.CarryKeys[k]));
            bool blind = seen.Length == 0;
            if (blind) waBlind.Add(d);
            Console.WriteLine($"| {d.Name} | {mine} | {seenS} | {(blind ? "**盲点**" : "○")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**窓口の無い通貨しか扱わない駒 {waBlind.Count} / {waUnits.Count} 体**: "
                          + (waBlind.Count == 0 ? "—" : string.Join(" / ", waBlind.Select(d => d.Name))));
        Console.WriteLine();
        Console.WriteLine("> この列は「通貨として画面に出るか」だけを見ている。**通常攻撃・回復・撃破・召喚は別**で、");
        Console.WriteLine("> 振れば `Attack` / `Damage` が出る。**盲点＝「その駒らしさが出ない」であって「映らない」ではない。**");
        Console.WriteLine();

        // (3) 既知の欠落（Q0-8。第97〜99期の報告書から）。
        Console.WriteLine("## 3. 既に判明している欠落（第97〜99期）");
        Console.WriteLine();
        Console.WriteLine("| | 事実 | 印 |");
        Console.WriteLine("|---|---|:-:|");
        Console.WriteLine("| 痺れ・標・破片の `StatusGain` | 窓口が無い。`Traits.cs` の16箇所から直に `SetCounter` される。第90期（毒）・第93期（傷）と同じ形で3本要る | **既知** |");
        Console.WriteLine("| 減算の窓口 | 引き取り・断ち・縫い・塞ぎは「通貨が減った」を1件も残さない。**画面では在庫が黙って消える** | **既知** |");
        // 第124期 段2 でここが埋まった。**印は reflection から引く**（手で書くと直しても文が残る）。
        Console.WriteLine(WaTakesWriter("Dull")
            ? "| `ctx.Dull` の writer | **第124期 段2 に埋めた。**"
              + "窓口が `by` を受け取り、`StatusGain` の `ActorId` に載る（第97期 §4 の (2)） | **解消** |"
            : "| `ctx.Dull` の writer | 窓口が書き手を受け取らないので、**なまりの札は出るが線は出ない**（第97期 §4 の (2)） | **既知** |");
        Console.WriteLine();
        Console.WriteLine("**第123期はこの3本に手を付けなかった**（`StatusKeys` のカウンタはバランスが載っている場所から直に書かれている）。"
                          + "**第124期 段2 は3本目（`ctx.Dull` の writer）だけを埋めた**"
                          + "——上2本は `Traits.cs` の多数箇所から直に `SetCounter` するので、まだ触っていない。");
        Console.WriteLine();

        // (4) デモ側の照合（A7）。
        Console.WriteLine("## 4. デモの編成プリセットと照合する数");
        Console.WriteLine();
        Console.WriteLine($"`Presets.Compare` **{Presets.Compare.Length}** ＋ `Presets.Cross` **{Presets.Cross.Length}** ＝ "
                          + $"**{Presets.Compare.Length + Presets.Cross.Length} 行**（`DemoApp` の「編成プリセット」に出る数）。");
        Console.WriteLine();
        Console.WriteLine($"軸コメントの出どころ: {waAxisSource}。");
        Console.WriteLine();
        Console.WriteLine($"**`Presets` に存在しない候補行 {waMissing.Count} 件**"
                          + (waMissing.Count == 0 ? "。" : ": " + string.Join(" / ", waMissing)));
        return;
    }

    // ------------------------------------------------------------------
    // 1行 × 1波を回す（`compare` と同じ呼び出し）。
    // ------------------------------------------------------------------
    (double Win, double MedTurn, double Surv, int SeedWin, int SeedLose, double Chain)
        WaCell(Formation f, EnemyCatalog.Stage st)
    {
        var turns = new int[WatchSeeds];
        var won = new bool[WatchSeeds];
        double survSum = 0, chainSum = 0;
        int wins = 0;
        for (int seed = 0; seed < WatchSeeds; seed++)
        {
            var r = BattleEngine.Run(f, st.Enemy, seed, verbose: false);
            turns[seed] = r.Turns; won[seed] = r.PlayerWon;
            chainSum += r.MaxEnemyKillsInOneTurn;
            if (r.PlayerWon) { wins++; survSum += r.PlayerSurvivors; }
        }
        var sorted = turns.OrderBy(x => x).ToArray();
        double med = WatchSeeds % 2 == 0
            ? (sorted[WatchSeeds / 2 - 1] + sorted[WatchSeeds / 2]) / 2.0
            : sorted[WatchSeeds / 2];
        // 代表 seed: 決着Tが中央値に最も近いもの。**同値は seed 昇順の最小**（決定的）。
        int Pick(bool wantWin)
        {
            int best = -1; double bestD = double.MaxValue;
            for (int seed = 0; seed < WatchSeeds; seed++)
            {
                if (won[seed] != wantWin) continue;
                double d = Math.Abs(turns[seed] - med);
                if (d < bestD - 1e-9) { bestD = d; best = seed; }
            }
            return best;
        }
        return (wins * 100.0 / WatchSeeds, med, wins > 0 ? survSum / wins : 0,
                Pick(true), Pick(false), chainSum / WatchSeeds);
    }

    // ------------------------------------------------------------------
    // `watch cast` —— 台本の出演内訳。
    // ------------------------------------------------------------------
    if (waMode == "cast")
    {
        string waFilter = args.Length > 3 ? args[3] : "";
        var waTargets = waNames.Where(n => waFilter.Length == 0 || n.Contains(waFilter, StringComparison.Ordinal)).ToArray();
        if (waTargets.Length == 0)
        {
            Console.WriteLine($"**候補行に「{waFilter}」を含む行が無い**（候補 {waNames.Count} 行）。ここで止める。");
            return;
        }

        Console.WriteLine($"# 台本の出演内訳（`watch cast`）— {waTargets.Length} 行");
        Console.WriteLine();
        Console.WriteLine("**1文（`PlusText` / `MinusText`）と、台本に実際に出た件数を横に並べる。**");
        Console.WriteLine("`通常攻撃` は `Attack` の件数と、それに紐づく `Damage` の件数");
        Console.WriteLine("（紐づけの規則は `Pattern` が非 null かつ `Reaction` が偽かつ `Relayed` が偽。Q0-6）。");
        Console.WriteLine("`特性由来` はそれに紐づかない `ActorId` 付きイベント。**0件なら画面に出ない。**");
        Console.WriteLine("**`Death`（撃破）は別列に出して判定から外す**——`ActorId` は持つが、それは");
        Console.WriteLine("普通の一振りの結果であって特性の痕跡ではない（`watch phase0` の 1-b）。");
        Console.WriteLine();

        int waScanned = 0, waAdjAll = 0, waAdjBadAll = 0;

        // 第124期 段2 の判定（P3 / A7）。**書き手が載った件数と、null のまま残った件数**を
        // 種類ごとに数える。**盤面には一切影響しない**——`r.Events` を読むだけ。
        // 「書き手が居ない経路には無理に入れない」（§5-3）ので、**null が残ること自体は正しい**。
        // 数えないと、どこが残ったのかが分からないだけである。
        var waHasActor = new Dictionary<BattleEventKind, int>();
        var waNullActor = new Dictionary<BattleEventKind, int>();

        foreach (string name in waTargets)
        {
            Formation f = waByName[name];
            // 見る波は §5-4 と同じ規則（第2〜5波で 25〜75%、50% に最も近い波）。
            // 線を満たさない行は「第2〜5波で 50% に最も近い波」に落とす（対象を必ず1つ返すため）。
            var cells = new (double Win, double MedTurn, double Surv, int SeedWin, int SeedLose, double Chain)[EnemyCatalog.Stages.Count];
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++) cells[w] = WaCell(f, EnemyCatalog.Stages[w]);
            int bestWave = 1; double bestD = double.MaxValue; bool onLine = false;
            for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
            {
                double d = Math.Abs(cells[w].Win - 50.0);
                bool band = cells[w].Win >= 25.0 && cells[w].Win <= 75.0;
                if (band && !onLine) { onLine = true; bestD = d; bestWave = w; continue; }
                if (band == onLine && d < bestD - 1e-9) { bestD = d; bestWave = w; }
            }
            var cell = cells[bestWave];

            Console.WriteLine($"## {name} ／ 第{bestWave + 1}波{(onLine ? "" : "（線の外。50% に最も近い波）")}");
            Console.WriteLine();
            Console.WriteLine($"勝率 {cell.Win:F1}% ／ 決着T中央値 {cell.MedTurn:F1} ／ 代表 seed（勝） "
                              + $"{(cell.SeedWin < 0 ? "—" : cell.SeedWin.ToString())} ／ 代表 seed（負） "
                              + $"{(cell.SeedLose < 0 ? "—" : cell.SeedLose.ToString())}");
            Console.WriteLine();

            foreach ((string label, int seed) in new[] { ("勝", cell.SeedWin), ("負", cell.SeedLose) })
            {
                if (seed < 0)
                {
                    Console.WriteLine($"### seed —（{label}の試行が 0 件。この波では{label}が存在しない）");
                    Console.WriteLine();
                    continue;
                }
                var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var foes = BattleEngine.Materialize(EnemyCatalog.Stages[bestWave].Enemy, BattleContext.EnemyTeam);
                var r = BattleEngine.Run(players, foes, seed, verbose: true);
                // **`InstanceId` は `ctx.Add` が振るので、`Run` の<b>後</b>でないと全部 0 になる**
                // （`Materialize` は盤面に載せる前の姿を作るだけ。デモの `StartBattle` も
                // `pending` を作ってから `Run` の後で `InstanceId` を読んでいる）。
                var idOf = players.ToDictionary(u => u.InstanceId, u => u.Def);
                var startId = f.Occupied().ToDictionary(x => x.Def.Id,
                                  x => players.First(u => u.Def.Id == x.Def.Id).InstanceId);
                waScanned += r.Events.Count;

                Console.WriteLine($"### seed {seed}（{label}・{r.Turns}T・イベント {r.Events.Count} 件）");
                Console.WriteLine();
                Console.WriteLine("| 駒 | ＋ | − | 通常攻撃(振/着弾) | 撃破 | 特性由来 | 内訳 | 判定 |");
                Console.WriteLine("|---|---|---|--:|--:|--:|---|:-:|");

                var swings = new Dictionary<int, int>();
                var linked = new Dictionary<int, int>();
                var traitKinds = new Dictionary<int, Dictionary<BattleEventKind, int>>();
                int waAdj = 0, waAdjBad = 0;
                int lastAttacker = -1;
                foreach (BattleEvent e in r.Events)
                {
                    if (e.ActorId is null) waNullActor[e.Kind] = waNullActor.GetValueOrDefault(e.Kind) + 1;
                    else waHasActor[e.Kind] = waHasActor.GetValueOrDefault(e.Kind) + 1;
                    if (e.Kind == BattleEventKind.TurnStart) lastAttacker = -1;
                    if (e.Kind == BattleEventKind.Attack) lastAttacker = e.ActorId ?? -1;
                    if (e.ActorId is not int aid) continue;
                    if (!idOf.ContainsKey(aid)) continue;
                    if (e.Kind == BattleEventKind.Attack) { swings[aid] = swings.GetValueOrDefault(aid) + 1; continue; }
                    bool plain = e.Kind == BattleEventKind.Damage && e.Pattern is not null && !e.Reaction && !e.Relayed;
                    if (plain)
                    {
                        linked[aid] = linked.GetValueOrDefault(aid) + 1;
                        // Q0-6: その `Damage` の直前の `Attack` が同じ駒か（＝台本上で連続しているか）。
                        if (lastAttacker == aid) waAdj++; else waAdjBad++;
                        continue;
                    }
                    if (!traitKinds.TryGetValue(aid, out var kd)) traitKinds[aid] = kd = new Dictionary<BattleEventKind, int>();
                    kd[e.Kind] = kd.GetValueOrDefault(e.Kind) + 1;
                }
                waAdjAll += waAdj; waAdjBadAll += waAdjBad;

                foreach (var x in f.Occupied())
                {
                    int id = startId[x.Def.Id];
                    int sw = swings.GetValueOrDefault(id), lk = linked.GetValueOrDefault(id);
                    var kd = traitKinds.TryGetValue(id, out var k) ? k : new Dictionary<BattleEventKind, int>();
                    // **`Death` は撃破した駒の札**（普通の一振りの結果）で特性の痕跡ではないので、
                    // 別に数えて判定から外す（`watch phase0` の 1-b）。**緩める向きではなく締める向き。**
                    int kills = kd.GetValueOrDefault(BattleEventKind.Death);
                    int tr = kd.Values.Sum() - kills;
                    string br = kd.Count == 0 ? "—" : string.Join(" / ", kd.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}"));
                    Console.WriteLine($"| {x.Def.Name} | {x.Def.PlusText} | {x.Def.MinusText} | {sw}/{lk} | {kills} | {tr} | {br} "
                                      + $"| {(tr == 0 ? "**画面に出ない**" : "○")} |");
                }
                Console.WriteLine();
                var byKind = r.Events.GroupBy(e => e.Kind).OrderByDescending(g => g.Count()).ToArray();
                Console.WriteLine($"イベント総数 {r.Events.Count} 件 —— "
                                  + string.Join(" / ", byKind.Select(g => $"{g.Key} {g.Count()}")));
                Console.WriteLine();
                Console.WriteLine($"**Q0-6**: 味方の「型つき・反撃でない・中継でない」`Damage` {waAdj + waAdjBad} 件のうち、"
                                  + $"直前の `Attack` が同じ駒 **{waAdj} 件** / 違う **{waAdjBad} 件**。");
                Console.WriteLine();
            }
        }

        Console.WriteLine("---");
        Console.WriteLine();
        Console.WriteLine($"**走査したイベント {waScanned} 件。**");
        Console.WriteLine();
        Console.WriteLine($"**Q0-6 の通算**: {waAdjAll + waAdjBadAll} 件のうち連続 **{waAdjAll}** / 非連続 **{waAdjBadAll}**"
                          + $"（{(waAdjAll + waAdjBadAll == 0 ? 0.0 : waAdjAll * 100.0 / (waAdjAll + waAdjBadAll)):F1}%）。");
        Console.WriteLine();

        // ------------------------------------------------------------------
        // 第124期 段2 —— 書き手の載り具合（P3 / A7）。
        // ------------------------------------------------------------------
        Console.WriteLine("## 書き手（`ActorId`）が載った件数（第124期 段2）");
        Console.WriteLine();
        Console.WriteLine("**null のまま残るのは、その出来事に「原因になった駒」が居ない経路**"
                          + "（盤面全体の区切り・継続効果の発火・ターン頭の写し）。");
        Console.WriteLine("**無理に「動いた本人」を入れない**——それは書き手ではないので、線を引くと嘘になる（§5-3）。");
        Console.WriteLine();
        Console.WriteLine("| 種類 | 書き手あり | null | null 率 |");
        Console.WriteLine("|---|--:|--:|--:|");
        int waHaveTot = 0, waNullTot = 0;
        foreach (BattleEventKind k in Enum.GetValues<BattleEventKind>())
        {
            int have = waHasActor.GetValueOrDefault(k), none = waNullActor.GetValueOrDefault(k);
            if (have + none == 0) continue;
            waHaveTot += have; waNullTot += none;
            Console.WriteLine($"| `{k}` | {have} | {none} | {none * 100.0 / (have + none):F1}% |");
        }
        Console.WriteLine($"| **計** | **{waHaveTot}** | **{waNullTot}** | "
                          + $"**{(waHaveTot + waNullTot == 0 ? 0.0 : waNullTot * 100.0 / (waHaveTot + waNullTot)):F1}%** |");
        if (waScanned == 0)
        {
            Console.WriteLine();
            Console.WriteLine("**0 件は異常**（台本が空）。ここで止める。");
        }
        return;
    }

    // ------------------------------------------------------------------
    // 主表 —— `watch`。
    // ------------------------------------------------------------------
    Console.WriteLine("# 見る地図 —— 候補行 × 波 × 代表 seed（第123期）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 watch > docs/watch.md` に続けて");
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 watch phase0 >> docs/watch.md` の出力。手で編集しない。");
    Console.WriteLine($"seed 0..{WatchSeeds - 1} の {WatchSeeds} 試行（`compare` と同じ帯）。**盤面は1ビットも動かない。**");
    Console.WriteLine();
    Console.WriteLine("> **盲点表はこの文書の後半（`watch phase0`）。**");
    Console.WriteLine("> **そこに載っているものは、画面で見えなくても駒の設計の問題ではない。**");
    Console.WriteLine();
    Console.WriteLine($"**候補行 {waNames.Count} 行 ＝ 主判定 {waPrimaryCount} ＋ 追加 {waExtra}**"
                      + $"（`Presets` に存在しない名前 **{waMissing.Count} 件**）。");
    foreach (string n in waMissing) Console.WriteLine($"- **見つからない**: {n}");
    Console.WriteLine();
    Console.WriteLine("`決着T` は 200 試行**全部**の中央値（勝敗を分けない）。代表 seed はこの中央値に");
    Console.WriteLine("最も近い試行で、**同値は seed 昇順の最小**（同じ HEAD で何度回しても同じ seed）。");
    Console.WriteLine("`残存` は勝った試行だけの平均生存数。`連鎖` は1ターンの最大同時撃破数の平均。");
    Console.WriteLine("片側が存在しない波は `—`。`情報帯` は勝率が 0.0% でも 100.0% でもない波。");
    Console.WriteLine();

    var waCells = new List<(string Name, int Wave, double Win, double Med, double Surv, int SW, int SL, double Ch)>();
    foreach (string name in waNames)
    {
        Formation f = waByName[name];
        string seats = string.Join(" / ", f.Occupied().Select(x => $"{FormationRules.SeatNames[x.Slot]} {x.Def.Name}"));
        Console.WriteLine($"## {name}");
        Console.WriteLine();
        Console.WriteLine($"軸: **{WaAxisOf(name)}** ／ 席: {seats}");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 決着T | 残存 | 連鎖 | 代表 seed（勝） | 代表 seed（負） | 情報帯 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|:-:|");
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
        {
            var c = WaCell(f, EnemyCatalog.Stages[w]);
            waCells.Add((name, w, c.Win, c.MedTurn, c.Surv, c.SeedWin, c.SeedLose, c.Chain));
            string tag = w == 0 ? "第1波（教習）" : $"第{w + 1}波";
            Console.WriteLine($"| {tag} | {c.Win:F1}% | {c.MedTurn:F1} | {(c.Win > 0 ? c.Surv.ToString("F1") : "—")} "
                              + $"| {c.Chain:F2} | {(c.SeedWin < 0 ? "—" : c.SeedWin.ToString())} "
                              + $"| {(c.SeedLose < 0 ? "—" : c.SeedLose.ToString())} "
                              + $"| {(c.Win > 0.0 && c.Win < 100.0 ? "○" : "")} |");
        }
        Console.WriteLine();
    }

    // ------------------------------------------------------------------
    // 推奨の見る順（§5-4）。**線と採る条件は同じ集合で書く（(G16)）。**
    // ------------------------------------------------------------------
    Console.WriteLine("---");
    Console.WriteLine();
    Console.WriteLine("# 推奨の見る順");
    Console.WriteLine();
    Console.WriteLine("**線**: 候補行のうち、**第2〜5波のいずれかで勝率が 25.0%〜75.0% にある行**。");
    Console.WriteLine("その行の中で **50% に最も近い波**を1つ選ぶ（同距離なら波番号の小さい方）。");
    Console.WriteLine();
    Console.WriteLine("**採る条件**: 線を満たした行を `Baseline.PrimaryRows` の軸コメントで **1軸につき1行**に絞り、");
    Console.WriteLine("50% に近い順に**上位6行**。軸コメントの無い追加行は「追加」という1つの軸として扱う。");
    Console.WriteLine();

    var waLine = new List<(string Name, int Wave, double Win, int SW, int SL, string Axis)>();
    foreach (string name in waNames)
    {
        int best = -1; double bestD = double.MaxValue;
        foreach (var c in waCells.Where(x => x.Name == name && x.Wave >= 1))
        {
            if (c.Win < 25.0 || c.Win > 75.0) continue;
            double d = Math.Abs(c.Win - 50.0);
            if (d < bestD - 1e-9) { bestD = d; best = c.Wave; }
        }
        if (best < 0) continue;
        var cell = waCells.First(x => x.Name == name && x.Wave == best);
        waLine.Add((name, best, cell.Win, cell.SW, cell.SL, WaAxisOf(name)));
    }

    var waTaken = new List<(string Name, int Wave, double Win, int SW, int SL, string Axis)>();
    var waUsedAxis = new HashSet<string>(StringComparer.Ordinal);
    int waDroppedByAxis = 0;
    foreach (var c in waLine.OrderBy(x => Math.Abs(x.Win - 50.0)).ThenBy(x => x.Name, StringComparer.Ordinal))
    {
        if (!waUsedAxis.Add(c.Axis)) { waDroppedByAxis++; continue; }
        if (waTaken.Count < 6) waTaken.Add(c);
    }

    Console.WriteLine($"**線を満たした行 {waLine.Count} / {waNames.Count}。"
                      + $"軸の重複で落ちた行 {waDroppedByAxis}。軸の数 {waUsedAxis.Count}。採った行 {waTaken.Count}。**");
    if (waTaken.Count < 6)
        Console.WriteLine($"**6 行に満たない（{waTaken.Count} 行）。線は緩めない**（(G16)・(G11)）。");
    Console.WriteLine();
    Console.WriteLine("| # | 行名 | 軸 | 波 | 勝率 | 代表 seed（勝） | 代表 seed（負） |");
    Console.WriteLine("|--:|---|---|---|--:|--:|--:|");
    for (int i = 0; i < waTaken.Count; i++)
    {
        var c = waTaken[i];
        Console.WriteLine($"| {i + 1} | {c.Name} | {c.Axis} | 第{c.Wave + 1}波 | {c.Win:F1}% "
                          + $"| {(c.SW < 0 ? "—" : c.SW.ToString())} | {(c.SL < 0 ? "—" : c.SL.ToString())} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**{waTaken.Count} 行 × 勝敗2本 ＝ "
                      + $"{waTaken.Count(x => x.SW >= 0) + waTaken.Count(x => x.SL >= 0)} 戦。**");
    Console.WriteLine();
    Console.WriteLine("線は満たしたが採らなかった行（**掛かっているが効かなかったことも記録する**・第114期）:");
    Console.WriteLine();
    // 落ちた理由を分ける。**「軸が重なった」と「6行の枠が埋まった」は別の落ち方**で、
    // (G16) が数えろと言っているのは前者だけ。
    var waSeenAxis = new HashSet<string>(StringComparer.Ordinal);
    bool waAnyDropped = false;
    foreach (var c in waLine.OrderBy(x => Math.Abs(x.Win - 50.0)).ThenBy(x => x.Name, StringComparer.Ordinal))
    {
        bool firstOfAxis = waSeenAxis.Add(c.Axis);
        if (waTaken.Any(t => string.Equals(t.Name, c.Name, StringComparison.Ordinal))) continue;
        waAnyDropped = true;
        Console.WriteLine($"- {c.Name}（軸 {c.Axis} ／ 第{c.Wave + 1}波 {c.Win:F1}%）"
                          + $" —— {(firstOfAxis ? "**上位6行の枠**" : "**軸の重複**")}");
    }
    if (!waAnyDropped) Console.WriteLine("- （なし）");
    Console.WriteLine();
    Console.WriteLine("**手順**: `DemoApp` を起動し、ヘッダの「編成プリセット」で行名を選び、"
                      + "「敵ウェーブ」で波を、「戦闘 seed」で代表 seed を入れて「この配置で出撃」。");
    Console.WriteLine("気づいたことは `design/PHASE123_WATCH_LOG.md` に書く。");
    Console.WriteLine();
    Console.WriteLine("**連鎖深度の内訳は `docs/chain.md` を横に置いて読むこと**"
                      + "（この表の `連鎖` は行 × 波の全試行平均で、`chain.md` は全ステージ通算なので分母が違う）。");
    return;
}
}
