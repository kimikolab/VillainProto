# 作戦マップの曲と音の口（第480期 §3-4）

**曲そのものは置いていない。** 下の名前でファイルを置けば鳴り、無ければ黙る（落ちない）。口は `DemoApp/Map11Main.Hunter.cs` の `MapMusic`。

## 曲（置き場: `DemoApp/audio/`）

| ファイル | いつ鳴るか | 繰り返し |
|---|---|---|
| `map_normal.ogg` | 作戦マップ（3D）のふだん | する |
| `map_hero.ogg` | 緊迫 —— 1-3「狩る者」で勇者が現れてから、撤退の局面が終わるまで | する |
| `map_win.ogg` | 結果の窓（勝ち・生還・勇者は退いた） | しない |
| `map_lose.ogg` | 結果の窓（負け・全滅・時間切れ） | しない |
| `story.ogg` | 会話の画面（ストーリーパート・第481期）のあいだ。終わると前の曲へ戻る | する |

- 形式は Ogg Vorbis（`.ogg`）。Godot に読み込ませる（エディタを 1 度開く）と `.import` ができる。
- 切り替えは **1 秒で重ねて替える**（`MapMusic.FadeSeconds`）。同じ曲を頼まれたら鳴らし直さない。
- 戦闘の画面へ行って戻るあいだは**止めない**（戦闘の画面に曲は無い）。題・編成の画面へ出るときは 1 秒で消す。
- 音量は `MapMusic.VolumeDb`（今は −12 dB の定数）。**設定の画面に音量の項目はまだ無い**——項目ができたらここをその値で置き換える。
  音の経路は、`Music` という名前のバスがあればそこへ、無ければ `Master`。
- **会話の画面**（第481期）: `story.ogg` があれば 1 秒で替え、無ければ盤の曲を **14 dB 下げる**（`MapMusic.Duck`・`DuckDb`）。会話が終わると戻す。
- 起動の行に `MAP_MUSIC <前> → <次> file=…` が出る（ファイルが無ければ「（無い——黙る）」）。

## 音（今ある効果音から仮で当てた）

| 出来事 | 当てた音 | 場所 |
|---|---|---|
| 勇者の登場の落雷 | `ShockMarkSound.ThunderHeavy`（戦闘の雷の重い音） | `Map11Main.Hunter.cs` `HunterEntrance` |
| 門の扉が吹き飛ぶ | `MapStagingAudio`（合成の低い音・`MapStageCue.Kind.Fall`） | 同上 |
| 隊が拠点に入った（生還） | `ShockMarkSound.RallyHeal` | `ProcessHunter3D` |
| 敵が湧いた・予備隊が出た・敵が拠点へ歩き出した | `ShockMarkSound.Deploy` | `Map11Main.Map3DRadar.cs` `FoeEmerged3D` |
| 勇者は退いた | `MapStagingAudio`（合成の高い音・`Kind.Open`） | `HunterRouted` |

差し替えるときは上の表の場所の 1 行を替える。
