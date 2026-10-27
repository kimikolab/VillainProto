# 逆しまのウツ 双剣リメイクのゲーム適用

適用日: 2026-10-27。ユーザーのゲーム適用依頼により通常・戦闘の2枚を差し替えた。

- 通常: `utsu-standing-dual-swords-remake-v2.png` → `DemoApp/assets/portraits/utsu.png`。両方の剣を下へ垂らす修正版。
- 戦闘: `utsu-battle-idle-right-dual-swords-remake-v1.png` → `DemoApp/assets/portraits/battle/utsu_idle_right.png`。
- 旧画像は `originals/utsu-before-remake.png` と `originals/utsu-idle-right-before-remake.png` に保存。
- ready差分・待機連番の優先ファイルはなく、既存のID規約で新しい静止画を読む。
- 戦闘絵のアルファ128以上の最下端は1483行目（0始まり）。下端の余白52pxに合わせ、`UiKit.BattlePortraitBottomPaddingRatio("utsu")` を `0.0436f` から `52f / 1536f` に更新した。
- 表示高は既存の2.50を維持。戦闘ルール・数値・特性・効果音・攻撃演出は変更していない。

## 確認

`dotnet build DemoApp/DemoApp.csproj --no-restore` 成功（0エラー）。既存BattleCoreにCS0162・CS8602の警告各1件。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/utsu/capture-integration.gd --audio-driver Dummy --log-file 'C:/works/VillainProto/design/art/utsu/game-capture.log'
```

Vulkan Forward+で実際の編成・戦闘画面を撮影。`UTSU_REMAKE_GAME_CAPTURE_OK result=0`、終了コード0。制限環境での起動は待ち状態になったため中断し、通常のユーザーデータへアクセスできる実行で完了した。

`selection-game-check.png` と `battle-game-check.png` を目視し、新しい通常絵・右向きの戦闘絵、両剣、透過、全身、足元と周囲の駒とのバランスを確認。採用原本とゲーム素材はSHA-256が一致し、ともに1024×1536の透過PNG。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless --quit-after 120000 --log-file 'C:/works/VillainProto/design/art/utsu/battle-smoke.log' -- --demo-autoplay --demo-quit --demo-fast '--demo-preset=逆しま (ネル×ウツ)' --demo-stage=0 --demo-seed=7
```

`DEMO_SMOKE_COMPLETE events=98 turns=3 won=True attackPlays=16 maxRun=5 maxRunBy=逆しまのウツ`、終了コード0。ウツの5連撃を含む1戦を完走。両実行とも終了時にFontAdvanced・CanvasItem・ObjectDBの解放警告あり。画像読み込み・再生の失敗なし。
