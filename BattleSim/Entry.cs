/// <summary>
/// <b>本体を大きなスタックのスレッドで走らせるだけの入口</b>（第139期 段0）。
///
/// <para><b>なぜ要るか。</b> <see cref="Prog.Body"/> は元はトップレベル文で、C# はそれを
/// 1つの <c>&lt;Main&gt;$</c> に畳む——<b>全モードの局所変数が1つのスタックフレームに同居する</b>。
/// 実測ではそのフレームだけで既定 1MB をほぼ食い尽くしていて、
/// <b><c>compare</c> は <c>ApplyDamage</c> の再帰で、<c>compare quality</c> と <c>chain</c> は
/// 集計の <c>Enumerable.Sum</c> で落ちていた</b>（どちらも exit 127・生成物が途中で壊れる）。</para>
///
/// <para><b>第138期の読み「`Run` の引数を2本増やしたから」は原因ではなく最後の一押しだった</b>
/// ——第139期に <c>eaec7e6</c> をクリーンな <c>git worktree</c> に出すと、
/// <b>引数を1本も足していない HEAD で `compare` が同じ 23 行目で落ちる。</b></para>
///
/// <para><b>盤面・乱数・順序には1ビットも触っていない。</b> 同じ本体を同じ順で1回走らせるだけで、
/// 変わるのは<b>既定 1MB のスタックが 256MB の「予約」になること</b>だけ
/// （予約は仮想アドレスなので、実際に触ったページしか物理メモリを使わない）。</para>
///
/// <para><b>例外はそのまま投げ直す</b>——スレッドの中で握り潰すと、
/// 診断が黙って途中で終わって「生成物が作れた」ように見える。
/// <b>StackOverflowException はここに来ない</b>（.NET では捕まえられずプロセスごと落ちる）ので、
/// 256MB で踏み抜いたらそれは再帰そのものが壊れている合図である。</para>
/// </summary>
internal static class Entry
{
    /// <summary>予約するスタック（256MB）。<b>踏まなければ物理メモリは使わない。</b></summary>
    const int StackBytes = 256 * 1024 * 1024;

    static void Main(string[] args)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? error = null;

        var worker = new Thread(() =>
        {
            try { Prog.Body(args); }
            catch (Exception ex) { error = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); }
        }, StackBytes);

        worker.Start();
        worker.Join();
        error?.Throw();
    }
}
