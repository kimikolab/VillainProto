using System;

// =====================================================================================
// 作戦マップの時計（第439期 段A2）
//
// **規則は1つも持たない。** 持つのは「次の作戦ターンをいつ回すか」だけで、回すのは画面
// （`Map11Main.TickOnce`）——`Map11State` ／ `Map11Orders` ／ `Map11Session.StepTurn` は1行も変えていない。
// 1 刻 ＝ 1 作戦ターン。出来事（接敵・迎撃・陥落・終了・`StopReason`）で止まり、人が ▶ を押すまで刻まない。
//
// **状態は static**——戦闘のたびにシーンを替えて `Map11Main` が作り直されるので、
// 速さ（1×／2×）は戦闘を跨いで残す。通しが替わったら（`Bind`）止めて目盛りを 0 に戻す。
// Godot の型は1つも入れない（`--map11-clock-smoke` が同じクラスを回す）。
// =====================================================================================

public static class Map11Clock
{
    /// <summary>刻んでいるか（▶）。偽 ＝ 一時停止中（⏸）または出来事で止まった。</summary>
    public static bool Running { get; private set; }

    /// <summary>2× か（間隔が半分）。</summary>
    public static bool Fast { get; set; }

    /// <summary>前の刻からの経過（秒）。</summary>
    public static double Elapsed { get; private set; }

    /// <summary><c>--map11-no-clock</c>（時計を切る）。</summary>
    public static bool Disabled { get; set; }

    /// <summary>
    /// 間隔の差し替え（器具だけ・<c>--map11-clock-smoke</c> は 0 にして <c>_Process</c> を手で回す）。
    /// <b>0 は「呼ぶたびに刻む」で、時計を切る（<see cref="On"/> が偽）のとは別。</b>
    /// </summary>
    public static double? IntervalOverride { get; set; }

    /// <summary>時計が効いているか。偽なら ▶ は第438期と同じく出来事まで一気に回す。</summary>
    public static bool On => !Disabled && (IntervalOverride is not null || Map11.TickSeconds > 0);

    /// <summary>いまの 1 刻の長さ（秒）。</summary>
    public static double Interval => IntervalOverride ?? (Fast ? Map11.TickSeconds / 2 : Map11.TickSeconds);

    /// <summary>次の刻までの目盛り（0〜1）。</summary>
    public static double Fraction => Interval <= 0 ? 0 : Math.Clamp(Elapsed / Interval, 0, 1);

    private static object? _owner;

    /// <summary>通しを結び付ける。<b>別の通し（<c>Reset</c> し直した）なら止めて目盛りを 0 に。</b></summary>
    public static void Bind(object state)
    {
        if (ReferenceEquals(_owner, state)) return;
        _owner = state;
        Stop();
    }

    public static void Start()
    {
        Running = true;
        Elapsed = 0;
    }

    /// <summary>止める（一時停止・出来事・組み直し）。<b>目盛りは 0 に戻す</b>——再開したら 1 刻ぶん待つ。</summary>
    public static void Stop()
    {
        Running = false;
        Elapsed = 0;
    }

    /// <summary>
    /// 時間を積む。<b>刻む番なら true</b>（1 回の呼び出しで刻むのは多くても 1 刻——
    /// 重いフレームの後にまとめて何刻も回さない）。
    /// </summary>
    public static bool Advance(double delta)
    {
        if (!Running) return false;
        Elapsed += Math.Max(delta, 0);
        if (Elapsed < Interval) return false;
        Elapsed -= Interval;
        if (Elapsed > Interval) Elapsed = 0;
        return true;
    }
}
