using BattleCore;
using static Common;

// =====================================================================================
// ward モード（第153期） —— 預かり（回復に「時間」の次元を入れる）
//
// 指示書は design/PHASE153_WARD_SPEC.md。
//
//     dotnet run --project BattleSim -c Release 0 ward phase0   # Q0-2〜Q0-8（実装前に答える）
//     dotnet run --project BattleSim -c Release 0 ward scan     # 台の下見だけ（素体版の第2〜5波が 40〜95% か）
//     dotnet run --project BattleSim -c Release 0 ward scan probe  # 埋め草の強さを掃引して帯へ入れる
//     dotnet run --project BattleSim -c Release 0 ward run      # 段B（Burst 対 Drip）
//     dotnet run --project BattleSim -c Release 0 ward sweep    # 段C（Threshold / Drip の掃引）
//     dotnet run --project BattleSim -c Release 0 ward check    # 自己検査
// =====================================================================================

static class WardDiag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": if (arg.StartsWith("probe")) Probe(); else Scan(); return;
            case "run": Stage(); return;
            case "sweep": Sweep(arg); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("ward: モードは phase0 / scan / run / sweep / check。");
                return;
        }
    }

    // =================================================================================
    // 台（§4 段0）
    // =================================================================================

    /// <summary>
    /// 埋め草の強さ（段0 の掃引で決める）。<b>特性を1つも持たない素体</b>にする
    /// ——第143期の則（埋め草が敵の数値に触ると、掃引点がそのまま埋め草の性能になる）。
    ///
    /// <para><b>台ごとに別の点を採る。</b> 5台に同じ強さを当てると帯（40〜95%）に
    /// <b>同時に入る点が1つも無い</b>（`ward scan probe` の 24 点すべてで最大 3 台）——
    /// 役割の駒（ゴルム・リィカ・ガルド）の強さが違うので、1つの目盛りでは揃わない。
    /// <b>比べるのは同じ台の中の版どうし</b>（素体差し替え）なので、台をまたいで
    /// 埋め草を揃える必要は無い。</para>
    /// </summary>
    static UnitDef Filler(string id, int hp, int atk) => new()
    {
        Id = "w_" + id, Name = "素体" + id, MaxHp = hp, Attack = atk, Speed = 7,
        Advances = true, Traits = Array.Empty<TraitId>()
    };

    /// <summary>
    /// 後払いのノチ（仮名）。<b>この期は <c>UnitCatalog.All</c> に入れない</b>ので、
    /// 診断のローカルにしか存在しない（第138期の礫のガレと同じ扱い）。
    /// 数値は Q0-3 で数え直した中央値から決める。
    /// </summary>
    /// <param name="plain">特性を1つも持たない素体（第69期の標準器具の対照）。</param>
    /// <param name="noForfeit">
    /// <b>マイナスを外した版（<c>yP</c>）。</b> 札を2枚に切ってあるので、
    /// <c>Traits</c> の配列から <see cref="TraitId.Forfeit"/> を抜くだけで作れる
    /// ——これが2枚に分けた理由そのもの（第74期の作法・指示書 §1-1）。
    /// </param>
    public static UnitDef Nochi(bool plain = false, bool noForfeit = false) => new()
    {
        Id = plain ? "nochi_plain" : "nochi",
        Name = plain ? "素体のノチ" : noForfeit ? "後払いのノチ（預かりのみ）" : "後払いのノチ",
        MaxHp = NochiHp, Attack = NochiAtk, Speed = NochiSpd,
        Advances = false,
        Traits = plain ? Array.Empty<TraitId>()
               : noForfeit ? new[] { TraitId.Ward }
               : new[] { TraitId.Ward, TraitId.Forfeit }
    };

    public static int NochiHp = 58, NochiAtk = 4, NochiSpd = 6;

    /// <summary>台ごとの埋め草の強さ（攻, HP）。段0 の掃引で決めた。</summary>
    /// <remarks>
    /// 選び方は「帯（40〜95%）に入る点のうち、<b>情報セル（第2〜5波で 0 でも 100 でもない波の数）が
    /// 最大</b>の点」。台4 だけは情報セル 4 の点が1つも無いので 3 を採った
    /// ——ガルドが第3波（渇き）を必ず抜け、第5波が床に張り付くため。
    /// </remarks>
    static readonly (int Atk, int Hp)[] FillerOf = { (14, 110), (14, 130), (22, 110), (18, 90), (14, 90) };

    /// <summary>
    /// ローカル台（§4）。<b><c>Presets</c> には置かない</b>（`compare` 61行を汚さない）。
    /// ノチは全台とも <b>後1</b>（預かりは隣接を1つも読まないので、席は「殴られない後列」で固定する）。
    /// <b>台5 はノチを1枚も含まない陰性対照</b>（予測5）。
    /// </summary>
    public static (string Name, Formation F)[] Rigs(UnitDef nochi)
    {
        var r = new (string, Formation)[5];
        for (int i = 0; i < 5; i++) r[i] = Rig(i, nochi, FillerOf[i].Atk, FillerOf[i].Hp);
        return r;
    }

    static (string Name, Formation F) Rig(int i, UnitDef nochi, int atk, int hp)
    {
        UnitDef A() => Filler("a", hp, atk);
        UnitDef B() => Filler("b", hp, atk);
        UnitDef C() => Filler("c", hp, atk);
        UnitDef D() => Filler("d", hp, atk);
        return i switch
        {
            // 予測3: タンクと同席すると閾値到達が早まる（1枚に被弾が集中するので預かりが太る）。
            // **ゴルム（巨躯）を採る**——ガルドは `Stoic` で ctx.Heal が届かないので、
            // 預かりが積まれても1点も返らない（Q0-4 の答え）。台としては別に立てる。
            0 => ("台1 タンク (ノチ×ゴルム)",
                Formation.Build(front1: UnitCatalog.Golm, front3: A(),
                                center: B(), back1: nochi, back3: C())),

            // 対照: 肩代わりも無効化も無い。被弾が5枚に散る。
            1 => ("台2 タンク無し (ノチ単独)",
                Formation.Build(front1: A(), front3: B(),
                                center: C(), back1: nochi, back3: D())),

            // 予測4: 死軸（墓守リィカ）の行では預かりがそのまま敵の回復になる。
            2 => ("台3 死軸 (ノチ×リィカ)",
                Formation.Build(front1: A(), front3: B(),
                                center: C(), back1: nochi, back3: UnitCatalog.Rica)),

            // Q0-4 の答えを台にしたもの: `Stoic` のガルドに預けると返らない。
            3 => ("台4 支援拒否 (ノチ×ガルド)",
                Formation.Build(front1: UnitCatalog.Gald, front3: A(),
                                center: B(), back1: nochi, back3: C())),

            // 陰性対照（予測5）。**ノチを1枚も含まない。**
            _ => ("台5 陰性対照 (ノチ非在席)",
                Formation.Build(front1: UnitCatalog.Golm, front3: A(),
                                center: B(), back1: C(), back3: UnitCatalog.Rica)),
        };
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第153期 `ward phase0` —— 前提を実装から引き直す");
        Console.WriteLine();

        string? tr = FindSource("BattleCore", "Traits.cs");
        string? eng = FindSource("BattleCore", "BattleEngine.cs");
        if (tr is null || eng is null)
        {
            Console.WriteLine("**`BattleCore` のソースが引けない。止める**（第117期）。");
            return;
        }
        string[] T = File.ReadAllLines(tr), E = File.ReadAllLines(eng);

        Q03();
        Console.WriteLine();
        Q04(T);
        Console.WriteLine();
        Q06(E, T);
        Console.WriteLine();
        Q07(T);
        Console.WriteLine();
        Q08(E);
        Console.WriteLine();
        Scan();
    }

    /// <summary>Q0-3 —— 中央値を <c>UnitCatalog.All</c> の 52 枚から数え直す。</summary>
    static void Q03()
    {
        Console.WriteLine("## Q0-3 —— 現在の中央値（`UnitCatalog.All` の 52 枚から数え直す）");
        Console.WriteLine();
        var all = UnitCatalog.All;
        Console.WriteLine("枚数: **" + all.Count + "**");
        Console.WriteLine();
        Console.WriteLine("| 量 | 最小 | p25 | **中央値** | p75 | 最大 | 平均 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
        Row("攻撃力", all.Select(u => u.Attack).ToArray());
        Row("最大HP", all.Select(u => u.MaxHp).ToArray());
        Row("速さ", all.Select(u => u.Speed).ToArray());
        Console.WriteLine();
        Console.WriteLine("**ノチの仮値は 攻 " + NochiAtk + " / HP " + NochiHp + " / 速 " + NochiSpd + "。**");

        static void Row(string name, int[] v)
        {
            var s = v.OrderBy(x => x).ToArray();
            Console.WriteLine("| " + name + " | " + s[0] + " | " + Pct(s, 25) + " | **" + Pct(s, 50)
                              + "** | " + Pct(s, 75) + " | " + s[^1] + " | " + v.Average().ToString("F1") + " |");
        }
        static int Pct(int[] sorted, int p)
        {
            int i = (int)Math.Round((sorted.Length - 1) * p / 100.0, MidpointRounding.AwayFromZero);
            return sorted[Math.Clamp(i, 0, sorted.Length - 1)];
        }
    }

    /// <summary>Q0-4 —— 被弾を読む既存の札の全数（走査。手で並べない）。</summary>
    static void Q04(string[] T)
    {
        Console.WriteLine("## Q0-4 —— 被弾を読む既存の札（`Traits.cs` の走査）");
        Console.WriteLine();
        Console.WriteLine("| 札（クラス） | `OnDamaged` | `OnAllyDamaged` |");
        Console.WriteLine("|---|---|---|");

        string cls = "(宣言の外)";
        var map = new Dictionary<string, (bool Own, bool Ally)>();
        foreach (string line in T)
        {
            // 第130期の則: `class` だけを見ない（`record struct` も宣言として数える）。
            var m = System.Text.RegularExpressions.Regex.Match(
                line, @"^\s*(?:public |internal |)(?:abstract |sealed |readonly |static |partial )*(?:record struct|record class|record|struct|class|interface)\s+(\w+)");
            if (m.Success) cls = m.Groups[1].Value;
            if (line.Contains("override void OnDamaged("))
                map[cls] = (true, map.TryGetValue(cls, out var a0) && a0.Ally);
            if (line.Contains("override void OnAllyDamaged("))
                map[cls] = (map.TryGetValue(cls, out var a1) && a1.Own, true);
        }
        foreach (var kv in map.OrderBy(k => k.Key))
            Console.WriteLine("| `" + kv.Key + "` | " + (kv.Value.Own ? "○" : "") + " | " + (kv.Value.Ally ? "○" : "") + " |");
        Console.WriteLine();
        Console.WriteLine("合計 **" + map.Count(k => k.Value.Own) + "** 枚が `OnDamaged`・"
                          + "**" + map.Count(k => k.Value.Ally) + "** 枚が `OnAllyDamaged`。");
        Console.WriteLine();
        Console.WriteLine("**預かりが積まれるのは `OnAllyDamaged` の段**——`ApplyDamage` が");
        Console.WriteLine("`target.Hp -= amount` を走らせた**後**で、本人の `OnDamaged` が全部終わった後。");
        Console.WriteLine("**肩代わり（庇う・巨躯・分かち・後備え・棘守り）は宛先を差し替えるか別の");
        Console.WriteLine("`ApplyDamage` を呼ぶかのどちらか**なので、この段に来る `target` は");
        Console.WriteLine("**実際に HP が減った駒**である（指示書 Q0-4 の決定どおり）。");
    }

    /// <summary>Q0-6 —— 預かりを抱えた味方が倒れる経路。</summary>
    static void Q06(string[] E, string[] T)
    {
        Console.WriteLine("## Q0-6 —— 死亡の経路は1本か（`BattleCore` の走査）");
        Console.WriteLine();
        int handle = E.Count(l => l.Contains("HandleDeath(") && !l.Contains("private void"));
        Console.WriteLine("- `HandleDeath(` の**呼び出し**: **" + handle + "** 箇所（`BattleEngine.cs`）");
        Console.WriteLine();
        Console.WriteLine("| ファイル:行 | 式 | 0 に落ちうるか |");
        Console.WriteLine("|---|---|---|");
        foreach (var (name, lines) in new[] { ("BattleEngine.cs", E), ("Traits.cs", T) })
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim();
                if (s.StartsWith("//") || s.StartsWith("///")) continue;
                if (!System.Text.RegularExpressions.Regex.IsMatch(s, @"\.Hp\s*(-=|\+=|=)[^=]")) continue;
                string note = s.StartsWith("target.Hp -= amount") ? "**○（`ApplyDamage`）**"
                    : s.StartsWith("dead.Hp = 0") ? "—（`HandleDeath` の中）"
                    : s.StartsWith("self.Hp -= paid") ? "×（`Hp <= 1` で返り、`amount ≤ Hp − 1`）"
                    : s.StartsWith("if (self.IsAlive) self.Hp = self.MaxHp") ? "×（満タンへ戻す・不死の器具）"
                    : s.Contains("Math.Max") || s.Contains("Math.Min") ? "×（クランプ）"
                    : "要確認";
                Console.WriteLine("| " + name + ":" + (i + 1) + " | `" + s + "` | " + note + " |");
            }
        Console.WriteLine();
        Console.WriteLine("**死は `ApplyDamage` の1本だけ**（毒・燃焼のティックも自傷も吸いも置き去りの削りも");
        Console.WriteLine("全部この窓口を通る）。だから `OnAllyDeath` は**全経路で走る**——没収は1箇所で足りる。");
    }

    /// <summary>Q0-7 —— 同型を過去に測っていないか。</summary>
    static void Q07(string[] T)
    {
        Console.WriteLine("## Q0-7 —— 名前と窓口の衝突");
        Console.WriteLine();
        foreach (string k in new[] { "RegenTrait", "class WardTrait", "class ForfeitTrait", "TraitId.Ward", "TraitId.Forfeit", "StatusKeys.Ward" })
            Console.WriteLine("- `" + k + "`: `Traits.cs` に **" + T.Count(l => l.Contains(k)) + "** 行");
        Console.WriteLine();
        string? design = FindSource("design", "PHASE118_TANK.md");
        Console.WriteLine(design is null ? "- `design/PHASE118_TANK.md`: **引けない**"
                                         : "- `design/PHASE118_TANK.md`: " + File.ReadAllLines(design).Length + " 行");
    }

    /// <summary>Q0-8 —— 状態キーを1本足すコスト。</summary>
    static void Q08(string[] E)
    {
        Console.WriteLine("## Q0-8 —— 状態キーを1本足すと、どこが自動で増えるか");
        Console.WriteLine();
        Console.WriteLine("- `StatusKeys.All` の現在の本数: **" + StatusKeys.All.Length + "**（"
                          + string.Join(" / ", StatusKeys.All.Select(StatusKeys.LabelOf)) + "）");
        Console.WriteLine("- `StatusLabels`（台本のスナップショット）は `StatusKeys.All` を列挙して作る"
                          + "（第97期 D4）ので**手で足す箇所は無い**");
        Console.WriteLine("- `NoteStatusGain` の `switch`: **" + E.Count(l => l.Contains("case StatusKeys.")) + "** 本"
                          + "（**未知のキーは既定で落ちる**ので足さなくても落ちない。`Stagger` が先例）");
        Console.WriteLine("- `UnitTally.CarryKeys`: **" + UnitTally.CarryKeys.Length + "** 本"
                          + "（**足さない**——増やすと過去の期の帳簿の添字が動く。第147期の混乱と同じ判断）");
        Console.WriteLine("- `Engagement` の境界: `StatusKeys.All` を回すので、**`All` に足せば会戦で消える**");
    }

    // =================================================================================
    // 段0 —— 台の下見
    // =================================================================================

    static void Scan()
    {
        Console.WriteLine("## 段0 —— 台の下見（**素体のノチ**・seed 0.." + (Seeds - 1) + "）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 埋め草（攻/HP） | 勝率（1..5波） | 第2〜5波平均 | 帯（40〜95%） | 情報セル（第2〜5波） |");
        Console.WriteLine("|---|---|---|---:|---|---:|");
        var rigs = Rigs(Nochi(plain: true));
        for (int i = 0; i < rigs.Length; i++)
        {
            double[] w = Waves(rigs[i].F);
            double m = (w[1] + w[2] + w[3] + w[4]) / 4;
            int info = w.Skip(1).Count(x => x > 0.0 && x < 100.0);
            Console.WriteLine("| " + rigs[i].Name + " | " + FillerOf[i].Atk + " / " + FillerOf[i].Hp
                              + " | " + string.Join(" / ", w.Select(x => x.ToString("F1")))
                              + " | " + m.ToString("F1") + " | " + (m >= 40 && m <= 95 ? "○" : "**×**")
                              + " | " + info + " |");
        }
    }

    /// <summary>埋め草の強さの掃引（§4 段0・第152期 段0 と同じ手順）。<b>台ごとに点を選ぶ。</b></summary>
    static void Probe()
    {
        Console.WriteLine("## 段0 —— 埋め草の掃引（**素体のノチ**・台ごとに帯 40〜95% の点を探す）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 攻 | HP | 勝率（1..5波） | 第2〜5波平均 | 帯 | 情報セル |");
        Console.WriteLine("|---|---:|---:|---|---:|---|---:|");
        for (int i = 0; i < 5; i++)
            foreach (int atk in new[] { 10, 14, 18, 22, 26, 30 })
                foreach (int hp in new[] { 70, 90, 110, 130 })
                {
                    (string name, Formation f) = Rig(i, Nochi(plain: true), atk, hp);
                    double[] w = Waves(f);
                    double m = (w[1] + w[2] + w[3] + w[4]) / 4;
                    int info = w.Skip(1).Count(x => x > 0.0 && x < 100.0);
                    Console.WriteLine("| " + name + " | " + atk + " | " + hp + " | "
                                      + string.Join(" / ", w.Select(x => x.ToString("F1")))
                                      + " | " + m.ToString("F1") + " | " + (m >= 40 && m <= 95 ? "○" : "×")
                                      + " | " + info + " |");
                }
    }

    // =================================================================================
    // 版（段B）
    // =================================================================================

    /// <summary>素体差し替えの対照（第69期の標準器具）。<b>この期の線はこの期の対照で引く</b>（第118期 §6-3 の申し送り1）。</summary>
    static (string Tag, UnitDef Nochi, WardRule Rule)[] Versions() => new[]
    {
        ("V0 素体", Nochi(plain: true), WardRule.Default),
        ("V1 Burst", Nochi(), WardRule.Default),
        ("V1p Burst 預かりのみ", Nochi(noForfeit: true), WardRule.Default),
        ("V2 Drip",  Nochi(), WardRule.Default with { Return = WardReturn.Drip }),
        ("V2p Drip 預かりのみ", Nochi(noForfeit: true), WardRule.Default with { Return = WardReturn.Drip }),
    };

    sealed class WdAcc
    {
        public long Battles, Wins, Turns, Life, Stacked, Released, Forfeited, Residual;
        public long Bursts, Drips, Dry, DryDrought, DryStoic, Forfeits, ForfeitHealed, Unbalanced;
        public double[] WaveWin = new double[5];
    }

    static WdAcc Measure(Formation f, WardRule rule, int stFrom = 1, int stTo = 5)
    {
        var a = new WdAcc();
        for (int st = stFrom; st < stTo; st++)
        {
            long w = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, ward: rule);
                a.Battles++; a.Turns += r.Turns;
                if (r.PlayerWon) { a.Wins++; w++; }
                foreach (var o in f.Occupied())
                    if (r.TallyByUnit.TryGetValue(o.Def.Id, out UnitTally? t)) a.Life += t.LastActiveTurn;
                WardLedger L = r.Ward;
                a.Stacked += L.Stacked; a.Released += L.Released; a.Forfeited += L.Forfeited;
                a.Residual += L.Residual; a.Bursts += L.Bursts; a.Drips += L.Drips;
                a.Dry += L.Dry; a.DryDrought += L.DryDrought; a.DryStoic += L.DryStoic;
                a.Forfeits += L.Forfeits; a.ForfeitHealed += L.ForfeitHealed;
                if (!L.Balanced) a.Unbalanced++;
            }
            a.WaveWin[st] = 100.0 * w / Seeds;
        }
        return a;
    }

    /// <summary>段B —— Burst と Drip を**同じ台・同じ seed** で並べる。</summary>
    static void Stage()
    {
        Console.WriteLine("# 第153期 段B —— `Burst` 対 `Drip`（同じ台・同じ seed 0.." + (Seeds - 1) + "・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`帰属` は素体差し替え比（版 − V0）。`取り分` は Δ ÷ (100 − V0)（§9-1）。");
        Console.WriteLine("`味方生存T` は編成5枚の `LastActiveTurn` の平均（1戦・1枚あたり）。");
        Console.WriteLine();

        var rigs = Rigs(Nochi());
        for (int i = 0; i < rigs.Length; i++)
        {
            Console.WriteLine("## " + rigs[i].Name);
            Console.WriteLine();
            Console.WriteLine("| 版 | 勝率 2/3/4/5波 | 第2〜5波平均 | 帰属 | 取り分 | 決着T | 味方生存T | 生存T差 | 生存T÷決着T | 比の差 |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            double baseM = 0, baseLife = 0, baseRatio = 0;
            var accs = new List<(string Tag, WdAcc A)>();
            foreach ((string tag, UnitDef nochi, WardRule rule) in Versions())
            {
                Formation f = Rig(i, nochi, FillerOf[i].Atk, FillerOf[i].Hp).F;
                WdAcc a = Measure(f, rule);
                accs.Add((tag, a));
                double m = 100.0 * a.Wins / a.Battles;
                double life = (double)a.Life / a.Battles / 5.0;
                double ratio = life / ((double)a.Turns / a.Battles);
                if (tag.StartsWith("V0")) { baseM = m; baseLife = life; baseRatio = ratio; }
                string share = tag.StartsWith("V0") ? "—"
                    : baseM >= 100.0 ? "—" : ((m - baseM) / (100 - baseM)).ToString("F3");
                Console.WriteLine("| " + tag + " | "
                    + string.Join(" / ", a.WaveWin.Skip(1).Select(x => x.ToString("F1"))) + " | "
                    + m.ToString("F1") + " | " + (tag.StartsWith("V0") ? "—" : (m - baseM).ToString("+0.0;-0.0;0.0"))
                    + " | " + share + " | " + ((double)a.Turns / a.Battles).ToString("F2")
                    + " | " + life.ToString("F2")
                    + " | " + (tag.StartsWith("V0") ? "—" : (life - baseLife).ToString("+0.00;-0.00;0.00"))
                    + " | " + ratio.ToString("F3")
                    + " | " + (tag.StartsWith("V0") ? "—" : (ratio - baseRatio).ToString("+0.000;-0.000;0.000")) + " |");
            }
            Console.WriteLine();
            Console.WriteLine("| 版 | 積んだ | 返した | 没収 | 残額 | 収支 | 返却の試行 | 空振り | 渇き | 支援拒否 | 没収発火 | 敵の回復 |");
            Console.WriteLine("|---|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|");
            foreach ((string tag, WdAcc a) in accs)
            {
                double b = a.Battles;
                Console.WriteLine("| " + tag + " | " + (a.Stacked / b).ToString("F2") + " | " + (a.Released / b).ToString("F2")
                    + " | " + (a.Forfeited / b).ToString("F2") + " | " + (a.Residual / b).ToString("F2")
                    + " | " + (a.Unbalanced == 0 ? "**閉じる**" : "**ずれ " + a.Unbalanced + " 戦**")
                    + " | " + ((a.Bursts + a.Drips) / b).ToString("F2") + " | " + (a.Dry / b).ToString("F2")
                    + " | " + (a.DryDrought / b).ToString("F2") + " | " + (a.DryStoic / b).ToString("F2")
                    + " | " + (a.Forfeits / b).ToString("F2") + " | " + (a.ForfeitHealed / b).ToString("F2") + " |");
            }
            Console.WriteLine();
        }
    }

    // =================================================================================
    // 段C —— 掃引
    // =================================================================================

    static void Sweep(string arg)
    {
        bool pct = arg.StartsWith("percent");
        Console.WriteLine(pct
            ? "# 第153期 段C-b —— `Percent` の掃引（**予測6 の確認**）"
            : "# 第153期 段C —— `Threshold`（Burst）と `Drip` の掃引（**返した量を揃えた点で比べる**）");
        Console.WriteLine();

        var rigs = Rigs(Nochi());
        var plainRigs = Rigs(Nochi(plain: true));
        for (int i = 0; i < rigs.Length - 1; i++)   // 台5（陰性対照）は掃引しない
        {
            WdAcc v0 = Measure(plainRigs[i].F, WardRule.Default);
            double baseM = 100.0 * v0.Wins / v0.Battles;
            double baseLife = (double)v0.Life / v0.Battles / 5.0;
            Console.WriteLine("## " + rigs[i].Name + "（V0 素体 " + baseM.ToString("F1") + "% / 生存T "
                              + baseLife.ToString("F2") + "）");
            Console.WriteLine();
            Console.WriteLine("| 版 | ノブ | 勝率 | 帰属 | 生存T差 | 積んだ | **返した** | 没収 | 残額 | 返却率 |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");

            var points = new List<(string Tag, WardRule R)>();
            if (pct)
                foreach (int pp in new[] { 25, 50, 75, 100 })
                    points.Add(("Burst", WardRule.Default with { Percent = pp }));
            else
            {
                foreach (int th in new[] { 10, 20, 30, 40, 60, 90, 130 })
                    points.Add(("Burst", WardRule.Default with { Threshold = th }));
                foreach (int dr in new[] { 2, 4, 6, 8, 12, 18, 26 })
                    points.Add(("Drip", WardRule.Default with { Return = WardReturn.Drip, Drip = dr }));
            }

            foreach ((string tag, WardRule r) in points)
            {
                WdAcc a = Measure(rigs[i].F, r);
                double m = 100.0 * a.Wins / a.Battles;
                double life = (double)a.Life / a.Battles / 5.0;
                string knob = pct ? "Percent " + r.Percent
                    : r.Return == WardReturn.Burst ? "Threshold " + r.Threshold : "Drip " + r.Drip;
                Console.WriteLine("| " + tag + " | " + knob + " | " + m.ToString("F1") + " | "
                    + (m - baseM).ToString("+0.0;-0.0;0.0") + " | " + (life - baseLife).ToString("+0.00;-0.00;0.00")
                    + " | " + ((double)a.Stacked / a.Battles).ToString("F2")
                    + " | **" + ((double)a.Released / a.Battles).ToString("F2") + "**"
                    + " | " + ((double)a.Forfeited / a.Battles).ToString("F2")
                    + " | " + ((double)a.Residual / a.Battles).ToString("F2")
                    + " | " + (a.Stacked > 0 ? (100.0 * a.Released / a.Stacked).ToString("F1") + "%" : "—") + " |");
            }
            Console.WriteLine();
        }
    }

    // =================================================================================
    // 自己検査（§7）
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第153期 `ward check` —— 自己検査");
        Console.WriteLine();

        string bal = arg.Trim().Length > 0 ? arg.Trim() : "docs/balance.md";
        Console.WriteLine("## (a) `compare` 305 セルが `" + bal + "` と 0 件");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**`" + bal + "` が無い。手で `compare` を回して差分を見ること。**");
        else
        {
            int cells = 0;
            foreach (string line in File.ReadAllLines(bal).Where(l => l.StartsWith("| ") && l.Contains("%")))
                cells += line.Split('|').Select(c => c.Trim()).Count(c => c.EndsWith("%"));
            Console.WriteLine("`" + bal + "` の `%` セルは **" + cells + "** 個。"
                              + "突き合わせは `0 compare > /tmp/x && diff docs/balance.md /tmp/x` で行う"
                              + "——**ノチは `UnitCatalog.All` にも `Presets` にも無いので、"
                              + "差分が出たら実装が既定を動かしている。**");
        }
        Console.WriteLine();

        string? eng = FindSource("BattleCore", "BattleEngine.cs");
        string? tr = FindSource("BattleCore", "Traits.cs");
        if (eng is not null && tr is not null)
        {
            int n = File.ReadAllLines(eng).Count(l => l.Contains("PickOne("))
                  + File.ReadAllLines(tr).Count(l => l.Contains("PickOne("));
            Console.WriteLine("## (b) `ctx.PickOne` を新たに使っていない");
            Console.WriteLine();
            Console.WriteLine("`BattleCore` の `PickOne(` は **" + n + "** 行（第153期の基準は 27）。"
                              + (n == 27 ? "**一致**" : "**要確認**"));
            Console.WriteLine();
        }

        var rigs = Rigs(Nochi());

        Console.WriteLine("## (c) 収支が閉じる（積んだ ＝ 返した ＋ 没収 ＋ 残額）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 戦数 | 積んだ | 返した＋没収＋残額 | ずれた戦 |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|");
        long badTotal = 0;
        for (int i = 0; i < rigs.Length; i++)
            foreach ((string tag, UnitDef nochi, WardRule rule) in Versions())
            {
                if (tag.StartsWith("V0")) continue;
                WdAcc a = Measure(Rig(i, nochi, FillerOf[i].Atk, FillerOf[i].Hp).F, rule);
                badTotal += a.Unbalanced;
                Console.WriteLine("| " + rigs[i].Name + " | " + tag + " | " + a.Battles + " | " + a.Stacked
                    + " | " + (a.Released + a.Forfeited + a.Residual) + " | " + a.Unbalanced + " |");
            }
        Console.WriteLine();
        Console.WriteLine(badTotal == 0 ? "**1点もずれていない。**" : "**ずれている。止める。**");
        Console.WriteLine();

        Console.WriteLine("## (d) `StatusKeys.All` に `Ward` が入っている（会戦の境界で消える）");
        Console.WriteLine();
        Console.WriteLine("- `StatusKeys.All`: "
                          + (StatusKeys.All.Contains(StatusKeys.Ward) ? "**入っている**" : "**入っていない。止める**")
                          + "（" + StatusKeys.All.Length + " 本）");
        {
            // 会戦を2戦またぐ。境界が消していなければ2戦目は「積んでいない預かりを返す」ので、
            // 収支（積んだ ＝ 返した ＋ 没収 ＋ 残額）が必ず破れる。
            var squads = new[] { rigs[0].F, rigs[1].F };
            var foes = EnemyCatalog.Stages.Skip(1).Take(3).Select(x => x.Enemy).ToList();
            int battles = 0, bad = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                EngagementResult er = EngagementEngine.Run(squads, foes, seed, verbose: false);
                foreach (BattleResult r in er.Battles) { battles++; if (!r.Ward.Balanced) bad++; }
            }
            Console.WriteLine("- 会戦 40 試行の部隊戦 **" + battles + "** 戦で、収支が破れた戦: **" + bad + "**"
                              + (bad == 0 ? "（**境界で消えている**）" : "（**止める**）"));
        }
        Console.WriteLine();

        Console.WriteLine("## (f) ノチ非在席で版が1ビットも違わない");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Versions().Select(v => v.Tag)) + " | 差 |");
        Console.WriteLine("|---|" + string.Concat(Versions().Select(_ => "---|")) + "---|");
        {
            Formation f = rigs[^1].F;   // 台5（陰性対照）
            var ws = Versions().Select(v => Measure(f, v.Rule).WaveWin).ToList();
            bool same = ws.All(w => Enumerable.Range(1, 4).All(st => w[st] == ws[0][st]));
            Console.WriteLine("| " + rigs[^1].Name + " | "
                + string.Join(" | ", ws.Select(w => string.Join(" / ", w.Skip(1).Select(x => x.ToString("F1")))))
                + " | " + (same ? "**0 件**" : "**差あり。止める**") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## (g) 渇き下で返った量が 0（`Heal` の入口で止まっているか）");
        Console.WriteLine();
        Console.WriteLine("第三波（渇きの祭司）だけを回す。**祭司が生きている間**は1点も返らないが、");
        Console.WriteLine("**祭司を割れば返る**ので戦全体では 0 にならない。読むのは `うち渇き` の側。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 返した | 空振り | うち渇き | うち支援拒否 |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|");
        for (int i = 0; i < rigs.Length - 1; i++)
            foreach ((string tag, UnitDef nochi, WardRule rule) in Versions())
            {
                if (tag.StartsWith("V0")) continue;
                WdAcc a = Measure(Rig(i, nochi, FillerOf[i].Atk, FillerOf[i].Hp).F, rule, stFrom: 2, stTo: 3);
                Console.WriteLine("| " + rigs[i].Name + " | " + tag + " | "
                    + ((double)a.Released / a.Battles).ToString("F2") + " | " + ((double)a.Dry / a.Battles).ToString("F2")
                    + " | " + ((double)a.DryDrought / a.Battles).ToString("F2")
                    + " | " + ((double)a.DryStoic / a.Battles).ToString("F2") + " |");
            }
    }

    static double[] Waves(Formation f)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
            w[st] = 100.0 * wins / Seeds;
        }
        return w;
    }

    static string? FindSource(string dirName, string leaf)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            string p = Path.Combine(dir.FullName, dirName, leaf);
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
