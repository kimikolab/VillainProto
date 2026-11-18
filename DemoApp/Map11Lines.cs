using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// セリフの表（第479期 §3・指示書 design/PHASE479_LESS_WINDOWS_SPEC.md）。**文はすべて仮**——後でポンが直す。直すのはこのファイルだけ。
//
// 画面の決まり（§1）: 説明は窓の文ではなく、顔つきの 1 行で。出すのは最初の 1 回だけ・盤の端に 4 秒・押せば消える・時計は止めない。
// ここは「いつ・誰が・何と言うか」の表と、「話す駒が手札にいなければ、いる駒の先頭が話す」だけを持つ。
// いつ出すか（盤のどの出来事か）は画面（`Map11Main.Lean.cs`）が盤に聞いて決める。**Godot の型は 1 つも使わない。**
// =====================================================================================
public static class Map11Lines
{
    /// <summary>セリフ 1 行。<c>Speaker</c> は駒の名前の一部（「リィカ」）。</summary>
    public sealed record Line(string Key, string Stage, string When, string Speaker, string Text);

    public const string Start11 = "1-1.start", Gate11 = "1-1.gate", Start12 = "1-2.start", Pass12 = "1-2.pass", NoPass12 = "1-2.nopass", Keeper12 = "1-2.keeper", Gold = "gold20";

    /// <summary>セリフの表（仮の文）。</summary>
    public static readonly IReadOnlyList<Line> All = new[]
    {
        new Line(Start11, "1-1", "1-1 の始め", "リィカ", "北は近いけど、関が固い。南は遠回りで楽"),
        new Line(Gate11, "1-1", "1-1 で峠の関に触れたとき", "ドルガ", "硬そうだ。……賞金は多いな"),
        new Line(Start12, "1-2", "1-2 の始め", "ガン", "中間拠点を 2 つ押さえりゃ、砦の門が開く"),
        new Line(Pass12, "1-2", "1-2 で関所を取ったとき", "ムド", "ここで止める。拠点までは、来させない"),
        new Line(NoPass12, "1-2", "1-2 で関所を取らずに門が開いたとき", "リリ", "留守、狙われるわよ"),
        new Line(Keeper12, "1-2", "1-2 で守将の前（連戦の札が出たとき）", "リィカ", "削られたら、引いて休める"),
        new Line(Gold, "", "金が 20 貯まったとき（最初の 1 回）", "ガン", "金が余ってる。強化しとけ"),
    };

    // ---- 会戦の台: 味方の先頭の駒が勝ち目に合わせて 1 行話す（第479期の続き 2・文は仮） ----
    public const string OddsGood = "odds.good", OddsEven = "odds.even", OddsBad = "odds.bad", OddsRisk = "odds.risk", OddsRiskSelf = "odds.risk.self";

    /// <summary>勝ち目のセリフ（全員の文）。<c>{0}</c> は倒れやすい駒の名前。</summary>
    public static readonly IReadOnlyDictionary<string, string> OddsLines = new Dictionary<string, string>
    {
        [OddsGood] = "行ける",
        [OddsEven] = "五分だな",
        [OddsBad] = "分が悪い。引くか",
        [OddsRisk] = "{0}は持たないぞ",
        [OddsRiskSelf] = "行けるが、こっちは持たないぞ",
    };

    /// <summary>駒ごとの言い回し（駒の Id → 鍵 → 文）。<b>今は空</b>——全員が <see cref="OddsLines"/> の文で話す。足せばその駒だけ替わる。</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> OddsLinesBy = new Dictionary<string, IReadOnlyDictionary<string, string>>();

    /// <summary>
    /// 勝ち目のセリフを選ぶ。<paramref name="wins"/> ＝ 裏の <paramref name="runs"/> 回の勝ちの数、<paramref name="risky"/> ＝ 倒れやすい駒（倒れた回数の多い順）。
    /// 優勢でも倒れやすい駒がいれば「＜駒の名前＞は持たないぞ」（話す駒自身しかいなければ自分の文）。線は呼ぶ側（画面）の 優勢 ／ 互角 の線をそのまま貰う。
    /// </summary>
    public static (string Key, string Text) OddsLine(UnitDef speaker, int wins, int good, int even, IReadOnlyList<UnitDef> risky)
    {
        string key = wins >= good ? OddsGood : wins >= even ? OddsEven : OddsBad;
        UnitDef? named = null;
        if (key == OddsGood && risky.Count > 0)
        {
            named = risky.FirstOrDefault(d => d.Id != speaker.Id);
            key = named is null ? OddsRiskSelf : OddsRisk;
        }
        string text = OddsLinesBy.TryGetValue(speaker.Id, out var own) && own.TryGetValue(key, out string? t) ? t : OddsLines[key];
        return (key, named is null ? text : string.Format(text, named.Name));
    }

    /// <summary>ステージの頭の札の目標の 1 行（20 字まで）。</summary>
    public static readonly IReadOnlyDictionary<string, string> Goals = new Dictionary<string, string>
    {
        ["1-1"] = "目標　敵の拠点を制圧する",
        ["1-2"] = "目標　中間拠点を 2 つ取り、砦を制圧する",
        ["1-3"] = "目標　砦を制圧する",
    };

    /// <summary>1-3 の頭の札の頭に足す主人公の内心（第478期は 1-2 の踏破の窓にあった予告の 1 行・仮）。</summary>
    public const string Stage13Lead = "砦は取った。——王国は、勇者を差し向けた。";

    // ---- 1-3「狩る者」（第480期・文と名前はすべて仮・表示だけ——規則は 1 つも読まない） ----
    /// <summary>勇者の名前（登場の帯・会戦の台の名札・戦闘の名札）。</summary>
    public const string HunterName = "白雷の勇者　ライゼル";
    /// <summary>盤の上の短い名札。</summary>
    public const string HunterShort = "ライゼル";
    /// <summary>味方が呼ぶ名前。</summary>
    public const string HunterAlias = "狩る者";
    /// <summary>勇者の技の名前（全体攻撃 ／ 毎ターン攻 ＋11）。</summary>
    public const string HunterStrike = "白雷", HunterCharge = "帯電";
    /// <summary>戦闘の名札の下の説明（<c>UnitDef.PlusText</c> の写しに入れる・表示だけ）。</summary>
    public const string HunterPlus = "「白雷」全員を撃つ。「帯電」毎ターン攻撃力が 11 上がる。転ばず痺れない。傷は戦いをまたいで残る";
    /// <summary>狩る者の盤の頭の札（勇者が来ることは言わない——優勢から始まる）。</summary>
    public const string Stage13LeadHunter = "砦は取った。あとは、逃げた残党を片付けるだけだ。";
    public const string GoalHunter = "目標　中間拠点を 2 つ取り、野営地を制圧する";
    /// <summary>登場のセリフ（話す駒がいなければ先頭の駒）と、主人公の内心。</summary>
    public const string HunterWarnSpeaker = "ヒサ", HunterWarn = "……逃げるなら、今だぞ", HunterInner = "捨て駒は、こっちだったか";
    /// <summary>撤退の局面の目標の札（赤）。</summary>
    public const string GoalRout = "目標　生きて拠点へ戻れ";
    /// <summary>会戦の台: 追いつかれた（初回）／ 自分から入った殿。</summary>
    public const string HunterCaught = "……無理だ。引け！", HunterRear = "ここは任せろ。行け！";
    /// <summary>自分から入った殿だが、台の印が「足止めできない」のとき（言っていることと印が食い違わないように）。</summary>
    public const string HunterRearWeak = "長くは持たない。急げ！";
    /// <summary>戦闘から戻った帯（負けたが足を止めた）と、勇者が退いた帯（HP が半分を割った ／ 0 になった——どちらも「退いた」）。</summary>
    public const string RearBand = "殿", HunterGone = "勇者は退いた";

    // ---- 敵の見分け（第480期 §3-5・初めて当たるときに味方が 1 行・仮） ----
    /// <summary>特徴の印の鍵 → セリフ。鍵は <c>雷 ／ 火 ／ 毒 ／ 盾 ／ 弓 ／ 槍</c>。</summary>
    public static readonly IReadOnlyDictionary<string, string> FoeTraitLines = new Dictionary<string, string>
    {
        ["雷"] = "雷だ。固まるな",
        ["火"] = "燃やされるぞ",
        ["毒"] = "毒だ。長引かせるな",
        ["盾"] = "硬い。手数が要る",
        ["弓"] = "後ろを狙ってくる",
        ["槍"] = "押し込まれるな",
    };

    /// <summary>金のセリフを出す金の額。</summary>
    public const int GoldLine = 20;
    /// <summary>吹き出しを出しておく秒数。</summary>
    public const float Seconds = 4f;

    public static Line Of(string key) => All.First(l => l.Key == key);

    /// <summary>話す駒: 手札（隊 → 控えの順）に <paramref name="speaker"/> がいればその駒、いなければ先頭の 1 体（誰もいなければ null）。</summary>
    public static UnitDef? SpeakerOf(Map11State st, string speaker)
    {
        var hand = st.Squads.Where(q => !q.Lost && q.Units is not null).SelectMany(q => q.Units!.Where(u => u.IsAlive).OrderBy(u => u.Slot)).Select(u => u.Def)
            .Concat(st.Bench.Select(u => u.Def)).ToList();
        return hand.FirstOrDefault(d => d.Name.EndsWith(speaker, StringComparison.Ordinal)) ?? hand.FirstOrDefault();
    }

    /// <summary>
    /// 敵部隊の特徴の印の鍵（第480期 §3-5・表示だけ）: 雷 ／ 火 ／ 毒 ／ 槍 ／ 弓 ／ 盾。どれでもなければ空。
    /// 見るのは駒の定義の札と Id だけ（敵の帯電・敵の火の粉・前の瘴気・敵の突き飛ばし ／ 狙撃手 2 枚以上 ／ 重装兵 2 枚以上）。
    /// </summary>
    public static string FoeTraitOf(IEnumerable<UnitDef> units)
    {
        var list = units.ToList();
        bool Has(TraitId t) => list.Any(u => u.Traits.Contains(t));
        if (Has(TraitId.ShockFoe)) return "雷";
        if (Has(TraitId.CinderFoe) || Has(TraitId.Cinder)) return "火";
        if (Has(TraitId.MiasmaFront)) return "毒";
        if (Has(TraitId.ShoveFoe)) return "槍";
        if (list.Count(u => u.Id == EnemyCatalog.Archer.Id || u.Id == EnemyCatalog.FireArcher.Id) >= 2) return "弓";
        if (list.Count(u => u.Id == EnemyCatalog.Warden.Id) >= 2) return "盾";
        return "";
    }

    // ---- 言ったことの覚え（場面を読み直しても残す・1 章を 1-1 から始め直したら捨てる） ----
    private static readonly HashSet<string> Said = new();
    public static bool Mark(string key) => Said.Add(key);
    public static bool WasSaid(string key) => Said.Contains(key);
    public static void Forget() => Said.Clear();
}
