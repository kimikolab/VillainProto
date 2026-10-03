using System.Reflection;
using BattleCore;
using static Common;

// checkwave check —— 自己検査（指示書 §7）。
static partial class CheckWaveDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
        ?? throw new InvalidOperationException("BattleContext.Add が見つからない");

    static partial void CheckImpl()
    {
        int pass = 0, fail = 0;
        void Ok(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            Console.WriteLine($"- [{(ok ? "○" : "×")}] {name}{(detail.Length > 0 ? $"（{detail}）" : "")}");
        }
        Console.WriteLine("# checkwave check —— 第260期 自己検査");
        Console.WriteLine();

        // (a) 第四波の EnemyWave 写しは元の第四波と台本が一致する（Phase 0-4）
        var (same, lines) = CopyDigest(6);
        Ok("(a) 第四波の EnemyWave 写し ＝ 元の第四波（6台 × seed 0..5 のログ＋出来事）", same, $"{lines:N0} 行");

        // (b) 癒し手は従軍司祭と数値・型・行動・踏み込みが同一で、差分は札1枚だけ
        var pr = EnemyCatalog.Priest;
        foreach (var h in new[] { Healer(TraitId.CheckMend30), Healer(TraitId.CheckMend50) })
        {
            bool eq = h.MaxHp == pr.MaxHp && h.Attack == pr.Attack && h.Speed == pr.Speed && h.Pattern == pr.Pattern && h.Advances == pr.Advances
                      && ReferenceEquals(h.Actions, pr.Actions) && pr.Traits.Count == 0 && h.Traits.Count == 1;
            Ok($"(b) 癒し手（{h.Traits[0]}）＝ 従軍司祭 {pr.MaxHp}/{pr.Attack}/{pr.Speed}・{pr.Pattern}・踏み込み {pr.Advances} ＋ 札1枚", eq);
        }

        // (c) 新しい札の保持者はロスター・本編の敵に 0 枚。ローカルの敵の Id を読む規則は BattleCore に無い
        TraitId[] mine = { TraitId.CheckMend30, TraitId.CheckMend50, TraitId.BossMendFull, TraitId.BossMendHalf, TraitId.BossRise4, TraitId.BossRise8 };
        var enemyDefs = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def))
            .Concat(EnemyCatalog.TestStages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)))
            .Concat(EnemyCatalog.Vanguards.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)))
            .Concat(EnemyCatalog.Pattern3Copies.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)));
        int holders = UnitCatalog.Everyone.Concat(enemyDefs).Count(u => u.Traits.Any(mine.Contains));
        Ok("(c-1) 新しい札 6 枚の保持者がロスター（`Everyone`）と本編・検証・先遣・パターン3 の敵に 0 枚", holders == 0, $"{holders} 枚");
        int idReads = Directory.GetFiles("BattleCore", "*.cs").Sum(f => File.ReadAllText(f).Split('\n').Count(l => l.Contains("cw_healer") || l.Contains("cw_boss")));
        Ok("(c-2) BattleCore の中に `cw_healer` ／ `cw_boss` を読む行が 0", idReads == 0, $"{idReads} 行");

        // (d) 回復は HP だけを戻し、状態（毒・燃焼・火勢・破片）を消さない（盤面を直に組む）
        {
            var ctx = new BattleContext(0, false);
            var p = BattleEngine.Materialize(BoardOf("毒"), BattleContext.PlayerTeam);
            var e = BattleEngine.MaterializeEnemy(EnemyWave.Of((2, Boss(TraitId.BossMendFull, TraitId.BossRise4))), EnemyScaleRule.None);
            foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
            var boss = e[0];
            boss.SetCounter(StatusKeys.Poison, 7); boss.SetCounter(StatusKeys.Burn, 3); boss.SetCounter(FireLevelRule.LvKey, 3); boss.SetCounter(StatusKeys.Armor, 5);
            boss.Hp = 100;
            TraitCatalog.Resolve(new[] { TraitId.BossMendFull })[0].OnTurnStart(ctx, boss);
            bool full = boss.Hp == boss.MaxHp;
            bool kept = boss.RawCounter(StatusKeys.Poison) == 7 && boss.RawCounter(StatusKeys.Burn) == 3 && boss.RawCounter(FireLevelRule.LvKey) == 3 && boss.RawCounter(StatusKeys.Armor) == 5;
            Ok("(d-1) 全快: HP 100 → 最大、毒 7・燃焼 3・火勢 3・破片 5 はそのまま", full && kept, $"HP {boss.Hp}/{boss.MaxHp}");
            boss.Hp = 100;
            TraitCatalog.Resolve(new[] { TraitId.BossMendHalf })[0].OnTurnStart(ctx, boss);
            Ok("(d-2) 半ば: HP 100 → 100 + 最大HP × 50%", boss.Hp == 100 + boss.MaxHp / 2, $"HP {boss.Hp}");
        }

        // (e) 癒し手の回復は刻みの後（その手番の最初の StatSnapshot より前に出ない）・刻みで削られた後に入るターンがある
        var o = OrderCensus(CWaveOf("T-50後"), 20);
        Ok("(e-1) 癒し手の回復が刻みより前に出たことが 0 件", o.HealBeforeSnap == 0 && o.Heals > 0, $"{o.Heals} 件中 {o.HealBeforeSnap}");
        Ok("(e-2) 刻みで削られた後に回復が入ったターンがある", o.TickThenHeal > 0, $"{o.TickThenHeal} ターン");

        // (f) 回復は軛に切られない
        var y = YokeHealCensus(20);
        Ok("(f) 軛が生きている間に 25 を超えた回復がある（最大 50）", y.Over25 > 0 && y.Max == PartyMendTrait.High, $"{y.Over25} 件・最大 {y.Max}");

        // (g) ボスの攻撃力 ＝ 12 + X × (t − 1)（StatSnapshot の写しで）
        foreach (var c in new[] { CWaveOf("B-全4"), CWaveOf("B-全8") })
        {
            var (ok, n, _) = RiseCensus(c, 10);
            Ok($"(g) {c.Name} のボスの攻撃力の写しが全件 12 + X × (t − 1)", ok == n && n > 0, $"{ok}/{n}");
        }

        // (h) 1件の回復が量を超えない・1ターンの回復の件数が敵の生存数を超えない
        {
            long over = 0, tooMany = 0;
            foreach (string b in Boards)
                for (int sd = 0; sd < 10; sd++)
                {
                    var (r, _, e) = Fight(BoardOf(b), CheckWave(CWaveOf("T-30後")), sd);
                    int healer = e.First(u => u.Def.Id == "cw_healer").InstanceId;
                    foreach (var g in r.Events.Where(x => x.Kind == BattleEventKind.Heal && x.ActorId == healer).GroupBy(x => x.Turn))
                    {
                        over += g.Count(x => x.Amount > PartyMendTrait.Low);
                        if (g.Count() > e.Count) tooMany++;
                    }
                }
            Ok("(h) T-30後: 1件の回復 ≤ 30・1ターンの回復の件数 ≤ 敵の数", over == 0 && tooMany == 0, $"超え {over}・件数超え {tooMany}");
        }

        // (i) verbose の有無で結果が変わらない（チェック波 6 版 × 6台 × seed 0..4）
        {
            int diff = 0, n = 0;
            foreach (var c in CheckWaves)
                foreach (string b in Boards)
                    for (int sd = 0; sd < 5; sd++)
                    {
                        var v = Fight(BoardOf(b), CheckWave(c), sd, true).R; var q = Fight(BoardOf(b), CheckWave(c), sd, false).R;
                        n++;
                        if (v.PlayerWon != q.PlayerWon || v.Turns != q.Turns || !v.PlayerStarterFallen.SequenceEqual(q.PlayerStarterFallen)) diff++;
                    }
            Ok("(i) verbose の有無で勝敗・決着T・倒れた駒が一致", diff == 0, $"{n - diff}/{n}");
        }

        // (j) 新しいコードが乱数を引かない（`PickOne` ／ `Roll(` ／ `Shuffle(` を使っていない）
        {
            string traits = File.ReadAllText("BattleCore/Traits.cs");
            int a = traits.IndexOf("public sealed class PartyMendTrait", StringComparison.Ordinal), z = traits.IndexOf("public sealed class BossRiseTrait", StringComparison.Ordinal);
            int z2 = traits.IndexOf("\n}", z, StringComparison.Ordinal);
            string body = a >= 0 && z2 > a ? traits[a..z2] : "";
            // 検査の本体（このファイル）は探す語そのものを持つので外す。
            string tool = string.Concat(Directory.GetFiles("BattleSim/Modes", "CheckWave*.cs").Where(f => !f.EndsWith("CheckWave.Check.cs")).Select(File.ReadAllText));
            bool clean = body.Length > 0 && !new[] { "PickOne", "Roll(", "Shuffle(" }.Any(k => body.Contains(k) || tool.Contains(k));
            Ok("(j) 新しい札 3 クラスと `checkwave`（検査の本体を除く）が `PickOne` ／ `Roll(` ／ `Shuffle(` を使っていない", clean);
        }

        Console.WriteLine();
        Console.WriteLine($"合計: ○ {pass} ／ × {fail}");
        if (fail > 0) Environment.ExitCode = 1;
    }
}
