using BattleCore;
using static Common;

// decoy check —— 自己検査（第226期）。
static partial class DecoyDiag
{
    static partial void CheckImpl() => Console.WriteLine("decoy check: 実装の後に回す。");
}
