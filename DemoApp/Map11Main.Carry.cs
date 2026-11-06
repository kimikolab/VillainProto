using BattleCore;

public partial class Map11Main
{
    /// <summary>
    /// 絵の口 `--map-chapter-play=N --map-chapter-stop=K`（第461期）: 1 章を seed N で始め、器具の方針（1-1 ／ 1-2 は分担＋戻り番・1-3 は逃げる）で
    /// ステージ K（0 ＝ 1-1）に<b>入ったところ</b>まで頭なしで回す（負ければやり直す）。持ち越して入ったステージの始め（組み直しの窓）を撮るため。
    /// 戦闘は画面と同じ口（`Map11Session.BeginBattle` ／ `CompleteBattle`）。
    /// </summary>
    /// 第462期: <paramref name="keepIntro"/> ならステージの頭の札を残す（`,card` の絵）。
    private static void PlayChapterUntil(int seed, int stopAt, bool keepIntro = false)
    {
        Map11Session.PickChapter(seed);
        Map12ChangeSmoke.FightHook = (st, squad, node, intercept) =>
        {
            bool begun = intercept ? Map11Session.BeginIntercept(squad, node) : Map11Session.BeginBattle(squad, node);
            if (!begun) return;
            BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: true);
            CampaignSession.ClearCarriedBattle();
            Map11Session.CompleteBattle(r.PlayerWon, r);
        };
        try
        {
            for (int guard = 0; guard < 60 && Chapter1.Active && !Chapter1.Over && Chapter1.Index < stopAt; guard++)
            {
                Map11State st = Map11Session.State!;
                bool last = Chapter1.Index == Chapter1.Stages.Count - 1;
                Map12ChangeSmoke.Greedy(st.Seed, st.Stage, guard: true, split: true, state: st, siegeRest: true,
                    heroPlan: last ? Map12ChangeSmoke.HeroPlan.Flee : Map12ChangeSmoke.HeroPlan.None);
                Chapter1.Settle(st, Map11Session.RelicWorked, Map11Session.BattleLog);
                if (!st.Won) Chapter1.Retry();
                else if (last) break;
                else Chapter1.Next();
            }
        }
        finally { Map12ChangeSmoke.FightHook = null; }
        Chapter1.IntroPending = keepIntro;
    }
}
