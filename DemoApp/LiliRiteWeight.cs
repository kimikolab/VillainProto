using BattleCore;
using System;
using System.Collections.Generic;

// 台本の名目量を描画幅へ変換するだけ。HPで切られた実ダメージから威力を逆算しない。
internal static class LiliRiteWeight
{
    internal static Dictionary<int, float> Widths(BattleEvent head, IEnumerable<BattleEvent> segment)
    {
        var widths = new Dictionary<int, float>();
        foreach (var e in segment)
        {
            if (e.Kind != BattleEventKind.Kiss || e.Text != KissLabels.RiteDrain
                || e.ActorId != head.ActorId || e.TargetId is not int id) continue;
            // 5体等分の1本を基準にする。余りを受け取った敵も、その台本の量で描く。
            // 幅を量そのものに比例させると隣席を覆うので、平方根＋表示上限で抑える。
            double ratio = head.Amount > 0 ? 5.0 * Math.Max(0, e.Amount) / head.Amount : 1;
            widths[id] = (float)Math.Sqrt(Math.Clamp(ratio, 1, 6.25));
        }
        return widths;
    }
}
