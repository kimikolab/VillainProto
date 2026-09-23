using BattleCore;
using static Common;

// =====================================================================================
// rebirtha2 モード（第185期） —— A群の転生 3〜5枚目（縛めのクグ・責め苦のシガ・据えのバン）
//
// 指示書は design/PHASE185_REBIRTH_A2_SPEC.md ／ 報告は design/PHASE185_REBIRTH_A2.md。
//
// **数値の線は1本も置かない**（第178期から引き継ぐ約束）。
// 回帰確認（3枚を含まない行が動かないこと）と対照の表・帳簿だけで、採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 rebirtha2 phase0  # Q0-1〜Q0-9 を実装から引き直す
//     dotnet run --project BattleSim -c Release 0 rebirtha2 run     # 1枚ずつの対照・3枚・`yP`・(G2)・勝ち方・動けない敵の分布
//     dotnet run --project BattleSim -c Release 0 rebirtha2 ledger  # 帳簿（組み付き・見せしめ・踏みしめ）
//     dotnet run --project BattleSim -c Release 0 rebirtha2 bench   # 診断台（クグ×シガ／シガ×ガン／バン×バサ）と1戦のログ
//     dotnet run --project BattleSim -c Release 0 rebirtha2 check [採用前のbalance.md]  # 自己検査
//
// 旧版の対照は「その駒だけ第184期の姿に戻したローカルの `UnitDef`」で作る（第180期と同じ作法）。
// =====================================================================================

static class RebirthA2Diag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": StageRun(); return;
            case "ledger": Ledger(); return;
            case "bench": Bench(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("rebirtha2: モードは phase0 / run / ledger / bench / check。");
                return;
        }
    }

    // =================================================================================
    // 版
    // =================================================================================

    static UnitDef Remake(UnitDef d, TraitId[] traits, IReadOnlyList<UnitAction>? actions,
                          string? plus = null, string? minus = null) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Pattern = d.Pattern, Advances = d.Advances, Actions = actions,
        Traits = traits, PlusText = plus ?? d.PlusText, MinusText = minus ?? d.MinusText, Flavor = d.Flavor
    };

    /// <summary>第184期のクグ（開戦時に大縛り・第2ターン以降は毎ターン味方1体を縛って攻+16・手番は攻3）。</summary>
    static readonly UnitDef KuguOld = Remake(UnitCatalog.Kugu, new[] { TraitId.Bind }, null,
        "開戦時に大縛りで最も速い敵1体を縛る。第2ターン以降は毎ターン味方1体を縛り、その味方の攻撃+16",
        "縛る味方は選べない。縛られた味方はそのターン動けない。第1ターンは味方の縛りが起きない");

    /// <summary>第184期のシガ（責め苦だけ）。</summary>
    static readonly UnitDef ShigaOld = Remake(UnitCatalog.Shiga, new[] { TraitId.Torment }, null,
        "動きを封じられた敵を殴ると、同じ重さの追い打ちを重ねる");

    /// <summary>第184期のバン（据え＋積み過ぎ・手番は攻5）。</summary>
    static readonly UnitDef BanOld = Remake(UnitCatalog.Ban, new[] { TraitId.Bulwark, TraitId.Overload }, null,
        "そのターン動かなかった味方の被ダメージを半減し、外から積まれた力が一定を越えているあいだは自分の一撃が薙ぎになる",
        "全員が働く編成では何も起きず、力は自分では1点も積めないうえ鈍重");

    /// <summary>踏みしめだけ・据えた足なし（`yP`）。</summary>
    static readonly UnitDef BanNoPlant = Remake(UnitCatalog.Ban, new[] { TraitId.Footing }, UnitCatalog.Ban.Actions);

    sealed record Version(string Name, bool NewKugu, bool NewShiga, bool NewBan, UnitDef? BanAs = null);

    static readonly Version V0 = new("旧（第184期）", false, false, false);
    static readonly Version VK = new("クグのみ", true, false, false);
    static readonly Version VS = new("シガのみ", false, true, false);
    static readonly Version VB = new("バンのみ", false, false, true);
    static readonly Version New = new("新（3枚）", true, true, true);
    static readonly Version YpB = new("新・据えた足なし", true, true, true, BanAs: BanNoPlant);

    static Formation Swap(Formation f, string id, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == id ? to : d;
        return g;
    }

    static Formation Apply(Formation f, Version v)
    {
        Formation g = f;
        g = Swap(g, "kugu", v.NewKugu ? UnitCatalog.Kugu : KuguOld);
        g = Swap(g, "shiga", v.NewShiga ? UnitCatalog.Shiga : ShigaOld);
        g = Swap(g, "ban", v.BanAs ?? (v.NewBan ? UnitCatalog.Ban : BanOld));
        return g;
    }

    static readonly string[] Three = { "kugu", "shiga", "ban" };
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static bool Mine(Formation f) => Three.Any(id => Has(f, id));
    static string Who(Formation f) => string.Join("・", Three.Where(id => Has(f, id)).Select(id => Short(UnitCatalog.ById(id))));

    static BattleResult Fight(Formation f, int st, int seed, Version v, bool verbose = false)
        => BattleEngine.Run(Apply(f, v), EnemyCatalog.Stages[st].Enemy, seed, verbose: verbose);

    static IEnumerable<(string Band, string Name, Formation F)> Bands()
    {
        foreach ((string n, Formation f) in CompareBuilds()) yield return ("compare", n, f);
        foreach ((string n, Formation f) in CrossBuilds()) yield return ("交差帯", n, f);
    }

    /// <summary>診断台（**`Presets` には足さない**）。</summary>
    static readonly (string Name, Formation F)[] Benches =
    {
        ("台1 クグ×シガ", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Kugu, center: UnitCatalog.Shiga,
                                          back1: UnitCatalog.Borg, back3: UnitCatalog.Dolga)),
        ("台2 シガ×ガン（クグ無し）", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Shiga, center: UnitCatalog.Gan,
                                                 back1: UnitCatalog.Borg, back3: UnitCatalog.Dolga)),
        // 台2 は叩き起こしが**のろまのドルガ**（攻38・2ターンに1回休む）を毎回選ぶので、シガが起こされない
        // （叩き起こしは休んだ味方のうち攻撃力が最大の1体）。**起こす相手がシガしかいない台**を別に置く。
        // 前列は大喰らいゴルム（後ろの列の傷を飲み込む）と分かちのドハ、シガは後列
        // （前列に置くと先に倒れて順序が回らない。棘のカドは味方を巻き込むので外した）。
        ("台2' シガ×ガン（起こす相手はシガだけ）", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Doha, center: UnitCatalog.Gan,
                                                          back1: UnitCatalog.Shiga, back3: UnitCatalog.Borg)),
        ("台3 バン×バサ（据えた足）", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Ban, center: UnitCatalog.Basa,
                                               back1: UnitCatalog.Borg, back3: UnitCatalog.Dolga)),
    };

    static string Short(UnitDef d)
    {
        int i = d.Name.LastIndexOf('の');
        return i >= 0 && i + 1 < d.Name.Length ? d.Name[(i + 1)..] : d.Name;
    }

    static string Neigh(Formation f, string id)
    {
        var me = f.Occupied().FirstOrDefault(o => o.Def.Id == id);
        if (me.Def is null) return "";
        return string.Join("・", f.Occupied().Where(o => FormationRules.AreAdjacent(me.Slot, o.Slot)).Select(o => Short(o.Def)));
    }

    static string SlotName(Formation f, string id)
    {
        var me = f.Occupied().FirstOrDefault(o => o.Def.Id == id);
        return me.Def is null ? "" : me.Slot switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => "召喚" + me.Slot };
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第185期 `rebirtha2 phase0` —— 前提を実装から引き直す");
        Console.WriteLine();
        if (!ParryScan.Init()) return;
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1. 3枚の在席行と、ガン・トウ・バサ・ヒサ・ヒビ（＋入れ替えの書き手）との同席");
        Console.WriteLine();
        string[] mates = { "gan", "tou", "basa", "hisa", "hibi", "hane", "sero", "sasa", "kado" };
        Console.WriteLine("| 行 | 帯 | クグ | シガ | バン | 席 | " + string.Join(" | ", mates.Select(m => Short(UnitCatalog.ById(m)))) + " | バンの隣 |");
        Console.WriteLine("|---|---|:-:|:-:|:-:|---|" + string.Concat(mates.Select(_ => ":-:|")) + "---|");
        int nMine = 0, nNone = 0;
        foreach ((string band, string name, Formation f) in Bands())
        {
            if (!Mine(f)) { if (band == "compare") nNone++; continue; }
            if (band == "compare") nMine++;
            string M(string id) => Has(f, id) ? "●" : "";
            string seats = string.Join(" ", Three.Where(id => Has(f, id)).Select(id => Short(UnitCatalog.ById(id)) + "=" + SlotName(f, id)));
            Console.WriteLine("| " + name + " | " + band + " | " + M("kugu") + " | " + M("shiga") + " | " + M("ban") + " | " + seats + " | "
                              + string.Join(" | ", mates.Select(M)) + " | " + Neigh(f, "ban") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- `compare` 61 行: 3枚のどれかを含む **" + nMine + " 行** ／ 含まない **" + nNone + " 行**（回帰の分母）。"
                          + "**3枚が2枚以上同席する行**: "
                          + string.Join("・", Bands().Where(b => Three.Count(id => Has(b.F, id)) >= 2).Select(b => b.Name)));
        Console.WriteLine();

        // ---- Q0-2 / Q0-6 ----
        Console.WriteLine("## Q0-2 / Q0-6. 組み付き・竦みの鍵（`Stun` を流用しない）と、その読み手");
        Console.WriteLine();
        Console.WriteLine("**どちらも専用キーにした**（`StatusKeys.Grappled` ／ `StatusKeys.Cowed`）。痺れを流用しない理由は3つ:");
        Console.WriteLine();
        Console.WriteLine("1. 痺れは**手番で消費される**（`TakeTurnCore` の頭で 0 に戻す）——組み付きは「ほどかれるまで続く」ので形が違う。");
        Console.WriteLine("2. 痺れには**読み手がいる**（責め苦・`ScapegoatTrait` の引き取り・滲み則の汚れの種類 `SoakRule.Kinds`）"
                          + "——流用するとクグ・シガの供給がそれらの帳簿にも混ざる。");
        Console.WriteLine("3. **ほどく**ときに相手の痺れを 0 にすると、トウの粉の痺れまで一緒に消える。");
        Console.WriteLine();
        foreach (string key in new[] { "StatusKeys.Grappled", "StatusKeys.Cowed" })
        {
            Console.WriteLine("`" + key + "` を読む／書く箇所（`//` の行を除く）:");
            Console.WriteLine();
            foreach ((string file, string src) in new[] { ("BattleEngine.cs", engine), ("Traits.cs", traits) })
            {
                string[] ls = src.Replace("\r", "").Split('\n');
                string cls = "";
                for (int i = 0; i < ls.Length; i++)
                {
                    string t = ls[i].TrimStart();
                    if (t.StartsWith("public sealed class ")) cls = t.Split(' ')[3];
                    if (t.StartsWith("//") || t.StartsWith("///")) continue;
                    if (ls[i].Contains(key)) Console.WriteLine("- `" + file + ":" + (i + 1) + "` " + (file == "Traits.cs" ? "`" + cls + "` " : "") + "—— `" + t + "`");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine("- 組み付かれた敵も竦んだ敵も、手番を失うときは engine が `IdleTurn` を立てる（転倒と同じ形）。"
                          + "**敵側に号令・叩き起こしの保持者は 0 体**（下の表）なので「差し出した手番」の買い手はいない。");
        Console.WriteLine("- 竦みのハメ防止の印（`ShameTrait.GuardKey`）は**敵の私有カウンタ**で、`StatusKeys.All` に入れていない"
                          + "（表示に出さない）。その駒の次の手番の頭で 0 に戻る。");
        Console.WriteLine("- 隣接判定は `FormationRules.AreAdjacent`（**敵陣でも同じ表**。席番号だけを見る）。");
        Console.WriteLine();
        Console.WriteLine("| 札 | 敵の保持者（全波） |");
        Console.WriteLine("|---|---|");
        foreach (TraitId id in new[] { TraitId.Rally, TraitId.Reveille, TraitId.Bulwark, TraitId.Grapple, TraitId.Shame, TraitId.Footing, TraitId.Planted })
        {
            var foes = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied()).Select(o => o.Def)
                .Where(d => d.Traits.Contains(id)).Select(d => d.Name).Distinct().ToList();
            Console.WriteLine("| `" + id + "` | " + (foes.Count == 0 ? "**0**" : string.Join("・", foes)) + " |");
        }
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3. 組み付きを維持する手番は「差し出した手番」か（実測）");
        Console.WriteLine();
        Console.WriteLine("クグの手番は `Actions = [Skill]` なので `TakeTurnCore` の `Skill` の枝を通り、**`IdleTurn` を立てない**。"
                          + "`TakeTurn` の外枠が数える `TurnsSurrendered`（号令・叩き起こしの判定 `Trait.SurrenderedTurn` で数える）で確かめる。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 手番 | Skill | 潰れた | うち差し出した |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach ((string band, string name, Formation f) in Bands().Concat(Benches.Select(b => (Band: "台", b.Name, b.F))))
        {
            if (!Has(f, "kugu")) continue;
            long turns = 0, skills = 0, stalls = 0, sold = 0, n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult r = Fight(f, st, seed, New);
                    n++;
                    if (!r.TallyByUnit.TryGetValue("kugu", out UnitTally? t)) continue;
                    turns += t.TurnsTaken; skills += t.TurnSkills; stalls += t.TurnStalls; sold += t.TurnsSurrendered;
                }
            Console.WriteLine("| " + name + " | " + (turns / (double)n).ToString("F2") + " | " + (skills / (double)n).ToString("F2") + " | "
                              + (stalls / (double)n).ToString("F2") + " | **" + (sold / (double)n).ToString("F2") + "** |");
        }
        Console.WriteLine();
        Console.WriteLine("- 潰れた手番（痺れ・転倒で失ったもの）は差し出した手番に入る——それは第184期までと同じ規則で、組み付きの維持とは別。");
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4. シガ×ガン（クグ無し）の順序");
        Console.WriteLine();
        Console.WriteLine("紙の順序（シガ 速3・ガン 速9）:");
        Console.WriteLine();
        Console.WriteLine("1. ターン T: シガが動ける敵を殴る → 怖気づき（`Stun = 1`）。");
        Console.WriteLine("2. ターン T+1: シガの手番で痺れを消費 → `IdleTurn = T+1`（差し出した手番。`CanAct` は偽にしていない）。");
        Console.WriteLine("3. ターン T+2 の頭: 号令（`OnTurnStart`）が `IdleTurn == Turn − 1` を見て **+8（恒久の `AtkBonus`）**。");
        Console.WriteLine("4. 同じターン T+2: ガン（速9）が殴った直後、叩き起こし（`IdleTurn == Turn − 1` かつ `CanActOutOfTurn`＝痺れは消費済みで通る）"
                          + "がシガに**1回攻撃させる**。その一撃が動ける敵に当たればまた怖気づく → シガ自身の T+2 の手番はまた失われる → 1 に戻る。");
        Console.WriteLine();
        Console.WriteLine("**号令の +8 は怖気づくたびに積もる**（1回の怖気づきにつき1回）。実測は `bench` の表と1戦のログ。");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5. 「動けない敵を優先」の置き場所（`SelectTargetChain` の中の位置を本文から引く）");
        Console.WriteLine();
        int chain = engine.IndexOf("private UnitState? SelectTargetChain(");
        var steps = new (string Key, string Label)[]
        {
            ("return SelectPierceEntry(foes, out lane);", "貫きの分岐（pool を作らない）"),
            ("FixateTrait.Remembered(attacker, pool)", "執着（ノミ）"),
            ("SeverTrait.Preferred(this, pool)", "断ち（ナタ）"),
            ("ShameTrait.Preferred(this, pool)", "**見せしめ（シガ）**"),
            ("pool[Roll(pool.Count)]", "無作為（pool）"),
            ("if (pattern != AttackPattern.Single)", "範囲はここで返る（後備えだけ）"),
            ("FinisherTrait.Preferred(this, foes)", "標の段"),
            ("RearGuardTrait.RedirectPercent", "後備え"),
            ("GuardianTrait.RedirectPercent", "庇う"),
            ("Martyr.RedirectPercent", "殉教"),
            ("ThornGuardTrait.Covers(f, target)", "棘守り"),
        };
        Console.WriteLine("| 順 | 段 | 位置 |");
        Console.WriteLine("|--:|---|--:|");
        int k = 0;
        foreach (var s in steps.Select(s => (s.Label, Pos: engine.IndexOf(s.Key, chain))).OrderBy(x => x.Pos < 0 ? int.MaxValue : x.Pos))
            Console.WriteLine("| " + (++k) + " | " + s.Label + " | " + (s.Pos < 0 ? "**見つからない**" : "+" + (s.Pos - chain)) + " |");
        Console.WriteLine();
        Console.WriteLine("- **執着・断ちと同じ段**（`pool` から無作為に選ぶ直前）。`pool` は1体も足さない・引かない＝**前列の制約の内側**。");
        Console.WriteLine("- 執着・断ちが効いている手番は見せしめを見ない（シガはどちらも持たないので実際には常に見る）。");
        Console.WriteLine("- **標の段・庇いの鎖はこの後ろ**——標持ちが盤上にいれば 75% で標へ引かれ、庇いは主目標を差し替える。"
                          + "差し替わった先が動けない敵でなければ、その一撃では見せしめは出ない（責め苦の判定は着弾した主目標で読む）。");
        Console.WriteLine("- 候補が1体なら乱数を引かない。2体以上なら `Roll` で割る（pool の無作為と同じ作法。**`PickOne` は増やしていない**）。");
        Console.WriteLine();

        // ---- Q0-7 ----
        Console.WriteLine("## Q0-7. 範囲の盾の置き場所（`PerformAttack` / `ResolvePierce`）と `ApplyDamage` の段の並び");
        Console.WriteLine();
        Console.WriteLine("範囲の一撃は `PerformAttack` が**1体ずつ別の `ApplyDamage`** に配る（主目標 → 巻き込み（`SecondaryTargets`・順番は `Shuffle`）"
                          + "／貫きは `ResolvePierce` がレーンを前から）。**盾はその配る直前で受け手を差し替える**——`ApplyDamage` の中ではない"
                          + "（中では「同じ一撃がほかに誰へ当たったか」が分からない）。");
        Console.WriteLine();
        Console.WriteLine("- 「バンが同じ攻撃に当たっている」の判定: 薙ぎ・全体は**振る前の盤面**で `主目標 == バン` か `SecondaryTargets` にバンが入るか"
                          + "（`SecondaryTargets` は乱数を引かない）。貫きはバンがそのレーンの列（`LaneOccupants`）にいるか。");
        Console.WriteLine("- 差し替えた一撃はバンへの**別の `ApplyDamage` 呼び出し**なので、**軛（25）は段ごとに独立に切る**"
                          + "（肩代わりの分割と同じ扱い・意図した帰結）。");
        Console.WriteLine("- 呪いの共有は単体の一撃にしか札を付けない（`singleHit`）ので、範囲の盾とは交わらない。");
        Console.WriteLine("- ヒビ（砕け）は**自分に来た範囲攻撃**で破片を配るので、ヒビがバンの隣にいると**その範囲はバンが受けてヒビには来ない**"
                          + "——同じ攻撃を奪い合う（指示書どおり直さない）。");
        Console.WriteLine();
        int body = engine.IndexOf("void ApplyDamageBody(");
        var stages = new (string Key, string Label)[]
        {
            ("ThornGuardTrait.AbsorbCap", "棘守りの肩代わり上限"),
            ("t.ModifyIncomingDamage", "駒の被ダメージ修正"),
            ("HavocTrait.Percent", "惨禍 +50%（入口）"),
            ("MarkRules.VulnerablePercent", "敵の標 +50%（入口）"),
            ("BulwarkTrait.ReductionPercent", "旧 据え（軽減・保持者 0 枚）"),
            ("LooseTrait.ReductionPercent", "散開（軽減）"),
            ("CowerTrait.ReductionPercent", "萎縮（軽減）"),
            ("BeckonTrait.GuardPercent", "矢面の半減（軽減）"),
            ("FootingTrait.PercentPerLayer", "**据えの層 −10%/層（軽減）**"),
            ("Colossus.Percent", "巨躯の肩代わり"),
            ("int armor = target.RawCounter(StatusKeys.Armor)", "破片"),
            ("Parry.Uses > 0", "受け流し"),
            ("bool yokeBinding = amount > Yoke.Cap", "軛の上限（HP を引く直前）"),
        };
        Console.WriteLine("| 順 | 段 | 位置 |");
        Console.WriteLine("|--:|---|--:|");
        k = 0;
        foreach (var s in stages.Select(s => (s.Label, Pos: engine.IndexOf(s.Key, body))).OrderBy(x => x.Pos < 0 ? int.MaxValue : x.Pos))
            Console.WriteLine("| " + (++k) + " | " + s.Label + " | " + (s.Pos < 0 ? "**見つからない**" : "+" + (s.Pos - body)) + " |");
        Console.WriteLine();

        // ---- Q0-8 ----
        Console.WriteLine("## Q0-8. バンを動かしうる入れ替えの経路（`SwapSlots(` の呼び出し口を全部引く）");
        Console.WriteLine();
        Console.WriteLine("| 箇所 | クラス | 行 |");
        Console.WriteLine("|---|---|---|");
        foreach ((string file, string src) in new[] { ("Traits.cs", traits), ("BattleEngine.cs", engine) })
        {
            string[] ls = src.Replace("\r", "").Split('\n');
            string cls = "";
            for (int i = 0; i < ls.Length; i++)
            {
                string t = ls[i].TrimStart();
                if (t.StartsWith("public sealed class ")) cls = t.Split(' ')[3];
                if (t.StartsWith("//")) continue;
                if (t.Contains("SwapSlots(") && !t.Contains("public void SwapSlots("))
                    Console.WriteLine("| `" + file + ":" + (i + 1) + "` | `" + (file == "Traits.cs" ? cls : "BattleContext") + "` | `" + t + "` |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("- **空振りは `SwapSlots` の入口1箇所**（動かす側・押しのけられる側のどちらかが据えた足を持てば、何も書かずに返る）。"
                          + "`OnMoved` / `OnAllyMoved`・`Move` イベント・混乱の付与・`HasFallenBack` の記録は**1つも走らない**（動いていないので）。");
        Console.WriteLine("- **呼び出し側は壊れない**——どの経路も戻り値を読まず、`SwapSlots` の後に相手の席を前提にした処理を置いていない"
                          + "（喧噪・逃げ回る・身構えの計数だけは「入れ替えた」を数えたまま残る。**帳簿の上では空振りも1回と数えられる**）。");
        Console.WriteLine("- 押しのけ先の選び方（`PickOne(同じ席の生存者)`）は空振りの前に済んでいるので、**乱数列は空振りでも変わらない**（候補は 0/1 体で `Roll` を引かない）。");
        Console.WriteLine();

        // ---- Q0-9 ----
        Console.WriteLine("## Q0-9. 過去の類似測定（`design/` を語で引く）");
        Console.WriteLine();
        string root = ParryScan.Root ?? ".";
        string[] words = { "組み付", "拘束", "竦", "範囲の肩代わり", "不動", "積み過ぎ", "据え", "縛め" };
        Console.WriteLine("| 語 | ファイル数 | 例（先頭3件） |");
        Console.WriteLine("|---|--:|---|");
        var files = Directory.GetFiles(Path.Combine(root, "design"), "*.md");
        foreach (string w in words)
        {
            var hit = files.Where(p => File.ReadAllText(p).Contains(w)).Select(Path.GetFileName).OrderBy(x => x).ToList();
            Console.WriteLine("| " + w + " | " + hit.Count + " | " + string.Join(", ", hit.Take(3)) + " |");
        }
        Console.WriteLine();
        foreach (string need in new[] { "PHASE116_LOAD.md", "PHASE127_GRADE.md", "PHASE128_GRADE2.md" })
            Console.WriteLine("- `design/" + need + "`: " + (File.Exists(Path.Combine(root, "design", need)) ? "あり（報告に要点を引く）" : "**無い**"));
        Console.WriteLine();
    }

    // =================================================================================
    // run
    // =================================================================================

    static void StageRun()
    {
        Console.WriteLine("# 第185期 `rebirtha2 run` —— 対照（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**旧** ＝ 第184期（3枚とも旧）。**クグのみ／シガのみ／バンのみ** ＝ その1枚だけ新。**新** ＝ この期の既定（3枚とも新）。"
                          + "**据えた足なし** ＝ バンから代金の札（`Planted`）だけを外した `yP`。第2〜5波の平均・seed 0..199。");
        Console.WriteLine();

        var versions = new[] { V0, VK, VS, VB, New, YpB };
        var rows = Bands().ToList();
        var res = new Dictionary<(string, string), double[]>();
        Parallel.ForEach(rows.SelectMany(r => versions.Select(v => (r, v))), x =>
        {
            if (!Mine(x.r.F) && x.v != V0 && x.v != New) return;
            double[] w = Rates(x.r.F, x.v);
            lock (res) res[(x.r.Band + "/" + x.r.Name, x.v.Name)] = w;
        });
        double A(string key, Version v) => Mean25(res[(key, v.Name)]);

        Console.WriteLine("## 表A. 3枚のどれかを含む行（第2〜5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 駒 | 旧 | クグのみ | シガのみ | バンのみ | **新** | 新−旧 | 据えた足の代金（新−足なし） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in rows)
        {
            if (!Mine(r.F)) continue;
            string key = r.Band + "/" + r.Name;
            string Or(string id, Version v) => Has(r.F, id) ? A(key, v).ToString("F1") : "—";
            Console.WriteLine("| " + r.Name + " | " + r.Band + " | " + Who(r.F) + " | " + A(key, V0).ToString("F1") + " | "
                              + Or("kugu", VK) + " | " + Or("shiga", VS) + " | " + Or("ban", VB) + " | **"
                              + A(key, New).ToString("F1") + "** | **" + D(A(key, New) - A(key, V0)) + "** | "
                              + (Has(r.F, "ban") ? D(A(key, New) - A(key, YpB)) : "—") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表A'. 波別（旧 → 新）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 旧 第2〜5波 | 新 第2〜5波 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var r in rows)
        {
            if (!Mine(r.F)) continue;
            string key = r.Band + "/" + r.Name;
            Console.WriteLine("| " + r.Name + " | " + r.Band + " | " + Cells(res[(key, V0.Name)]) + " | " + Cells(res[(key, New.Name)]) + " |");
        }
        Console.WriteLine();

        // ---- 回帰 ----
        int still = 0, stillRows = 0;
        foreach (var r in rows)
        {
            if (Mine(r.F)) continue;
            stillRows++;
            string key = r.Band + "/" + r.Name;
            double[] a = res[(key, V0.Name)], b = res[(key, New.Name)];
            if (Enumerable.Range(0, 5).Any(i => Math.Abs(a[i] - b[i]) > 0.001)) still++;
        }
        Console.WriteLine("- **3枚を含まない " + stillRows + " 行（compare ＋ 交差帯）で旧と新が違う行: " + still + " 行**（0 が正）");
        Console.WriteLine();

        // ---- (G2) ----
        Console.WriteLine("## 表B. (G2) の「壊れ／制約」の分解（`compare` 61 行の分母・**報告のみ**）");
        Console.WriteLine();
        var builds = CompareBuilds().ToList();
        double[] O(string n) => res[("compare/" + n, V0.Name)];
        double[] N(string n) => res[("compare/" + n, New.Name)];
        var dropped = new List<(string Name, int Wave, double Delta)>();
        foreach ((string name, Formation _) in builds)
            for (int w = 1; w < 5; w++)
                if (N(name)[w] - O(name)[w] <= -10.0) dropped.Add((name, w, N(name)[w] - O(name)[w]));
        if (dropped.Count == 0) Console.WriteLine("**−10.0pt 以上落ちたセルは 0 件。**");
        else
        {
            Console.WriteLine("| 行 | 波 | Δ |");
            Console.WriteLine("|---|--:|--:|");
            foreach (var d in dropped) Console.WriteLine("| " + d.Name + " | 第" + (d.Wave + 1) + "波 | " + D(d.Delta) + " |");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 他の行の数 | 平均変化 | 判定 |");
            Console.WriteLine("|---|--:|--:|---|");
            var units = new SortedSet<string>();
            foreach (var d in dropped.Select(x => x.Name).Distinct())
                foreach ((int _, UnitDef u) in builds.First(b => b.Name == d).F.Occupied()) units.Add(u.Id);
            foreach (string id in units)
            {
                var others = builds.Where(b => Has(b.F, id) && !dropped.Any(x => x.Name == b.Name)).ToList();
                if (others.Count == 0) { Console.WriteLine("| " + UnitCatalog.ById(id).Name + " | 0 | — | 分解が成立しない |"); continue; }
                double avg = others.Average(b => Mean25(N(b.Name)) - Mean25(O(b.Name)));
                string verdict = avg <= -3.0 ? "**壊れ**" : Math.Abs(avg) < 3.0 ? "制約" : "（上振れ）";
                Console.WriteLine("| " + UnitCatalog.ById(id).Name + " | " + others.Count + " | " + D(avg) + " | " + verdict + " |");
            }
        }
        Console.WriteLine();

        // ---- 勝ち方 ----
        Console.WriteLine("## 表C. 勝ち方（第2〜5波・勝った試行だけ）");
        Console.WriteLine();
        Console.WriteLine("`残存` ＝ 勝った試行の生存数の平均 ／ `全滅勝ち` ＝ 勝った試行のうち生存1体の割合（`docs/chain.md` と同じ定義）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 駒 | 残存 旧 | 残存 新 | 全滅勝ち 旧 | 全滅勝ち 新 | 決着T 旧 | 決着T 新 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var r in rows)
        {
            if (!Mine(r.F)) continue;
            var a = Quality(r.F, V0); var b = Quality(r.F, New);
            Console.WriteLine("| " + r.Name + " | " + r.Band + " | " + Who(r.F) + " | " + a.Surv.ToString("F2") + " | " + b.Surv.ToString("F2") + " | "
                              + a.Edge.ToString("F1") + "% | " + b.Edge.ToString("F1") + "% | " + a.T.ToString("F2") + " | " + b.T.ToString("F2") + " |");
        }
        Console.WriteLine();

        // ---- 同時に動けない敵 ----
        Console.WriteLine("## 表D. 1ターンに手番を失った敵の数の分布（第2〜5波・全ターン）");
        Console.WriteLine();
        Console.WriteLine("「手番を失った」＝そのターンに engine が `IdleTurn` を立てた敵（組み付き・竦み・大縛り・痺れ・転倒・`CanAct` 偽のすべて）。"
                          + "**4対2 に持ち込めているか**は `2` と `3+` の列で読む。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 駒 | 旧 0 / 1 / 2 / 3+ | 新 0 / 1 / 2 / 3+ | 2体以上 旧 → 新 |");
        Console.WriteLine("|---|---|---|---|---|--:|");
        foreach (var r in rows.Concat(Benches.Select(b => (Band: "台", b.Name, b.F))))
        {
            if (!Mine(r.F)) continue;
            double[] ho = Hist(r.F, V0), hn = Hist(r.F, New);
            Console.WriteLine("| " + r.Name + " | " + r.Band + " | " + Who(r.F) + " | " + HistCells(ho) + " | " + HistCells(hn) + " | "
                              + (ho[2] + ho[3]).ToString("F1") + "% → **" + (hn[2] + hn[3]).ToString("F1") + "%** |");
        }
        Console.WriteLine();

        // ---- 指標 ----
        Console.WriteLine("## 表E. 全体の指標（新）");
        Console.WriteLine();
        double[] all = new double[5], prim = new double[5];
        foreach ((string name, Formation _) in builds)
            for (int w = 0; w < 5; w++) all[w] += N(name)[w] / builds.Count;
        int pc = 0;
        foreach (string p in Baseline.PrimaryRows)
            if (builds.Any(b => b.Name == p)) { pc++; for (int w = 0; w < 5; w++) prim[w] += N(p)[w]; }
        for (int w = 0; w < 5; w++) prim[w] /= Math.Max(1, pc);
        Console.WriteLine("- 全" + builds.Count + "行: " + string.Join(" / ", all.Select(x => x.ToString("F1"))));
        Console.WriteLine("- 主判定" + pc + "行: " + string.Join(" / ", prim.Select(x => x.ToString("F1")))
                          + "（歯止め 33.2% との余裕 " + D(prim[4] - 33.2) + "pt）");
        int infoOld = 0, infoNew = 0, hiOld = 0, hiNew = 0;
        foreach ((string name, Formation _) in builds)
        {
            for (int w = 1; w < 5; w++) { if (O(name)[w] > 0 && O(name)[w] < 100) infoOld++; if (N(name)[w] > 0 && N(name)[w] < 100) infoNew++; }
            if (O(name)[4] > 95) hiOld++; if (N(name)[4] > 95) hiNew++;
        }
        Console.WriteLine("- 情報セル（`0 < x < 100`・第2〜5波）: " + infoOld + " → **" + infoNew + "** ／ 第五波 95% 超: " + hiOld + " → **" + hiNew + "**");
        Console.WriteLine();
    }

    static double[] Hist(Formation f, Version v)
    {
        var h = new double[4];
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = Fight(f, st, seed, v);
                for (int i = 0; i < 4; i++) h[i] += r.FoeStalledHist[i];
            }
        double sum = h.Sum();
        for (int i = 0; i < 4; i++) h[i] = sum == 0 ? 0 : 100.0 * h[i] / sum;
        return h;
    }

    static string HistCells(double[] h) => string.Join(" / ", h.Select(x => x.ToString("F1")));

    static (double Surv, double Edge, double T) Quality(Formation f, Version v)
    {
        long wins = 0, surv = 0, edge = 0, turns = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = Fight(f, st, seed, v);
                if (!r.PlayerWon) continue;
                wins++; surv += r.PlayerSurvivors; turns += r.Turns;
                if (r.PlayerSurvivors == 1) edge++;
            }
        return wins == 0 ? (0, 0, 0) : ((double)surv / wins, 100.0 * edge / wins, (double)turns / wins);
    }

    // =================================================================================
    // ledger
    // =================================================================================

    static void Ledger()
    {
        Console.WriteLine("# 第185期 `rebirtha2 ledger` —— 帳簿（1戦あたり・第2〜5波・seed 0..199・新）");
        Console.WriteLine();
        var rows = Bands().Concat(Benches.Select(b => (Band: "台", b.Name, b.F))).Where(r => Mine(r.F)).ToList();

        Console.WriteLine("## 表F. クグ（組み付き）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 組み付いた | 維持した手番 | 止めた敵の手番 | ほどけた | 大縛り含む敵の手番喪失 | 組み付いた相手（上位3・回/戦） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|");
        foreach (var r in rows)
        {
            if (!Has(r.F, "kugu")) continue;
            double fires = 0, holds = 0, stalled = 0, breaks = 0, lost = 0; int n = 0;
            var who = new Dictionary<string, double>();
            var ids = r.F.Occupied().Select(o => o.Def.Id).ToHashSet();
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult b = Fight(r.F, st, seed, New);
                    n++;
                    if (b.TallyByUnit.TryGetValue("kugu", out UnitTally? t))
                    { fires += t.GrappleFires; holds += t.GrappleHolds; stalled += t.GrappleStalled; breaks += t.GrappleBreaks; }
                    foreach (var kv in b.TallyByUnit)
                    {
                        if (ids.Contains(kv.Key)) continue;
                        lost += kv.Value.StallStun + kv.Value.StallGrappled;
                        if (kv.Value.GrappledTimes > 0)
                        {
                            string nm = NameOf(kv.Key);
                            who[nm] = who.GetValueOrDefault(nm) + kv.Value.GrappledTimes;
                        }
                    }
                }
            Console.WriteLine("| " + r.Name + " | " + r.Band + " | " + (fires / n).ToString("F2") + " | " + (holds / n).ToString("F2") + " | "
                              + (stalled / n).ToString("F2") + " | " + (breaks / n).ToString("F2") + " | " + (lost / n).ToString("F2") + " | "
                              + string.Join("・", who.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key + " " + (kv.Value / n).ToString("F2"))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表G. シガ（責め苦＋見せしめ）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 振った | 優先して狙った | 見せしめ | 竦ませた（延べ） | 耐性で弾いた | 怖気づいた | 叩き起こされた | 号令を受けた（攻） | 竦みで敵が失った手番 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in rows)
        {
            if (!Has(r.F, "shiga")) continue;
            double swings = 0, picks = 0, fires = 0, cowed = 0, blocked = 0, scared = 0, woken = 0, whet = 0, foeLost = 0; int n = 0;
            var ids = r.F.Occupied().Select(o => o.Def.Id).ToHashSet();
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult b = Fight(r.F, st, seed, New);
                    n++;
                    if (b.TallyByUnit.TryGetValue("shiga", out UnitTally? t))
                    {
                        swings += t.Attacks; picks += t.ShamePicks; fires += t.ShameFires; cowed += t.ShameCowed;
                        blocked += t.ShameBlocked; scared += t.StallStun; woken += t.ReveilleWoken; whet += t.Whetted;
                    }
                    foreach (var kv in b.TallyByUnit) if (!ids.Contains(kv.Key)) foeLost += kv.Value.StallCowed;
                }
            Console.WriteLine("| " + r.Name + " | " + r.Band + " | " + (swings / n).ToString("F2") + " | " + (picks / n).ToString("F2") + " | "
                              + (fires / n).ToString("F2") + " | " + (cowed / n).ToString("F2") + " | " + (blocked / n).ToString("F2") + " | "
                              + (scared / n).ToString("F2") + " | " + (woken / n).ToString("F2") + " | " + (whet / n).ToString("F1") + " | "
                              + (foeLost / n).ToString("F2") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- `怖気づいた` はシガが痺れで手番を失った回数（責め苦の自傷の結果。トウの粉などでも増える）。"
                          + "`号令を受けた` はシガの `Whetted`（号令の鬨 +4 と、差し出した手番への +8 の合計）。");
        Console.WriteLine();

        Console.WriteLine("## 表H. バン（踏みしめ＋範囲の盾＋据えた足）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 席 | 層の平均 | 層で防いだ | 盾で受けた回数 | 盾で受けた量（半分の後） | 半分で消えた量 | 入れ替えの空振り（うち敵） | 殴られて積んだ層 | 3層に届いた率 | 届いたT（平均） | 生存T | 倒れた率 | 旧の倒れた率 | 旧の被弾 → 新の被弾 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var r in rows)
        {
            if (!Has(r.F, "ban")) continue;
            double layerSum = 0, layerTurns = 0, saved = 0, takes = 0, taken = 0, halved = 0, refused = 0, refusedFoe = 0, hitSteps = 0, full = 0, fullT = 0, lifeT = 0, dead = 0, deadOld = 0, hitOld = 0, hitNew = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult b = Fight(r.F, st, seed, New);
                    n++;
                    if (b.TallyByUnit.TryGetValue("ban", out UnitTally? t))
                    {
                        layerSum += t.FootingLayerSum; layerTurns += t.FootingLayerTurns; saved += t.FootingSaved;
                        takes += t.ShieldTakes; taken += t.ShieldTaken; halved += t.ShieldHalved; refused += t.PlantedRefused; refusedFoe += t.PlantedRefusedFoe; hitSteps += t.FootingHitSteps;
                        if (t.FootingFullAt > 0) { full++; fullT += t.FootingFullAt; }
                        lifeT += t.FootingLayerTurns; hitNew += t.DamageTaken;
                    }
                    if (b.PlayerStarterFallen.Contains("ban")) dead++;
                    BattleResult o = Fight(r.F, st, seed, V0);
                    if (o.PlayerStarterFallen.Contains("ban")) deadOld++;
                    if (o.TallyByUnit.TryGetValue("ban", out UnitTally? to)) hitOld += to.DamageTaken;
                }
            Console.WriteLine("| " + r.Name + " | " + r.Band + " | " + SlotName(r.F, "ban") + " | " + (layerTurns == 0 ? 0 : layerSum / layerTurns).ToString("F2") + " | "
                              + (saved / n).ToString("F1") + " | " + (takes / n).ToString("F2") + " | " + (taken / n).ToString("F1") + " | "
                              + (halved / n).ToString("F1") + " | " + (refused / n).ToString("F2") + "（" + (refusedFoe / n).ToString("F2") + "） | "
                              + (hitSteps / n).ToString("F2") + " | " + (100.0 * full / n).ToString("F1") + "% | " + (full == 0 ? "—" : (fullT / full).ToString("F2")) + " | " + (lifeT / n).ToString("F2") + " | " + (100.0 * dead / n).ToString("F1") + "% | "
                              + (100.0 * deadOld / n).ToString("F1") + "% | " + (hitOld / n).ToString("F1") + " → " + (hitNew / n).ToString("F1") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- `層の平均` はバンが生きていたターン末の層の平均（最大 3）。`生存T` はバンが生きてターン末を迎えた数。");
        Console.WriteLine("- `盾で受けた量` は差し替えた一撃を**半分にした後・層の軽減の前**の量（層・軛はバンの `ApplyDamage` の中で掛かる）。");
        Console.WriteLine();

        // ---- 置き場所（中央／端）: 同じ5枚で、バンの席だけを中央の駒と入れ替える ----
        Console.WriteLine("## 表H'. 置き場所別のバンの倒れる率（同じ5枚で、バンと中央の駒の席だけを入れ替える・新）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 元の席 | 端のとき 倒れた率 / 盾の回数 / 勝率 | 中央のとき 倒れた率 / 盾の回数 / 勝率 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var r in rows)
        {
            if (!Has(r.F, "ban")) continue;
            var ban = r.F.Occupied().First(o => o.Def.Id == "ban");
            Formation edge, center;
            if (ban.Slot == 2)
            {
                // 中央にいる行は、前3（無ければ前1）の駒と入れ替えて端の版を作る
                int to = r.F.Occupied().Any(o => o.Slot == 1) ? 1 : 0;
                center = r.F; edge = SwapSeats(r.F, 2, to);
            }
            else { edge = r.F; center = SwapSeats(r.F, ban.Slot, 2); }
            string Cell(Formation f)
            {
                double dead = 0, takes = 0, wins = 0; int n = 0;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult b = Fight(f, st, seed, New);
                        n++;
                        if (b.PlayerWon) wins++;
                        if (b.PlayerStarterFallen.Contains("ban")) dead++;
                        if (b.TallyByUnit.TryGetValue("ban", out UnitTally? t)) takes += t.ShieldTakes;
                    }
                return (100.0 * dead / n).ToString("F1") + "% / " + (takes / n).ToString("F2") + " / " + (100.0 * wins / n).ToString("F1") + "%";
            }
            Console.WriteLine("| " + r.Name + " | " + SlotName(r.F, "ban") + " | " + Cell(edge) + " | " + Cell(center) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 勝率は第2〜5波の平均。**席を入れ替えた版は `Presets` に無い**（この表のためだけの配置）。");
        Console.WriteLine();
    }

    static Formation SwapSeats(Formation f, int a, int b)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot == a ? b : slot == b ? a : slot] = d;
        return g;
    }

    static readonly Dictionary<string, string> Names = UnitCatalog.Everyone
        .Concat(EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied()).Select(o => o.Def))
        .GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First().Name);

    static string NameOf(string id) => Names.TryGetValue(id, out string? n) ? n : id;

    // =================================================================================
    // bench
    // =================================================================================

    static void Bench()
    {
        Console.WriteLine("# 第185期 `rebirtha2 bench` —— 診断台（**`Presets` には足さない**）");
        Console.WriteLine();
        foreach ((string name, Formation f) in Benches)
            Console.WriteLine("- **" + name + "**: " + string.Join(" / ", f.Occupied().Select(o => SlotNameOf(o.Slot) + " " + Short(o.Def))));
        Console.WriteLine();

        Console.WriteLine("## 表I. 勝率（第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 旧 | クグのみ | シガのみ | バンのみ | **新** | 新 第2〜5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|");
        foreach ((string name, Formation f) in Benches)
        {
            double[] n = Rates(f, New);
            string Or(string id, Version v) => Has(f, id) ? Mean25(Rates(f, v)).ToString("F1") : "—";
            Console.WriteLine("| " + name + " | " + Mean25(Rates(f, V0)).ToString("F1") + " | " + Or("kugu", VK) + " | " + Or("shiga", VS) + " | "
                              + Or("ban", VB) + " | **" + Mean25(n).ToString("F1") + "** | " + Cells(n) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表J. シガ×ガン（クグ無し）: シガはアタッカーとして働いたか（新・第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | シガの振り | うち叩き起こし | 怖気づいた | 与ダメ | 受けた強化 | 見せしめ | 竦ませた | 生存T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach ((string bname, Formation b2) in new[] { Benches[1], Benches[2] })
        foreach (Version v in new[] { V0, VS })
        {
            double sw = 0, wk = 0, sc = 0, dmg = 0, wh = 0, sh = 0, cw = 0, life = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = Fight(b2, st, seed, v);
                    n++;
                    if (!r.TallyByUnit.TryGetValue("shiga", out UnitTally? t)) continue;
                    sw += t.Attacks; wk += t.ReveilleWoken; sc += t.StallStun; dmg += t.DamageToEnemy; wh += t.Whetted;
                    sh += t.ShameFires; cw += t.ShameCowed; life += t.TurnsTaken;
                }
            Console.WriteLine("| " + bname + " | " + v.Name + " | " + (sw / n).ToString("F2") + " | " + (wk / n).ToString("F2") + " | " + (sc / n).ToString("F2") + " | "
                              + (dmg / n).ToString("F1") + " | " + (wh / n).ToString("F1") + " | " + (sh / n).ToString("F2") + " | " + (cw / n).ToString("F2") + " | "
                              + (life / n).ToString("F2") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- `受けた強化` はシガの `Whetted`（号令の鬨 +4 と、差し出した手番への +8）。`生存T` はシガが手番を迎えた回数（潰れた手番を含む）。");
        Console.WriteLine();

        foreach ((string name, Formation f, int st, int seed) in new[] { (Benches[0].Name, Benches[0].F, 2, 0), (Benches[2].Name, Benches[2].F, 2, 0), (Benches[3].Name, Benches[3].F, 2, 0) })
        {
            Console.WriteLine("## 1戦のログ: " + name + "（第" + (st + 1) + "波・seed " + seed + "・新・ターン3まで）");
            Console.WriteLine();
            Console.WriteLine("```");
            BattleResult r = Fight(f, st, seed, New, verbose: true);
            int turn = 0;
            foreach (LogLine l in r.Log)
            {
                if (l.Kind == LogKind.Turn) { turn++; if (turn > 3) break; }
                Console.WriteLine(l.Text);
            }
            Console.WriteLine("```");
            Console.WriteLine();
        }
    }

    static string SlotNameOf(int s) => s switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => "召喚" + s };

    // =================================================================================
    // check
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第185期 `rebirtha2 check` —— 自己検査");
        Console.WriteLine();
        string path = string.IsNullOrWhiteSpace(arg) ? "docs/balance.md" : arg.Trim();

        // (a) 旧は採用前の balance.md と一致する
        if (File.Exists(path))
        {
            var want = ReadBalance(path);
            int cells = 0, miss = 0, rowsSeen = 0;
            foreach ((string name, Formation f) in CompareBuilds())
            {
                if (!want.TryGetValue(name, out double[]? w)) { miss++; continue; }
                rowsSeen++;
                double[] a = Rates(f, V0);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - w[i]) > 0.001) cells++;
            }
            Console.WriteLine("- (a) 旧（第184期のクグ・シガ・バン）の " + rowsSeen + " 行 × 5波 と `" + path + "` の差: **" + cells + " セル**（0 が正）"
                              + (miss > 0 ? " ／ 行名が引けなかった行 " + miss : ""));
        }
        else Console.WriteLine("- (a) `" + path + "` が無いので飛ばした");

        // (b) 3枚を含まない行は旧と新で1セルも動かない
        {
            int cells = 0, rows = 0;
            foreach ((string _, string _, Formation f) in Bands())
            {
                if (Mine(f)) continue;
                rows++;
                double[] a = Rates(f, V0), b = Rates(f, New);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - b[i]) > 0.001) cells++;
            }
            Console.WriteLine("- (b) 3枚を含まない " + rows + " 行（compare ＋ 交差帯）で動いたセル: **" + cells + " / " + rows * 5 + "**（0 が正）");
        }

        // (c) 帳簿が閉じる ／ クグは差し出さない ／ 敵に据えの層・味方に組み付き・竦みは立たない
        {
            long holderStalled = 0, foeStalled = 0, kuguSold = 0, allyGrappled = 0, allyCowed = 0, foeFooting = 0, battles = 0;
            long cowedGiven = 0, cowedUsed = 0, refused = 0;
            foreach ((string _, string _, Formation f) in Bands().Concat(Benches.Select(b => (Band: "台", b.Name, b.F))))
            {
                if (!Mine(f)) continue;
                var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = Fight(f, st, seed, New);
                        battles++;
                        foreach (var kv in r.TallyByUnit)
                        {
                            UnitTally t = kv.Value;
                            if (ids.Contains(kv.Key))
                            {
                                holderStalled += t.GrappleStalled;
                                allyGrappled += t.StallGrappled;
                                allyCowed += t.StallCowed + t.CowedLost;
                                cowedGiven += t.ShameCowed;
                                refused += t.PlantedRefused;
                                if (kv.Key == "kugu") kuguSold += t.TurnsSurrendered - t.StallStun - t.StallStagger;
                            }
                            else
                            {
                                foeStalled += t.StallGrappled;
                                cowedUsed += t.CowedLost;
                                foeFooting += t.FootingSteps;
                            }
                        }
                    }
            }
            Console.WriteLine("- (c) " + battles + " 戦: 組み付きで止めた手番 保持者の側 " + holderStalled + " ／ 敵の側 " + foeStalled
                              + "（**一致が正**）／ 竦ませた延べ " + cowedGiven + " ≧ 敵が消費した竦み " + cowedUsed
                              + "（差は戦闘の終わりに残った竦みと、倒れた敵）");
            Console.WriteLine("- (d) クグの「差し出した手番」のうち痺れ・転倒以外（＝組み付きの維持が数えられた分）: **" + kuguSold + "**（0 が正）"
                              + " ／ 味方が組み付かれた・竦んだ: **" + (allyGrappled + allyCowed) + "** ／ 敵が層を積んだ: **" + foeFooting + "**（どれも 0 が正）"
                              + " ／ 入れ替えの空振り（台3 を含む）: " + refused);
        }

        // (e) PickOne を新しく使っていない
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
            string pick = "Pick" + "One(";
            int nt = (traits.Length - traits.Replace(pick, "").Length) / pick.Length;
            int ne = (engine.Length - engine.Replace(pick, "").Length) / pick.Length;
            int mine = 0;
            foreach (string cls in new[] { "Grapple" + "Trait", "Shame" + "Trait", "Footing" + "Trait", "Planted" + "Trait" })
                mine += CountIn(traits, cls, pick);
            Console.WriteLine("- (e) 第185期の4枚が `" + pick + "` を呼ぶ回数: **" + mine + "**（0 が正）／ `Traits.cs` 全体 " + nt
                              + " 件・`BattleEngine.cs` 全体 " + ne + " 件（第184期は 9 / 18）");
        }

        // (f) 旧札の保持者
        Console.WriteLine("- (f) 旧 `Bind` / `Bulwark` / `Overload` の保持者（`All`）: **"
                          + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Bind)) + " / "
                          + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Bulwark)) + " / "
                          + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Overload)) + "**（どれも 0 が正）");
        Console.WriteLine();
    }

    static int CountIn(string src, string cls, string needle)
    {
        int i = src.IndexOf("class " + cls + " ");
        if (i < 0) return 0;
        int j = src.IndexOf("\npublic ", i + 10);
        string body = src.Substring(i, (j < 0 ? src.Length : j) - i);
        return (body.Length - body.Replace(needle, "").Length) / needle.Length;
    }

    static Dictionary<string, double[]> ReadBalance(string path)
    {
        var d = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            string[] c = line.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            if (c.Length < 6) continue;
            var w = new double[5];
            bool ok = true;
            for (int i = 0; i < 5; i++)
                ok &= double.TryParse(c[1 + i].TrimEnd('%'), System.Globalization.NumberStyles.Float,
                                      System.Globalization.CultureInfo.InvariantCulture, out w[i]);
            if (ok) d[c[0]] = w;
        }
        return d;
    }

    // =================================================================================
    // ヘルパ
    // =================================================================================

    static double[] Rates(Formation f, Version v, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (Fight(f, st, seed, v).PlayerWon) wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells(double[] w) => string.Join(" / ", Enumerable.Range(1, 4).Select(i => w[i].ToString("F1")));
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");
}
