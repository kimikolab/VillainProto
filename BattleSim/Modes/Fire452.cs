using BattleCore;
using static Common;

// =====================================================================================
// fire452 モード（第452期 段0・指示書 design/PHASE452_MAP_RELIC_SPEC.md §0）—— 火の部隊 × ホタの台。**報告のみ・線は置かない。**
// 味方 ＝ `Presets.MapBoards` の「試遊・火の部隊 × ホタ」（ボルグ抜きの燃焼の並び）、敵 ＝ 作戦マップの火の魔術師の部隊（`band_fire`）・
// 対照 ＝ 同じ味方 × 盾の列（`band_shields`・火なし）。倍率は作戦マップと同じく掛けない。seed 0..199。
// ホタが燃えていた刻 ＝ その刻の頭の残量（`StatusSnapshot`）が 1 以上か、その刻に火が点いた（`StatusGain`）刻。台本から数えるので verbose で回す。
//
//     dotnet run --project BattleSim -c Release 0 fire452
// =====================================================================================

static class Fire452Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        Formation board = Presets.MapBoards.First(r => r.Name == "試遊・火の部隊 × ホタ").F;
        // 倍率なし（作戦マップと同じ）。試遊の波のボス（規定形）が倍率を掛けない値を持っているのでそれを借りる——型の名前を書くと `derive rules` の「測った診断」の列にこのモードが載るため。
        var NoScale = EnemyCatalog.PlaytestStages[0].Scale;
        Console.WriteLine("# 第452期 段0 —— 火の部隊 × ホタ（`fire452`・報告のみ）");
        Console.WriteLine();
        Console.WriteLine($"味方: {string.Join(" ／ ", board.Occupied().OrderBy(o => o.Slot).Select(o => $"{FormationRules.SeatNames[o.Slot]} {o.Def.Name}"))}・seed 0..199・敵の倍率なし");
        Console.WriteLine();
        Console.WriteLine("| 敵 | 勝率 | 決着T（平均） | ホタが燃えた戦 | ホタが燃えていた刻の割合 | ホタの与ダメのうち燃えている間の割合 | ホタの与ダメ（平均） | ホタが倒れた戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string id in new[] { "band_fire", "band_shields" })
        {
            EnemyCatalog.MapSquad m = EnemyCatalog.MapSquadOf(id);
            int wins = 0, burnedBattles = 0, hotaDied = 0;
            long turns = 0, burnTurns = 0, aliveTurns = 0, dmg = 0, dmgBurning = 0;
            const int N = 200;
            for (int seed = 0; seed < N; seed++)
            {
                var p = BattleEngine.Materialize(board, BattleContext.PlayerTeam);
                var e = BattleEngine.Materialize(m.Enemy, BattleContext.EnemyTeam, NoScale);
                BattleResult r = BattleEngine.Run(p, e, seed, verbose: true);
                UnitState hota = p.First(u => u.Def.Id == UnitCatalog.Hota.Id);
                if (r.PlayerWon) wins++;
                turns += r.Turns;
                if (!hota.IsAlive) hotaDied++;
                var burning = new HashSet<int>();
                foreach (BattleEvent ev in r.Events)
                {
                    if (ev.TargetId != hota.InstanceId || ev.Text != StatusKeys.Burn) continue;
                    if ((ev.Kind == BattleEventKind.StatusSnapshot && ev.Amount > 0) || ev.Kind == BattleEventKind.StatusGain) burning.Add(ev.Turn);
                }
                if (burning.Count > 0) burnedBattles++;
                int lastTurn = r.Events.Where(ev => ev.Kind == BattleEventKind.Death && ev.TargetId == hota.InstanceId).Select(ev => ev.Turn).DefaultIfEmpty(r.Turns).First();
                aliveTurns += lastTurn;
                burnTurns += burning.Count(t => t <= lastTurn);
                foreach (BattleEvent ev in r.Events)
                {
                    if (ev.Kind != BattleEventKind.Damage || ev.ActorId != hota.InstanceId || ev.TargetId == hota.InstanceId || ev.FriendlyFire) continue;
                    dmg += ev.Amount;
                    if (burning.Contains(ev.Turn)) dmgBurning += ev.Amount;
                }
            }
            Console.WriteLine($"| 作戦マップ・{m.Name} | {100.0 * wins / N:F1}% | {(double)turns / N:F1} | {100.0 * burnedBattles / N:F1}% | "
                + $"{(aliveTurns == 0 ? 0 : 100.0 * burnTurns / aliveTurns):F1}% | {(dmg == 0 ? 0 : 100.0 * dmgBurning / dmg):F1}% | {(double)dmg / N:F1} | {100.0 * hotaDied / N:F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine("- 「ホタが燃えていた刻の割合」の分母はホタが生きていた刻（倒れた刻まで）。燃えていた刻 ＝ 刻の頭の燃焼の残量が 1 以上か、その刻に火が点いた刻");
        Console.WriteLine("- 「与ダメ」は台本の `Damage` のうちホタが出どころのもの（味方への巻き込みを除く）");
    }
}
