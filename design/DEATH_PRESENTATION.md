# 死軸の演出（2026-10-24）

依頼元: `CODEX_BRIEF_DEATH.md`。仕組みは `DEATH_AXIS_SUMMARY.md`。変更は DemoApp の表示・素材と本記録のみ。

リィカの最新の攻撃・構え・分離したエネルギー武器・FxViewerへの接続は
[`art/rica/attack-motion-v1/README.md`](art/rica/attack-motion-v1/README.md)。
以下の斬撃の記述は初期実装の記録で、現在は7種類のセルアニメーションと一枚の接触剣閃へ更新している。

## 表現

- リィカ: 3層で青白い目、6層で青紫の側焔、9層で紫の四本の焔。段が上がると短い溜めと光の開放。周囲の粒は9個まで、以後は焔の高さ・強さだけが増える。層が減れば表示も戻る。
- 斬撃: 台本の各 Attack ごとに実対象へ跳躍する。通常は単発の斜め斬り、覚醒後は横薙ぎになり、3・6・9層で残像が1・2・3本へ増える。段に従って幅と身体の振りも強める。命中先・通知件数は既存の経路を維持し、追加攻撃を作らない。
- 仲間の死: 胞子・亡者を含む倒れた仲間から、生きている同陣営のリィカへ光粒が流れる。流入と実層の写しは別にし、画面側で層を計算しない。
- 亡者: 新規の灰色の影絵と燐光の目。亡骸から召喚席への光、立ち上がる粒、朽ちるたびの輪郭の欠落と灰。朽ち・亡者起こしの代償にはダメージ数字を出さず、台本の確定HPだけを更新する。
- 胞子: ムグの撒く動作、実際に召喚された席へ黄緑の粒が流れ、膨らむ光。新たな召喚・死亡は作らない。
- ヴェル: 魂の実残数を最大3つの白い結び目で表示。増加時にヴェルから白い糸、起き上がりの Revive には下から引き上げる光。自身が倒れると白い粒へほどける。
- エレ: 音の実残量に従う金色の粒。既存の歌唱差分を維持し、実際の贈られた手番へ旋律を流す。敵への伝達は同じ形の赤金色。

## 台本と観測の境界

`DeathPresentation.Record` は既存の `CounterProbe` を使い、`necro`、魂の残数・印、哀歌の音・最大HPの実値だけを副台本へ記録する。ログ本文を解析せず、三角数・死による加算・減衰・縫い付けの選択規則を再実装しない。戦闘に死軸の3人がいなければ観測を付けない。

現行APIには Probe の `BattleContext` 引数がないため、`UnitState.Board` を reflection で一度だけ読み、その後は公開の `AllUnits` / `Events.Count` を読む。内部プロパティへの依存はこの境界だけ。盤面の変更・乱数の消費・イベントの挿入は一切しない。計算終了後に盤面の参照を捨てる。

専用の層／魂／哀歌イベントはまだ無いので、更新位置はカウンタの書き込み命令そのものではなく、直後に到達した Probe の台本位置。最後の観測は終端でも適用する。将来正式な通知が追加されたら、この観測境界だけを差し替える。演出の3・6・9という区分は表示の段であり、攻撃回数は常に実台本を使う。

## 素材

購入済み EffectBlocks v4 のローカル実ファイル `other/sparkles.tscn`、`fire/fire_magic.tscn`、`source_files/textures/sparkle.png` を確認。元の配置先 `D:/Assets/GodotAssets/EffectBlocks v4/` はこの環境では存在しないため、導入済みの `DemoApp/PolyBlocks/EffectBlocks/` で確認した。

`sparkle.png` を体の周囲の光粒・魂の結び目へ流用する。sparkles の自動反復や fire_magic の複合ノードの時計を持ち込まず、焔・斬撃・魂の流れは専用シェーダーで描く。購入素材の原本・共有材質は変更しない。購入素材本体はGitに含めない。

購入素材が無い場合は自作 `assets/fx/death_light.svg` を使う。亡者の `assets/portraits/battle/wight_idle_right.svg` も新規作成。リィカの目は既存の立ち絵・待機連番にシェーダーで重ね、連番の作り直しは不要。

## 検証

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://DeathAxisCheck.tscn
# 描画画像も保存する場合（output/death-axis/）
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --windowed --resolution 1440x900 res://DeathAxisCheck.tscn -- --capture
```

- 指定5編成 × ボス・近衛・大隊の15戦（seed 0）で、観測あり／なしの全 BattleEvent・勝敗・決着ターンが一致。最大層と最終の魂・音も実値と一致。
- 段・減衰・停止・倍速・次の対象への跳躍・死亡・起き上がり・勝利・再戦を検証。
- 「試遊・死 エレ」対ボスを本番の Main.ApplyEvent で最後まで再生。覚醒3回・吸収15回・斬撃11回・旋律7回、各駒の最終HPが台本と一致。
- Vulkanで3・6・9・18層と斬撃・魂の描画画像を保存して目視確認。
- `compare --no-build` の結果は `docs/balance.md` と差分0。`audit` はずれているファイル0件。
- Godot検証の終了時には既存の表示系を含む RID / ObjectDB の解放警告が出る。戦闘再生の検証は `DEATH_REPLAY_OK` と `DEATH_AXIS_CHECK_OK` で確認する。

`BattleCore`・`BattleSim`・`docs/` の既存作業差分には触れていない。
