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
        PlusText = "後ろの味方への攻撃を型を問わず肩代わりし、飲み込んだ力をその味方へ返す／倒れるとき、飲み込んだ量の一部が生存味方の回復になる（1戦に1度）",
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
        MinusText = "破裂は味方も巻き込み、巻き込んだ全員に火が点く。生きている間はほぼ無力",
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

    /// <summary>
    /// 責め苦のシガ。既存の「汚れ」（痺れ・IdleTurn）に付いた最初の読み手。
    ///
    /// 数値は仮置き。速さ3 は「遅いから捨てられた」層（ゴルム3・ドハ3）に合わせた
    /// テーマ選択であって**機能要件ではない**——責め苦の判定が二重条件（Stun / IdleTurn）
    /// なので、供給役より速くても遅くても読める（TormentTrait 参照）。
    ///
    /// 供給ゼロの編成では「1ターンおきにしか動けず、毎回 IdleTurn を差し出す駒」になる。
    /// それも仕様で、差し出したターンは号令（ガン）・据え（バン）が買い取る。
    /// </summary>
    public static readonly UnitDef Shiga = new()
    {
        Id = "shiga",
        Name = "責め苦のシガ",
        MaxHp = 52,
        Attack = 9,
        Speed = 3,
        Traits = new[] { TraitId.Torment },
        PlusText = "動きを封じられた敵を殴ると、同じ重さの追い打ちを重ねる",
        MinusText = "動ける敵を殴ると、怖気づいて自分が1ターン動けなくなる",
        Flavor = "縛られた的しか殴れない臆病者。だからこそ、縛る者の隣でだけ牙になる。"
    };

    /// <summary>
    /// 仇討ちのザン。標的（Marked）に初めて付いた読み手。
    ///
    /// 数値は仮置き。速さ5 は「反撃役だが手番も持つ」帯（カド＝不動とは違う）。
    /// 標的の書き手はヒサ1体だけなので、**ヒサと同居しなければ完全に置物**——
    /// プラス側が編成依存で、マイナス側（怯み）だけが必ず働く。それが値段。
    ///
    /// 怯みは痺れに乗っているので、破片（ヒビ）を配られていると受け切った被弾では
    /// 怯まない（AvengeTrait 参照）。破片に初めて実質的な読み手が付く組み合わせ。
    /// </summary>
    public static readonly UnitDef Zan = new()
    {
        Id = "zan",
        Name = "仇討ちのザン",
        MaxHp = 56,
        Attack = 10,
        Speed = 5,
        Traits = new[] { TraitId.Avenge },
        PlusText = "標的にされた味方が殴られるたび、殴った者へ割り込んで刃を返す",
        MinusText = "自分が殴られると怖気づき、次の手番を失う。怯んでいる間は刃も返せない",
        Flavor = "仲間が殴られた時だけ勇敢になれる。自分が殴られると、そこにはもう誰もいない。"
    };

    /// <summary>
    /// 裂きのキリ。傷（Wound）の**供給源**。物理側に初めて置いた「盤面に残る汚れ」の書き手。
    ///
    /// **速さ12 は機能要件**——セロ・ササと同格の最速帯に置くことで「刻んでから読み手が動く」
    /// 順序を配置ではなく速度で作る。第26期・第27期に続いて**速さが条件タグとして機能する
    /// 3例目**（トウの粉・ザンの帯に続く）。エグ（速6）と対で1つの要件なので、
    /// どちらかだけ動かすと順序が崩れる。
    ///
    /// <c>Attack = 12</c> は <see cref="RendTrait.ModifyAttack"/> が引数ごと捨てるので
    /// **盤面には一切効かない**——`docs/units.md` の一覧と PrototypeApp の表示にしか出ない。
    /// 呪詛の床として要る、という読みは**誤り**（<c>ModifyAttack</c> は <c>Def.Attack + AtkBonus</c> を
    /// 受け取ってから 1 を返すので、床は特性の側で閉じている）。0 にしても挙動は変わらないが、
    /// 12 を置いてあるのは**キリが「斬れない駒」ではなく「斬れるが断てない駒」だから**
    /// ——一覧で攻3のムドと並ぶと、捨てられた理由の系統が変わってしまう。
    ///
    /// 単独では毎ターン1ダメージしか出ない純粋な払い出しで、読み手（エグ）がいない編成では
    /// ほぼ無価値。それが値段——シガ・ザンと同じ可変コスト型で、「この駒をどう使うんだ」が
    /// 編成パズルそのものになる。
    ///
    /// **第29期に 48/受けるダメージ1.5倍 へ差し替えて測り、不合格だったので戻した**
    /// （経緯と反証は <see cref="RendTrait"/> の doc / README 第29期）。**この極端な
    /// マイナスが配置の値段を作っている**ので、予算を理由に緩めてはいけない
    /// ——緩めた版では席交換の価値が +15.1pt → +0.6pt に落ちた。
    /// 予算の側は第30期に**別の駒**（刻みのノミ。供給と出力を1枠に畳んだ形）で解く。
    /// </summary>
    public static readonly UnitDef Kiri = new()
    {
        Id = "kiri",
        Name = "裂きのキリ",
        MaxHp = 44,
        Attack = 12,
        Speed = 12,
        Traits = new[] { TraitId.Rend },
        PlusText = "攻撃した相手に傷を刻む",
        MinusText = "刃が薄く、与えるダメージは常に1",
        Flavor = "斬れるが、断てない。だから誰も戦力として数えなかった。"
    };

    /// <summary>
    /// 抉りのエグ。傷（Wound）の**読み手**。キリが刻んだ汚れを出力に変える変換器。
    ///
    /// 速さ6 はキリ（12）より確実に遅い。**刻む→抉るの順序を配置ではなく速度で保証する**
    /// （キリの速さ12 と対で1つの要件。どちらかだけ動かすと順序が崩れる）。
    ///
    /// マイナス（撃破すると次の手番を失う）は痺れ機構に乗せてあるので、
    /// 飛んだ手番は IdleTurn として号令（ガン）・据え（バン）に売れる。
    /// **エグ自身で倒し切るより、傷を積んで一撃で通すほうが強い**という勾配が
    /// 自己言及的に立つ——トドメを他の駒に譲る配置判断がそこから出る。
    /// </summary>
    public static readonly UnitDef Egu = new()
    {
        Id = "egu",
        Name = "抉りのエグ",
        MaxHp = 58,
        Attack = 12,
        Speed = 6,
        Traits = new[] { TraitId.Gouge },
        PlusText = "傷を持つ敵を攻撃すると、傷1つにつき3を上乗せする",
        MinusText = "敵を倒すと深追いし、次の手番を失う",
        Flavor = "開いた傷にしか興味がない。塞がった肌はただの壁だと言う。"
    };

    /// <summary>
    /// 刻みのノミ。傷（Wound）の**2つ目の入口**で、キリ（供給）＋エグ（変換）を1枠に畳んだ形。
    ///
    /// キリが「読み手がいる編成専用の極端な供給源」なら、ノミは
    /// **「単独でも回る代わりに連鎖が短い供給源」**。同じ資源に長い入口と短い入口を並べて、
    /// 編成に選ばせる——第29期に確定した「傷軸の予算不足は駒の単価の総和ではなく、
    /// **連鎖の長さが5枠に収まっていない**」への回答（README 第30期）。
    ///
    /// 上乗せは +2 で、エグ（+3）より低い。**畳んだぶんだけ単価を下げる。**
    ///
    /// マイナスは<b>対象選択の束縛</b>（<see cref="FixateTrait"/>）で、ロスター初の型。
    /// 出力も耐久も削らないので予算を食わないまま、**代金は敵の編成に依存して変動する**
    /// ——壁（軛の重装兵 145）に食いついたら他の敵に触れないままターンが流れる。
    ///
    /// 速さ7 は**エグ（6）より速くキリ（12）より遅い**。単騎で回る駒なので
    /// 「刻む→読む」の順序要件は自分の中で閉じており、速さに機能要件は無い
    /// ——キリ＋エグの順序（12 対 6）を壊さない帯に置いてあるだけ。
    ///
    /// **単体攻撃であることは執着の前提**。範囲型に変えると巻き込みの中心が固定されて
    /// マイナスの意味が変わる（<see cref="FixateTrait"/> の但し書き）。
    /// </summary>
    public static readonly UnitDef Nomi = new()
    {
        Id = "nomi",
        Name = "刻みのノミ",
        MaxHp = 52,
        Attack = 10,
        Speed = 7,
        Traits = new[] { TraitId.Carve, TraitId.Fixate },
        PlusText = "攻撃した相手に傷を刻み、相手の傷1つにつき2を上乗せする",
        MinusText = "一度狙った敵が倒れるまで、他の敵に目を向けられない",
        Flavor = "同じ場所を彫り続けることしかできない。彫り上がる頃には、戦は終わっている。"
    };

    /// <summary>
    /// 断ちのナタ。傷（Wound）の**消費型の終端**。エグ（維持読み）と同じ資源に、
    /// 「畳んで一撃で使う」もう1つの使い方を並べる（第37期）。
    ///
    /// **速さ5 は機能要件**——エグ（6）より確実に遅い。同じ傷をエグが読んでからナタが断つ順を、
    /// 配置ではなく速度で固定する（キリ12／エグ6 の対と同じ作法で、速さが条件タグとして
    /// 機能する4例目）。キリ（12）・ノミ（7）より遅いので「書く → 断つ」も同時に立つ。
    /// **エグ・キリ・ノミのいずれかを動かすと順序が崩れる**ので、3体と対で1つの要件。
    ///
    /// **待つのが仕様**（第38期）——狙える敵の最深の傷が <see cref="SeverTrait.Threshold"/>（2）に
    /// 達するまで刃を上げない。第37期は「傷が1つでもあれば振る」だったので毎ターン振ってしまい、
    /// 在庫が 1 のまま＝「消費型」なのに定額の上乗せとしてしか働かなかった
    /// （<c>傷/断ち</c> 1.00）。**待たせて初めて畳める**。
    ///
    /// マイナス（狙える敵の傷が浅い間は振らない）は<b>号令・据えに売れない</b>
    /// （<see cref="SeverTrait.SurrendersTurn"/> が false）。売れてしまうと、供給源を
    /// 1枚も持たない編成でナタが毎ターンの無償の収入源になり、マイナスが資産に化ける。
    ///
    /// **単体攻撃であることが標的選好の前提**（<see cref="SeverTrait"/> の窓口は
    /// 貫きを通らない）。範囲型に変えると選好が「巻き込みの中心の固定」に意味を変える。
    ///
    /// 攻13/HP60 は出力役の帯。**精密な釣り合いは第37期の対象外**（探索フェーズ）。
    /// </summary>
    public static readonly UnitDef Nata = new()
    {
        Id = "nata",
        Name = "断ちのナタ",
        MaxHp = 60,
        Attack = 13,
        Speed = 5,
        Traits = new[] { TraitId.Sever },
        PlusText = "狙える敵のうち傷が最も深い相手を狙う。攻撃した相手の傷をすべて断ち、1つにつき5を上乗せする",
        MinusText = "狙える敵の傷が2つ開くまで、刃を振るわず手番を捨てる",
        Flavor = "断てる。だが、閉じた肌には刃が入らない。誰かが開くまで、鉈はただの鉄塊だった。"
    };

    /// <summary>
    /// 縫いのハリ。傷（Wound）の**防御側の維持読み**で、傷軸の四役目（第39期）。
    ///
    /// 供給2（キリ＝広く薄く／ノミ＝1体へ積む）に対し、読み手はこれで3枚——
    /// **攻めの維持読み（エグ）／消費読み（ナタ）／防御の維持読み（ハリ）**。
    /// 同じ資源から3つの違う出口が出ることで、傷が「汚れ」ではなく通貨になる。
    ///
    /// **回復が `ctx.Heal` を通ることが設計の中心**——第三波（渇きの祭司）は持続回復に
    /// 課金する波なのに、ロスターに買い手が薄かった（第22/31/36期の残件）。
    /// 第36期の還し（腹の払い戻し）は**死亡時の一回きり**なので波の途中の封じが痛くならず、
    /// 第三波は17行中16行が ±0.0 だった。**足りなかったのは毎ターンの回復エンジン**で、
    /// それを編成が採用したくなる軸（傷）に結合した形がハリ。
    ///
    /// **渇き下では二重に払う**（回復の封じ ＋ 塞ぎによる傷の目減り）。仕様であって不具合ではない
    /// ——詳細は <see cref="SutureTrait"/> の doc。
    ///
    /// 速さ6 は**機能要件**——ノミ（7）より遅く＝書きの後に読む、ナタ（5）より速く＝消費の前に読む。
    /// エグ（6）と同速だが、**維持読み同士は非破壊なので順序に意味が無い**（タイブレークは乱数のまま）。
    /// 速さが条件タグとして機能する5例目（キリ12／エグ6／ノミ7／ナタ5 と対で1つの要件）。
    ///
    /// 攻10・HP54 は支援帯だが**攻撃はする**。ノノ（継ぎ当て）は手番を丸ごと繕いに使うのに対し、
    /// ハリは殴った先の傷から繕う——**手番を1つも余分に使わない回復**がノノとの差別化。
    ///
    /// **ナタとは同居させない**（塞ぎ 1/T が供給 1/T と等速なので在庫が閾値 2 に届かず、
    /// ナタが構造的に飢える）。エグとの同居は予算壁の側で止まる。計算は <see cref="SutureTrait"/>。
    /// </summary>
    public static readonly UnitDef Hari = new()
    {
        Id = "hari",
        Name = "縫いのハリ",
        MaxHp = 54,
        Attack = 10,
        Speed = 6,
        Traits = new[] { TraitId.Suture },
        PlusText = "傷が最も深い敵を狙い、その傷1つにつき最も傷ついた味方を3繕う",
        MinusText = "繕うたび、糸を通した敵の傷がひとつ塞がる",
        Flavor = "敵の傷口に糸を通して、味方を縫い戻す。敵まで塞ぐ針を、軍は疫病神と呼んで捨てた。"
    };

    /// <summary>
    /// 突き返しのハネ。<b>移動を読み、弱体化を書く</b>変換器（第41期）。
    ///
    /// <para><b>ヨミに依存しない移動の読み手</b>として足した。第40期の曝きは移動の供給を
    /// 大きく増やしたのに（<c>HasFallenBack</c> 0.26 → 2.15 回/戦）、恩恵を受けた 3 行は
    /// 全部ヨミとガルドを共有する実質1クラスタだった。読み手を増やせば余剰が回収される。</para>
    ///
    /// <para><b>出口は弱体化。</b> <c>AtkBonus</c> を負にする経路はロスターに3つしかなく、
    /// 2つは開戦時1回きり（呪詛の味方漏れ・萎縮）、残る1つ（分かちの「腕がなまる」）は
    /// 現行 44 行でウツと同席していない。読み手（逆しま）の変換係数は3倍と大きいのに
    /// <b>戦闘中の供給がゼロ</b>だったので、リィカの層やムドの怒りに相当する
    /// 「弱体化を経由した成長」がロスターに存在しなかった。</para>
    ///
    /// <para>攻11・HP56・速8 は探索段階の初期値。<b>掃引の対象は
    /// <see cref="ShoveRule.Penalty"/> だけ</b>で、HP・攻・速は振らない
    /// （1変数を振るときに一緒に動かすものを増やさない）。</para>
    /// </summary>
    public static readonly UnitDef Hane = new()
    {
        Id = "hane",
        Name = "突き返しのハネ",
        MaxHp = 56,
        Attack = 11,
        Speed = 8,
        Traits = new[] { TraitId.Shove },
        PlusText = "味方が押しのけられるたび、敵の隊列を突き崩す",
        MinusText = "勢い余って隣の味方の体勢まで崩す（攻撃力が下がる）",
        Flavor = "押されたら押し返す。それしかできないし、加減も知らない。"
    };

    /// <summary>
    /// 引き受けのウケ。<b>状態の肩代わり</b>——弱体を横取りしてアーマーに変える（第42期）。
    ///
    /// <para><b>肩代わりは5種あって全部ダメージだった</b>（庇う・分かち・巨躯・後備え・棘守り）。
    /// 状態を肩代わりするものが1つも無かったので、ここが完全な空白地。</para>
    ///
    /// <para><b>同じ通貨をウツと逆向きに使う。</b> 逆しま（ウツ）は弱体を攻撃力
    /// （下げ幅の3倍）に、引き受け（ウケ）はアーマーに変える。傷軸で維持攻（エグ）と
    /// 維持防（ハリ）が波ごとに別の順位を作ったのと同じ形を、弱体で作る。</para>
    ///
    /// <para>HP90・攻6・速5 は探索段階の初期値。<b>掃引の対象は
    /// <see cref="BearRule.ArmorPerDull"/> だけ</b>で、HP・攻・速は振らない。</para>
    /// </summary>
    public static readonly UnitDef Uke = new()
    {
        Id = "uke",
        Name = "引き受けのウケ",
        MaxHp = 90,
        Attack = 6,
        Speed = 5,
        Traits = new[] { TraitId.Bear },
        PlusText = "隣の味方が受ける攻撃力低下を代わりに背負い、その分だけ鎧になる",
        MinusText = "背負った分だけ自分の腕は落ちる。隣に誰もいなければ何も起きない",
        Flavor = "誰の痛みでも代わりに背負う。頼まれてもいないのに。"
    };

    /// <summary>
    /// 渡しのワタ。<b>味方の弱体を敵へ流す</b>——弱体軸の三役目（第43期）。
    ///
    /// <para><b>味方から敵へ状態を移す経路はロスターでこれが初めて。</b>
    /// 第40期の曝き（告発人）が「敵から味方へ」を作ったのの逆向きで、
    /// 窓口 <see cref="BattleContext.Dull"/> が最初から両陣営を通るように
    /// 作ってあるので engine 側に足した規則はゼロ。</para>
    ///
    /// <para><b>逆しま（ウツ）・引き受け（ウケ）と同じ通貨を、3つ目の向きで使う。</b>
    /// 前2枚は増幅がある代わりに<b>受け手1体で閉じる</b>（攻撃力3倍・アーマー2倍）。
    /// 渡しは<b>増幅が無い代わりに味方全体に効く</b>——敵の攻撃力が下がるので
    /// 殴られる全員が得をする。</para>
    ///
    /// <para>HP84・攻8・速7 は探索段階の初期値。<b>掃引の対象は
    /// <see cref="RelayRule.TransferPercent"/> だけ</b>で、HP・攻・速は振らない。</para>
    /// </summary>
    public static readonly UnitDef Wata = new()
    {
        Id = "wata",
        Name = "渡しのワタ",
        MaxHp = 84,
        Attack = 8,
        Speed = 7,
        Traits = new[] { TraitId.Relay },
        PlusText = "隣の味方が受ける攻撃力低下を引き受け、最も強い敵へそのまま渡す",
        MinusText = "渡した分だけ自分の身が削れる。隣に誰もいなければ何も起きない",
        Flavor = "受け取った呪いを、そのまま向こうへ渡す。通り道の自分だけが削れていく。"
    };

    /// <summary>
    /// 驕りのオゴ。<b>隣接を「量」ではなく「誰がいるか」で読む2枚目</b>（第46期）。
    ///
    /// <para><b>狙いは第45期の残件 B。</b> 第45期は「隣接を単調な量（隣に何人いるか）で読む駒は
    /// 席が定数になる」と結論したが、反例はロスターに1枚しかない非単調な読み手
    /// （囃し立てのヒサ＝隣で最大HPの1体を選ぶ）だけで、n=1 だった。
    /// <b>オゴは条件を隣接する味方<i>全員</i>への AND にする</b>ので、次数が増えるほど成立が
    /// 遠のき、しかも「隣が誰か」で成立時刻が変わる——単調な量では書けない形。</para>
    ///
    /// <para><b>出口は弱体化。</b> 削りは <see cref="BattleContext.Dull"/> を通るので、
    /// ウケ（アーマー化）・ワタ（敵へ転嫁）と組めば「見下すこと」がそのまま収入になる。
    /// ただし<b>ウツ（逆しま）を隣に置くと削るほど相手が強くなり、自分の条件から遠ざかる</b>
    /// ——<b>同じ通貨の供給者と読み手が隣り合えない</b>という自己矛盾は設計として正しく、潰さない。</para>
    ///
    /// <para>攻12 は<b>ロスターの分水嶺</b>として選んだ値。12 未満が 40 枚、12 以上（＝隣に置くと
    /// 条件を壊す駒）が キリ12・エグ12・ナタ13・ハギ16・ボルグ18・ドルガ38 の 6 枚になる。
    /// HP62・速5 とあわせて探索段階の初期値で、<b>掃引の対象は
    /// <see cref="OverbearRule.Drain"/> だけ</b>（1変数を振るときに一緒に動かすものを増やさない）。</para>
    /// </summary>
    /// <remarks>
    /// <b><see cref="All"/> には載せていない。</b> 第46期は驕りを測って<b>採用しなかった</b>ので、
    /// 定義だけを対照として残してある（逆位・まどろみ・誹りと同じ扱い）。
    /// 診断 <c>overbear</c> が編成をローカルに組んで使う。
    /// </remarks>
    public static readonly UnitDef Ogo = new()
    {
        Id = "ogo",
        Name = "驕りのオゴ",
        MaxHp = 62,
        Attack = 12,
        Speed = 5,
        Traits = new[] { TraitId.Overbear },
        PlusText = "隣の味方が全員自分より弱いあいだ、攻撃力が2倍になる",
        MinusText = "毎ターン隣の味方の腕を鈍らせる。隣に誰もいなければ本気も出せない",
        Flavor = "自分より下がいないと力が出ない。だから下を作ることにした。"
    };


    /// <summary>
    /// 鱗のウロ。<b>アーマーに初めての読み手を作る</b>（第47期）。
    ///
    /// <para><b>アーマー（<c>StatusKeys.Armor</c>）は7つの盤面状態キーの中で読み手が0枚だった
    /// 唯一の資源。</b> 書き手は砕け（ヒビ）と集約（ウケ）の2つあるのに、消費するのは
    /// <c>ApplyDamage</c> だけで、誰も条件として読んでいなかった（第47期 Phase 0-1 の表）。</para>
    ///
    /// <para><b>隣接を1つも読まない。</b> 第45〜46期で隣接は2期かけて否定的な結論が出ている
    /// （隣接を読む駒の席の最頻率 85%、驕りは 100%）。ウロが読むのは<b>自分が纏っている量</b>
    /// なので、席の問題から完全に外れる。</para>
    ///
    /// <para><b>供給・発揮・消費の1サイクルが1枚に入る。</b> 味方が倒れるたび破片を拾い
    /// （<see cref="ScaleTrait.GainPerDeath"/> = 4）、纏っているあいだ攻撃が貫きになり、
    /// 振るたび剥がれる。<b>アーマーは被弾でも削られるので二重支出</b>で、
    /// これが「盾を削って刃にする」1つの動作の表と裏になっている。</para>
    ///
    /// <para><b>ロスターに常時の貫きは1枚も無い</b>（<c>Def.Pattern</c> は 単体43 / 薙ぎ3 /
    /// 貫き0 / 全体0）。貫きは <c>ModifyPattern</c> を通してしか立たず、既存は3つとも
    /// 外部条件付き（後衛特化＝後退済み かつ 後列／熾火＝燃えている間）。
    /// ウロは4つ目で、条件が<b>資源の残高</b>である初めての例。</para>
    ///
    /// <para>HP70・攻9・速7 は探索段階の初期値。<b>掃引の対象は
    /// <see cref="ScaleRule.CostPerAttack"/> だけ</b>で、HP・攻・速は振らない
    /// （<see cref="ScaleTrait.GainPerDeath"/> も定数。1変数を振るときに一緒に動かすものを増やさない）。</para>
    /// </summary>
    public static readonly UnitDef Uro = new()
    {
        Id = "uro",
        Name = "鱗のウロ",
        MaxHp = 70,
        Attack = 9,
        Speed = 7,
        Traits = new[] { TraitId.Scale },
        PlusText = "味方が倒れるたび破片を纏う。纏っているあいだ攻撃が貫きになる",
        MinusText = "振るたびに剥がれ、殴られても剥がれる。自分では破片を作れない",
        Flavor = "倒れた者の欠片を拾って身に着ける。誰も惜しまないから、いくらでも拾える。"
    };

    /// <summary>
    /// 業のゴウ。<b>ロスターで初めて「状態異常の種類数」を読む駒</b>（第49期）。
    ///
    /// <para><b>第48期の棚卸しで見えたのは、すべての軸が「同じ通貨を厚くする」方向を
    /// 向いていること</b>だった——毒5枚・死9枚・弱体5枚に対して、<b>幅を要求する駒が1枚もない</b>。
    /// ゴウはそこを埋める。読むのは量ではなく<b>種類</b>で、
    /// <b>マイナス特性の多様性そのものが力になる</b>初めての形。</para>
    ///
    /// <para><b>味方に載る状態異常は4種類しかない</b>（第49期 Phase 0-1 の実測）——
    /// 毒（瘴気の味方漏れ／毒撃の隣への漏れ）・標（囃し立て）・痺（縛め／怯み／怖気／深追い／
    /// 敵の断罪）・燃（火の粉）。<b>傷は味方に載る経路が1つも無い</b>ので実効の分母は 4 で、
    /// 閾値 3 は「4つのうち3つ」を要求する。</para>
    ///
    /// <para><b>痺は構造的に「使えない種類」。</b> 引き取りは <c>OnTurnStart</c>（行動順ループの
    /// 外側）なので、痺を引き取ったターンのゴウは手番を飛ばす＝転写できない。
    /// 引き取りと発揮が同じ資源を奪い合う——これは設計として正しい
    /// （<see cref="ScapegoatTrait"/> の doc を参照）。</para>
    ///
    /// <para><b>標を引き取ると自分が的になる。</b> <c>BattleEngine.MarkPullPercent</c> = 75 は
    /// 陣営を問わないので、囃し立てが味方に付けた標をゴウが背負うと、敵の単体攻撃の
    /// 75% がゴウへ向く。<b>身代わりとしてテーマに合う</b>と同時に、この駒の最大の代金でもある。</para>
    ///
    /// <para>HP88・攻7・速4 は探索段階の初期値。<b>掃引の対象は
    /// <see cref="ScapegoatRule.Threshold"/> だけ</b>で、HP・攻・速は振らない
    /// （<see cref="ScapegoatTrait.TransferAmount"/> も定数）。速4 と遅くしてあるのは、
    /// 痺れを引き取っても失うものが小さいため。</para>
    /// </summary>
    /// <remarks>
    /// <b><see cref="All"/> には載せていない。</b> 第49期は業を測って<b>採用しなかった</b>ので、
    /// 定義だけを対照として残してある（逆位・まどろみ・誹り・驕りと同じ扱い）。
    /// 診断 <c>scapegoat</c> が編成をローカルに組んで使う。
    ///
    /// <para><b>棄却の理由は3つ</b>（design/PHASE49_SCAPEGOAT.md）:</para>
    /// <list type="number">
    ///   <item><b>設計した機構（種類数を読んで転写する）の値段が 1.7pt。</b>
    ///     閾値を 2 → 4 に振ると転写は 0.58 → 0.00 回/戦とゼロになるのに、勝率は 1.7pt しか動かない。</item>
    ///   <item><b>味方に載る種類は4つしかなく、しかも痺は使えない。</b> 引き取りは
    ///     <c>OnTurnStart</c>（行動順ループの外側）なので、痺を引き取ったターンは手番が飛んで
    ///     転写できない。実効の分母は 毒・標・燃 の3つで、閾値3は<b>「使える種類の全部」</b>を
    ///     要求する——「幅を要求する駒」にはならず、ただの三重条件になった。</item>
    ///   <item><b>供給を3種類そろえた台は 20.0%（100/0/0/0/0）に潰れる。</b>
    ///     Phase 0-7 で 29 通りの候補を測って、生き残ったのは反撃台に乗せた形だけだった。</item>
    /// </list>
    ///
    /// <para><b>ただし引き取りの半分だけは大きく効いた。</b> ゴウが囃し立ての標を引き取ると
    /// <b>反撃役（カド）から標を奪う</b>ので、反撃が得な波（第四波）では −18〜−23pt、
    /// 反撃役が先に潰される波（第五波）では +19〜+22pt——**符号が波で反転し、別 seed 帯でも再現する**。
    /// 種類数とは無関係な効果なので、残件として design/PHASE49_SCAPEGOAT.md に切り出してある。</para>
    /// </remarks>
    public static readonly UnitDef Gou = new()
    {
        Id = "gou",
        Name = "業のゴウ",
        MaxHp = 88,
        Attack = 7,
        Speed = 4,
        Traits = new[] { TraitId.Scapegoat },
        PlusText = "毎ターン味方の状態異常を1つ引き取る。種類が3つ揃うと、殴った相手に全部返す",
        MinusText = "引き取った呪いは自分に効く。味方が綺麗なら何も起きない",
        Flavor = "他人の呪いを引き取って歩く。溜め込んだものは、いつか誰かに返す。"
    };

    /// <summary>
    /// 逸らしのソラ。<b>ロスターで初めて標（<c>StatusKeys.Marked</c>）を操作する駒</b>（第50期）。
    ///
    /// <para><b>標は engine が常時読んでいる強い通貨なのに、盤面での操作手段が無かった。</b>
    /// 書き手は囃し立て（ヒサ）1枚で「隣接する最大HPの味方1体に開戦時1回」——選択の余地がゼロ。
    /// <b>消す経路は1つも無い</b>（第50期 Phase 0-3）。engine の窓口
    /// （<c>MarkPullPercent</c> = 75）は<b>すべての単体攻撃</b>で評価されるので、
    /// 駒の読み手が1枚しかなくても盤面への影響は大きい。</para>
    ///
    /// <para><b>1つの動作の表と裏。</b> 毎ターン、味方の標を外し（プラス）、自分に付け（マイナス）、
    /// 敵陣で現在HPが最も高い <c>DivertRule.TargetCount</c> 体へ付ける（プラス）。
    /// <b>代金に特別な実装は無い</b>——標を負うこと自体が代金で、回避率も被ダメ軽減も持たせていない。</para>
    ///
    /// <para><b>標は鎖の最初にある</b>（標 → 後備え → 庇う → 殉教 → 棘守り）ので、
    /// <b>敵に標を付けると庇い・後備え・殉教をすべて飛び越して狙い撃てる</b>
    /// ——第五波の殉教者（庇う 75%）との干渉がこの駒の効き所。</para>
    ///
    /// <para>HP96・攻6・速8 は探索段階の初期値。<b>掃引の対象は
    /// <c>DivertRule.TargetCount</c> だけ</b>で、HP・攻・速は振らない。
    /// HP96 と厚めにしてあるのは、毎ターン標を負うため。</para>
    /// </summary>
    public static readonly UnitDef Sora = new()
    {
        Id = "sora",
        Name = "逸らしのソラ",
        MaxHp = 96,
        Attack = 6,
        Speed = 8,
        Traits = new[] { TraitId.Divert },
        PlusText = "毎ターン味方に向いた視線を引き剥がし、いちばん硬い敵へ向け直す",
        MinusText = "引き剥がした視線は自分に刺さる。毎ターン狙われ続ける",
        Flavor = "誰かが見られている限り、代わりに見られてやる。それしか取り柄がない。"
    };


    /// <summary>
    /// 駆り立てのカリ。<b>ロスターで2枚目の標（<c>StatusKeys.Marked</c>）の書き手</b>（第52期）。
    ///
    /// <para><b>設計の出発点は囃し立てのヒサ（44/攻2/速10）だった。</b> ヒサは
    /// <c>PlusText</c> が「隣接する味方1体に敵の攻撃を集中させる」——<b>プラス欄に書いてあるのが
    /// 味方への害</b>で、発火は開戦時1回・対象は隣接する最大HPの味方に固定。盤上で何も起きず、
    /// 拾う理由が無い。対して縛めのクグ（54/攻3/速10）は「毎ターン味方1体を縛る」という害の中に
    /// <b>「その味方の攻撃+16」</b>が埋まっていて、`縛め収入型` という行が主判定に立っている。
    /// <b>カリはクグ側の構造で作ってある。</b></para>
    ///
    /// <para><b>設計原則</b>: <b>マイナスは編成のフックであって、その駒を入れる動機ではない。
    /// 動機は別に要る。</b> 味方を犠牲にするだけの駒は盤面を弱くするので、打点で釣り合わせても
    /// 「入れるほど損」になりやすい——<b>害の中に見返りを埋めるのが、この盤面で機能している
    /// 唯一の形</b>（クグが前例）。</para>
    ///
    /// <para><b>1つの動作の表と裏。</b> 毎ターン、隣接する生存味方のうち <c>CurrentAttack</c> が
    /// 最も高い1体を選び、<b>標を付け（マイナス）、<c>GoadRule.Boost</c> を渡す（プラス）</b>。
    /// 前ターンの対象からは標を外すが<b>強化は残す</b>——「一度渡した力は返らないが、矛先は移る」。
    /// <b>隣に誰もいなければ何も起きない</b>（自己完結しない）。</para>
    ///
    /// <para><b>選び方が「最高攻撃力」なので、強化するほどその駒が選ばれ続ける</b>
    /// ——強化と危険が同じ1体に集中し、前線が1枚できる代わりにその1枚が死ぬ。
    /// <b>ただし逆しま（ウツ）だけは自己修正する</b>（強化されると <c>PerverseTrait</c> が
    /// 攻撃力を半減するので、次のターンには選ばれにくくなる）。</para>
    ///
    /// <para>HP62・攻4・速9 は探索段階の初期値。<b>掃引の対象は <c>GoadRule.Boost</c> だけ</b>で、
    /// HP・攻・速は振らない。<b>攻4 と低いのは、貢献が他人を強くすることだから。</b></para>
    /// </summary>
    public static readonly UnitDef Kari = new()
    {
        Id = "kari",
        Name = "駆り立てのカリ",
        MaxHp = 62,
        Attack = 4,
        Speed = 9,
        Traits = new[] { TraitId.Goad },
        PlusText = "毎ターン隣のいちばん殴れる味方に自分の力を渡す（攻撃力が上がり続ける）",
        MinusText = "渡した相手を前に押し出すので、その味方は狙われる。隣に誰もいなければ何もできない",
        Flavor = "前に出ろとしか言えない。言い方を知らないだけで、渡せるものは全部渡している。"
    };


    /// <summary>
    /// 止めのトメ。<b>ロスターで初めて「敵に付いた標」を読む駒</b>（第53期）。
    ///
    /// <para><b>標の書き手は第52期に3枚になったのに、読み手は仇討ち（ザン）1枚のままだった</b>
    /// ——しかもザンが読むのは<b>味方</b>の標。第50期にソラが敵へ標を付けられるようになったのに、
    /// <b>それを読む駒が1枚も無い</b>のがこの期に埋めた空白（第42期の弱体・第47期のアーマーと同じ形で、
    /// どちらも供給が厚くなった直後に読み手を足して軸になった）。</para>
    ///
    /// <para><b>敵の標は「焦点」であり、列の壁を越えて届く経路。</b> engine の
    /// <c>SelectTargetChain</c> は標の段だけを <c>pool</c> ではなく <c>foes</c> から選ぶので、
    /// <b>標を持つ敵は前列が生きていても狙える</b>。トメはその段を 100%・決定的にする
    /// ——<b>この駒の価値は倍率ではなく列越えのほうかもしれない</b>ので、
    /// 診断は「発火」と「列越え」を必ず分けて数える。</para>
    ///
    /// <para><b>ザンと逆の手番の持ち方をする。</b> ザンは <c>CanActOutOfTurn</c> を通る
    /// ターン外の駒なので粛（第二波）に封じられるが、<b>トメは自分の手番でしか働かないので
    /// 粛の非対象</b>。第51期の「効いているのは窓口ではなく手番の持ち方」に従って、
    /// <b>同じ通貨を、逆の陣営で、逆の手番の持ち方で読む</b>形にしてある。</para>
    ///
    /// <para><b>1つの動作の表と裏。</b> 標を持つ敵に必ず食らいつき（倒しきれない相手にも）、
    /// 倍の力で殴り（プラス）、殴った後にその標を消す（マイナス）。
    /// <b>消すと engine の <c>MarkPullPercent</c> も切れる</b>ので、
    /// <b>味方全体の集中砲火を自分で終わらせてしまう</b>——これが倍率の見返りに対する代金。</para>
    ///
    /// <para><b>供給はソラ1枚しかない</b>（敵に標を付けられるのはソラだけ）ので、
    /// <b>ソラ抜きでは素の攻12として振る舞う</b>——第47期のウロ（アーマーの供給が砕け1枚）と同型。</para>
    ///
    /// <para>HP58・攻12・速6 は探索段階の初期値。<b>掃引の対象は
    /// <c>FinisherRule.Multiplier</c> だけ</b>で、HP・攻・速は振らない。</para>
    /// </summary>
    public static readonly UnitDef Tome = new()
    {
        Id = "tome",
        Name = "止めのトメ",
        MaxHp = 58,
        Attack = 12,
        Speed = 6,
        Traits = new[] { TraitId.Finisher },
        PlusText = "標的にされた敵を必ず狙い、倍の力で殴る（列の壁を越えて届く）",
        MinusText = "殴ると標的が外れるので味方の集中砲火が止まる。誰も指差さなければただの雑魚",
        Flavor = "誰かが指を差した相手にしか本気になれない。差されれば、確実に仕留める。"
    };

    /// <summary>
    /// 焚き付けのフイ。<b>ロスターで初めて「味方に付いた燃焼」を読む駒</b>（第58期）。
    ///
    /// <para><b>第57期の実測が出発点。</b> 燃焼は<b>他の9通貨と双方向とも接続 0</b> の閉じた2枚組で
    /// （表E の 18 セル）、しかも<b>着火の 52% は味方に付いている</b>（味 2.40 対 敵 2.21 /戦）。
    /// <b>その味方側の火を読む駒が1枚も無い</b>ので、ボルグを含む8行のうち3行では
    /// 火の粉が純粋な代金（−3.8〜−7.9pt）として払われっぱなしになっていた。
    /// <b>この駒はその在庫を強化（<see cref="WhetRoute.Kindle"/>）と
    /// 弱体（<see cref="DullRoute.Kindle"/>）へ変換する。</b></para>
    ///
    /// <para><b>1枚で2箇所に接続する。</b> 強化の供給は第56期の実測で
    /// <b>48.3% が吐き戻し1本</b>＝「ゴルムを入れると勝手に付いてくる通貨」で、
    /// <b>編成が選べる供給</b>が薄かった。燃焼を繋ぐと、その空白も同時に埋まる。</para>
    ///
    /// <para><b>1つの動作の表と裏。</b> 毎ターン、<b>燃えている味方全員</b>（位置を問わない）を
    /// 強化し、<b>自分に隣接する燃えていない味方</b>（位置で決まる）を鈍らせる。
    /// <b>プラスを全体・マイナスを隣接にするのが要点</b>——逆にすると「隣に火があるか」の
    /// 二値になって配置の判断が消える。この形なら
    /// 「火を全体に回すか、自分の隣を空けるか」の2つの解き方が同時に立つ。</para>
    ///
    /// <para>HP70・攻5・速6 は探索段階の初期値。<b>掃引の対象は
    /// <see cref="KindleRule.Gain"/> / <see cref="KindleRule.Loss"/> だけ</b>で、HP・攻・速は振らない。
    /// <b>攻5 と低いのは意図的</b>——出力で効いてしまうと、接続が効いたのか体が効いたのかが
    /// 分からなくなる（第34期「1変数を振るときは、その変数が他に何を一緒に動かすかを先に数える」）。</para>
    /// </summary>
    public static readonly UnitDef Fui = new()
    {
        Id = "fui",
        Name = "焚き付けのフイ",
        MaxHp = 70,
        Attack = 5,
        Speed = 6,
        Traits = new[] { TraitId.Kindle },
        PlusText = "燃えている味方の攻撃力を毎ターン上げる（位置を問わない）",
        MinusText = "自分の隣で燃えていない味方は、逆に腕が鈍る",
        Flavor = "火のそばにいる者しか目に入らない。えこひいきで隊を割った。"
    };

    public static IReadOnlyList<UnitDef> All { get; } = new[]
    {
        Borg, Mudo, Sero, Nel, Gald, Rica, Golm, Dolga, Mug, Zoto, Vel, Sid, Kado, Hisa, Nono, Mio, Rau, Guza, Tou, Beni, Gan, Vio, Yomi, Basa, Kugu, Ban, Shio, Utsu, Doha, Sasa, Kubi, Hagi, Sekki, Hota, Hibi, Nara, Shiga, Zan, Kiri, Egu, Nomi, Nata, Hari, Hane, Uke, Wata, Uro, Sora, Kari, Tome, Fui
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
    // 誹りの巡礼騎士（第44期）。**第二波の前3に置く。同じ波の前1にいる巡礼騎士（KnightG）と
    // 数値・型・速さが1つも違わない。**差分は誹り（Slander）を1つ足しただけ——渇き・軛・粛・
    // 殉教・曝きと同じ形で、差分を特性だけに閉じ込めるため。KnightG は前1で使われ続けるので
    // 定義はそのまま残る（差し替えれば盤面が完全に元へ戻ることの対照になる）。
    //
    // **対照が同じ波の中に残るのが第40期の告発人より良い条件。** 告発人（Knight2 と同数値）は
    // 対照が波の外にあったが、こちらは前1と前3に同じ数値の2体が並ぶので、
    // 「誹りを持つ側と持たない側が同じ戦闘の中で同じだけ殴られる」形になる。
    //
    // **第二波を選ぶ理由は天井の多さ。** 48行中19行が 100.0% で情報セル 23 は全波中最少
    // ——剥がす余地が最も大きい。中央は Husher（粛）で埋まっていて、第一波を除く全波の中央が
    // 既に盤面ルールの保持者なので、そもそも中央は空いていない。
    //
    // **前3は前1と等価な席**（角4つは隣接次数2で完全に等価）なので、席そのものに意味は無い。
    // 意味があるのは「前列である」ことだけ——前列は開幕から殴られるので、
    // **供給の総量は保持者の生存ターン数で決まる**（診断の「早逝」列）。
    //
    // **測って採用しなかった**（第44期）。逆位（InversionTrait）と同じ扱いで、
    // **`Stages` には載せずに定義だけ残してある**——診断 `slander` が波をローカルに組んで使う。
    // 機構は完全に動く（読み手に届き、ウケは 100% アーマー化し、ワタは撒いた本人へ流し返す）が、
    // **符号反転が再現しない**——第二波で上がった行は 48 行中1行（+1.0pt）だけで、
    // それも別 seed 帯（200..599）で −2.0pt に割れた。残り21行は全部下がり、符号は全部保つ。
    // **一様な課税**であって波の分離ではない。詳細は design/PHASE44_SLANDER.md。
    public static readonly UnitDef Slanderer =
        Make("knight_slander", "誹りの巡礼騎士", 75, 24, 7, TraitId.Slander);

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

    // 殉教者: **第五波の前1に採用した**（2026-08-31・第33期）。戦斧兵1枚と差し替えてある。
    // **数値・型・速さは戦斧兵（52/11/5・薙ぎ）と1つも違わない。庇うを1つ足しただけ**
    // ——渇き・軛・粛と同じ形で、差分を特性だけに閉じ込めるため。
    //
    // **敵に Guardian を持たせるのはロスターで初めて。** 第30期に「庇うで執着を引き剥がす」を
    // 検証したときは、庇う持ちの敵がいないので**一時的な敵を第三波へ差し込んで**測っていた
    // （README「『庇うで執着を引き剥がす』が、規則を1つも足さずに立った」）。同じ節に書いた
    // 「記憶した敵が後列に取り残されたら解ける経路は現在の敵ロスターでは到達不能」も、
    // この駒で初めて製品の波に出る。
    //
    // 課金する資源は「**対単体攻撃への依存**」。庇うは Single にしか割り込まない
    // （BattleEngine.SelectTargetChain の `pattern != Single` 分岐で薙ぎ・全体は手前で返り、
    // 貫きは SelectPierceEntry へ早期分岐する）ので、**単体打点に寄った編成だけが高く付く**。
    // 第二波は後列到達力とターン外の手数（粛）、第三波は持続（渇き）、第四波は1発の重さ（軛）
    // なので、どの波とも重ならない1本になる。
    //
    // **中央には置けない。** FormationRules.RowOf の表は Front/Front/Mid/Back/Back なので
    // center は Row.Mid で、庇うの `f.Row == Row.Front` を通らない。渇き・軛・粛が中央を
    // 選んだ理由（どちらの列を貫いても必ず通る）は庇うでは使えず、置き場所は前1 か前3 の二択。
    // 前3 の勇者候補に載せないのは、波の最大打点と断罪と介入が1枚に重なると
    // 何に課金したのかが分離できなくなるため（断罪の担い手数 2 も動かさない）。
    //
    // **窓は「前列2枚がともに生きている間」だけ。** 庇うの条件が `f != target` なので、
    // 勇者候補が落ちると pool が {殉教者} だけになって庇いは走らなくなる。しかも庇うほど
    // HP52 の殉教者へ打点が集中するので、**庇うほど窓が早く閉じる自己終息型**。
    //
    // **肩代わりで育つ**（RedirectGainTrait.DamagePerGain = 2）。逸れた被弾2につき攻撃+1 で、
    // 殉教者は薙ぎなので前1・前3・中央 の3枠に返る——**単体の1発が大きいほど二重に払う**。
    // 素の攻11 は味方後列の HP を1発では抜かないが、伸びた分がそのまま3枠に乗る。
    // 育つのは肩代わりで受けた分だけ（PendingKey の印が立った被弾のみ）で、
    // 毒・燃焼のような source が null の継続ダメージでは育たない。
    public static readonly UnitDef Martyr = new()
    {
        Id = "axeman_g", Name = "殉教者", MaxHp = 52, Attack = 11, Speed = 5,
        Traits = new[] { TraitId.Martyr }, Pattern = AttackPattern.Sweep
    };
    // 断罪は審問官と勇者候補の2体で持つ。1体だとカドの反撃が担い手を先に殺して罰が消える
    // （審問官 HP76 / 単独だと配置を変えるだけで第5波 97.5% まで戻った）。
    // 3体にすると今度はカド系が全部20%台まで落ちて逆の崖になる。数ではなく担い手の数が摘み。
    public static readonly UnitDef Hero2 = Make("hero_v", "勇者候補", 90, 20, 14, TraitId.Condemn);
    public static readonly UnitDef Knight2 = Make("knight_v", "巡礼騎士", 71, 15, 7);

    // 告発人（第40期）。**第五波の中央に置く。巡礼騎士（Knight2）と数値・型・速さが1つも違わない。**
    // 差分は曝き（Expose）を1つ足しただけ——渇き・軛・粛・殉教と同じ形で、対照を成立させるため。
    // Knight2 は削除せず残してある（差し替えれば盤面が完全に元へ戻ることの対照になる）。
    //
    // **ロスターで初めて「敵から味方へ状態を書く」経路になる。** それまで敵側から味方へ届くのは
    // 断罪（審問官・勇者候補の「反撃してきた相手を痺れさせる」）1本だけで、味方側には
    // 入力を資産に変える読み手（被弾強化・逆しま・澱み喰い・軋み・責め苦・移り木）が
    // 揃っているのに供給源が無かった。撒くものに移動を選んだのは、直接の読み手が最も多く
    // （後衛特化・軋み・移り木の3枚）、かつ**同じ1つの規則で駒の符号が反転する**ため。
    //
    // 中央を選ぶのは渇き・軛・粛と同じ理由——**どちらの列を貫いても必ず通る**位置だから。
    // 詳細と符号反転の実測は ExposeTrait の宣言と design/PHASE40_EXPOSE.md。
    public static readonly UnitDef Accuser = Make("accuser", "告発人", 71, 15, 7, TraitId.Expose);
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

    // 粛の伝令: **第二波の中央に採用した**（2026-08-31）。討伐隊の新兵1枚と差し替えてある。
    // **数値は討伐隊の新兵（45/11/6）と1つも違わない。トレイトだけを足してある**
    // ——渇き・軛と同じ形で、差分をルールだけに閉じ込めるため（逆位は数値も動かしたせいで
    // 「壁が変わったのか、ルールが効いたのか」の切り分けに追加測定が要った）。
    // 同数値・規則無効の対照（`hush` の V1）は 38編成 × 5波 = 190 セルで現行と1桁も違わない。
    // V0（差し替え前）も 190 セルすべてが差し替え前の docs/balance.md と一致（検算済み）。
    //
    // 課金する資源は「**ターン外の手数**」。第二波は後列到達力、第三波は持続（渇き）、
    // 第四波は1発の重さ（軛）なので、どの波とも重ならない1本になる。
    // **第四波と正反対の極**——軛は1発の重さに課金する（＝手数が有利）ので逆を向く。
    // 実測で第2波と第4波の相関が **+0.28 → −0.28** と符号ごと反転した。
    //
    // 止まるのは `CanActOutOfTurn` を通る4本だけ（棘・仇討ち・軋み・追い打ち）。
    // **肩代わりはダメージの再分配であって行動ではない**ので無風、責め苦も
    // `OnAfterAttack` ＝自分の手番の中なので無風（`hush` の機構表で実測: 生存中の発火が
    // 4本とも 0.00/戦、止めない2本は V1 = V2 で一致）。
    //
    // 測定（spread・seed 200・38編成）: 第二波の 100%編成 23 → 12 / 固有の敗者 0 → 6 /
    // 中間帯 13 → 16 / 第2〜4波すべて100% 5 → 1 / 第3波との相関 +0.71 → +0.07 /
    // 第5波との相関 +0.54 → +0.07。第二波でだけ落ちる編成ができたので**他の波の
    // 固有の勝者も湧いた**（第3波 0 → 4・第4波 1 → 3。盤全体で 5 → 10）。
    //
    // 課金された編成: カド軸6本（100% → 0%）・溜め軸2本（どちらも後列にカドを積んでいる）・
    // ザン軸2本（91.0 → 15.0 / 100.0 → 30.5 の傾斜）・ヨミ軸3本・ハギ軸1本。
    // 無風だった編成: シガ・毒軸・逆しま軸ほか23本が**与ダメの1の位まで一致**。
    //
    // **残した問題: 平均が 86.5 → 58.5 で、第三波（73.4）より難しい波になった。**
    // 第五波（55.4）よりは易しいので第25期の歯止めは守れているが、波の並びは
    // 100 / 58.5 / 73.4 / 59.0 / 55.4 と単調ではなくなった。粛には軛の Cap に当たる
    // 連続量のノブが無い（「ターン外に振れるか」は二値）ので**係数では緩められない**
    // ——緩めるなら窓口の絞り込み（回数制限・保持者の後列配置）で、どちらも別の規則として
    // 測り直しになる。経緯は README「波に『ターン外の行動禁止』を置いたら」。
    public static readonly UnitDef Husher = Make("husher", "粛の伝令", 45, 11, 6, TraitId.Hush);

    // 逆位の異端審問官: 第五波の後1に置く候補として作り、**測って採らなかった**（2026-08-31・第32期）。
    // 差し戻し済み（Stages[4] の後1 は審問官に戻してある）。**定義は対照として残す**
    // ——Inverter / Levy / ZealotPorter と同じ「採用が決まるまで Stages に足さない」扱い。
    // 測定の全文は design/PHASE32_REVERSAL.md。要点は下の「測定」段落。
    // 以下の宣言文は採用を前提に書いた当時のままにしてある（狙いと置き場所の根拠の記録）。
    // 審問官1枚と差し替えて測った。
    // **数値・型・断罪は審問官（76/12/10・全体・Condemn）と1つも違わない。逆位を1つ足しただけ**
    // ——渇き・軛・粛と同じ形で、差分をルールだけに閉じ込めるため。
    // 逆位を**第三波で測ったとき**は数値も動かした（巡礼騎士 75/15 → 逆位の祭司 90/10）ので
    // 「壁が変わったのか、ルールが効いたのか」の切り分けに追加測定が要った。今回はそこを直してある。
    //
    // **規則そのものは新設していない。** `TraitId.Inversion` / `InversionTrait` /
    // `BattleEngine.Run` の順序構築の分岐は第22期からあり、足りなかったのは
    // 「第五波にそれを持つ保持者を置くこと」だけだった。第22期に第三波で測って
    // 採らなかった規則を、**別の波に置き直して測り直す**のがこの差し替えの中身
    // （経緯は Inverter の宣言と README「盤面ルール（逆位）は実在したが、波を分離しなかった」）。
    //
    // 課金する資源は「**先に動けること**」。第二波は後列到達力（粛はターン外の手数）、
    // 第三波は持続（渇き）、第四波は1発の重さ（軛）なので、どの波とも重ならない1本になる。
    // 第五波の速さは {14, 12, 10, 7, 5} で、反転が動かすのは主に両端の2枚——
    // **勇者候補（攻20・速14・断罪）が1番目から最後尾へ**、戦斧兵（攻11・薙ぎ・速5）が
    // 最後から1番目へ。味方側では速≦4 の層（バン2・セッキ2・ゴルム3・ドハ3・シガ3・
    // ヒビ3・ガルド4・カド4・クビ4）が先頭に回る。
    //
    // **中央ではなく後1 に置いた。** 渇き・軛・粛は中央（どちらの列を貫いても必ず通る）に
    // 置いたが、第五波の中央は巡礼騎士 71/15 で**この波で唯一 断罪も範囲も持たない駒**。
    // そこにルールを載せると最も脆い枠に乗る。さらに**断罪の担い手は2体でないと成立しない**
    // （Hero2 の宣言: 1体だとカドの反撃が担い手を先に殺して罰が消える / 3体だとカド系が
    // 逆の崖になる）ので、担い手の数を動かさずにルールを載せられるのは
    // 「断罪持ちの審問官をそのまま逆位持ちにする」形だけになる。
    //
    // **保持者を倒すと次のターンから降順に戻る**（渇き・軛・粛が「倒したその場から」戻るのとは
    // 違う。order はターン頭に1回だけ組むという既存仕様で、逆位はそこに載っている）。
    //
    // **測定（compare・seed 200・42編成）: 採用しなかった。** 第五波の平均 54.8 → 61.7 /
    // 0%編成 8 → 2 で**波としては易しくなり**、**中間帯 22 → 20・標準偏差 39.9 → 38.3 と
    // 情報は減った**。固有の勝者 0 → 1（縛め収入型 +80.5）だが**固有の敗者は 1 → 0**。
    // 3×5 相関は下がるどころか **+0.56 → +0.62**（第2×5 も +0.06 → +0.25）。
    // 第一〜第四波 168 セルは食い違い 0 件だったので、実装は波の外に漏れていない。
    //
    // **第22期に第三波で測ったときとまったく同じ向きの失敗。** 逆位は第三波でも第五波でも
    // 波を易しくする方向に出た——**波の作りではなく規則の性質**。次に反転を使うなら
    // 「易しくなること自体を利用する波」（下限を切る波）に置く。
    //
    // 駒レベルでは強く効いた。**棘は「先に殴ってきた相手」に配られる**ので、反転すると
    // 反撃軸が最初に殺す敵が入れ替わる（降順では速12の貫きと速10の全体が T2 に落ちるが、
    // 昇順では速5の薙ぎと速7の単体に刺し返しが吸われ、**棘守りでも庇うでも止められない
    // 貫きと全体が生き残る**）——カド軸6行が −20〜−26.5pt。
    // **第五波は速さと攻撃型が相関している**（速5=薙ぎ / 速7=単体 / 速10=全体 /
    // 速12=貫き / 速14=単体）ので、行動順の反転は「どの型が先に来るか」の反転でもある。
    public static readonly UnitDef Reverser = new()
    {
        Id = "seer_r", Name = "逆位の異端審問官", MaxHp = 76, Attack = 12, Speed = 10,
        Traits = new[] { TraitId.Condemn, TraitId.Inversion }, Pattern = AttackPattern.All
    };

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
        //
        // **中央は粛の伝令（Husher）。** 討伐隊の新兵と数値は同一で、盤面ルールを1つ持つ
        // ——生きている間、**両陣営のターン外の行動が一切通らない**。狙いは「後列に届くか」しか
        // 問うていなかったこの波に、**ターン外の手数という別の軸を1本足す**こと。
        // **第四波と正反対の極になる**——第四波は1発の重さに課金する（＝手数が有利）ので、
        // ターン外の手数に課金するこの波とは逆を向く。
        // 中央に置くのは渇き・軛と同じ理由で、どちらの列を貫いても必ず通る位置。
        // 止まるのは棘（カド）・仇討ち（ザン）・軋み（ヨミ）・追い打ち（ハギ）の4本だけで、
        // 肩代わり（庇う・分かち・巨躯・後備え・棘守り）は**ダメージの再分配であって行動ではない**
        // ので無風。責め苦（シガ）の追撃も自分の手番の中なので通る。
        // 教えること:「ターン外の手数を数えて編成を組んだなら、それが黙らされる盤面がある」。
        // **保持者を倒せばその場から解禁される**ので、「早く割れば解禁される」勾配が
        // 自己言及的に立つ（軛と同じ狙い）。ただしカド軸は自分の手番を持たないため
        // 割りに行けず、実測で 100% → 0% に張り付いた——詳細は Husher の宣言。
        new Stage("第二波 / 巡礼騎士団",
            Formation.Build(front1: KnightG, front3: KnightG, center: Husher, back1: Almoner, back3: ArcherG)),

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
        // **前1は殉教者（Martyr）。** 戦斧兵と数値・型・速さが同一で、庇うを1つ持つ
        // ——**敵側の介入は製品のロスターでこれが初めて**。狙いは「総合力」しか
        // 問うていなかったこの波に、**対単体攻撃への依存という別の軸を1本足す**こと。
        // 庇うは Single にしか割り込まないので、薙ぎ・貫き・全体は素通りする。
        // 中央に置けないのは構造的な制約（center は Row.Mid）で、詳細は Martyr の宣言に全文。
        // 副次: 「庇うで執着を引き剥がす」（第30期）が**製品の波で初めて発火する**
        // ——それまで庇う・後備えを持つ敵が1体もいなかったので到達不能だった。
        // 教えること:「単体で殴ることを数えて編成を組んだなら、それを吸う駒がいる盤面がある」。
        //
        // 審問官と勇者候補は断罪を持つ。反撃してきた相手を痺れさせるので、
        // ターン外に動く駒（棘・割り込み・追い打ち）だけが代金を払う。
        // 反撃しない編成には何も起きない（19編成すべて ±0.0 で確認済み）。
        //
        // 後1を逆位の異端審問官（Reverser）に差し替える案は測って**採らなかった**
        // （第32期・2026-08-31）。理由は Reverser の宣言に全文。要するに
        // **逆位は第三波でも第五波でも「波を易しくする」方向に出た**——per-build の効果は
        // 本物（±20pt 超が10行・最大 +80.5 / −40.5）だが、正味では平均 54.8 → 61.7 と上がり、
        // 中間帯 22 → 20・標準偏差 39.9 → 38.3 と情報が減り、3×5 相関も +0.56 → +0.62 と
        // 上がった。**波の作りではなく規則の性質**なので、次に反転を使うなら
        // 「易しくなること自体を利用する波」（下限を切る波）に置く。
        new Stage("第五波 / 異端審問団",
            Formation.Build(front1: Martyr, front3: Hero2, center: Accuser, back1: Seer, back3: Lancer))
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
