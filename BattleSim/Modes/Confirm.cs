using BattleCore;
using static Common;

// =====================================================================================
// confirm モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "confirm")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 confirm
// =====================================================================================

static class ConfirmDiag
{
// layout モード: compare の各編成についてメンバー固定のまま6スロットへの全配置を試し、
// 全ステージ平均勝率で並べる。「この編成をどう置くか」を人手の勘で決めないための道具。
// 編成名が示す狙い（隣接ペア・後列必須など）との突き合わせは人がやる。上位だけでなく
// 現行配置の順位も出すのはそのため。
// reseat モード: 指定した編成だけを全配置に展開し、候補を seed 200 で測り直す。
// layout の上位5件は seed 50 の値で並んでいるうえ、「ガルドは前列」「セッキは後列」のような
// 狙いの制約を無視するので、制約下の最良が表に載らないことがある。
// ここでは 全体上位 / 制約を満たす上位 / 現行 を混ぜたプールを作り、seed 200 で並べ直す。
// confirm モード: 配置差し替えの採否を「選定に使っていない seed」で確かめる。
// reseat の値は 20〜30 件の候補から最大を採ったものなので、選択バイアスで必ず上振れする。
// 実際 2026-08-21 の差し替えでは 逆しま+後備え が in-sample +0.9pt → out-of-sample -0.1pt と符号ごと反転し、
// この1件だけ不採用になった。旧配置もここに直書きしてあるのは、採用後に走らせても
// 全部 0 になって記録として役に立たなくなるのを避けるため。
public static void Run(string[] args, int stageIndex)
{
    IReadOnlyList<EnemyCatalog.Stage> stages = EnemyCatalog.Stages;
    const int Base = 200, Seeds = 400;   // 選定に使った seed 0..199 とは重ならない範囲

    // **閾値は第46期に 2.0 → 5.0 へ上げた。** 第45期の `seats2` を全48行で測ると、
    // `reseat` の**1位と5位の差**（＝上位帯の内部変動）は 中央値 2.15pt・Q3 4.65pt で、
    // **2.0pt は 48行中 26行で上位帯の内部変動より小さい**——ほぼノイズを閾値にしていた。
    // 5.0pt は 38/48 行でその内部変動の外側に出る（1位−5位が 5pt 未満の行が 38）。
    // 併せて、採否は**この閾値を通った差**ではなく**上位5通りの次数**で読むこと（第45期の残件 D。
    // 1位そのものは別 seed 帯で 48行中28行で入れ替わるが、次数の一致率は 98%）。
    const double Threshold = 5.0;        // これ未満は誤差とみなして据え置く

    // (編成名, 旧配置, 候補配置)。候補は reseat の「狙いを満たす最良」。
    //
    // **いまここに載っているのは、席番号タイブレークの乱数化後に全32編成を reseat し直して
    // 出た唯一の候補（差 2pt 以上）で、追試の結果は +1.4pt の「据え置き」。**
    // つまり乱数化の前後で採るべき配置は変わらなかった、という記録そのもの。
    // X字化(盤面の対称化)に伴う振り直しぶん。旧盤面の座標で書かれた過去の追試は
    // 座標ごと無効になったので、ここでは持ち越していない。
    var picks = new (string Name, Formation Old, Formation New)[]
    {
        ("反撃改 (ドハ×カド)",
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Kado, center: UnitCatalog.Doha, back1: UnitCatalog.Nel, back3: UnitCatalog.Nono),
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Nel, center: UnitCatalog.Kado, back1: UnitCatalog.Doha, back3: UnitCatalog.Nono)),
        // 置き去り（ナラ）の新2編成ぶん。仮置きは「ナラを中央」だったが、reseat の 120通り全探索で
        // 中央はカドの席だと出た（被弾強化側は 42.1% → 91.7%）。速攻側は最良でも +1.4pt で、
        // 仮置きとの差が閾値未満（この2本を1回の追試で並べるために、据え置き側も載せてある）。
        ("置き去り×被弾強化",
            Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Golm, center: UnitCatalog.Nara, back1: UnitCatalog.Kado, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nara, center: UnitCatalog.Kado, back1: UnitCatalog.Mudo, back3: UnitCatalog.Vel)),
        // **この編成は第21期に compare から外した**（100/0/0/0/0 で情報が出ていなかった）。
        // 行は記録として残す——消すと「追試して据え置いた」事実まで消える。
        ("置き去り×速攻",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Borg, center: UnitCatalog.Nara, back1: UnitCatalog.Tou, back3: UnitCatalog.Sasa),
            Formation.Build(front1: UnitCatalog.Tou, front3: UnitCatalog.Sasa, center: UnitCatalog.Sero, back1: UnitCatalog.Borg, back3: UnitCatalog.Nara)),
        // route 診断（第19期）の V4。自傷の燃料をムドの被弾強化まで通す配置で、
        // seed 0..199 では -1.5pt と閾値の内側に入った。**閾値の境目なので追試が要る。**
        // reseat と違って勝率の探索から出た候補ではなく、「巨躯の被覆から出す」という
        // 人間側の狙いから組んだ席なので、採否は差の符号ではなく安定性で読む。
        ("置き去り×被弾強化 (route V4)",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nara, center: UnitCatalog.Kado, back1: UnitCatalog.Mudo, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Vel, front3: UnitCatalog.Nara, center: UnitCatalog.Kado, back1: UnitCatalog.Golm, back3: UnitCatalog.Mudo)),
        // 第20期の新1編成。仮置き（ナラ中央）は 86.8% で、reseat 1位はヴェルを中央に上げる形。
        // 「置き去り×被弾強化」で中央がカドの席だったのと同じで、**ナラは席を選ばない**
        // （速さで対象を選ぶので隣接も列も見ない）から、中央を要求する駒に譲るのが正しい。
        ("置き去り×死の連鎖",
            Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Mug, center: UnitCatalog.Nara, back1: UnitCatalog.Rica, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Nara, center: UnitCatalog.Vel, back1: UnitCatalog.Rica, back3: UnitCatalog.Mug)),
        // 第21期の差し替え行。仮置きは swap S4 の席そのまま（中央ナラ・34.4%）で、
        // reseat 1位はセロを中央へ上げてナラを後1へ下げる形。3期続けて同じ結論——
        // **ナラは席を選ばない**（速さで対象を選ぶので隣接も列も見ない）ので、
        // 中央を要求する駒に譲るのが正しい。ここでは狙撃のセロが中央に上がる。
        ("置き去り×分散回復",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Gald, center: UnitCatalog.Nara, back1: UnitCatalog.Dolga, back3: UnitCatalog.Sasa),
            Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Gald, center: UnitCatalog.Sero, back1: UnitCatalog.Nara, back3: UnitCatalog.Dolga)),
        // 物理軸の連鎖・第1弾の新3編成（第26期）。旧＝計画書の仮置き（メンバーは組み直し後で同じ）、
        // 候補＝reseat 1位。3本とも狙い（ガルド前列）を満たす席が最良だったので、
        // 「狙いを満たす最良」と全体1位が食い違う行は無い。
        ("責め苦 (トウ×シガ)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Tou, center: UnitCatalog.Shiga, back1: UnitCatalog.Gan, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Tou, front3: UnitCatalog.Gald, center: UnitCatalog.Gan, back1: UnitCatalog.Shiga, back3: UnitCatalog.Dolga)),
        // ヒサは中央でなくてよい、と出た行。中央に置くと隣接次数4で最大HPのガルドが確実に
        // 標的になるが、reseat 1位はガンを前3へ上げてドルガを後3へ下げる形（標的はガルドのまま）。
        ("仇討ち (ヒサ×ザン)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Hisa, back1: UnitCatalog.Zan, back3: UnitCatalog.Gan),
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gan, center: UnitCatalog.Hisa, back1: UnitCatalog.Zan, back3: UnitCatalog.Dolga)),
        // 破片の検証台。候補ではヒビが中央（範囲の集まる席）、ゴルムが前3で後方を被覆し、
        // ザンは後3——**ザンが殴られにくい席ほど刃が出る**という読みと一致する。
        // 標的はヒサ(前1)の隣接＝中央ヒビ(55)と後1ドルガ(85)の最大でドルガに移る。
        ("仇討ち×砕け (ヒビ×ザン)",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Hibi, center: UnitCatalog.Hisa, back1: UnitCatalog.Zan, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Golm, center: UnitCatalog.Hibi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Zan)),
        // ザンの「1ターンに1回」撤去に伴う振り直し（第26期の追補）。**規則を変えたら席も測り直す**
        // ——上限があった頃は「殴られる回数」が出力に乗らなかったので、標的を誰に付けるかの
        // 価値が潰れていた。撤去後は**巨躯ゴルム(150)を中央に置いてそこへ標的を集める**形が
        // 最良になる（ヒサは前1で隣接＝中央ゴルムと後1ドルガ、最大HPはゴルム）。
        // 仇討ち (ヒサ×ザン) の方は撤去後も現行が「狙いを満たす最良」のままなので候補なし。
        ("仇討ち×砕け (ヒビ×ザン) / 上限撤去後",
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Golm, center: UnitCatalog.Hibi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Zan),
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Hibi, center: UnitCatalog.Golm, back1: UnitCatalog.Dolga, back3: UnitCatalog.Zan)),
        // 物理軸の連鎖・第2弾の新2編成（第28期）。旧＝計画書の仮置き（顔ぶれは組み直し後で同じ）、
        // 候補＝reseat 1位。**どちらもガルド・セッキを含まないので狙いの制約が無く、
        // 「狙いを満たす最良」と全体1位が一致する。**
        //
        // 裂き の旧は「ゴルム前3・ドルガ中央のまま キリとエグだけを入れ替えた形」。
        // **速さの順序は入れ替えても崩れない**（12 対 6 で決まり、隣接も列も見ない）のに
        // +15.1pt 動く——効いているのは順序ではなく受けの配り方で、キリが前1の的になり
        // エグが後3のゴルムの被覆に入る側が上。**「席を選ばない駒」でも席で15pt動く。**
        ("裂き (キリ×エグ)",
            Formation.Build(front1: UnitCatalog.Egu, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Kiri),
            Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Egu)),
        // 中央をヴェル（蘇生。守られて完走する側）に譲る形が最良で +31.5pt。
        // **リィカもエグも中央を要求しない**——墓守は死んだ味方の数だけを読み、抉りは
        // 傷を持つ敵を読むので、どちらも隣接も列も見ない。中央を要求しない駒が並んだら、
        // 完走することに価値がある駒に譲る（第20期・第21期のナラと同じ結論の3例目）。
        ("裂き×責め苦 (キリ×エグ×シガ)",
            Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Kiri, center: UnitCatalog.Rica, back1: UnitCatalog.Vel, back3: UnitCatalog.Egu),
            Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Kiri, center: UnitCatalog.Vel, back1: UnitCatalog.Rica, back3: UnitCatalog.Egu)),
        // 傷軸・第3弾の新2編成（第30期）。旧＝仮置き（ノミを前1に出した形）、候補＝reseat 1位。
        // どちらもガルド・セッキを含まないので「狙いを満たす最良」と全体1位が一致する。
        //
        // **どちらの候補もノミが中央**。刻みは供給と変換を1手に畳んでいるので隣接も列も読まないが、
        // **執着（対象選択の束縛）は「殴り続けられること」が価値**なので、中央＝次数4の席で
        // 巨躯ゴルムの被覆に入って長く立つ側が上に来る。第20期・第21期の「中央を要求しない駒が
        // 並んだら完走する側に譲る」の系列だが、**譲られる理由が蘇生ではなく執着**なのが新しい。
        ("刻み (ノミ単騎)",
            Formation.Build(front1: UnitCatalog.Nomi, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Gan),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gan, center: UnitCatalog.Nomi, back1: UnitCatalog.Vel, back3: UnitCatalog.Dolga)),
        ("刻み×抉り (ノミ×エグ)",
            Formation.Build(front1: UnitCatalog.Nomi, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Egu),
            Formation.Build(front1: UnitCatalog.Egu, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Vel)),
        // 傷軸・第4弾の試験台2本（第37期）。旧＝仮置き（既存行のエグ1枚をナタに差し替えただけ）、
        // 候補＝reseat 1位。どちらもガルド・セッキを含まないので「狙いを満たす最良」と全体1位が一致する。
        //
        // **どちらの候補もナタを前列へ出す形。** ナタは傷を追って標的を選ぶので隣接も列も読まないが、
        // 第30期の「中央を要求しない駒は完走する側に譲る」とは逆に出た——ナタは**傷持ちがいない間は
        // 手番を捨てる**ので、後列で長く立っても振る回数が増えない。増えるのは供給が回っている
        // 時間の側で、そこは書き手（キリ・ノミ）の生存で決まる。
        ("断ち (キリ×ナタ)",
            Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Nata),
            Formation.Build(front1: UnitCatalog.Nata, front3: UnitCatalog.Golm, center: UnitCatalog.Vel, back1: UnitCatalog.Kiri, back3: UnitCatalog.Dolga)),
        // 候補は**第38期の reseat 1位**（74.3%）に差し替えた。閾値待ちを入れて盤面が動いたので、
        // 第37期の候補（ヴェル↔ノミ の入れ替わった形・72.5%）はもう1位ではない。
        ("刻み×断ち (ノミ×ナタ)",
            Formation.Build(front1: UnitCatalog.Nata, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nata, center: UnitCatalog.Dolga, back1: UnitCatalog.Nomi, back3: UnitCatalog.Vel)),
        // 傷軸・第5弾の試験台2本（第39期）。旧＝仮置き（既存行のエグ1枚をハリに差し替えただけ）、
        // 候補＝reseat 1位。どちらもガルド・セッキを含まないので「狙いを満たす最良」と全体1位が一致する。
        //
        // **どちらの候補もハリを後1へ、ゴルムを前1へ。** ナタ（第37期）が前列へ出る形だったのと
        // 逆を向く——ハリは**傷持ちがいなくても普通に殴る**ので、後列で長く立つほど繕いの機会が増える。
        // 「手番を捨てない読み手」は第30期のノミと同じ側（完走する価値がある駒）に戻る。
        ("裂き×縫い (キリ×ハリ)",
            Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Hari),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Kiri, center: UnitCatalog.Dolga, back1: UnitCatalog.Hari, back3: UnitCatalog.Vel)),
        ("刻み×縫い (ノミ×ハリ)",
            Formation.Build(front1: UnitCatalog.Hari, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nomi, center: UnitCatalog.Dolga, back1: UnitCatalog.Hari, back3: UnitCatalog.Vel)),
        // 移動軸・弱体化軸の試験台（第41期）。旧＝仮置き（ハネを前3・ウツを中央）、候補＝reseat 1位。
        //
        // **候補はハネを後3の角へ下げる形。** 効果Bは隣接する生存味方**全員**の攻撃力を引くので、
        // 隣接次数がそのまま値段になる（角2体・中央4体）。しかも候補の席では
        // ハネの隣が**ガルド（Stoic で弾かれる＝代金ゼロ）とウツ（弱体化を3倍で利益に変える）**
        // の2体だけになり、**払う相手が1体もいない**。指示書 §6-2 が「ガルドが答えとして
        // 安すぎないか」を疑った形が、そのまま探索1位として出てきた。
        ("突き返し (ハネ×ウツ)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Hane, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Basa),
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane)),
        // 第42期の台。仮置き（ガルド前1・ウツ前3・ドハ中央）は reseat 12位 63.0%、
        // 候補は reseat 1位 73.1%（ウツを前1へ、ドハを後1へ、ノノを中央へ）。
        ("分かち×逆しま (ドハ×ウツ)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Utsu, center: UnitCatalog.Doha, back1: UnitCatalog.Dolga, back3: UnitCatalog.Nono),
            Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald, center: UnitCatalog.Nono, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga)),
        // 第42期の集約。仮置き（ウケを前1＝台のウツの席）は reseat 21位 43.5%、
        // 候補は reseat 1位 56.0%（ウケを**中央＝隣接次数4**へ）。予測5の検証点。
        ("引き受け (ウケ×ドハ)",
            Formation.Build(front1: UnitCatalog.Uke, front3: UnitCatalog.Gald, center: UnitCatalog.Nono, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald, center: UnitCatalog.Uke, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga)),
        // 第43期の転嫁。仮置き（集約行と同じ席＝ワタ中央）は reseat 2位 77.6%、
        // 候補は reseat 1位 79.1%（ガルドとノノ／ドハとドルガをそれぞれ入れ替えた鏡像）。
        // **どちらもワタは中央**——上位8通りが全部ワタ中央で、角に落ちるのは19位から。
        ("渡し (ワタ×ドハ)",
            Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald, center: UnitCatalog.Wata, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Nono, center: UnitCatalog.Wata, back1: UnitCatalog.Dolga, back3: UnitCatalog.Doha)),
        // 驕り（第46期）。**新しい作法の1件目**——採否は「現行が `reseat` の上位5通りに入っているか」で決め、
        // 入っていない行だけを追試する（作法2）。`驕り (オゴ×ウケ)` は現行が 4位 なので候補なし。
        //
        // `驕り改 (オゴ×ウツ)` は現行（オゴ中央）が 21通り中 17位。上位5通りは**全部
        // 「ウツが中央・オゴが角」**で、値は 92.7 / 92.1 / 91.5 / 91.5 / 91.4 と 1.3pt の中に固まっている
        // ——**次数は一意（オゴ 2・ウツ 4）だが、その中のどれを採るかは測っても決まらない。**
        // 候補にはその帯の1位を置く（帯の中のどれでも同じ、という記録のためにこの注を残す）。
        //
        // **上位帯ではオゴが必ずウツの隣に来る**（角は必ず中央と隣接し、中央がウツだから）。
        // つまり採用される配置ではプラス側（2倍）が1回も発火しない——第46期 §3 を参照。
        ("驕り改 (オゴ×ウツ)",
            Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald, center: UnitCatalog.Ogo, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Doha, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Ogo)),
    };

    Console.WriteLine("## 採用候補の追試");
    Console.WriteLine();
    Console.WriteLine($"seed {Base}..{Base + Seeds - 1} の {Seeds} 試行。選定に使った seed 0..199 とは重ならない。");
    Console.WriteLine($"差が {Threshold:F0}pt 未満なら誤差とみなして据え置く。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 旧配置 | 候補 | 差 | 採否 |" + string.Concat(stages.Select((_, i) => $" 第{i + 1}波差 |")));
    Console.WriteLine("|---|--:|--:|--:|:-:|" + string.Concat(stages.Select(_ => "---:|")));

    foreach (var (name, oldF, newF) in picks)
    {
        double[] o = stages.Select(st => Rate(oldF, st.Enemy)).ToArray();
        double[] n = stages.Select(st => Rate(newF, st.Enemy)).ToArray();
        double gap = n.Average() - o.Average();
        Console.WriteLine($"| {name} | {o.Average():F1}% | {n.Average():F1}% | {gap:+0.0;-0.0}pt | {(gap >= Threshold ? "採用" : "据え置き")} |"
            + string.Concat(Enumerable.Range(0, stages.Count).Select(i => $" {n[i] - o[i]:+0.0;-0.0} |")));
        Console.Out.Flush();
    }
    return;

    double Rate(Formation f, Formation enemy)
    {
        int wins = 0;
        for (int seed = Base; seed < Base + Seeds; seed++)
            if (BattleEngine.Run(f, enemy, seed, verbose: false).PlayerWon) wins++;
        return wins * 100.0 / Seeds;
    }
}
}
