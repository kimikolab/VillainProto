using BattleCore;
using Godot;
using System;
using System.Collections.Generic;

public static class CampaignSession
{
    public const string CampaignScene = "res://CampaignMain.tscn";
    public const string BattleScene = "res://Main.tscn";

    // -----------------------------------------------------------------------------
    // 第169期 —— **持ち越した駒をそのまま戦闘シーンへ渡す窓口**（検証用マップ 1-1）。
    //
    // 既存の `BeginEncounter`（ステージ番号だけを渡し、戦闘シーンが `Materialize` する）
    // とは別の経路にしてある。あちらは**編成画面を経由する**ので、傷も死者も持ち込めない。
    //
    // **リストは写さない。** `BattleEngine.Run` が書き換えた同じ参照を
    // `Map11State.Resolve` が読むので、間に写しを1つでも挟むと持ち越しが切れる。
    // -----------------------------------------------------------------------------

    public static List<UnitState>? CarriedPlayers { get; private set; }
    public static List<UnitState>? CarriedEnemies { get; private set; }
    public static int CarriedSeed { get; private set; }
    public static string? CarriedEnemyName { get; private set; }
    /// <summary>背景の選択にしか使わない（判定には1ビットも効かない）。</summary>
    public static int CarriedStageIndex { get; private set; }
    public static bool HasCarriedBattle => CarriedPlayers is not null && CarriedEnemies is not null;

    public static void BeginCarriedBattle(List<UnitState> players, List<UnitState> enemies,
                                          int seed, string enemyName, int stageIndex)
    {
        CarriedPlayers = players;
        CarriedEnemies = enemies;
        CarriedSeed = seed;
        CarriedEnemyName = enemyName;
        CarriedStageIndex = Math.Clamp(stageIndex, 0, 4);
    }

    public static void ClearCarriedBattle()
    {
        CarriedPlayers = null;
        CarriedEnemies = null;
        CarriedEnemyName = null;
    }

    private static readonly Dictionary<string, Vector3> DefaultPositions = new(StringComparer.Ordinal)
    {
        ["grey-wolves"] = new Vector3(-8.5f, 0.0f, 5.5f),
        ["last-embers"] = new Vector3(-11.5f, 0.0f, 0.5f),
        ["iron-hunt"] = new Vector3(9.5f, 0.0f, -5.0f),
        ["red-tusk"] = new Vector3(8.0f, 0.0f, 2.0f),
    };

    public static Dictionary<string, Vector3> SquadPositions { get; } = new(StringComparer.Ordinal);
    public static Dictionary<string, int> BaseOwners { get; } = new(StringComparer.Ordinal);
    public static HashSet<string> DefeatedEnemies { get; } = new(StringComparer.Ordinal);

    public static string? PendingPlayerSquadId { get; private set; }
    public static string? PendingEnemySquadId { get; private set; }
    public static string? PendingEnemyName { get; private set; }
    public static int PendingStageIndex { get; private set; }
    public static bool? LastPlayerWon { get; private set; }
    public static string? LastOutcomeText { get; private set; }
    public static bool Initialized { get; private set; }
    public static bool FlowSmokeReturning { get; set; }
    public static bool HasPendingEncounter => PendingEnemySquadId is not null;

    public static void EnsureInitialized()
    {
        if (Initialized) return;
        ResetCampaign();
    }

    public static void ResetCampaign()
    {
        SquadPositions.Clear();
        foreach ((string id, Vector3 position) in DefaultPositions)
            SquadPositions[id] = position;

        BaseOwners.Clear();
        BaseOwners["west-camp"] = 0;
        BaseOwners["old-watch"] = -1;
        BaseOwners["east-fort"] = 1;
        DefeatedEnemies.Clear();
        PendingPlayerSquadId = null;
        PendingEnemySquadId = null;
        PendingEnemyName = null;
        PendingStageIndex = 0;
        LastPlayerWon = null;
        LastOutcomeText = null;
        FlowSmokeReturning = false;
        Initialized = true;
    }

    public static Vector3 PositionOf(string squadId)
    {
        EnsureInitialized();
        return SquadPositions.GetValueOrDefault(squadId, Vector3.Zero);
    }

    public static Vector3 HomeOf(string squadId)
        => DefaultPositions.GetValueOrDefault(squadId, Vector3.Zero);

    public static void SavePosition(string squadId, Vector3 position)
    {
        EnsureInitialized();
        SquadPositions[squadId] = new Vector3(position.X, 0.0f, position.Z);
    }

    public static void SaveBaseOwner(string baseId, int owner)
    {
        EnsureInitialized();
        BaseOwners[baseId] = owner;
    }

    public static void BeginEncounter(string playerSquadId, string enemySquadId, string enemyName, int stageIndex)
    {
        EnsureInitialized();
        PendingPlayerSquadId = playerSquadId;
        PendingEnemySquadId = enemySquadId;
        PendingEnemyName = enemyName;
        PendingStageIndex = Math.Clamp(stageIndex, 0, 4);
        LastPlayerWon = null;
        LastOutcomeText = null;
    }

    public static void CompleteBattle(bool playerWon)
    {
        if (!HasPendingEncounter) return;

        string playerId = PendingPlayerSquadId!;
        string enemyId = PendingEnemySquadId!;
        string enemyName = PendingEnemyName ?? "敵部隊";
        LastPlayerWon = playerWon;
        if (playerWon)
        {
            DefeatedEnemies.Add(enemyId);
            LastOutcomeText = $"迎撃成功 — {enemyName} を退けました";
        }
        else
        {
            SquadPositions[playerId] = HomeOf(playerId);
            SquadPositions[enemyId] = HomeOf(enemyId);
            LastOutcomeText = $"撤退 — {enemyName} は東方へ引きました";
        }

        PendingPlayerSquadId = null;
        PendingEnemySquadId = null;
        PendingEnemyName = null;
    }

    public static string? ConsumeOutcome()
    {
        string? value = LastOutcomeText;
        LastOutcomeText = null;
        return value;
    }
}
