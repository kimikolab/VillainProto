using BattleCore;
using static Common;

// =====================================================================================
// checkup モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "checkup")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 checkup
// =====================================================================================

static class CheckupDiag
{
// 第82期 —— ロスター健康診断（残す・転生・差し替えの3分）。**調査だけ。設計判断をしない。**
// **新機構ゼロ・駒ゼロ・差し替えの実行もゼロ・`TraitId` ゼロ・engine の変更ゼロ・`docs/` の差分ゼロ。**
//
// ロスターは 51、上限は 52（トランプ由来）。ここから先の設計は全部「何を捨てるか」の判断になるので、
// **捨てる基準を先に確定させておく**（指示書 §0-1）。51 体を **残す / 転生 / 差し替え** の3つに分ける。
//
// **器具は第81期の 2×2**（`pairs2`）。**在席差（第80期）はこの期では1度も使わない**（指示書 §0-3）:
//
//     y11 = 両方が本物 / y10 = A 本物・B 素体 / y01 = A 素体・B 本物 / y00 = 両方が素体
//     相乗(A,B) = y11 − y10 − y01 + y00                          ← 縦軸（第81期）
//     単独(A)   = y11 − y01（**相方が本物のまま A だけを素体に**）  ← 横軸（第69期の標準器具）
//
// **2つの量は同じ台・同じ席・同じ戦闘 seed の4版から出る**ので、器具は1つしか使っていない。
// `pairs2` は1文字も書き換えていない——`run` の吐く TSV は**先頭 9 + NT 列が `pairs2` と完全に同じ**で、
// 生の y を後ろに足しただけなので、**同じファイルを `pairs2 tables` にも渡せる**（Q1 の突き合わせに使う）。
//
//     dotnet run --project BattleSim -c Release 0 checkup phase0            # 紙の計算（**戦闘0回**。線・拒否権・予測）
//     dotnet run --project BattleSim -c Release 0 checkup run <skip> <take> # 2×2 を測って TSV（**分割実行**。1回で全部回すと落ちる）
//     dotnet run --project BattleSim -c Release 0 checkup tables <a> [<b>]  # 表A〜F・Q1〜Q6（<b> は `pairs2 run` の TSV）
//     dotnet run --project BattleSim -c Release 0 checkup ideal             # 理想台の帰属だけ（61 行 × 在席枠）
//     dotnet run --project BattleSim -c Release 0 checkup check             # `compare` 305 セルの突き合わせ
public static void Run(string[] args, int stageIndex)
{
    string hcArg = args.Length > 2 ? args[2] : "";
    var hcSw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> hcStages = EnemyCatalog.Stages;
    int hcW = hcStages.Count;
    // **第119期**: `run51` のときだけ第82期のロスター（51 体）に切り替える。
    // 第82期の `All` は「現行 52 枚のうち トモ の席に ハリ が座り、ソム が居ない」形なので、
    // **並び順まで含めて**その1点だけを差し替えて作る（並びが変われば台の抽選が変わる）。
    bool hc51 = hcArg == "run51";
    var hcRoster = (hc51
        ? UnitCatalog.All.Select(d => d.Id == "tomo" ? UnitCatalog.Hari : d).Where(d => d.Id != "som")
        : UnitCatalog.All).ToArray();
    int hcRN = hcRoster.Length;                       // 52（`run51` では 51）
    int hcNK = UnitTally.CarryKeys.Length;            // 11

    // ---- 第81期 `pairs2` の定数の写し（**1つも変えていない**。変えたら器具が別物になる）--------------
    const int HcTableSeed = 8_100_000;
    const int HcK = 64;                               // 1組・1系列あたりの台数
    const int HcS = 2;                                // 独立系列の本数（自己検査 (g)）
    const int HcBand = 0, HcM = 8;                    // 戦闘 seed 0..7
    const int HcStrong = 7, HcWeakPct = 60, HcDrawCap = 20000;
    const int HcIdealSeeds = 200;                     // 理想台は `compare` と同じ seed 0..199

    // ---- この期に決めた線（**測る前に固定した。数字を見てから動かさない**。指示書 §2-1）--------------
    const double HcSoloLine = 1.5;    // 単独の帰属（第62期以来 16 期にわたって採否に使ってきた閾値）
    const double HcSynLine = 5.0;     // 最良の相方との相乗（第46期の配置の採否閾値）
    const double HcCeil = 95.0;       // 天井（§1-4）
    const double HcCeilShare = 50.0;  // 天井の台が半数を超えたら「相乗が測れていない」＝別扱い
    const int HcTop = 30;             // Q1 の上位・下位の行数

    var hcIdx = new Dictionary<string, int>();
    for (int u = 0; u < hcRN; u++) hcIdx[hcRoster[u].Id] = u;
    string[] hcName = hcRoster.Select(d => d.Name).ToArray();

    // =====================================================================================
    // 第119期 —— マイナスの分類（(i) 独立した札 / (ii) 1つの札が両義 / (iii) 数値）
    //
    // **`UnitCatalog` は1文字も触らない。** yP（プラスのみ）/ yM（マイナスのみ）は
    // ここで作るローカルの `UnitDef` で、**版が本物と同じになるときは同じ参照を返す**
    // ——(ii) の駒で `yP == y11` になることを、実行する前に参照の等価で担保するため。
    //
    // 札の3値（マイナス = 単独で外せる代金 / プラス = 純粋な払い出し / 両義 = 1つの動作の表と裏）。
    // **既定は `Traits.cs` の enum のブロックから機械で引き、食い違うものだけを下の表が上書きする**
    // （走査が空なら止める——第117期）。
    // =====================================================================================
    const int HcMinusL = 0, HcPlusL = 1, HcBothL = 2;
    string[] hcLabelName = { "マイナス", "プラス", "両義" };
    var hcLabel = new Dictionary<TraitId, (int L, string Why)>
    {
        [TraitId.Splash]     = (HcMinusL, "薙ぎの払い出しは `Pattern` の側にあるので、巻き込みだけを外せる"),
        [TraitId.Cinder]     = (HcBothL,  "敵への着火と隣の味方への延焼が `OnAfterAttack` の同じ1回"),
        [TraitId.Rage]       = (HcPlusL,  "被弾で自分の攻撃力が上がるだけ"),
        [TraitId.Hex]        = (HcBothL,  "自分を殴った駒に呪いが付く。**敵味方を問わない**ので味方の刃も呪う"),
        [TraitId.Sniper]     = (HcPlusL,  "後退したあと後列にいれば2倍＋貫き"),
        [TraitId.Coward]     = (HcMinusL, "別の札。**ただし外すと後衛特化の起動条件（後退）を供給する者が居なくなる**"),
        [TraitId.Curse]      = (HcBothL,  "開戦時の敵全体への弱体と、味方全体への漏れが同じ発火"),
        [TraitId.Guardian]   = (HcPlusL,  "味方への単体攻撃を必ず肩代わりし、身に受けるたび受け流しの在庫を1つ戻す（第136期。攻撃力は上がらない）"),
        // 第136期 段2: 受け流しを本採用したときに足した（第128期の穴＝分類を足さずに `checkup` が止まる、を繰り返さない）。
        [TraitId.Parry]      = (HcBothL,  "回数ぶん敵の一撃を無効化して構えで戻すのと、自分からは攻撃しないことが1つの札（`Pursuer` と同型）"),
        // 第153期 段A: 預かりを足したときに分類も足した（第128期の穴＝分類を足さずに `checkup` が止まる、を繰り返さない）。
        [TraitId.Ward]       = (HcPlusL,  "味方の傷を預かって後から本人へ返すだけ。代金は `Forfeit` に切り出してある（第74期の作法）"),
        [TraitId.Forfeit]    = (HcMinusL, "預かったまま味方が倒れるとその全額が敵の回復になる、だけの別の札"),
        [TraitId.Burden]     = (HcMinusL, "預かりを抱えている味方の被ダメージが増える、だけの札（第154期・代金の枝1）"),
        [TraitId.Laden]      = (HcMinusL, "預かりを抱えている味方の攻撃力が下がる、だけの札（第154期・代金の枝2）"),
        [TraitId.Stoic]      = (HcMinusL, "支援を受け付けない、だけの別の札"),
        [TraitId.Necro]      = (HcPlusL,  "味方が倒れるたび層を積む"),
        [TraitId.Sacrifice]  = (HcMinusL, "開戦時に隣接する味方を削る、だけの別の札"),
        [TraitId.Colossus]   = (HcPlusL,  "後ろの味方への攻撃を肩代わりし、飲み込んだ量を返す"),
        [TraitId.Drain]      = (HcMinusL, "毎ターン味方から吸う、だけの別の札"),
        [TraitId.Sluggish]   = (HcMinusL, "2ターンに1回しか動かない、だけの別の札"),
        [TraitId.Splitter]   = (HcPlusL,  "**上書き**——ムグの `PlusText` は分裂そのもの。enum のマイナス側ブロックに並ぶのは追加順の都合"),
        [TraitId.Bomber]     = (HcBothL,  "**上書き**——破裂が敵と味方を同時に巻き込む（enum はマイナス側ブロック）"),
        [TraitId.Reviver]    = (HcBothL,  "1回縫うごとに自分の最大HPが半分になるのが同じ動作"),
        [TraitId.Venom]      = (HcBothL,  "殴られて毒を積むのと、毒が隣接する味方へ漏れるのが同じ発火"),
        [TraitId.ThornGuard] = (HcBothL,  "身代わりと位置の入れ替えが1つの動作（入れ替えた相手は必ず反撃に巻き込まれる）"),
        [TraitId.Thorns]     = (HcBothL,  "反撃が隣の味方も巻き込む"),
        [TraitId.Immobile]   = (HcMinusL, "**上書き**——自分からは攻撃しない、だけの別の札（enum はプラス側ブロック）"),
        [TraitId.Havoc]      = (HcMinusL, "**上書き**——味方全体の被ダメージが5割増える、だけの別の札（同上）"),
        [TraitId.Marker]     = (HcBothL,  "隣の味方に敵の攻撃を集める＝押し出しが効果そのもの"),
        [TraitId.Mender]     = (HcBothL,  "繕った量の半分だけ自分が減るのが同じ動作"),
        [TraitId.Seal]       = (HcMinusL, "第74期に縫いから切り出した代金の札"),
        [TraitId.Amplifier]  = (HcPlusL,  "敵の毒だけを濃くする（味方の毒には触らない）。マイナスは条件依存で機構の外"),
        [TraitId.Contagion]  = (HcPlusL,  "毒持ちが倒れたときに撒く。マイナス（自分では毒を積めない）は機構の外"),
        [TraitId.Miasma]     = (HcBothL,  "毎ターンの散布が敵にも味方にも同時に及ぶ"),
        [TraitId.Paralyze]   = (HcPlusL,  "殴った相手を止めるだけ"),
        [TraitId.Devour]     = (HcBothL,  "敵の毒を数えて癒すのと、味方が負った毒が2倍に効くのが同じ札"),
        [TraitId.Rally]      = (HcPlusL,  "開戦時＋休み番への強化"),
        [TraitId.Blightfed]  = (HcPlusL,  "味方の毒を吸って育つ。マイナス（毒が無ければ無為）は条件依存"),
        [TraitId.Displaced]  = (HcBothL,  "動かされて育つ＝自分では動かないことが条件そのもの"),
        [TraitId.Shuffler]   = (HcBothL,  "毎ターン味方2体を入れ替える（相手を選べないことが効果そのもの）"),
        [TraitId.Bind]       = (HcBothL,  "縛り（動けない）と攻撃+16 が1つの動作"),
        [TraitId.Bulwark]    = (HcPlusL,  "動かなかった味方の被ダメージを半減。マイナスは条件依存"),
        [TraitId.Overload]   = (HcPlusL,  "閾値を越えているあいだ薙ぎ。積めないのは条件（マイナスの外部化）"),
        [TraitId.Drifter]    = (HcPlusL,  "動かされた味方を癒し強化する。マイナスは条件依存"),
        [TraitId.Perverse]   = (HcBothL,  "強化で弱くなり弱体で強くなるが1つの規則"),
        [TraitId.Sharer]     = (HcBothL,  "肩代わりと自分の消耗が同じ動作"),
        [TraitId.Loose]      = (HcBothL,  "隣が空いた駒を硬くするのと、隣を弾くのが1つの動作"),
        [TraitId.Brace]      = (HcBothL,  "上限と破片の供給（プラス）と、自分の攻撃・味方の手番（マイナス）が1つの動作"),
        // **第139期がガレを `UnitCatalog.All` に入れたときに足し忘れていた**（第143期に判明）。
        // 第131期「駒に札を足す作業には、その札を分類している診断の一覧を添えること」の再発で、
        // `checkup check` は第139期から4期ぶん「止める」しか出していなかった。
        [TraitId.Shrapnel]   = (HcBothL,  "敵全体への打点（プラス）と、味方の破片・自分の手番（マイナス）が1つの動作"),
        [TraitId.Cower]      = (HcBothL,  "被ダメ −30% と味方全体の攻撃 −9 が1つの動作"),
        [TraitId.Pursuer]    = (HcBothL,  "ターン外の割り込みと、自分の手番では動かないことが1つの規則"),
        [TraitId.RearGuard]  = (HcPlusL,  "後列を肩代わりして育つ"),
        [TraitId.Pyre]       = (HcPlusL,  "燃えているあいだ4倍＋貫き。マイナスは条件依存"),
        [TraitId.Shatter]    = (HcPlusL,  "範囲を浴びて破片を配る"),
        [TraitId.Frail]      = (HcMinusL, "受けるダメージが5割増える、だけの別の札"),
        [TraitId.Forsake]    = (HcBothL,  "速い味方を癒し遅い味方を削るが1つの規則（enum が明記）"),
        [TraitId.Torment]    = (HcBothL,  "封じられた敵には追い打ち・動ける敵には自滅が1つの規則（同上）"),
        [TraitId.Avenge]     = (HcBothL,  "標的の味方への割り込みと、自分が殴られたときの怯みが1つの規則（同上）"),
        [TraitId.Rend]       = (HcPlusL,  "**上書き**——薄刃（代金）は第74期に別の札へ切り出してあるので、裂き本体は払い出しだけ"),
        [TraitId.ThinBlade]  = (HcMinusL, "第74期に裂きから切り出した代金の札"),
        [TraitId.Gouge]      = (HcPlusL,  "**上書き**——深追い（代金）は第74期に別の札へ切り出してある"),
        [TraitId.Overreach]  = (HcMinusL, "第74期に抉りから切り出した代金の札"),
        [TraitId.Carve]      = (HcPlusL,  "刻んで上乗せする。代金は執着の側にある"),
        [TraitId.Fixate]     = (HcMinusL, "第73期に唯一独立して外せたマイナス"),
        [TraitId.Sever]      = (HcPlusL,  "**上書き**——刃待ち（代金）は第74期に別の札へ切り出してある"),
        [TraitId.Await]      = (HcMinusL, "第74期に断ちから切り出した代金の札"),
        [TraitId.Suture]     = (HcPlusL,  "**上書き**——塞ぎ（代金）は第74期に別の札へ切り出してある"),
        [TraitId.Taillight]  = (HcBothL,  "灯と手番の譲渡が1つの動作"),
        [TraitId.Shove]      = (HcBothL,  "敵陣の突き崩しと隣の味方のよろけが1つの動作"),
        [TraitId.Bear]       = (HcBothL,  "横取りして鎧に変えるのと、自分の腕が落ちるのが1つの動作"),
        [TraitId.Relay]      = (HcBothL,  "横取りして敵へ渡すのと、自分の身が削れるのが1つの動作"),
        [TraitId.Scale]      = (HcBothL,  "破片を拾って貫きになるのと、振るたび剥がれるのが1サイクル"),
        [TraitId.Divert]     = (HcBothL,  "視線を引き剥がすのと、自分に刺さるのが1つの動作"),
        [TraitId.Goad]       = (HcBothL,  "力を渡すのと、渡した相手を押し出すのが1つの動作"),
        [TraitId.Finisher]   = (HcBothL,  "標を必ず狙って倍で殴るのと、標を消費するのが1サイクル"),
        [TraitId.Favor]      = (HcBothL,  "燃えている味方を上げるのと、隣の燃えていない味方を鈍らせるのが1つの動作"),
        [TraitId.Betrayed]   = (HcBothL,  "喚び出しと、喚んだものが敵につくことが1つの動作"),
        // 第132期 段0-a: 第128期に `GradeStep` を ドルガ に載せたとき、この表に足さなかったので
        // `checkup` が「分類の無い札がある」で**3期ぶん止まっていた**（第131期に判明）。
        // 分類は積み過ぎ（`Overload`）と同じ——読む値も閾値も共有し、違うのは上がる先の段だけ。
        [TraitId.GradeStep]  = (HcPlusL,  "閾値を越えているあいだ薙ぎ・その `GradeTrait.StepFactor` 倍で全体。積めないのは条件（`Overload` と同型）"),
        // 第179期: 灰（スス）を足したときに分類も足した（第128期の穴＝分類を足さずに `checkup` が止まる、を繰り返さない）。
        [TraitId.Ash]        = (HcBothL,  "味方の自傷を灰として溜めて撒くのと、抱えたまま倒れると隣へ降るのが1つの在庫の表と裏"),
        // 第180期: ムド（暴発＋泥散り）・ヴィオ（吐き戻し）・ガン（叩き起こし）。
        // **マイナスを別の `TraitId` に切り出してあるので `yP` が組める**（第74期の作法）。
        [TraitId.Erupt]      = (HcPlusL,  "殴られた回数を溜めて割り込み連撃する。代金は別の札（`Smear`）に切り出してある"),
        [TraitId.Smear]      = (HcMinusL, "被弾のたび隣の味方の攻撃力が下がるだけ。暴発の代金で、外せば `yP` になる"),
        [TraitId.Spit]       = (HcPlusL,  "腹に溜めた毒を殴った相手へ移すだけ（澱み喰いの chain-out）"),
        [TraitId.Reveille]   = (HcPlusL,  "手番を差し出した味方1体に割り込みの一撃をさせるだけ"),
        // 第183期: ヴェル（縫い合わせ）・ラウ（触れてうつす＋漏れ）。分類を同じコミットで足す（第132期 段0-a の再発防止）。
        [TraitId.Stitch]     = (HcBothL,  "隣を回復するのと、縫われた者の最大HPが減るのが同じ針の表と裏"),
        [TraitId.Touch]      = (HcPlusL,  "毒を持つ敵を殴ると隣の敵へ写すだけ。代金は別の札（`TouchLeak`）に切り出してある"),
        [TraitId.TouchLeak]  = (HcMinusL, "うつすたび隣の味方に毒が付くだけ。触れてうつすの代金で、外せば `yP` になる"),
        // 第184期: ヒサ（矢面＋逃げ回る）・ザン（仇指し＋返り血）。分類を同じコミットで足す（第132期 段0-a の再発防止）。
        [TraitId.Beckon]     = (HcPlusL,  "隣でいちばん元気な味方に標を付け、その味方の被ダメージを半分にする。代金は別の札（`Flee`）に切り出してある"),
        [TraitId.Flee]       = (HcMinusL, "指差したら標の相手以外の隣と入れ替わって逃げるだけ。矢面の代金で、外せば `yP` になる"),
        [TraitId.Vendetta]   = (HcPlusL,  "標の味方が殴られると倍の刃を返し、殴った敵に標を付ける。代金は別の札（`Recoil`）に切り出してある"),
        [TraitId.Recoil]     = (HcMinusL, "刃を返すたび自分が傷つくだけ（殺さない）。仇指しの代金で、外せば `yP` になる"),
        // 第185期: クグ（組み付き）・シガ（見せしめ）・バン（踏みしめ＋据えた足）。分類を同じコミットで足す。
        [TraitId.Grapple]    = (HcBothL,  "敵を掴んで止めるのと、掴んでいる間は自分も何もできず殴られるとほどけるのが1つの動作"),
        [TraitId.Shame]      = (HcPlusL,  "動けない敵を優先して狙い、責めると隣の敵を竦ませる。マイナスは責め苦の側"),
        [TraitId.Footing]    = (HcPlusL,  "手番で層を積んで被ダメージを減らし、隣の味方への範囲攻撃を代わりに受ける。代金は別の札（`Planted`）"),
        [TraitId.Deflect]    = (HcPlusL,  "自分への単体の一撃の半分を、指差した敵へ逸らす。代金は `Divert` の側（自分に刺さる標）"),
        [TraitId.Thrust]     = (HcPlusL,  "逸らした回数だけ攻撃力が倍になる貫きで、指差した敵の列を突く。回数は突くと 0"),
        [TraitId.ThrustPlain]= (HcPlusL,  "突きの対照（素の攻撃力で倍率）。保持者 0 枚"),
        [TraitId.Planted]    = (HcMinusL, "入れ替えを受け付けないだけ。踏みしめの代金で、外せば `yP` になる"),
        // 第188期: カタ（起爆＋逆流）。分類を同じコミットで足す（第132期 段0-a の再発防止）。
        [TraitId.Catalyst]   = (HcPlusL,  "敵全体の毒と燃焼をもう1回働かせる（両方持ちは倍）。代金は別の札（`Backfire`）に切り出してある"),
        [TraitId.Backfire]   = (HcMinusL, "起爆が味方の毒と燃焼も働かせるだけ。起爆の代金で、外せば `yP` になる"),
        [TraitId.Hexer]      = (HcPlusL,  "手番で敵を呪い、呪い持ち全員の攻撃力を下げる。代金は別の札（`HexLeak`）に切り出してある"),
        [TraitId.HexLeak]    = (HcMinusL, "呪うたび隣の味方の攻撃力が下がるだけ。外せば `yP` になる"),
        [TraitId.Huddle]     = (HcPlusL,  "味方全体の被ダメ −30%（旧 `Cower` の軽減だけ）"),
        [TraitId.Daunt]      = (HcPlusL,  "手番で敵を萎縮させる（次の一撃が半分）。代金は別の札（`DauntLeak`）に切り出してある"),
        [TraitId.DauntLeak]  = (HcMinusL, "萎縮させるたび隣の味方も萎縮するだけ。外せば `yP` になる"),
        [TraitId.Rebound]    = (HcPlusL,  "手番で前列の敵を後ろへ突き返して転ばせる＋押しのけられたときの突き崩し。代金は別の札（`Overrun`）"),
        [TraitId.Overrun]    = (HcMinusL, "突き返すたび自分が隣の味方と入れ替わるだけ。外せば `yP` になる"),
        // 第190期: ベニ（反転の結界）。分類を同じコミットで足す（第132期 段0-a）。
        [TraitId.Inverse]    = (HcPlusL,  "隣の味方の毒・燃焼の削りを回復に変える。代金は別の札（`InverseLeak`）に切り出してある"),
        [TraitId.Taint]      = (HcPlusL,  "手番で隣の味方全員に毒を1層積む（反転の燃料を自分で配る）"),
        [TraitId.InverseLeak]= (HcMinusL, "隣の味方への回復がダメージになるだけ。外せば `yP` になる"),
        [TraitId.Kindle]     = (HcPlusL,  "周期の火の拍で隣の味方全員に着火する（反転の燃料を自分で配る）"),
        // 第194期: ミオ（濃縮の印）。分類を同じコミットで足す（第132期 段0-a）。
        [TraitId.Concentrate]     = (HcPlusL,  "旧の +4 に続けて、刻みが最も大きい敵とその隣に印を +1（印1つにつき毒と燃焼の刻みが1回増える）。代金は別の札（`ConcentrateLeak`）に切り出してある"),
        [TraitId.ConcentrateLeak] = (HcMinusL, "手番ごとに隣の味方にも印が +1 されるだけ。外せば `yP` になる"),
        [TraitId.Spew]            = (HcPlusL,  "手番で一番手強い敵に毒 +6 と痺れ毒の印。攻撃を捨てた代金は `Actions` の側にある"),
        [TraitId.Numb]            = (HcPlusL,  "印のある敵の与ダメが毒の層 × 3%（上限 60%）下がる。外せば「鈍らせなし」になる"),
        [TraitId.SpewFixed]       = (HcPlusL,  "第195期の吐き（いつも一番手強い敵）。対照で保持者 0 枚"),
        [TraitId.VenomHeavy]      = (HcBothL,  "殴られて毒を +8 積むのと、毒が隣接する味方へ漏れるのが同じ発火（S+反の版・保持者 0 枚）"),
        // 第197期: ベニの紅蓮。分類を同じコミットで足す（第132期 段0-a）。
        [TraitId.Guren]           = (HcPlusL,  "啜りの余りを溜め、満ちたら敵全員に着火して等分した毒を積む"),
        [TraitId.GurenStrike]     = (HcPlusL,  "紅蓮の直撃の版（等分を直撃ダメージで）。対照で保持者 0 枚"),
        [TraitId.GurenLow]        = (HcPlusL,  "紅蓮の低閾の版（閾値 6）。対照で保持者 0 枚"),
        [TraitId.GurenFull]       = (HcPlusL,  "紅蓮の全額の版（等分せず満額）。参考で保持者 0 枚"),
        // 第198期: ガルドの剣の段。分類を同じコミットで足す（第132期 段0-a）。
        // 第199期
        [TraitId.Kiss]            = (HcPlusL,  "敵から精気を吸って最も傷ついた味方に与える（儀式・還る・溢れは破片）"),
        [TraitId.KissSpill]       = (HcMinusL, "吸った敵の状態を受け取った味方へ移す代金の札（第204期）"),
        [TraitId.KissBare]        = (HcPlusL,  "口づけ・吸うだけの版（儀式・還るなし）。対照で保持者 0 枚"),
        [TraitId.Kiss30]          = (HcPlusL,  "口づけ・30% の版。対照で保持者 0 枚"),
        // 第205期: 分類を同じコミットで足す（第132期 段0-a）。
        [TraitId.KissPain]        = (HcPlusL,  "口づけの吸う量を、前の手番から味方が受けた傷の半分（最低8）にする札"),
        [TraitId.KissVoid]        = (HcMinusL, "口づけの溢れを破片にせず捨てる札（第205期）"),
        [TraitId.KissTier]        = (HcPlusL,  "施した累計 40 ごとに段が上がり、1手番に吸う敵が増える札"),
        [TraitId.KissTri]         = (HcMinusL, "段の刻みを三角数にする札（段 n に 40 × n(n+1)/2 要る・第206期）"),
        [TraitId.KissRite5]       = (HcPlusL,  "祝福の儀の総量を吸う量の5倍に固定し、残った敵で等分する札（第206期）"),
        // 第207期: 継ぎ当てのツギ。分類を同じコミットで足す（第132期 段0-a）。
        [TraitId.Plank]           = (HcPlusL,  "手番で破片が最も薄い味方に板（最も強い敵の一撃ぶんの破片）を貼る"),
        [TraitId.PlankTinder]     = (HcMinusL, "板の印を持つ味方に付く燃焼の残りターンを倍にする、だけの札（第207期）"),
        [TraitId.Scrap]           = (HcPlusL,  "砕けた破片と倒れた駒を拾って次の板に上乗せする札（第207期）"),
        [TraitId.PlankRebound]    = (HcPlusL,  "板が敵に砕かれると、砕けた量だけその敵へ返す札（第208期）"),
        [TraitId.PlankScorch]     = (HcMinusL, "板の印を持つ味方が受ける燃焼の刻みを倍にする、だけの札（第208期）"),
        [TraitId.SharerArmored]   = (HcPlusL,  "分かちを殴られた味方の破片の段の後ろへ移す札（第208期）"),
        [TraitId.PlankThick]      = (HcPlusL,  "反射に残った板の厚さの半分を足す札（第209期）"),
        [TraitId.PlankThick25]    = (HcPlusL,  "反射に残った板の厚さの 25% を足す札（第209期・対照・保持者 0 枚）"),
        [TraitId.PlankThick100]   = (HcPlusL,  "反射に残った板の厚さをそのまま足す札（第209期・対照・保持者 0 枚）"),
        [TraitId.KissSteal]       = (HcBothL,  "吸った敵の攻撃力の上げ下げも受け取った味方へ移す札（弱体も強化も）"),
        [TraitId.LastStandHold]   = (HcBothL,  "味方が 0 体になると盾を捨てて剣を抜く（受け止めた刃の1割を力に変え ×2・薙ぎ、斬り返し、相打ちで勝つ）のと、構え直せなくなるのが1つの札"),
        [TraitId.LastStandHoldOldScar]    = (HcBothL, "第199期の剣の段で、傷を身に受けた分だけ数える版。対照で保持者 0 枚"),
        [TraitId.LastStandHoldNoStock]    = (HcBothL, "第199期の剣の段で、抜いた瞬間に在庫を捨てる版。対照で保持者 0 枚"),
        [TraitId.LastStandHoldMutualLoss] = (HcBothL, "第199期の剣の段で、相打ちを負けとする版。対照で保持者 0 枚"),
        [TraitId.LastStandScar]   = (HcPlusL,  "味方が 0 体になると盾を捨てて剣を抜く（受け流しを失い、庇った傷の1割を攻撃力に足して ×2・薙ぎで振り、殴られれば斬り返す）"),
        [TraitId.LastStand]       = (HcPlusL,  "剣の段の剣の版（傷を力に換えない）。対照で保持者 0 枚"),
        [TraitId.LastStandPlain]  = (HcPlusL,  "剣の段の返しなしの版。対照で保持者 0 枚"),
        [TraitId.LastStandShield] = (HcPlusL,  "盾剣の版（受け流しは今のまま・×1・単体で振る）。参考で保持者 0 枚"),
    };

    // ---- `Traits.cs` の enum のブロックを走査して既定を引く（**空なら止める**・第117期）--------
    string? hcTraitsPath = null;
    {
        string dir0 = Directory.GetCurrentDirectory();
        for (int up = 0; up < 6 && hcTraitsPath == null; up++)
        {
            string cand = Path.Combine(dir0, "BattleCore", "Traits.cs");
            if (File.Exists(cand)) hcTraitsPath = cand;
            else { var pdir = Directory.GetParent(dir0); if (pdir == null) break; dir0 = pdir.FullName; }
        }
    }
    var hcBlockOf = new Dictionary<string, string>(StringComparer.Ordinal);
    var hcCommentOf = new Dictionary<string, string>(StringComparer.Ordinal);
    int hcScanEnum = 0, hcScanBlock = 0;
    if (hcTraitsPath != null)
    {
        var tlines = File.ReadAllLines(hcTraitsPath);
        int tstart = Array.FindIndex(tlines, l => l.Contains("enum TraitId"));
        string tblock = "（先頭）", tlast = "";
        for (int i = tstart + 1; tstart >= 0 && i < tlines.Length; i++)
        {
            string t = tlines[i].Trim();
            if (t.StartsWith("}")) break;
            if (t.StartsWith("// ---")) { tblock = t.Trim('/', ' ', '-'); hcScanBlock++; continue; }
            if (t.StartsWith("//")) { if (tlast != "") hcCommentOf[tlast] += " " + t; continue; }
            var m = System.Text.RegularExpressions.Regex.Match(t, "^([A-Za-z][A-Za-z0-9]*)[ ]*,?[ ]*(//.*)?$");
            if (!m.Success) continue;
            tlast = m.Groups[1].Value;
            hcBlockOf[tlast] = tblock;
            hcCommentOf[tlast] = m.Groups[2].Value;
            hcScanEnum++;
        }
    }
    if (hcScanEnum == 0)
    {
        Console.WriteLine("**`BattleCore/Traits.cs` の enum を1つも走査できなかった。分類を実装から引けないので止める**（第117期）。");
        Console.WriteLine($"（作業ディレクトリ {Directory.GetCurrentDirectory()}）");
        return;
    }
    int HcBlockDefault(TraitId t)
    {
        string n = t.ToString();
        string b = hcBlockOf.TryGetValue(n, out string? bb) ? bb : "";
        string c = hcCommentOf.TryGetValue(n, out string? cc) ? cc : "";
        if (c.Contains("表と裏") || c.Contains("同上")) return HcBothL;
        if (b.Contains("表と裏")) return HcBothL;
        if (b.Contains("マイナス側")) return HcMinusL;
        if (b.Contains("プラス側")) return HcPlusL;
        return -1;                                    // 札・盤面ルール・器具は既定を持たない
    }

    // ---- 数値のマイナス（(iii)）は `MinusText` の語で拾う。**語と件数を必ず出す** ------------------
    var hcNumWord = new (string Word, int Stat)[]
    {
        ("攻撃力がほぼ無い", 0), ("火力もほぼ無い", 0), ("攻撃力もほぼ無い", 0),
        ("自分の火力はほぼ無い", 0), ("自分の火力はほぼ無く", 0), ("素の攻撃力はほぼ無い", 0),
        ("自分では何もできない", 0), ("ほぼ無力", 0),
        ("本体は脆く", 1),
        ("鈍重", 2),
    };
    string[] hcStatName = { "攻", "HP", "速" };
    double HcMedianI(IEnumerable<int> xs)
    {
        var a = xs.OrderBy(x => x).ToArray();
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2.0;
    }
    int[] hcMedStat =
    {
        (int)Math.Round(HcMedianI(hcRoster.Select(d => d.Attack))),
        (int)Math.Round(HcMedianI(hcRoster.Select(d => d.MaxHp))),
        (int)Math.Round(HcMedianI(hcRoster.Select(d => d.Speed))),
    };
    var hcNumHit = new List<(int Stat, string Word)>[hcRN];
    var hcNumWordHit = new List<string>[hcRN];        // 語だけ（下限で落ちたものも記録する）
    for (int u = 0; u < hcRN; u++)
    {
        hcNumHit[u] = new List<(int, string)>();
        hcNumWordHit[u] = new List<string>();
        UnitDef d = hcRoster[u];
        foreach (var (w, st) in hcNumWord)
        {
            if (!d.MinusText.Contains(w)) continue;
            hcNumWordHit[u].Add(w);
            int cur = st == 0 ? d.Attack : st == 1 ? d.MaxHp : d.Speed;
            if (cur < hcMedStat[st] && !hcNumHit[u].Any(h => h.Stat == st)) hcNumHit[u].Add((st, w));
        }
    }
    int hcNumHitN = Enumerable.Range(0, hcRN).Count(u => hcNumHit[u].Count > 0);
    int hcNumWordN = Enumerable.Range(0, hcRN).Count(u => hcNumWordHit[u].Count > 0);

    // ---- yP / yM の定義（**本物と同じなら同じ参照を返す**）----------------------------------------
    var hcDropP = new List<TraitId>[hcRN];            // yP で外した札
    var hcDropM = new List<TraitId>[hcRN];            // yM で外した札
    var hcPlusDef = new UnitDef[hcRN];
    var hcMinusDef = new UnitDef[hcRN];
    var hcUnlabeled = new List<TraitId>();
    for (int u = 0; u < hcRN; u++)
    {
        UnitDef d = hcRoster[u];
        foreach (TraitId t in d.Traits) if (!hcLabel.ContainsKey(t) && !hcUnlabeled.Contains(t)) hcUnlabeled.Add(t);
        hcDropP[u] = d.Traits.Where(t => hcLabel.TryGetValue(t, out var L) && L.L == HcMinusL).ToList();
        hcDropM[u] = d.Traits.Where(t => hcLabel.TryGetValue(t, out var L) && L.L == HcPlusL).ToList();
        int atk = d.Attack, hp = d.MaxHp, spd = d.Speed;
        foreach (var (st, _) in hcNumHit[u])
        {
            if (st == 0) atk = Math.Max(atk, hcMedStat[0]);
            if (st == 1) hp = Math.Max(hp, hcMedStat[1]);
            if (st == 2) spd = Math.Max(spd, hcMedStat[2]);
        }
        hcPlusDef[u] = hcDropP[u].Count == 0 && atk == d.Attack && hp == d.MaxHp && spd == d.Speed
            ? d
            : new UnitDef
            {
                Id = d.Id + "_plus", Name = d.Name + "（プラスのみ）", MaxHp = hp, Attack = atk, Speed = spd,
                Traits = d.Traits.Where(t => !hcDropP[u].Contains(t)).ToArray(),
                Pattern = d.Pattern, Actions = d.Actions
            };
        hcMinusDef[u] = hcDropM[u].Count == 0
            ? d
            : new UnitDef
            {
                Id = d.Id + "_minus", Name = d.Name + "（マイナスのみ）", MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
                Traits = d.Traits.Where(t => !hcDropM[u].Contains(t)).ToArray(),
                Pattern = d.Pattern, Actions = d.Actions
            };
    }
    if (hcUnlabeled.Count > 0)
    {
        Console.WriteLine($"**ロスターの札に分類の無いものがある: {string.Join("・", hcUnlabeled)}。止める**（第117期）。");
        return;
    }
    // 駒の型: (i) 外せる札がある / (iii) 数値が引き上がる / (ii) どちらも無い＝**yP は本物と同じ参照**
    string HcTypeOf(int u)
    {
        var v = new List<string>();
        if (hcDropP[u].Count > 0) v.Add("(i)");
        if (hcNumHit[u].Count > 0) v.Add("(iii)");
        if (v.Count == 0) v.Add("(ii)");
        return string.Join("＋", v);
    }
    bool HcIsII(int u) => ReferenceEquals(hcPlusDef[u], hcRoster[u]);

    // ---- 第78期の器具（入口 / 発火口 / キー）と第80期の分類（供給→読み）------------------------------
    var hcKeyOf = hcRoster.Select(TraitKeyMap.KeysOf).ToArray();
    var hcHook = hcRoster.Select(TraitHookMap.HooksOf).ToArray();
    var hcEntry = hcRoster.Select(d => TraitEntryMap.EntriesOf(d, withFoe: false)).ToArray();
    var hcRead = hcRoster.Select(d => d.Traits.SelectMany(t => TraitEntryMap.Reads.TryGetValue(t, out var r) ? r : Array.Empty<(int Key, TraitEntryMap.Where W)>()).Distinct().ToArray()).ToArray();
    var hcSup = hcRoster.Select(d => d.Traits.SelectMany(t => TraitEntryMap.Supplies.TryGetValue(t, out var sq) ? sq : Array.Empty<(int Key, TraitEntryMap.Where W)>()).Distinct().ToArray()).ToArray();

    bool HcFeeds((int Key, TraitEntryMap.Where W) su, (int Key, TraitEntryMap.Where W) r)
    {
        if (su.Key != r.Key) return false;
        return r.W switch
        {
            TraitEntryMap.Where.Any => true,
            TraitEntryMap.Where.Self => su.W is TraitEntryMap.Where.Ally or TraitEntryMap.Where.Any,
            TraitEntryMap.Where.Ally => su.W is TraitEntryMap.Where.Self or TraitEntryMap.Where.Ally or TraitEntryMap.Where.Any,
            TraitEntryMap.Where.Foe => su.W is TraitEntryMap.Where.Foe or TraitEntryMap.Where.Any,
            _ => false,
        };
    }
    bool HcSupplies(int a, int b) => hcSup[a].Any(su => hcRead[b].Any(r => HcFeeds(su, r)));
    string[] hcClassName = { "供給→読み", "読み→読み", "供給→供給", "キーだけ共有", "共有無し" };
    int HcClass(int a, int b)
    {
        if (HcSupplies(a, b) || HcSupplies(b, a)) return 0;
        if (hcRead[a].Select(r => r.Key).Intersect(hcRead[b].Select(r => r.Key)).Any()) return 1;
        if (hcSup[a].Select(su => su.Key).Intersect(hcSup[b].Select(su => su.Key)).Any()) return 2;
        if (hcKeyOf[a].Intersect(hcKeyOf[b]).Any()) return 3;
        return 4;
    }

    // 拒否権3: **その駒が唯一の書き手であるキー**（`TraitEntryMap.Supplies` で判定。**戦闘0回で出る**）
    var hcSupCnt = new int[hcNK];
    for (int u = 0; u < hcRN; u++) foreach (int k in hcSup[u].Select(s => s.Key).Distinct()) hcSupCnt[k]++;
    int[] HcSoleKeys(int u) => hcSup[u].Select(s => s.Key).Distinct().Where(k => hcSupCnt[k] == 1).OrderBy(x => x).ToArray();

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜81期と同一。`Stages` は書き換えない）------------------------
    var hcWeakCache = new Dictionary<string, UnitDef>();
    UnitDef HcWeakOf(UnitDef d)
    {
        if (hcWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * HcWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        hcWeakCache[d.Id] = w;
        return w;
    }
    var hcWeak = hcStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = HcWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    // 素体（同数値・特性なし）。**51 体ぶんを1度だけ作って使い回す**（第81期の作法）
    UnitDef HcPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var hcPlainMap = hcRoster.ToDictionary(d => d.Id, HcPlain);

    // 埋め草3枚（残り 49 体から規則 P）。**第81期 `PgFill` の写し**
    UnitDef[] HcFill(UnitDef[] pool, int strong0, int seed)
    {
        int rn = pool.Length;
        var rng = new Random(seed);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        int remain = rn, strong = strong0;
        var picked = new UnitDef[3];
        for (int r = 0; r < 3; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = pool[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= HcStrong) strong++;
            int px = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { px = t; break; }
            (idx[px], idx[remain - 1]) = (idx[remain - 1], idx[px]);
            remain--;
        }
        return picked;
    }
    // 台の seed（第81期 `PgSeed` の写し。**`Random` を2本並走させない**——第71期）
    int HcSeed(int pairIx, int draw)
    {
        ulong x = (ulong)HcTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    // 規則配置 H（第69期以来の写し）。**A と B の席は4版すべてで同じ**
    int[] HcSeats(UnitDef[] u)
    {
        var all5 = new[] { 0, 1, 2, 3, 4 };
        var front = all5.OrderByDescending(k => u[k].MaxHp)
                        .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        var rest = all5.Where(k => k != front[0] && k != front[1]).ToArray();
        var back = rest.OrderByDescending(k => u[k].Attack)
                       .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        int center = rest.Single(k => k != back[0] && k != back[1]);
        var r = new int[5];
        r[front[0]] = 0; r[front[1]] = 1; r[center] = 2; r[back[0]] = 3; r[back[1]] = 4;
        return r;
    }
    Formation HcForm(UnitDef[] u, int[] seats, int v)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (v & 1) != 0) || (k == 1 && (v & 2) != 0);
            f[seats[k]] = plain ? hcPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double HcRate(Formation f, int band)
    {
        double sum = 0;
        for (int wi = 1; wi < hcW; wi++)
        {
            int wins = 0;
            for (int seed = band; seed < band + HcM; seed++)
                if (BattleEngine.Run(f, hcWeak[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
            sum += wins * 100.0 / HcM;
        }
        return sum / (hcW - 1);
    }
    string HcP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double HcSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double HcMedian(IEnumerable<double> xs)
    {
        var a = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToArray();
        if (a.Length == 0) return double.NaN;
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2.0;
    }
    double HcCorr(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        int n = x.Count; if (n < 2) return double.NaN;
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < n; i++) { double a = x[i] - mx, b = y[i] - my; sxy += a * b; sxx += a * a; syy += b * b; }
        return sxx <= 0 || syy <= 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
    }

    var hcAllPairs = new List<(int A, int B)>();
    for (int a = 0; a < hcRN; a++) for (int b = a + 1; b < hcRN; b++) hcAllPairs.Add((a, b));
    int hcNP = hcAllPairs.Count;
    var hcPairIxOf = new int[hcRN, hcRN];
    for (int pi = 0; pi < hcNP; pi++) { hcPairIxOf[hcAllPairs[pi].A, hcAllPairs[pi].B] = pi; hcPairIxOf[hcAllPairs[pi].B, hcAllPairs[pi].A] = pi; }

    // ---- 理想台（`CompareBuilds()` の 61 行）------------------------------------------------------
    var hcAllRows = CompareBuilds();
    var hcPrimary = new HashSet<string>(Baseline.PrimaryRows);
    var hcRowUnits = hcAllRows.Select(r => r.F.Occupied()
                                            .Select(o => hcIdx.TryGetValue(o.Def.Id, out int u) ? u : -1)
                                            .Where(u => u >= 0).Distinct().ToArray()).ToArray();
    var hcInPrimary = new bool[hcRN];
    for (int i = 0; i < hcAllRows.Length; i++)
        if (hcPrimary.Contains(hcAllRows[i].Name)) foreach (int u in hcRowUnits[i]) hcInPrimary[u] = true;
    var hcRowCnt = new int[hcRN];
    for (int i = 0; i < hcAllRows.Length; i++) foreach (int u in hcRowUnits[i]) hcRowCnt[u]++;

    // 理想台の帰属（第69期の標準器具を 61 行に当てる・第2〜5波・seed 0..199）。**`ablate` ではない**
    (double[] Attr, double[] Raw, long Battles) HcIdeal()
    {
        double IdealRate(Formation f)
        {
            double sum = 0;
            for (int wi = 1; wi < hcW; wi++)
            {
                int wins = 0;
                for (int seed = 0; seed < HcIdealSeeds; seed++)
                    if (BattleEngine.Run(f, hcStages[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
                sum += wins * 100.0 / HcIdealSeeds;
            }
            return sum / (hcW - 1);
        }
        var jobs = new List<(int Row, int Slot, int U)>();
        for (int i = 0; i < hcAllRows.Length; i++)
            foreach ((int sl, UnitDef d) in hcAllRows[i].F.Occupied())
                if (hcIdx.TryGetValue(d.Id, out int u)) jobs.Add((i, sl, u));
        var full = new double[hcAllRows.Length];
        Parallel.For(0, hcAllRows.Length, i => full[i] = IdealRate(hcAllRows[i].F));
        var got = new double[jobs.Count];
        Parallel.For(0, jobs.Count, j =>
        {
            var f = new Formation();
            foreach ((int sl, UnitDef d) in hcAllRows[jobs[j].Row].F.Occupied())
                f[sl] = sl == jobs[j].Slot ? hcPlainMap[d.Id] : d;
            got[j] = IdealRate(f);
        });
        var attrSum = new double[hcRN]; var rawSum = new double[hcRN]; var cnt = new int[hcRN];
        for (int j = 0; j < jobs.Count; j++)
        {
            int u = jobs[j].U;
            attrSum[u] += full[jobs[j].Row] - got[j]; rawSum[u] += full[jobs[j].Row]; cnt[u]++;
        }
        var attr = new double[hcRN]; var raw = new double[hcRN];
        for (int u = 0; u < hcRN; u++)
        {
            attr[u] = cnt[u] == 0 ? double.NaN : attrSum[u] / cnt[u];
            raw[u] = cnt[u] == 0 ? double.NaN : rawSum[u] / cnt[u];
        }
        return (attr, raw, (long)(hcAllRows.Length + jobs.Count) * (hcW - 1) * HcIdealSeeds);
    }

    // ---- 1組ぶんの 2×2（**第81期 `PgMeasure` の写し。生の y も返す**）---------------------------
    (double[][] Y, int NT) HcMeasure(int pi)
    {
        (int a, int b) = hcAllPairs[pi];
        var pool = hcRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (hcRoster[a].Attack >= HcStrong ? 1 : 0) + (hcRoster[b].Attack >= HcStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < HcS * HcK && draw < HcDrawCap; draw++)
        {
            var f = HcFill(pool, strong0, HcSeed(hcPairIxOf[a, b], draw));
            var t = f.Select(d => hcIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = new[] { hcRoster[a], hcRoster[b], fills[t][0], fills[t][1], fills[t][2] };
            int[] seats = HcSeats(team);
            var y = new double[4];
            for (int v = 0; v < 4; v++) y[v] = HcRate(HcForm(team, seats, v), HcBand);
            ys[t] = y;
        }
        return (ys, fills.Count);
    }

    // =====================================================================================
    // check: `compare` 305 セルの突き合わせ（Q6）
    // =====================================================================================
    if (hcArg == "check")
    {
        var hcDoc = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines("docs/balance.md"))
        {
            if (!line.StartsWith("| ")) continue;
            var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
            if (cells.Length != hcW + 1 || !cells[1].EndsWith("%")) continue;
            var v = new double[hcW];
            bool ok = true;
            for (int w = 0; w < hcW; w++)
                if (!double.TryParse(cells[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
            if (ok) hcDoc[cells[0]] = v;
        }
        int mism = 0, cellsN = 0, missing = 0;
        var bad = new List<string>();
        Parallel.For(0, hcAllRows.Length, i =>
        {
            var v = new double[hcW];
            for (int w = 0; w < hcW; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < HcIdealSeeds; seed++)
                    if (BattleEngine.Run(hcAllRows[i].F, hcStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                v[w] = wins * 100.0 / HcIdealSeeds;
            }
            lock (bad)
            {
                if (!hcDoc.TryGetValue(hcAllRows[i].Name, out double[]? doc)) { missing++; return; }
                for (int w = 0; w < hcW; w++)
                {
                    cellsN++;
                    if (Math.Abs(doc[w] - v[w]) > 0.05) { mism++; bad.Add($"| {hcAllRows[i].Name} | 第{w + 1}波 | {doc[w]:F1} | {v[w]:F1} |"); }
                }
            }
        });
        Console.WriteLine("# 第82期 —— 受け入れ基準（Q6）: `compare` 305 セルの突き合わせ");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` {hcAllRows.Length} 行 × {hcW} 波 × seed 0..{HcIdealSeeds - 1} を回し直して `docs/balance.md` と突き合わせた: "
                          + $"**{cellsN} セル・ずれ {mism} 件**（`docs/balance.md` に無い行 {missing}）。");
        foreach (string l in bad) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"`UnitCatalog.All` は {UnitCatalog.All.Count} 体。この期は engine も `Traits.cs` も駒も波も触っていないので、ずれは 0 件でなければならない。");
        Console.WriteLine();
        // =================================================================================
        // 第119期の自己検査（(a)〜(e) と 必須4項目のうち機械で出る2本）
        // =================================================================================
        Console.WriteLine("## 第119期の自己検査");
        Console.WriteLine();
        // (a) `UnitCatalog.All` が 52 枚のまま・数値が `docs/units.md` と一致する
        string unitsPath = "";
        {
            string dq = Directory.GetCurrentDirectory();
            for (int up = 0; up < 6 && unitsPath == ""; up++)
            {
                string cand = Path.Combine(dq, "docs", "units.md");
                if (File.Exists(cand)) unitsPath = cand;
                else { var pq = Directory.GetParent(dq); if (pq == null) break; dq = pq.FullName; }
            }
        }
        int uScan = 0, uBad = 0;
        var uSeen = new Dictionary<string, (int Hp, int Atk, int Spd)>(StringComparer.Ordinal);
        if (unitsPath != "")
            foreach (string line in File.ReadAllLines(unitsPath))
            {
                if (!line.StartsWith("| **")) continue;
                var cs = line.Split('|');
                if (cs.Length < 6) continue;
                string nm = cs[1].Trim().Trim('*');
                if (!int.TryParse(cs[2].Trim(), out int hp) || !int.TryParse(cs[3].Trim(), out int at)
                    || !int.TryParse(cs[4].Trim(), out int sp)) continue;
                uSeen[nm] = (hp, at, sp); uScan++;
            }
        foreach (UnitDef d in UnitCatalog.All)
        {
            if (!uSeen.TryGetValue(d.Name, out var v)) { uBad++; Console.WriteLine($"- **`docs/units.md` に無い: {d.Name}**"); continue; }
            if (v.Hp != d.MaxHp || v.Atk != d.Attack || v.Spd != d.Speed)
            { uBad++; Console.WriteLine($"- **数値が違う: {d.Name} {v.Hp}/{v.Atk}/{v.Spd} 対 {d.MaxHp}/{d.Attack}/{d.Speed}**"); }
        }
        Console.WriteLine($"- **(a)** `UnitCatalog.All` は **{UnitCatalog.All.Count} 体**（52 のはず）。"
                          + $"`docs/units.md` から {uScan} 行を走査して数値を突き合わせ、**食い違い {uBad} 件**"
                          + "（この期は駒を1体も触っていないので 0 でなければならない）。");
        // (b) yP / yM で外す札の一覧
        Console.WriteLine($"- **(b)** yP で外す札は **{Enumerable.Range(0, hcRN).Sum(u => hcDropP[u].Count)} 枚ぶん / {Enumerable.Range(0, hcRN).Count(u => hcDropP[u].Count > 0)} 体**"
                          + $"（{string.Join("・", Enumerable.Range(0, hcRN).Where(u => hcDropP[u].Count > 0).Select(u => hcName[u] + ":" + string.Join("+", hcDropP[u])))}）"
                          + "——`phase0` の表と**同じ配列から出している**ので定義上一致する。");
        Console.WriteLine($"  数値の引き上げは **{Enumerable.Range(0, hcRN).Count(u => hcNumHit[u].Count > 0)} 体**"
                          + $"（{string.Join("・", Enumerable.Range(0, hcRN).Where(u => hcNumHit[u].Count > 0).Select(u => hcName[u] + ":" + string.Join("+", hcNumHit[u].Select(h => hcStatName[h.Stat]))))}）。");
        // (c) (ii) の駒では yP == y11（**内容だけ同じ複製を作って実測でも 0 を示す**）
        {
            var iiS = Enumerable.Range(0, hcRN).Where(HcIsII).Take(6).ToArray();
            double worst = 0; int cells = 0;
            foreach (int u in iiS)
            {
                int v = u == 0 ? 1 : 0;
                int pi = hcPairIxOf[u, v];
                (int a, int b) = hcAllPairs[pi];
                var pool = hcRoster.Where((_, k) => k != a && k != b).ToArray();
                int strong0 = (hcRoster[a].Attack >= HcStrong ? 1 : 0) + (hcRoster[b].Attack >= HcStrong ? 1 : 0);
                var seen2 = new HashSet<(int, int, int)>();
                var fills2 = new List<UnitDef[]>();
                for (int draw = 0; fills2.Count < 4 && draw < HcDrawCap; draw++)
                {
                    var f = HcFill(pool, strong0, HcSeed(hcPairIxOf[a, b], draw));
                    var t3 = f.Select(d => hcIdx[d.Id]).OrderBy(x => x).ToArray();
                    if (seen2.Add((t3[0], t3[1], t3[2]))) fills2.Add(f);
                }
                UnitDef src = hcRoster[u];
                var copy = new UnitDef
                {
                    Id = src.Id, Name = src.Name, MaxHp = src.MaxHp, Attack = src.Attack, Speed = src.Speed,
                    Traits = src.Traits.ToArray(), Pattern = src.Pattern, Actions = src.Actions
                };
                foreach (UnitDef[] fl in fills2)
                {
                    var team = new[] { hcRoster[a], hcRoster[b], fl[0], fl[1], fl[2] };
                    int[] seats = HcSeats(team);
                    double y11 = HcRate(HcForm(team, seats, 0), HcBand);
                    double yp = HcRate(HcFormD(team, seats, u == a ? 0 : 1, copy), HcBand);
                    worst = Math.Max(worst, Math.Abs(y11 - yp)); cells++;
                }
            }
            Console.WriteLine($"- **(c)** (ii) の駒 {iiS.Length} 体 × {cells / Math.Max(1, iiS.Length)} 台で、"
                              + $"**内容だけ同じ複製を差し込んだ版と y11 の最大差 {worst:F10}pt**（{cells} セル）。"
                              + "本測定では**同じ参照を返して測らない**ので、ここが 0 であることが「測らなかったこと」の担保になる。");
        }
        // (d) 走査件数
        Console.WriteLine($"- **(d)** 走査件数: `Traits.cs` の enum 列挙子 **{hcScanEnum}** / ブロック **{hcScanBlock}**、"
                          + $"`docs/units.md` の行 **{uScan}**、第82期の表A **{(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "design", "PHASE82_CHECKUP.md")) ? "読める" : "この場所からは読めない")}**。"
                          + "**0 件なら止める**（第117期）。");
        // `ctx.PickOne` の箇所数
        {
            int po = 0; string coreDir = "";
            string dq = Directory.GetCurrentDirectory();
            for (int up = 0; up < 6 && coreDir == ""; up++)
            {
                string cand = Path.Combine(dq, "BattleCore");
                if (Directory.Exists(cand)) coreDir = cand;
                else { var pq = Directory.GetParent(dq); if (pq == null) break; dq = pq.FullName; }
            }
            if (coreDir != "")
                foreach (string f in Directory.GetFiles(coreDir, "*.cs"))
                    po += System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"PickOne\(").Count;
            Console.WriteLine($"- **必須4項目 (4)** `PickOne` は `BattleCore` に **{po} 箇所**（定義1つを含む数え方で第89期以来 26。この期は1つも足していない）。");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {hcSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // ideal: 理想台の帰属だけ
    // =====================================================================================
    if (hcArg == "ideal")
    {
        var (attr0, raw0, nb0) = HcIdeal();
        Console.WriteLine("# 第82期 —— 理想台の帰属（`CompareBuilds()` 61 行・素体差し替え）");
        Console.WriteLine();
        Console.WriteLine($"第2〜{hcW}波・seed 0..{HcIdealSeeds - 1}・**{nb0:N0} 戦**。");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | 在席行 | 理想台の帰属 | 在席行の平均勝率 |");
        Console.WriteLine("|--:|---|--:|--:|--:|");
        int rk0 = 0;
        foreach (int u in Enumerable.Range(0, hcRN).OrderByDescending(u => double.IsNaN(attr0[u]) ? double.NegativeInfinity : attr0[u]))
            Console.WriteLine($"| {++rk0} | {hcName[u]} | {hcRowCnt[u]} | {HcP2(attr0[u])} | {(double.IsNaN(raw0[u]) ? "—" : raw0[u].ToString("F1"))} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {hcSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // phase0: 紙の計算（**戦闘0回**）
    // =====================================================================================
    if (hcArg == "phase0")
    {
        Console.WriteLine("# 第82期 Phase 0 —— 3分の線を測る前に固定する（紙の計算）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `dotnet run --project BattleSim -c Release 0 checkup phase0`");
        Console.WriteLine();
        Console.WriteLine("## 1-1. 器具（指示書 §0-2 / §0-3）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 定義 | 出どころ |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| **単独の帰属**（横軸） | `y11 − y01`（相方が本物のまま、その駒だけを素体に）の台ごとの平均 | 第69期の標準器具 |");
        Console.WriteLine("| **相乗**（縦軸） | `y11 − y10 − y01 + y00` | 第81期の 2×2 |");
        Console.WriteLine("| 在席差 | — | **第80期の器具。この期では1度も使わない** |");
        Console.WriteLine();
        Console.WriteLine($"**2つの量は同じ台・同じ席・同じ戦闘 seed の4版から出る**（器具は1つ）。"
                          + $"1組あたり **{HcS * HcK} 台**、駒1体あたり **{hcRN - 1} 組 × {HcS * HcK} 台 = {(hcRN - 1) * HcS * HcK:N0} 台**。");
        Console.WriteLine();
        Console.WriteLine("**`y10 − y00` も同じ4版から出るが、横軸には使わない**——相方が素体の版なので"
                          + "「5枚とも本物の編成から1枚だけ素体に落とす」という第69期の器具の形から外れる。"
                          + "**`(y11 − y01) − (y10 − y00)` がそのまま相乗**なので、"
                          + "軸に両方の平均を使うと**横軸に相乗が半分混ざる**（§0-3 の「器具を混ぜない」に触れる）。");
        Console.WriteLine();
        Console.WriteLine("## 1-2. 線（**測る前に固定した**。指示書 §2-1）");
        Console.WriteLine();
        Console.WriteLine("| 線 | 値 | 根拠 |");
        Console.WriteLine("|---|--:|---|");
        Console.WriteLine($"| 単独の帰属 | **+{HcSoloLine:F1}pt** | 第62期以来の帰属の閾値（16 期にわたって採否に使ってきた数字をそのまま） |");
        Console.WriteLine($"| 最良の相方との相乗 | **+{HcSynLine:F1}pt** | 第46期の配置の採否閾値（席を1つ動かして 5.0pt 上がるなら動かす価値がある） |");
        Console.WriteLine("| 有意 | \\|相乗\\| > 2 × SE | 第81期 `PgSig`。**2系列の符号一致は要求しない**——それは Q3 が測る量なので軸に入れない |");
        Console.WriteLine($"| 天井 | y00 > {HcCeil:F0}% の台が {HcCeilShare:F0}% 超 | 指示書 §1-4（第64期の「測れない席」と同じ扱い） |");
        Console.WriteLine();
        Console.WriteLine("    残す     : 単独 ≥ +1.5（相乗を問わない）");
        Console.WriteLine("    転生     : 単独 < +1.5 かつ 最良の相乗 ≥ +5.0");
        Console.WriteLine("    差し替え : 単独 < +1.5 かつ 最良の相乗 < +5.0");
        Console.WriteLine();
        Console.WriteLine("## 1-3. 拒否権（§2-3）の**紙で出る2本**");
        Console.WriteLine();
        int nPrim = Enumerable.Range(0, hcRN).Count(u => hcInPrimary[u]);
        var sole = Enumerable.Range(0, hcRN).Where(u => HcSoleKeys(u).Length > 0).ToArray();
        Console.WriteLine($"- **拒否権2（主判定 {Baseline.PrimaryRows.Length} 行に含まれる）: {nPrim} / {hcRN} 体**"
                          + $"（残り {hcRN - nPrim} 体は主判定に一度も出ない）");
        Console.WriteLine($"- **拒否権3（唯一の書き手であるキーを持つ）: {sole.Length} / {hcRN} 体**");
        Console.WriteLine();
        if (sole.Length > 0)
        {
            Console.WriteLine("| 駒 | 唯一の書き手であるキー | 主判定 |");
            Console.WriteLine("|---|---|:-:|");
            foreach (int u in sole)
                Console.WriteLine($"| {hcName[u]} | {string.Join("・", HcSoleKeys(u).Select(k => UnitTally.CarryKeys[k]))} | {(hcInPrimary[u] ? "○" : "—")} |");
            Console.WriteLine();
        }
        Console.WriteLine("キーごとの書き手の数（`TraitEntryMap.Supplies`・**駒で数える**）:");
        Console.WriteLine();
        Console.WriteLine("| キー | " + string.Join(" | ", UnitTally.CarryKeys) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", hcNK)));
        Console.WriteLine("| 書き手 | " + string.Join(" | ", hcSupCnt) + " |");
        Console.WriteLine();
        Console.WriteLine("## 1-4. 入口 / 発火口 / キー の分布（第78期の器具・**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 0 | 1 | 2 | 3 | 4+ | 平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        void HcDist(string nm, Func<int, int> f)
        {
            var c = new int[5];
            for (int u = 0; u < hcRN; u++) c[Math.Min(4, f(u))]++;
            Console.WriteLine($"| {nm} | {c[0]} | {c[1]} | {c[2]} | {c[3]} | {c[4]} | {Enumerable.Range(0, hcRN).Average(u => (double)f(u)):F2} |");
        }
        HcDist("入口（味方）", u => hcEntry[u].Length);
        HcDist("発火口", u => hcHook[u].Length);
        HcDist("キー", u => hcKeyOf[u].Length);
        HcDist("特性の数", u => hcRoster[u].Traits.Count);
        Console.WriteLine();
        Console.WriteLine("## 1-5. 予算");
        Console.WriteLine();
        long b2 = (long)hcNP * 4 * HcK * HcS * (hcW - 1) * HcM;
        Console.WriteLine($"- 2×2: {hcNP:N0} 組 × 4 版 × {HcS * HcK} 台 × {hcW - 1} 波 × seed {HcM} 本 = **{b2:N0} 戦**（第81期と同じ）");
        Console.WriteLine($"- 理想台: ({hcAllRows.Length} 行 + 在席枠) × {hcW - 1} 波 × seed {HcIdealSeeds} 本");
        Console.WriteLine("- **第81期と同じく `run <skip> <take>` に割る**（1回で全部回すと OS にメモリ不足で落とされる）");
        Console.WriteLine();
        Console.WriteLine("## 予測（**測る前に書く**・指示書 §1）");
        Console.WriteLine();
        Console.WriteLine("| # | 量 | 予測 |");
        Console.WriteLine("|--:|---|---|");
        Console.WriteLine("| P1 | 差し替え候補の数 | **3〜8 体**。0 なら線が緩すぎ、15 超なら厳しすぎる |");
        Console.WriteLine("| P2 | 転生候補の数 | **10 体前後**（第81期で「正の相乗を持たない駒は 0 体」なので単独が弱い駒の多くは転生側） |");
        Console.WriteLine("| P3 | ササ | **転生側**（散開は隣が空いた席でのみ効く＝条件が盤面に潰されている） |");
        Console.WriteLine("| P4 | 体の大きい駒 | カド・ゴルム・ドルガ・ボルグは**残す**。ただし負の相乗の相手数が多い |");
        Console.WriteLine("| P5 | 台の一致 | ドラフト台と理想台で群が割れる駒が **5 体以上** |");
        Console.WriteLine("| P6 | 天井で測れない駒 | **体の大きい駒に集中する**（y00 95% 超は体2枚の台で起きる） |");
        Console.WriteLine();
        // =================================================================================
        // 第119期 Phase 0 —— マイナスの分類・中央値・screen・予測（**戦闘0回**）
        // =================================================================================
        Console.WriteLine("---");
        Console.WriteLine();
        Console.WriteLine("# 第119期 Phase 0 —— マイナスを (i)(ii)(iii) に分ける（紙の計算）");
        Console.WriteLine();
        Console.WriteLine($"走査: `BattleCore/Traits.cs` の enum から **列挙子 {hcScanEnum} 件 / ブロック {hcScanBlock} 件**"
                          + $"（**0 件なら止める**——第117期。実装から引く表は、引けなかったときに「該当なし」と区別がつかない）。"
                          + $"ロスターは **{hcRN} 枚**。");
        Console.WriteLine();
        Console.WriteLine("## 1-1. 分類の定義");
        Console.WriteLine();
        Console.WriteLine("| 種 | 定義 | yP（プラスのみ）の作り方 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| **(i)** | マイナスが独立した札 | `Traits` からその札を抜く |");
        Console.WriteLine("| **(ii)** | 1つの札が両義（プラスとマイナスが同じ動作） | **外せない。yP は本物と同じ参照** |");
        Console.WriteLine("| **(iii)** | マイナスが数値 | その数値をロスターの中央値へ引き上げる |");
        Console.WriteLine();
        Console.WriteLine($"中央値（**実装から数えた**）: 攻 **{hcMedStat[0]}** / HP **{hcMedStat[1]}** / 速 **{hcMedStat[2]}**。");
        Console.WriteLine();
        Console.WriteLine("## 1-2. 52 枚の分類");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | 型 | 外す札（yP） | 引き上げ | 外す札（yM） | 判断の理由 |");
        Console.WriteLine("|--:|---|---|---|---|---|---|");
        for (int u = 0; u < hcRN; u++)
        {
            string dp = hcDropP[u].Count == 0 ? "—" : string.Join("・", hcDropP[u]);
            string dn = hcNumHit[u].Count == 0 ? "—" : string.Join("・", hcNumHit[u].Select(h =>
                $"{hcStatName[h.Stat]} {(h.Stat == 0 ? hcRoster[u].Attack : h.Stat == 1 ? hcRoster[u].MaxHp : hcRoster[u].Speed)} → {hcMedStat[h.Stat]}"));
            string dm = hcDropM[u].Count == 0 ? "—" : string.Join("・", hcDropM[u]);
            string why = hcDropP[u].Count > 0 ? hcLabel[hcDropP[u][0]].Why
                : hcNumHit[u].Count > 0 ? $"`MinusText` の「{hcNumHit[u][0].Word}」"
                : "外せる札が無く数値のマイナスも無い＝1つの札が両義";
            Console.WriteLine($"| {u + 1} | {hcName[u]} | {HcTypeOf(u)} | {dp} | {dn} | {dm} | {why} |");
        }
        Console.WriteLine();
        int p0i = Enumerable.Range(0, hcRN).Count(u => hcDropP[u].Count > 0);
        int p0iii = Enumerable.Range(0, hcRN).Count(u => hcNumHit[u].Count > 0);
        int p0ii = Enumerable.Range(0, hcRN).Count(HcIsII);
        Console.WriteLine($"**(i) を含む {p0i} 体 / (iii) を含む {p0iii} 体 / (ii)（どちらも無い）{p0ii} 体**"
                          + $"。(i) と (iii) を**両方**持つ駒は {Enumerable.Range(0, hcRN).Count(u => hcDropP[u].Count > 0 && hcNumHit[u].Count > 0)} 体"
                          + $"（0 なら3つの型はちょうど分割になり、合計は {hcRN} に一致する）。");
        Console.WriteLine();
        Console.WriteLine($"`MinusText` の語に当たったのは {hcNumWordN} 体、そのうち中央値未満で実際に引き上がるのは **{p0iii} 体**"
                          + "（語に当たっても既に中央値以上なら引き上げない）。");
        Console.WriteLine();
        Console.WriteLine("## 1-3. 札ごとの分類が enum のブロックと食い違うところ（**判断が入った箇所**）");
        Console.WriteLine();
        Console.WriteLine("| 札 | この期 | enum の既定 | 理由 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (TraitId t in hcRoster.SelectMany(d => d.Traits).Distinct().OrderBy(t => t.ToString(), StringComparer.Ordinal))
        {
            int def = HcBlockDefault(t);
            if (def == hcLabel[t].L) continue;
            Console.WriteLine($"| `{t}` | {hcLabelName[hcLabel[t].L]} | {(def < 0 ? "既定なし" : hcLabelName[def])} | {hcLabel[t].Why} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 1-4. ヴェルが測れる台か（指示書 §1 の 4）");
        Console.WriteLine();
        int velP0 = Array.FindIndex(hcRoster, d => d.Id == "vel");
        Console.WriteLine($"第118期の土台は攻撃役に固定した4台で、**味方が倒れないので蘇生の価値が測れなかった**。"
                          + $"`checkup` のドラフト台は**埋め草3枚を残り {hcRN - 2} 体から無作為に引く**ので、"
                          + $"同じ穴は構造的に踏まない。**第82期の実測でもヴェルは 単独 +12.68・天井 5.4% / 床 46.4% で測れている**"
                          + $"（この期の値は表C と Q5 に出す）。ヴェルの札は "
                          + $"{(velP0 < 0 ? "—" : string.Join("・", hcRoster[velP0].Traits.Select(t => $"`{t}`（{hcLabelName[hcLabel[t].L]}）")))}。");
        Console.WriteLine();
        Console.WriteLine("## 1-5. 情報帯の screen（指示書 §1 の 6・**第118期の穴の 8 例目にしない**）");
        Console.WriteLine();
        Console.WriteLine($"**天井（y00 > {HcCeil:F0}%）だけでなく床（y00 = 0%）も screen する**——第82期は天井しか見ていないが、"
                          + "実測では床のほうがずっと厚い（中央値 40% 台）。**判定式が読むセルが情報帯に入るかを先に数える**"
                          + $"（線は「天井% ≤ {HcCeilShare:F0} かつ 床% ≤ {HcCeilShare:F0}」。落ちた駒は象限に置かない）。");
        Console.WriteLine();
        Console.WriteLine("## 1-6. 予算");
        Console.WriteLine();
        int extra = Enumerable.Range(0, hcRN).Count(u => !HcIsII(u)) ;
        Console.WriteLine($"- `runp`: {hcNP:N0} 組 × (4 版 ＋ **本物と違う版だけ**) × {HcS * HcK} 台 × {hcW - 1} 波 × seed {HcM} 本");
        Console.WriteLine($"- yP が本物と違う駒は {extra} / {hcRN} 体、yM が本物と違う駒は {Enumerable.Range(0, hcRN).Count(u => !ReferenceEquals(hcMinusDef[u], hcRoster[u]))} / {hcRN} 体"
                          + "——**同じ参照になる版は測らずに y11 を写す**ので、(ii) だけの組は 4 版で済む。");
        Console.WriteLine($"- `run51`: 第82期のロスター（51 枚）で 4 版。**Q1 はこれと第82期の表A を突き合わせる**");
        Console.WriteLine();
        Console.WriteLine("## 予測（**測る前に書く**・指示書 §3-1）");
        Console.WriteLine();
        Console.WriteLine("| # | 予測 |");
        Console.WriteLine("|--:|---|");
        Console.WriteLine("| R1 | 「思想に合わない」象限は **3〜8 体**。第82期の差し替え候補6体と半分は重なるが一致はしない |");
        Console.WriteLine("| R2 | **(ii) は 10 体以上**（近隣加害型は構造的に (ii) になる） |");
        Console.WriteLine("| R3 | 「マイナスが実質プラス」象限に **1〜3 体**（バンの積み過ぎがこの形に近い） |");
        Console.WriteLine("| R4 | ドルガは4象限のどこにも綺麗に入らず**別扱い**になる（第82期は天井 73.6%） |");
        Console.WriteLine("| R5 | **ムドはプラス値が高く出る**（攻を中央値へ引き上げると怒りの伸びがそのまま乗る） |");
        Console.WriteLine();
        Console.WriteLine($"所要 {hcSw.Elapsed.TotalSeconds:F1} 秒（**戦闘 0 回**）。");
        return;
    }

    // =====================================================================================
    // run: 組の [skip, skip+take) だけを測って TSV で吐く（**`pairs2 run` と前半が完全に同じ形式**）
    // =====================================================================================
    // ---- 第119期: 1組ぶんの 8 版（2×2 の4版 ＋ yP(A) / yP(B) / yM(A) / yM(B)）--------------------
    // **本物と同じ参照になる版は測らずに y11 を写す**（(ii) の駒）。
    // 席は4版と同じ（`HcSeats` は本物の5枚から引く）ので、差し替えても隊列は1ビットも動かない。
    Formation HcFormD(UnitDef[] u, int[] seats, int k, UnitDef d)
    {
        var f = new Formation();
        for (int j = 0; j < 5; j++) f[seats[j]] = j == k ? d : u[j];
        return f;
    }
    (double[][] Y, int NT) HcMeasureX(int pi)
    {
        (int a, int b) = hcAllPairs[pi];
        var pool = hcRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (hcRoster[a].Attack >= HcStrong ? 1 : 0) + (hcRoster[b].Attack >= HcStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < HcS * HcK && draw < HcDrawCap; draw++)
        {
            var f = HcFill(pool, strong0, HcSeed(hcPairIxOf[a, b], draw));
            var t = f.Select(d => hcIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = new[] { hcRoster[a], hcRoster[b], fills[t][0], fills[t][1], fills[t][2] };
            int[] seats = HcSeats(team);
            var y = new double[8];
            for (int v = 0; v < 4; v++) y[v] = HcRate(HcForm(team, seats, v), HcBand);
            y[4] = ReferenceEquals(hcPlusDef[a], hcRoster[a]) ? y[0] : HcRate(HcFormD(team, seats, 0, hcPlusDef[a]), HcBand);
            y[5] = ReferenceEquals(hcPlusDef[b], hcRoster[b]) ? y[0] : HcRate(HcFormD(team, seats, 1, hcPlusDef[b]), HcBand);
            y[6] = ReferenceEquals(hcMinusDef[a], hcRoster[a]) ? y[0] : HcRate(HcFormD(team, seats, 0, hcMinusDef[a]), HcBand);
            y[7] = ReferenceEquals(hcMinusDef[b], hcRoster[b]) ? y[0] : HcRate(HcFormD(team, seats, 1, hcMinusDef[b]), HcBand);
            ys[t] = y;
        }
        return (ys, fills.Count);
    }

    // =====================================================================================
    // runp: 第119期の 8 版を TSV で吐く（**先頭は `run` と完全に同じ形式**・末尾に 4×台数 を足しただけ）
    // =====================================================================================
    if (hcArg == "runp")
    {
        var invP = System.Globalization.CultureInfo.InvariantCulture;
        int skipP = args.Length > 3 ? int.Parse(args[3]) : 0;
        int takeP = args.Length > 4 ? int.Parse(args[4]) : hcNP;
        skipP = Math.Clamp(skipP, 0, hcNP);
        takeP = Math.Clamp(takeP, 0, hcNP - skipP);
        var rowsP = new string[takeP];
        int doneP = 0;
        Console.Error.Write($"checkup runp {skipP} {takeP}: ");
        Parallel.For(0, takeP, j =>
        {
            var (ys, nt) = HcMeasureX(skipP + j);
            var vsum = new double[4];
            int f00 = 0, f11 = 0;
            for (int t = 0; t < nt; t++)
            {
                for (int v = 0; v < 4; v++) vsum[v] += ys[t][v];
                if (ys[t][3] <= 0.0 || ys[t][3] >= 100.0) f00++;
                if (ys[t][0] <= 0.0 || ys[t][0] >= 100.0) f11++;
            }
            var sb = new System.Text.StringBuilder();
            sb.Append(hcAllPairs[skipP + j].A).Append('\t').Append(hcAllPairs[skipP + j].B).Append('\t')
              .Append(nt).Append('\t').Append(f00).Append('\t').Append(f11);
            for (int v = 0; v < 4; v++) sb.Append('\t').Append((nt == 0 ? double.NaN : vsum[v] / nt).ToString("R", invP));
            for (int sx = 0; sx < HcS; sx++)
                for (int t = sx; t < nt; t += HcS)
                    sb.Append('\t').Append((ys[t][0] - ys[t][2] - ys[t][1] + ys[t][3]).ToString("R", invP));
            for (int t = 0; t < nt; t++)
                for (int v = 0; v < 4; v++) sb.Append('\t').Append(ys[t][v].ToString("R", invP));
            // **第119期に足した列**（台の順・yP(A) / yP(B) / yM(A) / yM(B)）
            for (int t = 0; t < nt; t++)
                for (int v = 4; v < 8; v++) sb.Append('\t').Append(ys[t][v].ToString("R", invP));
            rowsP[j] = sb.ToString();
            if (Interlocked.Increment(ref doneP) % 25 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rowsP) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {hcSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    if (hcArg == "run" || hcArg == "run51")
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int skip = args.Length > 3 ? int.Parse(args[3]) : 0;
        int take = args.Length > 4 ? int.Parse(args[4]) : hcNP;
        skip = Math.Clamp(skip, 0, hcNP);
        take = Math.Clamp(take, 0, hcNP - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"checkup run {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            var (ys, nt) = HcMeasure(skip + j);
            var vsum = new double[4];
            int f00 = 0, f11 = 0;
            for (int t = 0; t < nt; t++)
            {
                for (int v = 0; v < 4; v++) vsum[v] += ys[t][v];
                if (ys[t][3] <= 0.0 || ys[t][3] >= 100.0) f00++;
                if (ys[t][0] <= 0.0 || ys[t][0] >= 100.0) f11++;
            }
            var sb = new System.Text.StringBuilder();
            sb.Append(hcAllPairs[skip + j].A).Append('\t').Append(hcAllPairs[skip + j].B).Append('\t')
              .Append(nt).Append('\t').Append(f00).Append('\t').Append(f11);
            for (int v = 0; v < 4; v++) sb.Append('\t').Append((nt == 0 ? double.NaN : vsum[v] / nt).ToString("R", inv));
            // 相乗（系列ごと・台の順）——**`pairs2 run` と同じ並び**
            for (int sx = 0; sx < HcS; sx++)
                for (int t = sx; t < nt; t += HcS)
                    sb.Append('\t').Append((ys[t][0] - ys[t][2] - ys[t][1] + ys[t][3]).ToString("R", inv));
            // 生の y（台の順・y11 / y01 / y10 / y00）——**この期に足した列。`pairs2 tables` は読み飛ばす**
            for (int t = 0; t < nt; t++)
                for (int v = 0; v < 4; v++) sb.Append('\t').Append(ys[t][v].ToString("R", inv));
            rows[j] = sb.ToString();
            if (Interlocked.Increment(ref doneR) % 25 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {hcSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    if (hcArg != "tables")
    {
        Console.WriteLine("checkup: 引数は phase0 / run <skip> <take> / **runp <skip> <take>** / **run51 <skip> <take>** / tables <path> [<pairs2 の TSV>|-] [<run51 の TSV>] / ideal / check。");
        return;
    }

    // =====================================================================================
    // tables: 表A〜F・Q1〜Q6
    // =====================================================================================
    string hcPath = args.Length > 3 ? args[3] : "";
    string hcRefPath = args.Length > 4 && args[4] != "-" ? args[4] : "";
    string hcRefPath51 = args.Length > 5 && args[5] != "-" ? args[5] : "";   // **第119期**: `run51` の TSV
    if (hcPath == "" || !File.Exists(hcPath))
    {
        Console.WriteLine("checkup tables <`checkup run` の吐いた TSV を連結したファイル> [<`pairs2 run` の TSV>]");
        return;
    }
    var hcInv = System.Globalization.CultureInfo.InvariantCulture;

    var hcY = new double[hcNP][][];
    var hcNT = new int[hcNP];
    int hasX = 0;                                       // 第119期の列が付いていた組の数
    {
        var seenPair = new bool[hcNP];
        int got = 0;
        foreach (string line in File.ReadLines(hcPath))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int pi = hcPairIxOf[int.Parse(c[0]), int.Parse(c[1])];
            if (seenPair[pi]) continue;                 // シャードが重なっても取り違えない
            seenPair[pi] = true;
            int nt = int.Parse(c[2]);
            hcNT[pi] = nt;
            int at = 9 + nt;                            // 9 列 ＋ 相乗 nt 個 の後ろに生の y が並ぶ
            var ys = new double[nt][];
            for (int t = 0; t < nt; t++)
            {
                ys[t] = new double[8];                  // **第119期**: 後ろ4つは yP(A)/yP(B)/yM(A)/yM(B)
                for (int v = 0; v < 4; v++) ys[t][v] = double.Parse(c[at++], hcInv);
            }
            // 第119期の列が付いていれば読む。付いていない（第82期の `run` の TSV）なら y11 を写して
            // **プラス値 = 単独 になる**ので、表C は「読めていない」と書いて出さない。
            if (c.Length >= at + 4 * nt)
            {
                for (int t = 0; t < nt; t++)
                    for (int v = 4; v < 8; v++) ys[t][v] = double.Parse(c[at++], hcInv);
                hasX++;
            }
            else for (int t = 0; t < nt; t++) for (int v = 4; v < 8; v++) ys[t][v] = ys[t][0];
            hcY[pi] = ys;
            got++;
        }
        if (got != hcNP)
        {
            Console.WriteLine($"**シャードが足りない: {got} / {hcNP} 組。`checkup run` の全区間を連結してから `tables` に渡すこと。**");
            return;
        }
    }

    // ---- 組ごとの相乗（合算と系列）------------------------------------------------------------
    var hcSynAll = new double[hcNP];
    var hcSeAll = new double[hcNP];
    var hcSynS = new double[hcNP][];
    var hcSeS = new double[hcNP][];
    var hcSerial = new double[hcNP][];                  // 系列順に並べた相乗（Q1 の突き合わせ用）
    for (int pi = 0; pi < hcNP; pi++)
    {
        int nt = hcNT[pi];
        var all = new double[nt];
        for (int t = 0; t < nt; t++) all[t] = hcY[pi][t][0] - hcY[pi][t][2] - hcY[pi][t][1] + hcY[pi][t][3];
        hcSynAll[pi] = all.Average();
        hcSeAll[pi] = HcSd(all) / Math.Sqrt(nt);
        hcSynS[pi] = new double[HcS]; hcSeS[pi] = new double[HcS];
        var ser = new List<double>();
        for (int sx = 0; sx < HcS; sx++)
        {
            var xs = new List<double>();
            for (int t = sx; t < nt; t += HcS) xs.Add(all[t]);
            hcSynS[pi][sx] = xs.Average();
            hcSeS[pi][sx] = HcSd(xs) / Math.Sqrt(xs.Count);
            ser.AddRange(xs);
        }
        hcSerial[pi] = ser.ToArray();
    }
    bool HcPosP(int pi) => hcSynAll[pi] > 2 * hcSeAll[pi];
    bool HcNegP(int pi) => hcSynAll[pi] < -2 * hcSeAll[pi];
    bool HcPosS(int pi, int sx) => hcSynS[pi][sx] > 2 * hcSeS[pi][sx];

    // ---- 駒ごとの単独の帰属（`y11 − y01`）と天井の混入 --------------------------------------------
    var hcSoloS = new List<double>[hcRN][];
    for (int u = 0; u < hcRN; u++) { hcSoloS[u] = new List<double>[HcS]; for (int sx = 0; sx < HcS; sx++) hcSoloS[u][sx] = new List<double>(); }
    var hcCeilHit = new int[hcRN];
    var hcCeilAll = new int[hcRN];
    var hcFloorHit = new int[hcRN];
    for (int pi = 0; pi < hcNP; pi++)
    {
        (int a, int b) = hcAllPairs[pi];
        for (int t = 0; t < hcNT[pi]; t++)
        {
            double[] y = hcY[pi][t];
            int sx = t % HcS;
            hcSoloS[a][sx].Add(y[0] - y[1]);             // A だけを素体に（相方 B は本物）
            hcSoloS[b][sx].Add(y[0] - y[2]);
            bool ceil = y[3] > HcCeil, floor = y[3] <= 0.0;
            if (ceil) { hcCeilHit[a]++; hcCeilHit[b]++; }
            if (floor) { hcFloorHit[a]++; hcFloorHit[b]++; }
            hcCeilAll[a]++; hcCeilAll[b]++;
        }
    }
    var hcSolo = new double[hcRN];
    var hcSoloSe = new double[hcRN];
    var hcSoloSv = new double[hcRN][];
    var hcCeilPct = new double[hcRN];
    var hcFloorPct = new double[hcRN];
    for (int u = 0; u < hcRN; u++)
    {
        var all = hcSoloS[u][0].Concat(hcSoloS[u][1]).ToArray();
        hcSolo[u] = all.Average();
        hcSoloSe[u] = HcSd(all) / Math.Sqrt(all.Length);
        hcSoloSv[u] = new[] { hcSoloS[u][0].Average(), hcSoloS[u][1].Average() };
        hcCeilPct[u] = 100.0 * hcCeilHit[u] / hcCeilAll[u];
        hcFloorPct[u] = 100.0 * hcFloorHit[u] / hcCeilAll[u];
    }

    // ---- 相乗の集計（最良の相方 / 正の相手数 / 負の相手数 / 合計）---------------------------------
    var hcBest = new int[hcRN];
    var hcBestV = new double[hcRN];
    var hcPosN = new int[hcRN];
    var hcNegN = new int[hcRN];
    var hcSynSum = new double[hcRN];
    var hcBestS = new double[hcRN][];
    for (int u = 0; u < hcRN; u++)
    {
        hcBest[u] = -1; hcBestV[u] = double.NaN;
        hcBestS[u] = new[] { double.NaN, double.NaN };
        for (int v = 0; v < hcRN; v++)
        {
            if (v == u) continue;
            int pi = hcPairIxOf[u, v];
            hcSynSum[u] += hcSynAll[pi];
            if (HcPosP(pi))
            {
                hcPosN[u]++;
                if (double.IsNaN(hcBestV[u]) || hcSynAll[pi] > hcBestV[u]) { hcBestV[u] = hcSynAll[pi]; hcBest[u] = v; }
            }
            if (HcNegP(pi)) hcNegN[u]++;
            for (int sx = 0; sx < HcS; sx++)
                if (HcPosS(pi, sx) && (double.IsNaN(hcBestS[u][sx]) || hcSynS[pi][sx] > hcBestS[u][sx])) hcBestS[u][sx] = hcSynS[pi][sx];
        }
    }

    // ---- 3分（§2-1）----------------------------------------------------------------------------
    string[] hcGroupName = { "残す", "転生", "差し替え" };
    int HcGroup(double solo, double best)
        => solo >= HcSoloLine ? 0 : (!double.IsNaN(best) && best >= HcSynLine ? 1 : 2);
    var hcGrp = new int[hcRN];
    var hcGrpS = new int[hcRN][];
    for (int u = 0; u < hcRN; u++)
    {
        hcGrp[u] = HcGroup(hcSolo[u], hcBestV[u]);
        hcGrpS[u] = new[] { HcGroup(hcSoloSv[u][0], hcBestS[u][0]), HcGroup(hcSoloSv[u][1], hcBestS[u][1]) };
    }
    // 別扱い（§2-2）
    var hcSplitSign = Enumerable.Range(0, hcRN).Select(u => hcSoloSv[u][0] * hcSoloSv[u][1] < 0).ToArray();
    var hcNoMeasure = Enumerable.Range(0, hcRN).Select(u => hcCeilPct[u] > HcCeilShare).ToArray();
    var hcSpecial = Enumerable.Range(0, hcRN).Select(u => hcSplitSign[u] || hcNoMeasure[u]).ToArray();

    // ---- 理想台 ---------------------------------------------------------------------------------
    Console.Error.Write("理想台の帰属: ");
    var (hcIdealAttr, hcIdealRaw, hcIdealB) = HcIdeal();
    Console.Error.WriteLine("done");
    // 理想台の2値（**同じ線 +1.5 を使う**。§2-3 の拒否権1と Q5 が読む量）
    var hcIdealKeep = Enumerable.Range(0, hcRN).Select(u => !double.IsNaN(hcIdealAttr[u]) && hcIdealAttr[u] >= HcSoloLine).ToArray();
    var hcDraftKeep = Enumerable.Range(0, hcRN).Select(u => hcGrp[u] == 0).ToArray();
    var hcSplitStage = Enumerable.Range(0, hcRN).Select(u => hcIdealKeep[u] != hcDraftKeep[u]).ToArray();

    // ---- 拒否権（§2-3）---------------------------------------------------------------------------
    bool[] hcVeto1 = Enumerable.Range(0, hcRN).Select(u => hcSplitStage[u]).ToArray();
    bool[] hcVeto2 = hcInPrimary;
    bool[] hcVeto3 = Enumerable.Range(0, hcRN).Select(u => HcSoleKeys(u).Length > 0).ToArray();
    string HcVetoOf(int u)
    {
        var v = new List<string>();
        if (hcVeto1[u]) v.Add("1台");
        if (hcVeto2[u]) v.Add("2主判定");
        if (hcVeto3[u]) v.Add("3唯一");
        return v.Count == 0 ? "—" : string.Join("/", v);
    }

    long hcBattles = (long)hcNP * 4 * HcK * HcS * (hcW - 1) * HcM + hcIdealB;

    // =====================================================================================
    Console.WriteLine("# 第82期 —— ロスター健康診断（残す・転生・差し替えの3分）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 checkup tables <TSV>` の出力。**`docs/` には置かない。**");
    Console.WriteLine();
    Console.WriteLine("**器具は第81期の 2×2 ひとつだけ。在席差（第80期）は1度も使っていない**（指示書 §0-3）。");
    Console.WriteLine();
    Console.WriteLine("    単独(A) = y11 − y01      （相方 B は本物のまま A だけを素体に）  ← 横軸・第69期の標準器具");
    Console.WriteLine("    相乗(A,B) = y11 − y10 − y01 + y00                              ← 縦軸・第81期");
    Console.WriteLine();
    Console.WriteLine($"台: 埋め草3枚（残り 49 体・規則 P）＋ A ＋ B・席は規則配置 H・弱い波 {HcWeakPct}%・第2〜{hcW}波・"
                      + $"戦闘 seed {HcBand}..{HcBand + HcM - 1}。**全 {hcNP:N0} 組 × {HcS * HcK} 台**（系列 {HcS} × K {HcK}）。"
                      + $"理想台は `CompareBuilds()` {hcAllRows.Length} 行 × seed 0..{HcIdealSeeds - 1}。**合計 {hcBattles:N0} 戦**。");
    Console.WriteLine();
    Console.WriteLine($"駒1体あたりの標本は **{hcRN - 1} 組 × {HcS * HcK} 台 = {(hcRN - 1) * HcS * HcK:N0} 台**"
                      + $"（単独の帰属の SE の中央値 **{HcMedian(hcSoloSe):F3}pt**）。");
    Console.WriteLine();

    // ---- Q1: 第81期の再現 -----------------------------------------------------------------------
    Console.WriteLine("## Q1 —— 器具の再現（第81期 `pairs2` との突き合わせ）");
    Console.WriteLine();
    int q1Top = -1, q1Bot = -1;
    double q1MaxDiff = double.NaN, q1R = double.NaN;
    if (hcRefPath != "" && File.Exists(hcRefPath))
    {
        var refSyn = new double[hcNP];
        var refSeen = new bool[hcNP];
        double maxDiff = 0;
        int refGot = 0, refCells = 0;
        foreach (string line in File.ReadLines(hcRefPath))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int pi = hcPairIxOf[int.Parse(c[0]), int.Parse(c[1])];
            if (refSeen[pi]) continue;
            refSeen[pi] = true;
            int nt = int.Parse(c[2]);
            double sum = 0;
            for (int t = 0; t < nt; t++)
            {
                double x = double.Parse(c[9 + t], hcInv);
                sum += x;
                if (t < hcSerial[pi].Length) { maxDiff = Math.Max(maxDiff, Math.Abs(x - hcSerial[pi][t])); refCells++; }
            }
            refSyn[pi] = sum / nt;
            refGot++;
        }
        if (refGot == hcNP)
        {
            var mine = Enumerable.Range(0, hcNP).OrderByDescending(pi => hcSynAll[pi]).ToArray();
            var theirs = Enumerable.Range(0, hcNP).OrderByDescending(pi => refSyn[pi]).ToArray();
            q1Top = mine.Take(HcTop).Intersect(theirs.Take(HcTop)).Count();
            q1Bot = mine.TakeLast(HcTop).Intersect(theirs.TakeLast(HcTop)).Count();
            q1MaxDiff = maxDiff;
            q1R = HcCorr(hcSynAll, refSyn);
            Console.WriteLine($"`pairs2 run` を別に走らせた TSV（**この期の測定とは別の実行**）と突き合わせた:");
            Console.WriteLine();
            Console.WriteLine("| 量 | 実測 |");
            Console.WriteLine("|---|--:|");
            Console.WriteLine($"| 読めた組 | {refGot} / {hcNP} |");
            Console.WriteLine($"| 台ごとの相乗の**最大の食い違い** | **{maxDiff:F10}pt**（{refCells:N0} セル） |");
            Console.WriteLine($"| 組ごとの相乗の相関 r | {q1R:F6} |");
            Console.WriteLine($"| **上位 {HcTop} の一致** | **{q1Top} / {HcTop}** |");
            Console.WriteLine($"| **下位 {HcTop} の一致** | **{q1Bot} / {HcTop}** |");
        }
        else Console.WriteLine($"**参照 TSV が足りない（{refGot} / {hcNP} 組）。Q1 は判定できない。**");
    }
    else Console.WriteLine("**参照 TSV（`pairs2 run` の出力）が渡されていない。Q1 は `pairs2 tables` の出力と手で突き合わせること。**");
    Console.WriteLine();

    // ---- 表A -----------------------------------------------------------------------------------
    Console.WriteLine("## 表A —— 3分の一覧（51 体・**単独の帰属の降順**）");
    Console.WriteLine();
    Console.WriteLine("| # | 駒 | 群 | 単独 | SE | 最良の相方 | 相乗 | 正 | 負 | 相乗計 | 入口 | 発火口 | キー | 理想台 | 天井% | 床% | 拒否権 |");
    Console.WriteLine("|--:|---|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
    int rank = 0;
    foreach (int u in Enumerable.Range(0, hcRN).OrderByDescending(u => hcSolo[u]))
    {
        string grp = hcSpecial[u] ? "**別扱い**" : hcGroupName[hcGrp[u]];
        Console.WriteLine($"| {++rank} | {hcName[u]} | {grp} | {HcP2(hcSolo[u])} | {hcSoloSe[u]:F2} | "
                          + $"{(hcBest[u] < 0 ? "—" : hcName[hcBest[u]])} | {HcP2(hcBestV[u])} | {hcPosN[u]} | {hcNegN[u]} | "
                          + $"{HcP2(hcSynSum[u])} | {hcEntry[u].Length} | {hcHook[u].Length} | {hcKeyOf[u].Length} | "
                          + $"{HcP2(hcIdealAttr[u])} | {hcCeilPct[u]:F1} | {hcFloorPct[u]:F1} | {HcVetoOf(u)} |");
    }
    Console.WriteLine();
    int nKeep = Enumerable.Range(0, hcRN).Count(u => !hcSpecial[u] && hcGrp[u] == 0);
    int nReborn = Enumerable.Range(0, hcRN).Count(u => !hcSpecial[u] && hcGrp[u] == 1);
    int nSwap = Enumerable.Range(0, hcRN).Count(u => !hcSpecial[u] && hcGrp[u] == 2);
    int nSpecial = Enumerable.Range(0, hcRN).Count(u => hcSpecial[u]);
    Console.WriteLine($"**残す {nKeep} / 転生 {nReborn} / 差し替え {nSwap} / 別扱い {nSpecial}**（合計 {hcRN}）。");
    Console.WriteLine();
    Console.WriteLine("「正」「負」は**有意な**（\\|相乗\\| > 2SE）相手の数、「相乗計」は 50 組すべての相乗の和。"
                      + "「天井%」は **y00 > 95% の台の割合**（§1-4）、「床%」は **y00 = 0% の台の割合**"
                      + "（**指示書 §1-4 は天井しか見ていないが、実測では床のほうがずっと厚い**）。"
                      + "入口は**味方側**（被弾を含めない・第78期）。");
    Console.WriteLine();

    // ---- 表B -----------------------------------------------------------------------------------
    Console.WriteLine("## 表B —— 差し替え候補（**切ったときに失われるもの**）");
    Console.WriteLine();
    var swaps = Enumerable.Range(0, hcRN).Where(u => !hcSpecial[u] && hcGrp[u] == 2).OrderBy(u => hcSolo[u]).ToArray();
    if (swaps.Length == 0) Console.WriteLine("**該当なし。**");
    else
    {
        Console.WriteLine("| 駒 | 単独 | 最良の相乗 | 供給するキー | 唯一の書き手 | compare 行 | 主判定 | 理想台 | 拒否権 |");
        Console.WriteLine("|---|--:|--:|---|---|--:|:-:|--:|---|");
        foreach (int u in swaps)
        {
            var sup = hcSup[u].Select(s => s.Key).Distinct().OrderBy(x => x).Select(k => UnitTally.CarryKeys[k]).ToArray();
            var sole = HcSoleKeys(u).Select(k => UnitTally.CarryKeys[k]).ToArray();
            Console.WriteLine($"| {hcName[u]} | {HcP2(hcSolo[u])} | {HcP2(hcBestV[u])}"
                              + $"{(hcBest[u] < 0 ? "" : "（" + hcName[hcBest[u]] + "）")} | "
                              + $"{(sup.Length == 0 ? "**無し**" : string.Join("・", sup))} | "
                              + $"{(sole.Length == 0 ? "—" : "**" + string.Join("・", sole) + "**")} | "
                              + $"{hcRowCnt[u]} | {(hcInPrimary[u] ? "○" : "—")} | {HcP2(hcIdealAttr[u])} | {HcVetoOf(u)} |");
        }
    }
    Console.WriteLine();

    // ---- 表C -----------------------------------------------------------------------------------
    Console.WriteLine("## 表C —— 転生候補（**単独が弱いが、特定の相方と大きい相乗**）");
    Console.WriteLine();
    var reborn = Enumerable.Range(0, hcRN).Where(u => !hcSpecial[u] && hcGrp[u] == 1).OrderByDescending(u => hcBestV[u]).ToArray();
    if (reborn.Length == 0) Console.WriteLine("**該当なし。**");
    else
    {
        Console.WriteLine("| # | 駒 | 単独 | 最良の相方 | 相乗 | 分類 | 相方が供給するキー | 入口 | 発火口 | キー | 正 | 理想台 |");
        Console.WriteLine("|--:|---|--:|---|--:|---|---|--:|--:|--:|--:|--:|");
        int rk = 0;
        foreach (int u in reborn)
        {
            int v = hcBest[u];
            var psup = hcSup[v].Select(s => s.Key).Distinct().OrderBy(x => x).Select(k => UnitTally.CarryKeys[k]).ToArray();
            Console.WriteLine($"| {++rk} | {hcName[u]} | {HcP2(hcSolo[u])} | {hcName[v]} | {HcP2(hcBestV[u])} | "
                              + $"{hcClassName[HcClass(u, v)]} | {(psup.Length == 0 ? "無し" : string.Join("・", psup))} | "
                              + $"{hcEntry[u].Length} | {hcHook[u].Length} | {hcKeyOf[u].Length} | {hcPosN[u]} | {HcP2(hcIdealAttr[u])} |");
        }
    }
    Console.WriteLine();

    // ---- 表D -----------------------------------------------------------------------------------
    Console.WriteLine("## 表D —— 拒否権に当たった駒（§2-3。**差し替えたいが切れない**）");
    Console.WriteLine();
    var vetoed = swaps.Where(u => hcVeto1[u] || hcVeto2[u] || hcVeto3[u]).ToArray();
    Console.WriteLine($"差し替え候補 {swaps.Length} 体のうち **{vetoed.Length} 体**が拒否権に当たった。");
    Console.WriteLine();
    Console.WriteLine("| 拒否権 | 条件 | 差し替え候補で該当 | 51 体で該当 |");
    Console.WriteLine("|---|---|--:|--:|");
    Console.WriteLine($"| 1 | 理想台とドラフト台で群が割れる | {swaps.Count(u => hcVeto1[u])} | {Enumerable.Range(0, hcRN).Count(u => hcVeto1[u])} |");
    Console.WriteLine($"| 2 | `compare` の主判定 {Baseline.PrimaryRows.Length} 行に含まれる | {swaps.Count(u => hcVeto2[u])} | {Enumerable.Range(0, hcRN).Count(u => hcVeto2[u])} |");
    Console.WriteLine($"| 3 | 唯一の書き手であるキーがある | {swaps.Count(u => hcVeto3[u])} | {Enumerable.Range(0, hcRN).Count(u => hcVeto3[u])} |");
    Console.WriteLine();
    if (vetoed.Length > 0)
    {
        Console.WriteLine("| 駒 | 単独 | 理想台 | 拒否権 | 内訳 |");
        Console.WriteLine("|---|--:|--:|---|---|");
        foreach (int u in vetoed)
        {
            var det = new List<string>();
            if (hcVeto1[u]) det.Add($"理想台 {HcP2(hcIdealAttr[u])}（線 +{HcSoloLine:F1}）");
            if (hcVeto2[u]) det.Add("主判定に在席");
            if (hcVeto3[u]) det.Add("唯一の書き手: " + string.Join("・", HcSoleKeys(u).Select(k => UnitTally.CarryKeys[k])));
            Console.WriteLine($"| {hcName[u]} | {HcP2(hcSolo[u])} | {HcP2(hcIdealAttr[u])} | {HcVetoOf(u)} | {string.Join(" / ", det)} |");
        }
        Console.WriteLine();
    }

    // ---- 表E -----------------------------------------------------------------------------------
    Console.WriteLine("## 表E —— 別扱い（§2-2）");
    Console.WriteLine();
    var special = Enumerable.Range(0, hcRN).Where(u => hcSpecial[u]).OrderByDescending(u => hcCeilPct[u]).ToArray();
    if (special.Length == 0) Console.WriteLine("**該当なし**——天井の台が半数を超えた駒も、単独の帰属の符号が系列で割れた駒も 0 体。");
    else
    {
        Console.WriteLine("| 駒 | 理由 | 天井% | 床% | 単独（系列1 / 系列2） | 3分に載せたら |");
        Console.WriteLine("|---|---|--:|--:|---|---|");
        foreach (int u in special)
        {
            var why = new List<string>();
            if (hcNoMeasure[u]) why.Add($"天井 {hcCeilPct[u]:F1}% > {HcCeilShare:F0}%");
            if (hcSplitSign[u]) why.Add("単独の符号が系列で割れた");
            Console.WriteLine($"| {hcName[u]} | {string.Join(" / ", why)} | {hcCeilPct[u]:F1} | {hcFloorPct[u]:F1} | "
                              + $"{HcP2(hcSoloSv[u][0])} / {HcP2(hcSoloSv[u][1])} | {hcGroupName[hcGrp[u]]} |");
        }
    }
    Console.WriteLine();
    Console.WriteLine($"天井%（y00 > {HcCeil:F0}%）の51体での中央値 **{HcMedian(hcCeilPct):F1}%**・最大 {hcCeilPct.Max():F1}%（{hcName[Array.IndexOf(hcCeilPct, hcCeilPct.Max())]}）、"
                      + $"床%（y00 = 0%）の中央値 **{HcMedian(hcFloorPct):F1}%**。");
    Console.WriteLine();

    // ---- 表F -----------------------------------------------------------------------------------
    Console.WriteLine("## 表F —— 象限（横軸 単独の帰属 × 縦軸 最良の相乗）");
    Console.WriteLine();
    double[] colEdge = { -5, 0, HcSoloLine, 5 };
    string[] colName = { "単独 < -5", "-5 〜 0", $"0 〜 +{HcSoloLine:F1}", $"+{HcSoloLine:F1} 〜 +5", "+5 以上" };
    string[] rowName = { "有意な正 無し", $"0 〜 +{HcSynLine:F1}", "+5 〜 +10", "+10 〜 +20", "+20 以上" };
    int ColOf(double x) { int i = 0; while (i < colEdge.Length && x >= colEdge[i]) i++; return i; }
    var cell = new List<int>[rowName.Length, colName.Length];
    for (int r = 0; r < rowName.Length; r++) for (int c = 0; c < colName.Length; c++) cell[r, c] = new List<int>();
    for (int u = 0; u < hcRN; u++)
    {
        int c = ColOf(hcSolo[u]);
        int r = double.IsNaN(hcBestV[u]) ? 0 : (hcBestV[u] < HcSynLine ? 1 : hcBestV[u] < 10 ? 2 : hcBestV[u] < 20 ? 3 : 4);
        cell[r, c].Add(u);
    }
    Console.WriteLine("| 最良の相乗 ＼ 単独 | " + string.Join(" | ", colName) + " |");
    Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", colName.Length)));
    for (int r = rowName.Length - 1; r >= 0; r--)
    {
        var line = new List<string>();
        for (int c = 0; c < colName.Length; c++) line.Add(cell[r, c].Count == 0 ? "" : cell[r, c].Count.ToString());
        Console.WriteLine($"| {rowName[r]} | " + string.Join(" | ", line) + " |");
    }
    Console.WriteLine();
    Console.WriteLine("**線の左下（単独 < +1.5 かつ 最良の相乗 < +5.0）が差し替え候補**、"
                      + "**左上が転生**、**右側は相乗を問わず残す**。");
    Console.WriteLine();

    // ---- Q3: 系列の再現 --------------------------------------------------------------------------
    Console.WriteLine("## Q3 —— 系列の再現（**主判定**）");
    Console.WriteLine();
    int q3Same = Enumerable.Range(0, hcRN).Count(u => hcGrpS[u][0] == hcGrpS[u][1]);
    int q3SameNoSp = Enumerable.Range(0, hcRN).Count(u => !hcSpecial[u] && hcGrpS[u][0] == hcGrpS[u][1]);
    int q3NoSp = Enumerable.Range(0, hcRN).Count(u => !hcSpecial[u]);
    Console.WriteLine($"**独立な2系列（{HcK} 台ずつ・台を共有しない）で 3分の群が一致したのは {q3Same} / {hcRN} 体**"
                      + $"（別扱いを除くと {q3SameNoSp} / {q3NoSp}）。単独の帰属の系列間相関 r = "
                      + $"**{HcCorr(Enumerable.Range(0, hcRN).Select(u => hcSoloSv[u][0]).ToArray(), Enumerable.Range(0, hcRN).Select(u => hcSoloSv[u][1]).ToArray()):F3}**。");
    Console.WriteLine();
    var q3Bad = Enumerable.Range(0, hcRN).Where(u => hcGrpS[u][0] != hcGrpS[u][1]).ToArray();
    if (q3Bad.Length > 0)
    {
        Console.WriteLine("| 駒 | 合算の群 | 系列1 | 系列2 | 単独（合算 / 1 / 2） | 最良の相乗（合算 / 1 / 2） | 境界 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (int u in q3Bad)
        {
            string edge = hcGrpS[u].Contains(0) ? $"単独の線 +{HcSoloLine:F1}" : $"相乗の線 +{HcSynLine:F1}";
            Console.WriteLine($"| {hcName[u]} | {hcGroupName[hcGrp[u]]} | {hcGroupName[hcGrpS[u][0]]} | {hcGroupName[hcGrpS[u][1]]} | "
                              + $"{HcP2(hcSolo[u])} / {HcP2(hcSoloSv[u][0])} / {HcP2(hcSoloSv[u][1])} | "
                              + $"{HcP2(hcBestV[u])} / {HcP2(hcBestS[u][0])} / {HcP2(hcBestS[u][1])} | {edge} |");
        }
        Console.WriteLine();
        int nearSolo = q3Bad.Count(u => Math.Abs(hcSolo[u] - HcSoloLine) < 1.0);
        int nearSyn = q3Bad.Count(u => !double.IsNaN(hcBestV[u]) && Math.Abs(hcBestV[u] - HcSynLine) < 2.0);
        Console.WriteLine($"割れた {q3Bad.Length} 体のうち、**合算の単独が線 ±1.0pt の内側にいるのが {nearSolo} 体**、"
                          + $"**合算の最良の相乗が線 ±2.0pt の内側にいるのが {nearSyn} 体**。");
        Console.WriteLine();
    }

    // ---- Q5: 台の一致 ----------------------------------------------------------------------------
    Console.WriteLine("## Q5 —— 台の一致（ドラフト台 × 理想台）");
    Console.WriteLine();
    int q5 = Enumerable.Range(0, hcRN).Count(u => hcSplitStage[u]);
    Console.WriteLine($"**群が割れた駒は {q5} / {hcRN} 体**（2値: 「残す（単独 ≥ +{HcSoloLine:F1}）」か否か）。"
                      + $"帰属どうしの相関 r = **{HcCorr(Enumerable.Range(0, hcRN).Where(u => !double.IsNaN(hcIdealAttr[u])).Select(u => hcSolo[u]).ToArray(), Enumerable.Range(0, hcRN).Where(u => !double.IsNaN(hcIdealAttr[u])).Select(u => hcIdealAttr[u]).ToArray()):F3}**。");
    Console.WriteLine();
    Console.WriteLine("| | 理想台で残す | 理想台で残さない |");
    Console.WriteLine("|---|--:|--:|");
    Console.WriteLine($"| ドラフト台で残す | {Enumerable.Range(0, hcRN).Count(u => hcDraftKeep[u] && hcIdealKeep[u])} | {Enumerable.Range(0, hcRN).Count(u => hcDraftKeep[u] && !hcIdealKeep[u])} |");
    Console.WriteLine($"| ドラフト台で残さない | {Enumerable.Range(0, hcRN).Count(u => !hcDraftKeep[u] && hcIdealKeep[u])} | {Enumerable.Range(0, hcRN).Count(u => !hcDraftKeep[u] && !hcIdealKeep[u])} |");
    Console.WriteLine();
    Console.WriteLine("| 駒 | ドラフト台の単独 | 理想台の帰属 | 群（ドラフト） | 在席行 |");
    Console.WriteLine("|---|--:|--:|---|--:|");
    foreach (int u in Enumerable.Range(0, hcRN).Where(u => hcSplitStage[u]).OrderByDescending(u => hcIdealAttr[u]))
        Console.WriteLine($"| {hcName[u]} | {HcP2(hcSolo[u])} | {HcP2(hcIdealAttr[u])} | "
                          + $"{(hcSpecial[u] ? "別扱い" : hcGroupName[hcGrp[u]])} | {hcRowCnt[u]} |");
    Console.WriteLine();

    // ---- 予測の答え合わせ -------------------------------------------------------------------------
    Console.WriteLine("## 予測の答え合わせ");
    Console.WriteLine();
    Console.WriteLine("| # | 予測 | 実測 | |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| P1 | 差し替え候補 3〜8 体 | {nSwap} 体 | {(nSwap >= 3 && nSwap <= 8 ? "○" : "**×**")} |");
    Console.WriteLine($"| P2 | 転生候補 10 体前後 | {nReborn} 体 | {(nReborn >= 5 && nReborn <= 15 ? "○" : "**×**")} |");
    {
        int sasa = Array.FindIndex(hcRoster, d => d.Name.Contains("ササ"));
        string sg = sasa < 0 ? "—" : (hcSpecial[sasa] ? "別扱い" : hcGroupName[hcGrp[sasa]]);
        Console.WriteLine($"| P3 | ササは転生側 | {sg}（単独 {(sasa < 0 ? "—" : HcP2(hcSolo[sasa]))} / 最良 {(sasa < 0 ? "—" : HcP2(hcBestV[sasa]))}） | {(sasa >= 0 && !hcSpecial[sasa] && hcGrp[sasa] == 1 ? "○" : "**×**")} |");
        var bodyNames = new[] { "ボルグ", "ゴルム", "ドルガ", "カド" };
        var body = bodyNames.Select(n => Array.FindIndex(hcRoster, d => d.Name.Contains(n))).Where(i => i >= 0).ToArray();
        int bodyKeep = body.Count(u => !hcSpecial[u] && hcGrp[u] == 0);
        Console.WriteLine($"| P4 | 体4枚は残す | {string.Join(" / ", body.Select(u => hcName[u] + " " + (hcSpecial[u] ? "別扱い" : hcGroupName[hcGrp[u]])))} | {(bodyKeep == body.Length ? "○" : "**×**")} |");
        Console.WriteLine($"| P5 | 台が割れる駒が 5 体以上 | {q5} 体 | {(q5 >= 5 ? "○" : "**×**")} |");
        double bodyCeil = body.Length == 0 ? double.NaN : body.Average(u => hcCeilPct[u]);
        double restCeil = Enumerable.Range(0, hcRN).Where(u => !body.Contains(u)).Average(u => hcCeilPct[u]);
        Console.WriteLine($"| P6 | 天井は体に集中 | 体4枚の天井% {bodyCeil:F1} 対 残り {restCeil:F1} | {(bodyCeil > restCeil * 1.5 ? "○" : "**×**")} |");
    }
    Console.WriteLine();

    // ---- 判定 -----------------------------------------------------------------------------------
    Console.WriteLine("## 判定");
    Console.WriteLine();
    Console.WriteLine("| # | 問い | 実測 | 判定 |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| Q1 | 第81期の上位・下位 30 と 20/30 以上一致 | 上位 {(q1Top < 0 ? "—" : q1Top.ToString())} / 下位 {(q1Bot < 0 ? "—" : q1Bot.ToString())}"
                      + $"（台ごとの最大差 {(double.IsNaN(q1MaxDiff) ? "—" : q1MaxDiff.ToString("F10"))}） | {(q1Top >= 20 && q1Bot >= 20 ? "○" : q1Top < 0 ? "—" : "**×**")} |");
    Console.WriteLine($"| Q2 | 差し替え候補が 1〜15 体 | {nSwap} 体 | {(nSwap >= 1 && nSwap <= 15 ? "○" : "**×**")} |");
    Console.WriteLine($"| **Q3** | **2系列で群が一致する駒が 45/51 以上** | **{q3Same} / {hcRN}** | {(q3Same >= 45 ? "○" : "**×**")} |");
    int v1 = Enumerable.Range(0, hcRN).Count(u => hcVeto1[u]), v2 = Enumerable.Range(0, hcRN).Count(u => hcVeto2[u]), v3 = Enumerable.Range(0, hcRN).Count(u => hcVeto3[u]);
    Console.WriteLine($"| Q4 | 拒否権が働いているか（3条件とも 0 体なら緩い疑い） | 51 体で {v1} / {v2} / {v3}、差し替え候補で {swaps.Count(u => hcVeto1[u])} / {swaps.Count(u => hcVeto2[u])} / {swaps.Count(u => hcVeto3[u])} | {(v1 + v2 + v3 > 0 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q5 | 台が割れた駒の数 | {q5} / {hcRN} | — |");
    Console.WriteLine("| Q6 | `compare` 305 セル 0 件・`docs/` 差分 0 | `checkup check` と `docs/` の再生成で別に確かめる | — |");
    Console.WriteLine();
    // =====================================================================================
    // 第119期 —— 「マイナスを外した版」の軸（表P・Q1・表B'・表C・表D・表E）
    // =====================================================================================
    Console.WriteLine("---");
    Console.WriteLine();
    Console.WriteLine("# 第119期 —— 健康診断の回し直し（52 枚）と「マイナスを外した版」の新しい軸");
    Console.WriteLine();
    Console.WriteLine("    単独     = y11 − y01      第82期の軸（特性ぜんぶの値段）。差し替えの判定に使う");
    Console.WriteLine("    プラス値 = yP  − y01      マイナスを外したときの強さ（(ii) では yP = y11 なので単独と一致する）");
    Console.WriteLine("    マイナス代 = yP  − y11    マイナスが払わせている代金（**負なら「マイナスが実質プラス」**）");
    Console.WriteLine();
    Console.WriteLine("**指示書 §2-2 の式は `y11 − yP` だが、それだと「代金」の符号が但し書きと逆になる**"
                      + "——マイナスが高くつく駒ほど `yP > y11` なので `y11 − yP` は負になり、"
                      + "「負なら実質プラス」と読めなくなる。**符号だけ直した**（但し書きのほうを正とした）。"
                      + "この向きだと **プラス値 = 単独 + マイナス代** が恒等式になる。");
    Console.WriteLine();

    // ---- 駒ごとの プラス値 / マイナス代 / マイナスのみ ---------------------------------------------
    var hcPlusS = new List<double>[hcRN][];
    var hcMinusS = new List<double>[hcRN];
    for (int u = 0; u < hcRN; u++)
    {
        hcPlusS[u] = new List<double>[HcS];
        for (int sx = 0; sx < HcS; sx++) hcPlusS[u][sx] = new List<double>();
        hcMinusS[u] = new List<double>();
    }
    for (int pi = 0; pi < hcNP; pi++)
    {
        (int a, int b) = hcAllPairs[pi];
        for (int t = 0; t < hcNT[pi]; t++)
        {
            double[] y = hcY[pi][t];
            int sx = t % HcS;
            hcPlusS[a][sx].Add(y[4] - y[1]);
            hcPlusS[b][sx].Add(y[5] - y[2]);
            hcMinusS[a].Add(y[6] - y[1]);
            hcMinusS[b].Add(y[7] - y[2]);
        }
    }
    var hcPlusVal = new double[hcRN];
    var hcPlusSe = new double[hcRN];
    var hcPlusSv = new double[hcRN][];
    var hcMinusVal = new double[hcRN];
    for (int u = 0; u < hcRN; u++)
    {
        var all = hcPlusS[u][0].Concat(hcPlusS[u][1]).ToArray();
        hcPlusVal[u] = all.Average();
        hcPlusSe[u] = HcSd(all) / Math.Sqrt(all.Length);
        hcPlusSv[u] = new[] { hcPlusS[u][0].Average(), hcPlusS[u][1].Average() };
        hcMinusVal[u] = hcMinusS[u].Average();
    }
    double[] hcCost = Enumerable.Range(0, hcRN).Select(u => hcPlusVal[u] - hcSolo[u]).ToArray();

    // ---- 表P: 分類 -------------------------------------------------------------------------------
    Console.WriteLine("## 表P —— マイナスの分類（52 枚）と、外した札 / 引き上げた数値");
    Console.WriteLine();
    Console.WriteLine($"`BattleCore/Traits.cs` の enum を走査: **列挙子 {hcScanEnum} 件 / ブロック {hcScanBlock} 件**"
                      + $"（0 件なら止める・第117期）。**数値のマイナス（(iii)）の語に当たった駒は {hcNumWordN} 体**、"
                      + $"そのうち**実際に中央値未満で引き上がったのは {hcNumHitN} 体**。");
    Console.WriteLine();
    Console.WriteLine($"中央値（**実装から数えた**・指示書 §1 の 2）: 攻 **{hcMedStat[0]}** / HP **{hcMedStat[1]}** / 速 **{hcMedStat[2]}**。");
    Console.WriteLine();
    Console.WriteLine("| # | 駒 | 型 | 外した札（yP） | 引き上げ（yP） | 外した札（yM） | 型の理由 |");
    Console.WriteLine("|--:|---|---|---|---|---|---|");
    for (int u = 0; u < hcRN; u++)
    {
        string dp = hcDropP[u].Count == 0 ? "—" : string.Join("・", hcDropP[u]);
        string dn = hcNumHit[u].Count == 0 ? "—" : string.Join("・", hcNumHit[u].Select(h =>
            $"{hcStatName[h.Stat]} {(h.Stat == 0 ? hcRoster[u].Attack : h.Stat == 1 ? hcRoster[u].MaxHp : hcRoster[u].Speed)} → {hcMedStat[h.Stat]}"));
        string dm = hcDropM[u].Count == 0 ? "—" : string.Join("・", hcDropM[u]);
        string why = hcDropP[u].Count > 0
            ? hcLabel[hcDropP[u][0]].Why
            : hcNumHit[u].Count > 0
                ? $"`MinusText` に「{hcNumHit[u][0].Word}」があり、{hcStatName[hcNumHit[u][0].Stat]} が中央値未満"
                : "外せる札が1枚も無く、数値のマイナスも無い＝**1つの札が両義**";
        Console.WriteLine($"| {u + 1} | {hcName[u]} | {HcTypeOf(u)} | {dp} | {dn} | {dm} | {why} |");
    }
    Console.WriteLine();
    Console.WriteLine("札ごとの分類（**enum のブロックから引いた既定と食い違うものだけ**を並べる）:");
    Console.WriteLine();
    Console.WriteLine("| 札 | この期の分類 | enum のブロック | 理由 |");
    Console.WriteLine("|---|---|---|---|");
    int hcOverride = 0;
    foreach (TraitId t in hcRoster.SelectMany(d => d.Traits).Distinct().OrderBy(t => t.ToString(), StringComparer.Ordinal))
    {
        int def = HcBlockDefault(t);
        if (def == hcLabel[t].L) continue;
        hcOverride++;
        Console.WriteLine($"| `{t}` | {hcLabelName[hcLabel[t].L]} | {(def < 0 ? "既定なし（札・器具）" : hcLabelName[def])}"
                          + $"（{(hcBlockOf.TryGetValue(t.ToString(), out string? bq) ? bq : "—")}） | {hcLabel[t].Why} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**上書きは {hcOverride} 件**（残りは enum のブロックがそのまま既定になった）。");
    Console.WriteLine();

    // ---- Q1: 第82期の 51 体の再現 -----------------------------------------------------------------
    Console.WriteLine("## Q1 —— 器具の再現（第82期の 51 体・`checkup run51` の TSV）");
    Console.WriteLine();
    Console.WriteLine("**指示書の「ソム・トモを除いた 51 体」は数が合わない**——第82期のロスターは"
                      + "**現行 52 枚から ソム を抜き、トモ の席に ハリ を戻した 51 枚**である"
                      + "（第108期に ハリ → トモ の入れ替えがあったので、2 枚抜くと 50 枚になる）。**ハリ を戻して 51 枚で回した。**");
    Console.WriteLine();
    int q1Match = -1, q1N = 0;
    string hcP82Path = "";
    {
        string dirq = Directory.GetCurrentDirectory();
        for (int up = 0; up < 6 && hcP82Path == ""; up++)
        {
            string cand = Path.Combine(dirq, "design", "PHASE82_CHECKUP.md");
            if (File.Exists(cand)) hcP82Path = cand;
            else { var pq = Directory.GetParent(dirq); if (pq == null) break; dirq = pq.FullName; }
        }
    }
    var hcOld82 = new Dictionary<string, string>(StringComparer.Ordinal);
    var hcOld82Solo = new Dictionary<string, double>(StringComparer.Ordinal);
    var hcOld82Syn = new Dictionary<string, double>(StringComparer.Ordinal);
    if (hcP82Path != "")
        foreach (string line in File.ReadAllLines(hcP82Path))
        {
            if (!line.StartsWith("| ")) continue;
            var cs = line.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            if (cs.Length < 4 || !int.TryParse(cs[0], out _)) continue;
            string g = cs[2].Replace("*", "");
            if (g is "残す" or "転生" or "差し替え" or "別扱い")
            {
                hcOld82[cs[1]] = g;
                if (cs.Length > 3 && double.TryParse(cs[3].Replace("+", ""), out double so82)) hcOld82Solo[cs[1]] = so82;
                if (cs.Length > 6 && double.TryParse(cs[6].Replace("+", ""), out double sy82)) hcOld82Syn[cs[1]] = sy82;
            }
        }
    if (hcOld82.Count != 51)
    {
        Console.WriteLine($"**第82期の表A を {hcOld82.Count} 行しか走査できなかった（51 行のはず）。Q1 は判定しない**（第117期）。");
        Console.WriteLine();
    }
    else if (hcRefPath51 == "" || !File.Exists(hcRefPath51))
    {
        Console.WriteLine("**`checkup run51` の TSV が渡されていない。Q1 は判定しない。**");
        Console.WriteLine();
    }
    else
    {
        // 51 体のロスターを組み直して、TSV だけから 3分を出す（台の抽選には触らない）
        var r51 = UnitCatalog.All.Select(d => d.Id == "tomo" ? UnitCatalog.Hari : d).Where(d => d.Id != "som").ToArray();
        int n51 = r51.Length;
        var pairs51 = new List<(int A, int B)>();
        for (int a = 0; a < n51; a++) for (int b = a + 1; b < n51; b++) pairs51.Add((a, b));
        var ix51 = new int[n51, n51];
        for (int pi = 0; pi < pairs51.Count; pi++) { ix51[pairs51[pi].A, pairs51[pi].B] = pi; ix51[pairs51[pi].B, pairs51[pi].A] = pi; }
        var y51 = new double[pairs51.Count][][];
        var nt51 = new int[pairs51.Count];
        int got51 = 0;
        foreach (string line in File.ReadLines(hcRefPath51))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int pi = ix51[int.Parse(c[0]), int.Parse(c[1])];
            if (y51[pi] != null) continue;
            int nt = int.Parse(c[2]);
            nt51[pi] = nt;
            int at = 9 + nt;
            var ys = new double[nt][];
            for (int t = 0; t < nt; t++)
            {
                ys[t] = new double[4];
                for (int v = 0; v < 4; v++) ys[t][v] = double.Parse(c[at++], hcInv);
            }
            y51[pi] = ys; got51++;
        }
        if (got51 != pairs51.Count)
        {
            Console.WriteLine($"**`run51` のシャードが足りない: {got51} / {pairs51.Count} 組。Q1 は判定しない。**");
            Console.WriteLine();
        }
        else
        {
            var solo51 = new List<double>[n51][];
            var ceilN = new int[n51]; var ceilD = new int[n51];
            for (int u = 0; u < n51; u++) { solo51[u] = new List<double>[HcS]; for (int sx = 0; sx < HcS; sx++) solo51[u][sx] = new List<double>(); }
            var syn51 = new double[pairs51.Count]; var se51 = new double[pairs51.Count];
            var synS51 = new double[pairs51.Count][]; var seS51 = new double[pairs51.Count][];
            for (int pi = 0; pi < pairs51.Count; pi++)
            {
                (int a, int b) = pairs51[pi];
                int nt = nt51[pi];
                var all = new double[nt];
                for (int t = 0; t < nt; t++)
                {
                    double[] y = y51[pi][t];
                    all[t] = y[0] - y[2] - y[1] + y[3];
                    solo51[a][t % HcS].Add(y[0] - y[1]);
                    solo51[b][t % HcS].Add(y[0] - y[2]);
                    if (y[3] > HcCeil) { ceilN[a]++; ceilN[b]++; }
                    ceilD[a]++; ceilD[b]++;
                }
                syn51[pi] = all.Average(); se51[pi] = HcSd(all) / Math.Sqrt(nt);
                synS51[pi] = new double[HcS]; seS51[pi] = new double[HcS];
                for (int sx = 0; sx < HcS; sx++)
                {
                    var xs = new List<double>();
                    for (int t = sx; t < nt; t += HcS) xs.Add(all[t]);
                    synS51[pi][sx] = xs.Average(); seS51[pi][sx] = HcSd(xs) / Math.Sqrt(xs.Count);
                }
            }
            var g51 = new string[n51];
            var soloV51 = new double[n51];
            var bestV51 = new double[n51];
            for (int u = 0; u < n51; u++)
            {
                var all = solo51[u][0].Concat(solo51[u][1]).ToArray();
                soloV51[u] = all.Average();
                double best = double.NaN;
                for (int v = 0; v < n51; v++)
                {
                    if (v == u) continue;
                    int pi = ix51[u, v];
                    if (syn51[pi] > 2 * se51[pi] && (double.IsNaN(best) || syn51[pi] > best)) best = syn51[pi];
                }
                bestV51[u] = best;
                bool split = solo51[u][0].Average() * solo51[u][1].Average() < 0;
                bool noMeasure = 100.0 * ceilN[u] / ceilD[u] > HcCeilShare;
                g51[u] = split || noMeasure ? "別扱い"
                    : soloV51[u] >= HcSoloLine ? "残す"
                    : (!double.IsNaN(best) && best >= HcSynLine ? "転生" : "差し替え");
            }
            int same = 0; var moved = new List<string>();
            for (int u = 0; u < n51; u++)
            {
                if (!hcOld82.TryGetValue(r51[u].Name, out string? old)) { moved.Add($"| {r51[u].Name} | — | {g51[u]} | 第82期の表A に無い |"); continue; }
                if (old == g51[u]) same++;
                else moved.Add($"| {r51[u].Name} | {old} | {g51[u]} | 単独 {HcP2(soloV51[u])} / 最良 {HcP2(bestV51[u])} |");
            }
            q1Match = same; q1N = n51;
            Console.WriteLine($"**第82期の3分と一致したのは {same} / {n51} 体**（線 48/51）。"
                              + $"分布は 残す {g51.Count(g => g == "残す")} / 転生 {g51.Count(g => g == "転生")} / "
                              + $"差し替え {g51.Count(g => g == "差し替え")} / 別扱い {g51.Count(g => g == "別扱い")}"
                              + "（第82期は **31 / 13 / 6 / 1**）。");
            Console.WriteLine();
            // **群（3値）ではなく、その下の連続量が再現するかを見る**——線の近くに標本が溜まっていると、
            // 群の一致率は器具の再現性ではなく「線と分布の位置関係」を測ってしまう。
            {
                var xa = new List<double>(); var ya = new List<double>();
                var xb = new List<double>(); var yb = new List<double>();
                double worstSolo = 0;
                for (int u = 0; u < n51; u++)
                {
                    if (hcOld82Solo.TryGetValue(r51[u].Name, out double o))
                    { xa.Add(o); ya.Add(soloV51[u]); worstSolo = Math.Max(worstSolo, Math.Abs(o - soloV51[u])); }
                    if (hcOld82Syn.TryGetValue(r51[u].Name, out double o2) && !double.IsNaN(bestV51[u]))
                    { xb.Add(o2); yb.Add(bestV51[u]); }
                }
                Console.WriteLine($"**群の下の連続量**: 単独の相関 r = **{HcCorr(xa, ya):F3}**（{xa.Count} 体・最大差 {worstSolo:F2}pt）、"
                                  + $"最良の相乗の相関 r = **{HcCorr(xb, yb):F3}**（{xb.Count} 体）。");
                Console.WriteLine();
                int nearLine = 0;
                foreach (int u in Enumerable.Range(0, n51))
                {
                    if (!hcOld82.TryGetValue(r51[u].Name, out string? og) || og == g51[u]) continue;
                    if (Math.Abs(soloV51[u] - HcSoloLine) < 1.0 || (!double.IsNaN(bestV51[u]) && Math.Abs(bestV51[u] - HcSynLine) < 1.5)) nearLine++;
                }
                Console.WriteLine($"**群が変わった駒のうち、この期の値が線から 1.0pt（相乗は 1.5pt）以内にいるのは {nearLine} 体。**");
                Console.WriteLine();
            }
            if (moved.Count > 0)
            {
                Console.WriteLine("| 駒 | 第82期 | この期（51 体） | この期の値 |");
                Console.WriteLine("|---|---|---|---|");
                foreach (string l in moved) Console.WriteLine(l);
                Console.WriteLine();
                Console.WriteLine("**engine の規則も駒の数値も第82期から動いている**（呪い則・再行動・積み過ぎ・灯の濾しほか）ので、"
                                  + "ここで一致しない駒は「器具が壊れた」ではなく「盤面が変わった」でもありうる。**判定は線どおり読む。**");
                Console.WriteLine();
            }
        }
    }

    // ---- 表B': 52 枚の3分と、第82期からの異動 ------------------------------------------------------
    Console.WriteLine("## 表B' —— 52 枚の3分（Q2）と第82期からの異動");
    Console.WriteLine();
    Console.WriteLine($"**残す {nKeep} / 転生 {nReborn} / 差し替え {nSwap} / 別扱い {nSpecial}**（合計 {hcRN}）。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 第82期 | この期（52 枚） | 単独 | 最良の相乗 |");
    Console.WriteLine("|---|---|---|--:|--:|");
    int hcMoved = 0;
    for (int u = 0; u < hcRN; u++)
    {
        string now = hcSpecial[u] ? "別扱い" : hcGroupName[hcGrp[u]];
        string old = hcOld82.TryGetValue(hcName[u], out string? o) ? o : "—（第82期に居ない）";
        if (old == now) continue;
        hcMoved++;
        Console.WriteLine($"| {hcName[u]} | {old} | {now} | {HcP2(hcSolo[u])} | {HcP2(hcBestV[u])} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**異動は {hcMoved} 体**（52 枚の台は第82期の 51 枚の台とは埋め草の母集団が違うので、"
                      + "ここは「同じ器具の再現」ではない。再現は Q1 で見る）。");
    Console.WriteLine();

    // ---- 表C: 4象限（Q3。**この期の主産物**）-------------------------------------------------------
    Console.WriteLine("## 表C —— 4象限（横軸 単独 × 縦軸 プラス値。**Q3・この期の主産物**）");
    Console.WriteLine();
    if (hasX == 0)
    {
        Console.WriteLine("**第119期の列が付いた TSV（`checkup runp`）が渡されていない。表C は出せない。**");
        Console.WriteLine();
    }
    else
    {
        bool[] hcMeasurable = Enumerable.Range(0, hcRN)
            .Select(u => hcCeilPct[u] <= HcCeilShare && hcFloorPct[u] <= HcCeilShare).ToArray();
        Console.WriteLine($"**情報帯の screen（自己検査 (e)・指示書 §1 の 6）: 天井% ≤ {HcCeilShare:F0} かつ 床% ≤ {HcCeilShare:F0}**。"
                          + $"通ったのは **{hcMeasurable.Count(x => x)} / {hcRN} 体**"
                          + $"（落ちたのは {string.Join("・", Enumerable.Range(0, hcRN).Where(u => !hcMeasurable[u]).Select(u => $"{hcName[u]}（天井 {hcCeilPct[u]:F1} / 床 {hcFloorPct[u]:F1}）"))}）。"
                          + "**落ちた駒は象限に置かず、別扱いとして並べる。**");
        Console.WriteLine();
        string[] quadName = { "素で有能（正常）", "**マイナスが実質プラス**", "**思想どおり**", "**思想に合わない**" };
        int QuadOf(int u) => (hcSolo[u] >= HcSoloLine ? 0 : 2) + (hcPlusVal[u] >= HcSoloLine ? 0 : 1);
        var quad = new List<int>[4];
        for (int q = 0; q < 4; q++) quad[q] = new List<int>();
        for (int u = 0; u < hcRN; u++) if (hcMeasurable[u]) quad[QuadOf(u)].Add(u);
        Console.WriteLine("| | マイナス抜きが強い（プラス値 ≥ +1.5） | マイナス抜きも弱い |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 現行が強い（単独 ≥ +1.5） | {quad[0].Count}（素で有能） | {quad[1].Count}（**マイナスが実質プラス**） |");
        Console.WriteLine($"| 現行が弱い | {quad[2].Count}（**思想どおり**） | {quad[3].Count}（**思想に合わない**） |");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | 型 | 象限 | 単独 | プラス値 | マイナス代 | マイナスのみ | 最良の相乗 | 群 |");
        Console.WriteLine("|--:|---|---|---|--:|--:|--:|--:|--:|---|");
        int rkq = 0;
        foreach (int u in Enumerable.Range(0, hcRN).Where(u => hcMeasurable[u]).OrderBy(u => QuadOf(u)).ThenBy(u => hcPlusVal[u]))
            Console.WriteLine($"| {++rkq} | {hcName[u]} | {HcTypeOf(u)} | {quadName[QuadOf(u)]} | {HcP2(hcSolo[u])} | "
                              + $"{HcP2(hcPlusVal[u])} | {HcP2(hcCost[u])} | {HcP2(hcMinusVal[u])} | {HcP2(hcBestV[u])} | "
                              + $"{(hcSpecial[u] ? "別扱い" : hcGroupName[hcGrp[u]])} |");
        Console.WriteLine();
        var noMeasure119 = Enumerable.Range(0, hcRN).Where(u => !hcMeasurable[u]).ToArray();
        if (noMeasure119.Length > 0)
        {
            Console.WriteLine("**screen で落ちた駒（象限に置かない）**:");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 型 | 天井% | 床% | 単独 | プラス値 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|");
            foreach (int u in noMeasure119)
                Console.WriteLine($"| {hcName[u]} | {HcTypeOf(u)} | {hcCeilPct[u]:F1} | {hcFloorPct[u]:F1} | {HcP2(hcSolo[u])} | {HcP2(hcPlusVal[u])} |");
            Console.WriteLine();
        }
        // **プラスが特性ではなく体でできている駒**——マイナスを外すと素体そのものになる
        var asPlain = Enumerable.Range(0, hcRN)
            .Where(u => !HcIsII(u) && hcPlusDef[u].Traits.Count == 0
                        && hcPlusDef[u].Attack == hcRoster[u].Attack
                        && hcPlusDef[u].MaxHp == hcRoster[u].MaxHp && hcPlusDef[u].Speed == hcRoster[u].Speed).ToArray();
        if (asPlain.Length > 0)
            Console.WriteLine($"**yP が素体と同一になる駒が {asPlain.Length} 体**: "
                              + string.Join("・", asPlain.Select(u => $"{hcName[u]}（プラス値 {HcP2(hcPlusVal[u])}）"))
                              + "——**持っている札がマイナスだけ**なので、外すと素体そのものになる。"
                              + "**プラス値が定義上ちょうど 0 になる**（この駒のプラスは特性ではなく体の側にある）。");
        Console.WriteLine();
        var bad = quad[3].Where(u => !HcIsII(u)).ToArray();
        var badII = quad[3].Where(HcIsII).ToArray();
        Console.WriteLine($"**「思想に合わない」象限は {quad[3].Count} 体。うち (ii)（分離できない＝マイナスを外せない）が {badII.Length} 体、"
                          + $"実際にマイナスを外して測れたのに弱いままだったのが {bad.Length} 体。**");
        Console.WriteLine();
        Console.WriteLine($"- **外して測れたのに弱い（見直し対象・プラス値の低い順）**: {(bad.Length == 0 ? "**該当なし**" : string.Join("・", bad.OrderBy(u => hcPlusVal[u]).Select(u => $"{hcName[u]}（{HcP2(hcPlusVal[u])}）")))}");
        Console.WriteLine($"- **(ii) なので外しようが無い**: {(badII.Length == 0 ? "該当なし" : string.Join("・", badII.OrderBy(u => hcPlusVal[u]).Select(u => $"{hcName[u]}（{HcP2(hcPlusVal[u])}）")))}");
        Console.WriteLine();
        if (quad[1].Count > 0)
            Console.WriteLine($"**「マイナスが実質プラス」象限**: {string.Join("・", quad[1].OrderBy(u => hcCost[u]).Select(u => $"{hcName[u]}（マイナス代 {HcP2(hcCost[u])}）"))}"
                              + "——**現行は線を越えているのに、マイナスを外すと越えなくなる。**");
        else Console.WriteLine("**「マイナスが実質プラス」は 0 体。**");
        Console.WriteLine();
        Console.WriteLine("**マイナス代の大きい順（＝マイナスがいちばん高くついている駒。(ii) は定義上 0）**: "
                          + string.Join("・", Enumerable.Range(0, hcRN).Where(u => !HcIsII(u)).OrderByDescending(u => hcCost[u]).Take(8)
                              .Select(u => $"{hcName[u]} {HcP2(hcCost[u])}")) + "。");
        Console.WriteLine();
        var neg = Enumerable.Range(0, hcRN).Where(u => !HcIsII(u) && hcCost[u] < 0).OrderBy(u => hcCost[u]).ToArray();
        Console.WriteLine($"**代金が負（＝マイナスを外すと弱くなる）駒は {neg.Length} 体**: "
                          + (neg.Length == 0 ? "該当なし" : string.Join("・", neg.Select(u => $"{hcName[u]} {HcP2(hcCost[u])}")))
                          + "。**象限に関係なく数えている**（象限は線 +1.5 の左右で切るので、"
                          + "両方とも強い駒の中にある「外すと弱くなる」を拾わない）。");
        Console.WriteLine();
    }

    // ---- 表D: (ii) の一覧（Q4）--------------------------------------------------------------------
    var iiList = Enumerable.Range(0, hcRN).Where(HcIsII).ToArray();
    Console.WriteLine($"## 表D —— (ii) 分離できない駒（Q4）: **{iiList.Length} / {hcRN} 体**");
    Console.WriteLine();
    Console.WriteLine("**プラスとマイナスが同じ1つの動作**なので、`yP` を作ろうとすると本物と同じものしか作れない"
                      + "（`hcPlusDef` が本物と同じ参照を返す＝**測る前に決まっている**）。指示書 §0-2 の但し書きどおり、"
                      + "**見直し対象とは区別して報告する。1文で読める駒はむしろ良い設計である。**");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 札 | 単独 | 群 | マイナスの持ち方 |");
    Console.WriteLine("|---|---|--:|---|---|");
    foreach (int u in iiList.OrderByDescending(u => hcSolo[u]))
        Console.WriteLine($"| {hcName[u]} | {string.Join("・", hcRoster[u].Traits.Select(t => $"`{t}`"))} | {HcP2(hcSolo[u])} | "
                          + $"{(hcSpecial[u] ? "別扱い" : hcGroupName[hcGrp[u]])} | {hcLabel[hcRoster[u].Traits[0]].Why} |");
    Console.WriteLine();

    // ---- 表E: 理想台との割れ（拒否権1）------------------------------------------------------------
    Console.WriteLine("## 表E —— 理想台との割れ（拒否権1。§0-3 で**ドラフト台を正**と決めた）");
    Console.WriteLine();
    Console.WriteLine("**この期の決定: 差し替えの判定はドラフト台を正とし、理想台は拒否権1 としてのみ使う**（指示書 §0-3 の提案をそのまま採る）。"
                      + "理由——理想台（`CompareBuilds()` 61 行）は**その駒のために組まれた台**なので、"
                      + "「入れる価値があるか」ではなく「最良の相方と組めば働くか」を測っている。**52 枠が埋まっている今、問いは前者である。**");
    Console.WriteLine();
    Console.WriteLine($"割れた駒は **{Enumerable.Range(0, hcRN).Count(u => hcSplitStage[u])} / {hcRN} 体**"
                      + $"（第82期は 15 / 51・うち 14 が「ドラフト台で残さない × 理想台で残す」）。内訳は上の Q5 の表に出してある。");
    Console.WriteLine();

    // ---- Q5: ヴェル -------------------------------------------------------------------------------
    int velIx = Array.FindIndex(hcRoster, d => d.Id == "vel");
    Console.WriteLine("## Q5 —— ヴェル（第118期で測れなかった駒）が情報帯に入る台で測れたか");
    Console.WriteLine();
    if (velIx < 0) Console.WriteLine("**ヴェルがロスターに居ない。**");
    else
    {
        bool velOk = hcCeilPct[velIx] <= HcCeilShare && hcFloorPct[velIx] <= HcCeilShare
                     && Math.Abs(hcSolo[velIx]) > 2 * hcSoloSe[velIx];
        Console.WriteLine($"| 量 | 実測 |");
        Console.WriteLine($"|---|--:|");
        Console.WriteLine($"| 天井%（y00 > {HcCeil:F0}%） | {hcCeilPct[velIx]:F1} |");
        Console.WriteLine($"| 床%（y00 = 0%） | {hcFloorPct[velIx]:F1} |");
        Console.WriteLine($"| 単独 | {HcP2(hcSolo[velIx])}（SE {hcSoloSe[velIx]:F2}） |");
        Console.WriteLine($"| プラス値 | {HcP2(hcPlusVal[velIx])} |");
        Console.WriteLine($"| 群 | {(hcSpecial[velIx] ? "別扱い" : hcGroupName[hcGrp[velIx]])} |");
        Console.WriteLine();
        Console.WriteLine($"**{(velOk ? "○ 測れている" : "× 測れていない（別扱い）")}**"
                          + "——ドラフト台は 128 台の埋め草を無作為に引くので、"
                          + "**第118期の土台（攻撃役に固定した4台）と違って「味方が倒れる台」が必ず混ざる。**");
        Console.WriteLine();
    }

    // ---- 判定 -------------------------------------------------------------------------------------
    Console.WriteLine("## 第119期の判定");
    Console.WriteLine();
    Console.WriteLine("| # | 問い | 実測 | 判定 |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| Q1 | 51 体で第82期の3分と一致（48/51 以上） | {(q1Match < 0 ? "—" : $"{q1Match} / {q1N}")} | "
                      + $"{(q1Match < 0 ? "—" : q1Match >= 48 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q2 | 52 枚の3分・差し替え候補が 1〜15 体 | {nSwap} 体（残す {nKeep} / 転生 {nReborn} / 別扱い {nSpecial}） | {(nSwap >= 1 && nSwap <= 15 ? "○" : "**×**")} |");
    Console.WriteLine($"| **Q3** | **4象限に 52 枚を割る** | {(hasX == 0 ? "**測っていない**" : "上の表C")} | {(hasX == 0 ? "**×**" : "○")} |");
    Console.WriteLine($"| Q4 | (ii) の体数と名前 | **{iiList.Length} / {hcRN} 体** | ○ |");
    Console.WriteLine($"| Q5 | ヴェルが情報帯に入る台で測れたか | {(velIx < 0 ? "—" : $"天井 {hcCeilPct[velIx]:F1} / 床 {hcFloorPct[velIx]:F1}")} | "
                      + $"{(velIx >= 0 && hcCeilPct[velIx] <= HcCeilShare && hcFloorPct[velIx] <= HcCeilShare ? "○" : "**×**")} |");
    Console.WriteLine("| Q6 | `compare` 305 セル 0 件・`docs/` 差分 0 | `checkup check` と `docs/` の再生成で別に確かめる | — |");
    Console.WriteLine();
    Console.WriteLine($"所要 {hcSw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}
}
