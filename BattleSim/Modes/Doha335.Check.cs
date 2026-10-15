using BattleCore;
using static Common;
using T323 = Tou323Diag;

// doha335 の自己検査（第335期）。規則は盤を直に組んで ApplyDamage を呼ぶ（`T323.Ctx`）。
static partial class Doha335Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    /// <summary>札の無い木偶の味方（HP 500）。</summary>
    static UnitDef PalOf(string id) => new()
    {
        Id = id, Name = "木偶の味方", MaxHp = 500, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly UnitDef Pal = PalOf("pal335"), Pal2 = PalOf("pal335b"), Pal3 = PalOf("pal335c");
    static readonly UnitDef Dummy = new()
    {
        Id = "dummy335", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };

    static int Lost(UnitState u) => u.MaxHp - u.Hp;

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# doha335 check —— 第335期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef Z = UnitCatalog.Zan, ZO = UnitCatalog.ZanH334;

        // ---- 段0: 定義と固定の口
        {
            bool def = Z.Traits.SequenceEqual(ZO.Traits.Append(TraitId.VendettaDirectOnly)) && Z.Traits.SequenceEqual(UnitCatalog.ZanZCa.Traits)
                && Z.PlusText == UnitCatalog.ZanZCa.PlusText && Z.PlusText.Contains("標を付けられた味方が直に殴られるたび", StringComparison.Ordinal) && Z.MinusText == ZO.MinusText
                && ZO.Traits.SequenceEqual(new[] { TraitId.Vendetta, TraitId.Recoil, TraitId.VendettaFrameAll, TraitId.VendettaRound, TraitId.VendettaMarkFirst })
                && new[] { (UnitCatalog.ZanZCb, TraitId.VendettaRoundCap3), (UnitCatalog.ZanZCc, TraitId.RecoilLethal), (UnitCatalog.ZanZCd, TraitId.VendettaFrameOnce) }.All(c => c.Item1.Traits.SequenceEqual(ZO.Traits.Append(c.Item2)))
                && UnitCatalog.All.Contains(Z) && !UnitCatalog.Everyone.Contains(ZO) && UnitCatalog.All.Count == 52;
            Expect("段0 定義: 規定のザン ＝ 第301〜334期の規定（`ZanH334`）＋ `VendettaDirectOnly`（第334期の ZC-a と札 ／ 文面が同じ）・ZC-b ／ c ／ d は旧から作り直した（札は1枚ずつ）・旧は `Everyone` の外", def);
            bool pin = ReferenceEquals(PinZ334(Z), ZO) && ReferenceEquals(Pin334(Formation.Build(front3: Z))[1], ZO)
                && new Func<UnitDef, UnitDef>[] { PinM333, PinS332, PinTK331, PinT328, PinTK327, PinT326, PinTKK324, PinS317, PinDS316 }.All(p => ReferenceEquals(p(Z), ZO))
                && ReferenceEquals(Pin299(Formation.Build(front3: Z))[1], UnitCatalog.ZanZN0) && ReferenceEquals(Pin300(Formation.Build(front3: Z))[1], UnitCatalog.ZanZNb)
                && ReferenceEquals(Pin301(Formation.Build(front3: Z))[1], UnitCatalog.ZanZMa) && ReferenceEquals(Pin299(PlaytestH316.First(r => r.Name == GuardRow).F)[1], UnitCatalog.ZanZN0)
                && ReferenceEquals(Zan334Diag.Vers[0].Zan, ZO) && CompareBuildsH334().Concat(PlaytestH334).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, Z));
            Expect("段0 固定の口: `PinZ334` ／ `Pin334` は規定のザンを `ZanH334` へ・古い口の終端（`PinM333` を最後に通す口すべて）も・`Pin299` ／ `Pin300` ／ `Pin301` は内側の口が先に替えた `ZanH334` も旧へ・`zan334` の「規定」は `ZanH334`", pin);
        }

        // ---- 段0: 規定のザンは中継で仇討ちせず直でする・旧は中継でもする（標の付いたドハが前3 の木偶の痛みを肩代わり）
        foreach (var (zan, tag) in new[] { (Z, "規定（ZC-a）"), (ZO, "旧（`ZanH334`）") })
        {
            bool now = zan == Z;
            var ctx = T323.Ctx(Formation.Build(front1: zan, front3: Pal, center: UnitCatalog.DohaH335), Formation.Build(front1: Dummy), out var p, out var e);   // 第336期: 第317〜335期の規定のドハ
            var doha = T323.Seat(p, 2); var pal = T323.Seat(p, 1);
            doha.SetCounter(StatusKeys.Marked, 1);
            ctx.ApplyDamage(pal, 20, e[0]);
            long relay = T323.Tal(ctx, "zan").VendettaFires;
            ctx.ApplyDamage(doha, 20, e[0]);
            long direct = T323.Tal(ctx, "zan").VendettaFires - relay;
            Expect($"段0 {tag}: 標の付いたドハへの中継の一撃では{(now ? "仇討ちしない" : "仇討ちする")}・直の一撃では仇討ちする", (now ? relay == 0 : relay == 1) && direct == 1, $"中継 {relay} ／ 直 {direct}");
        }

        // ---- 版の定義と試遊の台
        {
            UnitDef D = UnitCatalog.DohaH335, G = UnitCatalog.GolmH344;   // 第336期: 版の土台は第317〜335期の規定のドハ
            bool vers = UnitCatalog.DohaDMa.Traits.SequenceEqual(D.Traits.Append(TraitId.DohaSplit)) && UnitCatalog.DohaDMc.Traits.SequenceEqual(D.Traits.Append(TraitId.DohaOverflow))
                && UnitCatalog.GolmDrink.Traits.SequenceEqual(G.Traits.Append(TraitId.ColossusDrink)) && UnitCatalog.GolmGL.Traits.SequenceEqual(G.Traits.Append(TraitId.ColossusToStriker))
                && UnitCatalog.DohaDMa.MinusText == "自分は強くならず、自分が受ける痛みは、仲間に等しく分かれる" && UnitCatalog.DohaDMc.MinusText == "自分は強くならず、倒れると、背負ってきた痛みの半分が仲間に溢れる"
                && UnitCatalog.GolmDrink.PlusText.EndsWith("／ドハが溢れさせた痛みを飲み干し、自分の力にする", StringComparison.Ordinal) && UnitCatalog.GolmGL.PlusText.Contains("飲み込んだ力は、戦う仲間へ返す", StringComparison.Ordinal)
                && new[] { UnitCatalog.DohaDMa, UnitCatalog.DohaDMc, UnitCatalog.GolmDrink, UnitCatalog.GolmGL }.All(d => !UnitCatalog.Everyone.Contains(d))
                && UnitCatalog.Everyone.All(d => !d.Traits.Any(t => (t is TraitId.DohaSplit && !ReferenceEquals(d, UnitCatalog.Doha)) || t is TraitId.DohaOverflow or TraitId.ColossusDrink or TraitId.ColossusToStriker));   // 第336期: DM-a は規定のドハの札になった
            Expect("版の定義: DM-a ／ DM-c ＝ 規定のドハ ＋ 札1枚（マイナスの「味方が多いほど早く尽きる」を新しい句に置き換え）・ゴルム（飲み干す ／ GL）＝ 規定のゴルム ＋ 札1枚・`Everyone` の外・保持者 0", vers);
            var v = Presets.Versions;
            // 第336期: `Versions` は期ごとに「版・」の台を入れ替えるので、1行目（ポンの台）だけを見る（後ろの行は「版・」で始まること）。見出しの文は第335期のまま。
            bool rows = v.Length >= 1 && v[0].Name == "試遊・感電 ポンの台" && v.Skip(1).All(r => r.Name.StartsWith("版・", StringComparison.Ordinal) || r.Name.StartsWith("試遊・", StringComparison.Ordinal))   /* 第340期: 「試遊・ミサ 書き手なし」も */ && Presets.Playtest.Length == 17;
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」＋「版・DM-a ／ DM-c ／ DM-c＋ゴルム ／ GL 守り型」（守り型の並びでドハ ／ ゴルムだけ版）・第334期の「版・ZC」の4台は外した・`Playtest` は 17 台のまま", rows);
        }

        // ---- DM-a: ドハへの直の一撃はドハ以外に等分・ドハは受けない・欠片は巨躯 ／ 分かち ／ 仇討ち ／ 指差しに乗らない・標のドハへの直の一撃で仇討ちが1回
        {
            // (1) 前1 ゴルム ／ 前3 ドハ（ゴルムと同じ列なので巨躯は掛からない）／ 中央・後1・後3 木偶。40 を 4 体で 10 ずつ（後ろの木偶への欠片もゴルムは飲まない）。
            var ctx = T323.Ctx(Formation.Build(front1: UnitCatalog.GolmH344, front3: UnitCatalog.DohaDMa, center: Pal, back1: Pal2, back3: Pal3), Formation.Build(front1: Dummy), out var p, out var e);
            var golm = T323.Seat(p, 0); var doha = T323.Seat(p, 1); var a1 = T323.Seat(p, 2); var a2 = T323.Seat(p, 3); var a3 = T323.Seat(p, 4);
            ctx.ApplyDamage(doha, 40, e[0]);
            var dt = T323.Tal(ctx, "doha");
            bool ok = Lost(doha) == 0 && Lost(golm) == 10 && Lost(a1) == 10 && Lost(a2) == 10 && Lost(a3) == 10
                && T323.Tal(ctx, "golm").Swallowed == 0 && dt.ShareLoad335 == 0 && dt.Dm335Splits == 1 && dt.Dm335SplitAmt == 40;
            Expect("DM-a: ドハへの直の一撃 40 は、ゴルム ／ 木偶3体に 10 ずつ（ドハは受けない）・欠片はゴルムの巨躯にもドハの分かちにも乗らない",
                ok, $"ドハ {Lost(doha)}・ゴルム {Lost(golm)}・木偶 {Lost(a1)} ／ {Lost(a2)} ／ {Lost(a3)}・巨躯 {T323.Tal(ctx, "golm").Swallowed}・分かち {dt.ShareLoad335}");

            // (2) 合図: 標の付いたドハへの直の一撃で仇討ちが1回。欠片は標の付いた木偶に当たっても仇討ちを呼ばない——中継でも仇討ちする旧のザン（`ZanH334`）で確かめる。
            var ctx1 = T323.Ctx(Formation.Build(front1: ZO, front3: UnitCatalog.DohaDMa, center: Pal, back1: Pal2), Formation.Build(front1: Dummy), out var r, out var re);
            var rd = T323.Seat(r, 1); var rp = T323.Seat(r, 2);
            rd.SetCounter(StatusKeys.Marked, 1); rp.SetCounter(StatusKeys.Marked, 1);
            ctx1.ApplyDamage(rd, 30, re[0]);
            long vf = T323.Tal(ctx1, "zan").VendettaFires;
            Expect("DM-a: 標の付いたドハへの直の一撃で仇討ちは1回（直に殴られた合図）・欠片が標の付いた木偶に当たっても仇討ちしない（中継でも仇討ちする旧のザンで）", vf == 1 && Lost(rp) == 10,
                $"仇討ち {vf}・標の木偶 {Lost(rp)}");

            // (3) 端数: 41 を 4 体 → 席の若い順に 11 ／ 10 ／ 10 ／ 10。同士討ち（木偶の撃ち手）でも等分し、ヒサの「あいつがやった！」は合図の1回だけ（欠片では出ない）。
            var ctx2 = T323.Ctx(Formation.Build(front1: Pal3, front3: UnitCatalog.DohaDMa, center: Pal, back1: Pal2, back3: UnitCatalog.Hisa), Formation.Build(front1: Dummy), out var q, out var _);
            var d2 = T323.Seat(q, 1); var b0 = T323.Seat(q, 0); var b2 = T323.Seat(q, 2); var b3 = T323.Seat(q, 3); var hisa = T323.Seat(q, 4);
            d2.SetCounter(StatusKeys.Marked, 1);
            ctx2.ApplyDamage(d2, 41, b0, isFriendlyFire: true);
            bool ok2 = Lost(d2) == 0 && Lost(b0) == 11 && Lost(b2) == 10 && Lost(b3) == 10 && Lost(hisa) == 10 && T323.Tal(ctx2, "hisa").FrameAccuses == 1;
            Expect("DM-a: 端数は席の若い方へ（41 → 11 ／ 10 ／ 10 ／ 10）・味方の一撃でも等分・ヒサの指差しは直の一撃の合図の1回だけ（欠片では出ない）", ok2,
                $"{Lost(b0)} ／ {Lost(b2)} ／ {Lost(b3)} ／ {Lost(hisa)}・ドハ {Lost(d2)}・指差し {T323.Tal(ctx2, "hisa").FrameAccuses}");
        }

        // ---- DM-c ／ DM-c ＋ ゴルム
        foreach (var (golmDef, drink, golmDead) in new[] { (UnitCatalog.GolmH344, false, false), (UnitCatalog.GolmDrink, true, false), (UnitCatalog.GolmDrink, true, true) })
        {
            // 前1 ゴルム ／ 前3 木偶（殴られ役・ゴルムと同じ列）／ 中央 ドハ（DM-c）／ 後1 木偶（あとで標）／ 後3 旧のザン（中継でも仇討ちする）。
            // 前3 の木偶への一撃 50 を5回 → ドハの肩代わり（名目）は 20 ＋ ゴルムと押し付け合う中継の分（巨躯が飲んだ中継をドハがまた4割…）で 1回 29 → 累計 145 → 倒れると 72 が溢れる。
            var ctx = T323.Ctx(Formation.Build(front1: golmDef, front3: Pal3, center: UnitCatalog.DohaDMc, back1: Pal, back3: ZO), Formation.Build(front1: Dummy), out var p, out var e);
            var golm = T323.Seat(p, 0); var hit = T323.Seat(p, 1); var doha = T323.Seat(p, 2); var a1 = T323.Seat(p, 3); var zan = T323.Seat(p, 4);
            for (int i = 0; i < 5; i++) ctx.ApplyDamage(hit, 50, e[0]);
            int load = doha.RawCounter(DohaOverflowTrait.LoadKey);
            if (golmDead) golm.Hp = 0;   // 先に倒れていた（生きている味方から外れれば足りる）
            a1.SetCounter(StatusKeys.Marked, 1);
            var rest = new[] { golm, hit, a1, zan };
            int[] hp0 = rest.Select(u => u.Hp).ToArray(); int atk0 = golm.AtkBonus; long sw0 = T323.Tal(ctx, "golm").Swallowed, vf0 = T323.Tal(ctx, "zan").VendettaFires;
            // とどめは徴収で入れる（出どころがゴルムなら巨躯は自分の出どころを肩代わりしない・ゴルムが倒れていれば木偶から）
            ctx.ApplyDamage(doha, 9999, golmDead ? hit : golm, isFriendlyFire: true, levy: true);
            int[] lost = rest.Select((u, i) => hp0[i] - u.Hp).ToArray();
            long vf = T323.Tal(ctx, "zan").VendettaFires - vf0, sw = T323.Tal(ctx, "golm").Swallowed - sw0;
            int[] hp1 = rest.Select(u => u.Hp).ToArray();
            ctx.DohaOverflow(doha);   // 2度目は溢れない（1戦に1度）
            bool once = rest.Select(u => u.Hp).SequenceEqual(hp1);
            int total = load * DohaOverflowTrait.Percent / 100;
            bool ok;
            string tag;
            if (drink && !golmDead)
            {
                tag = "DM-c ＋ ゴルム（生きている）";
                ok = load == T323.Tal(ctx, "doha").ShareLoad335 && total > 0 && !doha.IsAlive && lost.All(x => x == 0) && golm.AtkBonus - atk0 == Math.Max(1, total / ColossusTrait.DamagePerGain)
                     && T323.Tal(ctx, "golm").Dm335Drank == total && once;
            }
            else
            {
                tag = drink ? "DM-c ＋ ゴルム（ゴルムが倒れていた）" : "DM-c";
                int[] alive = golmDead ? new[] { 1, 2, 3 } : new[] { 0, 1, 2, 3 };
                ok = load == T323.Tal(ctx, "doha").ShareLoad335 && total > 0 && !doha.IsAlive && alive.Sum(i => lost[i]) == total && alive.All(i => lost[i] >= total / alive.Length) && sw == 0 && vf == 0
                     && T323.Tal(ctx, "doha").Dm335Overflows == 1 && T323.Tal(ctx, "doha").Dm335OverflowAmt == total && golm.AtkBonus == atk0 && once;
            }
            Expect($"{tag}: ドハが倒れると肩代わりの累計（名目）の5割が{(drink && !golmDead ? "ゴルムにすべて飲まれ（誰もダメージを受けない）、巨躯と同じ換算（÷4）でゴルムの攻撃力になる" : "生きている味方に等分され（巨躯に乗らない・標の木偶に当たっても仇討ちしない）")}・1戦に1度",
                ok, $"累計 {load} → 溢れ {total}・失った HP {string.Join(" ／ ", lost)}・巨躯 {sw}・仇討ち {vf}・ゴルムの攻撃 +{golm.AtkBonus - atk0}・2度目 {(once ? "なし" : "あり")}");
        }

        // ---- GL: 返り先が殴らない駒なら、攻撃力の最も高い殴る味方へ（規定と GL で同じ一撃を入れ、規定でドハへ返った力が GL ではミサへ回ることを見る）
        {
            var got = new Dictionary<bool, (int Doha, int Misa, int Zan, int Misa2, long Redir, bool Strikers)>();
            foreach (var (golmDef, gl) in new[] { (UnitCatalog.GolmH344, false), (UnitCatalog.GolmGL, true) })
            {
                var ctx = T323.Ctx(Formation.Build(front1: golmDef, front3: Z, center: UnitCatalog.DohaH335, back1: UnitCatalog.Tome, back3: UnitCatalog.Hisa), Formation.Build(front1: Dummy), out var p, out var e);   // 第336期: 第317〜335期の規定のドハ
                var zan = T323.Seat(p, 1); var doha = T323.Seat(p, 2); var misa = T323.Seat(p, 3); var hisa = T323.Seat(p, 4);
                bool strikers = BattleContext.StrikesOnTurn(misa.Def) && BattleContext.StrikesOnTurn(zan.Def) && !BattleContext.StrikesOnTurn(doha.Def) && !BattleContext.StrikesOnTurn(hisa.Def);
                ctx.ApplyDamage(doha, 40, e[0]);   // ゴルムがドハへの一撃を飲む（ドハ ↔ ゴルムの中継の押し付け合いの分も）
                int d1 = doha.AtkBonus, m1 = misa.AtkBonus;
                ctx.ApplyDamage(misa, 40, e[0]);   // 殴る駒（ミサ）への一撃
                got[gl] = (d1, m1, zan.AtkBonus, misa.AtkBonus - m1, T323.Tal(ctx, "golm").Gl335Redirects, strikers);
            }
            var b0 = got[false]; var g1 = got[true];
            bool ok = b0.Strikers && g1.Strikers && b0.Doha > 0 && b0.Misa == 0 && g1.Doha == 0 && g1.Misa == b0.Doha && g1.Redir > 0 && b0.Redir == 0 && b0.Zan == 0 && g1.Zan == 0 && b0.Misa2 > 0 && g1.Misa2 >= b0.Misa2;
            Expect("GL: ゴルムがドハ（殴らない）への一撃を飲んだ力は、規定ではドハへ・GL では攻撃力の最も高い殴る味方（ミサ 12 ＞ ザン 10）へ同じ量・殴る駒（ミサ）への一撃の分はどちらもミサへ", ok,
                $"規定 ドハ +{b0.Doha} ／ ミサ +{b0.Misa}・GL ドハ +{g1.Doha} ／ ミサ +{g1.Misa}（回した {g1.Redir}）・ミサへの一撃の分 {b0.Misa2} ／ {g1.Misa2}・ザン {b0.Zan} ／ {g1.Zan}");
        }

        // ---- 計数は盤面を変えない: verbose の有無で勝敗 ／ 決着T が同じ（代表台 6 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 4 版）
        {
            int diff = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, f) in Boards())
                    foreach (var w in new[] { Waves[0], Waves[1], Waves[2] })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            var fv = Apply(f, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            n++;
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        }
            Expect($"代表台 6 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 4 版（{n} 戦）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show a7fa8fb:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第334期（a7fa8fb）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

}
