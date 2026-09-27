using System.Reflection;
using BattleCore;
using static Common;

// nine check —— 自己検査（第221期・指示書 §6 の 2〜4）
static partial class NineDiag
{
    static partial void CheckImpl()
    {
        Console.WriteLine("# 第221期 自己検査（`0 nine check`）");
        Console.WriteLine();
        bool all = true;
        void Ok(string name, bool ok, string note = "")
        {
            Console.WriteLine($"- {(ok ? "○" : "×")} {name}{(note == "" ? "" : "  —— " + note)}");
            all &= ok;
        }

        // (a) 新しい入口は、5枠の波では既存の入口と1ビットも違わない（本編5波 × 61 行 × seed 0..199）
        {
            var rows = CompareBuilds();
            long diff = 0, n = 0;
            var gate = new object();
            Parallel.For(0, rows.Length * EnemyCatalog.Stages.Count, j =>
            {
                var f = rows[j / EnemyCatalog.Stages.Count].F;
                var st = EnemyCatalog.Stages[j % EnemyCatalog.Stages.Count].Enemy;
                var wave = EnemyWave.Of(st.Occupied().Select(o => (o.Slot, o.Def)).ToArray());
                long d = 0;
                for (int s = 0; s < Seeds; s++)
                {
                    var a = BattleEngine.Run(f, st, s, verbose: false);
                    var b = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(wave), s, verbose: false);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns || a.PlayerSurvivors != b.PlayerSurvivors) d++;
                }
                lock (gate) { diff += d; n += Seeds; }
            });
            Ok("(a) 本編5波を EnemyWave に写して MaterializeEnemy を通すと、Formation の入口と勝敗・決着T・生存数が全戦一致", diff == 0, $"{n} 戦中 不一致 {diff}");
        }

        // (b) プレイヤー側から9枠に届かない
        {
            bool throws = false;
            try { var f = new Formation(); f[5] = UnitCatalog.Gald; } catch (IndexOutOfRangeException) { throws = true; }
            Ok("(b1) Formation は5枠のまま（席 5 に書くと添字で落ちる）", throws && FormationRules.PlayableSlotCount == 5);

            var takers = typeof(BattleEngine).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(EnemyWave)))
                .Select(m => m.DeclaringType!.Name + "." + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name)) + ")")
                .ToList();
            bool noTeamParam = typeof(BattleEngine).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(EnemyWave)))
                .All(m => m.GetParameters().All(p => p.ParameterType != typeof(int)));
            Ok("(b2) EnemyWave を受け取る BattleCore のメソッドは陣営（int）を取らない", noTeamParam, string.Join(" ／ ", takers));
            bool noCtor = typeof(EnemyWave).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0;
            Ok("(b3) EnemyWave に public なコンストラクタが無い（Of / FillAll / FillX だけ）", noCtor);
            bool enemyOnly = EnemyCatalog.TestStages.All(t => BattleEngine.MaterializeEnemy(t.Enemy).All(u => u.TeamId == BattleContext.EnemyTeam && u.Shape == FormationShape.X));
            Ok("(b4) MaterializeEnemy の駒は全部 敵陣営・X 字", enemyOnly);
        }

        // (c) 9体が全員 slot 0〜8 の別々の席に立つ
        foreach (var t in EnemyCatalog.TestStages.Where(t => t.Enemy.Count == 9))
        {
            var u = BattleEngine.MaterializeEnemy(t.Enemy);
            Ok($"(c) {t.Name}: 9体が席 0〜8 に1体ずつ", u.Count == 9 && u.Select(x => x.Slot).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, 9)));
        }

        // (d)(e) 前列が生きている間は後列を狙わない ／ ○前2・○後2 に貫きが当たらない（経路に誰かいる間）
        //        台は特性を持たない素の駒（単体4 ＋ 貫き1）。敵を動かす駒がいないので席は戦の間変わらない。
        {
            UnitDef Plain(string id, AttackPattern p) => new()
            { Id = id, Name = id, MaxHp = 80, Attack = 14, Speed = 7, Pattern = p, Traits = Array.Empty<TraitId>() };
            var f = Formation.Build(front1: Plain("p1", AttackPattern.Single), front3: Plain("p2", AttackPattern.Single),
                                    center: Plain("p3", AttackPattern.Single), back1: Plain("p4", AttackPattern.Single),
                                    back3: Plain("pk", AttackPattern.Pierce));
            long singleSwings = 0, rowBreaks = 0, pierceHits = 0, pierceOffLane = 0, pierceDeadHits = 0, battles = 0;
            foreach (var t in EnemyCatalog.TestStages.Where(t => t.Enemy.Count == 9))
                for (int s = 0; s < Seeds; s++)
                {
                    var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var en = BattleEngine.MaterializeEnemy(t.Enemy);
                    var r = BattleEngine.Run(pl, en, s, verbose: true);
                    battles++;
                    var byId = pl.Concat(en).ToDictionary(u => u.InstanceId);
                    var alive = en.Select(u => u.InstanceId).ToHashSet();
                    UnitState? swinger = null;
                    foreach (var e in r.Events)
                    {
                        if (e.Kind == BattleEventKind.Death && e.TargetId is int d) alive.Remove(d);
                        if (e.Kind == BattleEventKind.Attack && e.ActorId is int a && byId[a].TeamId == BattleContext.PlayerTeam)
                        {
                            swinger = byId[a];
                            if (swinger.Def.Pattern == AttackPattern.Single && e.TargetId is int tg)
                            {
                                singleSwings++;
                                bool frontAlive = alive.Any(i => byId[i].Row == Row.Front);
                                bool midAlive = alive.Any(i => byId[i].Row == Row.Mid);
                                Row row = byId[tg].Row;
                                if ((frontAlive && row != Row.Front) || (!frontAlive && midAlive && row == Row.Back)) rowBreaks++;
                            }
                        }
                        if (e.Kind == BattleEventKind.Damage && swinger?.Def.Id == "pk" && e.ActorId == swinger.InstanceId
                            && e.TargetId is int h && byId[h].TeamId == BattleContext.EnemyTeam)
                        {
                            pierceHits++;
                            int seat = byId[h].Slot;
                            if (FormationRules.LanesOf(seat).Count == 0)
                            {
                                bool othersOnLane = alive.Where(i => i != h).Any(i => FormationRules.LanesOf(byId[i].Slot).Count > 0);
                                if (othersOnLane) pierceOffLane++; else pierceDeadHits++;
                            }
                        }
                    }
                }
            Ok("(d) 前列が生きている間は単体の一撃が後ろの列へ行かない（中列が生きている間は後列へ行かない）", rowBreaks == 0 && singleSwings > 0,
               $"{battles} 戦・単体の一撃 {singleSwings} 回・違反 {rowBreaks}");
            Ok("(e) 経路に誰かいる間、貫きは ○前2・○後2 に当たらない", pierceOffLane == 0 && pierceHits > 0,
               $"貫きの被弾 {pierceHits} 回・違反 {pierceOffLane}・行き止まりで 7／8 に落ちた単体1発 {pierceDeadHits}");
        }

        // (f) 61 行 × 検証の3波 × 200 戦を完走する（例外なし・30 ターンで必ず止まる）
        {
            var rows = CompareBuilds();
            long n = 0, over = 0; var gate = new object();
            Parallel.For(0, rows.Length * EnemyCatalog.TestStages.Count, j =>
            {
                var f = rows[j / EnemyCatalog.TestStages.Count].F;
                var w = EnemyCatalog.TestStages[j % EnemyCatalog.TestStages.Count].Enemy;
                long o = 0;
                for (int s = 0; s < Seeds; s++)
                {
                    var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(w), s, verbose: false);
                    if (r.Turns > 30) o++;
                }
                lock (gate) { n += Seeds; over += o; }
            });
            Ok("(f) 61 行 × 検証の3波 × 200 戦を完走（例外なし・30 ターン超え 0）", over == 0 && n == 61L * 3 * Seeds, $"{n} 戦");
        }

        // (g) 本編の Stages は5波のまま・検証の波は混ざらない
        Ok("(g) 本編の Stages は5波のまま・Columns は Formation だけ", EnemyCatalog.Stages.Count == 5 && EnemyCatalog.TestStages.Count == 3);

        Console.WriteLine();
        Console.WriteLine($"NINE_CHECK_COMPLETE ok={all}");
    }
}
