using Godot;
using System;
using System.Collections.Generic;

public static class CampaignSession
{
    public const string CampaignScene = "res://CampaignMain.tscn";
    public const string BattleScene = "res://Main.tscn";

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
