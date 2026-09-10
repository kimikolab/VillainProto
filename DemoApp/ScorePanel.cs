using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 戦績パネル（第124期 段1）。<b><c>BattleCore</c> を1行も触らずに「誰が何をしたか」を出す。</b>
///
/// <para>第123期の観察でポンが出した×の多くは「<b>機能しているのか</b>わからない」であって
/// 「1文が読めない」ではない。<b>「1文が読めるか」と「機能しているか」は別の問い</b>で、
/// 後者は数字で閉じられる——<see cref="BattleResult.TallyByUnit"/> は既に載っていて
/// <c>verbose</c> にも依存しない（Phase 0 Q0-1 / Q0-2）ので、
/// <b>判定を1つも通さずに画面へ出せる。</b></para>
///
/// <para><b>列の定義は <c>docs/pulse.md</c> と同じ言葉・同じ出どころを使う</b>（受け入れ条件 A4）
/// ——同じ言葉で2つの表を作らない。<c>Taillight*</c> / <c>Reader*</c> のような
/// <b>特性専用の計数は出さない</b>（出すと52枚ぶんの列が要る）。</para>
///
/// <para><b>この表は戦闘全体の最終集計である。</b> <c>BattleEngine.Run</c> は開戦前に
/// 戦闘を丸ごと計算し切る純関数（<c>CLAUDE.md</c> の「決定性」）で、画面はその列の再生にすぎない。
/// 再生位置で数字を切り出すと <see cref="UnitTally"/> ではなく台本から数え直すことになり、
/// <b>同じ言葉の表が2つできる</b>ので、そうしていない。</para>
/// </summary>
public partial class ScorePanel : PanelContainer
{
    /// <summary>
    /// 列。<b>左が見出し・右が <see cref="UnitTally"/> の出どころ</b>（A4 の照合はこの表と
    /// <c>docs/pulse.md</c> の見出しを並べて行う）。<c>null</c> は<b>計数が無い列</b>で、
    /// 常に <see cref="Absent"/> を出す（A5）。
    /// </summary>
    private static readonly (string Head, Func<UnitTally, int>? Of)[] Columns =
    {
        ("振",      t => t.Attacks),
        ("干渉",    t => t.Interventions),
        ("与(敵)",  t => t.DamageToEnemy),
        ("与(味)",  t => t.DamageToAlly),
        ("被",      t => t.DamageTaken),
        ("被(味)",  t => t.TakenFromAlly),
        ("回復(受)", t => t.Healed),
        ("回復(与)", t => t.HealOutInTurn + t.HealOutOffTurn),
        ("強弱",    t => t.BuffOutInTurn + t.BuffOutOffTurn),
        ("付与",    t => t.StatusOutInTurn + t.StatusOutOffTurn),
        ("大技",    t => t.BigAttacks),
        ("撃破",    t => t.Kills),
        ("庇い",    null),
        ("落ちた",  t => t.LastActiveTurn),
    };

    /// <summary><b>計数が無い</b>ことを表す印。<c>0</c>（＝やっていない）と必ず描き分ける（A5）。</summary>
    private const string Absent = "—";

    private VBoxContainer _body = null!;
    private Label _caption = null!;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.03f, 0.055f, 0.05f, 0.97f), UiKit.Line, 1, 10));

        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 16);
        AddChild(margin);

        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        margin.AddChild(scroll);

        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_body);

        _caption = UiKit.Text("", 11, UiKit.Muted);
        _caption.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    /// <summary>台本の最終集計を描き直す。<b>盤面にも台本にも一切触らない。</b></summary>
    public void Render(BattleResult result, IReadOnlyList<DemoOpening> opening, string subtitle)
    {
        foreach (Node child in _body.GetChildren()) child.QueueFree();

        _body.AddChild(UiKit.Text("戦績 —— 誰が何をしたか", 19, Colors.White));

        Label lead = UiKit.Text(subtitle, 11, UiKit.Muted);
        lead.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.AddChild(lead);

        // 陣営は開幕の駒から引く。**戦闘中に湧いた駒（胞子・餌）は台本の Summon から拾う**
        // ——`TallyByUnit` は `Def.Id` で引く辞書なので、湧いた駒も同じ行に混ざる。
        var team = new Dictionary<string, int>(StringComparer.Ordinal);
        var label = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DemoOpening o in opening) { team[o.UnitId] = o.Team; label[o.UnitId] = o.Name; }
        foreach (BattleEvent e in result.Events)
        {
            if (e.Kind != BattleEventKind.Summon || e.Team is not int t) continue;
            string id = SummonIdOf(e.Text) ?? $"summon-{e.Text}";
            team[id] = t;
            label[id] = e.Text ?? id;
        }

        Section(result, team, label, BattleContext.PlayerTeam, "味方", UiKit.Player);
        Section(result, team, label, BattleContext.EnemyTeam, "敵", UiKit.Enemy);

        _body.AddChild(new HSeparator());

        // ---------------------------------------------------------------------------
        // 読み違えを画面が作らないための注記（受け入れ条件 A6）。
        // `docs/pulse.md` に既に書いてある文をそのまま載せる——プロジェクトは
        // 「干渉 0 は無価値ではない」を既に知っているので、**知っている誤読を画面が作ってはいけない。**
        // ---------------------------------------------------------------------------
        Note("干渉 0 は「価値が無い」ではない。 呪詛（ネル）・萎縮（クビ）・庇い（ガルド）は"
             + "ダメージを経由せずに盤面を変えるので、この列には最初から出ない。"
             + "ここで測れるのは体験の密度であって貢献度ではない。", UiKit.Gold);
        Note($"「{Absent}」は計数が無い（0 ではない）。 庇い＝庇った回数は `UnitTally` に1つも無く、"
             + "ガルド・ドハ・セッキは `被` が増えるだけなので「庇ったから増えた」と「殴られたから増えた」が"
             + "数字から分けられない。", UiKit.Faint);
        Note("回復(受) は回復された量（`docs/pulse.md` と同じ出どころ）で、回復(与) は配った側の量。"
             + "強弱＝この駒が動かした攻撃力の量（他人への強化・弱体も、自分で積んだぶんも、符号を問わず足す）。"
             + "付与＝状態異常を書いた回数（毒・燃・痺・標・破片・傷・手番の7キー合計）。", UiKit.Faint);
        Note("回復(与)・強弱・付与は「いま実行中の特性の持ち主」に帰属する（第94期の印）。"
             + "キー別の内訳は計数が無いので、『標を何体に付けたか』はこの列からは引けない。"
             + "取り上げた／消費したぶん（止めが標を食う・断ちが傷を食う）は減算なので1件も数えられない。", UiKit.Faint);
        Note("落ちた＝最後に行動できたターン（生存なら決着ターン）。"
             + "振＝`PerformAttack` を通った回数、干渉＝実際にダメージを通した回数。"
             + "この2つのズレが体験の密度を測っている（`docs/pulse.md`）。", UiKit.Faint);
    }

    private void Note(string text, Color color)
    {
        Label line = UiKit.Text(text, 10, color);
        line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        line.CustomMinimumSize = new Vector2(560, 0);
        _body.AddChild(line);
    }

    private void Section(
        BattleResult result,
        IReadOnlyDictionary<string, int> team,
        IReadOnlyDictionary<string, string> label,
        int side,
        string title,
        Color color)
    {
        var rows = result.TallyByUnit
            .Where(kv => team.TryGetValue(kv.Key, out int t) && t == side)
            .OrderByDescending(kv => kv.Value.DamageToEnemy)
            .ThenByDescending(kv => kv.Value.Interventions)
            .ToList();
        if (rows.Count == 0) return;

        _body.AddChild(new HSeparator());
        _body.AddChild(UiKit.Text(title, 13, color));

        var grid = new GridContainer { Columns = Columns.Length + 1 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 3);
        _body.AddChild(grid);

        Label head = UiKit.Text("駒", 11, UiKit.Faint);
        head.CustomMinimumSize = new Vector2(140, 0);
        grid.AddChild(head);
        foreach ((string h, _) in Columns) grid.AddChild(Cell(h, UiKit.Faint, 11));

        foreach ((string id, UnitTally t) in rows)
        {
            Label name = UiKit.Text(label.TryGetValue(id, out string? n) ? n : id, 12, UiKit.Ink);
            name.CustomMinimumSize = new Vector2(140, 0);
            grid.AddChild(name);
            foreach ((_, Func<UnitTally, int>? of) in Columns)
            {
                if (of is null) { grid.AddChild(Cell(Absent, UiKit.Faint, 12)); continue; }
                int v = of(t);
                grid.AddChild(Cell(v.ToString(), v == 0 ? UiKit.Faint : UiKit.Ink, 12));
            }
        }
    }

    private static Label Cell(string value, Color color, int size)
    {
        Label cell = UiKit.Text(value, size, color);
        cell.HorizontalAlignment = HorizontalAlignment.Right;
        cell.CustomMinimumSize = new Vector2(50, 0);
        return cell;
    }

    /// <summary>召喚された駒の <c>Def.Id</c> を名前から引く（<c>Main.FindDefByName</c> と同じ引き方）。</summary>
    private static string? SummonIdOf(string? name)
        => name is null ? null
         : new[] { UnitCatalog.Spore, UnitCatalog.Fodder }.FirstOrDefault(d => d.Name == name)?.Id;
}
