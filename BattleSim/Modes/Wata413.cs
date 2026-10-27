using BattleCore;
using static Common;
using M = Mudo412Diag;
using S307 = Som307Diag;

// =====================================================================================
// wata413 —— 第413期 段0: ムド M-ud を規定に ／ 本体【軽い期・版だけ】: ワタの渡し手番（W-u 刃を呼ぶ ／ W-m 泥を呼ぶ）。
// 指示書は design/PHASE413_WATA_GIFT_SPEC.md ／ 報告は design/PHASE413_WATA_GIFT.md。
//
//     dotnet run --project BattleSim -c Release 0 wata413 s0       # 段0 の確かめ: 「試遊・呪い」「試遊・カーズ 呪い」× 4 波・seed 0..99 が第412期 §4-1 の M-ud の行と同じか（違えば exit 1）
// =====================================================================================
static class Wata413Diag
{
    /// <summary>第412期 §4-1 の M-ud の行（勝率・倒しT ／ 倒れた味方 ／ 尻尾の直 ／T・尻尾の長さ）。</summary>
    static readonly (string Board, string[] Cells)[] Expect412 =
    {
        ("試遊・呪い", new[] { "100.0・6.0T ／ 0.00 ／ 510.0 ／ 6.0T", "100.0・4.0T ／ 0.82 ／ 1.0 ／ 1.0T", "100.0・4.0T ／ 0.94 ／ 9.5 ／ 1.0T", "100.0・6.1T ／ 1.00 ／ 54.2 ／ 1.6T" }),
        ("試遊・カーズ 呪い", new[] { "100.0・7.0T ／ 0.00 ／ 438.8 ／ 7.0T", "100.0・4.8T ／ 0.31 ／ 28.9 ／ 1.2T", "100.0・4.5T ／ 0.62 ／ 17.1 ／ 1.1T", "100.0・6.3T ／ 0.15 ／ 42.0 ／ 1.4T" }),
    };

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "s0";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "s0": S0(); break;
            case "hang": Hang(args.Length > 3 ? args[3] : "速攻 (ボルグ×ムド)"); break;
            default: Console.WriteLine("wata413: モードは s0。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static string Cell(M.Fx a) => $"{a.WT} ／ {(a.N == 0 ? "—" : ((double)a.Fell / a.N).ToString("F2"))} ／ {a.Tail}";

    static void S0()
    {
        Console.WriteLine("# 第413期 段0 の確かめ —— 規定のムド（M-ud）の2台 × 4 波・seed 0..99 が第412期 §4-1 の M-ud の行と同じか");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 第412期 M-ud | いま（規定） | 同じ |");
        Console.WriteLine("|---|---|---|---|---|");
        int same = 0, all = 0;
        foreach (var (board, cells) in Expect412)
        {
            var f = M.BoardOf(board);
            for (int i = 0; i < M.Waves.Length; i++)
            {
                string now = Cell(M.Measure(f, M.Waves[i]));
                bool s = now == cells[i]; all++; if (s) same++;
                Console.WriteLine($"| {board} | {M.Waves[i].Name} | {cells[i]} | {now} | {(s ? "はい" : "**いいえ**")} |");
            }
        }
        Console.WriteLine();
        bool mudoOk = UnitCatalog.Mudo.Traits.SequenceEqual(UnitCatalog.MudoMud.Traits) && UnitCatalog.All.Contains(UnitCatalog.Mudo) && !UnitCatalog.All.Contains(UnitCatalog.MudoH412);
        Console.WriteLine($"- 規定のムドの札が第412期の M-ud と同じ・旧（`MudoH412`）は `All` に無い: {(mudoOk ? "○" : "**×**")}");
        Console.WriteLine($"- 「版・M-ud 呪い」が試遊の台に無い: {(!Presets.Versions.Any(r => r.Name == "版・M-ud 呪い") ? "○" : "**×**")}");
        Console.WriteLine();
        Console.WriteLine($"{all} セルのうち同じ {same}。");
        if (same != all || !mudoOk || Presets.Versions.Any(r => r.Name == "版・M-ud 呪い")) Environment.ExitCode = 1;
    }

    /// <summary>止まる戦を探す（verbose・1戦ごとに 5 秒で打ち切り）。</summary>
    static void Hang(string row)
    {
        var f = CompareBuilds().First(r => r.Name == row).F;
        var waves = new (string, EnemyWave, EnemyScaleRule)[] { ("近衛", EnemyCatalog.EliteGuardWave, EnemyCatalog.EliteScale), ("大隊", EnemyCatalog.EliteBattalionWave, EnemyCatalog.EliteScale) };
        foreach (var (wn, w, sc) in waves)
            for (int seed = 0; seed < 1000; seed++)
            {
                var t = System.Threading.Tasks.Task.Run(() => BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(w, sc), seed, verbose: true));
                if (!t.Wait(5000)) { Console.WriteLine($"止まった: {row} × {wn} × seed {seed}"); Environment.Exit(3); }
                if (t.Result.Turns >= 30) Console.WriteLine($"{wn} seed {seed}: T30");
            }
    }
}
