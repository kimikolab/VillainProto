using BattleCore;
using static Common;

// =====================================================================================
// replay モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "replay")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 replay
// =====================================================================================

static class ReplayDiag
{
// replay モード: 1戦ぶんの台本を JSON で吐く。戦闘画面（ビューア）が読む。
//
// BattleEngine.Run は seed 決定的な純関数で戦闘を丸ごと計算し切るので、
// ビューアはシミュレーションを持たず、この列を再生するだけでよい。
// ここが JSON を吐く唯一の場所。docs/ と違って生成物を repo に置かない
// （盤面が変わるたび腐るし、diff が読めない）。
//
//     dotnet run --project BattleSim -c Release <stage> replay [編成の部分一致] [seed]
public static void Run(string[] args, int stageIndex)
{
    string want = args.Length > 2 ? args[2] : "";
    int replaySeed = args.Length > 3 && int.TryParse(args[3], out int rs) ? rs : 0;

    var (buildName, playerF) = CompareBuilds()
        .FirstOrDefault(b => want.Length == 0 || b.Name.Contains(want));
    if (playerF is null)
    {
        Console.Error.WriteLine($"編成が見つからない: {want}");
        return;
    }

    EnemyCatalog.Stage st = EnemyCatalog.Stages[stageIndex];
    BattleResult res = BattleEngine.Run(playerF, st.Enemy, replaySeed, verbose: true);

    // 初期盤面は Run の前の状態が要るが、Run は編成を書き換えないので
    // ここで Formation から組み直せる。InstanceId は Deploy の順（味方→敵、スロット昇順）で
    // 振られるので、同じ順で数えれば一致する。
    var roster = new List<object>();
    int id = 0;
    foreach (var (team, f) in new[] { (0, playerF), (1, st.Enemy) })
        foreach (var (slot, def) in f.Occupied())
            roster.Add(new
            {
                id = id++,
                team,
                slot,
                name = def.Name,
                maxHp = def.MaxHp,
                attack = def.Attack,
                speed = def.Speed,
                pattern = def.Pattern.ToString(),
                plus = def.PlusText,
                minus = def.MinusText
            });

    // 増援・蘇生で後から出る駒は roster に無いので、ビューアは Summon イベントで足す。
    // その駒の見た目に要る情報をイベント側からは引けないため、カタログ全体も併せて渡す。
    var catalog = UnitCatalog.All.ToDictionary(
        u => u.Name,
        u => (object)new { maxHp = u.MaxHp, attack = u.Attack, pattern = u.Pattern.ToString() });

    var payload = new
    {
        build = buildName,
        stage = st.Name,
        stageIndex,
        seed = replaySeed,
        playerWon = res.PlayerWon,
        turns = res.Turns,
        maxChain = res.MaxEnemyKillsInOneTurn,
        roster,
        catalog,
        events = res.Events.Select(e => new
        {
            kind = e.Kind.ToString(),
            turn = e.Turn,
            actor = e.ActorId,
            target = e.TargetId,
            amount = e.Amount,
            hpAfter = e.HpAfter,
            friendly = e.FriendlyFire,
            slot = e.Slot,
            team = e.Team,
            pattern = e.Pattern?.ToString(),
            shareFrom = e.ShareFromId,   // 第182期・呪いの受け渡しの出どころ（それ以外は null）
            deflectFrom = e.DeflectFromId,             // 第186期・逸らした駒（ソラ）。actor は元の攻撃者
            thrustCharge = e.ThrustCharge,             // 第186期 追補・逸らし: 積んだ後の段 ／ 突きの Attack: 乗った回数
            poisonRoute = e.PoisonRoute?.ToString(),   // 第183期 追補2・毒の付与経路（毒の StatusGain 以外は null）
            spreadFrom = e.SpreadFromId,               // 第183期 追補2・伝染の元の敵（Touch 以外は null）
            remaining = e.StatusRemaining,             // 第183期 追補3・StatusDrain: 吸われた駒に残った量
            drainSeq = e.DrainSeq,                     // 第183期 追補3・StatusDrain: 同じ吸い上げの一連
            drainLast = e.DrainLast,                   // 第183期 追補3・StatusDrain: 一連の最後の1件
            attackAfter = e.AttackAfter,               // 第183期 追補3・StatusDrain: 最後の1件にだけ吸った側の攻撃力
            intended = e.IntendedId,                   // Whet: 本来の対象（支援拒否・横流しで受け手と違う）
            whetRoute = e.WhetRoute?.ToString(),       // Whet: 強化の経路
            sourceTrait = e.SourceTrait?.ToString(),   // Whet / HealBlocked: 書き手（回復の出どころ）の札
            supportSeq = e.SupportSeq,                 // Whet: 同じ1回の配りの一連
            supportLast = e.SupportLast,               // Whet: 一連の最後の1件
            inverter = e.InverterId,                   // 第194期・反転した刻みの Status: 反転させたベニ（sourceTrait = Inverse）
            tickIndex = e.TickIndex,                   // 第194期・濃縮の印で広がった刻みの Status: 何回目か（1 始まり）
            tickCount = e.TickCount,                   // 第194期・同: 全部で何回の予定か（1+n）
            plankBase = e.PlankBase,                   // 第210期・ツギの板（手番・応急処置）: 量のうち基本の分（在庫の分は slot）
            plankSkill = e.PlankSkill,                 // 第210期・同: 腕の分
            aidOrdinal = e.AidOrdinal,                 // 第212期・応急処置: そのターンの何回目か（1 始まり）
            text = e.Text
        }).ToList()
    };

    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload,
        new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    return;
}
}
