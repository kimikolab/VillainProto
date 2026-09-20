using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 演出の穴の棚卸し（第171期 部C）—— **調べて表にするだけ。盤面も画面も1ビットも動かさない。**
//
// 第170期の観察ログ:
//   「**追加攻撃の演出を入れたおかげかヨミが目立っていた**」
//   「**ハネが活躍している印象は無い。バサも敵を動かす**ので区別が付かない」（問い4）
//
// **同じ盤面の出来事でも、絵が付いた駒だけが認識される。** どの駒のどの働きに絵が無いかを
// 一覧にして、本番の演出（Codex の担当）への発注表にする。
//
// 出どころは3つとも**実測か走査**:
//   発火の頻度       3 隊 × 4 区画を各 N 戦、`verbose: true` で回した台本の実数
//   台本にあるか     その駒が `ActorId` として出した `BattleEventKind` の集合（実測）
//   専用の絵があるか `DemoApp/Main.cs` の `case BattleEventKind.X:` から
//                    `_battleField.<メソッド>` を走査（引けなければ「該当なし」と区別できないので止める）
//
// **手書きは 0 行。** ただし**札（`TraitId`）と出来事（`BattleEvent`）を結ぶ列は作れない**
// ——台本は `ActorId`（駒）しか運んでおらず、どの札がその出来事を起こしたかは載っていない。
// この期はそれを埋めない（部C は発注表であって、engine を触る期ではない）。
// =====================================================================================

public static class Map11ArtGap
{
    /// <summary>
    /// 「その駒らしさ」に数えない種類。<b>振れば誰でも出る</b>ので、これだけの駒は
    /// 画面の上で互いに区別が付かない（第123期の盲点表と同じ線引き）。
    /// </summary>
    private static readonly BattleEventKind[] Generic =
    {
        BattleEventKind.TurnStart, BattleEventKind.Attack, BattleEventKind.Damage,
        BattleEventKind.StatSnapshot, BattleEventKind.StatusSnapshot, BattleEventKind.Death,
    };

    private sealed class Acc
    {
        public readonly Dictionary<BattleEventKind, long> Kinds = new();
        public long Swings, Battles;
    }

    public static bool Run(Action<string> write, string mainSource, int battlesPerPair = 20)
    {
        write("# 第171期 部C —— 演出の穴の棚卸し");
        write("");

        if (mainSource.Length == 0)
        {
            write("**走査が空**: `DemoApp/Main.cs` を読めなかった。"
                + "「該当なし」と区別が付かないのでここで止める（R034）。");
            return false;
        }

        // ---- 1) 再生側の絵（`Main.ApplyEvent` の走査） ----
        Dictionary<string, List<string>> art = ScanReplay(mainSource);
        if (art.Count == 0)
        {
            write("**走査が空**: `case BattleEventKind.` を1件も引けなかった。ここで止める（R034）。");
            return false;
        }

        // ---- 2) 実測（3 隊 × 4 区画 × N 戦） ----
        var byUnit = new Dictionary<string, Acc>(StringComparer.Ordinal);
        var name = new Dictionary<string, string>(StringComparer.Ordinal);
        var side = new Dictionary<string, string>(StringComparer.Ordinal);
        var traits = new Dictionary<string, IReadOnlyList<TraitId>>(StringComparer.Ordinal);
        int battles = 0;

        foreach (Map11.SquadDef sq in Map11.Squads)
            foreach (IReadOnlyList<Map11.RoadNode> road in Map11.Roads)
                foreach (Map11.RoadNode n in road)
                    for (int seed = 0; seed < battlesPerPair; seed++)
                    {
                        var pu = BattleEngine.Materialize(sq.F, BattleContext.PlayerTeam);
                        var eu = BattleEngine.Materialize(n.Enemy, BattleContext.EnemyTeam);
                        BattleResult r = BattleEngine.Run(pu, eu, seed, verbose: true);
                        battles++;
                        // **`InstanceId` は `BattleContext.Add` が振る**ので、索引は
                        // `Run` の<b>あと</b>で作る（前に作ると全員 0 に潰れる）。
                        var idOf = new Dictionary<int, UnitState>();
                        foreach (UnitState u in pu.Concat(eu)) idOf[u.InstanceId] = u;

                        foreach (UnitState u in idOf.Values)
                        {
                            name[u.Def.Id] = u.Def.Name;
                            traits[u.Def.Id] = u.Def.Traits;
                            side[u.Def.Id] = u.TeamId == BattleContext.PlayerTeam ? "味方" : "敵";
                            Of(u.Def.Id).Battles++;
                        }
                        foreach (BattleEvent e in r.Events)
                        {
                            if (e.ActorId is not int aid || !idOf.TryGetValue(aid, out UnitState? a)) continue;
                            Acc acc = Of(a.Def.Id);
                            acc.Kinds[e.Kind] = acc.Kinds.GetValueOrDefault(e.Kind) + 1;
                            if (e.Kind == BattleEventKind.Attack) acc.Swings++;
                        }

                        Acc Of(string id)
                        {
                            if (!byUnit.TryGetValue(id, out Acc? a)) byUnit[id] = a = new Acc();
                            return a;
                        }
                    }

        write($"3 隊 × 4 区画 × {battlesPerPair} 戦 ＝ **{battles} 戦**（`verbose: true`）。"
            + $"数字はすべて**その駒が盤上にいた1戦あたり**の平均。"
            + $"味方 {side.Count(s => s.Value == "味方")} 枚 ／ 敵 {side.Count(s => s.Value == "敵")} 体。");
        write("");

        // ---- 3) 主表 ----
        write("## 表A: 駒 × 出来事 × 絵");
        write("");
        write("| 陣営 | 駒 | 札 | その駒らしい出来事（回/戦） | 台本 | 専用の絵 |");
        write("|---|---|---|---|:-:|---|");

        var rows = new List<(string Id, string Side, double Rate, string Kinds, bool HasArt)>();
        foreach ((string id, Acc acc) in byUnit.OrderBy(kv => side[kv.Key]).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var own = acc.Kinds.Where(k => !Generic.Contains(k.Key))
                               .OrderByDescending(k => k.Value).ToList();
            double per = acc.Battles == 0 ? 0 : own.Sum(k => k.Value) / (double)acc.Battles;
            string kindsText = own.Count == 0
                ? "—（振るだけ）"
                : string.Join(" / ", own.Select(k => $"`{k.Key}` {k.Value / (double)acc.Battles:F2}"));
            // 専用の絵 ＝ その駒らしい種類のどれかに `_battleField.` の呼び口があること。
            var artCalls = own.SelectMany(k => art.GetValueOrDefault(k.Key.ToString(), new List<string>()))
                              .Distinct().ToList();
            bool hasArt = artCalls.Count > 0;
            rows.Add((id, side[id], per, kindsText, hasArt));
            write($"| {side[id]} | {name[id]}<br>`{id}` | "
                + $"{(traits[id].Count == 0 ? "—" : string.Join(" ", traits[id]))} "
                + $"| {kindsText} | {(own.Count == 0 ? "**×**" : "○")} "
                + $"| {(hasArt ? string.Join(" / ", artCalls.Select(c => "`" + c + "`")) : "**無い**")} |");
        }
        write("");
        write("**`札` の列と `出来事` の列は結べない**——台本は `ActorId`（駒）しか運んでおらず、"
            + "**どの札がその出来事を起こしたかは1ビットも載っていない**"
            + "（第94期 (T2) の印 `TraitMark` は engine の中だけで、`BattleEvent` には出ない）。"
            + "**2 つ以上の札を持つ駒では、行の中で内訳が割れない。**");
        write("");

        // ---- 4) 同じ絵になる駒 ----
        write("## 表B: 同じ絵になる駒（区別が付かない組）");
        write("");
        var sig = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach ((string id, Acc acc) in byUnit)
        {
            var own = acc.Kinds.Where(k => !Generic.Contains(k.Key)).Select(k => k.Key.ToString())
                               .OrderBy(x => x, StringComparer.Ordinal).ToList();
            string key = own.Count == 0 ? "（振るだけ）" : string.Join("+", own);
            if (!sig.TryGetValue(key, out List<string>? l)) sig[key] = l = new List<string>();
            l.Add(name[id]);
        }
        write("| 台本の見え方 | 駒 |");
        write("|---|---|");
        foreach ((string key, List<string> members) in sig.Where(kv => kv.Value.Count > 1)
                                                          .OrderByDescending(kv => kv.Value.Count))
            write($"| {key} | **{string.Join(" / ", members)}** |");
        write("");
        write($"**{sig.Count(kv => kv.Value.Count > 1)} 組**が同じ見え方になる。"
            + "**これは engine の出来事が同じという意味であって、盤面で同じことをしているという意味ではない。**");
        write("");

        // ---- 5) 頻度が高いのに絵が無い上位 10 ----
        write("## 表C: 頻度が高いのに絵が無い（上位 10・発注の順番）");
        write("");
        write("| # | 陣営 | 駒 | 回/戦 | 出来事 |");
        write("|--:|---|---|--:|---|");
        int rank = 0;
        foreach (var r in rows.Where(x => !x.HasArt).OrderByDescending(x => x.Rate).Take(10))
            write($"| {++rank} | {r.Side} | {name[r.Id]} | {r.Rate:F2} | {r.Kinds} |");
        if (rank == 0) write("| — | — | 該当なし | — | — |");
        write("");
        var silent = rows.Where(x => x.Kinds.StartsWith("—", StringComparison.Ordinal)).ToList();
        write($"**台本に1件も「その駒らしい出来事」を出さない駒 {silent.Count} 枚**: "
            + (silent.Count == 0 ? "—" : string.Join(" / ", silent.Select(x => name[x.Id])))
            + "。**「回/戦 0.00」は盤面で何もしていないという意味ではない**"
            + "——囃し立て（標）は `StatusKeys.Marked` に窓口が無いので、"
            + "**開戦時に1回必ず働いているのに台本には1件も残らない**（第123期の盲点表と同じ穴）。"
            + "**絵を発注する前に、まず台本へ出す必要がある。**");
        write("");

        // ---- 6) ハネ（名指しの1節） ----
        write("## 表D: ハネ —— 1戦で何回、何をしているか");
        write("");
        WriteOne(UnitCatalog.Hane.Id, "ハネ（突き返し）");
        WriteOne(UnitCatalog.Basa.Id, "バサ（喧噪）— 比較のため");
        WriteOne(UnitCatalog.Yomi.Id, "ヨミ（軋み）— 「目立っていた」側");
        write("");
        write("**ハネとバサは台本の上で見分けが付かない**——どちらも `Move` しか出さず、"
            + "再生側の `Move` は駒を滑らせるだけで**誰が動かしたかを描き分けない**。"
            + "一方ヨミは `Attack` を**ターン外に**出すので、"
            + "第170期に入れた追加攻撃の演出がそのまま「その駒らしさ」になった"
            + "——**問い4 の答えはこの 3 行に全部出ている。**");
        write("");

        // ---- 7) 再生側の走査そのもの ----
        write("## 表E: 再生側の絵（`Main.ApplyEvent` の走査）");
        write("");
        write($"`case BattleEventKind.` の分岐 **{art.Count} 件** / "
            + $"`BattleEventKind` は **{Enum.GetNames(typeof(BattleEventKind)).Length} 種**。");
        write("");
        write("| 種類 | 再生側の呼び口 |");
        write("|---|---|");
        foreach (string k in Enum.GetNames(typeof(BattleEventKind)))
        {
            List<string> calls = art.GetValueOrDefault(k, new List<string>());
            write($"| `{k}` | {(calls.Count == 0 ? "**分岐なし（素通り）**" : string.Join(" / ", calls.Select(c => "`" + c + "`")))} |");
        }
        write("");
        write("**ART_GAP_COMPLETE**");
        return true;

        void WriteOne(string id, string label)
        {
            if (!byUnit.TryGetValue(id, out Acc? acc) || acc.Battles == 0)
            {
                write($"- **{label}**: このマップに1戦も出ていない");
                return;
            }
            var own = acc.Kinds.OrderByDescending(k => k.Value)
                .Select(k => $"`{k.Key}` {k.Value / (double)acc.Battles:F2}");
            write($"- **{label}**: {acc.Battles} 戦 ／ 振り {acc.Swings / (double)acc.Battles:F2} 回/戦 "
                + $"／ {string.Join(" ・ ", own)}");
        }
    }

    /// <summary>
    /// `Main.ApplyEvent` の `case BattleEventKind.X:` ごとに、その中で呼ばれている
    /// <c>_battleField.&lt;メソッド&gt;</c> を引く。<b>手書きの表を持たない。</b>
    /// </summary>
    private static Dictionary<string, List<string>> ScanReplay(string source)
    {
        var art = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var cases = System.Text.RegularExpressions.Regex
            .Matches(source, @"case BattleEventKind\.(\w+):")
            .Select(m => (m.Index, Kind: m.Groups[1].Value)).ToList();
        for (int i = 0; i < cases.Count; i++)
        {
            int from = cases[i].Index;
            int to = i + 1 < cases.Count ? cases[i + 1].Index : source.Length;
            // 分岐の終わりは**行に `break;` だけが立っている行**。
            //
            // **最初の `break;` で切ってはいけない**——早抜け（`if (...) break;`）を持つ
            // `Damage` / `Parry` が「絵が無い」に化ける。
            // **次の `case` まで取ってもいけない**——switch の最後の分岐（`Skill`）が
            // ファイルの残り全部を飲み込む。**走査で2回とも踏んだ。**
            var endM = System.Text.RegularExpressions.Regex.Match(
                source[from..to], @"(?m)^[ \t\r]*break;[ \t\r]*$");
            if (endM.Success) to = from + endM.Index;
            // **`_battleField.X(` だけでは足りない。** `Damage` / `Parry` は
            // 局所のヘルパ（`ShowDamage` / `ShowParry`）へ委ねていて、
            // 呼び口だけ見ると「絵が無い」に化ける（走査で3回目に踏んだ穴）。
            string block = source[from..to];
            var calls = System.Text.RegularExpressions.Regex
                .Matches(block, @"_battleField\.(\w+)\(")
                .Select(m => m.Groups[1].Value)
                .Concat(System.Text.RegularExpressions.Regex
                    .Matches(block, @"\b(Show[A-Z]\w*|Apply[A-Z]\w*)\(")
                    .Select(m => m.Groups[1].Value))
                .Distinct().ToList();
            if (!art.TryGetValue(cases[i].Kind, out List<string>? l)) art[cases[i].Kind] = l = new List<string>();
            foreach (string c in calls) if (!l.Contains(c)) l.Add(c);
        }
        return art;
    }
}
