using System.Text.RegularExpressions;
using BattleCore;

// =====================================================================================
// ember モード（第130期） —— 火を配る（ホタ）
//
// 第129期の本当の結論は「**鍵が他人依存の駒は、時間を買っても働かない。
// 要るのは時間ではなく鍵の供給である**」だった（延命台の密度がトメ ×0.55・ハギ ×0.72）。
// この期は**鍵の供給を増やす側**に回る。最初の対象は**燃焼**。
//
//   ボルグ ＝ 火を**作る**駒（無条件・敵にも隣の味方にも）
//   ホタ   ＝ 火を**配る**駒（**燃えている間だけ**・**味方の隣だけ**）  ← 第130期に足した
//
// **これは「起点を増やす」のではなく「起点から先の伝播を増やす」案である。**
// ホタは自分では着火できないので、**ボルグがいない編成では何も変わらない**（P5）。
//
// **主判定は勝率ではなく P1（燃焼していたターンの割合）。**
//   90% 超  → 「点きっぱなし」で判断が消えている。採らない
//   40〜85% → 採用
//   40% 未満 → 効いていない。採らない
//
//     dotnet run --project BattleSim -c Release 0 ember phase0  # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 ember run     # 段1 の測定（主判定 P1 ＋ 盤面）
//     dotnet run --project BattleSim -c Release 0 ember check [採用前のbalance.md]
// =====================================================================================

/// <summary>
/// 第130期の走査。<b>すべて実装から引く</b>（第94期の作法）。
/// <b>走査が空なら異常として止める</b>（第117期。引けなかったときと「該当なし」は区別がつかない）。
/// </summary>
static class RelayScan
{
    public static string Root { get; } = FindRoot();

    static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "BattleCore", "Traits.cs")))
            d = d.Parent;
        return d?.FullName ?? "";
    }

    public static string Read(string rel)
    {
        if (Root.Length == 0) return "";
        string p = Path.Combine(Root, rel);
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    /// <summary>走査が空なら止める（第117期）。</summary>
    public static bool Guard(string what, int count)
    {
        Console.WriteLine($"走査 `{what}`: **{count} 件**"
            + (count == 0 ? " —— **0 件。異常として止める**（第117期）" : ""));
        return count > 0;
    }

    /// <summary>
    /// ある呼び出しがどの型の中にあるかを、直前の宣言から引く。
    ///
    /// <para><b>`class` だけを見てはいけない。</b> 第130期の Phase 0 で
    /// <c>SoakRule</c>（`record struct`）の中の <c>Kinds</c> が直前の
    /// <c>ImmobileTrait</c> に結び付き、<b>「棘鎧のカドが燃焼を読む」という嘘の行が出た</b>
    /// ——`record struct` / `struct` / `interface` も宣言として数える。
    /// 第121期（引けているのに結ばれない）・第129期（結ばれ過ぎる）に続く3例目で、
    /// <b>症状は「走査が空」ではなく「静かに違う表を作る」</b>（第123期）。</para>
    /// </summary>
    public static string TypeAt(string src, int index)
    {
        var ms = Regex.Matches(src[..index],
            @"\b(?:class|record\s+struct|record|struct|interface)\s+(\w+)");
        return ms.Count == 0 ? "?" : ms[^1].Groups[1].Value;
    }

    /// <summary>その特性を持つ味方の駒（<c>UnitCatalog.All</c> の中だけ）。</summary>
    public static string Holders(TraitId t)
    {
        var l = UnitCatalog.All.Where(d => d.Traits.Contains(t)).Select(d => d.Name).ToList();
        return l.Count == 0 ? "**味方に保持者なし**" : string.Join("・", l);
    }

    /// <summary>クラス名 → そのクラスが宣言している <c>TraitId</c>（登録の側からも引く＝第127期）。</summary>
    public static List<TraitId> IdsOf(string cls, string src)
    {
        var ids = new List<TraitId>();
        foreach (Match m in Regex.Matches(src, @"TraitId\s+Id\s*=>\s*TraitId\.(\w+)"))
            if (TypeAt(src, m.Index) == cls && Enum.TryParse(m.Groups[1].Value, out TraitId id)) ids.Add(id);
        foreach (Match m in Regex.Matches(src, $@"new {Regex.Escape(cls)}\(TraitId\.(\w+)"))
            if (Enum.TryParse(m.Groups[1].Value, out TraitId id)) ids.Add(id);
        return ids.Distinct().ToList();
    }
}

static class RelayDiag
{
    const int Seeds = 200;

    static readonly string BorgId = UnitCatalog.Borg.Id;
    static readonly string HotaId = UnitCatalog.Hota.Id;
    static readonly string HiyoId = UnitCatalog.Hiyo.Id;
    static readonly string ZotoId = UnitCatalog.Zoto.Id;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); break;
            case "run": Stage1(); break;
            case "check": Check(arg); break;
            default: Console.WriteLine("ember: モードは phase0 / run / check。"); break;
        }
    }

    // ==================================================================================
    // Phase 0
    // ==================================================================================
    static void Phase0()
    {
        Console.WriteLine("# 第130期 Phase 0 —— 火を配る（ホタ）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 ember phase0` の出力。**戦闘0回。**");
        Console.WriteLine();
        if (RelayScan.Root.Length == 0)
        {
            Console.WriteLine("**リポジトリの走査に失敗した。止める**（第117期）。");
            return;
        }
        string traits = RelayScan.Read("BattleCore/Traits.cs");
        string engine = RelayScan.Read("BattleCore/BattleEngine.cs");
        if (traits.Length == 0 || engine.Length == 0)
        {
            Console.WriteLine("**`BattleCore` の走査が空。止める**（第117期）。");
            return;
        }

        Q01(traits);
        Q02(traits, engine);
        Q03(engine);
        Q04(engine);
        Q05();
        Q06();
        Q07();
        Q08();
        Q09();
    }

    static void Q01(string traits)
    {
        Console.WriteLine("## Q0-1 —— 燃焼の書き手の全件");
        Console.WriteLine();
        var hits = Regex.Matches(traits, @"ctx\.Ignite\(").Cast<Match>().ToList();
        if (!RelayScan.Guard("ctx.Ignite( in Traits.cs", hits.Count)) return;
        Console.WriteLine();
        Console.WriteLine("| クラス | `TraitId` | 味方の保持者 | 宛先 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var g in hits.GroupBy(m => RelayScan.TypeAt(traits, m.Index)))
        {
            var ids = RelayScan.IdsOf(g.Key, traits);
            string who = ids.Count == 0 ? "?" : string.Join("・", ids.Select(RelayScan.Holders));
            string dest = string.Join(" / ", g.Select(m =>
            {
                string tail = traits.Substring(m.Index, Math.Min(80, traits.Length - m.Index));
                return tail.Contains("friendly: true") ? "味方" : "敵";
            }).Distinct());
            Console.WriteLine($"| `{g.Key}` | {(ids.Count == 0 ? "—" : string.Join("・", ids))} | {who} | {dest} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**`BlazeRule` の既定は `{BlazeRule.Default.Targets}`**"
            + $"（味方 {BlazeRule.Default.Allies} / 敵 {BlazeRule.Default.Foes}）。");
        Console.WriteLine();
        if (BlazeRule.Default.Targets != BlazeTargets.None)
            Console.WriteLine("> **指示書 §0-3 の「燃焼の起点は `CinderTrait`（ボルグ）1枚しかない」は誤り。**"
                + " 破裂（`BomberTrait`・ゾト）は**第59期に着火が採用済み**（既定 `Both`）なので、"
                + "**起点は 2 枚**である。ただしゾトの着火は**自分が倒れたとき**なので"
                + "**蘇生が無ければ 1.00 回/戦**（第59期）——**毎ターン供給する起点はボルグ1枚**で、"
                + "指示書の見立ての要（供給が細い）は成り立つ。");
        else
            Console.WriteLine("> 破裂の着火は既定で無効なので、**起点はボルグ1枚**。");
        Console.WriteLine();
    }

    static void Q02(string traits, string engine)
    {
        Console.WriteLine("## Q0-2 —— 燃焼の読み手の全件");
        Console.WriteLine();
        var hits = Regex.Matches(traits, @"StatusKeys\.Burn").Cast<Match>().ToList();
        var eng = Regex.Matches(engine, @"StatusKeys\.Burn").Cast<Match>().ToList();
        if (!RelayScan.Guard("StatusKeys.Burn in Traits.cs", hits.Count)) return;
        RelayScan.Guard("StatusKeys.Burn in BattleEngine.cs", eng.Count);
        Console.WriteLine();
        Console.WriteLine("| クラス | 件数 | `TraitId` | 味方の保持者 |");
        Console.WriteLine("|---|--:|---|---|");
        foreach (var g in hits.GroupBy(m => RelayScan.TypeAt(traits, m.Index)))
        {
            var ids = RelayScan.IdsOf(g.Key, traits);
            Console.WriteLine($"| `{g.Key}` | {g.Count()} | {(ids.Count == 0 ? "—" : string.Join("・", ids))} "
                + $"| {(ids.Count == 0 ? "—" : string.Join("・", ids.Select(RelayScan.Holders)))} |");
        }
        Console.WriteLine();
        Console.WriteLine("**書き手と読み手の枚数**（味方の駒で数える）:");
        Console.WriteLine();
        Console.WriteLine($"- 書き手: 火の粉 {RelayScan.Holders(TraitId.Cinder)} ／ "
            + $"破裂 {RelayScan.Holders(TraitId.Bomber)}"
            + (BlazeRule.Default.Targets == BlazeTargets.None ? "（既定で着火しない）" : "（`OnDeath` なので1戦に1回）")
            + $" ／ **熾火 {RelayScan.Holders(TraitId.Pyre)}（第130期に足した配布）**");
        Console.WriteLine($"- 読み手: 熾火 {RelayScan.Holders(TraitId.Pyre)} ／ 火選り {RelayScan.Holders(TraitId.Favor)}");
        Console.WriteLine();
    }

    static void Q03(string engine)
    {
        Console.WriteLine("## Q0-3 —— `Ignite` の再付与の挙動");
        Console.WriteLine();
        bool sets = Regex.IsMatch(engine, @"target\.SetCounter\(StatusKeys\.Burn,\s*turns\)");
        bool adds = Regex.IsMatch(engine,
            @"SetCounter\(StatusKeys\.Burn,[^)]*RawCounter\(StatusKeys\.Burn\)\s*\+");
        RelayScan.Guard("target.SetCounter(StatusKeys.Burn, turns)", sets ? 1 : 0);
        Console.WriteLine();
        Console.WriteLine($"- 残ターンは **{(sets ? "設定（`SetCounter`）" : "?")}**。"
            + $"加算の形は **{(adds ? "ある" : "0 件")}**"
            + " ——**既燃への再付与は持続のリセットにしかならない**（ダメージは増えない）。");
        Console.WriteLine($"- `BurnRules.Turns` = **{BurnRules.Turns}** ／ "
            + $"`BurnRules.Damage` = **{BurnRules.Damage}**");
        Console.WriteLine();
        Console.WriteLine("> **これが P1 の根拠。** 設定である以上、ボルグとホタが隣接して毎ターン互いに戻し合えば"
            + "**燃焼が永続しうる**。永続したら「点いたり消えたり」という判断が消える。");
        Console.WriteLine();
    }

    static void Q04(string engine)
    {
        Console.WriteLine("## Q0-4 —— `OnAfterAttack` の呼ばれ方");
        Console.WriteLine();
        var calls = Regex.Matches(engine, @"OnAfterAttack\(").Cast<Match>().ToList();
        if (!RelayScan.Guard("OnAfterAttack( in BattleEngine.cs", calls.Count)) return;
        Console.WriteLine();
        foreach (Match m in calls)
        {
            int ls = engine.LastIndexOf('\n', m.Index) + 1;
            int le = engine.IndexOf('\n', m.Index);
            string line = engine[ls..(le < 0 ? engine.Length : le)].Trim();
            int no = engine[..m.Index].Count(c => c == '\n') + 1;
            Console.WriteLine($"- `BattleEngine.cs:{no}`  `{line}`");
        }
        Console.WriteLine();
        Console.WriteLine("**主目標に対して攻撃1回につき1度**（`CLAUDE.md`「特性の発動（`OnAfterAttack`）は"
            + "攻撃1回につき1度、主目標に対してのみ」）。");
        Console.WriteLine();
        Console.WriteLine("> **だからホタが貫きで3体抜いても配布は1回**（P6）。"
            + "範囲持ちが燃焼の供給を独占して「誰に着火役をやらせるか」の判断が消えることはない。");
        Console.WriteLine();
    }

    static void Q05()
    {
        Console.WriteLine("## Q0-5 —— 隣接の定義と、ホタの席ごとの隣の数");
        Console.WriteLine();
        string[] nm = { "前1", "前3", "中央", "後1", "後3", "○中1", "○中3", "○前2", "○後2" };
        Console.WriteLine("`FormationRules.AreAdjacent` は**前後を含む**（`Models.cs` の但し書き）。"
            + "`編成枠` は 0-4 ／ `召喚枠` は 5-8。");
        Console.WriteLine();
        Console.WriteLine("| ホタの席 | 隣接する編成枠 | 隣接する召喚枠 | 合計 |");
        Console.WriteLine("|---|---|---|--:|");
        for (int s = 0; s < 5; s++)
        {
            var play = Enumerable.Range(0, 5).Where(t => t != s && FormationRules.AreAdjacent(s, t)).ToList();
            var summ = Enumerable.Range(5, 4).Where(t => FormationRules.AreAdjacent(s, t)).ToList();
            Console.WriteLine($"| {nm[s]} | {string.Join("・", play.Select(t => nm[t]))} "
                + $"| {(summ.Count == 0 ? "—" : string.Join("・", summ.Select(t => nm[t])))} "
                + $"| {play.Count + summ.Count} |");
        }
        Console.WriteLine();
        foreach ((string name, Formation f) in Presets.Compare)
        {
            var occ = f.Occupied().ToList();
            if (!occ.Any(o => o.Def.Id == HotaId)) continue;
            int hs = occ.First(o => o.Def.Id == HotaId).Slot;
            var nb = occ.Where(o => o.Def.Id != HotaId && FormationRules.AreAdjacent(hs, o.Slot))
                        .Select(o => o.Def.Name).ToList();
            bool borgNext = occ.Any(o => o.Def.Id == BorgId && FormationRules.AreAdjacent(hs, o.Slot));
            Console.WriteLine($"- `{name}`: ホタは **{nm[hs]}**、隣は {string.Join("・", nb)}"
                + $" —— **ボルグと隣接 {(borgNext ? "○" : "×")}**");
        }
        Console.WriteLine();
    }

    static void Q06()
    {
        Console.WriteLine("## Q0-6 —— 燃焼を含む `compare` 行");
        Console.WriteLine();
        var rows = new List<(string Name, bool B, bool H, bool Y, bool Z)>();
        foreach ((string name, Formation f) in Presets.Compare)
        {
            var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
            bool b = ids.Contains(BorgId), h = ids.Contains(HotaId);
            bool y = ids.Contains(HiyoId), z = ids.Contains(ZotoId);
            if (b || h || y || z) rows.Add((name, b, h, y, z));
        }
        if (!RelayScan.Guard("燃焼に関わる compare 行", rows.Count)) return;
        Console.WriteLine();
        Console.WriteLine("| 編成 | ボルグ | ホタ | ヒヨ | ゾト | 主判定 |");
        Console.WriteLine("|---|:-:|:-:|:-:|:-:|:-:|");
        foreach (var r in rows)
            Console.WriteLine($"| {r.Name} | {(r.B ? "○" : "")} | {(r.H ? "○" : "")} "
                + $"| {(r.Y ? "○" : "")} | {(r.Z ? "○" : "")} "
                + $"| {(Baseline.PrimaryRows.Contains(r.Name) ? "○" : "")} |");
        Console.WriteLine();
        int nb = rows.Count(r => r.B), nh = rows.Count(r => r.H);
        int nhb = rows.Count(r => r.H && r.B), nhnb = rows.Count(r => r.H && !r.B);
        Console.WriteLine($"**ボルグを含む行 {nb} / {Presets.Compare.Length}** ／ "
            + $"**ホタを含む行 {nh}**（うち**ボルグと同席 {nhb}**・**ボルグ無し {nhnb}**）。");
        Console.WriteLine($"主判定19行のうち該当は "
            + $"**{Baseline.PrimaryRows.Count(n => rows.Any(r => r.Name == n))} 行**（規約 (G4)）。");
        Console.WriteLine();
        if (nhnb == 0)
            Console.WriteLine("> **P5 を分ける行が 0 行。** ホタを含む行はすべてボルグと同席しているので、"
                + "**「ボルグを含まずホタを含む行は ±0.0」は `compare` 61 行では確かめられない**"
                + "——確かめられるのは「**ホタを含まない行が ±0.0**」のほうである（A7 はそちらで読む）。"
                + "**判定式が読むセルが存在するかを先に数える**（第118期・規約 (G4)）。");
        Console.WriteLine();
    }

    static void Q07()
    {
        Console.WriteLine("## Q0-7 —— 燃焼していたターンの割合は測れるか");
        Console.WriteLine();
        var t = typeof(UnitTally);
        bool ticks = t.GetField("BurnTicks") is not null;
        bool life = t.GetField("LastActiveTurn") is not null;
        bool writes = t.GetField("SoakBurnWrites") is not null;
        Console.WriteLine($"- `UnitTally.BurnTicks`（燃えたターンの延べ数）: **{(ticks ? "ある" : "無い")}**");
        Console.WriteLine($"- `UnitTally.LastActiveTurn`（生存T）: **{(life ? "ある" : "無い")}**");
        Console.WriteLine($"- `UnitTally.SoakBurnWrites`（**書き手側**に帰属する `Ignite` の回数）: "
            + $"**{(writes ? "ある" : "無い")}**");
        Console.WriteLine();
        Console.WriteLine(ticks && life && writes
            ? "> **計数は1本も足さない。** P1（燃焼していたターンの割合）は "
              + "`BurnTicks ÷ LastActiveTurn` で既存の計数から引ける。"
              + "配布回数も `SoakBurnWrites`（`Ignite` が `source` に帰属させる）で引ける"
              + "——**engine に足した計数は 0 本**（受け入れ条件 A2）。"
            : "> **計数を足す必要がある。**");
        Console.WriteLine();
    }

    static void Q08()
    {
        Console.WriteLine("## Q0-8 —— ホタの説明文");
        Console.WriteLine();
        UnitDef h = UnitCatalog.Hota;
        Console.WriteLine($"- `PlusText`  : {h.PlusText}");
        Console.WriteLine($"- `MinusText` : {h.MinusText}");
        Console.WriteLine($"- `Flavor`    : {h.Flavor}");
        Console.WriteLine();
        Console.WriteLine("**足したのは `PlusText` の1節だけ**（「攻撃するたび隣接する味方にも火が移る」）。"
            + "`MinusText` と `Flavor` は1文字も変えていない"
            + "——**「自分では火を点けられない」は今も真であり、この案の要でもある。**");
        Console.WriteLine();
    }

    static void Q09()
    {
        Console.WriteLine("## Q0-9 —— 過去に燃焼の伝播を測っていないか");
        Console.WriteLine();
        string dir = Path.Combine(RelayScan.Root, "design");
        if (!Directory.Exists(dir)) { Console.WriteLine("`design/` が無い。**止める**（第117期）。"); return; }
        var files = Directory.GetFiles(dir, "*.md");
        if (!RelayScan.Guard("design/*.md", files.Length)) return;
        var hit = new List<(string F, int N)>();
        foreach (string f in files)
        {
            int n = Regex.Matches(File.ReadAllText(f), "燃え移|火が移|燃焼の伝播|火を配").Count;
            if (n > 0) hit.Add((Path.GetFileName(f), n));
        }
        Console.WriteLine();
        Console.WriteLine($"「燃え移／火が移／燃焼の伝播／火を配」を含むファイル **{hit.Count} 件**:");
        Console.WriteLine();
        foreach (var (f, n) in hit.OrderByDescending(x => x.N).Take(12))
            Console.WriteLine($"- `design/{f}` … {n} 件");
        Console.WriteLine();
    }

    // ==================================================================================
    // 段1 の測定
    // ==================================================================================

    sealed class RelayAcc
    {
        public double Ticks, Life, Writes, Lit, Relit, Dealt;
        public int N;
    }

    static void Stage1()
    {
        Console.WriteLine("# 第130期 段1 —— 火を配る（測定）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 ember run` の出力。");
        Console.WriteLine($"`compare` {Presets.Compare.Length} 行 × 5 波 × seed 0..{Seeds - 1} を 2 版。");
        Console.WriteLine("**V0 ＝ `EmberRule.Off`（配らない ＝ 現在の既定）／ "
            + "V1 ＝ `EmberRule.On`（配る）。**");
        Console.WriteLine();

        // **V1 は `EmberRule.On`。** 第130期に測って採用しなかったので既定は `Off` になっている
        // ——`Default` を使うと V0 と V1 が同じものを指す（第60期の事故と同じ形）。
        var vers = new (string Name, EmberRule Rule)[] { ("V0", EmberRule.Off), ("V1", EmberRule.On) };
        var cell = new Dictionary<string, double[,]>();
        var per = new Dictionary<string, Dictionary<string, RelayAcc>>();
        var favorTo = new Dictionary<string, Dictionary<string, int>>();

        var fireRows = Presets.Compare
            .Select((b, i) => (I: i, b.Name, Ids: b.F.Occupied().Select(o => o.Def.Id).ToHashSet()))
            .Where(x => x.Ids.Contains(HotaId) || x.Ids.Contains(BorgId)).ToList();

        foreach ((string vn, EmberRule rule) in vers)
        {
            var g = new double[Presets.Compare.Length, EnemyCatalog.Stages.Count];
            var acc = new Dictionary<string, RelayAcc>();
            var ft = new Dictionary<string, int>();
            for (int bi = 0; bi < Presets.Compare.Length; bi++)
            {
                (string name, Formation f) = Presets.Compare[bi];
                bool here = fireRows.Any(r => r.I == bi);
                for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed,
                                                          verbose: false, ember: rule);
                        if (r.PlayerWon) wins++;
                        if (!here || w == 0) continue;
                        foreach ((int _, UnitDef d) in f.Occupied())
                        {
                            if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
                            if (!acc.TryGetValue(d.Id, out RelayAcc? a)) acc[d.Id] = a = new RelayAcc();
                            a.N++;
                            a.Ticks += t.BurnTicks; a.Life += t.LastActiveTurn;
                            a.Writes += t.SoakBurnWrites; a.Lit += t.BurnLit; a.Relit += t.BurnRelit;
                            a.Dealt += t.DamageToEnemy;
                        }
                        foreach ((string id, int amt) in r.WhetToByRoute[(int)WhetRoute.Favor])
                            ft[id] = ft.TryGetValue(id, out int p) ? p + amt : amt;
                    }
                    g[bi, w] = wins * 100.0 / Seeds;
                }
            }
            cell[vn] = g; per[vn] = acc; favorTo[vn] = ft;
        }

        TableP1(per, fireRows);
        TableCost(per);
        TableFavor(favorTo);
        TableBoard(cell);
    }

    static string NameOf(string id) => UnitCatalog.All.FirstOrDefault(d => d.Id == id)?.Name ?? id;

    static double Rate(Dictionary<string, RelayAcc> d, string id)
        => d.TryGetValue(id, out RelayAcc? a) && a.Life > 0 ? a.Ticks / a.Life : 0;

    static double Life(Dictionary<string, RelayAcc> d, string id)
        => d.TryGetValue(id, out RelayAcc? a) && a.N > 0 ? a.Life / a.N : 0;

    static void TableP1(Dictionary<string, Dictionary<string, RelayAcc>> per,
                        List<(int I, string Name, HashSet<string> Ids)> rows)
    {
        Console.WriteLine("## 表A（主判定 P1） —— 燃焼していたターンの割合");
        Console.WriteLine();
        Console.WriteLine("`燃焼率` は `BurnTicks ÷ 生存T`（`LastActiveTurn`）。"
            + "分母は**ホタかボルグを含む行 × 第2〜5波 × 全 seed**（勝敗を問わない・規約 (G10)）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | V0 燃焼率 | **V1 燃焼率** | Δ | V0 生存T | V1 生存T "
            + "| V1 点いた/戦 | V1 煽られた/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string id in per["V1"].Keys.OrderByDescending(k => Rate(per["V1"], k)))
        {
            double r0 = Rate(per["V0"], id), r1 = Rate(per["V1"], id);
            RelayAcc a1 = per["V1"][id];
            Console.WriteLine($"| {NameOf(id)} | {r0 * 100:F1}% | **{r1 * 100:F1}%** "
                + $"| {(r1 - r0) * 100:+0.0;-0.0}pt "
                + $"| {Life(per["V0"], id):F2} | {Life(per["V1"], id):F2} "
                + $"| {a1.Lit / Math.Max(1, a1.N):F2} | {a1.Relit / Math.Max(1, a1.N):F2} |");
        }
        Console.WriteLine();

        double hb0 = Rate(per["V0"], BorgId), hb1 = Rate(per["V1"], BorgId);
        double hh0 = Rate(per["V0"], HotaId), hh1 = Rate(per["V1"], HotaId);
        Console.WriteLine($"**ボルグ {hb0 * 100:F1}% → {hb1 * 100:F1}% ／ "
            + $"ホタ {hh0 * 100:F1}% → {hh1 * 100:F1}%。**");
        Console.WriteLine();
        double key = Math.Max(hb1, hh1);
        string verdict = key > 0.90
            ? "**90% 超 ＝ 点きっぱなし。判断が消えているので、そのままでは採らない**（§5-1）"
            : key >= 0.40
            ? "**40〜85% ＝ 点いたり消えたりする。採用の側**（§5-2）"
            : "**40% 未満 ＝ 効いていない。採らない**（§5-3）";
        Console.WriteLine($"> 主判定（ボルグ・ホタの高いほう）は **{key * 100:F1}%** —— {verdict}");
        Console.WriteLine();
        Console.WriteLine($"参考: 対象行は {rows.Count} 行（{string.Join(" / ", rows.Select(r => r.Name))}）。");
        Console.WriteLine();
    }

    static void TableCost(Dictionary<string, Dictionary<string, RelayAcc>> per)
    {
        Console.WriteLine("## 表B（P4） —— 代金は効いているか");
        Console.WriteLine();
        Console.WriteLine($"燃焼は {BurnRules.Damage}/ターン。**常時燃焼になると実HPから毎ターン払う**ので、"
            + "**生存Tが下がるはず**。下がらないなら代金が効いていない＝上振れだけの変更になる。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | V0 生存T | V1 生存T | Δ | V0 与ダメ(敵)/戦 | V1 与ダメ(敵)/戦 | V1 配った/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (string id in new[] { BorgId, HotaId, HiyoId })
        {
            if (!per["V1"].ContainsKey(id)) continue;
            RelayAcc a0 = per["V0"][id], a1 = per["V1"][id];
            double l0 = Life(per["V0"], id), l1 = Life(per["V1"], id);
            Console.WriteLine($"| {NameOf(id)} | {l0:F2} | {l1:F2} | {l1 - l0:+0.00;-0.00}T "
                + $"| {a0.Dealt / Math.Max(1, a0.N):F1} | {a1.Dealt / Math.Max(1, a1.N):F1} "
                + $"| {a1.Writes / Math.Max(1, a1.N):F2} |");
        }
        Console.WriteLine();
    }

    static void TableFavor(Dictionary<string, Dictionary<string, int>> favorTo)
    {
        Console.WriteLine("## 表C（P2） —— 火選り（ヒヨ）の強化の宛先");
        Console.WriteLine();
        Console.WriteLine("`WhetToByRoute[Favor]` の総量（ホタかボルグを含む行 × 第2〜5波 × 全 seed の延べ）。");
        Console.WriteLine("**ボルグは燃焼軸の供給源でありながら、`CinderTrait` が `ally == self` で"
            + "自分を除外するので一度も燃えず、ヒヨの強化対象になれなかった**（指示書 §0-3）。");
        Console.WriteLine();
        var keys = favorTo["V0"].Keys.Union(favorTo["V1"].Keys).ToList();
        if (keys.Count == 0)
        {
            Console.WriteLine("**火選りの配布は 0 件**（ヒヨが対象行にいない）。");
            Console.WriteLine();
            return;
        }
        Console.WriteLine("| 受け手 | V0 | **V1** | Δ |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (string id in keys.OrderByDescending(k => favorTo["V1"].GetValueOrDefault(k)))
        {
            int v0 = favorTo["V0"].GetValueOrDefault(id), v1 = favorTo["V1"].GetValueOrDefault(id);
            Console.WriteLine($"| {NameOf(id)}{(id == BorgId ? "（供給源）" : "")} | {v0} | **{v1}** "
                + $"| {v1 - v0:+0;-0} |");
        }
        Console.WriteLine();
        int b0 = favorTo["V0"].GetValueOrDefault(BorgId), b1 = favorTo["V1"].GetValueOrDefault(BorgId);
        Console.WriteLine($"**ボルグが受けた強化: {b0} → {b1}。** "
            + (b0 == 0 && b1 > 0 ? "**P2 は当たり**——供給源が初めてヒヨの強化対象になった。"
               : b0 == 0 && b1 == 0 ? "**P2 は外れ**——V1 でもボルグは1点も受けていない。"
               : "**P2 は半分外れ**——V0 の時点でボルグは既に受けていた。"));
        Console.WriteLine();
    }

    static void TableBoard(Dictionary<string, double[,]> cell)
    {
        Console.WriteLine("## 表D —— 盤面（`compare` 61 行 × 5 波）");
        Console.WriteLine();
        Console.WriteLine($"`主判定` は `Baseline.PrimaryRows` 19 行の平均、"
            + $"`歯止め` は {Baseline.PrimaryFifthFloor:F1}%。"
            + "`拒否権3` は**いずれかの波で −10.0pt 以上落ちた行**の数。"
            + "`余波` は**ホタを含まない行**で動いたセルの数（A7。**0 でなければならない**）。"
            + "`情報セル` は第2〜5波で 0% でも 100% でもないセルの合計（規約 (G14)）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 主判定 第2〜5波 | 第五波 | 余裕 | 全61行 第2〜5波 "
            + "| 拒否権3 | 余波 | 情報セル |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|");

        var hotaRows = Presets.Compare.Select((b, i) => (I: i, Has: b.F.Occupied().Any(o => o.Def.Id == HotaId)))
                                      .Where(x => x.Has).Select(x => x.I).ToHashSet();

        foreach (string vn in new[] { "V0", "V1" })
        {
            double[,] g = cell[vn], g0 = cell["V0"];
            var pri = new double[EnemyCatalog.Stages.Count];
            var all = new double[EnemyCatalog.Stages.Count];
            int pn = 0;
            for (int bi = 0; bi < Presets.Compare.Length; bi++)
            {
                bool isPri = Baseline.PrimaryRows.Contains(Presets.Compare[bi].Name);
                if (isPri) pn++;
                for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                { all[w] += g[bi, w]; if (isPri) pri[w] += g[bi, w]; }
            }
            int veto = 0, spill = 0, info = 0;
            for (int bi = 0; bi < Presets.Compare.Length; bi++)
            {
                bool bad = false;
                for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                {
                    if (g[bi, w] - g0[bi, w] <= -10.0) bad = true;
                    if (!hotaRows.Contains(bi) && Math.Abs(g[bi, w] - g0[bi, w]) > 1e-9) spill++;
                    if (w >= 1 && g[bi, w] > 0.0 && g[bi, w] < 100.0) info++;
                }
                if (bad) veto++;
            }
            double fifth = pri[EnemyCatalog.Stages.Count - 1] / pn;
            Console.WriteLine($"| {vn}{(vn == "V1" ? "（配る）" : "（配らない）")} | "
                + string.Join(" / ", Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
                    .Select(w => $"{pri[w] / pn:F1}"))
                + $" | {fifth:F1}% | {fifth - Baseline.PrimaryFifthFloor:+0.0;-0.0}pt | "
                + string.Join(" / ", Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
                    .Select(w => $"{all[w] / Presets.Compare.Length:F1}"))
                + $" | {veto} | {spill} | {info} |");
        }
        Console.WriteLine();

        Console.WriteLine("### 動いた行（V1 − V0）");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(0, EnemyCatalog.Stages.Count)
            .Select(i => $" 第{i + 1}波 |")) + " ホタ | 主判定 |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, EnemyCatalog.Stages.Count)
            .Select(_ => "---:|")) + ":-:|:-:|");
        int moved = 0, movedHota = 0;
        for (int bi = 0; bi < Presets.Compare.Length; bi++)
        {
            var ds = new double[EnemyCatalog.Stages.Count];
            bool any = false;
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
            {
                ds[w] = cell["V1"][bi, w] - cell["V0"][bi, w];
                if (Math.Abs(ds[w]) > 1e-9) any = true;
            }
            if (!any) continue;
            moved++;
            if (hotaRows.Contains(bi)) movedHota++;
            Console.WriteLine($"| {Presets.Compare[bi].Name} |"
                + string.Concat(ds.Select(x => Math.Abs(x) < 1e-9 ? " ±0.0 |" : $" {x:+0.0;-0.0} |"))
                + (hotaRows.Contains(bi) ? " ○ |" : " **—** |")
                + (Baseline.PrimaryRows.Contains(Presets.Compare[bi].Name) ? " ○ |" : "  |"));
        }
        Console.WriteLine();
        Console.WriteLine($"**動いた行 {moved} 行**（うちホタを含む行 {movedHota} 行・"
            + $"**ホタを含まない行 {moved - movedHota} 行**）。");
        Console.WriteLine();
    }

    // ==================================================================================
    // 自己検査
    // ==================================================================================
    static void Check(string before)
    {
        Console.WriteLine("# 第130期 —— 自己検査");
        Console.WriteLine();

        string path = before.Length > 0 ? before : Path.Combine(RelayScan.Root, "docs", "balance.md");

        Console.WriteLine("## (1) `compare` 305 セルが `docs/balance.md` と 0 件（規約 (G8) 必須1）");
        Console.WriteLine();
        CompareToFile(path, null, "現行（既定 ＝ `EmberRule.Off`・配らない）");

        Console.WriteLine("## (2) A3 / A4 —— ホタは自分にも敵にも着火しない");
        Console.WriteLine();
        string traits = RelayScan.Read("BattleCore/Traits.cs");
        int ps = traits.IndexOf("public sealed class PyreTrait", StringComparison.Ordinal);
        int pe = traits.IndexOf("public readonly record struct EmberRule", StringComparison.Ordinal);
        string body = ps >= 0 && pe > ps ? traits[ps..pe] : "";
        if (body.Length == 0)
        {
            Console.WriteLine("**`PyreTrait` の走査が空。止める**（第117期）。");
            return;
        }
        // **コメントを落としてから数える。** `PyreTrait` の doc とコード中の注記が
        // どちらも `ally == self` に言及するので、素直に数えると 2 件になって判定が化ける
        // （第123期「走査対象が走査する側を含むと、静かに違う表を作る」の小さい版）。
        string code = string.Join("\n", body.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        int self = Regex.Matches(code, @"ally == self").Count;
        int all = Regex.Matches(code, @"ctx\.Ignite\(").Count;
        int friendly = Regex.Matches(code, @"ctx\.Ignite\(ally, friendly: true").Count;
        Console.WriteLine($"- `ally == self` の除外: **{self} 件** —— {(self == 1 ? "**A3 ○**" : "**A3 ×**")}");
        Console.WriteLine($"- `ctx.Ignite(` の全件 **{all} 件** / うち味方へ向いたもの **{friendly} 件**"
            + $" —— {(all == friendly && all > 0 ? "**A4 ○**（敵へ向いた呼び出しは 0 件）" : "**A4 ×**")}");
        Console.WriteLine();

        Console.WriteLine("## (3) A2 —— engine に新しい規則が増えていない");
        Console.WriteLine();
        string engine = RelayScan.Read("BattleCore/BattleEngine.cs");
        int emberInEngine = Regex.Matches(engine, @"Ember").Count;
        int branches = Regex.Matches(engine, @"Ember\.Enabled").Count;
        Console.WriteLine($"`BattleEngine.cs` の `Ember` の出現 **{emberInEngine} 件**"
            + "（プロパティの宣言と doc・コンストラクタの引数と代入・`Run` の引き回しだけ）。");
        Console.WriteLine($"**`Ember.Enabled` で分岐している箇所: {branches} 件** —— "
            + (branches == 0 ? "**A2 ○**（判定は `PyreTrait` の中にある。engine は1つも読まない）" : "**A2 ×**"));
        Console.WriteLine();

        Console.WriteLine("## (4) `ctx.PickOne` を新たに使っていない（規約 (G8) 必須4）");
        Console.WriteLine();
        int pick = Regex.Matches(code, "PickOne").Count;
        int roll = Regex.Matches(code, @"\bRoll\(").Count;
        Console.WriteLine($"`PyreTrait` の中の `PickOne`: **{pick} 件** ／ `Roll`: **{roll} 件** —— "
            + (pick == 0 && roll == 0 ? "**○**（乱数を1つも引かない）" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("## (5) `EmberRule.Off` が採用前の盤面と 0 件で一致する");
        Console.WriteLine();
        Console.WriteLine("**これが機構そのものの検算**——`Ignite` は乱数を1つも引かないので、"
            + "配布を足しても `Off` なら乱数列も盤面も1ビットも動かない"
            + "（比較先は**採用前の `docs/balance.md`** を渡すこと）。");
        Console.WriteLine();
        CompareToFile(path, EmberRule.Off, "V0（`EmberRule.Off`）");
    }

    static void CompareToFile(string path, EmberRule? rule, string label)
    {
        if (!File.Exists(path)) { Console.WriteLine($"`{path}` が無い。**止める**。"); return; }
        var want = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal) || !line.Contains('%')) continue;
            var c = line.Split('|').Select(x => x.Trim()).ToList();
            if (c.Count < 2 + EnemyCatalog.Stages.Count) continue;
            var v = new double[EnemyCatalog.Stages.Count];
            bool ok = true;
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                if (!double.TryParse(c[2 + w].TrimEnd('%'), out v[w])) ok = false;
            if (ok) want[c[1]] = v;
        }
        int diff = 0, cells = 0;
        for (int bi = 0; bi < Presets.Compare.Length; bi++)
        {
            (string name, Formation f) = Presets.Compare[bi];
            if (!want.TryGetValue(name, out double[]? exp)) continue;
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed,
                                         verbose: false, ember: rule).PlayerWon) wins++;
                cells++;
                if (Math.Abs(wins * 100.0 / Seeds - exp[w]) > 1e-9) diff++;
            }
        }
        Console.WriteLine($"{label} 対 `{Path.GetFileName(path)}`: **{cells} セル中 {diff} 件のずれ** —— "
            + (diff == 0 ? "**○**" : "**×**"));
        Console.WriteLine();
    }
}
