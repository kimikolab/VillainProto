using BattleCore;

// =====================================================================================
// tank モード（第118期） —— 時間を買う機構（自己回復タンク）を作って測る
//
// 起点は第117期: **傾きは 16 組すべて 1.0 未満**で、育ちは本物（ムド 3.0 → 36.1）なのに
// 育った駒が 3.3〜7.5T で倒れる。「死なない」4枚のうち時間を買っていたのは**ゴルム1枚**
// （+1.30T）で、ヴェルは符号が負。**壁は「時間を買う手段が薄い」ではなく「1枚しかない」。**
//
// **第115期の形を踏襲する**——機構を作って**ローカル台で測るだけ**。
// `UnitCatalog.All` にも `Presets` にも1文字も足さない（`compare` 305 セル 0 件が受け入れ基準）。
//
// engine に足したのは (1) 規則の受け渡し（`NourishRule`）、(2) 計数、
// (3) ダメージ1回ぶんの札（`BattleContext.Hit`）だけ。**判定は特性の中にある。**
//
//     dotnet run --project BattleSim -c Release 0 tank phase0   # 表P（経路表・§1-B の再測定）
//     dotnet run --project BattleSim -c Release 0 tank run      # 表A〜E
//     dotnet run --project BattleSim -c Release 0 tank check    # 自己検査
//
// **`Program.cs` の top-level statements ではなく、この期だけ別ファイルにしてある。**
// あちらは 67,000 行が全部で1つのメソッドで、ビルドに 4 分半かかる（Release・実測）。
// 診断1本ぶんのローカルとクロージャをそこへ足す理由が無いので、
// **クロージャを1つも作らない形**（すべて static メソッドと static フィールド）で外に置いた。
// **`Program.cs` 側は 5 行の振り分けだけ。** 中身は他の診断と同じ作法で書いてある。
// =====================================================================================
static class TankDiag
{
    // --- 定数（測る前に固定する。第117期と同じ定義）------------------------------------
    const int Seeds = 200;                 // 帯A。`compare` と揃える（規約 (G14)）
    const int Half = 3;                    // 前半3T / 後半3T
    const int MinTurns = 2 * Half;         // 前半と後半が重ならない最短の戦
    static readonly int[] Waves = { 1, 2, 3, 4 };   // 第2〜5波（規約 (G10)）

    static IReadOnlyList<EnemyCatalog.Stage> Stages => EnemyCatalog.Stages;

    // --- 実装から索いた表（`Init` が埋める。**空なら止める**）---------------------------
    static string? _root;
    static readonly Dictionary<TraitId, string> _body = new();

    static UnitDef[] _grow = Array.Empty<UnitDef>();
    static UnitDef[] _eight = Array.Empty<UnitDef>();
    static UnitDef[] _pool = Array.Empty<UnitDef>();
    static UnitDef[] _fill = Array.Empty<UnitDef>();
    static UnitDef _tank = null!, _tankPlain = null!, _golm = null!, _golmPlain = null!;
    static UnitDef? _mover, _duller;

    static readonly List<Tk1BRow> _1b = new();

    // ==================================================================================
    public static void Run(string mode)
    {
        if (!Init()) return;
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunTables(); return;
            case "check": Check(); return;
            default: Console.WriteLine("tank: モードは phase0 / run / check のいずれか。"); return;
        }
    }

    // ==================================================================================
    // 索く。**手で書いた分類は1件も無い**（第94期の作法）。
    // **索けなかったら止める**（§1-C の 8。第117期に踏んだ穴——走査が空でも分類はすべて
    // 偽を返すので、黙って土台の顔ぶれが変わる）。
    // ==================================================================================
    static bool Init()
    {
        _root = Directory.GetCurrentDirectory();
        while (_root != null && !File.Exists(Path.Combine(_root, "docs", "balance.md")))
            _root = Path.GetDirectoryName(_root);

        string path = _root is null ? "" : Path.Combine(_root, "BattleCore", "Traits.cs");
        if (File.Exists(path))
        {
            string src = File.ReadAllText(path);
            var decl = System.Text.RegularExpressions.Regex.Matches(
                src, @"public (?:sealed |abstract )?class (\w+Trait)(?:\s*:\s*(\w+))?");
            var byName = new Dictionary<string, string>(StringComparer.Ordinal);
            var baseOf = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < decl.Count; i++)
            {
                int st = decl[i].Index;
                int en = i + 1 < decl.Count ? decl[i + 1].Index : src.Length;
                byName[decl[i].Groups[1].Value] = src.Substring(st, en - st);
                if (decl[i].Groups[2].Success) baseOf[decl[i].Groups[1].Value] = decl[i].Groups[2].Value;
            }
            foreach (KeyValuePair<string, string> kv in byName)
            {
                string body = kv.Value;
                // 基底の本体を連結する（庇う＝`GuardianTrait : RedirectGainTrait` がこれ）。
                if (baseOf.TryGetValue(kv.Key, out string? b) && byName.TryGetValue(b, out string? bb)) body += bb;
                var m = System.Text.RegularExpressions.Regex.Match(body, @"TraitId Id => TraitId\.(\w+)");
                if (m.Success && Enum.TryParse(m.Groups[1].Value, out TraitId tid)) _body[tid] = body;
            }
        }
        if (_body.Count == 0)
        {
            Console.WriteLine("tank: `BattleCore/Traits.cs` が見つからない（リポジトリ直下から実行すること）。"
                + " **走査が空なら止める**（第117期の器具の事故）。");
            return false;
        }

        // --- 台（§2-1）。**第117期の 16 組の台をそのまま使い、「死なない」側だけを差し替える。**
        _grow = new[] { UnitCatalog.Mudo, UnitCatalog.Kado, UnitCatalog.Yomi, UnitCatalog.Utsu };
        _eight = _grow.Concat(new[] { UnitCatalog.Gald, UnitCatalog.Vel, UnitCatalog.Mug, UnitCatalog.Golm }).ToArray();

        var pool = new List<UnitDef>();
        foreach (UnitDef d in UnitCatalog.All)
            if (FillerOk(d) && !Heals(d) && !SelfGrows(d)) pool.Add(d);
        pool.Sort(delegate (UnitDef a, UnitDef b)
        {
            int c = b.Attack.CompareTo(a.Attack);
            return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
        });
        _pool = pool.ToArray();
        _fill = _pool.Take(3).ToArray();

        // 糧タンク（§0-3）。**`UnitCatalog.All` には入れない。診断のローカル。**
        // **名前・フレーバーはこの期では決めない**（§5。差し替え先が決まってから）。
        _tank = new UnitDef
        {
            Id = "kate",
            Name = "糧のカテ（仮）",
            MaxHp = 160,
            Attack = 3,
            Speed = 3,
            Pattern = AttackPattern.Single,
            Traits = new[] { TraitId.Regen, TraitId.Nourish },
            PlusText = "毎ターン自分のHPが戻る",
            MinusText = "自分にダメージを通した者の攻撃力が上がる（敵味方を問わない）",
            Flavor = "（仮）"
        };
        _tankPlain = Plain(_tank);
        _golm = UnitCatalog.Golm;
        _golmPlain = Plain(_golm);

        _mover = null;
        _duller = null;
        foreach (UnitDef d in UnitCatalog.All)
        {
            if (_eight.Contains(d)) continue;
            if (_mover is null && SuppliesMove(d)) _mover = d;
            if (_duller is null && Has(d, "ctx.Dull(")) _duller = d;
        }
        return true;
    }

    static bool Has(UnitDef d, params string[] needles)
    {
        foreach (TraitId t in d.Traits)
        {
            if (!_body.TryGetValue(t, out string? b)) continue;
            foreach (string n in needles) if (b.Contains(n, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    // 規則 (a)〜(d)（第117期 §1-1）。**すべて実装から引く。**
    static bool WritesAtk(UnitDef d) => Has(d, "ctx.Whet(", "ctx.Dull(");
    static bool ReadsAtk(UnitDef d) => d.Traits.Any(t => t is TraitId.Overload or TraitId.Perverse);
    static bool SuppliesHit(UnitDef d) => Has(d, "isFriendlyFire: true");
    static bool SuppliesMove(UnitDef d) => Has(d, "SwapSlots", "HaulOutPair", "FallBack", "ctx.Summon");
    static bool Fragile(UnitDef d) => d.MaxHp < 50;
    static bool Heals(UnitDef d) => Has(d, "ctx.Heal(", "ctx.Revive(");
    static bool SelfGrows(UnitDef d) => Has(d, "self.AtkBonus +=");
    static bool FillerOk(UnitDef d) => !_eight.Contains(d)
        && !WritesAtk(d) && !ReadsAtk(d) && !SuppliesHit(d) && !SuppliesMove(d) && !Fragile(d);

    /// <summary>素体（同じ数値・特性なし）。<c>Actions</c> も落とす。</summary>
    static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain",
        Name = "素体の" + d.Name,
        MaxHp = d.MaxHp,
        Attack = d.Attack,
        Speed = d.Speed,
        Pattern = d.Pattern,
        Traits = Array.Empty<TraitId>()
    };

    /// <summary>前1 = 死なない ／ 中央 = 育つ ／ 前3・後1・後3 = 土台3枚（第117期と同じ席）。</summary>
    static Formation Bench(UnitDef? grow, UnitDef? hold) => Formation.Build(
        front1: hold, front3: _fill.Length > 0 ? _fill[0] : null, center: grow,
        back1: _fill.Length > 1 ? _fill[1] : null, back3: _fill.Length > 2 ? _fill[2] : null);

    static string NameOf(string id)
    {
        foreach (UnitDef d in UnitCatalog.All) if (d.Id == id) return d.Name;
        if (id == _tank.Id) return _tank.Name;
        return id;
    }

    // ==================================================================================
    // 1戦。**`BossRule(true)` は計数だけを起こす**（第117期と同じ。盤面は動かない）。
    // ==================================================================================
    static TkRow One(Formation f, Formation enemy, int seed, string growId, string holdId,
                     NourishRule? nourish, ReaderRule? reader)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false,
                                          reader: reader, boss: new BossRule(true), nourish: nourish);
        int L = Math.Max(1, r.Turns);
        var row = new TkRow();
        row.Turns = L;
        row.Won = r.PlayerWon;
        row.Ok = L >= MinTurns;

        int[] team = new int[BattleEngine.MaxTurns + 2];
        foreach ((int _, UnitDef d) in f.Occupied())
        {
            if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
            if (t.BossLastAliveTurn > row.PartyAlive) row.PartyAlive = t.BossLastAliveTurn;
            int[]? by = t.BossDmgByTurn;
            if (by is null) continue;
            for (int i = 0; i < team.Length && i < by.Length; i++) team[i] += by[i];
        }
        for (int t = 1; t <= Math.Min(Half, L); t++) row.DmgEarly += team[t];
        for (int t = Math.Max(1, L - Half + 1); t <= L && t < team.Length; t++) row.DmgLate += team[t];

        if (r.TallyByUnit.TryGetValue(holdId, out UnitTally? h))
        {
            row.HoldAlive = h.BossLastAliveTurn;
            row.HoldTaken = h.DamageTaken;
            row.HoldHealed = h.Healed;
            // 交差点（門2）= 回復を差し引いてもHPが減り始めた最初のターン。
            // census はターン頭（回復の**前**）に写すので、hp[T] < hp[T-1] は
            // 「T-1 のあいだの削りが、そのあいだに戻した回復を上回った」を意味する。
            int[] hp = h.BossHpByTurn ?? Array.Empty<int>();
            for (int t = 2; t <= L && t < hp.Length; t++)
            {
                if (hp[t] <= 0 || hp[t] >= hp[t - 1]) continue;
                row.Cross = t;
                break;
            }
        }
        if (r.TallyByUnit.TryGetValue(growId, out UnitTally? g))
        {
            row.GrowAlive = g.BossLastAliveTurn;
            int[] a = g.BossAtkByTurn ?? Array.Empty<int>();
            for (int t = 1; t <= L && t < a.Length; t++) if (a[t] > row.GrowAtkMax) row.GrowAtkMax = a[t];
        }

        row.Fires = r.NourishFires;
        row.Given = r.NourishGiven;
        row.ToFoe = r.NourishToFoe;
        row.ToAlly = r.NourishToAlly;
        row.Soaked = r.NourishSoaked;
        row.Levy = r.NourishLevy;
        row.NoSrc = r.NourishNoSource;
        row.SelfHit = r.NourishSelf;
        row.Dead = r.NourishDead;
        row.WhetNourish = r.WhetByRoute[(int)WhetRoute.Nourish];
        row.Path = r.NourishByPath.ToArray();
        foreach (KeyValuePair<string, int> kv in r.WhetToByRoute[(int)WhetRoute.Nourish])
            row.Fed[kv.Key] = kv.Value;
        return row;
    }

    static List<TkRow> RunWave(Formation f, int w, string growId, string holdId,
                               NourishRule? nourish, ReaderRule? reader, int seeds)
    {
        var rows = new List<TkRow>();
        for (int seed = 0; seed < seeds; seed++)
            rows.Add(One(f, Stages[w].Enemy, seed, growId, holdId, nourish, reader));
        return rows;
    }

    static List<TkRow> RunAll(Formation f, string growId, string holdId,
                              NourishRule? nourish = null, ReaderRule? reader = null, int seeds = Seeds)
    {
        var rows = new List<TkRow>();
        foreach (int w in Waves) rows.AddRange(RunWave(f, w, growId, holdId, nourish, reader, seeds));
        return rows;
    }

    // --- 集計。**傾きは戦ごとの比を平均しない。合計どうしを割る**（第117期と同じ）。--------
    static double RateEarly(List<TkRow> xs)
    {
        int n = 0; long s = 0;
        foreach (TkRow x in xs) { if (!x.Ok) continue; n++; s += x.DmgEarly; }
        return n == 0 ? 0 : s / (double)(n * Half);
    }
    static double RateLate(List<TkRow> xs)
    {
        int n = 0; long s = 0;
        foreach (TkRow x in xs) { if (!x.Ok) continue; n++; s += x.DmgLate; }
        return n == 0 ? 0 : s / (double)(n * Half);
    }
    static double Slope(List<TkRow> xs)
    {
        double e = RateEarly(xs);
        return e <= 0 ? 0 : RateLate(xs) / e;
    }
    static double Win(List<TkRow> xs)
    {
        if (xs.Count == 0) return 0;
        int w = 0;
        foreach (TkRow x in xs) if (x.Won) w++;
        return w * 100.0 / xs.Count;
    }
    static double OkPct(List<TkRow> xs)
    {
        if (xs.Count == 0) return 0;
        int n = 0;
        foreach (TkRow x in xs) if (x.Ok) n++;
        return n * 100.0 / xs.Count;
    }
    static double AvgAlive(List<TkRow> xs) { long s = 0; foreach (TkRow x in xs) s += x.PartyAlive; return xs.Count == 0 ? 0 : s / (double)xs.Count; }
    static double AvgHold(List<TkRow> xs) { long s = 0; foreach (TkRow x in xs) s += x.HoldAlive; return xs.Count == 0 ? 0 : s / (double)xs.Count; }
    static double AvgReach(List<TkRow> xs) { long s = 0; foreach (TkRow x in xs) s += x.GrowAtkMax; return xs.Count == 0 ? 0 : s / (double)xs.Count; }
    static double AvgFires(List<TkRow> xs) { long s = 0; foreach (TkRow x in xs) s += x.Fires; return xs.Count == 0 ? 0 : s / (double)xs.Count; }
    static double AvgGiven(List<TkRow> xs) { long s = 0; foreach (TkRow x in xs) s += x.Given; return xs.Count == 0 ? 0 : s / (double)xs.Count; }
    static double AvgToFoe(List<TkRow> xs) { long s = 0; foreach (TkRow x in xs) s += x.ToFoe; return xs.Count == 0 ? 0 : s / (double)xs.Count; }
    static double AvgToAlly(List<TkRow> xs) { long s = 0; foreach (TkRow x in xs) s += x.ToAlly; return xs.Count == 0 ? 0 : s / (double)xs.Count; }
    static double AvgHealed(List<TkRow> xs) { long s = 0; foreach (TkRow x in xs) s += x.HoldHealed; return xs.Count == 0 ? 0 : s / (double)xs.Count; }
    static double AvgTaken(List<TkRow> xs) { long s = 0; foreach (TkRow x in xs) s += x.HoldTaken; return xs.Count == 0 ? 0 : s / (double)xs.Count; }

    /// <summary>交差した戦だけの平均（<b>0 は「一度も減らなかった」なので混ぜない</b>）。</summary>
    static string CrossText(List<TkRow> xs)
    {
        int n = 0; long s = 0;
        foreach (TkRow x in xs) { if (x.Cross <= 0) continue; n++; s += x.Cross; }
        return n == 0 ? "—" : (s / (double)n).ToString("F2") + "T";
    }
    static double NoCrossPct(List<TkRow> xs)
    {
        if (xs.Count == 0) return 0;
        int n = 0;
        foreach (TkRow x in xs) if (x.Cross <= 0) n++;
        return n * 100.0 / xs.Count;
    }

    static string F1(double v) => v.ToString("F1");
    static string F2(double v) => v.ToString("F2");
    static string Signed(double v) => (v >= 0 ? "+" : "") + v.ToString("F1");
    static string Signed2(double v) => (v >= 0 ? "+" : "") + v.ToString("F2");

    // ==================================================================================
    // §1-B —— ヨミ・ウツ・ヴェルの再測定（第117期の器具の穴をこの期で閉じる）。
    // **本体とは別の判定**（Q5）で、**本体の台には持ち込まない**。
    // `phase0` は表として、`run` は判定として同じ数字を読む（**器具は1つ**）。
    // ==================================================================================
    static void Run1B()
    {
        if (_1b.Count > 0) return;
        One1B("軋みのヨミ", UnitCatalog.Yomi, "移動", _mover, false);
        One1B("軋みのヨミ", UnitCatalog.Yomi, "移動", _mover, true);
        One1B("逆しまのウツ", UnitCatalog.Utsu, "弱体", _duller, false);
        One1B("逆しまのウツ", UnitCatalog.Utsu, "弱体", _duller, true);

        // ヴェル: **倒れても出力が消えない土台**にする（蘇生の価値が測れる台）。
        // 第117期の土台3枚は攻撃役で「倒れると出力が消える」ので、蘇生が測れていなかった。
        UnitDef vel = UnitCatalog.Vel;
        UnitDef bare = Plain(vel);
        Formation fv = Formation.Build(front1: vel, front3: _fill[0], center: UnitCatalog.Mudo,
                                       back1: _fill[1], back3: _fill[2]);
        Formation pv = Formation.Build(front1: bare, front3: _fill[0], center: UnitCatalog.Mudo,
                                       back1: _fill[1], back3: _fill[2]);
        List<TkRow> av = RunAll(fv, UnitCatalog.Mudo.Id, vel.Id, seeds: 50);
        List<TkRow> bv = RunAll(pv, UnitCatalog.Mudo.Id, bare.Id, seeds: 50);
        var rv = new Tk1BRow();
        rv.Label = "継ぎ接ぎのヴェル";
        rv.Input = "味方の死";
        rv.With = true;
        rv.Supplier = "中央にムド（倒れる駒）";
        rv.Reach = AvgAlive(av);
        rv.PlainReach = AvgAlive(bv);
        rv.Win = Win(av);
        rv.PlainWin = Win(bv);
        _1b.Add(rv);
    }

    static void One1B(string label, UnitDef target, string input, UnitDef? supplier, bool with)
    {
        if (with && supplier is null) return;
        UnitDef[] fill = with && supplier is not null
            ? new[] { supplier, _fill[0], _fill[1] }
            : new[] { _fill[0], _fill[1], _fill[2] };
        UnitDef bare = Plain(target);
        Formation f = Formation.Build(front1: _golm, front3: fill[0], center: target, back1: fill[1], back3: fill[2]);
        Formation p = Formation.Build(front1: _golm, front3: fill[0], center: bare, back1: fill[1], back3: fill[2]);
        List<TkRow> a = RunAll(f, target.Id, _golm.Id, seeds: 50);
        List<TkRow> b = RunAll(p, bare.Id, _golm.Id, seeds: 50);
        var row = new Tk1BRow();
        row.Label = label;
        row.Input = input;
        row.With = with;
        row.Supplier = supplier is null ? "—" : supplier.Name;
        row.Reach = AvgReach(a);
        row.PlainReach = AvgReach(b);
        row.Win = Win(a);
        row.PlainWin = Win(b);
        _1b.Add(row);
    }

    static void Print1B()
    {
        Console.WriteLine("移動の供給者: **" + (_mover is null ? "（索けず）" : _mover.Name)
            + "** ／ 弱体の供給者: **" + (_duller is null ? "（索けず）" : _duller.Name)
            + "**（どちらも `UnitCatalog.All` から実装の走査で引いた1枚目）");
        Console.WriteLine();
        Console.WriteLine("| 測る駒 | 入力 | 版 | 到達点 | 素体の到達点 | 差 | 勝率 | 素体の勝率 | 帰属 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (Tk1BRow x in _1b)
        {
            string ver = x.With ? "**供給者あり**（" + x.Supplier + "）" : "供給者なし";
            Console.WriteLine("| " + x.Label + " | " + x.Input + " | " + ver + " | "
                + F1(x.Reach) + " | " + F1(x.PlainReach) + " | " + Signed(x.Reach - x.PlainReach) + " | "
                + F1(x.Win) + "% | " + F1(x.PlainWin) + "% | " + Signed(x.Win - x.PlainWin) + "pt |");
        }
        Console.WriteLine();
        Console.WriteLine("**ヴェルの行だけ「到達点」の列は味方全体の生存T**"
            + "（継ぎ接ぎは `CurrentAttack` を1点も動かさないので、到達点では測れない）。");
    }

    // ==================================================================================
    // phase0（§1）
    // ==================================================================================
    static void Phase0()
    {
        Console.WriteLine("# 第118期 Phase 0 —— 糧タンク（`tank phase0`）");
        Console.WriteLine();
        Console.WriteLine("`Traits.cs` から索けた特性: **" + _body.Count + " 件**"
            + "（0 件なら表を1つも出さずに止まる。手で書いた分類は 0 件）。");

        // --- 表P-0: 実装の事実（§1-A の 1〜5）。**戦闘0回。**
        Console.WriteLine();
        Console.WriteLine("## 表P-0. ダメージ経路の切り分け（§1-A・ソース走査・戦闘0回）");
        Console.WriteLine();
        string eng = _root is null ? "" : File.ReadAllText(Path.Combine(_root, "BattleCore", "BattleEngine.cs"));
        string trs = _root is null ? "" : File.ReadAllText(Path.Combine(_root, "BattleCore", "Traits.cs"));
        int levySites = System.Text.RegularExpressions.Regex.Matches(trs + eng, "levy: true").Count;
        int relaySites = System.Text.RegularExpressions.Regex.Matches(eng, "relayed: true").Count;
        int hpMinus = eng.IndexOf("target.Hp -= amount;", StringComparison.Ordinal);
        int onDamaged = eng.IndexOf("t.OnDamaged(this, target, amount, source);", StringComparison.Ordinal);
        int soakReturn = eng.IndexOf("if (target.HasTrait(TraitId.Nourish)) NourishSoaked++;", StringComparison.Ordinal);
        bool hasSource = eng.Contains("ApplyDamage(UnitState target, int amount, UnitState? source", StringComparison.Ordinal);
        _body.TryGetValue(TraitId.Nourish, out string? nb);
        _body.TryGetValue(TraitId.Regen, out string? rb);
        bool whetOk = nb is not null && nb.Contains("ctx.Whet(", StringComparison.Ordinal)
                      && !nb.Contains("AtkBonus +=", StringComparison.Ordinal);
        bool healOk = rb is not null && rb.Contains("ctx.Heal(", StringComparison.Ordinal)
                      && !rb.Contains("Hp +=", StringComparison.Ordinal);

        Console.WriteLine("| # | 事実 | 実測 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 1 | `ApplyDamage` が `source` を受け取る | " + Mark(hasSource) + " |");
        Console.WriteLine("| 2 | 徴収の札 `levy: true` を立てた箇所 | **" + levySites + " 箇所** |");
        Console.WriteLine("| 3 | `OnDamaged` は `target.Hp -= amount` の**後** | "
            + Mark(hpMinus > 0 && onDamaged > hpMinus) + "（通ったダメージだけが届く） |");
        Console.WriteLine("| 3' | 破片で受け切った被弾は `OnDamaged` より**手前**で return | "
            + Mark(soakReturn > 0 && soakReturn < hpMinus) + "（計数 `NourishSoaked` をそこに置いた） |");
        Console.WriteLine("| 4 | 肩代わりの中継が `source` を保持したまま呼ぶ | **" + relaySites + " 箇所**（`levy` も引き継ぐ） |");
        Console.WriteLine("| 5 | 糧は `AtkBonus` を直接足さず `ctx.Whet` を通る | " + Mark(whetOk) + " |");
        Console.WriteLine("| 5' | 自己回復は `ctx.Heal` を通る（渇きが止める） | " + Mark(healOk) + " |");
        Console.WriteLine();
        Console.WriteLine("`levy: true` を立てた行（**実装から引いた**）:");
        Console.WriteLine();
        foreach (string ln in trs.Split('\n'))
            if (ln.Contains("levy: true", StringComparison.Ordinal)) Console.WriteLine("    " + ln.Trim());

        // --- 表P-1: 経路表を実測で
        Console.WriteLine();
        Console.WriteLine("## 表P-1. 経路表を実測で1行ずつ検証（§3。台はすべて診断のローカル）");
        Console.WriteLine();
        UnitDef? splash = With(TraitId.Splash);
        UnitDef? thorn = With(TraitId.Thorns);
        UnitDef? shatter = With(TraitId.Shatter);
        UnitDef? guard = With(TraitId.Guardian);
        UnitDef? sharer = With(TraitId.Sharer);
        UnitDef? sac = With(TraitId.Sacrifice);
        UnitDef? drain = With(TraitId.Drain);
        var missing = new List<string>();
        if (splash is null) missing.Add("Splash");
        if (thorn is null) missing.Add("Thorns");
        if (shatter is null) missing.Add("Shatter");
        if (guard is null) missing.Add("Guardian");
        if (sharer is null) missing.Add("Sharer");
        if (sac is null) missing.Add("Sacrifice");
        if (drain is null) missing.Add("Drain");
        if (missing.Count > 0)
        {
            Console.WriteLine("**索けなかった特性がある（" + string.Join("・", missing) + "）。表P-1 は出さない。**");
        }
        else
        {
            Console.WriteLine("| 場面 | 台（前1 / 中央） | 発火 | 敵単 | 敵範 | 型無 | 味方 | 中継 | 徴収✕ | 破片✕ | 源無✕ | 育った先（上位3） |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
            Scene("素の台（敵の刃だけ）", _tank, _fill[0]);
            Scene("味方の薙ぎ（巻き込み）", _tank, splash!);
            Scene("味方の反撃（棘の巻き込み）", _tank, thorn!);
            Scene("破片を配る駒と同席", _tank, shatter!);
            Scene("庇う駒が前に立つ", guard!, _tank);
            Scene("巨躯が前に立つ", _golm, _tank);
            Scene("分かちと同席（4割肩代わり）", _tank, sharer!);
            Scene("生贄と同席（徴収）", _tank, sac!);
            Scene("吸いと同席（徴収）", _tank, drain!);
            Console.WriteLine();
            Console.WriteLine("`敵単` = 敵の単体攻撃 ／ `敵範` = 敵の薙ぎ・貫き・全体 ／ `型無` = 反撃・破裂（攻撃型が付かない経路） ／");
            Console.WriteLine("`味方` = 味方の刃の巻き込み ／ `中継` = 肩代わりの内側の段。");
            Console.WriteLine("`徴収✕` `破片✕` `源無✕` は**発火しなかった回数**（4波 × seed 0..19）。");
        }

        // --- 表P-2: §1-B の再測定
        Console.WriteLine();
        Console.WriteLine("## 表P-2. ヨミ・ウツ・ヴェルの再測定（§1-B・第117期の器具の穴）");
        Console.WriteLine();
        Console.WriteLine("第117期の土台の選定規則 (c)（被弾・移動・弱体の供給者を入れない）が、");
        Console.WriteLine("**ヨミ（移動）とウツ（弱体）の入力を1枚残らず台から除いていた。**");
        Console.WriteLine("ここでは規則 (c) を緩めた台を組み、**供給者を入れた版と入れない版を並べる**（差が台の性質であることを示す）。");
        Console.WriteLine();
        Run1B();
        Print1B();

        // --- 表P-3: 波ルールの現況
        Console.WriteLine();
        Console.WriteLine("## 表P-3. 波ルールの現況（§1-C の 6。**指示書の記述ではなく実装から引く**）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 盤面ルールを持つ敵 | ルール | 効果 |");
        Console.WriteLine("|---|---|---|---|");
        for (int w = 0; w < Stages.Count; w++)
        {
            var who = new List<string>();
            var rule = new List<string>();
            var eff = new List<string>();
            foreach ((int _, UnitDef d) in Stages[w].Enemy.Occupied())
                foreach (TraitId t in d.Traits)
                {
                    string? text = BoardRuleText(t);
                    if (text is null) continue;
                    who.Add(d.Name);
                    rule.Add(t.ToString());
                    eff.Add(text);
                }
            Console.WriteLine("| 第" + (w + 1) + "波 | " + (who.Count == 0 ? "—" : string.Join("・", who))
                + " | " + (rule.Count == 0 ? "—" : string.Join("・", rule))
                + " | " + (eff.Count == 0 ? "—" : string.Join("・", eff)) + " |");
        }

        // --- 表P-4: design の grep
        Console.WriteLine();
        Console.WriteLine("## 表P-4. `design/` の grep（§1-C の 7。過去に同種を測っていないか）");
        Console.WriteLine();
        if (_root is null) Console.WriteLine("（リポジトリ直下が見つからない）");
        else
        {
            string[] needles = { "自己回復", "毎ターン回復", "殴った者", "被弾で攻撃者", "タンク" };
            int hitFiles = 0;
            string[] files = Directory.GetFiles(Path.Combine(_root, "design"), "*.md");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string fp in files)
            {
                string txt = File.ReadAllText(fp);
                var hit = new List<string>();
                foreach (string n in needles) if (txt.Contains(n, StringComparison.Ordinal)) hit.Add(n);
                if (hit.Count == 0) continue;
                hitFiles++;
                Console.WriteLine("- `" + Path.GetFileName(fp) + "` — " + string.Join("・", hit));
            }
            Console.WriteLine();
            Console.WriteLine("当たったファイル: **" + hitFiles + " 件**（走査の分母は `design/*.md`）。");
        }

        // --- 表P-5: 台と紙
        Console.WriteLine();
        Console.WriteLine("## 表P-5. 台と紙（§2-1・§2-4 の予測3）");
        Console.WriteLine();
        var names = new List<string>();
        int atkSum = 0;
        foreach (UnitDef d in _fill) { names.Add(d.Name); atkSum += d.Attack; }
        Console.WriteLine("土台3枚: **" + string.Join("・", names) + "**（総攻 " + atkSum + "）"
            + " ／ 候補の分母 " + _pool.Length + " 枚（**第117期と同一**）");
        Console.WriteLine();
        Console.WriteLine("タンク: **" + _tank.Name + "** HP " + _tank.MaxHp + " / 攻 " + _tank.Attack
            + " / 速 " + _tank.Speed + " / 回復 " + RegenTrait.Amount + "/T / 糧 +"
            + NourishRule.Default.Gain + "/hit");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵の総攻 | 回復 | 差（1Tあたりの目減り） | 紙の交差点（HP ÷ 差） |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (int w in Waves)
        {
            int atk = 0;
            foreach ((int _, UnitDef d) in Stages[w].Enemy.Occupied()) atk += d.Attack;
            int gap = atk - RegenTrait.Amount;
            Console.WriteLine("| 第" + (w + 1) + "波 | " + atk + " | " + RegenTrait.Amount + " | " + gap + " | "
                + (gap <= 0 ? "**交差しない**" : F1(_tank.MaxHp / (double)gap) + "T") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**この紙は下限**（規約 (G7)）——糧が敵を育てるので差は毎ターン開き、実測はこれより早いはず。");
        Console.WriteLine("**第三波は渇きで回復が 0 になるのでこの行は成立しない**（実測は表D で見る）。");
        Console.WriteLine("**敵が全員タンクを狙う前提**でもある——標的選択は分散するので、その意味では上限側にも振れる。");
    }

    static string Mark(bool ok) => ok ? "**○**" : "**×**";

    static UnitDef? With(TraitId t)
    {
        foreach (UnitDef d in UnitCatalog.All) if (d.Traits.Contains(t)) return d;
        return null;
    }

    static string? BoardRuleText(TraitId t)
    {
        if (t == TraitId.Drought) return "**回復が一切通らない**（自己回復が丸ごと止まる）";
        if (t == TraitId.Yoke) return "1回のダメージが **" + YokeTrait.Cap + "** で切られる";
        if (t == TraitId.Hush) return "ターン外の行動が通らない";
        if (t == TraitId.Inversion) return "行動順が速さ昇順";
        return null;
    }

    /// <summary>経路表の1行ぶん（4波 × seed 0..19）。</summary>
    static void Scene(string label, UnitDef front1, UnitDef center)
    {
        Formation f = Formation.Build(front1: front1, front3: _fill[0], center: center,
                                      back1: _fill[1], back3: null);
        int fires = 0, soaked = 0, levy = 0, noSrc = 0;
        int[] path = new int[NourishPaths.Count];
        var fed = new Dictionary<string, int>();
        foreach (int w in Waves)
            for (int seed = 0; seed < 20; seed++)
            {
                TkRow x = One(f, Stages[w].Enemy, seed, center.Id, front1.Id, null, null);
                fires += x.Fires;
                soaked += x.Soaked;
                levy += x.Levy;
                noSrc += x.NoSrc;
                for (int i = 0; i < x.Path.Length && i < path.Length; i++) path[i] += x.Path[i];
                foreach (KeyValuePair<string, int> kv in x.Fed)
                    fed[kv.Key] = fed.TryGetValue(kv.Key, out int had) ? had + kv.Value : kv.Value;
            }
        var top = new List<string>();
        var order = fed.ToList();
        order.Sort(delegate (KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        {
            int c = b.Value.CompareTo(a.Value);
            return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
        });
        for (int i = 0; i < order.Count && i < 3; i++) top.Add(NameOf(order[i].Key) + " " + order[i].Value);

        Console.WriteLine("| " + label + " | " + front1.Name + " / " + center.Name + " | " + fires
            + " | " + path[0] + " | " + path[1] + " | " + path[2] + " | " + path[3] + " | " + path[4]
            + " | " + levy + " | " + soaked + " | " + noSrc
            + " | " + (top.Count == 0 ? "—" : string.Join("・", top)) + " |");
    }

    // ==================================================================================
    // run（§2）
    // ==================================================================================
    static void RunTables()
    {
        Console.WriteLine("# 第118期 —— 糧タンク（`tank run`）");
        Console.WriteLine();
        var names = new List<string>();
        foreach (UnitDef d in _fill) names.Add(d.Name);
        Console.WriteLine("台 = 育つ4枚 × 死なない（**タンク** / ゴルム）。土台3枚は第117期と同一（"
            + string.Join("・", names) + "）。");
        Console.WriteLine("第2〜5波（規約 (G10)）× seed 0.." + (Seeds - 1) + "。**傾きの定義は第117期と同じ**"
            + "（後半" + Half + "T ÷ 前半" + Half + "T の与ダメ/T・分母は L ≥ " + MinTurns + " の戦だけ）。");

        var now = new Dictionary<string, List<TkRow>>();
        var g0 = new Dictionary<string, List<TkRow>>();
        var bare = new Dictionary<string, List<TkRow>>();
        var r0 = new Dictionary<string, List<TkRow>>();
        var golm = new Dictionary<string, List<TkRow>>();
        var golmBare = new Dictionary<string, List<TkRow>>();

        foreach (UnitDef g in _grow)
        {
            now[g.Id] = RunAll(Bench(g, _tank), g.Id, _tank.Id);
            g0[g.Id] = RunAll(Bench(g, _tank), g.Id, _tank.Id, nourish: new NourishRule(0));
            bare[g.Id] = RunAll(Bench(g, _tankPlain), g.Id, _tankPlain.Id);
            r0[g.Id] = RunAll(Bench(g, _tank), g.Id, _tank.Id, reader: new ReaderRule(0));
            golm[g.Id] = RunAll(Bench(g, _golm), g.Id, _golm.Id);
            golmBare[g.Id] = RunAll(Bench(g, _golmPlain), g.Id, _golmPlain.Id);
        }

        // --- 表A
        Console.WriteLine();
        Console.WriteLine("## 表A. 門1〜3 ＋ 傾き（8台・第2〜5波まとめ）");
        Console.WriteLine();
        Console.WriteLine("| 育つ | 死なない | 勝率 | 味方生存T | 前1生存T | 交差点 | 未交差 | 発火/戦 | 育ち→敵 | 育ち→味方 | 傾き | L≥6 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (UnitDef g in _grow) RowA(g.Name, "**タンク**", now[g.Id]);
        foreach (UnitDef g in _grow) RowA(g.Name, "ゴルム", golm[g.Id]);
        Console.WriteLine();
        Console.WriteLine("`交差点` = 前1のHPが回復を上回って減り始めた最初のターン（**減り始めた戦だけの平均**）。");
        Console.WriteLine("`未交差` = 決着まで一度も減らなかった戦の割合。`育ち→敵/味方` は1戦あたりに渡した攻撃力。");

        // --- 表A'
        Console.WriteLine();
        Console.WriteLine("## 表A'. 波別（タンク台。**渇きの第三波と軛の第四波が主眼**）");
        Console.WriteLine();
        Console.WriteLine("| 育つ | 波 | 勝率 | 素体の勝率 | 帰属 | 味方生存T | 素体 | 差 | 交差点 | 回復量/戦 | 発火/戦 | 育ち→敵 | 傾き |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var attrib = new Dictionary<string, double>();
        foreach (UnitDef g in _grow)
            foreach (int w in Waves)
            {
                List<TkRow> a = RunWave(Bench(g, _tank), w, g.Id, _tank.Id, null, null, Seeds);
                List<TkRow> b = RunWave(Bench(g, _tankPlain), w, g.Id, _tankPlain.Id, null, null, Seeds);
                double at = Win(a) - Win(b);
                attrib[g.Id + "/" + w] = at;
                Console.WriteLine("| " + g.Name + " | 第" + (w + 1) + "波 | " + F1(Win(a)) + "% | " + F1(Win(b)) + "% | **"
                    + Signed(at) + "pt** | " + F2(AvgAlive(a)) + " | " + F2(AvgAlive(b)) + " | "
                    + Signed2(AvgAlive(a) - AvgAlive(b)) + " | " + CrossText(a) + " | "
                    + F1(AvgHealed(a)) + " | " + F2(AvgFires(a)) + " | " + F2(AvgToFoe(a)) + " | "
                    + F2(Slope(a)) + " |");
            }

        // --- 表B
        Console.WriteLine();
        Console.WriteLine("## 表B. 対照（`Gain = 0` ／ 素体 ／ `ReaderRule(0)`）");
        Console.WriteLine();
        Console.WriteLine("| 育つ | 版 | 勝率 | 帰属（対 素体） | 味方生存T | 前1生存T | 発火/戦 | 育ち量/戦 | 傾き |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (UnitDef g in _grow)
        {
            double baseWin = Win(bare[g.Id]);
            RowB(g.Name, "**現行**（`Gain=2`）", now[g.Id], baseWin, true);
            RowB(g.Name, "`Gain = 0`（回復のみ）", g0[g.Id], baseWin, true);
            RowB(g.Name, "素体（回復も糧も無し）", bare[g.Id], baseWin, false);
            RowB(g.Name, "`ReaderRule(0)`（積み過ぎを切る）", r0[g.Id], baseWin, true);
            RowB(g.Name, "ゴルム（第117期の物差し）", golm[g.Id], Win(golmBare[g.Id]), true);
            RowB(g.Name, "素体のゴルム", golmBare[g.Id], baseWin, false);
        }

        // --- 表C
        Console.WriteLine();
        Console.WriteLine("## 表C. 波ごとの符号（Q3。**この駒の合否**）");
        Console.WriteLine();
        Console.WriteLine("| 育つ | 第2波 | 第3波（渇き） | 第4波（軛） | 第5波 | 第3波と第4波の符号 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        int flip = 0;
        foreach (UnitDef g in _grow)
        {
            double a3 = attrib[g.Id + "/2"], a4 = attrib[g.Id + "/3"];
            bool rev = Math.Sign(a3) * Math.Sign(a4) < 0;
            if (rev) flip++;
            Console.WriteLine("| " + g.Name + " | " + Signed(attrib[g.Id + "/1"]) + " | " + Signed(a3) + " | "
                + Signed(a4) + " | " + Signed(attrib[g.Id + "/4"]) + " | "
                + (rev ? "**逆（○）**" : "同じ（×）") + " |");
        }

        // --- 表D
        Console.WriteLine();
        Console.WriteLine("## 表D. 交差点の分布（門2）");
        Console.WriteLine();
        Console.WriteLine("| 育つ | 波 | 紙の交差点 | 実測の交差点 | 未交差 | 回復量/戦 | 被弾量/戦 | 前1生存T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (UnitDef g in _grow)
            foreach (int w in Waves)
            {
                List<TkRow> a = RunWave(Bench(g, _tank), w, g.Id, _tank.Id, null, null, Seeds);
                int atk = 0;
                foreach ((int _, UnitDef d) in Stages[w].Enemy.Occupied()) atk += d.Attack;
                int gap = atk - RegenTrait.Amount;
                Console.WriteLine("| " + g.Name + " | 第" + (w + 1) + "波 | "
                    + (gap <= 0 ? "交差しない" : F1(_tank.MaxHp / (double)gap) + "T") + " | "
                    + CrossText(a) + " | " + F1(NoCrossPct(a)) + "% | " + F1(AvgHealed(a)) + " | "
                    + F1(AvgTaken(a)) + " | " + F2(AvgHold(a)) + " |");
            }

        // --- 表E
        Run1B();
        Console.WriteLine();
        Console.WriteLine("## 表E. ヨミ・ウツ・ヴェルの再測定（Q5。詳細は `tank phase0` の表P-2）");
        Console.WriteLine();
        Print1B();

        // --- 判定
        Console.WriteLine();
        Console.WriteLine("## 判定（§2-3）");
        Console.WriteLine();
        int q1 = 0, q2 = 0, q4n = 0;
        double q4sum = 0;
        foreach (UnitDef g in _grow)
        {
            if (AvgAlive(now[g.Id]) - AvgAlive(bare[g.Id]) >= 1.30) q1++;
            if (Slope(now[g.Id]) > 1.0) q2++;
            double d = Math.Abs(Win(now[g.Id]) - Win(g0[g.Id]));
            q4sum += d;
            if (d >= 5.0) q4n++;
        }
        double q4avg = q4sum / _grow.Length;
        int q5 = 0;
        foreach (Tk1BRow x in _1b) if (x.With && Math.Abs(x.Reach - x.PlainReach) > 0.05) q5++;

        Console.WriteLine("| | 内容 | 線 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|--:|:-:|");
        Console.WriteLine("| **Q1** | 味方全体の生存Tが素体比で +1.30T 以上（ゴルム超え） | 4台中 2台以上 | **"
            + q1 + " 台** | " + Mark(q1 >= 2) + " |");
        Console.WriteLine("| **Q2** | 傾きが 1.0 を超える | 1組以上 | **" + q2 + " 組** | " + Mark(q2 >= 1) + " |");
        Console.WriteLine("| **Q3** | 第三波（渇き）と第四波（軛）で帰属の符号が逆 | 4台中 2台以上 | **"
            + flip + " / 4 台** | " + Mark(flip >= 2) + " |");
        Console.WriteLine("| **Q4** | `Gain = 0` との帰属の差 | 4台の平均で 5pt 以上 | **" + F1(q4avg)
            + "pt**（5pt 以上の台 " + q4n + "/4） | " + Mark(q4avg >= 5.0) + " |");
        Console.WriteLine("| **Q5** | 供給者ありの台でヨミ・ウツ・ヴェルが 0.0pt でなくなる | 3枚中 2枚以上 | **"
            + q5 + " 枚** | " + Mark(q5 >= 2) + " |");
        Console.WriteLine("| **Q6** | `compare` 305 セル 0 件 | 1セルでも動けば事故 | `tank check` | — |");
        Console.WriteLine();
        Console.WriteLine("**Q1 の線（+1.30T）・Q3 と Q5 の台数・Q4 の集約（4台の平均）は `run` を回す前に固定した**（規約 (G16)）。");
        Console.WriteLine("指示書 §2-3 が台数を書いていない項（Q3・Q4）はこちらで決めてから測っている。");
    }

    static void RowA(string grow, string hold, List<TkRow> xs)
    {
        Console.WriteLine("| " + grow + " | " + hold + " | " + F1(Win(xs)) + "% | " + F2(AvgAlive(xs)) + " | "
            + F2(AvgHold(xs)) + " | " + CrossText(xs) + " | " + F1(NoCrossPct(xs)) + "% | "
            + F2(AvgFires(xs)) + " | " + F2(AvgToFoe(xs)) + " | " + F2(AvgToAlly(xs)) + " | **"
            + F2(Slope(xs)) + "** | " + F1(OkPct(xs)) + "% |");
    }

    static void RowB(string grow, string label, List<TkRow> xs, double baseWin, bool attrib)
    {
        Console.WriteLine("| " + grow + " | " + label + " | " + F1(Win(xs)) + "% | "
            + (attrib ? Signed(Win(xs) - baseWin) + "pt" : "—") + " | "
            + F2(AvgAlive(xs)) + " | " + F2(AvgHold(xs)) + " | " + F2(AvgFires(xs)) + " | "
            + F2(AvgGiven(xs)) + " | " + F2(Slope(xs)) + " |");
    }

    // ==================================================================================
    // check（§3 の自己検査）
    // ==================================================================================
    static void Check()
    {
        Console.WriteLine("# 第118期 —— 糧タンクの自己検査（`tank check`）");
        Console.WriteLine();

        // --- 必須1
        var builds = Presets.Compare;
        var cells = new List<string>();
        foreach ((string Name, Formation F) b in builds)
        {
            var row = new List<string>();
            foreach (EnemyCatalog.Stage st in Stages)
            {
                int w = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                row.Add(F1(w * 100.0 / 200) + "%");
            }
            cells.Add("| " + b.Name + " | " + string.Join(" | ", row) + " |");
        }
        int diff = -1;
        if (_root is not null)
        {
            var doc = new List<string>();
            foreach (string l in File.ReadAllLines(Path.Combine(_root, "docs", "balance.md")))
                if (l.StartsWith("| ", StringComparison.Ordinal) && l.Contains('%')) doc.Add(l);
            diff = Math.Abs(doc.Count - cells.Count);
            for (int i = 0; i < Math.Min(doc.Count, cells.Count); i++)
                if (doc[i].Trim() != cells[i].Trim()) diff++;
        }
        Console.WriteLine("- **必須1** `compare` " + builds.Length + " 行 × " + Stages.Count + " 波 ＝ "
            + (builds.Length * Stages.Count) + " セルを `docs/balance.md` と突き合わせ: **ずれ " + diff + " 行**"
            + (diff == 0 ? "（○）" : "（×）"));

        // --- 必須3
        Console.WriteLine("- **必須3** `NourishRule.Default` = `" + NourishRule.Default + "`（この期に足したノブ）"
            + " ／ `ReaderRule.Default` = `" + ReaderRule.Default + "`（第116期の 5 のまま）"
            + " ／ `BossRule.Default` = `" + BossRule.Default + "`（第117期のまま）");

        // --- 必須4
        int pick = 0;
        if (_root is not null)
            foreach (string f in Directory.GetFiles(Path.Combine(_root, "BattleCore"), "*.cs"))
                pick += System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"PickOne\(").Count;
        Console.WriteLine("- **必須4** `BattleCore` の `PickOne(` の出現: **" + pick + " 箇所**（第89期 (h) の 26 から"
            + (pick == 26 ? "動かしていない（○）" : "**動いている（×）**") + "）");

        // --- (a)
        // **第110期の教訓をそのまま踏む。** 盤面ルールは「保持者が盤上に生きている間だけ」効くので、
        // 「渇きの波で回復量が 0」は**原理的に到達不能な線**である（祭司を割った後は回復が戻る）。
        // 最初にこの形で書いて実際に × が出た（実測 1.8/戦）。**線のほうを直した。**
        // 正しい実証は**同数値・特性なしの祭司に差し替えた対照**との比。
        Console.WriteLine();
        Console.WriteLine("- **(a)** 自己回復が `ctx.Heal` を通っている（渇きが止める）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 盤面ルール | 回復量/戦 | 渇きを素体に差し替えた版 | 比 | 発火/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        bool aOk = true;
        foreach (int w in Waves)
        {
            List<TkRow> xs = RunWave(Bench(UnitCatalog.Mudo, _tank), w, UnitCatalog.Mudo.Id, _tank.Id, null, null, 50);
            bool drought = false;
            foreach ((int _, UnitDef d) in Stages[w].Enemy.Occupied())
                if (d.Traits.Contains(TraitId.Drought)) drought = true;
            double heal = AvgHealed(xs);
            string other = "—", ratio = "—";
            if (drought)
            {
                // 渇き持ちだけを同数値・特性なしに落とした波（**他の敵は1体も触らない**）。
                var f2 = new Formation();
                foreach ((int sl, UnitDef d) in Stages[w].Enemy.Occupied())
                    f2[sl] = d.Traits.Contains(TraitId.Drought) ? Plain(d) : d;
                var ys = new List<TkRow>();
                for (int seed = 0; seed < 50; seed++)
                    ys.Add(One(Bench(UnitCatalog.Mudo, _tank), f2, seed, UnitCatalog.Mudo.Id, _tank.Id, null, null));
                double h2 = AvgHealed(ys);
                other = F1(h2);
                ratio = h2 <= 0 ? "—" : F1(heal * 100.0 / h2) + "%";
                if (h2 <= 0 || heal * 10 >= h2) aOk = false;   // 渇き下は 1/10 未満であること
            }
            else if (heal <= 0) aOk = false;
            Console.WriteLine("| 第" + (w + 1) + "波 | " + (drought ? "**渇き**" : "—") + " | " + F1(heal)
                + " | " + other + " | " + ratio + " | " + F2(AvgFires(xs)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("  → " + (aOk ? "**○**（渇きの波では素体差し替え版の 1/10 未満・他の波は 0 でない）" : "**×**"));
        Console.WriteLine();
        Console.WriteLine("  **渇きの波でも 0 にはならない**——粛と同じで、**祭司を割った後は回復が戻る**"
            + "（第110期 Q4「盤面ルールの効きを波の集計で判定してはいけない」）。");

        // --- (b)
        // **除外が実際に立つ台で測る。** 主判定の台（ムド × タンク）には徴収も破片も毒も無いので、
        // そこで数えると内訳が全部 0 になり「除外が働いた」ことの証拠にならない。
        Console.WriteLine();
        Console.WriteLine("- **(b)** 糧が §3 の経路表どおり（1行ずつは `tank phase0` の表P-1）。ここでは収支と除外:");
        Console.WriteLine();
        {
            // 徴収（生贄・吸い）・破片・毒の出どころを1つの台に集める。
            UnitDef? sac = With(TraitId.Sacrifice);
            UnitDef? shatter = With(TraitId.Shatter);
            Formation f = Formation.Build(front1: _tank, front3: sac ?? _fill[0], center: _golm,
                                          back1: shatter ?? _fill[1], back3: _fill[0]);
            List<TkRow> xs = RunAll(f, _golm.Id, _tank.Id, seeds: 50);
            int fires = 0, given = 0, whet = 0, noSrc = 0, self = 0, levy = 0, dead = 0, soaked = 0;
            foreach (TkRow x in xs)
            {
                fires += x.Fires; given += x.Given; whet += x.WhetNourish; noSrc += x.NoSrc;
                self += x.SelfHit; levy += x.Levy; dead += x.Dead; soaked += x.Soaked;
            }
            int expect = fires * NourishRule.Default.Gain;
            Console.WriteLine("  - 台: " + _tank.Name + " / " + (sac?.Name ?? "—") + " / " + _golm.Name
                + " / " + (shatter?.Name ?? "—") + " / " + _fill[0].Name + "（生贄・吸い・破片を同席させた）");
            Console.WriteLine("  - 発火 " + fires + " 回 × `Gain` " + NourishRule.Default.Gain + " = " + expect
                + " 対 実際に渡した量 " + given + " → " + (expect == given ? "**一致（○）**" : "**ずれ（×）**"));
            Console.WriteLine("  - `NourishGiven` " + given + " 対 `WhetByRoute[糧]` " + whet + " → "
                + (given == whet ? "**一致（○）**——窓口を通っている" : "**ずれ（×）**"));
            Console.WriteLine("  - 発火しなかった内訳: 源無 " + noSrc + " / 自傷 " + self + " / **徴収 " + levy
                + "** / 相打ち " + dead + " / **破片 " + soaked + "**");
            Console.WriteLine("  - → " + Mark(expect == given && given == whet && levy > 0)
                + "（収支が合い、かつ**徴収の除外が実際に立っている**）");
        }

        // --- (c)
        Console.WriteLine();
        Console.WriteLine("- **(c)** `Gain = 0` の版が**素体と生存Tで一致しない**（回復は効いている）が、**育ちが 0**:");
        Console.WriteLine();
        Console.WriteLine("| 育つ | `Gain=0` の前1生存T | 素体の前1生存T | 差 | `Gain=0` の育ち量 | `Gain=0` の発火 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        bool cOk = true;
        foreach (UnitDef g in _grow)
        {
            List<TkRow> z = RunAll(Bench(g, _tank), g.Id, _tank.Id, nourish: new NourishRule(0), seeds: 50);
            List<TkRow> p = RunAll(Bench(g, _tankPlain), g.Id, _tankPlain.Id, seeds: 50);
            double dz = AvgHold(z) - AvgHold(p);
            double given = AvgGiven(z);
            if (Math.Abs(dz) < 0.01 || given != 0) cOk = false;
            Console.WriteLine("| " + g.Name + " | " + F2(AvgHold(z)) + " | " + F2(AvgHold(p)) + " | "
                + Signed2(dz) + " | " + F2(given) + " | " + F2(AvgFires(z)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("  → " + (cOk ? "**○**（生存Tは動くのに育ちは 0）" : "**×**"));

        // --- (d)
        {
            int fires = 0, given = 0;
            foreach (UnitDef g in _grow)
            {
                List<TkRow> xs = RunAll(Bench(g, _golm), g.Id, _golm.Id, seeds: 50);
                foreach (TkRow x in xs) { fires += x.Fires; given += x.Given; }
            }
            Console.WriteLine();
            Console.WriteLine("- **(d)** タンクを含まない台（ゴルム版4台 × 4波 × seed 0..49）で糧の発火が **"
                + fires + " 回** / 渡した量 **" + given + "**: " + Mark(fires == 0 && given == 0));
        }

        // --- (e)
        Console.WriteLine();
        Console.WriteLine("- **(e)** 実装からの走査: `Traits.cs` から **" + _body.Count + " 件**索けた"
            + "（0 件なら `tank` は表を1つも出さずに止まる。第117期の器具の事故への手当て）: " + Mark(_body.Count > 0));

        // --- (f)
        {
            int max = 0;
            bool ok = true;
            foreach (UnitDef g in _grow)
                foreach (int w in Waves)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = BattleEngine.Run(Bench(g, _tank), Stages[w].Enemy, seed,
                                                          verbose: false, boss: new BossRule(true));
                        foreach (KeyValuePair<string, UnitTally> kv in r.TallyByUnit)
                        {
                            int[] a = kv.Value.BossAtkByTurn ?? Array.Empty<int>();
                            foreach (int v in a) { if (v > max) max = v; if (v < 0) ok = false; }
                        }
                        if (r.Turns <= 0 || r.Turns > BattleEngine.MaxTurns) ok = false;
                    }
            Console.WriteLine();
            Console.WriteLine("- **(f)** 成長に上限を設けていないが、攻撃力の最大は **" + max
                + "**（負値なし・決着Tも範囲内）: " + Mark(ok));
        }

        // --- (g)
        {
            int d = 0, n = 0;
            foreach (UnitDef g in _grow)
                foreach (int w in Waves)
                    for (int seed = 0; seed < 25; seed++)
                    {
                        Formation f = Bench(g, _tank);
                        BattleResult a = BattleEngine.Run(f, Stages[w].Enemy, seed, verbose: false);
                        BattleResult c = BattleEngine.Run(f, Stages[w].Enemy, seed, verbose: false,
                                                          boss: new BossRule(true), nourish: NourishRule.Default);
                        n++;
                        if (a.PlayerWon != c.PlayerWon || a.Turns != c.Turns
                            || a.PlayerSurvivors != c.PlayerSurvivors) d++;
                    }
            Console.WriteLine();
            Console.WriteLine("- **(g)** `BossRule(true)` と既定の `NourishRule` を渡した版が、何も渡さない版と一致: "
                + "**ずれ " + d + " / " + n + " 戦**" + (d == 0 ? "（○）" : "（×）")
                + "（計数は盤面を1ビットも動かさない）");
        }
    }
}

/// <summary>
/// 糧タンク（第118期・<c>tank</c>）の1戦ぶんの記録。第117期の <c>BsRow</c> と同じ形で、
/// 門2（交差点）と糧の計数の列だけを足してある。
/// </summary>
sealed class TkRow
{
    /// <summary>決着ターンと勝敗。</summary>
    public int Turns;
    public bool Won;
    /// <summary>味方が最後にターン頭で生きていたターン（＝味方全体の生存T。門1）。</summary>
    public int PartyAlive;
    /// <summary>前1（タンク／ゴルム）の生存T ／ 受けた総ダメージ ／ 実際に戻った回復量。</summary>
    public int HoldAlive, HoldTaken, HoldHealed;
    /// <summary>中央（育つ側）の生存T と <c>CurrentAttack</c> の最大（到達点）。</summary>
    public int GrowAlive, GrowAtkMax;
    /// <summary>味方の与ダメ（前半3T ／ 後半3T）。</summary>
    public int DmgEarly, DmgLate;
    /// <summary>前半と後半が重ならない戦か。<b>傾きの分母はこれが真の戦だけ。</b></summary>
    public bool Ok;
    /// <summary>交差点（門2）。<b>0 は「一度も減らなかった」</b>で、平均に混ぜてはいけない。</summary>
    public int Cross;
    /// <summary>糧の計数（発火 ／ 渡した量 ／ 敵へ ／ 味方へ ／ 窓口を通った量）。</summary>
    public int Fires, Given, ToFoe, ToAlly, WhetNourish;
    /// <summary>発火しなかった内訳（破片で受け切り ／ 徴収 ／ 出どころなし ／ 自傷 ／ 相打ち）。</summary>
    public int Soaked, Levy, NoSrc, SelfHit, Dead;
    /// <summary>経路別の発火回数（<c>NourishPaths</c> の並び）。</summary>
    public int[] Path = Array.Empty<int>();
    /// <summary>糧で育った駒（<c>Def.Id</c> → 量）。</summary>
    public Dictionary<string, int> Fed = new();
}

/// <summary>§1-B（ヨミ・ウツ・ヴェルの再測定）の1行。</summary>
sealed class Tk1BRow
{
    public string Label = "";
    public string Input = "";
    public bool With;
    public string Supplier = "";
    /// <summary>到達点（<c>CurrentAttack</c> の最大）。<b>ヴェルの行だけは味方全体の生存T。</b></summary>
    public double Reach, PlainReach;
    public double Win, PlainWin;
}
