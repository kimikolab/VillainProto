using System.Reflection;
using BattleCore;
using static Common;
using T323 = Tou323Diag;

// dense326 の自己検査（第326期）。盤は `tou323` と同じ作り（`T323.Ctx`・壁は HP 5000 ／ 攻 1）。
static partial class Dense326Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly FieldInfo DensityField = typeof(BattleContext).GetField("_density", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_density が見つからない");
    static void SetDensity(BattleContext ctx, UnitState u, int n) => ((Dictionary<int, int>)DensityField.GetValue(ctx)!)[u.InstanceId] = n;
    static List<int> Dis(BattleContext ctx, UnitState from, UnitState to, int since)
        => ctx.Events.Skip(since).Where(x => x.Kind == BattleEventKind.Discharge && x.ActorId == from.InstanceId && x.TargetId == to.InstanceId).Select(x => x.Amount).ToList();

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# dense326 check —— 第326期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var Wall = T323.Wall; var Mate = T323.Mate;

        // ---- 定義
        {
            var S = UnitCatalog.Shiga; var O = UnitCatalog.ShigaH325;
            bool shiga = S.Traits.SequenceEqual(O.Traits.Append(TraitId.ScourgeFearDoubled)) && O.Traits.SequenceEqual(UnitCatalog.ShigaSA.Traits)
                && S.PlusText == O.PlusText && S.MinusText == O.MinusText.Replace(UnitCatalog.ShigaDauntBoundClause, UnitCatalog.ShigaDoubledBoundClause) && S.MinusText.Contains("感電した敵は動けない敵とみなす", StringComparison.Ordinal)
                && S.Id == O.Id && S.MaxHp == O.MaxHp && S.Attack == O.Attack && S.Speed == O.Speed && S.Flavor == O.Flavor;
            bool sap = UnitCatalog.ShigaSApF.Traits.SequenceEqual(UnitCatalog.ShigaSAp.Traits.Append(TraitId.ScourgeFearDoubled)) && UnitCatalog.ShigaSApF.MinusText.Contains(UnitCatalog.ShigaDoubledBoundClause, StringComparison.Ordinal);
            bool sd = UnitCatalog.ShigaSD.Traits.SequenceEqual(O.Traits.Append(TraitId.WhipDischarge));
            var T = UnitCatalog.TouH326;   // 第327期: 第326期の規定のトウ（DR-a の前）
            bool dra = UnitCatalog.TouDRa.Traits.SequenceEqual(T.Traits.Append(TraitId.DenseConduct)) && UnitCatalog.TouDRa.PlusText.Contains("濃い駒は浴びる雷が重くなり、弾けた本人も焼く", StringComparison.Ordinal);
            bool drb = UnitCatalog.TouDRb.Traits.SequenceEqual(T.Traits.Append(TraitId.DenseConductBoth)) && UnitCatalog.TouDRb.PlusText.Contains("濃い駒は放つ雷も浴びる雷も重くなり、弾けた本人も焼く", StringComparison.Ordinal);
            bool body = new[] { UnitCatalog.TouDRa, UnitCatalog.TouDRb }.All(d => d.Id == T.Id && d.MaxHp == T.MaxHp && d.Attack == T.Attack && d.Speed == T.Speed && d.MinusText == T.MinusText);
            var vs = new[] { O, UnitCatalog.ShigaSApF, UnitCatalog.TouDRa, UnitCatalog.TouDRb };
            bool outside = vs.All(d => !UnitCatalog.Everyone.Contains(d)) && UnitCatalog.All.Contains(S) && UnitCatalog.All.Contains(UnitCatalog.Tou) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.Where(d => d != S).All(d => !d.Traits.Contains(TraitId.ScourgeFearDoubled)) && UnitCatalog.Everyone.All(d => (d == UnitCatalog.Tou || !d.Traits.Contains(TraitId.DenseConduct)) && !d.Traits.Contains(TraitId.DenseConductBoth));   // 第327期: 規定のトウが DR-a の札を持つ
            Expect("定義: 規定のシガ ＝ 第325期の規定（`ShigaH325`）＋ 段0 の札・文面はマイナスの括弧だけ・SA+（段0 入り）・SA-SD は旧の規定から・DR-a ／ DR-b ＝ 規定のトウ ＋ 札（濃さの句を差し替え）・版は `Everyone` の外・`All` 52 枚",
                shiga && sap && sd && dra && drb && body && outside, $"シガ {shiga}・SA+ {sap}・SD {sd}・DR-a {dra}・DR-b {drb}・体 {body}・外 {outside}");
        }

        // ---- 固定の口
        {
            var S = UnitCatalog.Shiga;
            bool pin = ReferenceEquals(PinS325(S), UnitCatalog.ShigaH325) && ReferenceEquals(PinS325(UnitCatalog.Tou), UnitCatalog.Tou)
                && ReferenceEquals(Pin324(Formation.Build(front1: S, front3: UnitCatalog.Tou))[0], UnitCatalog.ShigaH325) && ReferenceEquals(Pin324(Formation.Build(front1: S, front3: UnitCatalog.Tou))[1], UnitCatalog.TouH324)
                && ReferenceEquals(Pin325(Formation.Build(front1: S, front3: UnitCatalog.Tou))[1], UnitCatalog.TouH326)   // 第327期: `Pin325` はトウも第326期の規定へ
                && ReferenceEquals(PinSH321(S), UnitCatalog.ShigaH321) && ReferenceEquals(PinSS318(S), UnitCatalog.ShigaH318) && ReferenceEquals(PinS317(S), UnitCatalog.ShigaH318) && ReferenceEquals(PinDS316(S), UnitCatalog.ShigaSWa)
                && CompareBuildsH325().Select(r => r.F).SelectMany(f => f.Occupied()).All(o => !ReferenceEquals(o.Def, S))
                && PlaytestH324.Select(r => r.F).SelectMany(f => f.Occupied()).All(o => !ReferenceEquals(o.Def, S));
            Expect("固定の口: `PinS325` ／ `Pin325` はシガだけを第325期の規定へ・`Pin324` はシガも・古い口（`PinSH321` ／ `PinSS318` ／ `PinS317` ／ `PinDS316`）は今まで通りもっと古い規定へ", pin);
        }

        // ---- 段0: 感電していた主目標（2倍）→ 弾けて痺れなければ当たった後は動ける。旧は怖気づき、規定は怖気づかない。萎縮した主目標（2倍）は両方とも怖気づかない。動ける主目標には両方とも怖気づく。
        {
            int Cower(UnitDef shiga, string kind, out int doubled, out int spared)
            {
                int cow = 0; doubled = 0; spared = 0;
                for (int sd = 0; sd < 40; sd++)
                {
                    var ctx = T323.Ctx(Formation.Build(front1: shiga), Formation.Build(front1: Wall), out var p, out var e, seed: sd);
                    var sh = p[0]; var ef = e[0];
                    if (kind == "shock") ctx.MarkShock(ef, sh);
                    if (kind == "daunt") ef.SetCounter(StatusKeys.Daunted, 1);
                    int from = ctx.Events.Count;
                    ctx.TakeTurn(sh);
                    var t = T323.Tal(ctx, "shiga");
                    cow += (int)t.WhipCowered; spared += (int)t.Fear326DoubledSpared;
                    doubled += (int)t.WhipDoubled;
                    // 当たった後に主目標が動けない（痺れた）戦は数えない
                }
                return cow;
            }
            int oc = Cower(UnitCatalog.ShigaH325, "shock", out int od, out _), nc = Cower(UnitCatalog.Shiga, "shock", out int nd, out int ns);
            Expect("段0: 感電した敵を主目標に打った鞭（2倍が入る）——旧（`ShigaH325`）は弾けて痺れなかった戦で怖気づき、規定は1回も怖気づかない（seed 0..39）",
                od == 40 && nd == 40 && oc > 0 && nc == 0 && ns == oc, $"2倍 旧 {od} ／ 規定 {nd}・怖気 旧 {oc} ／ 規定 {nc}・規定で怖気づかなかった {ns}");
            int oa = Cower(UnitCatalog.ShigaH325, "daunt", out int oad, out _), na = Cower(UnitCatalog.Shiga, "daunt", out int nad, out _);
            Expect("段0: 萎縮した敵（2倍が入る）に打った鞭の後は、旧も規定も怖気づかない（seed 0..39）", oad == 40 && nad == 40 && oa == 0 && na == 0, $"2倍 {oad} ／ {nad}・怖気 {oa} ／ {na}");
            int om = Cower(UnitCatalog.ShigaH325, "free", out int omd, out _), nm = Cower(UnitCatalog.Shiga, "free", out int nmd, out _);
            Expect("段0: 動ける敵（2倍なし）には今どおり怖気づく（旧も規定も 40 回・seed 0..39）", omd == 0 && nmd == 0 && om == 40 && nm == 40, $"2倍 {omd} ／ {nmd}・怖気 {om} ／ {nm}");
        }

        // ---- DR: 放電1本の量（敵の側・味方の側・糸）
        //      敵: 前1（出し手・帯電・濃さ s）と中央（受け手・乾いている・濃さ r）。味方: 後3 にトウの版（保持者）。前1 を味方の一撃で弾けさせる。
        {
            const int s = 3, r = 5;
            foreach (var (def, tag, want) in new[] { (UnitCatalog.TouH326, "規定", 8 * (1 + s)), (UnitCatalog.TouDRa, "DR-a", 8 * (1 + r)), (UnitCatalog.TouDRb, "DR-b", 8 * (1 + s + r)) })
            {
                var ctx = T323.Ctx(Formation.Build(back3: def, front1: Mate), Formation.Build(front1: Wall, center: Wall), out var p, out var e);
                var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2);
                ctx.MarkShock(ef, ef);
                SetDensity(ctx, ef, s); SetDensity(ctx, ec, r);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(ef, 1, T323.Seat(p, 0));
                var d = Dis(ctx, ef, ec, from);
                int burned = T323.Burned(ctx, ef, from);
                var tt = T323.Tal(ctx, "tou");
                var rds = ctx.Events.Skip(from).Where(x => x.Kind == BattleEventKind.Discharge && x.ActorId == ef.InstanceId && x.TargetId == ec.InstanceId).Select(x => x.ReceiverDensity).ToList();
                bool disp = rds.Count == 1 && (tag == "規定" ? rds[0] is null : rds[0] == r);
                bool ok = d.Count == 1 && d[0] == want && burned == 8 * s && H(tt.Dr326N, 1 * 4 + 3) == 1 && H(tt.Dr326RecvDens, 1 * 4 + 3) == r && disp;
                Expect($"{tag}（敵の側）: 濃さ {s} の敵が弾けて、濃さ {r} の隣の敵へ放電 {want}（{(tag == "規定" ? "出し手だけ" : tag == "DR-a" ? "受け手だけ・出し手は効かない" : "足し算")}）・焼は今のまま 8 × {s}・表示の欄 `ReceiverDensity` は{(tag == "規定" ? " null" : $" {r}")}", ok,
                    $"放電 {string.Join(",", d)}・焼 {burned}・帳簿 {H(tt.Dr326N, 7)} 本 ／ 受け手の濃さ {H(tt.Dr326RecvDens, 7)}・表示 {string.Join(",", rds.Select(x => x?.ToString() ?? "null"))}");
            }
            // 味方の側（敵味方を問わない）: 味方の前1（帯電・濃さ s）が敵の一撃で弾けて、味方の中央（濃さ r）へ
            foreach (var (def, tag, want) in new[] { (UnitCatalog.TouH326, "規定", 8 * (1 + s)), (UnitCatalog.TouDRa, "DR-a", 8 * (1 + r)), (UnitCatalog.TouDRb, "DR-b", 8 * (1 + s + r)) })
            {
                var ctx = T323.Ctx(Formation.Build(back1: def, front1: Mate, center: Mate), Formation.Build(front1: Wall), out var p, out var e);
                var af = T323.Seat(p, 0); var ac = T323.Seat(p, 2);
                ctx.MarkShock(af, af);
                SetDensity(ctx, af, s); SetDensity(ctx, ac, r);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(af, 1, e[0]);
                var d = Dis(ctx, af, ac, from);
                Expect($"{tag}（味方の側）: 濃さ {s} の味方が弾けて、濃さ {r} の隣の味方へ放電 {want}（敵味方を問わない）", d.Count == 1 && d[0] == want, $"放電 {string.Join(",", d)}");
            }
            // 糸: クグ（帯電・濃さ s）の放電は糸の先の敵（濃さ r）へ——DR では糸の先の敵の濃さが乗る
            foreach (var (def, tag, want) in new[] { (UnitCatalog.TouH326, "規定", 8 * (1 + s)), (UnitCatalog.TouDRa, "DR-a", 8 * (1 + r)), (UnitCatalog.TouDRb, "DR-b", 8 * (1 + s + r)) })
            {
                var ctx = T323.Ctx(Formation.Build(back3: def, front1: UnitCatalog.Kugu), Formation.Build(front1: Wall, front3: Wall), out var p, out var e);
                var kugu = T323.Seat(p, 0); var e3 = T323.Seat(e, 1);
                kugu.SetCounter(GrappleTrait.TargetKey, e3.InstanceId + 1);
                ctx.MarkShock(kugu, kugu);
                SetDensity(ctx, kugu, s); SetDensity(ctx, e3, r);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(kugu, 1, T323.Seat(e, 0));
                var d = Dis(ctx, kugu, e3, from);
                var tt = T323.Tal(ctx, "tou");
                Expect($"{tag}（糸）: 濃さ {s} のクグの放電は糸の先の敵（濃さ {r}）へ {want}", d.Count == 1 && d[0] == want && H(tt.Dr326N, 1 * 4 + 2) == 1, $"放電 {string.Join(",", d)}・帳簿 糸 {H(tt.Dr326N, 6)}");
            }
        }

        // ---- 戦の中で（ポンの台 × ボス ／ 近衛 × seed 0..9）: verbose の有無で勝敗 ／ 決着T が同じ・規定のシガは2倍の後に怖気づかない
        {
            foreach (var v in Vers.Append(Old))
                foreach (var w in new[] { WaveOf("boss"), WaveOf("guard") })
                {
                    int diff = 0; long cowDoubled = 0;
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var f = Apply(PonBoard(), v);
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        cowDoubled += r1.TallyByUnit["shiga"].Fear326CowerDoubled;
                    }
                    bool fixedShiga = v.Shiga.Traits.Contains(TraitId.ScourgeFearDoubled);
                    Expect($"ポンの台 × {w.Name} × {v.Name}（seed 0..9）: verbose の有無で勝敗 ／ 決着T が同じ{(fixedShiga ? "・2倍の後の怖気 0" : "")}", diff == 0 && (!fixedShiga || cowDoubled == 0), $"違い {diff}・2倍の後の怖気 {cowDoubled}");
                }
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 61c4a48:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第325期（61c4a48）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
