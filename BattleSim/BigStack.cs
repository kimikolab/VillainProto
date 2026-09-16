using BattleCore;

/// <summary>
/// <b>大きなスタックの上で1戦を走らせるだけの窓口</b>（第139期 段0）。
///
/// <para><b>なぜ要るか。</b> 第138期が「<c>BattleEngine.Run</c> の引数を1本増やすと
/// <c>compare</c> が <c>ApplyDamage</c> の再帰でスタックを踏み抜く（exit 127・
/// <c>docs/balance.md</c> が途中で壊れる）」を踏んで <c>ArmorRule</c> を
/// <c>ShrapnelRule.ArmorCensus</c> に畳んで通したが、<b>その余裕は数百バイトしか無かった</b>
/// ——第139期に <c>eaec7e6</c> をクリーンな <c>git worktree</c> に出して測ると、
/// <b>引数を1本も足していない HEAD そのものが同じ 23 行目で落ちる</b>
/// （<c>反撃改3 (カド×ハギ)</c>。棘の反撃 <c>ThornsTrait.OnDamaged</c> →
/// <c>BattleContext.Reaction</c> → <c>ApplyDamage</c> が数千段）。</para>
///
/// <para><b>盤面・乱数・順序には1ビットも触っていない。</b> 呼び出し側から見ると
/// <c>BattleEngine.Run(player, enemy, seed, verbose: false)</c> と同じ引数・同じ戻り値で、
/// 同じ順に1戦ずつ同期して走る。変わるのは<b>既定 1MB のスタックが 256MB の「予約」になること</b>だけ
/// （予約は仮想アドレスなので、実際に触ったページしか物理メモリを使わない）。</para>
///
/// <para><b>クロージャを1つも作らない。</b> 最初はトップレベル文の <c>compare</c> の分岐を
/// まるごとローカル関数にして大きなスタックのスレッドへ渡したが、
/// <b>本体が参照するトップレベルの局所変数がすべて表示クラスへ巻き上げられ、
/// Roslyn がメモリ 52GB を掴んでビルドが終わらなくなった</b>（実測）。
/// <b>状態は static フィールドで渡す</b>——呼び出し側は単一スレッドなので競合しない。</para>
///
/// <para><b>ワーカーは1本を使い回す</b>（1戦ごとにスレッドを立てると 61 行 × 5 波 × 200 seed ＝
/// 61,000 回の生成になる）。<c>IsBackground</c> なので、<c>Main</c> が終われば黙って消える。</para>
///
/// <para><b>これは構造的な直しではない。</b> 本筋は <c>Run</c> の 49 本の引数を1つの束に畳むことだが、
/// 呼び出し口が数百あり<b>「計測器と測定対象を同時に動かさない」</b>に正面から反する。
/// この期に閉じるべきなのは「<b>生成物が作れない</b>」の1点である。</para>
/// </summary>
internal static class BigStack
{
    /// <summary>予約するスタック（256MB）。<b>踏まなければ物理メモリは使わない。</b></summary>
    const int StackBytes = 256 * 1024 * 1024;

    static readonly SemaphoreSlim Go = new(0, 1), Done = new(0, 1);
    static Thread? _worker;

    // 受け渡しは static フィールド（クロージャを作らないため）。呼び出し側は単一スレッド。
    static Formation? _player, _enemy;
    static int _seed;
    static BattleResult? _result;
    static Exception? _error;

    /// <summary>
    /// 1戦を大きなスタックの上で走らせる。<c>BattleEngine.Run(p, e, seed, verbose: false)</c> と同値。
    /// </summary>
    public static BattleResult Run(Formation player, Formation enemy, int seed)
    {
        if (_worker is null)
        {
            _worker = new Thread(Loop, StackBytes) { IsBackground = true, Name = "bigstack" };
            _worker.Start();
        }

        _player = player; _enemy = enemy; _seed = seed;
        Go.Release();
        Done.Wait();

        if (_error is not null)
        {
            Exception ex = _error;
            _error = null;
            throw ex;
        }
        return _result!;
    }

    static void Loop()
    {
        while (true)
        {
            Go.Wait();
            try
            {
                _result = BattleEngine.Run(_player!, _enemy!, _seed, verbose: false);
                _error = null;
            }
            catch (Exception ex)
            {
                // **StackOverflowException はここに来ない**（.NET では捕まえられずプロセスごと落ちる）。
                // 256MB で踏み抜いたら、それは再帰そのものが壊れている合図である。
                _error = ex; _result = null;
            }
            Done.Release();
        }
    }
}
