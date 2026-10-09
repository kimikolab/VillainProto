using BattleCore;
using static Common;

// =====================================================================================
// dump モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "dump")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 dump
// =====================================================================================

static class DumpDiag
{
// dump モード: カタログから資料を吐く。手書きの一覧とコードがずれないようにするため。
public static void Run(string[] args, int stageIndex)
{
    static string Pat(AttackPattern p) => p switch
    {
        AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き",
        AttackPattern.All => "全体", _ => "単体"
    };
    Console.WriteLine("# ユニット・特性・ステージ一覧");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 dump > docs/units.md` の出力。手で編集しない。");
    Console.WriteLine();
    Console.WriteLine("## ユニット");
    Console.WriteLine();
    // 行動列は「説明文と挙動のズレ」を防ぐための列（過去4回発生）。Actions を持たない駒は
    // 空欄——味方は全員そちらなので、この表の見た目は第9期までと変わらない。
    static string Acts(UnitDef u) => u.Actions is null
        ? ""
        : string.Join(" → ", u.Actions.Select(a => a.Kind switch
        {
            ActionKind.Charge => a.Label ?? "溜め",
            ActionKind.Skill => a.Label ?? "術",
            _ => a.AttackPercent == 100 ? "攻撃" : $"攻撃×{a.AttackPercent}%"
        }));

    // 踏込 列は**表示専用**（第131期）。engine に射程という軸は無く、この札を読んで分岐する
    // 規則は 0 件——`DemoApp` が「敵の前まで出て振るか、その場から振るか」を選ぶためだけにある。
    // **列は末尾側に足すこと**——`checkup check` の (a) は `docs/units.md` の
    // 2〜4 列目（HP / 攻 / 速）を位置で読むので、前に挟むとその自己検査が壊れる。
    static string Adv(UnitDef u) => u.Advances ? "踏込" : "据置";

    Console.WriteLine("| 名前 | HP | 攻 | 速 | 型 | 踏込 | 行動 | プラス | マイナス | 由来 |");
    Console.WriteLine("|---|---:|---:|---:|---|---|---|---|---|---|");
    foreach (UnitDef u in UnitCatalog.All.Where(u => u.Id != "spore"))
        Console.WriteLine($"| **{u.Name}** | {u.MaxHp} | {u.Attack} | {u.Speed} | {Pat(u.Pattern)} | {Adv(u)} | {Acts(u)} | {u.PlusText} | {u.MinusText} | {u.Flavor} |");

    Console.WriteLine();
    Console.WriteLine("## 特性");
    Console.WriteLine();
    Console.WriteLine("| 特性 | 保持者 |");
    Console.WriteLine("|---|---|");
    foreach (TraitId id in Enum.GetValues<TraitId>())
    {
        var owners = UnitCatalog.All.Where(u => u.Traits.Contains(id)).Select(u => u.Name).ToList();
        Console.WriteLine($"| `{id}` | {(owners.Count == 0 ? "-" : string.Join("、", owners))} |");
    }

    // 第246期 前段: 敵の火勢（第245期 E2）は駒ではなく燃焼そのものの規則として書く。札の持ち主（火勢の土台を持つ駒）が味方にいるときだけ働くので、
    // 持ち主は `UnitCatalog.All` から引く（手で名前を書かない）。持ち主がいなければ節ごと出さない。
    var fireRuleOwners = UnitCatalog.All.Where(u => u.Traits.Contains(TraitId.FoeFireLevel)).Select(u => u.Name).ToList();
    // 第256期: 被弾の燃焼（H-分担）は駒の札ではなく `EmberRule.Default` の規則なので、持ち主に依らず節を出す。
    bool burnHit = EmberRule.Default.BurnHit;
    if (fireRuleOwners.Count > 0 || burnHit)
    {
        Console.WriteLine();
        Console.WriteLine("## 燃焼の規則");
        Console.WriteLine();
    }
    if (burnHit)
    {
        Console.WriteLine("どの編成でも（第256期に規定）:");
        Console.WriteLine();
        Console.WriteLine($"- 燃えている駒は、出どころのある一撃を受けるたびに炎が燃え上がり、燃焼 {BurnRules.Damage} を火の強さの回数だけ（火の強さを持たない編成では1回）受ける"
                          + "（その一撃より前から燃えていたときだけ。毒と燃焼そのもの・肩代わりで受けた分・自分の一撃・かわした一撃では燃え上がらない）。殴られても燃焼の残りターンは減らない。");
        Console.WriteLine($"- ターンの頭の燃焼は火の強さに関わらず {BurnRules.Damage}。");
        Console.WriteLine();
    }
    if (fireRuleOwners.Count > 0)
    {
        // 第248期: 燃焼の軸の一区切り——味方の火勢（育つ・萎む・大技で戻る）も今の規定に合わせて書く。数値は規則の定数から引く。
        Console.WriteLine($"味方に{string.Join("・", fireRuleOwners)}がいるとき（第242〜248期に規定）:");
        Console.WriteLine();
        Console.WriteLine($"- 燃えている駒には火の強さ（1〜{FireLevelRule.Max}）がある。燃えていなかった駒に火が点くと 1（点け直しでは上がらない）。燃焼が切れると消える。");
        Console.WriteLine("- 燃えている駒の攻撃が、当たる前から燃えていた敵に当たると、その駒の火が育つ（1回の攻撃で 1 つまで）。そのとき火選りの駒も育つ。そのターン一度も育たなかった駒は、ターンの終わりに 1 つ弱まる（1 より下にはならない）。");
        Console.WriteLine("- 火を渡した駒・大技（放つ・焼き尽くす）を撃った駒の火は 1 に戻る。");
        Console.WriteLine($"- 燃えている敵にも火の強さがある。燃えている味方が燃えている敵を叩くたびに1つ育ち、育たない周回は弱まる。"
                          + (burnHit ? $"火が強いほど、殴られたときの燃え上がりは火の強さの回数だけ入り（1回 {BurnRules.Damage}）、受ける傷は大きくなる（"
                                     : $"火が強いほど、燃焼の刻みは火の強さの回数だけ入り（1回 {BurnRules.Damage}）、受ける傷は大きくなる（")
                          + string.Join(" ／ ", Enumerable.Range(1, 4).Select(l => $"{l} +{FoeFireRule.BrittlePercent[l]}%")) + "）。");
        Console.WriteLine($"- 火の強さ 4 の敵が倒れると、隣の敵に火が移る（燃えていなければ火の強さ {FoeFireRule.SpreadLevel} で点く・燃えていれば 1つ育つ）。");
        Console.WriteLine(burnHit
            ? "- 味方の燃え上がりも、火の強さの回数だけ入る（火に焼かれない駒は焼かれず、火選りの駒が生きている間は、燃えている味方は焼かれる代わりに癒える）。"
            : "- 味方の燃焼の刻みも、火の強さの回数だけ入る（火に焼かれない駒は焼かれず、火選りの駒が生きている間は、燃えている味方は焼かれる代わりに癒える）。");
    }

    Console.WriteLine();
    Console.WriteLine("## ステージ");
    Console.WriteLine();
    // 第187期: 敵は盤面に出るとき `EnemyScaleRule.Default` の倍率が掛かる。**盤面の値を出し、定義の素の値を〈〉に添える**。
    EnemyScaleRule esc = EnemyScaleRule.Default;
    if (esc.Active)
    {
        Console.WriteLine($"敵は盤面に出るとき最大HP {esc.HpPercent}% ／ 攻撃力 {esc.AtkPercent}% が掛かる（`EnemyScaleRule.Default`・第187期）。"
                          + "下の値は掛けた後で、〈〉は定義の素の値。");
        Console.WriteLine();
    }
    string Stat(int scaled, int raw) => scaled == raw ? $"{scaled}" : $"{scaled}〈{raw}〉";
    foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
    {
        var e = st.Enemy.Occupied().Select(x => (Raw: x.Def, Def: esc.Apply(x.Def))).Select(x =>
            $"{x.Def.Name}(HP{Stat(x.Def.MaxHp, x.Raw.MaxHp)}/攻{Stat(x.Def.Attack, x.Raw.Attack)}/{Pat(x.Def.Pattern)}/{Adv(x.Def)}"
            + (x.Def.Actions is null ? "" : $"/{Acts(x.Def)}")
            + (x.Def.Traits.Count == 0 ? "" : "/札 " + string.Join("・", x.Def.Traits.Select(t => $"`{t}`"))) + ")");   // 第306期: 札を読めるように
        Console.WriteLine($"- **{st.Name}**: {string.Join("、", e)}");
    }
    return;
}
}
