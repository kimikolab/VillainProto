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
    public static UnitDef Nochi(bool plain = false) => new()
    {
        Id = plain ? "nochi_plain" : "nochi",
        Name = plain ? "素体のノチ" : "後払いのノチ",
        MaxHp = NochiHp, Attack = NochiAtk, Speed = NochiSpd,
        Advances = false,
        Traits = plain ? Array.Empty<TraitId>() : new[] { TraitId.Ward, TraitId.Forfeit }
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
