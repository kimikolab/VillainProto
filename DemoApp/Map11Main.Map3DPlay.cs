using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 第476期 §3-3: 3D の画面を「本物のクリック」で通して遊び、場面ごとに写しを撮る口（`--map3d-play=<方針>,<写しの置き場>`）。
// 方針は north（北だけ）／ south（南だけ）／ split（2 隊に分ける）。<b>判定は 1 つも持たない</b>——駒・マス・ボタンを画面の座標で押すだけで、
// 何が起きるかは今の画面（`Map11Main` ／ `Map11Session`）が決める。戦闘は戦闘の画面へ行き、早回しで戻る（通しの門と同じ `_fastSmoke`）。
// 戦闘のたびにこの場面は作り直されるので、どこまで進んだかは static に置き、毎回「いま画面に何が出ているか」を見て次の 1 手を決める。
// 例外: 「2 隊に分ける」で 2 枚呼んで第2隊の席に置くところだけは器具の口（`Map11State.Recruit` ／ `SwapWithBench`）で行う（編成の窓の中の操作は見ない）。
// 合否は `MAP3D_PLAY_COMPLETE` の行。遊ぶ起動では読まない。
public partial class Map11Main
{
    private static string? _play3dPlan, _play3dDir;
    private static bool _play3dReadOdds;
    private static int _play3dDeclines;
    private static int _play3dShot, _play3dGuard;
    private static bool _play3dFormed, _play3dHovered;
    private static readonly HashSet<string> _play3dOnce = new(StringComparer.Ordinal);
    // 第477期: 1-2 の方針（p1 ＝ 中＋北 ／ p2 ＝ 中＋南 ／ p3 ＝ 北＋南）。主力・別働・留守番の隊の番号は入ったときに 1 度決める。
    private static int _play3dMain = -1, _play3dSecond = -1, _play3dHeroSeen;
    private static readonly int[] _play3dRests = new int[8];
    private static bool Play12 => _play3dPlan is "p1" or "p2" or "p3";
    // 第480期: 1-3「狩る者」の方針（1-3 の始めの写しから・`--map-chapter-from=` と使う）。
    //   ha ＝ 全員まっすぐ帰る ／ hb1 ＝ 硬い 1 枚の殿 ／ hb2 ＝ 弱い 1 枚の殿 ／ hb3 ＝ 3 枚の殿 ／ hc ＝ 隊を順に当てる
    //   hd ＝ 主力 1 隊だけで 2 つ取る（谷の橋 → 横の辺 → 森の集落・「2 隊目が倒れた後に立て直せるか」の確かめ）／ hn ＝ 主力は北の砦跡・別働は谷の橋（北の道と弓隊）
    private static bool Play13 => _play3dPlan is "ha" or "hb1" or "hb2" or "hb3" or "hc" or "hd" or "hn" or "hr";
    private static int _play3dRear = -1, _play3dHeroTurn = -1, _play3dRearSize;
    private static readonly HashSet<string> _play3dRearIds = new(StringComparer.Ordinal);
    private static readonly List<string> _play3dHunterLog = new();

    /// <summary>起動の引数に `--map3d-play=` があるか（戦闘の画面の早回しも同じ引数を読む）。</summary>
    internal static bool Play3DRequested(IEnumerable<string> user) => user.Any(a => a.StartsWith("--map3d-play=", StringComparison.Ordinal));

    private static int _play3dStalls;
    /// <summary>通しのあいだに出た「時計を止めて盤を隠す窓」（第479期 §7・頭の札と投資の札・編成の窓・結果の窓・戦闘の画面は数えない）。</summary>
    private static readonly List<string> _play3dCovers = new();
    private static int _play3dSides, _play3dSays, _play3dRestClicks;
    private void StartPlay3D(string[] user)
    {
        if (!Map3DMode.Active || user.FirstOrDefault(a => a.StartsWith("--map3d-play=", StringComparison.Ordinal)) is not { } arg) return;
        string[] parts = arg["--map3d-play=".Length..].Split(',', 2);
        _play3dPlan = parts[0];
        // 第479期の続き: 末尾に r を付けると、勝ち目の印を読む（劣勢なら引き返す ／ 引いて待つ・期限の 3 刻前からは戦う）——人が印を見て決める遊び方の代わり。
        if (_play3dPlan is "p1r" or "p2r" or "p3r") { _play3dPlan = _play3dPlan[..2]; _play3dReadOdds = true; }
        _play3dDir = parts.Length > 1 ? parts[1].Trim('"') : ProjectSettings.GlobalizePath("user://map3d_play");
        System.IO.Directory.CreateDirectory(_play3dDir);
        _ = Play3D();
    }

    private async System.Threading.Tasks.Task Play3D()
    {
        try { await Play3DLoop(); }
        catch (ObjectDisposedException) { /* 戦闘の画面へ移った（この場面は捨てられた） */ }
        catch (Exception e)
        {
            GD.PrintErr($"MAP3D_PLAY_FAILED plan={_play3dPlan} {e}");
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task Play3DLoop()
    {
        async System.Threading.Tasks.Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        async System.Threading.Tasks.Task Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        void Move(Vector2 at)
        {
            Vector2 p = GetViewport().GetFinalTransform() * at;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
        }
        void Click(Vector2 at)
        {
            Vector2 p = GetViewport().GetFinalTransform() * at;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p, GlobalPosition = p });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = p, GlobalPosition = p });
        }
        // 今の窓の層（1440×900 の作りを拡げた層）のボタンは、層の拡大率を掛けた画面の座標で押す。
        Vector2 OldUi(Control c) => c.GetGlobalRect().GetCenter() * _uiScale;
        Button? Find(Control root, Func<Button, bool> match)
        {
            var stack = new Stack<Node>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Node n = stack.Pop();
                if (n is Button { Visible: true, Disabled: false } b && b.IsVisibleInTree() && match(b)) return b;
                foreach (Node child in n.GetChildren()) stack.Push(child);
            }
            return null;
        }
        string State()
            => $"作戦ターン {Map11Session.ShownTurn} 金 {St.Gold} 戦闘 {St.Battles} 湧き {St.SpawnCount}（湧くまで {St.PortalCooldown}）隊 "
               + string.Join(" ／ ", St.Squads.Where(q => !St.IsLocked(q.Index)).Select(q => q.Lost ? $"{q.Label} 失" : $"{q.Label} {PlaceName(q.Pos)} {q.Units?.Count(u => u.IsAlive) ?? 0}枚 HP{SquadHpPercent(q):F0}%"))
               + $" 敵 {string.Join("・", St.Nodes.SelectMany(r => r).Concat(St.Spawns).Where(n => !n.Cleared).Select(n => $"{n.Def.Name}@{(n.Pos == St.Graph.Portal ? "本陣" : n.Pos == St.Graph.Home ? "拠点" : PlaceName(n.Pos))}"))}";
        async System.Threading.Tasks.Task Shot(string tag, bool once = false)
        {
            if (once && !_play3dOnce.Add(tag)) return;
            await Wait(0.45);
            await Frames(2);
            string name = $"{_play3dPlan}_{++_play3dShot:00}_{tag}.png";
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_play3dDir!, name));
            GD.Print($"MAP3D_PLAY shot={name} {State()} 案内「{_toast.Text}」「{_orderHint.Text}」");
        }
        void Finish(bool ok, string why = "")
        {
            int lost = St.Squads.Sum(q => q.Units?.Count(u => !u.IsAlive) ?? 0) + St.FallenUnits.Count;
            if (Play13)
            {
                foreach (string line in _play3dHunterLog) GD.Print("MAP3D_PLAY_HUNTER " + line);
                GD.Print($"MAP3D_PLAY_HUNTER 終わり: {_resultTitle.Text} ・ 勇者が現れた {(St.HeroTurn >= 0 ? $"{St.HeroTurn + 1} 刻目" : "—")} ・ 終わった {Map11Session.ShownTurn} 刻目（登場から {(St.HeroTurn >= 0 ? St.Turn - St.HeroTurn : 0)} 刻）"
                    + $" ・ 生還 {St.HunterSurvivors}／{St.HunterStartUnits} ・ 殿 {(St.RearGuards.Count == 0 ? "なし" : string.Join("・", St.RearGuards.OrderBy(i => i).Select(i => St.Squads[i].Label)))}"
                    + $" ・ 足を止めた {St.HunterHolds} 回 ／ 止められなかった {St.HunterSlips} 回 ／ 追いつかれた {St.HunterCatches} 回 ・ 勇者 HP {St.Hero?.HpNow}／{St.Hero?.DefMaxHp} ・ 退かせた {St.HunterDriven} ・ 時間切れ {St.TimeUp}");
            }
            GD.Print($"MAP3D_PLAY_COMPLETE plan={_play3dPlan} ok={ok && St.Finished} won={St.Won} fallen={St.Fallen} captured_turn={(St.Captured ? St.CapturedTurn + 1 : -1)} "
                + $"gold={St.Gold} lost={lost} battles={St.Battles} spawns={St.SpawnCount} shots={_play3dShot} covers={_play3dCovers.Count}{(_play3dCovers.Count > 0 ? "（" + string.Join("・", _play3dCovers) + "）" : "")} "
                + $"sides={_play3dSides} says={_play3dSays} declines={_play3dDeclines} rest_clicks={_play3dRestClicks} {why}");
            GetTree().Quit(ok ? 0 : 1);
        }

        // ---- 命令 1 つ（第477期の 1-2 の押し方を切り出した）: 隊の札を押して選び、行き先が画面の外なら全体図を押して視点を動かし、マスに触れて案内を読み、押して決める ----
        async System.Threading.Tasks.Task Order(int squad, Dest want)
        {
            MapGraph g = St.Graph;
            if (Map11Session.Destinations[squad] == want || St.AtDest(squad, want)) return;
            // 第479期: 今いる場所で休むなら、隊の札の上の「休む」を押す（出ていれば）。
            if (!want.IsPortal && !want.AtHome && want.Id == St.Squads[squad].Pos && _hud3d!.ActionRect(squad, 2) is { } restAt)
            {
                await Shot($"第{squad + 1}隊_札の上の休む", once: true);
                Click(restAt.GetCenter());
                await Frames(4);
                if (Map11Session.Destinations[squad] == want) { _play3dRestClicks++; return; }
            }
            Click(_hud3d!.CardRect(squad).GetCenter());
            await Frames(4);
            Vector3 world = _cellMarks3d[want.IsPortal ? g.Portal : want.AtHome ? g.Home : want.Id].GlobalPosition;
            Vector2 view = GetViewport().GetVisibleRect().Size;
            Vector2 at = _camera.UnprojectPosition(world);
            if (!new Rect2(120, 150, view.X - 240, view.Y - 150 - 260).HasPoint(at) && _radar3d is not null)
            {
                Click(_radar3d.MapPoint(world));   // 全体図を押して視点を動かす
                await Wait(1.1);
                at = _camera.UnprojectPosition(world);
            }
            Move(at);
            await Frames(4);
            string name = want.IsPortal ? "敵の拠点" : want.AtHome ? "拠点" : PlaceName(want.Id);
            await Shot($"第{squad + 1}隊_行き先に触れた_{name}", once: true);
            Click(at);
            await Frames(4);
            bool set = Map11Session.Destinations[squad] == want;
            if (!set) { Map11Session.SetDestination(squad, want); GD.Print($"MAP3D_PLAY 行き先は器具の口で決めた 第{squad + 1}隊 → {name}"); }
            else await Shot($"第{squad + 1}隊_行き先を決めた_{name}", once: true);
        }

        // ---- 殿に出す（追記 4）: 隊の札を押して選び、勇者の駒に触れて点線と「殿に出す」の札を見て、押す ----
        async System.Threading.Tasks.Task RearOrder(int squad)
        {
            Click(_hud3d!.CardRect(squad).GetCenter());
            await Frames(4);
            var pick = _mapPicks.FirstOrDefault(p => p.Foe is { IsHero: true, Cleared: false });
            if (pick.Button is null) { Map11Session.SetRear(squad); return; }
            Vector2 view = GetViewport().GetVisibleRect().Size;
            if (HeroPiece() is not { } piece) { Map11Session.SetRear(squad); return; }
            Vector2 at = _camera.UnprojectPosition(piece.GlobalPosition + new Vector3(0, 2.2f, 0));
            if (!new Rect2(120, 150, view.X - 240, view.Y - 150 - 260).HasPoint(at) && _radar3d is not null)
            {
                Click(_radar3d.MapPoint(piece.GlobalPosition));
                await Wait(1.1);
                at = _camera.UnprojectPosition(piece.GlobalPosition + new Vector3(0, 2.2f, 0));
            }
            GD.Print($"MAP3D_PLAY 殿に出す: 勇者の駒 {at} 押す領域 {pick.Button.Visible} {pick.Button.GetGlobalRect()} 押せる {St.Hero is { } rh0 && RearPickable(rh0)} 選んでいる隊 {_selected}");
            Move(at);
            await Frames(6);
            _play3dHunterLog.Add($"{Map11Session.ShownTurn} 刻目 {St.Squads[squad].Label} で勇者の駒に触れた: 「殿に出す」の札 {(RearMarkShown ? "出た" : "出ない")}");
            await Shot($"第{squad + 1}隊_勇者の駒に触れた_殿に出す", once: true);
            Click(at);
            await Frames(4);
            bool set = Map11Session.IsRear(squad);
            if (!set) { Map11Session.SetRear(squad); GD.Print($"MAP3D_PLAY 殿は器具の口で決めた 第{squad + 1}隊"); }
            else { _play3dHunterLog.Add($"{Map11Session.ShownTurn} 刻目 {St.Squads[squad].Label} を殿に出した（勇者の駒を押した）"); await Shot($"第{squad + 1}隊_殿に出した", once: true); }
        }

        await Wait(0.8);
        while (IsInsideTree())
        {
            if (_play3dGuard++ > 500) { Finish(false, "step-limit"); return; }
            // 第480期: 勇者の登場・退く演出のあいだは待つ（演出の節目ごとに 1 枚撮る）。
            if (HunterBusy)
            {
                if (HunterBeat.Length > 0 && HunterBeat != "終わり" && _play3dOnce.Add("beat:" + HunterBeat))
                {
                    await Wait(HunterBeat switch { "落雷" => 0.15, "扉" => 0.1, "歩み出る" => 0.7, "帯" => 0.5, "セリフ" => 0.4, "退く" => 1.2, _ => 0.3 });
                    string name = $"{_play3dPlan}_{++_play3dShot:00}_{(HunterBeat == "退く" ? "勇者は退いた" : "登場_" + HunterBeat)}.png";
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_play3dDir!, name));
                    GD.Print($"MAP3D_PLAY shot={name} 登場の演出 {HunterBeat}");
                }
                else await Frames(2);
                _play3dGuard--;   // 演出を待つあいだは歩数に数えない
                continue;
            }
            if (_play3dGuard % 40 == 0) GD.Print($"MAP3D_PLAY 診断 step={_play3dGuard} running={Map11Session.TurnRunning} reform={_reform} 窓: 遭遇={_encounterOverlay.Visible} 迎撃={_interceptOverlay.Visible} 連戦={_siegeOverlay.Visible} 確かめ={_confirmOverlay.Visible} 結果={_resultOverlay.Visible} 候補={St.RelicOffer.Count} pending={Map11Session.HasPendingBattle} {State()}");
            if (_introOverlay is { Visible: true })
            {
                await Shot("頭の札", once: true);
                if (Find(_introOverlay, b => b.Text == "始める") is { } go) Click(OldUi(go));
                await Wait(0.5);
                continue;
            }
            if (_resultOverlay.Visible) { await Shot("結果"); Finish(true); return; }
            if (_investOverlay is { Visible: true })
            {
                await Shot("投資の札", once: true);
                if (Find(_investOverlay, _ => true) is { } card) Click(OldUi(card));
                await Wait(0.5);
                if (_investOverlay is { Visible: true }) { Chapter1.AutoInvest(); _investOverlay.Visible = false; Refresh(); }   // 押せなければ器具の選び方で
                continue;
            }
            if ((Play12 || Play13) && St.RelicOffer.Count > 0)
            {
                // 器具の口: レリックの候補は 1 枚目を取り、主力の先頭の駒に持たせる（候補の窓の中の操作は見ない）。
                await Shot("レリックの候補");
                _play3dCovers.Add("レリックの候補");
                string got = RelicCatalog.Info(St.RelicOffer[0]).Name;
                St.ChooseRelic(0);
                if (St.RelicHand.Count > 0 && _play3dMain >= 0 && St.Squads[_play3dMain].Units?.FirstOrDefault(u => u.IsAlive && u.Relic is null && St.CanEquip(u)) is { } holder)
                    St.EquipRelic(holder, St.RelicHand[^1]);
                GD.Print($"MAP3D_PLAY relic={got}");
                Refresh();
                await Wait(0.4);
                continue;
            }
            // 第479期: 盤を隠す窓がほかに出ていれば数える（頭の札・投資の札・結果の窓・確かめの窓・レリックの候補は上で扱っている）。
            if (CoverShown && !_confirmOverlay.Visible) _play3dCovers.Add("名前の分からない窓");
            if (_hud3d is { SayShown: true } && _play3dOnce.Add("say:" + _hud3d.SayText)) { _play3dSays++; await Shot($"セリフ_{_play3dSays}"); }
            if (Play12 && _siegeOverlay.Visible)
            {
                _play3dSides++;
                await Shot("本陣の連戦の窓");
                // 次が勇者で、もう 1 隊がまだ本陣の手前に着いていなければ引く。それ以外は続けて戦う。
                Map11State.Node? nextFoe = Map11Session.SiegeNode;   // 第479期: 連戦の札のボタンは短い文になった（相手は盤に聞く）
                bool hero = nextFoe is { IsHero: true };
                bool weak = _siegeHp.Value < 45;   // 傷が深ければ引く（引いた先の中間拠点で休む）
                int other = Enumerable.Range(0, St.SquadCap).FirstOrDefault(i => (i == _play3dMain || i == _play3dSecond) && St.Squads[i].Pos != St.Graph.Portal, -1);
                bool alone = other >= 0 && !St.Squads[other].Lost && St.Squads[other].Units is { } ou && ou.Any(u => u.IsAlive)
                             && !St.Graph.Lanes.Any(l => l[^1] == St.Squads[other].Pos);
                // 第478期: 次が砦の守将なら、4 枚以下か HP 70% 未満のとき引いて休む（期限の 2 刻前からは続ける）。
                bool keeper = nextFoe?.Def.Name == MapStage.KeeperName;
                int mine = Map11Session.SiegeSquad >= 0 ? St.Squads[Map11Session.SiegeSquad].Units?.Count(u => u.IsAlive) ?? 0 : 0;
                // 勇者のいない盤では、次が守将でなくても（本陣の守り・湧いた部隊）3 枚以下か HP 50% 未満なら引く——頭の札の「削られたら中間拠点へ引いて休める」のとおり。
                bool back = keeper ? (mine < 5 || _siegeHp.Value < 70) && St.Turn < St.Stage.TurnCap - 2
                    : !St.Stage.Hero.On ? (mine <= 2 || _siegeHp.Value < 40) && St.Turn < St.Stage.TurnCap - 2 : hero && (alone || weak);
                // 第479期の続き: 「引いて待つ」に触れると引く先が光る——写しを 1 枚撮って、輪が画面の上 2／3 に入っているかを書く。
                ClashBackHover = true;
                await Wait(0.9);
                {
                    Vector2 v = GetViewport().GetVisibleRect().Size;
                    Rect2 ring = ClashBackRect;
                    bool seen = ClashBackCell >= 0 && new Rect2(0, 0, v.X, v.Y * 2f / 3f).Intersects(ring);
                    GD.Print($"MAP3D_PLAY back-hint 引く先 {(ClashBackCell >= 0 ? PlaceName(ClashBackCell) : "—")} 輪 {ring} 画面の上 2／3 に見える {seen} 名札と固定の UI の重なり {PlatesUnderHud3D()}");
                    await Shot("引く先の輪", once: true);
                }
                ClashBackHover = false;
                if (_play3dReadOdds && St.Turn < St.Stage.TurnCap - 3) back = ClashOddsText == "劣勢";   // 印を読む遊び方: 劣勢なら引いて待つ・それ以外は続ける
                Click(OldUi(back ? _clashB : _clashA));   // 第479期の追記: 遭遇の台のボタン
                GD.Print($"MAP3D_PLAY siege={(back ? "引く" : "続けて戦う")} next={_siegeGo.Text} hp={_siegeHp.Value:F0} 上の行「{string.Join("／", _siegeStats.Text.Split((char)10))}」引くボタン「{_siegeBack.Text}」窓の文「{string.Join("／", _siegeBody.Text.Split((char)10))}」");
                await Wait(back ? 0.6 : 3.0);
                continue;
            }
            if (_encounterOverlay.Visible)
            {
                _play3dSides++;
                await Shot($"遭遇_{_encounterTitle.Text.Replace(' ', '_').Replace('×', 'x').Replace('　', '_')}");
                GD.Print($"MAP3D_PLAY clash 台={ClashRect} 勝ち目「{ClashOddsText}」敵 {_clashFoeLead.NameText} 席 {_clashFoe.SeatCount} 味方 {_clashOwnLead.NameText} 席 {_clashOwn.SeatCount} 線 {_clashOwn.LinkCount} 引き返す {_clashB.Visible} 割れの印 {ClashRiskCount} 名札と固定の UI の重なり {PlatesUnderHud3D()}");
                if (Play13 && Map11Session.PendingNode is { IsHero: true } hn)
                {
                    int ps = Map11Session.PendingSquad;
                    for (int f = 0; f < 200 && ClashOddsText == "…"; f++) await Frames(2);
                    _play3dHunterLog.Add($"{Map11Session.ShownTurn} 刻目 {St.Squads[ps].Label}（{St.Squads[ps].Units!.Count(u => u.IsAlive)} 枚: {string.Join("・", St.Squads[ps].Units!.Where(u => u.IsAlive).Select(u => u.Def.Name))}）が{(!St.IsRearFight(ps, Map11Session.FoePhase) ? "追いつかれた" : Map11Session.FoePhase ? "殿に出して勇者の番に当たった" : "自分から入った")}"
                        + $" ・ 台の印「{ClashHoldText}」勝ち目「{ClashOddsText}」セリフ {ClashSayText} ・ 隊の札の印「{_hud3d!.CardTag(St.Squads.Take(ps + 1).Count(q => !St.IsLocked(q.Index)) - 1)}」 ・ 勇者 HP {hn.HpNow}");
                    await Shot($"勇者との会戦の台_{St.Squads[ps].Label}");
                }
                if (_play3dReadOdds && ClashShown && _clashB.Visible && ClashOddsText == "劣勢" && St.Turn < St.Stage.TurnCap - 3)
                {
                    int sq = Map11Session.PendingSquad, from = St.Squads[sq].Pos;
                    ClashBackHover = true;
                    await Wait(0.9);
                    await Shot("引き返す先の輪", once: true);
                    ClashBackHover = false;
                    Click(OldUi(_clashB));
                    await Wait(0.8);
                    _play3dDeclines++;
                    GD.Print($"MAP3D_PLAY decline {St.Squads[sq].Label} {PlaceName(from)} → {PlaceName(St.Squads[sq].Pos)} 行き先「{Map11Session.OrderText(sq)}」台は消えた {!ClashShown} 引き返した回数 {St.EncounterRetreats}");
                    continue;
                }
                if (ClashShown && _clashA.Visible) { Click(OldUi(_clashA)); await Wait(3.0); continue; }
                Fight();
                return;
            }
            if (_interceptOverlay.Visible)
            {
                _play3dSides++;
                await Shot("迎撃の窓");
                if (ClashShown && _clashA.Visible) Click(OldUi(_clashA));
                await Wait(0.5);
                continue;
            }
            if (_confirmOverlay.Visible)
            {
                _play3dCovers.Add("確かめの窓");
                await Shot("確かめの窓", once: true);
                if (Find(_confirmOverlay, b => b.Text == "このまま出る") is { } go) Click(OldUi(go));
                await Wait(0.5);
                continue;
            }
            if (_reform)
            {
                if (!_play3dFormed && Play13)
                {
                    // 1-3 は持ち越した隊のまま出る。主力 ＝ いちばん枚数の多い隊（谷の橋へ）・別働 ＝ 次（森の集落へ）。
                    var live = Enumerable.Range(0, St.SquadCap).Where(i => St.Squads[i].Units is { Count: > 0 })
                        .OrderByDescending(i => St.Squads[i].Units!.Count).ThenBy(i => i).ToList();
                    _play3dMain = live.Count > 0 ? live[0] : 0;
                    _play3dSecond = live.Count > 1 ? live[1] : -1;
                    await Shot("編成の窓_持ち越した隊");
                    // 器具の口: 殿の隊を作る（hb1 ＝ いちばん硬い 1 枚 ／ hb2 ＝ いちばん脆い 1 枚 ／ hb3 ＝ 硬い順に 3 枚）。残りは手札へ下げる。
                    if (_play3dPlan == "hr" && live.Count > 2)
                    {
                        // 追記 4: 殿を 1 隊（留守番の隊をそのまま・主力の後ろを付いていく）。
                        _play3dRear = live[2];
                        _play3dRearSize = St.Squads[_play3dRear].Units!.Count;
                        _play3dRearIds.Clear();
                        foreach (UnitState u in St.Squads[_play3dRear].Units!) _play3dRearIds.Add(St.Stage.Stars.On ? St.Stage.Stars.BaseOf(u.Def).Id : u.Def.Id);
                    }
                    if (_play3dPlan is "hb1" or "hb2" or "hb3" && live.Count > 2)
                    {
                        // 留守番の隊（主力・別働のほか）のうち、いちばん硬い駒（hb1・hb3）／ いちばん脆い駒（hb2）のいる隊を殿にする。
                        var spare = live.Skip(2).ToList();
                        _play3dRear = _play3dPlan == "hb2" ? spare.OrderBy(i => St.Squads[i].Units!.Min(u => u.MaxHp)).First() : spare.OrderByDescending(i => St.Squads[i].Units!.Max(u => u.MaxHp)).First();
                        var units = St.Squads[_play3dRear].Units!.OrderByDescending(u => u.MaxHp).ThenBy(u => u.Slot).ToList();
                        var keep = _play3dPlan == "hb1" ? units.Take(1).ToList() : _play3dPlan == "hb2" ? units.TakeLast(1).ToList() : units.Take(3).ToList();
                        foreach (UnitState u in units.Except(keep).ToList()) St.SwapWithBench(_play3dRear, u.Slot, -1);
                        _play3dRearSize = keep.Count;
                        _play3dRearIds.Clear();
                        foreach (UnitState u in keep) _play3dRearIds.Add(St.Stage.Stars.On ? St.Stage.Stars.BaseOf(u.Def).Id : u.Def.Id);
                        Refresh();
                        await Shot("編成の窓_殿の隊を作った（器具の口）");
                    }
                    GD.Print($"MAP3D_PLAY roles main=第{_play3dMain + 1}隊 second={(_play3dSecond < 0 ? "—" : $"第{_play3dSecond + 1}隊")} rear={(_play3dRear < 0 ? "—" : $"第{_play3dRear + 1}隊（{string.Join("・", St.Squads[_play3dRear].Units!.Select(u => $"{u.Def.Name} HP{u.MaxHp}"))}）")}");
                    _play3dFormed = true;
                }
                if (!_play3dFormed && Play12)
                {
                    // 1-2 は持ち越した隊のまま出る。主力 ＝ いちばん枚数の多い隊・別働 ＝ 次・残りは留守番（拠点で待つ）。
                    var live = Enumerable.Range(0, St.SquadCap).Where(i => St.Squads[i].Units is { Count: > 0 })
                        .OrderByDescending(i => St.Squads[i].Units!.Count).ThenBy(i => i).ToList();
                    _play3dMain = live.Count > 0 ? live[0] : 0;
                    _play3dSecond = live.Count > 1 ? live[1] : -1;
                    int guard = live.Count > 2 ? live[2] : -1;
                    await Shot("編成の窓_持ち越した隊");
                    if (_play3dPlan == "p3" && guard >= 0 && _play3dSecond >= 0 && St.Squads[guard].Units!.Count < 3 && St.Squads[_play3dSecond].Units!.Count > 3)
                    {
                        // 器具の口: 方針 3 は留守番を 3 枚にする（別働の後ろの 1 枚を留守番の空いた席へ）。
                        int from = St.Squads[_play3dSecond].Units!.Max(u => u.Slot);
                        int to = FormationRules.PlayableSlots.First(x => St.UnitAt(guard, x) is null);
                        St.SwapSeats(_play3dSecond, from, guard, to);
                        Refresh();
                        await Shot("編成の窓_留守番を3枚に（器具の口）");
                    }
                    GD.Print($"MAP3D_PLAY roles main=第{_play3dMain + 1}隊 second={(_play3dSecond < 0 ? "—" : $"第{_play3dSecond + 1}隊")} guard={(guard < 0 ? "—" : $"第{guard + 1}隊")}");
                    _play3dFormed = true;
                }
                if (!_play3dFormed)
                {
                    await Shot("編成の窓_開いたところ");
                    Click(OldUi(_defaultButton));
                    await Wait(0.4);
                    await Shot("編成の窓_既定の組み方を押した直後");
                    if (_play3dPlan == "split")
                    {
                        // 器具の口: 金 8 で 2 枚呼び、ムグのいる第2隊の空いた席へ（編成の窓の中の操作は通さない）。
                        while (St.Recruit(0)) { }
                        foreach (int slot in new[] { 0, 3, 2, 4 })
                            if (St.Bench.Count > 0 && St.UnitAt(1, slot) is null) St.SwapWithBench(1, slot, 0);
                        Refresh();
                        await Shot("編成の窓_2枚呼んで第2隊へ（器具の口）");
                    }
                    _play3dFormed = true;
                }
                Click(OldUi(_reformButton));   // 「出撃」
                await Wait(0.6);
                continue;
            }
            if (_clashMode == ClashMode.Result && _play3dOnce.Add($"result:{Map11Session.BattleLog.Count}")) { await Shot("戦闘の後_台の上の結果"); continue; }
            if (Map11Session.TurnRunning || St.Finished) { await Wait(0.3); continue; }
            await Shot("作戦の画面_始め", once: true);

            if (Play13)
            {
                // ---- 1-3「狩る者」の命令（第480期）。前半: 主力は谷の橋・別働は森の集落を取りに行き、取ったら休む。殿の隊は橋が取れてから橋へ。
                //      撤退の局面: ha ＝ 全員拠点へ ／ hb* ＝ 殿の隊は勇者のいるマスへ・ほかは拠点へ ／ hc ＝ 全員、勇者のいるマスへ（近い順に当たる）。
                MapGraph g = St.Graph;
                int bridge = g.Lanes[1].First(c => g.IsOutpost(c)), village = g.Lanes[2].First(c => g.IsOutpost(c));
                bool Up(int i) => i >= 0 && !St.Squads[i].Lost && St.Squads[i].Units is { } us && us.Any(u => u.IsAlive);
                if (St.Rout && _play3dHeroTurn < 0)
                {
                    _play3dHeroTurn = St.Turn;
                    _play3dHunterLog.Add($"勇者が現れた: {Map11Session.ShownTurn} 刻目 ・ 中間拠点 {St.HeldOutposts.Count} ・ 隊 {string.Join(" ／ ", St.Squads.Where(q => Up(q.Index)).Select(q => $"{q.Label} {PlaceName(q.Pos)} {q.Units!.Count(u => u.IsAlive)}枚"))}");
                    await Shot("撤退の局面_始め");
                    // 隊の札の足止めの印（裏の数えが終わるのを待って読む）。
                    await Wait(1.5);
                    _play3dHunterLog.Add("隊の札の印: " + string.Join(" ／ ", St.Squads.Where(q => !St.IsLocked(q.Index)).Select((q, n) => $"{q.Label}「{_hud3d!.CardTag(n)}」")));
                    await Shot("撤退の局面_隊の札の印");
                }
                if (!St.Rout)
                {
                    int gold0 = St.Gold, fallen0 = St.FallenUnits.Count;
                    Chapter1Stage12Smoke.Spend(St);
                    // 殿の隊は決めた枚数のまま（呼び戻した駒が空いた席へ入ったら、手札へ下げる）。
                    if (_play3dRear >= 0 && St.Squads[_play3dRear].Units is { } ru && ru.Count > _play3dRearSize && St.CanReform(_play3dRear))
                        foreach (UnitState extra in ru.Where(u => !_play3dRearIds.Contains(St.Stage.Stars.On ? St.Stage.Stars.BaseOf(u.Def).Id : u.Def.Id)).ToList()) St.SwapWithBench(_play3dRear, extra.Slot, -1);
                    if (St.Gold != gold0) { Refresh(); GD.Print($"MAP3D_PLAY 金を使った {gold0} → {St.Gold}（呼び戻した {fallen0 - St.FallenUnits.Count} 枚・強化 {St.Enhances} 回）"); }
                }
                foreach (int squad in new[] { _play3dMain, _play3dSecond, _play3dRear }.Concat(Enumerable.Range(0, St.SquadCap)).Distinct())
                {
                    if (!Up(squad) || !St.CanSend(squad)) continue;
                    Dest want;
                    if (St.Rout)
                    {
                        bool strike = _play3dPlan == "hc" || (squad == _play3dRear && _play3dPlan is "hb1" or "hb2" or "hb3" or "hr");
                        Map11State.Node? hero = St.Hero is { Cleared: false } h ? h : null;
                        // 殿（hb*）は、勇者が隣のマスへ来るまでその場で待ち、隣へ来たら自分から入る（自分から入った戦闘だけが足を止める）。
                        // hc は全員、勇者のいるマスへ歩く（近い順に当たる）。
                        // 間合い: 勇者は 1 刻 2 歩・隊は 1 歩なので、隊の番が終わったときに勇者まで 3 歩なら、勇者は隣のマスで止まる——次の刻に自分から入れる。
                        int here = St.Squads[squad].Pos;
                        List<int> toHero = hero is null ? new List<int>() : g.Path(here, -1, hero.Pos, -1);
                        int gap = toHero.Count - 1;
                        // 第480期 追記 4（殿は (C)）: 殿の隊は、勇者の駒に触れて「殿に出す」を押す（行き先が勇者を追う・間合いは数えない）。
                        if (strike && hero is not null && _play3dPlan != "hc")
                        {
                            if (!Map11Session.IsRear(squad) && here != hero.Pos) await RearOrder(squad);
                            continue;
                        }
                        if (!strike || hero is null) want = Dest.Home;
                        else if (_play3dPlan == "hc") want = hero.Pos != g.Portal ? Dest.Of(g, hero.Pos) : Dest.Of(g, here == g.Home ? g.Lanes[1][1] : here);
                        else if (gap == 1 && hero.Pos != g.Portal) want = Dest.Of(g, hero.Pos);                    // 隣にいる: 自分から入る
                        else if (gap >= 4 && toHero[1] != g.Portal) want = Dest.Of(g, toHero[1]);                  // 遠い: 1 歩寄る
                        else if (gap == 2 && here != g.Home) want = Dest.Home;                                     // 近すぎる（このままだと追いつかれる）: 1 歩下がる
                        else want = here == g.Home ? Dest.Of(g, g.Lanes[1][1]) : Dest.Of(g, here);                 // 3 歩: その場で待つ
                        if (strike && _play3dPlan != "hc") GD.Print($"MAP3D_PLAY 殿の間合い {St.Squads[squad].Label} {PlaceName(here)} 勇者まで {gap} 歩 → {Map11Orders.Where(g, want)}");
                    }
                    else if (_play3dPlan == "hd") { if (squad != _play3dMain) continue; want = Dest.Of(g, St.HeldOutposts.Contains(bridge) ? village : bridge); }
                    else if (_play3dPlan == "hn") { if (squad != _play3dMain && squad != _play3dSecond) continue; want = Dest.Of(g, squad == _play3dMain ? g.Lanes[0].First(c => g.IsOutpost(c)) : bridge); }
                    else if (squad == _play3dMain) want = Dest.Of(g, bridge);
                    else if (squad == _play3dSecond) want = Dest.Of(g, village);
                    else if (squad == _play3dRear)
                    {
                        // 殿の隊は主力の 1 マス後ろを付いていく（敵は待ち構えていて歩かないので、主力の後ろは安全）。橋が取れたら橋で待つ。
                        int mainAt = Up(_play3dMain) ? g.IndexOn(1, St.Squads[_play3dMain].Pos) : -1;
                        want = St.HeldOutposts.Contains(bridge) ? Dest.Of(g, bridge) : mainAt >= 1 ? Dest.Of(g, g.Lanes[1][mainAt - 1]) : Dest.Home;
                    }
                    else continue;   // 残りの隊は拠点で留守番
                    await Order(squad, want);
                }
            }
            else if (Play12)
            {
                // ---- 1-2 の命令（第477期）: 隊の札を押して選び、行き先が画面の外なら全体図を押して視点を動かし、マスに触れて案内を読み、押して決める ----
                MapGraph g = St.Graph;
                int gate = g.Lanes[1].First(c => g.IsOutpost(c)), tower = g.Lanes[0].First(c => g.IsOutpost(c)), ferry = g.Lanes[2].First(c => g.IsOutpost(c));
                (int[] Seq, int Lane) Route(int i) => i == _play3dMain
                    ? _play3dPlan switch { "p2" => (new[] { gate }, 1), "p3" => (new[] { tower }, 0), _ => (new[] { gate, tower }, 0) }
                    : _play3dPlan == "p1" ? (new[] { gate }, 1) : (new[] { ferry }, 2);
                bool Up(int i) => i >= 0 && !St.Squads[i].Lost && St.Squads[i].Units is { } us && us.Any(u => u.IsAlive);
                bool Staged(int i) => !Up(i) || St.Squads[i].Pos == g.Portal || St.Squads[i].Pos == g.Lanes[Route(i).Lane][^1];
                Dest Want(int i)
                {
                    (int[] seq, int lane) = Route(i);
                    int other = i == _play3dMain ? _play3dSecond : _play3dMain;
                    if (St.Squads[i].Pos == g.Portal) return Dest.Portal(g, lane);
                    // 取った中間拠点で傷が深ければ、動かずに休む（隊ごとに 4 回まで）。
                    // 勇者のいない盤（第478期）では、門が開いた後も 70% 未満のときだけ休み、期限の 4 刻前からは休まない（1 回目の通しは休みすぎて時間切れになった）。
                    int restBelow = !St.Stage.Hero.On ? (St.Turn >= St.Stage.TurnCap - 4 ? 0 : St.GateOpen ? 70 : 60) : St.GateOpen ? 85 : 60;
                    if (St.AtOutpost(i) && _play3dRests[i] < 4 && SquadHpPercent(St.Squads[i]) < restBelow) { _play3dRests[i]++; return Dest.Of(g, St.Squads[i].Pos); }
                    if (St.GateOpen)
                    {
                        if (i == _play3dSecond && _play3dPlan == "p1" && Up(_play3dMain) && !Staged(_play3dMain)) return Dest.Of(g, gate);
                        int stage = g.Lanes[lane][^1];
                        if (St.Squads[i].Pos != stage) return Dest.Of(g, stage);
                        // 勇者のいない盤（第478期）では、もう 1 隊がそろうのを待たない。
                        return !St.Stage.Hero.On || Staged(other) || St.Turn >= St.Stage.TurnCap - 3 ? Dest.Portal(g, lane) : Dest.Of(g, stage);
                    }
                    foreach (int cell in seq) if (!St.HeldOutposts.Contains(cell)) return Dest.Of(g, cell);
                    return Dest.Of(g, seq[^1]);
                }
                // 器具の口: 拠点・取った中間拠点にいる隊がいれば、倒れた駒を呼び戻して空いた席へ入れ、残りの金で強化する（編成の窓の中の操作は見ない）。
                {
                    int gold0 = St.Gold, fallen0 = St.FallenUnits.Count;
                    Chapter1Stage12Smoke.Spend(St);
                    if (St.Gold != gold0) { Refresh(); GD.Print($"MAP3D_PLAY 金を使った {gold0} → {St.Gold}（呼び戻した {fallen0 - St.FallenUnits.Count} 枚・強化 {St.Enhances} 回）"); }
                }
                if (St.Hero is not null && _play3dHeroSeen == 0) { _play3dHeroSeen = 1; _cam3d!.FocusOn(PosOf(g.Portal), snap: true); await Frames(3); await Shot("勇者が現れた"); }
                foreach (int squad in new[] { _play3dMain, _play3dSecond })
                {
                    if (!Up(squad) || !St.CanSend(squad)) continue;
                    await Order(squad, Want(squad));
                }
            }
            else
            // ---- 命令: 駒を押して隊を選び、マスに触れて案内を読み、押して行き先を決める ----
            foreach (int squad in _play3dPlan == "split" ? new[] { 0, 1 } : new[] { 0 })
            {
                Map11State.Squad s = St.Squads[squad];
                if (s.Lost || !St.CanSend(squad)) continue;
                bool south = _play3dPlan == "south" || squad == 1;
                bool deepSouth = St.Graph[s.Pos].Lane == 1 && St.Graph.IndexOn(1, s.Pos) >= 1;
                Dest want = !south || deepSouth ? Dest.Portal(St.Graph, south ? 1 : 0) : Dest.Of(St.Graph, St.Graph.Lanes[1][^1]);
                if (Map11Session.Destinations[squad] == want || St.AtDest(squad, want)) continue;
                var piece = _mapPicks.FirstOrDefault(p => p.Foe is null && p.Squad == squad);
                if (piece.Marker is null || !_pieces3d.TryGetValue(piece.Marker, out Map3DPiece? body)) continue;
                Click(body.ScreenRect(_camera).GetCenter());
                await Frames(4);
                if (_selected != squad) { Click(_hud3d!.CardRect(squad).GetCenter()); await Frames(4); }
                Vector2 at = _camera.UnprojectPosition(_cellMarks3d[want.IsPortal ? St.Graph.Portal : want.Id].GlobalPosition);
                Move(at);
                await Frames(4);
                await Shot($"第{squad + 1}隊_行き先に触れた_{(want.IsPortal ? "敵の拠点" : PlaceName(want.Id))}");
                Click(at);
                await Frames(4);
                bool set = Map11Session.Destinations[squad] == want || (want.IsPortal && Map11Session.Destinations[squad].IsPortal);
                await Shot($"第{squad + 1}隊_行き先を{(set ? "決めた" : "決められなかった")}");
                if (!set) Map11Session.SetDestination(squad, want);
            }
            Move(new Vector2(GetViewport().GetVisibleRect().Size.X / 2, 140));
            await Frames(3);
            int before = St.Turn;
            Click(_hud3d!.StepRect.GetCenter());   // 「1 刻」
            await Wait(1.2);
            if (IsInsideTree() && !Map11Session.TurnRunning && St.Turn != before && !St.Finished && !_encounterOverlay.Visible && !_interceptOverlay.Visible)
                await Shot($"作戦ターン{before + 1}の後");
            // 「1 刻」が効かなかったとき（窓も出ず、刻も流れず、作戦ターンも進まない）は、案内の行と押した場所を残す（第478期・方針 3 が止まる件の調べ）。
            else if (IsInsideTree() && !Map11Session.TurnRunning && St.Turn == before && !St.Finished && !_encounterOverlay.Visible && !_interceptOverlay.Visible && !_siegeOverlay.Visible
                     && !_confirmOverlay.Visible && !_reform)
            {
                Move(_hud3d!.StepRect.GetCenter());
                await Frames(3);
                Control? under = GetViewport().GuiGetHoveredControl();
                GD.Print($"MAP3D_PLAY 押した所の上にある物={(under is null ? "無し" : under.GetPath().ToString())} 文「{(under is Button ub ? ub.Text : "")}」四角={under?.GetGlobalRect()} 行き先の札か={_destButtons.Any(d => ReferenceEquals(d.Node, under))} 層の拡大={_uiScale:F2} filter={under?.MouseFilter} disabled={_hud3d.StepDisabled} 誰も出ていない={NothingDeployed()} drafting={Map11Session.Drafting} bench={St.Bench.Count}");
                if (++_play3dStalls >= 3) { Finish(false, "stall"); return; }
                GD.Print($"MAP3D_PLAY 1刻が効かない turn={St.Turn} 押した所={_hud3d!.StepRect} 画面={GetViewport().GetVisibleRect().Size} 下の UI の割合={_hud3d.BottomShare * 100:F0}% 隊の札={string.Join("・", Enumerable.Range(0, St.SquadCap).Select(i => _hud3d.CardRect(i).Size.Y.ToString("F0")))} 案内「{_toast.Text}」「{_orderHint.Text}」迎撃の合間={Map11Session.InterceptBreak} 時計={Map11Clock.On}/{Map11Clock.Running} running={_running} "
                         + $"行き先 {string.Join(" ／ ", Enumerable.Range(0, St.SquadCap).Select(i => $"{St.Squads[i].Label}:{(St.CanSend(i) ? Map11Orders.Where(St.Graph, Map11Session.Destinations[i]) : "出せない")}"))}");
            }
        }
    }
}
