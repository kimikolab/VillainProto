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
        PlusText = "味方への攻撃を肩代わりし、その傷のぶん強くなる",
        MinusText = "味方全体に配られる強化も弱体も自分には乗らず、隣接する味方へそのまま流れる（1体を選ぶ回復・強化は受け取れない）",
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
        PlusText = "後ろの味方への攻撃を型を問わず肩代わりし、飲み込んだ力をその味方へ返す",
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
        // **ThornGuard を Thorns より前に置く。** ApplyDamage は target.Traits の順に
        // OnDamaged を通知し、TraitCatalog.Resolve は Def.Traits の順をそのまま保つので、
        // この配列の順序がそのまま「入れ替え → 反撃」の実行順になる
        // （ThornGuardTrait.OnDamaged 参照）。逆にすると、移動前の隣接に対して刺し返す。
        Traits = new[] { TraitId.ThornGuard, TraitId.Thorns, TraitId.Immobile, TraitId.Havoc },
        // 棘を張り直すのが手番そのもの（不動は攻撃だけを禁じるので、術の手番は通る）。
        // 1要素なので毎ターン構え直す＝構えは常に張られている状態になる。
        Actions = new UnitAction[] { new(ActionKind.Skill, Label: "棘を外へ向けて構えている") },
        PlusText = "殴られると、自分の攻撃力の2倍を敵に返す。反撃は隣の敵にも届き、巻き込んだ味方のダメージ分だけ自分の攻撃力が上がる / 毎ターン構え、前か横の味方への単体攻撃を身代わりして、その味方と位置を入れ替える",
        MinusText = "自分からは決して攻撃しない / 反撃が隣の味方も巻き込む（身代わりした相手は入れ替え後も必ず隣にいるので、必ず巻き込む）/ 味方全体の受けるダメージが5割増える",
        Flavor = "命令しても動かない。庇われた者は、庇われたことを後で悔やむ。"
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
        // 繕いを手番の行動そのものにする（第11期 Phase BB）。攻撃3 は出なくなる。
        // [Skill] 1つだけの周期で移すのは、挙動の差を「攻撃が出ない」だけに絞るため。
        Actions = new UnitAction[] { new(ActionKind.Skill, Label: "傷を繕っている") },
        PlusText = "毎ターン、最も傷ついた味方を繕う",
        MinusText = "繕った分だけ自分が減る。攻撃はしない（繕いが手番そのもの）",
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
        // 濃縮を手番の行動そのものにする（第11期 Phase BB）。攻撃2 は出なくなる。
        Actions = new UnitAction[] { new(ActionKind.Skill, Label: "水を濁らせている") },
        PlusText = "毎ターン、敵に積まれた毒を濃くする（+4層）",
        MinusText = "毒が積まれていなければ完全に無意味。攻撃はしない",
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
        // 縄は1本。開戦時にその1本を敵へ投げるので、第1ターンだけ味方の縛りが起きない。
        // 代金は振り（攻3）ではなく味方の縛り1回ぶんで、収入の有無で意味が反転する（BindTrait）。
        // 周期（Actions）は持たせない——稼働率が低い駒の周期スキルは発動しないまま決着する。
        PlusText = "開戦時に大縛りで最も速い敵1体を縛る。第2ターン以降は毎ターン味方1体を縛り、その味方の攻撃+16",
        MinusText = "縛る味方は選べない。縛られた味方はそのターン動けない。第1ターンは味方の縛りが起きない",
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
        PlusText = "味方が敵を倒すと、ターン順を無視して薙ぎ払う（同じターンに続けて踏み込むほど自分が傷つく）",
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
        PlusText = "自分が燃えている間、攻撃力が4倍になり、攻撃が貫きに変わる",
        MinusText = "火が消えればただの湿った薪。自分では火を点けられない",
        Flavor = "焚きつけられている間だけ働く。誰かが火を放つのを待っている。"
    };

    /// <summary>
    /// 砕け盾のヒビ。範囲攻撃に対する唯一の受け手。
    ///
    /// 庇う（ガルド）が標的選択の層で単体だけを止めるのに対し、こちらは damage の層にいて
    /// 薙ぎ・全体・貫きだけを拾う。実測で敵の攻撃力に占める範囲の割合は第五波で53%あり、
    /// そこが丸ごと素通りしていた（後備えは主目標を差し替えるだけで巻き込みには触れない）。
    ///
    /// 脆弱は罰ではなく燃料。浴びる量が増えるほど配れる破片も増える。
    /// </summary>
    public static readonly UnitDef Hibi = new()
    {
        Id = "hibi",
        Name = "砕け盾のヒビ",
        MaxHp = 55,
        Attack = 5,
        Speed = 3,
        Traits = new[] { TraitId.Shatter, TraitId.Frail },
        PlusText = "範囲攻撃を受けると、その4分の1を破片として味方全員に配る（HPの前に削られる／回復を受け付けない味方にも届く）",
        MinusText = "受けるダメージが5割増し / 単体攻撃しか飛んでこない相手には何も起きない",
        Flavor = "盾として不良品と判定された。割れながら破片を撒くので周りが危ないとも書かれている。"
    };

    /// <summary>
    /// 置き去りのナラ。速さを読む唯一の駒。
    ///
    /// 速さ8。**7（35体の中央値）から動かしてある（第20期）。**
    /// 7 のときの無風帯（同速）は リィカ・ゾト・ラウ・ヴィオ・ハギ・ホタ で、
    /// **削りの最良の消費者である即時払いの変換器（ゾトの破裂・リィカの層）が
    /// まるごとそこに落ちていた。** 8 に動かすと無風帯は
    /// バサ・ミオ・ボルグ・シオ・ヴェル になり、被弾変換器が1体も含まれない。
    /// 割れ方は 削り22 / 無風5 / 回復8。
    ///
    /// 回復側が 13 → 8 に減るのは織り込み済み（回復側の実証は「置き去り×速攻」が担う）。
    /// 7 に戻す条件は「即時払いの変換器でも燃料が出力にならない」と出たとき——
    /// そのときは変換器の型ではなく台の長さの問題なので、速さではなく `engage` 側へ移る。
    ///
    /// **規則ではなく速さを動かすこと。** Heal / Toll は触らない
    /// （効き方を変えるノブは保持者の速さ、というのがこの駒の設計）。
    ///
    /// 攻撃9・単体は「支援役だが殴りもする」帯。ノノ（攻3・攻撃しない）と違って
    /// 手番を潰さないので、置き去りは OnTurnStart のパッシブのまま（第11期の
    /// アクティブ移行の対象外）。
    /// </summary>
    public static readonly UnitDef Nara = new()
    {
        Id = "nara",
        Name = "置き去りのナラ",
        MaxHp = 62,
        Attack = 9,
        Speed = 8,
        Traits = new[] { TraitId.Forsake },
        PlusText = "毎ターン、自分より速い味方を癒す",
        MinusText = "毎ターン、自分より遅い味方を削る。同じ速さの味方には何も起きない",
        Flavor = "付いて来られる者だけを引き上げた。残りは、置いていくものだと思っていた。"
    };

    public static IReadOnlyList<UnitDef> All { get; } = new[]
    {
        Borg, Mudo, Sero, Nel, Gald, Rica, Golm, Dolga, Mug, Zoto, Vel, Sid, Kado, Hisa, Nono, Mio, Rau, Guza, Tou, Beni, Gan, Vio, Yomi, Basa, Kugu, Ban, Shio, Utsu, Doha, Sasa, Kubi, Hagi, Sekki, Hota, Hibi, Nara
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
    // 溜めてから撃つ（第10期 Phase AB）。**平均火力は変えない**——2周期の 200% は
    // (0 + 2) / 2 = 1.0 で、毎ターン 14 を振るのと総量が同じ。変えたのは配り方だけで、
    // 「何ターンで終わらせるか」が代金を決めるようにするのが狙い（第10期 §0）。
    //
    // 3周期案（通常 → 溜め → 強貫き）は測って却下した。平均火力は同じく 1.0 だが、
    // 戦闘長 3.9〜5.6 ターンに対して長すぎて、発火数が 反撃3(カド×ハギ) で 0.00、
    // 惨禍×被弾強化 で 0.06——**大技が一度も出ないまま終わる編成が出る**。
    // そうなると波はただ 67% 引きになるだけで、溜めを見て合わせるという体験が成立しない。
    // 2周期なら全31編成が最低 1.00 回は浴びる（発火 平均 2.15 / 最小 1.00 / 最大 3.69）。
    //
    // 倍率は 180 / 200 / 220 を振って残存で確かめた。どれも「浴びて全滅」は起こさない
    // （220% でも勝率 -1.4pt・残存 3.14→3.09）ので、平均火力を保つ 200 を採る。
    // 180 は波が 10% 安くなり、220 は 10% 高くなる——どちらも代金を静かに動かして
    // 計測の交絡になる。
    public static readonly UnitDef Archer = new()
    {
        Id = "archer", Name = "狙撃手", MaxHp = 38, Attack = 14, Speed = 11,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Pierce,
        Actions = new UnitAction[]
        {
            new(ActionKind.Charge, Label: "狙いを定めている"),
            new(ActionKind.Attack, 200),
        }
    };
    public static readonly UnitDef Warden = Make("warden", "城塞の重装兵", 145, 12, 3);
    // 溜めてから撃つ（第10期 Phase AB）。狙撃手と同じ 2周期 200%（理由は上）。
    // 全体 16 は味方後列の HP（40〜55 前後）を1発では抜かない。第四波は決着が遅い波
    // （積み上げ系の立ち上がりを見るための波）なので発火 平均 2.68 と多いが、
    // 残存は 3.14 → 3.10 しか動かない。
    public static readonly UnitDef Chanter = new()
    {
        Id = "chanter", Name = "詠唱兵", MaxHp = 70, Attack = 8, Speed = 5,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.All,
        Actions = new UnitAction[]
        {
            new(ActionKind.Charge, Label: "魔力を集めている"),
            new(ActionKind.Attack, 200),
        }
    };
    public static readonly UnitDef Hero = Make("hero", "勇者候補", 95, 20, 14, TraitId.Executioner);

    // ここから第二波用。共有定義を触ると第一・三・四波が一緒に動くので、Id を変えて別定義にする。
    // 調整は Attack のみ。HP を触ると決着ターン数が変わり、積み上げ系の成立可否まで動く。
    public static readonly UnitDef KnightG = Make("knight_g", "巡礼騎士", 75, 24, 7);
    // 第二波から外した（2026-08-28）。回復役という設定コメントだけで何も回復しないので、
    // 実際に回復する Chaplain に差し替えた。第二波の性格を戻すときの対照として定義は残す。
    public static readonly UnitDef PriestG = Make("priest_g", "従軍司祭", 40, 9, 8);
    public static readonly UnitDef RecruitG = Make("recruit_g", "討伐隊の新兵", 45, 11, 6);
    // 第10期でもチャージを付けない。第二波は練習用の波で、ここを溜めさせると
    // 「支援役はレーンを選べば潰せる」という教える内容の手前に、溜めの読み合いが挟まる。
    // 易しい波の性格が変わると第1波・第2波の代金の基準も動く。
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

    // ここから第5期・勾配列（design/ENGAGEMENT_PLAN_5.md）の候補素材。既存の波が参照しない限り
    // 何も動かない（compare 差分ゼロが受け入れ条件）。採用が決まるまで Stages / Columns には
    // 足さない。候補波の編成は BattleSim の gradient モードがローカルに組む。

    // 駆り出された農兵:「数だけ多い雑兵」の波の素体。第一波の新兵(45/11)より個体を明確に
    // 弱くし、体数で総圧を作る。HP 30 は味方の主な単体打点(14〜20)の2発圏・ドルガの薙ぎ(38)や
    // 育った駒の1発圏で、「範囲・高打点なら1手で複数落ちるが、素の単体では1体2手」の境目に
    // 置いた値。攻撃 8 は6体並べて 48/T——第一波(34/T)を上回るが、1体落ちるごとに 8 ずつ
    // 急落するので「早く減らした編成ほど安く抜けられる」勾配を作る。速さ 6 は新兵と同じ
    // （この波の個性は数だけ。速度で個性を作らない）。
    public static readonly UnitDef Levy = Make("levy", "駆り出された農兵", 30, 8, 6);

    // 従軍司祭長: 精鋭波の「回復役入り」候補のための、実際に回復する司祭。
    // 既存の従軍司祭(priest)は回復役という設定コメントだけで特性を持たない（素の 40/9/8）ので、
    // 「回復役を入れると波の性格が変わるか」（第5期 §3-3）はこの def でしか測れない。
    // 回復は継ぎ当て（Mender: 毎ターン、最も傷ついた味方を 14 回復し、同量だけ自分が減る）。
    // 等価交換なので回復総量は自分の HP が上限——HP 62 は精鋭1体の被弾4〜5ターン分を
    // 肩代わりする量で、無限に支えて浄化と同じ崖（README「引き算は崖」）を作らないための刻み。
    // 攻撃 7 はほぼ飾り。速さ 8 は既存の司祭と同じ。
    // 第二波で使用（2026-08-28）。ここを触ると第二波が動く——他の波が回復役を要るなら
    // 新しい変異体を作ること。攻撃 7 には床がある: 呪詛（CurseTrait.EnemyDebuff = 6）で
    // 7 → 1 になるが 0 にはならず ApplyDamage の早期 return に落ちない。> 6 を割らないこと。
    public static readonly UnitDef Chaplain = Make("chaplain", "従軍司祭長", 62, 7, 8, TraitId.Mender);

    // 施しの司祭長: 第二波の支援役（2026-08-30）。Chaplain（継ぎ当て）は消さずに対照として残す
    // ——PriestG を残してあるのと同じ扱いで、第二波の性格を戻すときにここへ差し戻せる。
    // 施し（Alms: 毎ターン、最も傷ついた味方を 14 回復する。**自分は減らない**）。
    // 継ぎ当てのままでは「保持者に与えた1ダメージ ＝ 否定できる回復1」で価値が線形になり、
    // 25% 減衰する貫きで狙うと正味の損になっていた（AlmsTrait のコメントに全文）。
    // HP 36 = 減衰後の貫き（24 × 75% = 18）のちょうど2発。2ターンで落ちるので、否定できるのは
    // 14 × 残り3ターン = 42（第二波の平均決着は約5ターン）。**払った 36 を上回るので、
    // 初めて「潰す価値」が立つ**。通常攻撃は後列に届かないので、開くのは後列に届く手段
    // （貫き・全体・毒）を持つ編成にだけ。
    // 攻撃 7 は Chaplain から据え置き。床も同じ——呪詛（CurseTrait.EnemyDebuff = 6）で 7 → 1 に
    // なるが 0 にはならず ApplyDamage の早期 return に落ちない。> 6 を割らないこと。速さ 8 も据え置き。
    public static readonly UnitDef Almoner = Make("chaplain_g", "施しの司祭長", 36, 7, 8, TraitId.Alms);

    // ここから第6期・安い波の再設計（design/ENGAGEMENT_PLAN_6.md）の候補素材。
    // 第5期の農兵では代金の「向き」（範囲持ちの編成にだけ安い）が作れなかった
    // （単体 − 範囲 が +3.1pt で、編成間のばらつき 9.4pt に埋もれる）。原因の仮説は2つあり、
    // どちらが正しいかで作るべき素体が正反対になるので、両方の素体を用意して測る。
    //   H1（戦闘が短すぎる）: 総HPを上げて戦闘を長くすれば範囲の複利が効く
    //   H2（1体あたりの価値が低すぎる）: 1キルの価値 = その駒の攻撃力 × 残りターン数 なので、
    //                                     攻8 では範囲で3体倒しても 24/T しか減らない
    // 全て単体攻撃・速さ6（農兵と同じ）。敵側の攻撃型と速度は測定の交絡になるので振らない。
    // 候補波の編成は BattleSim の aim モードがローカルに組む（Stages / Columns には足さない）。
    //
    // 打点の基準について: 農兵のコメントは「味方の主な単体打点(14〜20)」と書いているが、
    // docs/pulse.md から実測した1振りあたりの打点（与ダメ(敵) ÷ 振/T ÷ 平均ターン、143 駒行）は
    // **中央値 10.6 / 四分位 4.4〜20.4 / 上位1割 51.1 / 最大 90.1（墓守リィカ）** で、
    // 「一撃圏」は編成によって 1〜3 発に振れる。以下の何発圏という表記は中央値 10.6 基準。

    // 駆り出された人足（H1 用）: 個体HPを上げ、攻撃を下げた雑兵。農兵(30/8)に対して 45/6。
    // 6体で総HP 270（農兵6の 180 の1.5倍）・総攻 36/T（同 48/T の0.75倍）——総HPで戦闘を伸ばし、
    // 総攻撃力で代金を抑える。HP 45 は実測中央値の5発圏・上位1割の1発圏で、「範囲で薙いでも
    // 1手では落ちない」側に意図的に置いた値（落ちないぶん戦闘が伸びる。H1 の主張そのもの）。
    public static readonly UnitDef Laborer = Make("laborer", "駆り出された人足", 45, 6, 6);

    // 狂信者3種（H2 用）: 攻撃 16 を固定して個体HPだけを振った軸。攻16 は農兵の2倍で、
    // 5体並べて 80/T——1体落とすごとに 16 ずつ落ちるので、1キルの価値が農兵の2倍になる。
    // HP は実測中央値 10.6 の 2発圏 / 3発圏 / 4発圏（上位25%の 20.4 なら 1 / 2 / 2発圏）。
    // 却下した案: 指示書の目安どおり「打点14〜20の1〜2発圏」として HP 24 の1点だけを作る案。
    // 実測分布が 0〜90 に広がっていて、その1点が編成ごとに 1〜3 発圏へ振れる——つまり
    // 「一撃圏」を1点に決めた瞬間、それが仮定なのか測定結果なのか区別できなくなるので、
    // 推測で決めずに HP を軸にして3点測る形にした（どこから向きが出るかは測定で決める）。
    public static readonly UnitDef ZealotBare = Make("zealot_bare", "裸の狂信者", 16, 16, 6);
    public static readonly UnitDef ZealotLeather = Make("zealot_leather", "革鎧の狂信者", 24, 16, 6);
    public static readonly UnitDef ZealotMail = Make("zealot_mail", "鎖帷子の狂信者", 32, 16, 6);

    // 傭兵崩れ（中間点）: 農兵(30/8)・人足(45/6)・狂信者(16〜32/16) の中間。36/11 は
    // 5体で総HP 180・総攻 55/T。総HP × 1体あたり攻撃 の2軸で候補を散らすための4点目で、
    // 「向きが出るとしたら軸のどちら側か」を単調性で読むために置く（H1 でも H2 でもない対照）。
    public static readonly UnitDef Drifter = Make("drifter", "傭兵崩れ", 36, 11, 6);

    // ここから第7期・高い波の反転（design/ENGAGEMENT_PLAN_7.md）の候補素材。
    // 第6期で「代金の向きの正体は 1手で何体落ちるか」だと分かった（攻16 固定で個体HPを
    // 16 → 24 → 32 と厚くすると 単体−範囲 が +8.7 → +8.4 → +5.8pt と単調に減る）。
    // その鏡像として、**範囲に高くつく波**＝少数・高個体HP・高攻撃 を作れるかを測る。
    // 攻撃は狂信者3種と同じ **16 に固定**する。第6期の HP 軸（16/24/32）の延長線上に
    // 60 / 90 を置けば、向きが +8.7pt から反転するまでの閾値を1本の軸で読めるため
    // （攻撃も一緒に振ると、どちらが効いたのかが分離できなくなる）。
    // 速さ 5 は既存の第3波素材（重装兵3・聖騎士長9）の中間。狂信者3種の 6 から動かしたのは
    // 第3波の位置に置く波だからで、この軸で個性は作らない。全て単体攻撃（範囲持ちを
    // 入れない規則は第6期と同じ）。
    //
    // 却下した案: 「1体あたりの攻撃を上げる」を素直に読んで攻22（聖騎士長と同値）にする案。
    // 攻撃と HP を同時に動かすと、反転が「一撃圏の外に出たから」なのか「単に総攻が上がって
    // 代金が膨らんだから」なのか区別できない。第6期が攻16固定で HP を振って閾値を挟んだのと
    // 同じ形を維持する。
    //
    // 打点の基準（第6期の実測。docs/pulse.md から 143 駒行）: 中央値 10.6 / 四分位 4.4〜20.4 /
    // 上位1割 51.1 / 最大 90.1。
    // HP 60 は**上位1割（51.1）でも1発では落ちない**最初の刻み（中央値なら6発圏）。
    // HP 90 は上位1割の2発圏で、最大打点（90.1）でようやく1発——明確に一撃圏の外。
    public static readonly UnitDef ZealotPlate = Make("zealot_plate", "板金鎧の狂信者", 60, 16, 5);
    public static readonly UnitDef ZealotGreat = Make("zealot_great", "重甲の狂信者", 90, 16, 5);

    // 処刑なしの聖騎士長（第7期 §2-4 の対照）。数値は Champion と完全に同じで、特性だけを
    // 落としてある。処刑（HPが減った敵を優先して仕留める）が少数高HPの波の向きに効いて
    // いるかを、他を動かさずに測るための対照。既存の Champion は触らない。
    public static readonly UnitDef ChampionPlain = Make("champion_plain", "聖騎士長", 130, 22, 9);

    // 重甲の従卒（攻撃を下げた重甲。反転の3軸を重ねる点）。
    // 初回の格子で分かった向きの作り方は3つあり、どれも第6期の裏返しになっている:
    //   体数を**増やす**（倒しきれない相手が並ぶほど範囲は撒いて損をする）
    //   個体HPを一撃圏の外に置く（第6期の HP 軸の逆向き）
    //   1体あたりの攻撃を**下げる**（第6期の 単体−範囲 と1体あたり攻撃の相関は r=+0.93）
    // 攻16 の重甲6体（96/T）は反転が -4.3pt までしか出なかったが、これは代金 87% で
    // 10編成が勝率 0% に落ち、**高くつくはずの編成が集計から消える打ち切りバイアス**が
    // 乗ったため。攻撃だけを 10 に下げて 60/T にすると、同じ「6体・一撃圏の外」の盤面を
    // 全編成が勝ち切れる範囲に収められる。HP 90・速さ 5 は重甲と同値（軸を1つだけ動かす）。
    public static readonly UnitDef ZealotSquire = Make("zealot_squire", "重甲の従卒", 90, 10, 5);

    // ここから第8期・合計代金を振る（design/ENGAGEMENT_PLAN_8.md Phase V）の候補素材。
    // 狙いは「第3波を安くしつつ、範囲に高くつく向きを保つ」こと。
    //
    // 指示書は体数を減らして安くする案だったが、測ると**向きが体数と一緒に消える**——
    // 従卒6(90/攻10) -8.4pt / 従卒5 -3.6pt / 重甲3 +2.6pt。第7期の結論どおり体数が向きの
    // 源泉なので、体数・代金・向きが1本の軸に乗ってしまい、体数では分離できない。
    // 残る軸は**1体あたり攻撃**（第7期で 単体−範囲 との相関 r=+0.93 を測った軸）で、
    // 攻撃を下げると代金だけが落ちて体数6・HP90（一撃圏の外）はそのまま残せる。
    // HP 90・速さ 5・単体攻撃は重甲の従卒と同値（動かす軸は攻撃だけ）。
    //
    // 実測（flip と同じ物差し・seed 200・31編成）: 攻10 → 67.9%/-8.4pt、攻7 → 53.9%/-4.5pt、
    // 攻5 → 46.1%/-2.9pt、攻4 → 42.4%/-2.2pt。**安くするほど向きも薄くなる**ので、
    // 攻4 の列は「向きの列」ではなく「合計代金だけを下げた列」として読むこと（第8期 §3-3）。
    //
    // 却下した案: 総HPを下げて安くする（板金従卒6＝60/攻7 で 42.3%）。同じ 42% 帯で
    // 向きは -2.9pt と攻4 より僅かに濃いが、HP 60 は上位1割の打点でも1発では落ちない
    // とはいえ一撃圏の縁で、**代金を下げた効果と一撃圏を跨いだ効果が混ざる**。
    // 攻撃だけを振れば HP 軸は第7期のまま固定でき、安さの効果を単独で読める。
    public static readonly UnitDef ZealotPorter = Make("zealot_porter", "重甲の荷駄兵", 90, 7, 5);
    public static readonly UnitDef ZealotPilgrim = Make("zealot_pilgrim", "重甲の巡礼者", 90, 4, 5);

    // 逆位の祭司: 第三波の中央に置く候補として作り、**測って採らなかった**（2026-08-30）。
    // どの波も参照していないので何も動かない。Levy / ZealotPorter と同じ「採用が決まるまで
    // Stages に足さない候補素材」の扱いで、**次に盤面ルールを試すときの対照として残す**。
    // 数値は当時の案のまま。HP 90 は巡礼騎士 75 から上げ、攻 10 は 15 から下げてある——
    // ルールは効いている時間そのものが効果量なので早く落ちると測れず、しかしこの駒で波の
    // 火力を上げると反転の効果と混ざるため。速 7 は据え置き（保持者の速さでは個性を作らない）。
    //
    // 狙いは「第三波を速さの向きという別軸の検出器にする」こと。第2〜4波は固有の勝者も敗者も
    // 0 本で、勝敗の順序を1つも変えていなかった（spread の表3）。
    //
    // **測定の結論: 反転は実在するが、波を分離しない。**
    //
    // 1. 同じ壁(90/10/7)で逆位 ON/OFF を比べると、第三波は **12編成が上がり 5編成が下がり
    //    18編成が不動**（+23.5pt 〜 -6.5pt）。per-build の効果は本物で、置物ではない。
    // 2. ただし**平均 +2.86pt で、正味は難度の引き下げ**。反転はこの波の勇者候補（速14・処刑）を
    //    最後尾に回すので、味方側が失う速さより敵の雪だるまが潰れる利得のほうが大きい。
    //    計画が「落ちる」と予想した速い駒依存の編成は逆に上がった（突き出し +15.0 /
    //    隊列崩し +8.0 / 速攻 +10.5）。予想どおり落ちたのは縛め非収入型 -5.5 だけ。
    // 3. 分離は**むしろ悪化**する。標準偏差 26.4 → 22.7、第2波との相関 +0.81 → +0.81。
    //    動きが既存の難度軸と平行なので、弱い編成を本隊のほうへ押し上げるだけで
    //    新しい次元が開かない。
    // 4. HP を 60〜260 まで振ると分離は単調に良くなる（SD 18.1 → 41.0 / 相関 0.81 → 0.57 /
    //    固有の敗者 0 → 2）が、**同じ HP で逆位を外しても同じ値が出る**（260・逆位なしで
    //    SD 42.7 / 相関 0.56 / 固有の敗者 2）。**効いていたのは壁であって、ルールではない。**
    //
    // 「固有の勝者」側は定義上そもそも立たない。spread の判定が「他のどの波でも 100% 未満」で、
    // 第一波は全35編成が 100% なので、**第2〜5波の固有の勝者は恒等的に 0**。
    // 第三波をどう作っても動かない（第一波を 100% のまま置く限り）。
public static readonly UnitDef Inverter = Make("inverter", "逆位の祭司", 90, 10, 7, TraitId.Inversion);

    // 渇きの祭司: **第三波の中央に採用した**（2026-08-30）。巡礼騎士1枚と差し替えてある。
    // **数値は巡礼騎士（75/15/7）と1つも違わない。トレイトだけを足してある。**
    // 逆位は HP を 75→90・攻を 15→10 と動かしたせいで「壁が変わったのか、ルールが効いたのか」
    // の切り分けに追加測定が要った。今回は数値を固定したので、差分はルールだけに閉じ込まる
    // ——同数値・トレイト無しの対照は 35編成すべてで現行と1桁も違わなかった（検算済み）。
    //
    // 測定（spread・seed 200・35編成）: 第三波の 100%編成 18 → 11 / 固有の敗者 0 → 2 /
    // 中間帯 12 → 14 / 第2〜4波すべて100% 16 → 9 / 第2波との相関 +0.85 → +0.71。
    // 平均は 80.5 → 70.0 なので**波としては難しくなっている**（逆位は易しくしていた）。
    public static readonly UnitDef Droughter = Make("droughter", "渇きの祭司", 75, 15, 7, TraitId.Drought);

    // 軛の重装兵: **第四波の中央に採用した**（2026-08-31）。重装兵1枚と差し替えてある。
    // **数値は城塞の重装兵（145/12/3）と1つも違わない。トレイトだけを足してある**
    // ——渇きと同じ形で、差分をルールだけに閉じ込めるため（逆位は数値も動かしたせいで
    // 「壁が変わったのか、ルールが効いたのか」の切り分けに追加測定が要った）。
    // 同数値・トレイト無しの対照（`yoke` の V1）は 35編成 × 5波 で現行と1桁も違わない（検算済み）。
    //
    // 課金する資源は「**1発の重さ**」。第二波は後列到達力、第三波は持続（渇き）、
    // 第五波は総合なので、どの波とも重ならない1本になる。
    // **敵側の打点は全部 25 以下**（重装 12・詠唱兵の溜め 16・従軍司祭 9）なので、
    // この波で課税されるのは味方の打点だけ。
    // 「硬いので大打点で押し切れない」は第四波の既存の性格（重装 145×3）と一貫していて、
    // 新しい教え事を足さずに済む。
    //
    // 測定（spread・seed 200・35編成）: 第四波の 100%編成 21 → 9 / 固有の敗者 0 → 3 /
    // 中間帯 7 → 11 / 第2〜4波すべて100% 11 → 5 / 第2波との相関 +0.62 → +0.31。
    // 平均は 87.0 → 61.8 で、波の並びが 100 / 85.8 / 72.5 / 61.8 / 59.8 と単調に落ちる。
    // **課税されたのは大打点の駒ではなく積み上げ系**（墓守の層 攻撃151 → 25・毒の刻み 52 → 25）
    // で、ドルガ38 や反撃軸はほぼ無風だった——経緯は README「波に『1発の上限』を置いたら」。
    public static readonly UnitDef Yoker = Make("yoker", "軛の重装兵", 145, 12, 3, TraitId.Yoke);

    public sealed record Stage(string Name, Formation Enemy);

    public static IReadOnlyList<Stage> Stages { get; } = new[]
    {
        // 前列に固まると斧の薙ぎに巻かれる。範囲攻撃の存在をここで教える。
        // 斧は前3。前1・前3 を薙がれると中央まで巻き込まれるので、
        // 前に固めるほど1発で全員に届く。
        new Stage("第一波 / 物見の兵",
            Formation.Build(front1: Recruit, front3: Axeman, center: Recruit)),

        // 施しの司祭長は後1。自分は減らずに毎ターン14を配るので、放置すると戦闘長ぶん（約70）が
        // 敵の実効HPに乗る。通常攻撃は後列に届かないので、潰せるのは後列に届く手段を持つ編成だけ。
        //
        // X字化で2本のレーンは奥行きが等しくなった（前X → 中央 →〔○中X〕→ 後X）ので、
        // 「浅いレーンを選べば安く届く」という抜け道は無い。司祭長は貫きの3体目に当たり
        // 50%まで減衰する（旧盤面のレーン0では2体目＝75%だった。ここは意図して重くなっている）。
        // 教えること:「どちらの列も同じ深さ。選ぶのは深さではなく、その列に誰がいるか」。
        new Stage("第二波 / 巡礼騎士団",
            Formation.Build(front1: KnightG, front3: KnightG, center: RecruitG, back1: Almoner, back3: ArcherG)),

        // 貫きは強烈なので1枚まで。2枚置くと後列に支援を置く編成が全滅する。
        // 勇者候補（断罪持ち・攻20）は前3。旧盤面の前2と同じく最初から狙える位置に置く。
        // 中央に隠すと単体軸の編成が本命に一度も触れないまま決着し、波が別物になる。
        // 後列に届くまでに削るHPは 75+95+75=245 で旧前列と同じ。
        // 教えること:「狙撃手は最奥。前から割るか、貫きで減衰を飲むか」。
        //
        // 中央を逆位の祭司（EnemyCatalog.Inverter）に差し替える案は測って**採らなかった**。
        // 理由は Inverter の宣言に全文（要するに「反転はこの波を易しくする方向に働く」）。
        //
        // **中央は渇きの祭司（Droughter）。** 巡礼騎士と数値は同一で、盤面ルールを1つ持つ
        // ——生きている間、**両陣営の回復が一切通らない**。狙いは「後列に届くか」しか
        // 問うていなかったこの波に、**持続資源という別の軸を1本足す**こと。
        // 中央に置くのは逆位のときと同じ理由で、単体攻撃は前列2枚を割るまで届かず、
        // 貫きなら2番目（減衰75%）で必ず当たる——「前から割るか、貫きで減衰を飲むか」という
        // この波が既に教えている内容が、そのままルール駒への解答になる。
        // 教えること:「回復を数えて編成を組んだなら、それが通らない盤面がある」。
        new Stage("第三波 / 討伐隊本隊",
            Formation.Build(front1: Knight, front3: Hero, center: Droughter, back1: Archer, back3: Axeman)),

        // 一撃は軽いが硬い。決着まで時間がかかるので、
        // 積み上げ系が立ち上がる余地があるかを確かめるためのステージ。
        // 全体攻撃は1枚まで。2枚置くと支援型の駒が編成から消える。
        // 支援2枚は後列に並ぶ。重装3枚が 前1・中央・前3 を埋めるので、どちらの列を貫いても
        // 必ず中央の重装を通る（＝2経路とも減衰が満額かかる）。壁を割り切るまで全体攻撃が止まらない。
        //
        // **中央は軛の重装兵（Yoker）。** 重装兵と数値は同一で、盤面ルールを1つ持つ
        // ——生きている間、**両陣営の1回のダメージが 25 で切られる**。狙いは「硬さ」しか
        // 問うていなかったこの波に、**1発の重さという別の軸を1本足す**こと。
        // 中央に置くのは渇きのときと同じ理由で、**どちらの列を貫いても必ず中央を通る**という
        // この波が既に持っているボトルネックが、そのままルール駒への解答になる
        // （新しい教え事が要らない）。上限がかかっている間は 145 を割るのが遅くなるので、
        // 「早く割れば上限が外れる」という勾配が自己言及的に立つ。
        // 教えること:「一撃の大きさを数えて編成を組んだなら、それが切られる盤面がある」。
        new Stage("第四波 / 城塞守備隊",
            Formation.Build(front1: Warden, front3: Warden, center: Yoker, back1: Chanter, back3: Priest)),

        // 前列に薙ぎ、後列に貫きと全体。4種の攻撃パターンが同時に飛んでくる。
        // 単体前提の防御（庇う・標的）だけでは支えられない構成にしてある。
        //
        // 審問官と勇者候補は断罪を持つ。反撃してきた相手を痺れさせるので、
        // ターン外に動く駒（棘・割り込み・追い打ち）だけが代金を払う。
        // 反撃しない編成には何も起きない（19編成すべて ±0.0 で確認済み）。
        new Stage("第五波 / 異端審問団",
            Formation.Build(front1: Axeman2, front3: Hero2, center: Knight2, back1: Seer, back3: Lancer))
    };

    /// <summary>会戦（Engagement）の敵部隊列。名前と「なぜこの並びを測るのか」のメモを持つ。</summary>
    public sealed record Column(string Name, string Note, IReadOnlyList<Formation> Squads);

    /// <summary>
    /// 会戦で測る部隊列。敵の中身はどれも既存5波のままで、並びと長さだけが違う。
    /// 新しい敵は作らない（会戦の計測が敵の変更と混ざると効き目が読めなくなる）。
    /// 宣言は Stages より後ろに置くこと（静的初期化子は上から順に走る）。
    /// </summary>
    public static IReadOnlyList<Column> Columns { get; } = new[]
    {
        // 既存5波をそのまま並べたもの。初回計測（2026-08-25）の基準列。
        new Column("順路", "既存5波の並び順。第1期の基準",
            Stages.Select(s => s.Enemy).ToList()),

        // 逆順。**第1削りを情報のある列にするための列。**
        // 順路では第一波が全編成必勝で `第1削り` が一律 100% になり、特攻隊（勝てないが削る編成）を
        // 判別できなかった（README 未解決の課題）。敵を新造せず、並べ替えだけで測定条件を作る。
        // ステージ定義のコメントにある教育的意図（範囲攻撃をここで教える等）は独立5戦の
        // 提示順の話で、Stages 自体は触っていないから矛盾しない。この列は計測専用。
        // 突破数の列は第2期に測って捨てた——逆順は全編成が 0 か 1 抜きで初戦＝第五波の勝敗しか
        // 測らず、docs/balance.md の第5波（独立勝率）の測り直しにしかならない（第3期 §0-2）。
        new Column("逆順", "強い波が先頭。第1削り専用（突破数は第五波の独立勝率の測り直しにしかならない）",
            Stages.Reverse().Select(s => s.Enemy).ToList()),

        // 3部隊。コンセプト上、マップ上の1地点は敵1〜3部隊（design/concept_wave_engagement.md §7）。
        // 順路の先頭3つを切り出す——**長さだけを変数にする**ため、中身も順序も順路と同じにしてある。
        new Column("地点", "順路の先頭3波。1地点の想定サイズ",
            Stages.Take(3).Select(s => s.Enemy).ToList()),
    };

    /// <summary>
    /// 会戦の敵部隊列の第1号（＝Columns[0]「順路」）。「5波を独立に戦う」と「5波を持ち越して
    /// 戦う」の差が、そのまま会戦導入の効き目になる。GodotApp が使っているので削除しない。
    /// 列を選べるようにするのは、どの列を標準にするかを計測結果で決めてから（別作業）。
    /// </summary>
    public static IReadOnlyList<Formation> EngagementColumn => Columns[0].Squads;
}
