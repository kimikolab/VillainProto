using BattleCore;
using Godot;
using System.Linq;

public partial class Map11Main
{
    // =================================================================================
    // 第467期 §1-3 —— 軸への投資の札の窓（判定は持たない・`Chapter1.PickInvest` を呼ぶだけ・見た目は仮で Codex）
    // =================================================================================

    private Control? _investOverlay;

    /// <summary>
    /// ステージの始めの投資の札（3 枚から 1 枚・見送りは無い・選ぶまで閉じない）。1 章のステージで、まだ選んでいないときだけ出す。
    /// 頭の札が出ていればその「始める」の後に出る（組み直しの窓より前）。
    /// </summary>
    private void ShowInvestPicker()
    {
        if (!Chapter1.InvestPending || _investOverlay is { Visible: true }) return;
        var cards = Chapter1.InvestCards;
        if (cards.Count == 0) return;
        InvestRule rule = St.Stage.Invest;
        _investOverlay = Modal(_uiRoot, 470, 250, out VBoxContainer col, "投資", GoldColor);
        col.AddChild(UiKit.Text(rule.Doctrines ? $"{St.Stage.Id} ・ どの札を選ぶ？" : $"{St.Stage.Id} ・ どの軸に投資する？", 22, Colors.White));
        Label help = UiKit.Text($"選んだ軸の駒が、呼ぶ候補に出やすくなる（投資 1 回につき候補 1 枚ごとに {rule.PerPick}%・上限 {rule.Cap}%）。"
                                + "選んだ軸は次のステージへ持ち越し、同じ軸を選べば重なる。金はかからない。"
                                + (rule.Doctrines ? "\n盤の札・金の札は、良いことと悪いことを両方持つ（1 章の終わりまで効く・同じ札は 1 章で 1 度だけ）。盤の札は開戦時の席で数値が変わる（戦闘中に動かされても変わらない）。" : ""),
                                13, Color.FromHtml("#ccd5cb"));
        help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(help);
        col.AddChild(UiKit.Text(St.InvestText, 13, GoldColor));
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);
        for (int i = 0; i < cards.Count; i++)
        {
            string axis = cards[i];
            int index = i;
            var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            card.AddThemeConstantOverride("separation", 6);
            // 第468期 §1-3: 盤・金の札は「プラス ／ マイナス」の 2 行（色は仮・見た目は Codex）。
            if (Doctrine.Find(axis) is { } doc)
            {
                AddDoctrineCard(card, doc, index);
                row.AddChild(card);
                continue;
            }
            card.AddChild(UiKit.Text(axis, 22, GoldColor));
            Label line = UiKit.Text(UnitTags.LineOf(axis), 12, Color.FromHtml("#ccd5cb"));
            line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            line.CustomMinimumSize = new Vector2(0, 34);
            card.AddChild(line);
            // その軸の駒の顔 3〜4 体（持っていない駒を先に）。
            var faces = new HBoxContainer();
            faces.AddThemeConstantOverride("separation", 2);
            foreach (UnitDef d in InvestRule.PiecesOf(axis).OrderBy(d => St.OnBoardOrHand(d)).Take(4))
            {
                var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(52, 52), TooltipText = d.Name + (St.OnBoardOrHand(d) ? "（持っている）" : "") };
                face.SetUnit(d);
                faces.AddChild(face);
            }
            card.AddChild(faces);
            int have = St.OwnedAxisCount(axis), all = InvestRule.PiecesOf(axis).Count;
            card.AddChild(UiKit.Text($"手持ち {have} ／ {all} 体・投資 ×{St.InvestCount(axis)}", 12, UiKit.Muted));
            card.AddChild(UiKit.Text("この軸の駒が呼ぶ候補に出やすくなる", 12, UiKit.Muted));
            Button pick = UiKit.ActionButton($"{axis}に投資する", GoldColor);
            pick.CustomMinimumSize = new Vector2(0, 38);
            pick.Pressed += () => OnInvestPicked(index);
            card.AddChild(pick);
            row.AddChild(card);
        }
        _investOverlay.Visible = true;
    }

    /// <summary>盤・金の札 1 枚（第468期 §1-3・名前 ・種類 ・プラス ／ マイナスの 2 行 ・選ぶボタン・見た目は仮）。</summary>
    private void AddDoctrineCard(VBoxContainer card, Doctrine doc, int index)
    {
        Color accent = doc.Kind == DoctrineKind.Board ? Color.FromHtml("#9fc4e8") : GoldColor;
        card.AddChild(UiKit.Text(doc.Name, 22, accent));
        card.AddChild(UiKit.Text(doc.KindName, 12, UiKit.Muted));
        Label plus = UiKit.Text($"＋ {doc.Plus}", 13, Color.FromHtml("#9fdc9a"));
        plus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        card.AddChild(plus);
        Label minus = UiKit.Text($"－ {doc.Minus}", 13, Color.FromHtml("#e89a8a"));
        minus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        card.AddChild(minus);
        card.AddChild(UiKit.Text("1 章の終わりまで効く（同じ札は 1 章で 1 度だけ）", 12, UiKit.Muted));
        Button pick = UiKit.ActionButton($"{doc.Name}を選ぶ", accent);
        pick.CustomMinimumSize = new Vector2(0, 38);
        pick.Pressed += () => OnInvestPicked(index);
        card.AddChild(pick);
    }

    /// <summary>
    /// 絵の口 `enhance-press`（第467期 §2・門 `--chapter1-invest-smoke` (e) の画面の側）: 組み直しの窓で、画面の押し方の口（<see cref="OnBenchPressed"/> ／
    /// <see cref="OnSquadSlot"/> ／ <see cref="PressEnhance"/>）をそのまま押して確かめる。`--map-chapter-play=N --map-chapter-stop=1` と使う（持ち越して入った 1-2 の組み直しの窓）。
    ///   ① 手札の駒をつまんで強化 → 別の手札を押す: 入れ替わらず、押した駒を選び直す
    ///   ② 隊の駒をつまんで強化 → 手札を押す: 入れ替わらず、押した手札を選び直す
    ///   ③ 強化を続けて押せる（同じ駒が 2 回強化される）
    ///   ④ 強化していなければ今のまま（つまんで → 押すと入れ替わる）
    /// 結果は `MAP11_CAPTURE_ENHANCE_PRESS ok=` の 1 行。
    /// </summary>
    private void CaptureEnhancePress()
    {
        Chapter1.AutoInvest();
        if (_investOverlay is not null) _investOverlay.Visible = false;
        if (_introOverlay is not null) _introOverlay.Visible = false;
        _reform = true;
        ClearHold();
        Refresh();
        var parts = new System.Collections.Generic.List<string>();
        // 支度（押し方の確かめのためだけ）: 金を足し、手札が 2 枚に満たなければ隊の後ろの席の駒を「ここへ下げる」で手札へ（画面の口のまま）。
        St.GrantGoldForCheck(10 * St.EnhancePrice);
        for (int guard = 0; guard < 4 && St.Bench.Count < 2; guard++)
        {
            int from = Enumerable.Range(0, St.Squads.Length).FirstOrDefault(i => St.CanReform(i) && St.Squads[i].Units is { } us && us.Count(u => u.IsAlive) >= 2, -1);
            if (from < 0) break;
            _selected = from;
            OnSquadSlot(St.Squads[from].Units!.Where(u => u.IsAlive).Max(u => u.Slot));
            OnBenchPressed(St.Bench.Count);
        }
        ClearHold();
        bool ok = St.Stage.Stars.EnhancePrice > 0 && St.Bench.Count >= 2 && St.Gold >= 4 * St.EnhancePrice;
        string Ids() => string.Join(",", St.Bench.Select(u => u.Def.Id)) + "|" + string.Join(",", St.Squads.Select(q => string.Join(".", (q.Units ?? new()).OrderBy(u => u.Slot).Select(u => $"{u.Slot}{u.Def.Id}"))));
        int Star(UnitState u) => St.StarOf(u.Def.Id).Star * 10 + St.StarOf(u.Def.Id).Pips;
        if (ok)
        {
            // ①
            int sq0 = Enumerable.Range(0, St.Squads.Length).FirstOrDefault(i => St.CanReform(i) && St.Squads[i].Units is { Count: > 0 } us && us.Any(u => u.IsAlive), -1);
            string before = Ids();
            OnBenchPressed(0);
            UnitState a = St.Bench[0];
            int sa = Star(a);
            PressEnhance(a);
            bool up1 = Star(St.Bench[0]) != sa;
            bool c1 = false;
            if (sq0 >= 0)
            {
                _selected = sq0;
                int slot0 = St.Squads[sq0].Units!.Where(u => u.IsAlive).Min(u => u.Slot);
                OnSquadSlot(slot0);
                c1 = up1 && Ids() == before && _holdSquad == sq0 && _holdSlot == slot0 && _holdBench < 0;
                parts.Add($"① 手札 {a.Def.Name} を強化（☆ {sa} → {Star(St.Bench[0])}）→ {St.Squads[sq0].Label} {FormationRules.SeatNames[slot0]} を押す: 並び同じ {Ids() == before}・つまみ = 席 {(_holdSlot >= 0 ? FormationRules.SeatNames[_holdSlot] : "—")}");
            }
            ok &= c1;
            // ②
            ClearHold();
            int sq = Enumerable.Range(0, St.Squads.Length).FirstOrDefault(i => St.CanReform(i) && St.Squads[i].Units is { Count: > 0 } us && us.Any(u => u.IsAlive), -1);
            if (sq >= 0)
            {
                _selected = sq;
                UnitState seat = St.Squads[sq].Units!.Where(u => u.IsAlive).OrderBy(u => u.Slot).First();
                int slot = seat.Slot, ss = Star(seat);
                before = Ids();
                OnSquadSlot(slot);
                bool held = _holdSquad == sq && _holdSlot == slot;
                PressEnhance(St.UnitAt(sq, slot)!);
                bool up2 = Star(St.UnitAt(sq, slot)!) != ss;
                OnBenchPressed(0);
                bool c2 = held && up2 && Ids() == before && _holdBench == 0 && _holdSlot < 0;
                parts.Add($"② {St.Squads[sq].Label} {FormationRules.SeatNames[slot]} {seat.Def.Name} を強化 → 手札 1 枚目を押す: 並び同じ {Ids() == before}・つまみ = 手札 {_holdBench}");
                ok &= c2;
                // ③
                ClearHold();
                OnBenchPressed(0);
                UnitState b = St.Bench[0];
                int sb = Star(b), en0 = St.Enhances;
                PressEnhance(b);
                PressEnhance(St.Bench[0]);
                int sb2 = Star(St.Bench[0]);
                bool c3 = St.Enhances == en0 + 2 && sb2 > sb && _holdBench == 0;
                parts.Add($"③ 手札 {b.Def.Name} を続けて 2 回強化: ☆ {sb} → {sb2}");
                ok &= c3;
                // ④
                ClearHold();
                before = Ids();
                string benchId = St.Bench[0].Def.Id, seatId = St.UnitAt(sq, slot)!.Def.Id;
                OnBenchPressed(0);
                OnSquadSlot(slot);
                bool c4 = St.UnitAt(sq, slot)?.Def.Id == benchId && St.Bench.Any(u => u.Def.Id == seatId);
                parts.Add($"④ 強化せずに 手札 1 枚目 → {FormationRules.SeatNames[slot]} を押す: 入れ替わった {c4}");
                ok &= c4;
            }
            else { ok = false; parts.Add("組み直せる隊が無い"); }
        }
        else parts.Add($"強化の盤でない ／ 手札が 2 枚ない ／ 金が 4 回ぶん無い（手札 {St.Bench.Count}・金 {St.Gold}）");
        ClearHold();
        Refresh();
        foreach (string p in parts) GD.Print("MAP11_CAPTURE_ENHANCE_PRESS   " + p);
        GD.Print($"MAP11_CAPTURE_ENHANCE_PRESS ok={ok} stage={St.Stage.Id} gold={St.Gold}");
    }

    /// <summary>投資の札を選んだ（`Chapter1.PickInvest` が候補を引き直す）。</summary>
    private void OnInvestPicked(int index)
    {
        var cards = Chapter1.InvestCards;
        if (!Chapter1.PickInvest(index)) { ShowToast("いまは選べません", EnemyColor, 3.0f); return; }
        if (_investOverlay is not null) _investOverlay.Visible = false;
        ShowToast(Doctrine.Find(cards[index]) is null ? $"{cards[index]}に投資した（{St.InvestText}）" : $"{cards[index]}を選んだ（{St.InvestText}）", GoldColor, 3.5f);
        GD.Print($"CHAPTER1_INVEST stage={St.Stage.Id} pick={cards[index]} cards={string.Join("/", cards)} {St.InvestText}");
        Refresh();
    }
}
