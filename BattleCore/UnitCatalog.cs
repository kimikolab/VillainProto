namespace BattleCore;

/// <summary>
/// 主人公に押し付けられた「使えない」駒たち。
/// 単体性能では勝てないように意図的に調整してある。
/// </summary>
public static class UnitCatalog
{
    public static readonly UnitDef Borg = new()
    {
        Id = "borg",
        Name = "焼け残りのボルグ",
        MaxHp = 60,
        Attack = 18,
        Speed = 8,
        Traits = new[] { TraitId.Splash, TraitId.Cinder },
        Pattern = AttackPattern.Sweep,
        PlusText = "火力が高く、薙ぎ払いが敵の両隣にも届く。斬った相手に燃焼を移す",
        MinusText = "同じ一振りが、自分の両隣の味方も巻き込み、隣の味方にも火が移る",
        Flavor = "三度、味方の部隊を半壊させて追い出された。"
    };

    public static readonly UnitDef Mudo = new()
    {
        Id = "mudo",
        Name = "泥人形ムド",
        MaxHp = 80,
        Attack = 3,
        Speed = 5,
        Traits = new[] { TraitId.Rage },
        PlusText = "受けたダメージに応じて攻撃力が上がる",
        MinusText = "素の攻撃力がほぼ無い",
        Flavor = "殴られないと働かないので、誰も連れて行きたがらない。"
    };

    public static readonly UnitDef Sero = new()
    {
        Id = "sero",
        Name = "逃亡兵セロ",
        MaxHp = 42,
        Attack = 11,
        Speed = 12,
        Traits = new[] { TraitId.Sniper, TraitId.Coward },
        PlusText = "戦闘中に一度後退してから後列にいると攻撃力2倍になり、敵の後列を狙い撃つ貫きに変わる（最初から後列に置いても発動しない）",
        MinusText = "3分の1削られると後列の味方を突き飛ばして逃げる（味方が矢面に立つ）",
        Flavor = "敵前逃亡二回。三度目は無いと言われて処分場に送られた。"
    };

    public static readonly UnitDef Nel = new()
    {
        Id = "nel",
        Name = "呪詛官ネル",
        MaxHp = 45,
        Attack = 7,
        Speed = 9,
        Traits = new[] { TraitId.Curse },
        PlusText = "戦闘開始時、敵全体の攻撃力を下げる",
        MinusText = "呪詛が味方全体にも漏れる",
        Flavor = "効果は本物。ただし味方の被害が計算に合わないとされた。"
    };

    public static readonly UnitDef Gald = new()
    {
        Id = "gald",
        Name = "廃棄聖騎士ガルド",
        MaxHp = 100,
        Attack = 9,
        Speed = 4,
        Traits = new[] { TraitId.Guardian, TraitId.Stoic },
        PlusText = "味方への攻撃を肩代わりし、その傷のぶん強くなる / 呪いや弱体を受け付けない",
        MinusText = "回復も強化も一切受け付けない",
        Flavor = "誓約が壊れていて、もう誰の助けも届かない。"
    };

    public static readonly UnitDef Rica = new()
    {
        Id = "rica",
        Name = "墓守リィカ",
        MaxHp = 55,
        Attack = 5,
        Speed = 7,
        Traits = new[] { TraitId.Necro, TraitId.Sacrifice },
        PlusText = "味方が倒れるたび累積で強化される（層は毎ターン1つ薄れる）／3層以上で攻撃が薙ぎに変わる",
        MinusText = "戦闘開始時に隣接する味方を削る",
        Flavor = "味方の死を待っている顔をする、と report に書かれた。"
    };

    public static readonly UnitDef Golm = new()
    {
        Id = "golm",
        Name = "大喰らいゴルム",
        MaxHp = 150,
        Attack = 10,
        Speed = 3,
        Traits = new[] { TraitId.Colossus, TraitId.Drain },
        PlusText = "圧倒的な耐久",
        MinusText = "毎ターン味方から精気を吸う",
        Flavor = "維持費が高すぎる。連れて行くと部隊が保たない。"
    };

    public static readonly UnitDef Mug = new()
    {
        Id = "mug",
        Name = "胞子体ムグ",
        MaxHp = 38,
        Attack = 6,
        Speed = 6,
        Traits = new[] { TraitId.Splitter },
        PlusText = "倒れると胞子が2体湧く",
        MinusText = "本体は脆く、火力もほぼ無い",
        Flavor = "掃除の手間が増えるという理由で焼却処分が決まっていた。"
    };

    public static readonly UnitDef Zoto = new()
    {
        Id = "zoto",
        Name = "爆ぜるゾト",
        MaxHp = 34,
        Attack = 5,
        Speed = 7,
        Traits = new[] { TraitId.Bomber },
        PlusText = "倒れたとき破裂し、敵全体に大ダメージ",
        MinusText = "破裂は味方も巻き込む。生きている間はほぼ無力",
        Flavor = "一度しか使えない駒を編成に入れる指揮官はいない。"
    };

    public static readonly UnitDef Vel = new()
    {
        Id = "vel",
        Name = "継ぎ接ぎのヴェル",
        MaxHp = 46,
        Attack = 6,
        Speed = 8,
        Traits = new[] { TraitId.Reviver },
        PlusText = "倒れた味方を戦線に戻す（2回まで）",
        MinusText = "1回縫うごとに自分の最大HPが半分になる",
        Flavor = "縫い直された者はもう元の者ではない、と嫌われた。"
    };

    public static readonly UnitDef Sid = new()
    {
        Id = "sid",
        Name = "毒吐きのスィド",
        MaxHp = 84,
        Attack = 4,
        Speed = 9,
        Traits = new[] { TraitId.Venom },
        PlusText = "殴られると、殴ってきた相手に毒を積む（毒は毎ターン層の分だけ削る）",
        MinusText = "自分からは毒を積めない。毒が隣接する味方にも漏れる。攻撃力もほぼ無い",
        Flavor = "袋が破れるまで役に立たない。誰も隣に立ちたがらない。"
    };

    public static readonly UnitDef Kado = new()
    {
        Id = "kado",
        Name = "棘鎧のカド",
        MaxHp = 96,
        Attack = 11,
        Speed = 4,
        Traits = new[] { TraitId.Thorns, TraitId.Immobile, TraitId.Havoc },
        PlusText = "殴られると、自分の攻撃力の2倍を敵に返す。反撃は隣の敵にも届き、巻き込んだ味方のダメージ分だけ自分の攻撃力が上がる",
        MinusText = "自分からは決して攻撃しない / 反撃が隣の味方も巻き込む / 味方全体の受けるダメージが5割増える",
        Flavor = "命令しても動かない。そばにいる者の傷がなぜか深くなる。"
    };

    public static readonly UnitDef Hisa = new()
    {
        Id = "hisa",
        Name = "囃し立てのヒサ",
        MaxHp = 44,
        Attack = 2,
        Speed = 10,
        Traits = new[] { TraitId.Marker },
        PlusText = "隣接する味方1体に敵の攻撃を集中させる",
        MinusText = "自分では何もできない。押し出された味方は普通は死ぬ",
        Flavor = "味方を矢面に立たせて生き延びた男。誰も隣に立ちたがらない。"
    };

    public static readonly UnitDef Nono = new()
    {
        Id = "nono",
        Name = "継ぎ当てのノノ",
        MaxHp = 78,
        Attack = 3,
        Speed = 6,
        Traits = new[] { TraitId.Mender },
        PlusText = "毎ターン、最も傷ついた味方を繕う",
        MinusText = "繕った分だけ自分が減る。支える相手が多いほど早く尽きる",
        Flavor = "自分の身を削ることをやめられず、隊の資産を食い潰した。"
    };

    public static readonly UnitDef Mio = new()
    {
        Id = "mio",
        Name = "澱みのミオ",
        MaxHp = 42,
        Attack = 2,
        Speed = 8,
        Traits = new[] { TraitId.Amplifier },
        PlusText = "毎ターン、敵に積まれた毒を濃くする（+4層）",
        MinusText = "毒が積まれていなければ完全に無意味",
        Flavor = "水を濁らせることしかできない。それ単体では兵器にならない。"
    };

    public static readonly UnitDef Rau = new()
    {
        Id = "rau",
        Name = "疫みのラウ",
        MaxHp = 50,
        Attack = 5,
        Speed = 7,
        Traits = new[] { TraitId.Contagion },
        PlusText = "毒に侵された駒が倒れると、残りの敵へ毒が飛ぶ（味方の死骸からも飛ぶ）",
        MinusText = "自分では毒を与えられない",
        Flavor = "死体を運ばせると必ず疫病が出るので、隊列から外された。"
    };

    public static readonly UnitDef Guza = new()
    {
        Id = "guza",
        Name = "瘴気袋のグザ",
        MaxHp = 58,
        Attack = 2,
        Speed = 5,
        Traits = new[] { TraitId.Miasma },
        PlusText = "毎ターン、敵全体へ薄く毒を撒く",
        MinusText = "瘴気は味方にも及ぶ（味方全体に毒+1）。攻撃力もほぼ無い",
        Flavor = "近くにいるだけで具合が悪くなるので、天幕にすら入れてもらえない。"
    };

    public static readonly UnitDef Tou = new()
    {
        Id = "tou",
        Name = "痺れ粉のトウ",
        MaxHp = 46,
        Attack = 3,
        Speed = 11,
        Traits = new[] { TraitId.Paralyze },
        PlusText = "攻撃した相手を高確率で1ターン動けなくする",
        MinusText = "自分の火力はほぼ無い。粉が尽きれば何も残らない",
        Flavor = "自分の粉で味方を眠らせた前科がある。"
    };

    public static readonly UnitDef Beni = new()
    {
        Id = "beni",
        Name = "毒喰らいのベニ",
        MaxHp = 64,
        Attack = 4,
        Speed = 6,
        Traits = new[] { TraitId.Devour },
        PlusText = "毒に侵された敵の数だけ味方全体を癒す",
        MinusText = "毒が積まれていなければ何もしない。味方が負った毒は2倍に効く",
        Flavor = "戦場の澱みを啜って生きている。同席したい者はいない。"
    };

    public static readonly UnitDef Gan = new()
    {
        Id = "gan",
        Name = "鬨の号令ガン",
        MaxHp = 52,
        Attack = 4,
        Speed = 9,
        Traits = new[] { TraitId.Rally },
        PlusText = "開戦時に味方全体+4 / 前のターンに動かなかった味方を+8",
        MinusText = "自分の火力はほぼ無い。全員が働く編成では無意味",
        Flavor = "号令だけは達者だが、自分では槍一本まともに振れない。"
    };

    public static readonly UnitDef Vio = new()
    {
        Id = "vio",
        Name = "澱み喰いのヴィオ",
        MaxHp = 58,
        Attack = 6,
        Speed = 7,
        Traits = new[] { TraitId.Blightfed },
        PlusText = "味方が負った毒を吸い取り、その層の分だけ攻撃力が上がる",
        MinusText = "味方が汚れていなければただの穀潰し",
        Flavor = "仲間の膿を舐めて回る。治るのは事実だが、誰も礼を言わない。"
    };

    public static readonly UnitDef Yomi = new()
    {
        Id = "yomi",
        Name = "軋みのヨミ",
        MaxHp = 92,
        Attack = 6,
        Speed = 5,
        Traits = new[] { TraitId.Displaced },
        PlusText = "隊列を動かされるたび攻撃力が上がり、その場で割り込んで攻撃する。前へ突き出されると上昇が特に大きい",
        MinusText = "自分では動かない。誰も乱してくれなければ置物",
        Flavor = "どこに置いても文句を言わない。だから誰も気に留めなかった。"
    };

    public static readonly UnitDef Basa = new()
    {
        Id = "basa",
        Name = "喧噪のバサ",
        MaxHp = 56,
        Attack = 7,
        Speed = 8,
        Traits = new[] { TraitId.Shuffler },
        PlusText = "毎ターン味方2体の位置を入れ替える",
        MinusText = "入れ替える相手は選べない。後列前提の駒や庇う駒の配置を自分で壊す",
        Flavor = "隊列を整えている横で騒ぎ立て、二度と行軍に加えられなかった。"
    };

    public static readonly UnitDef Kugu = new()
    {
        Id = "kugu",
        Name = "縛めのクグ",
        MaxHp = 54,
        Attack = 3,
        Speed = 10,
        Traits = new[] { TraitId.Bind },
        PlusText = "毎ターン味方1体を縛り、その味方の攻撃+16",
        MinusText = "縛る相手は選べない。縛られた味方はそのターン動けない",
        Flavor = "味方を縛り上げる癖が抜けず、何度も牢に入れられた。"
    };

    public static readonly UnitDef Ban = new()
    {
        Id = "ban",
        Name = "据えのバン",
        MaxHp = 88,
        Attack = 5,
        Speed = 2,
        Traits = new[] { TraitId.Bulwark },
        PlusText = "そのターン動かなかった味方の被ダメージを半減する",
        MinusText = "全員が働く編成では何も起きない。自分も鈍重",
        Flavor = "動かない者を守ることしかできない。動く者は守れない。"
    };

    public static readonly UnitDef Shio = new()
    {
        Id = "shio",
        Name = "移り木のシオ",
        MaxHp = 60,
        Attack = 4,
        Speed = 8,
        Traits = new[] { TraitId.Drifter },
        PlusText = "隊列を動かされた味方を回復し、攻撃力を上げる",
        MinusText = "隊列が乱れなければ何もしない",
        Flavor = "落ち着きのない者にしか懐かない。整った隊では浮く。"
    };

    public static readonly UnitDef Utsu = new()
    {
        Id = "utsu",
        Name = "逆しまのウツ",
        MaxHp = 66,
        Attack = 9,
        Speed = 6,
        Traits = new[] { TraitId.Perverse },
        PlusText = "弱体化されるほど攻撃力が上がる（下げ幅の3倍）",
        MinusText = "強化されると攻撃力が半減する。支援を積む編成には入れない",
        Flavor = "褒められると腕が落ちる。呪われている間だけまともに戦う。"
    };

    public static readonly UnitDef Doha = new()
    {
        Id = "doha",
        Name = "分かちのドハ",
        MaxHp = 104,
        Attack = 4,
        Speed = 3,
        Traits = new[] { TraitId.Sharer },
        PlusText = "味方が受けるダメージの4割を肩代わりする（薙ぎでも全体でも効く）。肩代わり込みで受けた痛みに応じて自分の攻撃力も上がる",
        MinusText = "自分の火力はほぼ無く、味方が多いほど早く尽きる",
        Flavor = "他人の痛みを勝手に引き受ける。感謝はされず、ただ先に倒れる。"
    };

    public static readonly UnitDef Sasa = new()
    {
        Id = "sasa",
        Name = "散開のササ",
        MaxHp = 58,
        Attack = 7,
        Speed = 12,
        Traits = new[] { TraitId.Loose },
        PlusText = "隣に味方がいない駒の被ダメージを35%下げる",
        MinusText = "隊列を詰める編成では何も起きない。空きスロットを強いる",
        Flavor = "誰かの隣に立つことができない。近づかれると錯乱する。"
    };

    public static readonly UnitDef Kubi = new()
    {
        Id = "kubi",
        Name = "萎縮のクビ",
        MaxHp = 70,
        Attack = 3,
        Speed = 4,
        Traits = new[] { TraitId.Cower },
        PlusText = "味方全体の被ダメージを30%下げる",
        MinusText = "味方全体の攻撃力が9下がる",
        Flavor = "怯えが伝染する。隊が生き延びても、戦果は上がらなくなる。"
    };

    public static readonly UnitDef Hagi = new()
    {
        Id = "hagi",
        Name = "追い打ちのハギ",
        MaxHp = 62,
        Attack = 16,
        Speed = 7,
        Traits = new[] { TraitId.Pursuer },
        Pattern = AttackPattern.Sweep,
        PlusText = "味方が敵を倒すと、ターン順を無視して薙ぎ払う（1ターン1回）",
        MinusText = "自分の手番では決して動かない。味方が誰も倒せなければ置物",
        Flavor = "止めを刺した者の背後から現れる。手柄だけを持っていく。"
    };

    public static readonly UnitDef Sekki = new()
    {
        Id = "sekki",
        Name = "後備えのセッキ",
        MaxHp = 70,
        Attack = 2,
        Speed = 2,
        Traits = new[] { TraitId.RearGuard, TraitId.Rage },
        PlusText = "後列の味方への攻撃を肩代わりする。庇って受けたダメージに応じて攻撃力が上がる",
        MinusText = "素の攻撃力はほぼ無い。前列は一切守らず、狙われなければ育たない",
        Flavor = "前に出ろと言われても決して出ない。背中しか守らない。"
    };

    /// <summary>ムグの死骸から湧く駒。編成には選べない。</summary>
    public static readonly UnitDef Spore = new()
    {
        Id = "spore",
        Name = "胞子",
        MaxHp = 14,
        Attack = 4,
        Speed = 10,
        Traits = new[] { TraitId.Ephemeral },
        PlusText = "",
        MinusText = ""
    };

    public static readonly UnitDef Dolga = new()
    {
        Id = "dolga",
        Name = "のろまの巨兵ドルガ",
        MaxHp = 85,
        Attack = 38,
        Speed = 6,
        Traits = new[] { TraitId.Sluggish },
        Pattern = AttackPattern.Sweep,
        PlusText = "極めて重い一撃を、敵の両隣まで薙ぎ払う",
        MinusText = "2ターンに1回しか動けない",
        Flavor = "強い。ただ遅い。それだけの理由で外された。"
    };

    public static readonly UnitDef Hota = new()
    {
        Id = "hota",
        Name = "熾のホタ",
        MaxHp = 78,
        Attack = 6,
        Speed = 7,
        Traits = new[] { TraitId.Pyre },
        PlusText = "自分が燃えている間、攻撃力が4倍になり、攻撃が薙ぎに変わる",
        MinusText = "火が消えればただの湿った薪。自分では火を点けられない",
        Flavor = "焚きつけられている間だけ働く。誰かが火を放つのを待っている。"
    };

    public static IReadOnlyList<UnitDef> All { get; } = new[]
    {
        Borg, Mudo, Sero, Nel, Gald, Rica, Golm, Dolga, Mug, Zoto, Vel, Sid, Kado, Hisa, Nono, Mio, Rau, Guza, Tou, Beni, Gan, Vio, Yomi, Basa, Kugu, Ban, Shio, Utsu, Doha, Sasa, Kubi, Hagi, Sekki, Hota
    };

    public static UnitDef ById(string id) => All.First(u => u.Id == id);
}

/// <summary>討伐に来る人間側。プレイヤーは編成できない。</summary>
public static class EnemyCatalog
{
    private static UnitDef Make(string id, string name, int hp, int atk, int spd,
                                params TraitId[] traits) => new()
    {
        Id = id,
        Name = name,
        MaxHp = hp,
        Attack = atk,
        Speed = spd,
        Traits = traits
    };

    public static readonly UnitDef Recruit = Make("recruit", "討伐隊の新兵", 45, 11, 6);
    public static readonly UnitDef Axeman = new()
    {
        Id = "axeman", Name = "戦斧兵", MaxHp = 55, Attack = 12, Speed = 5,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Sweep
    };
    public static readonly UnitDef Knight = Make("knight", "巡礼騎士", 75, 15, 7);
    public static readonly UnitDef Priest = Make("priest", "従軍司祭", 40, 9, 8);
    public static readonly UnitDef Archer = new()
    {
        Id = "archer", Name = "狙撃手", MaxHp = 38, Attack = 14, Speed = 11,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Pierce
    };
    public static readonly UnitDef Warden = Make("warden", "城塞の重装兵", 145, 12, 3);
    public static readonly UnitDef Chanter = new()
    {
        Id = "chanter", Name = "詠唱兵", MaxHp = 70, Attack = 8, Speed = 5,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.All
    };
    public static readonly UnitDef Hero = Make("hero", "勇者候補", 95, 20, 14, TraitId.Executioner);

    // ここから第二波用。共有定義を触ると第一・三・四波が一緒に動くので、Id を変えて別定義にする。
    // 調整は Attack のみ。HP を触ると決着ターン数が変わり、積み上げ系の成立可否まで動く。
    public static readonly UnitDef KnightG = Make("knight_g", "巡礼騎士", 75, 24, 7);
    public static readonly UnitDef PriestG = Make("priest_g", "従軍司祭", 40, 9, 8);
    public static readonly UnitDef RecruitG = Make("recruit_g", "討伐隊の新兵", 45, 11, 6);
    public static readonly UnitDef ArcherG = new()
    {
        Id = "archer_g", Name = "狙撃手", MaxHp = 38, Attack = 18, Speed = 11,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Pierce
    };

    // ここから第五波用。既存3体の数値違いは Id を変えて別定義にする（第一〜三波を動かさないため）。
    public static readonly UnitDef Axeman2 = new()
    {
        Id = "axeman_v", Name = "戦斧兵", MaxHp = 52, Attack = 11, Speed = 5,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Sweep
    };
    // 断罪は審問官と勇者候補の2体で持つ。1体だとカドの反撃が担い手を先に殺して罰が消える
    // （審問官 HP76 / 単独だと配置を変えるだけで第5波 97.5% まで戻った）。
    // 3体にすると今度はカド系が全部20%台まで落ちて逆の崖になる。数ではなく担い手の数が摘み。
    public static readonly UnitDef Hero2 = Make("hero_v", "勇者候補", 90, 20, 14, TraitId.Condemn);
    public static readonly UnitDef Knight2 = Make("knight_v", "巡礼騎士", 71, 15, 7);
    public static readonly UnitDef Lancer = new()
    {
        Id = "lancer", Name = "槍騎兵", MaxHp = 66, Attack = 17, Speed = 12,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Pierce
    };
    public static readonly UnitDef Seer = new()
    {
        Id = "seer", Name = "審問官", MaxHp = 76, Attack = 12, Speed = 10,
        Traits = new[] { TraitId.Condemn }, Pattern = AttackPattern.All
    };
    // 第五波では使わない。第六波以降の素材として置いておく。
    public static readonly UnitDef Champion = Make("champion", "聖騎士長", 130, 22, 9, TraitId.Executioner);

    public sealed record Stage(string Name, Formation Enemy);

    public static IReadOnlyList<Stage> Stages { get; } = new[]
    {
        // 前列に固まると斧の薙ぎに巻かれる。範囲攻撃の存在をここで教える。
        new Stage("第一波 / 物見の兵",
            Formation.Build(front1: Recruit, front2: Axeman, front3: Recruit)),

        // 回復役の司祭はレーン0の奥行き2（騎士→司祭）。貫きで撃てば75%で届く。
        // 教えること:「支援役はレーンを選べば潰せる」。第一波の次に来る狙い撃ちの練習台。
        new Stage("第二波 / 巡礼騎士団",
            Formation.Build(front1: KnightG, front2: KnightG, front3: RecruitG, mid: ArcherG, back1: PriestG)),

        // 貫きは強烈なので1枚まで。2枚置くと後列に支援を置く編成が全滅する。
        // 射手はレーン1の最深部（勇者→斧→射手の奥行き3）。貫きでも50%まで減衰する。
        // 教えること:「奥行きが深いレーンは狙撃では崩せない。前から割れ」。
        new Stage("第三波 / 討伐隊本隊",
            Formation.Build(front1: Knight, front2: Hero, front3: Knight, mid: Axeman, back2: Archer)),

        // 一撃は軽いが硬い。決着まで時間がかかるので、
        // 積み上げ系が立ち上がる余地があるかを確かめるためのステージ。
        // 全体攻撃は1枚まで。2枚置くと支援型の駒が編成から消える。
        // 支援2枚はどちらもレーン1の奥（中と後2）。壁を割り切るまで全体攻撃が止まらない。
        new Stage("第四波 / 城塞守備隊",
            Formation.Build(front1: Warden, front2: Warden, front3: Warden, mid: Priest, back2: Chanter)),

        // 前列に薙ぎ、後列に貫きと全体。4種の攻撃パターンが同時に飛んでくる。
        // 単体前提の防御（庇う・標的）だけでは支えられない構成にしてある。
        //
        // 審問官と勇者候補は断罪を持つ。反撃してきた相手を痺れさせるので、
        // ターン外に動く駒（棘・割り込み・追い打ち）だけが代金を払う。
        // 反撃しない編成には何も起きない（19編成すべて ±0.0 で確認済み）。
        new Stage("第五波 / 異端審問団",
            Formation.Build(front1: Axeman2, front2: Hero2, front3: Knight2, mid: Lancer, back1: Seer))
    };
}
