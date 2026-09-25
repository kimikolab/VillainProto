using BattleCore;
using static Common;

// =====================================================================================
// galdlast モード（第198期） —— ガルドの最後の段（守る者がいなくなったら、盾を捨てて剣を抜く）
//
// 指示書は design/PHASE198_GALD_LAST_SPEC.md ／ 報告は design/PHASE198_GALD_LAST.md。
//
// **線は置かない**（指示書の冒頭）。回帰確認（ガルドを含まない行が動かないこと）と、
// 版（無／剣／剣・返しなし／盾剣）の対照・帳簿だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 galdlast phase0   # Q0-1（第197期の盤面でガルドが最後の1体になる戦）
//     dotnet run --project BattleSim -c Release 0 galdlast run      # 版 × ガルドの在席行（第2〜5波）
//     dotnet run --project BattleSim -c Release 0 galdlast ledger   # 帳簿（剣の段に入った戦・与えたダメージの内訳）
//     dotnet run --project BattleSim -c Release 0 galdlast check [採用前のbalance.md]  # 自己検査
// =====================================================================================

static partial class GaldLastDiag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "phase199": Phase199(); return;
            default:
                if (RunMore(mode, arg)) return;
                Console.WriteLine("galdlast: モードは phase0 / run / ledger / check。");
                return;
        }
    }

    static partial void RunMoreImpl(string mode, string arg, ref bool handled);

    static bool RunMore(string mode, string arg)
    {
        bool handled = false;
        RunMoreImpl(mode, arg, ref handled);
        return handled;
    }

    /// <summary><c>compare</c> 61 行のうちガルドが在席する行。</summary>
    static IEnumerable<(string Name, Formation F)> GaldRows()
        => CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "gald"));

    static string P(int a, int n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1") + "%";
    static string Avg(double s, int n, string f = "F2") => n == 0 ? "—" : (s / n).ToString(f);

    /// <summary>1行ぶんの「最後の1体」（第2〜5波・seed 0..199）。</summary>
    sealed class Alone
    {
        public int N, Wins, Timeouts, AloneN, AloneWins, AloneTimeouts, OtherAloneN;
        public double AloneTurnSum, RemainSum, HpPctSum, FoesSum, TurnsSum;
    }

    static Alone AloneOf(Formation f, int stFrom = 1, int stTo = 4)
    {
        var a = new Alone();
        for (int st = stFrom; st <= stTo; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                a.N++;
                a.TurnsSum += r.Turns;
                if (r.PlayerWon) a.Wins++;
                bool to = !r.PlayerWon && r.Turns >= BattleEngine.MaxTurns;
                if (to) a.Timeouts++;
                UnitTally? g = r.TallyByUnit.TryGetValue("gald", out UnitTally? gg) ? gg : null;
                if (g is not null && g.AloneTurn > 0)
                {
                    a.AloneN++;
                    if (r.PlayerWon) a.AloneWins++;
                    if (to) a.AloneTimeouts++;
                    a.AloneTurnSum += g.AloneTurn;
                    a.RemainSum += r.Turns - g.AloneTurn;
                    a.HpPctSum += g.AloneMaxHp == 0 ? 0 : 100.0 * g.AloneHp / g.AloneMaxHp;
                    a.FoesSum += g.AloneFoes;
                }
                else if (r.TallyByUnit.Values.Any(t => t.AloneTurn > 0)) a.OtherAloneN++;
            }
        return a;
    }

    static void Phase0()
    {
        Console.WriteLine("# 第198期 `galdlast phase0` —— Q0-1（第197期の盤面・計数だけ）");
        Console.WriteLine();
        Console.WriteLine("## Q0-1 ガルドが最後の1体になる戦");
        Console.WriteLine();
        Console.WriteLine("`compare` 61 行のうちガルドが在席する行 × 第2〜5波 × seed 0..199。"
                          + "`最後の1体` ＝ 味方の死が死亡通知（蘇生・分裂の召喚）を全部通った後に、味方で生きているのがガルドだけになった戦"
                          + "（`UnitTally.AloneTurn`・第198期に足した計数。召喚された駒も味方に数える）。"
                          + "`残りT` ＝ 決着T − なったターン。`HP` はそのときのガルドの HP ÷ 最大HP。`打ち切り` ＝ 30 ターンで決着しなかった負け。"
                          + "`他が最後` ＝ ガルド以外の駒が最後の1体になった戦（ガルドが先に倒れた）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 勝率 | 打ち切り(全) | 最後の1体 | なったT | 残りT | HP | 敵の数 | その戦の勝率 | その戦の打ち切り | 他が最後 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var all = new Alone();
        var perWave = new Alone[5];
        foreach (var (name, f) in GaldRows())
        {
            Alone a = AloneOf(f);
            Add(all, a);
            for (int st = 1; st <= 4; st++) { perWave[st] ??= new Alone(); Add(perWave[st], AloneOf(f, st, st)); }
            Console.WriteLine(Row(name, a));
        }
        Console.WriteLine(Row("**計**", all));
        Console.WriteLine();
        Console.WriteLine("### 波ごと（ガルドの在席行の合計）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 打ち切り(全) | 最後の1体 | なったT | 残りT | HP | 敵の数 | その戦の勝率 | その戦の打ち切り | 他が最後 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int st = 1; st <= 4; st++) Console.WriteLine(Row("第" + (st + 1) + "波", perWave[st]));
        Console.WriteLine();
        Console.WriteLine("在席行: " + GaldRows().Count() + " / " + CompareBuilds().Length
                          + "。`打ち切り` のうち最後の1体の戦の割合: " + P(all.AloneTimeouts, all.Timeouts) + "。");
    }

    static void Add(Alone to, Alone a)
    {
        to.N += a.N; to.Wins += a.Wins; to.Timeouts += a.Timeouts; to.AloneN += a.AloneN; to.AloneWins += a.AloneWins;
        to.AloneTimeouts += a.AloneTimeouts; to.OtherAloneN += a.OtherAloneN; to.AloneTurnSum += a.AloneTurnSum;
        to.RemainSum += a.RemainSum; to.HpPctSum += a.HpPctSum; to.FoesSum += a.FoesSum; to.TurnsSum += a.TurnsSum;
    }

    static string Row(string name, Alone a)
        => "| " + name + " | " + P(a.Wins, a.N) + " | " + P(a.Timeouts, a.N) + " | " + P(a.AloneN, a.N) + " | "
           + Avg(a.AloneTurnSum, a.AloneN, "F1") + " | " + Avg(a.RemainSum, a.AloneN, "F1") + " | "
           + (a.AloneN == 0 ? "—" : (a.HpPctSum / a.AloneN).ToString("F0") + "%") + " | " + Avg(a.FoesSum, a.AloneN) + " | "
           + P(a.AloneWins, a.AloneN) + " | " + P(a.AloneTimeouts, a.AloneN) + " | " + P(a.OtherAloneN, a.N) + " |";
}
