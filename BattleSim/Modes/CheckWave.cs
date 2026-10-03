using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FS = FoeSurgeDiag;

// =====================================================================================
// checkwave —— 第260期「チェック波（手数チェック・1ターン火力チェック）と各軸の棚卸し」・第261期「チェック波の作り直し（ボスの手番・癒し手の体）」。
// 指示書は design/PHASE260_CHECK_WAVE_SPEC.md ／ 報告は design/PHASE260_CHECK_WAVE.md。
// **どちらの波も検証波**（`Stages` / `Columns` / 会戦 / 作戦マップには載せない）。敵はこの器具のローカルで、
// 新しい札（`CheckMend30/50` ／ `BossMendFull/Half` ／ `BossRise4/8`）はロスターの駒に付かない。燃焼の規則は今の規定（`Pre256` に固定しない）。
// 波・台は `firecycle` ／ `foesurge` の定義を引く（複製しない）。倍率はすべて「なし」。
//
//     dotnet run --project BattleSim -c Release 0 checkwave phase0          # Phase 0 の数え物 ＋ 段1 の棚卸し（6台 × 8波 × seed 0..199）
//     dotnet run --project BattleSim -c Release 0 checkwave run             # 第262期: 400/300 の第四波＋癒し手 W2-* ＋ 全体攻撃の動じないボス B2-*（6台 × 5版）と採否の表
//     dotnet run --project BattleSim -c Release 0 checkwave run261          # 第261期: 重装兵の体の癒し手 W-* ＋ 動じないボス B-*（棄却・対照）
//     dotnet run --project BattleSim -c Release 0 checkwave run260          # 第260期の段2・段3（棄却・対照）
//     dotnet run --project BattleSim -c Release 0 checkwave check           # 自己検査
//     dotnet run --project BattleSim -c Release 0 checkwave log <台> <波> [seed]   # 1戦のログ（波は T-30後 ／ T-50後 ／ T-50奥 ／ B-全4 ／ B-全8 ／ B-半4 ／ 第四波写し）
// =====================================================================================
static partial class CheckWaveDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(); return;
            case "run260": Run260(); return;
            case "run261": Run261(); return;
            case "check": CheckImpl(); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "燃焼 T3-244", args.Length > 4 ? args[4] : "B-全4", args.Length > 5 ? int.Parse(args[5]) : 0);
                return;
            default:
                Console.WriteLine("checkwave: モードは phase0 / run / check / log。");
                return;
        }
    }
    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    internal const int Seeds = 200;
    /// <summary>ボス波の打ち切り（§4.1）。<c>MaxTurns</c>（30）は触らず、撃破のターンが 20 を超えた勝ちを負けに数える。的の波の棚卸しも 1〜20 ターン目の窓だけを読む。</summary>
    internal const int BossTurns = 20;

    // ---------------------------------------------------------------------------------
    // 台（§2）——駒は今の規定（ボルグ・ホタ・ヒヨは `UnitCatalog` の規定）。席は各期の定義を引く。
    // ---------------------------------------------------------------------------------
    internal static readonly FB.Ver Now = new("規定", "第258期の規定（ボルグ W4・ホタ・ヒヨ）", UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo);
    internal static readonly string[] Boards = { "燃焼 T3-244", "燃焼 T3-255", "移動", "雷", "毒", "混ぜ-255" };
    /// <summary>軸（予測の向きを書くための札・判定には使わない）。</summary>
    internal static string AxisOf(string b) => b switch { "燃焼 T3-244" or "燃焼 T3-255" => "燃焼", "混ぜ-255" => "混ぜ", _ => b };
    internal static Formation BoardOf(string b) => b switch
    {
        "燃焼 T3-244" => FS.BoardOf("T3-244", Now),
        "燃焼 T3-255" => FS.BoardOf("T3-255", Now),
        "混ぜ-255" => FS.BoardOf("混ぜ-255", Now),
        // 第232期の規定の席（第228期 H3 の1位）。駒は今の規定（`BA.RefMove` は第256期の器具のために旧セロ・旧ハネに固定してあるので使わない）。
        "移動" => Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Sero, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Hane),
        "雷" => BA.RefThunder,
        "毒" => CompareBuilds().First(r => r.Name.StartsWith("毒 (グザ×ミオ×ラウ)")).F,
        _ => throw new ArgumentException(b),
    };

    // ---------------------------------------------------------------------------------
    // 波。棚卸し（段1）は `firecycle` の波の添字（0〜3 本編 第2〜5波 ／ 4 九/新兵 ／ 6 重い波 ／ 7 的・一 ／ 8 的・九）。
    // ---------------------------------------------------------------------------------
    internal static readonly int[] InvWaves = { 0, 1, 2, 3, 4, FC.WaveHeavy, FC.WaveTarget1, FC.WaveTarget9 };
    internal static string InvName(int w) => FC.WaveNames[w];
    internal const int Wave4 = 2;   // 本編第四波（`FC.WaveOf` の添字）

    /// <summary>
    /// 癒し手（§3.1）: 従軍司祭（40/9/8・無特性・踏み込まない）と<b>数値・型・行動・踏み込みを1つも変えず</b>、札1枚だけを足した写し。
    /// Id と名前はログで見分けるためだけに変える（どの規則も敵の Id を読まない——自己検査 (c)）。
    /// </summary>
    internal static UnitDef Healer(TraitId mend) => new()
    {
        Id = "cw_healer", Name = "癒し手", MaxHp = EnemyCatalog.Priest.MaxHp, Attack = EnemyCatalog.Priest.Attack, Speed = EnemyCatalog.Priest.Speed,
        Pattern = EnemyCatalog.Priest.Pattern, Advances = EnemyCatalog.Priest.Advances, Actions = EnemyCatalog.Priest.Actions,
        Traits = new[] { mend }, PlusText = "ターン頭に味方全員を回復する（検証波）",
    };
    static readonly UnitDef Healer30 = Healer(TraitId.CheckMend30), Healer50 = Healer(TraitId.CheckMend50);

    /// <summary>
    /// ボス（§4.1）。<b>この器具のローカル</b>（`EnemyCatalog` にも `compare` にも入れない）。攻12・速5・単体・踏み込む。HP は段1 の実測から決めた（報告書 §3）。
    /// </summary>
    internal static int BossHp = ChosenBossHp;
    internal static UnitDef Boss(TraitId mend, TraitId rise) => new()
    {
        Id = "cw_boss", Name = "ボス", MaxHp = BossHp, Attack = 12, Speed = 5, Pattern = AttackPattern.Single,
        Traits = new[] { mend, rise }, PlusText = "ターン頭に傷が塞がり、毎ターン攻撃力が上がる（検証波）",
    };
    /// <summary>第261期のボス: 第260期のボスに「動じない」（`BossSteadfast`・手番を奪う状態が付かない）を足し、HP を引数で固定する（`BossHp` を読まない）。</summary>
    internal static UnitDef Boss2(TraitId mend, TraitId rise, int hp) => new()
    {
        Id = "cw_boss", Name = "ボス", MaxHp = hp, Attack = 12, Speed = 5, Pattern = AttackPattern.Single,
        Traits = new[] { mend, rise, TraitId.BossSteadfast }, PlusText = "ターン頭に傷が塞がり、毎ターン攻撃力が上がる。転ばず痺れない（検証波）",
    };

    /// <summary>第262期のボス: 第261期のボス（<see cref="Boss2"/>）と同じ数値・札で、攻撃型だけを<b>全体</b>にした。</summary>
    internal static UnitDef Boss3(TraitId mend, TraitId rise, int hp) => new()
    {
        Id = "cw_boss", Name = "ボス", MaxHp = hp, Attack = 12, Speed = 5, Pattern = AttackPattern.All,
        Traits = new[] { mend, rise, TraitId.BossSteadfast }, PlusText = "ターン頭に傷が塞がり、毎ターン攻撃力が上がる。転ばず痺れない。全員を薙ぎ払う（検証波）",
    };

    /// <summary>
    /// 第261期の癒し手: 城塞の重装兵（145/12/3・無特性・踏み込む）と<b>数値・型・行動・踏み込みを1つも変えず</b>、札1枚だけを足した写し。
    /// 司祭ベース（<see cref="Healer"/>・40/9/8）は対照として残す。
    /// </summary>
    internal static UnitDef WardHealer(TraitId mend) => new()
    {
        Id = "cw_healer", Name = "癒し手", MaxHp = EnemyCatalog.Warden.MaxHp, Attack = EnemyCatalog.Warden.Attack, Speed = EnemyCatalog.Warden.Speed,
        Pattern = EnemyCatalog.Warden.Pattern, Advances = EnemyCatalog.Warden.Advances, Actions = EnemyCatalog.Warden.Actions,
        Traits = new[] { mend }, PlusText = "ターン頭に味方全員を回復する（検証波・重装兵の体）",
    };
    static readonly UnitDef WardHealer30 = WardHealer(TraitId.CheckMend30), WardHealer50 = WardHealer(TraitId.CheckMend50);

    /// <summary>第四波の <see cref="EnemyWave"/> 写し（Phase 0-4）。席 0〜4 に元の第四波と同じ駒。<paramref name="back3"/> を差し替え、<paramref name="deep"/> があれば ○後2 に置く。</summary>
    internal static EnemyWave Wave4Copy(UnitDef? back3, UnitDef? deep = null)
    {
        var f = EnemyCatalog.Stages[3].Enemy;
        var seats = new List<(int, UnitDef)>();
        for (int s = 0; s < 5; s++)
        {
            UnitDef? d = s == 4 ? back3 : f[s];
            if (d is not null) seats.Add((s, d));
        }
        if (deep is not null) seats.Add((8, deep));   // ○後2
        return EnemyWave.Of(seats.ToArray());
    }

    internal sealed record CWave(string Name, string What, bool IsBoss, int Mend, Func<EnemyWave> Make, EnemyScaleRule? Scale = null);
    /// <summary>第260期の版（棄却・対照として残す）。`run260` が回す。</summary>
    internal static readonly CWave[] CheckWaves260 =
    {
        new("T-30後", "第四波の後3 の従軍司祭 → 癒し手（全員 +30）", false, PartyMendTrait.Low, () => Wave4Copy(Healer30)),
        new("T-50後", "第四波の後3 の従軍司祭 → 癒し手（全員 +50）", false, PartyMendTrait.High, () => Wave4Copy(Healer50)),
        new("T-50奥", "第四波の後3 を空け、癒し手（全員 +50）を ○後2 に", false, PartyMendTrait.High, () => Wave4Copy(null, Healer50)),
        new("B-全4", "ボス（毎ターン全快・攻撃力 +4/ターン）", true, 0, () => EnemyWave.Of((2, Boss(TraitId.BossMendFull, TraitId.BossRise4)))),
        new("B-全8", "ボス（毎ターン全快・攻撃力 +8/ターン）", true, 0, () => EnemyWave.Of((2, Boss(TraitId.BossMendFull, TraitId.BossRise8)))),
        new("B-半4", "ボス（毎ターン最大HPの50%・攻撃力 +4/ターン）", true, 0, () => EnemyWave.Of((2, Boss(TraitId.BossMendHalf, TraitId.BossRise4)))),
    };
    /// <summary>対照: 第四波の写し（従軍司祭のまま）。元の第四波と台本が一致する（自己検査 (a)）。</summary>
    internal static readonly CWave Copy4 = new("第四波写し", "第四波を EnemyWave に写しただけ（対照）", false, 0, () => Wave4Copy(EnemyCatalog.Priest));
    /// <summary>第261期の版（指示書 §3）。手数チェックは重装兵の体の癒し手、ボスは動じない・HP 3,000（参考は 500）。</summary>
    internal static readonly CWave[] CheckWaves =
    {
        new("W-30後", "第四波の後3 の従軍司祭 → 重装兵の体の癒し手（全員 +30）", false, PartyMendTrait.Low, () => Wave4Copy(WardHealer30)),
        new("W-50後", "第四波の後3 の従軍司祭 → 重装兵の体の癒し手（全員 +50）", false, PartyMendTrait.High, () => Wave4Copy(WardHealer50)),
        new("W-50奥", "第四波の後3 を空け、重装兵の体の癒し手（全員 +50）を ○後2 に", false, PartyMendTrait.High, () => Wave4Copy(null, WardHealer50)),
        new("B-全4", "動じないボス（HP 3,000・毎ターン全快・攻撃力 +4/ターン）", true, 0, () => EnemyWave.Of((2, Boss2(TraitId.BossMendFull, TraitId.BossRise4, ChosenBossHp)))),
        new("B-半4", "動じないボス（HP 3,000・毎ターン最大HPの50%・攻撃力 +4/ターン）", true, 0, () => EnemyWave.Of((2, Boss2(TraitId.BossMendHalf, TraitId.BossRise4, ChosenBossHp)))),
        new("B-参500", "動じないボス（HP 500・毎ターン全快・攻撃力 +4/ターン・対照）", true, 0, () => EnemyWave.Of((2, Boss2(TraitId.BossMendFull, TraitId.BossRise4, DraftBossHp)))),
    };
    /// <summary>
    /// 第262期の版（指示書 §3）。手数チェックは第四波を <b>400/300</b> に（癒し手も倍率に乗る・回復 50 は札の定数で乗らない）、
    /// ボスは第261期の動じないボスを<b>全体攻撃</b>にしただけ（HP 3,000・天井 +4・回復は同じ・倍率なし）。
    /// </summary>
    internal static readonly EnemyScaleRule Scale400 = new(400, 300);
    internal static readonly CWave[] CheckWaves262 =
    {
        new("W2-対照", "第四波（400/300・癒し手なし＝従軍司祭のまま）", false, 0, () => Wave4Copy(EnemyCatalog.Priest), Scale400),
        new("W2-50後", "第四波（400/300）の後3 の従軍司祭 → 重装兵の体の癒し手（全員 +50）", false, PartyMendTrait.High, () => Wave4Copy(WardHealer50), Scale400),
        new("W2-50奥", "第四波（400/300）の後3 を空け、重装兵の体の癒し手（全員 +50）を ○後2 に", false, PartyMendTrait.High, () => Wave4Copy(null, WardHealer50), Scale400),
        new("B2-全4", "全体攻撃の動じないボス（HP 3,000・毎ターン全快・攻撃力 +4/ターン）", true, 0, () => EnemyWave.Of((2, Boss3(TraitId.BossMendFull, TraitId.BossRise4, ChosenBossHp)))),
        new("B2-半4", "全体攻撃の動じないボス（HP 3,000・毎ターン最大HPの50%・攻撃力 +4/ターン）", true, 0, () => EnemyWave.Of((2, Boss3(TraitId.BossMendHalf, TraitId.BossRise4, ChosenBossHp)))),
    };

    /// <summary>第262期の版 → 第261期の版 → 対照の第四波写し → 第260期の版（名前が重なる B-全4 ／ B-半4 は第261期が先）。`261:` ／ `260:` を前に付けても引ける。</summary>
    internal static CWave CWaveOf(string n) => n == Copy4.Name ? Copy4
        : n.StartsWith("260:") ? CWave260(n[4..])
        : n.StartsWith("261:") ? CheckWaves.First(w => w.Name == n[4..])
        : CheckWaves262.FirstOrDefault(w => w.Name == n) ?? CheckWaves.FirstOrDefault(w => w.Name == n) ?? CWave260(n);
    internal static CWave CWave260(string n) => n == Copy4.Name ? Copy4 : CheckWaves260.First(w => w.Name == n);

    internal static (BattleResult R, List<UnitState> P, List<UnitState> E) Fight(Formation f, Func<List<UnitState>> wave, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = wave();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        return (r, p, e);
    }
    internal static Func<List<UnitState>> InvWave(int w) => FC.WaveOf(w, EnemyScaleRule.None);
    internal static Func<List<UnitState>> CheckWave(CWave c) => () => BattleEngine.MaterializeEnemy(c.Make(), c.Scale ?? EnemyScaleRule.None);

    static void LogOne(string board, string wave, int seed)
    {
        var c = CWaveOf(wave);
        var (r, p, e) = Fight(BoardOf(board), CheckWave(c), seed);
        var a = new Agg(); a.Take(r, p, e, c.IsBoss ? Kind.Boss : Kind.Normal, c.Mend);
        Console.WriteLine($"# {board}（{BA.SeatsNamed(BoardOf(board))}）× {c.Name}（{c.What}）× seed {seed} → {(a.Wins > 0 ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    // ---------------------------------------------------------------------------------
    // 集計（1戦 → 窓）。盤面は1ビットも動かさない（台本を読むだけ）。
    //
    // **窓** ＝ ターン t の手番（そのターンの最初の StatSnapshot の後）＋ ターン t+1 の頭の刻み（TurnStart から最初の StatSnapshot まで）。
    // ターン頭の回復（`OnTurnStart`）は刻みの後に入るので、回復される前に削れる量はちょうどこの窓になる（Phase 0-1）。
    // 与ダメは敵陣の駒が実際に減らした HP（Damage）。命中は肩代わりの中継段を除いた Damage の件数。
    // ---------------------------------------------------------------------------------
    internal enum Kind { Normal, Target, Boss }
    internal static readonly int[] Thresholds = { 200, 300, 400, 500, 600, 800, 1000, 1500 };

    internal sealed class Agg
    {
        public long N, Wins, Surv, Turns;
        public long MaxWinSum, MaxWinMax, MaxHitSum, MaxHitMax, WinHits, WinCnt, WinDmg;
        public readonly long[] OverTh = new long[Thresholds.Length];   // その戦の窓の最大が閾値以上だった戦
        public readonly long[] TurnDmg = new long[BossTurns + 1], TurnCnt = new long[BossTurns + 1];   // ターンごとの窓の与ダメ（和 ／ 窓のあった戦）
        public readonly long[] FirstOver = new long[Thresholds.Length], FirstOverN = new long[Thresholds.Length];   // 窓が閾値に初めて届いたターン（和 ／ 届いた戦）
        // 手数チェック
        public long HealTurns, OverTurns, HealerDead, HealerDeadT, HealGiven;
        // ボス
        public long Kill, KillT, FirstDeath, FirstDeathT, Wiped;
        public long BossAlive, BossActs;   // ボスが生きていたターン頭の数 ／ ボスの攻撃（Attack）の数
        public long BossNominal, BossDealt;   // 第261期: ボスが振った手番の名目（**10 倍で持つ**・そのターンの StatSnapshot × 命中の重み〈単体 1 ／ 全体 1 ＋ 0.6 × (生きている味方 − 1)〉）／ ボスが味方に実際に入れた HP
        public long MineHealed, MineStartHp;   // 第262期: 味方が受けた回復（Heal・HP が実際に増えた分）／ 味方の開戦時の HP の和
        public long HealerWinMax, HealerTaken;   // 第261期: 癒し手が1窓で受けた量の最大（戦ごとの和）／ 受けた量の和
        public readonly Dictionary<string, long> HealerKiller = new();   // 癒し手に最後の一撃を入れた駒（名前・刻みは「刻み」）

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; Surv += o.Surv; Turns += o.Turns;
            MaxWinSum += o.MaxWinSum; MaxWinMax = Math.Max(MaxWinMax, o.MaxWinMax); MaxHitSum += o.MaxHitSum; MaxHitMax = Math.Max(MaxHitMax, o.MaxHitMax);
            WinHits += o.WinHits; WinCnt += o.WinCnt; WinDmg += o.WinDmg;
            for (int i = 0; i < OverTh.Length; i++) { OverTh[i] += o.OverTh[i]; FirstOver[i] += o.FirstOver[i]; FirstOverN[i] += o.FirstOverN[i]; }
            for (int t = 0; t <= BossTurns; t++) { TurnDmg[t] += o.TurnDmg[t]; TurnCnt[t] += o.TurnCnt[t]; }
            HealTurns += o.HealTurns; OverTurns += o.OverTurns; HealerDead += o.HealerDead; HealerDeadT += o.HealerDeadT; HealGiven += o.HealGiven;
            Kill += o.Kill; KillT += o.KillT; FirstDeath += o.FirstDeath; FirstDeathT += o.FirstDeathT; Wiped += o.Wiped;
            BossAlive += o.BossAlive; BossActs += o.BossActs; BossNominal += o.BossNominal; BossDealt += o.BossDealt; MineHealed += o.MineHealed; MineStartHp += o.MineStartHp; HealerWinMax += o.HealerWinMax; HealerTaken += o.HealerTaken;
            foreach (var (k, v) in o.HealerKiller) HealerKiller[k] = HealerKiller.GetValueOrDefault(k) + v;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Kind kind, int mend)
        {
            N++;
            var foes = e.Select(u => u.InstanceId).ToHashSet();
            foreach (var x in r.Events)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team is int tm && tm != BattleContext.PlayerTeam) foes.Add(sid);
            var mine = p.Select(u => u.InstanceId).ToHashSet();
            int? healer = e.FirstOrDefault(u => u.Def.Id == "cw_healer")?.InstanceId;
            int? boss = e.FirstOrDefault(u => u.Def.Id == "cw_boss")?.InstanceId;
            int cut = kind == Kind.Normal ? int.MaxValue : BossTurns;

            var dmg = new Dictionary<int, long>(); var hits = new Dictionary<int, long>();
            var aliveFoes = new HashSet<int>(foes.Where(id => e.Any(u => u.InstanceId == id)));
            var livingAtHead = new Dictionary<int, int>(); var healerAtHead = new Dictionary<int, bool>();
            int turn = 0; bool snap = false; int? killT = null, firstDeathT = null, healerDeadT = null;
            var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Name);
            string? lastHitOnHealer = null;
            int bossAtk = 0; var healerWin = new Dictionary<int, long>();
            var aliveMine = new HashSet<int>(mine); MineStartHp += p.Sum(u => u.MaxHp);
            foreach (var x in r.Events)
            {
                switch (x.Kind)
                {
                    case BattleEventKind.TurnStart: turn = x.Turn; snap = false; break;
                    case BattleEventKind.StatSnapshot when !snap:
                        snap = true;
                        livingAtHead[turn] = aliveFoes.Count;
                        healerAtHead[turn] = healer is int h0 && aliveFoes.Contains(h0);
                        if (boss is int b0 && aliveFoes.Contains(b0) && turn <= cut) BossAlive++;
                        break;
                    case BattleEventKind.StatSnapshot when boss is int bs && x.TargetId == bs: bossAtk = x.Amount; break;
                    case BattleEventKind.Attack when boss is int b1 && x.ActorId == b1 && x.Turn <= cut:
                        BossActs++;
                        BossNominal += x.Pattern == AttackPattern.All ? (long)bossAtk * (10 + 6 * Math.Max(0, aliveMine.Count - 1)) : bossAtk * 10L;
                        break;
                    case BattleEventKind.Heal when x.TargetId is int hm && mine.Contains(hm) && x.Turn <= cut: MineHealed += x.Amount; break;
                    case BattleEventKind.Revive when x.TargetId is int rv && mine.Contains(rv): aliveMine.Add(rv); break;
                    case BattleEventKind.Damage when boss is int b2 && x.ActorId == b2 && x.TargetId is int tm2 && mine.Contains(tm2) && x.Turn <= cut: BossDealt += x.Amount; break;
                    case BattleEventKind.Damage when x.TargetId is int t && foes.Contains(t) && x.Amount > 0:
                    {
                        if (t == healer) lastHitOnHealer = x.ActorId is int ai && names.TryGetValue(ai, out var nm) ? nm : "刻み・出どころなし";
                        if (t == healer) { int hw = snap ? x.Turn : x.Turn - 1; healerWin[hw] = healerWin.GetValueOrDefault(hw) + x.Amount; HealerTaken += x.Amount; }
                        int w = snap ? x.Turn : x.Turn - 1;
                        if (w >= 1 && w <= cut)
                        {
                            dmg[w] = dmg.GetValueOrDefault(w) + x.Amount;
                            if (!x.Relayed) hits[w] = hits.GetValueOrDefault(w) + 1;
                        }
                        break;
                    }
                    case BattleEventKind.Heal when healer is int hh && x.ActorId == hh: HealGiven += x.Amount; break;
                    case BattleEventKind.Death when x.TargetId is int d:
                        if (foes.Contains(d)) aliveFoes.Remove(d);
                        if (d == boss) killT ??= x.Turn;
                        if (d == healer) healerDeadT ??= x.Turn;
                        if (mine.Contains(d)) { firstDeathT ??= x.Turn; aliveMine.Remove(d); }
                        break;
                }
            }

            long mx = dmg.Count == 0 ? 0 : dmg.Values.Max(), mh = hits.Count == 0 ? 0 : hits.Values.Max();
            MaxWinSum += mx; MaxWinMax = Math.Max(MaxWinMax, mx); MaxHitSum += mh; MaxHitMax = Math.Max(MaxHitMax, mh);
            foreach (var (w, d) in dmg) { WinCnt++; WinDmg += d; WinHits += hits.GetValueOrDefault(w); }
            for (int i = 0; i < Thresholds.Length; i++) if (mx >= Thresholds[i]) OverTh[i]++;
            foreach (var (w, d) in dmg) if (w <= BossTurns) { TurnDmg[w] += d; TurnCnt[w]++; }
            for (int i = 0; i < Thresholds.Length; i++)
            {
                int first = dmg.Where(kv => kv.Value >= Thresholds[i]).Select(kv => kv.Key).DefaultIfEmpty(0).Min();
                if (first > 0) { FirstOver[i] += first; FirstOverN[i]++; }
            }

            if (firstDeathT is int fd) { FirstDeath++; FirstDeathT += fd; }
            switch (kind)
            {
                case Kind.Normal:
                    Turns += r.Turns;
                    if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) Surv++; }
                    break;
                case Kind.Boss:
                    bool won = r.PlayerWon && killT is int kt && kt <= BossTurns;
                    if (won) { Wins++; Kill++; KillT += killT!.Value; Turns += killT!.Value; if (r.PlayerStarterFallen.Count == 0) Surv++; }
                    else Turns += Math.Min(r.Turns, BossTurns);
                    if (!r.PlayerWon && r.Turns <= BossTurns) Wiped++;
                    break;
            }
            if (healer is not null)
            {
                HealerWinMax += healerWin.Count == 0 ? 0 : healerWin.Values.Max();
                if (healerDeadT is int hd) { HealerDead++; HealerDeadT += hd; string k = lastHitOnHealer ?? "—"; HealerKiller[k] = HealerKiller.GetValueOrDefault(k) + 1; }
                // 窓 t の与ダメが、それを押し返すターン t+1 の頭の名目回復量（癒し手が生きているときの 量 × 敵の生存数）を上回ったか。
                foreach (var (w, d) in dmg)
                    if (healerAtHead.GetValueOrDefault(w + 1) && livingAtHead.TryGetValue(w + 1, out int live))
                    {
                        HealTurns++;
                        if (d > (long)mend * live) OverTurns++;
                    }
            }
        }

        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public double SurvPct => N == 0 ? double.NaN : 100.0 * Surv / N;
    }

    internal static Agg Measure(Formation f, Func<List<UnitState>> wave, Kind kind, int mend, int seed0 = 0, int seeds = Seeds)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var (r, p, e) = Fight(f, wave, seed0 + i);
            var a = new Agg(); a.Take(r, p, e, kind, mend); parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    internal static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    internal static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    internal static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
}
