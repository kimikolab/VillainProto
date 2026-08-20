using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BattleCore;

namespace PrototypeApp;

/// <summary>
/// UI は編成を組み立てて BattleEngine.Run を呼び、返ってきたログを表示するだけ。
/// ここに戦闘ルールを一行も書かないこと。書いた瞬間に Godot / Unity へ運べなくなる。
/// </summary>
public partial class MainWindow : Window
{
    private const string EmptyLabel = "（空き）";

    private readonly ComboBox[] _slots;

    public MainWindow()
    {
        InitializeComponent();
        _slots = new[] { Slot0, Slot1, Slot2, Slot3, Slot4 };

        foreach (ComboBox cb in _slots)
        {
            cb.Items.Add(EmptyLabel);
            foreach (UnitDef def in UnitCatalog.All)
                cb.Items.Add(def);
            cb.SelectedIndex = 0;
            cb.SelectionChanged += Slot_SelectionChanged;
        }

        foreach (EnemyCatalog.Stage stage in EnemyCatalog.Stages)
            StageBox.Items.Add(stage.Name);
        StageBox.SelectedIndex = 0;
        StageBox.SelectionChanged += (_, _) => UpdateUnitInfo();

        // 初期配置: ボルグの隣にムドを置いた、噛み合う例
        Slot0.SelectedItem = UnitCatalog.Borg;
        Slot1.SelectedItem = UnitCatalog.Mudo;
        Slot2.SelectedItem = UnitCatalog.Gald;
        Slot3.SelectedItem = UnitCatalog.Sero;
        Slot4.SelectedItem = UnitCatalog.Nel;

        UpdateUnitInfo();
    }

    private Formation BuildFormation()
    {
        var f = new Formation();
        for (int i = 0; i < _slots.Length; i++)
            f[i] = _slots[i].SelectedItem as UnitDef;
        return f;
    }

    private Formation CurrentEnemy()
        => EnemyCatalog.Stages[Math.Max(0, StageBox.SelectedIndex)].Enemy;

    private int CurrentSeed()
        => int.TryParse(SeedBox.Text, out int s) ? s : 0;

    private void Slot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateUnitInfo();

    private static string PatternLabel(AttackPattern p) => p switch
    {
        AttackPattern.Sweep => "薙ぎ（両隣も）",
        AttackPattern.Pierce => "貫き（後列を狙う）",
        AttackPattern.All => "全体",
        _ => "単体"
    };

    private void UpdateUnitInfo()
    {
        if (UnitInfo is null || StageBox is null) return;

        var sb = new StringBuilder();
        foreach (ComboBox cb in _slots)
        {
            if (cb.SelectedItem is not UnitDef d) continue;
            sb.AppendLine($"■ {d.Name}   HP {d.MaxHp} / 攻 {d.Attack} / 速 {d.Speed} / {PatternLabel(d.Pattern)}");
            sb.AppendLine($"　 ＋ {d.PlusText}");
            sb.AppendLine($"　 － {d.MinusText}");
            sb.AppendLine();
        }

        // オートバトルは編成を決めた時点で勝負がつく。
        // だから敵の届き方は、出撃前に必ず全部見せる。
        var stage = EnemyCatalog.Stages[Math.Max(0, StageBox.SelectedIndex)];
        sb.AppendLine("──── 敵編成 ────");
        foreach ((int slot, UnitDef e) in stage.Enemy.Occupied())
        {
            string row = slot < FormationRules.FrontSlots ? "前" : "後";
            sb.AppendLine($"[{row}] {e.Name}   HP {e.MaxHp} / 攻 {e.Attack} / {PatternLabel(e.Pattern)}");
        }

        UnitInfo.Text = sb.ToString().TrimEnd();
    }

    /// <summary>
    /// ログ行の種類ごとの色。文字列を解析して色を決めないこと。
    /// 見せ場（破裂・覚醒）だけ明確に浮かせ、それ以外は静かに保つ。
    /// </summary>
    private static readonly Dictionary<LogKind, (string Hex, bool Bold)> Palette = new()
    {
        [LogKind.System]       = ("#6E6961", false),
        [LogKind.Turn]         = ("#C9A227", true),
        [LogKind.Action]       = ("#9E988E", false),
        [LogKind.Damage]       = ("#C8D6DE", false),
        [LogKind.FriendlyFire] = ("#C97B4E", false),
        [LogKind.Status]       = ("#A98BC4", false),
        [LogKind.Trigger]      = ("#78B58C", false),
        [LogKind.Highlight]    = ("#F2C14E", true),
        [LogKind.Summon]       = ("#6FA3B8", false),
        [LogKind.Death]        = ("#C05C5C", true)
    };

    private static readonly Dictionary<LogKind, Brush> BrushCache = new();

    private static Brush BrushFor(LogKind kind)
    {
        if (BrushCache.TryGetValue(kind, out Brush? cached)) return cached;
        var color = (Color)ColorConverter.ConvertFromString(Palette[kind].Hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        BrushCache[kind] = brush;
        return brush;
    }

    private void RenderLog(IReadOnlyList<LogLine> lines)
    {
        LogText.Inlines.Clear();
        foreach (LogLine line in lines)
        {
            var run = new Run(new string('\u3000', line.Indent) + line.Text + Environment.NewLine)
            {
                Foreground = BrushFor(line.Kind)
            };
            if (Palette[line.Kind].Bold) run.FontWeight = FontWeights.Bold;
            LogText.Inlines.Add(run);
        }
    }

    private void ShowPlainText(string text)
    {
        LogText.Inlines.Clear();
        LogText.Inlines.Add(new Run(text) { Foreground = BrushFor(LogKind.Action) });
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        Formation player = BuildFormation();
        if (player.Count == 0)
        {
            ResultLine.Text = "ユニットが1体も入っていない";
            ShowPlainText("");
            return;
        }

        BattleResult result = BattleEngine.Run(player, CurrentEnemy(), CurrentSeed(), verbose: true);

        ResultLine.Text = result.PlayerWon
            ? $"勝利　{result.Turns}ターン　生存 {result.PlayerSurvivors} 体"
            : $"敗北　{result.Turns}ターン";

        RenderLog(result.Log);
        LogScroll.ScrollToTop();
    }

    private void BatchButton_Click(object sender, RoutedEventArgs e)
    {
        const int trials = 200;

        Formation player = BuildFormation();
        if (player.Count == 0)
        {
            ResultLine.Text = "ユニットが1体も入っていない";
            return;
        }

        Formation enemy = CurrentEnemy();

        int wins = 0;
        int totalTurns = 0;
        var damage = new Dictionary<string, int>();

        for (int seed = 0; seed < trials; seed++)
        {
            // verbose:false でログを作らないので一括実行が速い
            BattleResult r = BattleEngine.Run(player, enemy, seed, verbose: false);
            if (r.PlayerWon) wins++;
            totalTurns += r.Turns;

            foreach (KeyValuePair<string, int> kv in r.DamageByUnit)
            {
                damage.TryGetValue(kv.Key, out int prev);
                damage[kv.Key] = prev + kv.Value;
            }
        }

        ResultLine.Text = $"勝率 {wins * 100.0 / trials:F1}%　（{trials}回 / 平均 {totalTurns / (double)trials:F1}ターン）";

        var sb = new StringBuilder();
        sb.AppendLine($"{trials} 回試行");
        sb.AppendLine();
        sb.AppendLine("ユニット別 与ダメージ合計（働いていない駒を探す）");
        sb.AppendLine();

        foreach (var (_, def) in player.Occupied().OrderByDescending(x => Total(damage, x.Def.Id)))
        {
            int total = Total(damage, def.Id);
            int bar = totalTurns == 0 ? 0 : Math.Min(40, total / Math.Max(1, trials * 4));
            sb.AppendLine($"  {def.Name,-16} {total,8}  {new string('#', bar)}");
        }

        ShowPlainText(sb.ToString());
        LogScroll.ScrollToTop();

        static int Total(Dictionary<string, int> d, string id)
            => d.TryGetValue(id, out int v) ? v : 0;
    }
}
