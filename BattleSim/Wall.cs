using System.Reflection;
using System.Text.RegularExpressions;
using BattleCore;

// =====================================================================================
// wall モード（第136期） —— ガルドを壁にする（確実な庇い・受け流し・中継）
//
// 指示書は design/PHASE136_WALL_SPEC.md。**Phase 0 はすべて実装から引く**（走査は件数を出し、
// 0 件を異常として止める・第117期）。**走査の検索文字列は連結で組む**——この診断自身が
// リポジトリ内のファイルなので、素直に書くと自分のコードに当たる（第123期）。
//
//     dotnet run --project BattleSim -c Release 0 wall phase0            # Q0-1〜Q0-10
//     dotnet run --project BattleSim -c Release 0 wall n                 # 段2 の N の決め方（§5-2）
//     dotnet run --project BattleSim -c Release 0 wall run [段1のbalance.md]   # 段2/段3 の版 × 群A/B/C
//     dotnet run --project BattleSim -c Release 0 wall check [段1のbalance.md] # 自己検査
// =====================================================================================

static class WallDiag
{
    public static void Run(string mode, string arg)
    {
        if (!ParryScan.Init()) return;
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "n": DeriveN(); return;
            case "run": Stage2(arg); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("wall: モードは phase0 / n / run / check（第136期）。");
                return;
        }
    }

    static int Count(string hay, params string[] needles) => ParryDiag.Count(hay, needles);

    /// <summary>本体の先頭からの行数（走査の目印の位置を出すため）。</summary>
    static int LineOf(string text, int at) => text.Take(Math.Max(0, at)).Count(c => c == '\n');

    static string Between(string text, string from, string to)
    {
        int a = text.IndexOf(from, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
    }

    // =================================================================================
    // 群A / 群B / 群C（§1-4）。**行の中身から機械で引く**（駒名ではなく札で数える）。
    // =================================================================================

    /// <summary>B = ガンのみ（指示書の定義）／ Bb = **バンのみ**（指示書に無い群。据えは `Stoic` を通らず本人に乗るので分けて出す）。</summary>
    internal enum Group { None, A, B, Bb, C }

    internal static Group GroupOf(Formation f)
    {
        var defs = f.Occupied().Select(o => o.Def).ToList();
        if (!defs.Any(d => d.Id == "gald")) return Group.None;
        bool rally = defs.Any(d => d.Traits.Contains(TraitId.Rally));
        bool bulwark = defs.Any(d => d.Traits.Contains(TraitId.Bulwark));
        if (rally && bulwark) return Group.C;
        if (rally) return Group.B;
        if (bulwark) return Group.Bb;
        return Group.A;
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第136期 Phase 0 —— 走査と数え物（実装から引く）");
        Console.WriteLine();

        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
        string prog = ParryScan.Read(Path.Combine("BattleSim", "Program.cs"));

        // ---- Q0-0 ------------------------------------------------------------------
        Console.WriteLine("## Q0-0 —— `git fetch` と behind の確認");
        Console.WriteLine();
        Console.WriteLine("この診断は git を叩かない。**結果は報告書に手で書く**（`git rev-list --left-right --count master...origin/master`）。");
        Console.WriteLine();

        // ---- 現状 ------------------------------------------------------------------
        UnitDef g = UnitCatalog.Gald;
        Console.WriteLine("## ガルドの現状（`UnitCatalog.Gald`）");
        Console.WriteLine();
        Console.WriteLine($"- HP {g.MaxHp} / 攻 {g.Attack} / 速 {g.Speed} / 型 {g.Pattern} / 踏込 {(g.Advances ? "○" : "×")}");
        Console.WriteLine($"- 札: {string.Join(" / ", g.Traits.Select(t => "`" + t + "`"))}");
        Console.WriteLine($"- `Actions`: {(g.Actions is null ? "**null**（通常攻撃のみ）" : string.Join(" → ", g.Actions.Select(a => a.Kind.ToString())))}");
        Console.WriteLine($"- `GuardianTrait.RedirectPercent` = **{GuardianTrait.RedirectPercent}** ／ `MartyrTrait.DefaultPercent` = **{MartyrTrait.DefaultPercent}**（`MartyrRule.Default` = `{MartyrRule.Default}`）");
        Console.WriteLine($"- `ParryRule.Default` = `{ParryRule.Default}` ／ `GatherRule.Default` = `{GatherRule.Default}`");
        Console.WriteLine();

        // ---- Q0-1 ------------------------------------------------------------------
        Console.WriteLine("## Q0-1 —— 「構える」をどう書けば `IdleTurn` が立つか（3通り）");
        Console.WriteLine();
        string core = Between(engine, "private TurnOutcome TakeTurnCore(", "private void HandleDeath(");
        if (core.Length == 0) { Console.WriteLine("**`TakeTurnCore` を引けなかった。止める**（第117期）。"); return; }
        string idleSet = "SetCounter(StatusKeys" + ".IdleTurn";
        string chargeBlock = Between(core, "if (act.Kind == ActionKind" + ".Charge)", "if (act.Kind == ActionKind" + ".Skill)");
        string skillBlock = Between(core, "if (act.Kind == ActionKind" + ".Skill)", "PerformAttack(actor, attackPercent");
        string vetoBlock = Between(core, "bool canAct = vetoed is null", "if (act is null)");
        int stunIdle = Count(Between(core, "if (actor.RawCounter(StatusKeys" + ".Stun) > 0)", "Colossus.Slumber"), idleSet);
        int slumberIdle = Count(Between(core, "Colossus.Slumber", "bool canAct = vetoed is null"), idleSet);
        Console.WriteLine($"`TakeTurnCore` の中で `IdleTurn` を立てる箇所: **{Count(core, idleSet)} 箇所**"
            + $"（痺れ {stunIdle} ／ まどろみ {slumberIdle} ／ `CanAct` 偽 {Count(vetoBlock, idleSet)}）。");
        Console.WriteLine();
        Console.WriteLine("| 形 | `IdleTurn` が立つか | 特性側のフック | engine を触らずに書けるか |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine($"| (1) `ActionKind.Charge` | **立たない**（ブロック内 {Count(chargeBlock, idleSet)} 箇所。コメントに理由: 据え・号令が溜めを無償の毎ターン収入として拾う） | **無し**（`OnAction` の呼び出し {Count(chargeBlock, "OnAction(")} 件）→ 補充の口が無い | 補充も中継も書けない |");
        Console.WriteLine($"| (2) `ActionKind.Skill` | **立たない**（ブロック内 {Count(skillBlock, idleSet)} 箇所。「振ってはいないが手番は使っている」） | `Trait.OnAction`（{Count(skillBlock, "OnAction(")} 件）→ 補充は書ける | 段2 は書けるが**段3（中継）は書けない** |");
        Console.WriteLine($"| (3) `CanAct` 偽 | **立つ**（ブロック内 {Count(vetoBlock, idleSet)} 箇所。売れるかは `Trait.SurrendersTurn`） | **無し**（`OnAction` {Count(vetoBlock, "OnAction(")} 件・周期も進まない）→ 補充は `OnTurnStart`（行動順ループの外）で書く | 段2・段3 とも書ける |");
        Console.WriteLine();
        Console.WriteLine("**採る形は (3)。** 段2 と段3 で形を変えないため（段3 の差分を `SurrendersTurn` の1ビットに閉じる）。");
        Console.WriteLine("補充の口は `OnTurnStart`——ターンの順序は `TickStatuses` → `OnTurnStart` → 行動順ループ（第58期）なので、");
        Console.WriteLine("**その手番（速4・敵はほぼ全員より遅い）で満タンにするのと、次のターン頭で満タンにするのは、");
        Console.WriteLine("ガルドより遅い敵がいない限り同じ供給**である（敵の速さは §Q0-1' に出す）。");
        Console.WriteLine("`CanAct(Attack)` を偽にする＝**ガルドは自分の手番で攻撃しなくなる**（不動のカドと同じ形。指示書 §4-3 の「ポンに確認」）。");
        Console.WriteLine();
        Console.WriteLine("### Q0-1' —— 敵の速さの分布（ガルドの速 4 に対して）");
        Console.WriteLine();
        var spd = new SortedDictionary<int, int>();
        int slower = 0, total = 0;
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
            foreach (var o in EnemyCatalog.Stages[w].Enemy.Occupied())
            {
                total++;
                spd[o.Def.Speed] = spd.GetValueOrDefault(o.Def.Speed) + 1;
                if (o.Def.Speed < g.Speed) slower++;
            }
        Console.WriteLine("| 速 | " + string.Join(" | ", spd.Keys) + " |");
        Console.WriteLine("|---|" + string.Concat(spd.Select(_ => "---:|")));
        Console.WriteLine("| 体（5波の延べ） | " + string.Join(" | ", spd.Values) + " |");
        Console.WriteLine();
        Console.WriteLine($"5波の延べ {total} 体のうち**ガルドより遅い（速 < {g.Speed}）敵は {slower} 体**。"
            + (slower == 0 ? "**0 体なので「手番で補充」と「ターン頭で補充」は供給として同値。**" : "**0 体ではないので供給がずれる**——報告書に書く。"));
        Console.WriteLine("（同速 4 の敵は速さ降順 → チーム → スロットの安定ソートでプレイヤー側が先。）");
        Console.WriteLine();

        // ---- Q0-2 ------------------------------------------------------------------
        Console.WriteLine("## Q0-2 —— `SurrendersTurn` の効き方");
        Console.WriteLine();
        var surrFalse = new List<TraitId>();
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            Trait tr;
            try { tr = TraitCatalog.Get(id); } catch (KeyNotFoundException) { continue; }
            if (!tr.SurrendersTurn) surrFalse.Add(id);
        }
        Console.WriteLine($"- 既定 `true`。`false` にしている札: **{surrFalse.Count} 枚** "
            + string.Join(" / ", surrFalse.Select(t => $"`{t}`（{string.Join("・", ParryScan.Holders(t).Select(d => d.Name))}）")));
        Console.WriteLine($"- 買い手（`Trait.SurrenderedTurn(` を呼ぶ箇所）: engine **{Count(engine, "Trait.SurrenderedTurn(")} 箇所**"
            + $"（据え・背かれの計数）／ Traits.cs **{Count(traits, "SurrenderedTurn(ctx, ally)", "SurrenderedTurn(ctx, u)")} 箇所**（号令と判定本体）");
        Console.WriteLine("- 判定本体は `Trait.SurrenderedTurn`（1箇所）: 「`CanAct` が偽の札のうち `SurrendersTurn` が偽のものが1つも無い」。");
        Console.WriteLine("  **`CanAct` を偽にする札と `SurrendersTurn` は同じ札に載せる**（別の札に分けると片方だけ見て食い違う。`ImmobileTrait` / `PursuerTrait` の doc）。");
        Console.WriteLine("  → 構えを持つ札（受け流し）が `CanAct(Attack)` を偽にし、**同じ札の `SurrendersTurn` を段3 で `true` にすれば号令・据えが買い取る。**");
        Console.WriteLine();

        // ---- Q0-3 ------------------------------------------------------------------
        Console.WriteLine("## Q0-3 —— `Stoic` が号令の強化を弾いて隣へ流す経路");
        Console.WriteLine();
        string support = Between(engine, "public IReadOnlyList<UnitState> SupportTargets(UnitState u)", "public int Opponent(");
        string whet = Between(engine, "public void Whet(UnitState target, int amount", "\n    public ");
        string rally = Between(traits, "public sealed class RallyTrait", "\npublic ");
        if (support.Length == 0 || whet.Length == 0 || rally.Length == 0) { Console.WriteLine("**走査が空。止める**（第117期）。"); return; }
        Console.WriteLine($"- `RallyTrait` が `ctx.Whet` を呼ぶ前に `SupportTargets(ally)` を通す箇所: **{Count(rally, "ctx.SupportTargets(ally)")} 箇所**（鬨・溜めの両方）");
        Console.WriteLine($"- `SupportTargets`: `AcceptsSupport` なら本人（{Count(support, "if (u.AcceptsSupport) return new[] { u }")} 箇所）／`Stoic` なら**隣接する `AcceptsSupport` の味方全員**（`FormationRules.AreAdjacent`・{Count(support, "FormationRules.AreAdjacent(u.Slot, a.Slot)")} 箇所）／それ以外は空");
        string whetCode = string.Join("
", whet.Split('
').Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///")));
        Console.WriteLine($"- `Whet` 本体（コメントを除く）の `AcceptsSupport` の読み: **{Count(whetCode, "AcceptsSupport")} 箇所**——横流し（`Funnel`）の宛先候補を濾す1箇所だけで、**宛先（`receiver`）自体は検査しない**（窓口は見ない。呼び出し側に残す・第56期）");
        Console.WriteLine("- **したがってガルド自身は号令から 1 も得ない。得るのは隣接する味方**（角なら 2 枠・中央なら 4 枠。隣接は `AdjacencyTable`）。");
        Console.WriteLine("  据え（バン）だけは `ApplyDamage` の中で本人の被弾を半減するので `Stoic` を通らない → **中継の危険はガンではなくバン**（指示書 §1-3）。");
        Console.WriteLine();

        // ---- Q0-4 ------------------------------------------------------------------
        Console.WriteLine("## Q0-4 —— `Bulwark`（据え）の半減が乗る条件");
        Console.WriteLine();
        string bul = Between(engine, "// 据え: このターン差し出された駒は硬くなる", "// 散開:");
        if (bul.Length == 0) { Console.WriteLine("**据えの段を引けなかった。止める。**"); return; }
        Console.WriteLine($"- 読むのは **`StatusKeys.IdleTurn`**（`RawCounter(StatusKeys.IdleTurn) >= Turn`: {Count(bul, "IdleTurn) >= Turn")} 箇所）と **`Trait.SurrenderedTurn`**（{Count(bul, "Trait.SurrenderedTurn(this, target)")} 箇所）。別のカウンタは読まない。");
        Console.WriteLine("- `IdleTurn` は**その駒の手番が回ったとき**（行動順ループの中・速4 なら敵のほぼ全員の後）に立つ。");
        Console.WriteLine("  → **構えたターンのうち、ガルドの手番より後に来た被弾だけが半減される**（速 4 より遅い敵が 0 体なら、そのターンの被弾には乗らず、`>= Turn` は次のターンで偽になる）。");
        Console.WriteLine("  → 据えがガルドに乗るのは**同速の敵・ターン外の割り込み（棘の反撃など）だけ**。Q0-1' の分布を見ること。");
        Console.WriteLine("- 号令（ガン）は次のターン頭に `idle == Turn - 1` で払う（`Stoic` で隣へ）。");
        Console.WriteLine();

        // ---- Q0-5 ------------------------------------------------------------------
        Console.WriteLine("## Q0-5 —— 群A / 群B / 群C を機械で引く（§1-4 の 34 / 5 / 2 と照合）");
        Console.WriteLine();
        foreach ((string title, var rows) in new[] { ("`compare` 61 行", Presets.Compare), ("交差帯 12 行", Presets.Cross) })
        {
            var grp = rows.Select(r => (r.Name, G: GroupOf(r.F))).ToList();
            Console.WriteLine($"### {title}（{rows.Length} 行）");
            Console.WriteLine();
            Console.WriteLine($"- ガルドを含む行: **{grp.Count(x => x.G != Group.None)}** ／ 含まない行: **{grp.Count(x => x.G == Group.None)}**");
            Console.WriteLine($"- **群A**（ガン・バンなし）: **{grp.Count(x => x.G == Group.A)} 行**");
            Console.WriteLine($"- **群B**（ガンのみ）: **{grp.Count(x => x.G == Group.B)} 行** — {string.Join(" / ", grp.Where(x => x.G == Group.B).Select(x => x.Name))}");
            Console.WriteLine($"- **群B'**（**バンのみ**。指示書に無い群）: **{grp.Count(x => x.G == Group.Bb)} 行** — {string.Join(" / ", grp.Where(x => x.G == Group.Bb).Select(x => x.Name))}");
            Console.WriteLine($"- **群C**（ガンとバンの両方）: **{grp.Count(x => x.G == Group.C)} 行** — {string.Join(" / ", grp.Where(x => x.G == Group.C).Select(x => x.Name))}");
            Console.WriteLine();
        }
        var all = Presets.Compare.Concat(Presets.Cross).Select(r => (r.Name, G: GroupOf(r.F))).ToList();
        Console.WriteLine($"**両方を足すと 群A {all.Count(x => x.G == Group.A)} / 群B {all.Count(x => x.G == Group.B)} / 群B' {all.Count(x => x.G == Group.Bb)} / 群C {all.Count(x => x.G == Group.C)}。**"
            + " 指示書 §1-4 の 34 / 5 / 2 は `compare` 61 行だけの数ではない——**34 は「ガルドを含む行」の総数で、群A はそれから群B・群C を引いた数**である（照合結果は報告書に）。");
        Console.WriteLine();
        var prim = new HashSet<string>(Baseline.PrimaryRows);
        int primNoGald = Presets.Compare.Count(r => prim.Contains(r.Name) && GroupOf(r.F) == Group.None);
        Console.WriteLine($"- 主判定 19 行のうちガルドを含まない行: **{primNoGald} 行**（A10 の「ガルドを含まない 19 行」は `compare` 61 行のうちの {Presets.Compare.Count(r => GroupOf(r.F) == Group.None)} 行を指す）");
        Console.WriteLine();

        // ---- Q0-6 ------------------------------------------------------------------
        Console.WriteLine("## Q0-6 —— 受け流しを置く段（`ApplyDamageBody` の並び。第135期 Q0-6 の再走査）");
        Console.WriteLine();
        int bodyAt = engine.IndexOf("void ApplyDamageBody(", StringComparison.Ordinal);
        if (bodyAt < 0) { Console.WriteLine("**`ApplyDamageBody` を引けなかった。止める**（第117期）。"); return; }
        string[] marks =
        {
            "棘守りの上限|target.HasTrait(TraitId.ThornGuard)",
            "**入口**（`ModifyIncomingDamage` ＝ 脆弱・育ち耐性）|t.ModifyIncomingDamage(target, amount)",
            "惨禍 +50%|HavocTrait.Percent",
            "据え|BulwarkTrait.ReductionPercent",
            "散開|LooseTrait.ReductionPercent",
            "萎縮|CowerTrait.ReductionPercent",
            "巨躯（肩代わり）|Colossus.Percent",
            "分かち（肩代わり）|SharerTrait.Percent",
            "破片（Armor）|StatusKeys.Armor",
            "`lethal: false` のクランプ|if (!lethal) amount",
            "**受け流し**（第135期に置いた段）|ParryTrait.UsedKey",
            "猶予（HP1 で耐える）|ReprieveTrait.UsedKey",
            "不死（器具）|TraitId.Undying",
            "軛（1発の上限）|Yoke.Cap",
            "**HP を引く**|target.Hp -= amount",
        };
        Console.WriteLine("| # | 段 | 目印の位置（本体の先頭からの行数） |");
        Console.WriteLine("|---:|---|---:|");
        int prev = -1; bool ordered = true; int k = 0;
        foreach (string m in marks)
        {
            string[] p = m.Split('|');
            int at = engine.IndexOf(p[1], bodyAt, StringComparison.Ordinal);
            int line = at < 0 ? -1 : LineOf(engine, at) - LineOf(engine, bodyAt);
            if (line >= 0 && line < prev) ordered = false;
            if (line >= 0) prev = line;
            Console.WriteLine($"| {++k} | {p[0]} | {(line < 0 ? "**引けない**" : line.ToString())} |");
        }
        Console.WriteLine();
        Console.WriteLine(ordered ? "**並びは表のとおり単調**（走査で確認）。" : "**並びが単調でない。走査を疑うこと。**");
        Console.WriteLine("**受け流しは入口（`ModifyIncomingDamage`）に置かない**——惨禍・脆弱が 0 にしたつもりの量を押し戻す（第25・126・135期）。");
        Console.WriteLine("**破片・肩代わり・据え・散開・萎縮より後ろ、猶予・軛より前**——第135期の位置をそのまま使う（段を動かさない）。");
        Console.WriteLine();

        // ---- Q0-7 ------------------------------------------------------------------
        Console.WriteLine("## Q0-7 —— `RedirectGainTrait` の他の保持者（攻撃力アップの振り替えが敵に及ばないか）");
        Console.WriteLine();
        var subs = new List<TraitId>();
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            Trait tr;
            try { tr = TraitCatalog.Get(id); } catch (KeyNotFoundException) { continue; }
            if (tr is RedirectGainTrait) subs.Add(id);
        }
        Console.WriteLine($"- `RedirectGainTrait` を継承する札: **{subs.Count} 枚** — {string.Join(" / ", subs.Select(t => "`" + t + "`"))}");
        foreach (TraitId t in subs)
        {
            var mine = ParryScan.Holders(t).Select(d => d.Name).ToList();
            var foes = new SortedSet<string>();
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                foreach (var o in EnemyCatalog.Stages[w].Enemy.Occupied())
                    if (o.Def.Traits.Contains(t)) foes.Add(o.Def.Name);
            Console.WriteLine($"  - `{t}`: 味方 {mine.Count} 枚（{string.Join("・", mine)}）／ 敵 {foes.Count} 種（{string.Join("・", foes)}）");
        }
        string rbody = Between(traits, "public abstract class RedirectGainTrait", "\npublic ");
        string gbody = Between(traits, "public sealed class GuardianTrait", "\npublic ");
        Console.WriteLine($"- `self.AtkBonus += gain`: 基底 **{Count(rbody, "self.AtkBonus += gain")} 箇所**／`GuardianTrait` **{Count(gbody, "self.AtkBonus += gain")} 箇所**");
        Console.WriteLine("- **基底を触ると殉教者にも及ぶ。** 振り替えは「見返り」を基底の virtual に切り出し、**`GuardianTrait` だけが上書きする**形にする（殉教は既定＝攻撃力のまま）。A5 は `wall check` が敵側 0 件を実測する。");
        Console.WriteLine();

        // ---- Q0-8 ------------------------------------------------------------------
        Console.WriteLine("## Q0-8 —— `docs/harm.md` の再生成");
        Console.WriteLine();
        Console.WriteLine("段ごとに `parry harm > docs/harm.md` を回し、前後を報告書に並べる（8 枚ぶん）。段2 からは受け流しの表（表I）が足される。");
        Console.WriteLine();

        // ---- Q0-9 ------------------------------------------------------------------
        Console.WriteLine("## Q0-9 —— `checkup` の分類表");
        Console.WriteLine();
        int hcParry = Regex.Matches(prog, @"\[TraitId\.Parry\]\s*=\s*\(Hc").Count;
        int hcGuardian = Regex.Matches(prog, @"\[TraitId\.Guardian\]\s*=\s*\(Hc").Count;
        Console.WriteLine($"- `hcLabel` に `TraitId.Parry` の行: **{hcParry} 件**（0 なら段2 で足す。足さないと `checkup` が止まる——第128期の穴）");
        Console.WriteLine($"- `hcLabel` の `TraitId.Guardian` の行: **{hcGuardian} 件**（理由の文「その傷で育つ」は段2 で書き換える）");
        Console.WriteLine();

        // ---- Q0-10 -----------------------------------------------------------------
        Console.WriteLine("## Q0-10 —— 過去に庇いの確率を触った期");
        Console.WriteLine();
        string designDir = Path.Combine(ParryScan.Root!, "design");
        var hits = Directory.GetFiles(designDir, "*.md")
            .Where(f => File.ReadAllText(f).Contains("RedirectPercent", StringComparison.Ordinal))
            .Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Console.WriteLine($"- `design/*.md` で `RedirectPercent` に触れる文書: **{hits.Count} 件** — {string.Join(" / ", hits)}");
        Console.WriteLine($"- `Traits.cs` の `RedirectPercent = 50`: **{Count(traits, "RedirectPercent = 50")} 箇所**（ガルド）／ `RedirectPercent = 45`: {Count(traits, "RedirectPercent = 45")} 箇所（後備え）／ `DefaultPercent = 75`: {Count(traits, "DefaultPercent = 75")} 箇所（殉教）");
        Console.WriteLine("- 掃引されたのは**殉教者の割合だけ**（第35期・50/75/100）。**ガルドの 50 は最初のコミット以来一度も動いていない**（`git log -S` は報告書に）。");
        Console.WriteLine();
    }

    // =================================================================================
    // 段2 の N の決め方（§5-2）——**段1 の実測から引く**。`ParryRule(0)` は段1 と同値。
    // =================================================================================

    static void DeriveN()
    {
        Console.WriteLine("# 第136期 §5-2 —— N の決め方（段1 の実測から）");
        Console.WriteLine();
        Console.WriteLine("`ParryRule(Uses: 0)` で回す——**段2 の実装で `Uses = 0` は段1 と1ビットも違わない**（`wall check` の (c) が検算）。");
        Console.WriteLine();
        var led = ParryDiag.Collect(new ParryRule(0, ParryScope.Any));
        var G = led["gald"]; var M = led["golm"];
        double gLife = G.LifeSum / (double)G.Battles, mLife = M.LifeSum / (double)M.Battles;
        double taken = G.Taken / (double)G.Battles;
        double perT = taken / gLife;
        double dT = mLife - gLife;
        Console.WriteLine("## 材料（段1 の `docs/harm.md` と同じ帳簿）");
        Console.WriteLine();
        Console.WriteLine($"- ガルド: 被弾 **{taken:F1}/戦**・生存 **{gLife:F2}T** → **1ターンあたり {perT:F1} 点**");
        Console.WriteLine($"- ゴルム: 生存 **{mLife:F2}T** → 差 **{dT:+0.00;-0.00}T**");
        Console.WriteLine($"- 埋めるのに要る量: {perT:F1} × {dT:F2} = **{perT * dT:F1} 点**");
        Console.WriteLine();
        Console.WriteLine("## 受け流し1回で消える平均量（致命打の経路で重み付け）");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 1回あたりの平均量 | 致命打の取り分 |");
        Console.WriteLine("|---|---:|---:|");
        double wsum = 0, wavg = 0;
        long fatalSum = G.Fatal.Sum();
        for (int i = 0; i < DamageRoutes.Count; i++)
        {
            if (G.Hits[i] == 0) continue;
            double avg = G.Amount[i] / (double)G.Hits[i];
            double share = fatalSum == 0 ? 0 : G.Fatal[i] / (double)fatalSum;
            var r = (DamageRoute)i;
            bool parriable = r is DamageRoute.Single or DamageRoute.Sweep or DamageRoute.Pierce or DamageRoute.All;
            Console.WriteLine($"| {DamageRoutes.Names[i]}{(parriable ? "" : "（受け流せない）")} | {avg:F1} | {share * 100:F1}% |");
            if (parriable) { wsum += share; wavg += share * avg; }
        }
        double avgHit = wsum == 0 ? 0 : wavg / wsum;
        double allAvg = taken / (G.Hits.Sum() / (double)G.Battles);
        Console.WriteLine();
        Console.WriteLine($"- 受け流せる経路（単体・薙ぎ・貫き・全体）を致命打の取り分で重み付けした平均: **{avgHit:F1} 点/回**（参考: 全経路の単純平均 {allAvg:F1}）");
        double nExact = avgHit == 0 ? 0 : perT * dT / avgHit;
        int n = Math.Max(1, (int)Math.Round(nExact));
        Console.WriteLine($"- **N = {perT * dT:F1} ÷ {avgHit:F1} = {nExact:F2} → 中央 {n}、振るのは {Math.Max(1, n - 1)} / {n} / {n + 1}**");
        Console.WriteLine();
        Console.WriteLine("**注**: この N は「1戦の総量」から引いた数だが、段2 の実装は**構え（毎ターン頭）で満タンに戻る**ので");
        Console.WriteLine("実効の在庫は N × 生きたターン数になる。指示書 §5-2 の決め方はそのまま当て、**過剰なら報告書に書く**（線を後から動かさない・第64期）。");
    }

    // =================================================================================
    // 段2 / 段3 —— 版 × 群A/B/C（本体は段2 の実装後に書く）
    // =================================================================================

    static void Stage2(string arg)
    {
        Console.WriteLine("wall run: 段2 の実装後に書く。");
    }

    static void Check(string arg)
    {
        Console.WriteLine("wall check: 段2 の実装後に書く。");
    }
}
