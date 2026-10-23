# 発注メモ —— 演出ビューア（FxViewer）

置き場所: `design/CODEX_BRIEF_FX_VIEWER.md`。触るのは `DemoApp` だけ（`BattleCore` / `BattleSim` / `docs/` には触らない）。
期の測定には関係しない道具なので、`sweep` も `digest_cache` も不要。

## 0. 何のための道具か

演出・モーションを「編成を組んで戦闘を進めて、その場面が来るのを待つ」ことなく、**一覧から選んで即座に見る**ための画面。

今の `DemoApp` には `*Check.tscn` が約45本ある（`ThunderCheck` `BasaWindCheck` `PoisonAxisCheck` `MovementCheck` …）。中身はどれも同じ型で、

1. `BattlefieldView3D` を置いて `BeginBattle(DemoOpening[])` で駒を並べる
2. `BattleEvent` を手で組む（または `Main` を埋め込んで実台本を回す）
3. `Show〜` / `ApplyEvent` で再生し、`Require` で検査、PNG を保存

つまり**「駒の並び＋イベント列」という演出の材料はもう45本分ある**のに、各シーンに閉じていて一覧できない。これを1本の画面に集める。

ポンの作業は「一覧から選ぶ → 見る → 気になったら数値を直して再生」だけになる。Codex の作業は「演出を足したら Cue を1つ登録する」が1つ増えるだけ。

## 1. 仕上がりの画面

`DemoApp/FxViewer.tscn`（`FxViewer.cs`）。起動: `Godot --path DemoApp res://FxViewer.tscn`。

```
┌──────────────┬─────────────────────────────────────┐
│ [絞り込み]     │                                     │
│ 軸: ▼ 駒: ▼   │                                     │
│ 検索: ______  │        BattlefieldView3D            │
│──────────────│        （Main と同じ描画）             │
│ ▸ 雷軸        │                                     │
│   カタ 雷 1発 │                                     │
│   カタ 跳ね   │                                     │
│   放電 連鎖   │                                     │
│ ▸ 移動軸      ├─────────────────────────────────────┤
│   バサ 嵐     │ ▶再生 ‖一時停止 ⏭次の見せ場 🔁繰返し  │
│   ヨミ 居合   │ 速度 ▼0.5/1/2/4   左右反転 □  📷撮影  │
│ ▸ 台本（実戦）│ Cue: カタ 跳ね（手組み）  event 12/18 │
└──────────────┴─────────────────────────────────────┘
```

- 左: Cue の一覧。軸・駒・文字列で絞り込める。選ぶと即座に盤面が組まれて再生が始まる
- 中央: `BattlefieldView3D`。**Main と同じ描画・同じ `ApplyEvent` 経路**で再生する（別の描画経路を作らない）
- 下: 再生 / 一時停止 / 次の見せ場へ / 繰り返し（ON なら Cue を終端でループ）/ 速度 / 左右反転（駒の陣営を入れ替えて敵側の見え方を確認）/ 撮影（PNG を `design/art/_viewer/<cueId>.png` へ）

## 2. 設計

### 2-1. Cue（演出単位）の登録簿 —— `DemoApp/FxCatalog.cs`

```csharp
public sealed record FxCue(
    string Id,                       // "kata.thunder.chain"
    string Title,                    // "カタ 雷の跳ね（3体）"
    string Axis,                     // "雷" "移動" "毒" "破片" … 一覧の見出し
    IReadOnlyList<string> Units,     // 絞り込み用の駒 Id ["kata","beni"]
    FxCueKind Kind,                  // Scripted（手組み）/ Live（実台本）
    // --- Scripted ---
    Func<DemoOpening[]>? Openings = null,
    Func<BattleEvent[]>? Events = null,
    // --- Live ---
    string? PresetName = null,       // Presets の行名（"試遊・標 守り型" など）
    int Stage = 0, int Seed = 0,
    Func<BattleEvent, bool>? Highlight = null   // 「見せ場」の判定（⏭ が飛ぶ先）
);

public static class FxCatalog
{
    public static IReadOnlyList<FxCue> All => _all;
    // 登録は static コンストラクタか、部分クラス FxCatalog.<軸>.cs に分けて Add(...) する
}
```

- **Scripted（手組み）**: 既存 Check の `Preview()` が持っている `DemoOpening[]` と `BattleEvent[]` をそのまま移す。盤面は固定・毎回同じ絵が出る。演出の数値調整はこちらで見る
- **Live（実台本）**: `Presets` の行 + 波 + seed で `BattleEngine.Run(verbose:true)` を回し、`Highlight` に合う event を「見せ場」として ⏭ で順に飛べる。手組みでは出ない「実戦での見え方」「他の演出との重なり」はこちらで見る
- `Openings` / `Events` を `Func` にしているのは、`UnitCatalog.*.Traits` など起動時に初期化が要るものを遅延させるため
- 軸ごとに `FxCatalog.Thunder.cs` `FxCatalog.Movement.cs` … の部分クラスに分けると、Codex が演出を足すときに触るファイルが1つで済む

### 2-2. `Main` への窓口追加 —— 実台本でないイベント列を再生できるようにする

今の `Main.EnterBattle` は中で `BattleEngine.Run` を呼ぶので、手組みの `BattleEvent[]` を Main の再生経路に乗せられない。
ビューアは Main を埋め込んで使う（`ThunderCheck` と同じやり方）ので、次を足す:

```csharp
// Main.cs（または Main.FxViewer.cs）
/// <summary>台本を外から渡して再生する（演出ビューア用）。BattleEngine は呼ばない。</summary>
public void EnterScripted(IReadOnlyList<DemoOpening> openings, IReadOnlyList<BattleEvent> events,
                          string title, int stageIndex = 0)
```

- 中身は `EnterBattle` の後半（`_result` 以降の Index〜Build〜`BeginBattle`〜再生開始）を切り出して共用する。`_result` は `BattleResult` を要求するので、`Events` だけ入った軽い `BattleResult` を組む（`PlayerWon` 等は false/0 でよい・表示専用）
- `EnterBattle` 自身の挙動は変えない（既存の Check が全部これを通る）
- あわせて次の3つを `public` に出す（今は `ThunderCheck` 等がリフレクションで `_speed` `_playing` `ReplayBattle` を叩いている。ビューアからはリフレクションを使わない）:
  - `SetPlaybackSpeed(double)`
  - `bool IsPlaying`
  - `int EventIndex`（今の `_eventIndex`）
  - `Task SeekTo(int eventIndex)` —— **ここまで早送り**。`_fastSmoke` と同じ要領で速度を 1000 にして `eventIndex` 直前まで回し、速度を戻す。盤面の状態を N 件目で正しく組む最も安い方法（別途「状態の復元」を書かない）
- 既存の Check のリフレクションは**今回は直さない**（動いているものに触らない）。公開 API に乗せ換えるのは別の機会

### 2-3. ⏭「次の見せ場」

- Scripted: `Events` の中で `Kind` が `Attack` / `Thunder` / `Discharge` / `Move` / `Death` / `Summon` など「絵が動く」種のもの（`TurnStart` `StatSnapshot` `StatusSnapshot` は飛ばす）
- Live: Cue の `Highlight` に合うもの。未指定なら `Units` の駒が `ActorId` のイベント
- 押すと `SeekTo(次の見せ場の index)` して、そこから通常速度で再生

### 2-4. 左右反転

`DemoOpening.Team` を 0↔1 で入れ替え、`Slot` はそのまま。**敵として置いたときの立ち絵・演出の向きを確認する**ため（`BasaWindCheck` が `team in {0,1}` で回しているのと同じ目的）。Live では無効（グレーアウト）。

## 3. 段取り

**第1段（この発注の範囲）**

1. `FxCatalog.cs` と `FxCue` の型
2. `Main.EnterScripted` と公開 API 4つ（§2-2）
3. `FxViewer.tscn` / `FxViewer.cs`: 一覧・絞り込み・再生・一時停止・⏭・繰り返し・速度・左右反転・撮影
4. 既存 Check から **5本だけ** Scripted Cue に移す。候補（軸が散るように）:
   - `ThunderCheck.Preview` → `kata.thunder.*`（雷）
   - `BasaWindCheck` の嵐 → `basa.storm`（移動）
   - `PoisonAxisCheck` の1つ → 毒
   - `GolmCheck` の1つ → 標
   - `DropkickCheck` または `YomiIaiCheck` → 移動／撃破
5. Live Cue を **2本**: `Presets.Playtest` または `Presets.Versions` の行から（名前は現役の行を `Presets` から拾うこと。ハードコードした行名が消えていたら起動時に警告して飛ばす）
6. `design/DEMOAPP_HISTORY.md` に「FxViewer を足した・Cue の登録の仕方」を1項

**移植の規則**: Check の `Preview()` の中身を Cue に写したら、**Check 側の `Preview()` は Cue を参照して回す**ように書き換える（並びとイベント列が二重に存在しない状態にする）。`Require` による検査は Check に残す。

**第2段以降（別の発注）**: 残り40本の順次移植は、その演出を触る期に合わせて都度やる。一度に全部移さない。

**Codex の作法として今後残すもの**: 新しい演出を足したら、その演出の Cue を `FxCatalog.<軸>.cs` に1つ登録する。Check を書くときは Cue を参照する。

## 4. 検証

- `DemoApp` ビルド: 警告0・エラー0
- `Godot --path DemoApp res://FxViewer.tscn --headless -- --fx-smoke` で全 Cue を速度1000で一巡し、例外なく終端に到達したら `FX_VIEWER_SMOKE_OK` と Cue 数を出す（終了コードは当てにしない。`*_OK` の行で読む・既存の頭なしの門と同じ）
- 移した5本の Check が今まで通り `*_CHECK_OK` を出す
- 移した5本について、Check が保存していた PNG と、ビューアの撮影 PNG を同じ場面で並べて目視で一致を確認（`design/art/_viewer/` に置く）
- 左右反転で5本すべてが崩れず表示される（立ち絵の向き・演出の向き）
- `SeekTo` で中盤に飛んでから再生して、HP・状態アイコン・席がその時点の台本と一致する（Live の Cue 1本で確認。`ThunderCheck` の「最終HPは台本に一致」と同じ要領）

## 5. 触らないもの・決めごと

- `BattleCore` には触らない。`BattleEvent` の種類も足さない
- `Main.EnterBattle` の挙動は変えない。既存の Check は全部そのまま通ること
- `DemoApp/assets/` に新しい素材は足さない（撮影 PNG は `design/art/_viewer/` へ）
- 別の描画経路を作らない。ビューアの再生は必ず `Main.ApplyEvent` を通す（ビューアで見た絵と本番の絵が違ったら道具の意味が無い）
- 編成画面（`Main` の setup モード）はビューアでは出さない
