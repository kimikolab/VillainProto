using BattleCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

// =====================================================================================
// 遊びの写し（第466期 §1・指示書 design/PHASE466_TUNING_TOOLS_SPEC.md）
//
// 1 章で遊ぶと、各ステージに入った時点（`_start`）・最初の作戦ターンの直前（`_first`・組み直しの後）・ステージの終わり（`_end`）の
// 持ち越しの写し（`Map11State.CarryPack`）を `design/plays/<1 章の seed>/<ステージ>_<種類>.json` に書く。
// **書くだけで盤を1ビットも動かさない**（`TakeCarry` は盤を読むだけ）。<see cref="Root"/> が null なら何もしない
// ——頭なしの門と器具は書かない（遊ぶ起動の `Main` だけが <see cref="Enable"/> を呼ぶ）。
// 読み戻すのは「写しから 1 章を始める」（`Chapter1.BeginFrom`）と敵の重さの表（`FoeWeight`）。
// **ここには Godot の型を1つも使わない。**
// =====================================================================================

public static class PlayLog
{
    /// <summary>写しの置き場（`design/plays` の絶対パス）。null なら書かない（既定）。</summary>
    public static string? Root { get; private set; }
    public static bool Enabled => Root is not null;

    /// <summary>写しを書き始める（遊ぶ起動の `Main` ・門が自分の置き場で呼ぶ）。</summary>
    public static void Enable(string root) { Root = Path.GetFullPath(root); _readOnly = false; }
    public static void Disable() { Root = null; _readOnly = false; }

    /// <summary>読む置き場だけを決める（絵の口・写しの一覧を出すが書かない——<see cref="BeginChapter"/> がフォルダを持たない）。</summary>
    public static void UseForReading(string root) { Root = Path.GetFullPath(root); _readOnly = true; }
    private static bool _readOnly;

    /// <summary>リポジトリの `design/plays`（DemoApp の 1 つ上）。</summary>
    public static string DefaultRoot(string demoAppDir) => Path.GetFullPath(Path.Combine(demoAppDir, "..", "design", "plays"));

    /// <summary>手で写した写しの置き場（`design/plays/manual`・第461〜464期の観察ログから）。</summary>
    public static string ManualDir(string root) => Path.Combine(root, "manual");

    public const int Format = 1;

    // ---------------- 写しの中身（JSON） ----------------

    /// <summary>駒 1 枚。<c>Slot</c> は隊の席（手札は -1）。<c>Relic</c> は持たせた札の <see cref="TraitId"/> の名前。</summary>
    public sealed class Unit
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Star { get; set; } = 3;
        public int Pips { get; set; }
        public int Slot { get; set; } = -1;
        public string? Relic { get; set; }
    }

    /// <summary>
    /// 持ち越しの写し。<b><c>Gold</c> は「このステージの始めの金を足す前」の金</b>（`CarryPack.Gold` と同じ意味——読み込むと始めの金が足される）。
    /// <c>Stars</c> はキャラごとの☆と途中まで重ねた枚数（☆の無い 1 章は null）。
    /// </summary>
    public sealed class Pack
    {
        public int Gold { get; set; }
        public int SquadCap { get; set; } = 2;
        public List<string> RelicHand { get; set; } = new();
        public List<Unit> Bench { get; set; } = new();
        public List<List<Unit>> Squads { get; set; } = new();
        public Dictionary<string, int[]>? Stars { get; set; }
        /// <summary>第467期: 投資の記録（[ステージ, 軸] を選んだ順・投資の無い 1 章は null）。</summary>
        public List<string[]>? Invest { get; set; }
    }

    /// <summary>戦闘 1 回（結果画面の行と同じもの・`Map11Session.Line`）。</summary>
    public sealed class Battle
    {
        public int No { get; set; }
        public int Turn { get; set; }
        public string Squad { get; set; } = "";
        public string Road { get; set; } = "";
        public string Foe { get; set; } = "";
        public bool Won { get; set; }
        public bool Intercept { get; set; }
        public int Alive { get; set; }
        public double Hp { get; set; }
        /// <summary>第467期 §3: 味方の雷・粉で倒れた枚数（0 なら書かない）。</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int AllyShock { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public sealed class Snap
    {
        public int Format { get; set; } = PlayLog.Format;
        /// <summary>start（入った時点）・first（最初の作戦ターンの直前）・end（ステージの終わり）。</summary>
        public string Kind { get; set; } = "start";
        public int ChapterSeed { get; set; }
        public string Stage { get; set; } = "";
        public int StageIndex { get; set; }
        /// <summary>そのステージの何回目の配りか（0 から）。</summary>
        public int Attempt { get; set; }
        public int StageSeed { get; set; }
        /// <summary>1 章の規則の版（`ChapterRule.Versions` のキー・「V465」）。</summary>
        public string Rule { get; set; } = "";
        public string RuleName { get; set; } = "";
        /// <summary>そのステージの敵の倍率（表示の文）。</summary>
        public string Scale { get; set; } = "";
        public string Written { get; set; } = "";
        /// <summary>真なら <see cref="Pack"/> を持ち越しとして載せて始める（1 章の 1-1 の自動の写しは偽——配りから始める）。</summary>
        public bool Entry { get; set; }
        public Pack? Pack { get; set; }
        /// <summary>写した時点の金（画面の上の金）。</summary>
        public int GoldNow { get; set; }
        public string? Result { get; set; }
        public int Retries { get; set; }
        public List<Battle>? Battles { get; set; }
        /// <summary>注（手で写した写しの出どころ・推定したこと）。</summary>
        public List<string> Notes { get; set; } = new();
        /// <summary>推定した駒・値（手で写した写しだけ・「☆は推定（☆3）」など）。</summary>
        public List<string> Estimated { get; set; } = new();

        /// <summary>読んだファイル（書かない）。</summary>
        [JsonIgnore] public string Path { get; set; } = "";

        /// <summary>一覧の 1 行（「186678 ・ 1-3 ・ 始め ・ 第464期 ・ 隊 4 ・ 駒 34 ・ 金 61」）。</summary>
        [JsonIgnore]
        public string Label
        {
            get
            {
                string kind = Kind switch { "start" => "入った時点", "first" => "1 刻目の前（組み直しの後）", "end" => "終わり", _ => Kind };
                string body = Pack is { } p
                    ? $"隊 {p.Squads.Count(s => s.Count > 0)} ・ 駒 {p.Bench.Count + p.Squads.Sum(s => s.Count)} ・ 金 {p.Gold}"
                    : "配りから";
                string rule = RuleName.Split('（')[0];
                return $"{ChapterSeed} ・ {Stage} ・ {kind} ・ {rule} ・ {body}{(Estimated.Count > 0 ? " ・ 手で写した（推定あり）" : "")}";
            }
        }
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ---------------- CarryPack ⇔ Pack ----------------

    /// <summary>持ち越しを写しにする（<paramref name="goldOffset"/> を金から引く——始めの金を足した後の盤から写すとき）。</summary>
    public static Pack FromCarry(Map11State.CarryPack p, StarRule stars, int goldOffset = 0)
    {
        Unit U(Map11State.CarryUnit c)
        {
            UnitDef b = stars.BaseOf(c.Def);
            StarState s = p.Stars is { } ps && ps.TryGetValue(b.Id, out StarState x) ? x : new StarState(stars.On ? stars.Max : 3, 0);
            return new Unit { Id = b.Id, Name = b.Name, Star = s.Star, Pips = s.Pips, Slot = c.Slot, Relic = c.Relic?.ToString() };
        }
        return new Pack
        {
            Gold = p.Gold - goldOffset,
            SquadCap = p.SquadCap,
            RelicHand = p.RelicHand.Select(r => r.ToString()).ToList(),
            Bench = p.Bench.Select(U).ToList(),
            Squads = p.Squads.Select(s => s.Select(U).ToList()).ToList(),
            Stars = p.Stars?.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => new[] { kv.Value.Star, kv.Value.Pips }),
            Invest = p.Invest?.Select(x => new[] { x.Stage, x.Axis }).ToList(),   // 第467期
        };
    }

    /// <summary>駒の定義を引く（Id → 現役 → 退いた駒 → 名前）。見つからなければ例外。</summary>
    public static UnitDef BaseDef(Unit u)
        => UnitCatalog.All.FirstOrDefault(d => d.Id == u.Id)
           ?? UnitCatalog.Everyone.FirstOrDefault(d => d.Id == u.Id)
           ?? UnitCatalog.Everyone.FirstOrDefault(d => d.Name == u.Name)
           ?? throw new InvalidDataException($"駒が見つからない: {u.Id} {u.Name}");

    public static TraitId? RelicOf(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (Enum.TryParse(s, out TraitId t) && RelicCatalog.IsRelic(t)) return t;
        string name = s.TrimStart('◆');
        return RelicCatalog.All.FirstOrDefault(r => r.Name == name)?.Id ?? throw new InvalidDataException($"レリックが見つからない: {s}");
    }

    /// <summary>
    /// 写しを、<paramref name="stage"/>（☆の規則を持つ遊ぶ版）の持ち越しに戻す。☆の無いステージなら駒は今の定義のまま（☆は捨てる）。
    /// ☆のあるステージで写しに☆が無ければ（第461〜462期）☆3 とする。<paramref name="relics"/> が偽なら札を外す（器具だけ）。
    /// </summary>
    public static Map11State.CarryPack ToCarry(Pack p, StarRule stars, bool relics = true)
    {
        var map = new Dictionary<string, StarState>();
        if (stars.On && p.Stars is { } ps)
            foreach (var (id, v) in ps) map[id] = new StarState(Math.Clamp(v[0], 1, stars.Max), v.Length > 1 ? v[1] : 0);
        Map11State.CarryUnit C(Unit u)
        {
            UnitDef b = BaseDef(u);
            if (stars.On && !map.ContainsKey(b.Id)) map[b.Id] = new StarState(Math.Clamp(u.Star, 1, stars.Max), u.Pips);
            UnitDef d = stars.On ? stars.Def(b, map[b.Id].Star) : b;
            return new Map11State.CarryUnit(d, u.Slot, relics ? RelicOf(u.Relic) : null);
        }
        var bench = p.Bench.Select(C).ToList();
        var squads = p.Squads.Select(s => (IReadOnlyList<Map11State.CarryUnit>)s.Select(C).ToList()).ToList();
        var hand = relics ? p.RelicHand.Select(r => RelicOf(r)!.Value).ToList() : new List<TraitId>();
        // 第467期: 投資の記録（投資の無いステージに載せると `ApplyCarry` が捨てる）。
        var invest = p.Invest?.Where(x => x.Length >= 2).Select(x => (x[0], x[1])).ToList();
        return new Map11State.CarryPack(bench, squads, p.SquadCap, p.Gold, hand, stars.On ? map : null, invest);
    }

    /// <summary>持ち越しの並べ方（第461期の門 (c)(d) と同じ並べ方 ＋ ☆）。2 つの持ち越しが同じ中身かを比べる。</summary>
    public static string PackText(Map11State.CarryPack p)
    {
        static string U(Map11State.CarryUnit u) => u.Def.Id + (u.Relic is { } r ? "◆" + r : "");
        string stars = p.Stars is null ? "なし" : string.Join(",", p.Stars.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}{kv.Value.Star}.{kv.Value.Pips}"));
        return $"手札[{string.Join(",", p.Bench.Select(U).OrderBy(x => x, StringComparer.Ordinal))}]"
               + $" 隊[{string.Join(" | ", p.Squads.Select(s => string.Join(",", s.OrderBy(u => u.Slot).Select(u => $"{u.Slot}:{U(u)}"))))}]"
               + $" 枠{p.SquadCap} 金{p.Gold} 手持ち[{string.Join(",", p.RelicHand)}] ☆[{stars}]"
               + $" 定義[{string.Join(",", p.Bench.Concat(p.Squads.SelectMany(s => s)).Select(u => $"{u.Def.Id}:{u.Def.MaxHp}/{u.Def.Attack}").OrderBy(x => x, StringComparer.Ordinal))}]"
               + (p.Invest is { } inv ? $" 投資[{string.Join(",", inv.Select(x => $"{x.Stage}:{x.Axis}"))}]" : "");   // 第467期（投資の無い 1 章は第466期のまま）
    }

    // ---------------- 読む・書く ----------------

    public static Snap Read(string path)
    {
        Snap s = JsonSerializer.Deserialize<Snap>(File.ReadAllText(path), Json) ?? throw new InvalidDataException($"写しが読めない: {path}");
        s.Path = Path.GetFullPath(path);
        return s;
    }

    public static void Write(string path, Snap s)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(s, Json) + "\n");
    }

    /// <summary>置き場の下の写しを全部（新しい順）。<paramref name="kinds"/> を渡せばその種類だけ。読めないファイルは飛ばす。</summary>
    public static List<Snap> List(string? root = null, params string[] kinds)
    {
        root ??= Root;
        var list = new List<Snap>();
        if (root is null || !Directory.Exists(root)) return list;
        foreach (string f in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                Snap s = Read(f);
                if (kinds.Length == 0 || kinds.Contains(s.Kind)) list.Add(s);
            }
            catch (Exception) { }
        }
        return list.OrderByDescending(s => File.GetLastWriteTimeUtc(s.Path)).ThenBy(s => s.Path, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// 「写しから始める」の一覧（入った時点 ・ 1 刻目の前）。並びは<b>遊んだ写し（書いた時刻の新しい順・ステージの順）→ 手で写した写し（期の新しい順・ステージの順）</b>
    /// ——先頭が「1 章（写しから）」を選んで入ったときに後ろに出す写し。
    /// </summary>
    public static List<Snap> Starts(string? root = null)
    {
        static int Phase(Snap s) => int.TryParse(Path.GetFileName(s.Path).Split('_')[0], out int n) ? n : 0;
        return List(root, "start", "first")
            .OrderBy(s => s.Estimated.Count == 0 ? 0 : 1)
            .ThenByDescending(s => s.Estimated.Count == 0 ? Directory.GetLastWriteTimeUtc(Path.GetDirectoryName(s.Path)!) : default)
            .ThenByDescending(Phase).ThenBy(s => s.StageIndex).ThenBy(s => s.Kind == "first" ? 1 : 0).ToList();
    }

    // ---------------- 1 章から呼ばれる口（Chapter1 ／ Map11Session だけが呼ぶ） ----------------

    /// <summary>いまの 1 章の写しのフォルダ（<see cref="BeginChapter"/> が決める）。</summary>
    public static string? ChapterDir { get; private set; }

    /// <summary>
    /// 1 章を始めた（`Chapter1.Begin` ／ `BeginFrom`）。フォルダは `<seed>`（もう写しがあれば `<seed>_<時刻>`）、写しから始めた 1 章は
    /// `<seed>_from<ステージ>_<時刻>`——<b>前の遊びの写しを上書きしない</b>。
    /// </summary>
    public static void BeginChapter(int seed, string? fromStage = null)
    {
        ChapterDir = null;
        if (Root is null || _readOnly) return;
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string name = fromStage is null ? $"{seed}" : $"{seed}_from{fromStage}_{stamp}";
        string dir = Path.Combine(Root, name);
        if (fromStage is null && Directory.Exists(dir) && Directory.EnumerateFiles(dir).Any()) dir = Path.Combine(Root, $"{seed}_{stamp}");
        ChapterDir = dir;
    }

    private static Snap Head(string kind, Map11State st)
        => new()
        {
            Kind = kind,
            ChapterSeed = Chapter1.Seed,
            Stage = st.Stage.Id,
            StageIndex = Chapter1.Index,
            Attempt = Chapter1.Attempt,
            StageSeed = st.Seed,
            Rule = Chapter1.Rule.Key,
            RuleName = Chapter1.Rule.Name,
            Scale = st.Stage.FoeScale.Text + (st.Stage.FoeScale.HeroMends ? "" : "（勇者の回復なし）"),
            Written = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            GoldNow = st.Gold,
        };

    private static void Put(Snap s) { if (ChapterDir is { } d) Write(Path.Combine(d, $"{s.Stage}_{s.Kind}.json"), s); }

    /// <summary>ステージに入った（持ち越しを載せた直後・作戦ターンを 1 度も進めていない）。やり直すと同じファイルを書き直す（配りの回数が変わる）。</summary>
    public static void OnStart(Map11State st, Map11State.CarryPack? entry)
    {
        if (ChapterDir is null) return;
        Snap s = Head("start", st);
        s.Entry = entry is not null;
        s.Pack = entry is not null ? FromCarry(entry, st.Stage.Stars) : FromCarry(st.TakeCarry(), st.Stage.Stars, st.Stage.Gold.On ? st.Stage.Gold.Start : 0);
        Put(s);
    }

    /// <summary>最初の作戦ターンの直前（`Map11Session.TakeOpening`・組み直しの窓で呼んだ駒を含む）。</summary>
    public static void OnFirst(Map11State st)
    {
        if (ChapterDir is null || !Chapter1.Active) return;
        Snap s = Head("first", st);
        s.Entry = true;
        s.Pack = FromCarry(st.TakeCarry(), st.Stage.Stars, st.Stage.Gold.On ? st.Stage.Gold.Start : 0);
        s.Notes.Add("最初の作戦ターンの直前の写し（組み直しの窓で呼んだ駒・並べ替えを含む）。金は「このステージの始めの金を足す前」に直してある");
        Put(s);
    }

    /// <summary>ステージの終わり（`Chapter1.Settle`）。持ち越し ＋ 戦闘の記録。</summary>
    public static void OnEnd(Map11State st, IReadOnlyList<Map11Session.Line> log, Chapter1.Record r)
    {
        if (ChapterDir is null) return;
        Snap s = Head("end", st);
        s.Entry = true;
        s.Pack = FromCarry(st.TakeCarry(), st.Stage.Stars);
        s.Result = r.Result;
        s.Retries = r.Retries;
        s.Battles = log.Select(l => new Battle
        {
            No = l.No, Turn = l.Turn, Squad = l.Squad, Road = l.Road, Foe = l.Foe, Won = l.Won, Intercept = l.Intercept,
            Alive = l.Alive, Hp = Math.Round(l.HpPercent, 1), AllyShock = l.AllyShock, Notes = l.Notes.ToList(),
        }).ToList();
        Put(s);
    }
}
