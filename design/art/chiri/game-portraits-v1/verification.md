# チリ立ち絵 v1 — 検証記録

2026-11-01

## 配置と画像

通常: DemoApp/assets/portraits/chiri.png
戦闘: DemoApp/assets/portraits/battle/chiri_idle_right.png
両方1024×1536、RGBA。内蔵image_genの透過を変更せずコピー。
4ピクセル間隔のサンプリングで完全透過領域を確認（通常59541点、戦闘52198点）。人物内部のアルファは主に252〜253/255。
原本と配置先はそれぞれSHA256一致。
通常: 855AF226C825748746FC387B65A100F91158F3F60DB2A6737714EB7FFDE23F98
戦闘: 6CE068CA951D653C3DA3C9C53977A751083EA75A795F7E94FAACD1788D7F6A72

## 接続

UiKitの既存のIDから画像パスを引く処理を利用。チリの戦闘画像下端の余白35/1536を同クラスに1行追加し足元を補正。
BattleCore・戦闘演出・待機モーションは変更なし。他の作業で存在していた変更も保持。

## 確認

- dotnet build DemoApp/DemoApp.csproj --no-restore: 成功、エラー0。BattleCore/BattleEngine.cs のCS0162とCS8602の警告2件あり（今回変更なし）。
- 編成画面: selection-game-check.png。試遊・導火線 チリの後1に正常表示。キャプチャの選択枠2はヒビで、右側の説明欄はヒビのもの。
- 戦闘画面: battle-game-check/battle-2_5d-01.png〜06.png。右向きの立ち絵・透過・足元を目視確認。DEMO_CAPTURE_COMPLETE。
- 頭なし通し確認: Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast "--demo-preset=試遊・導火線 チリ" --demo-playtest-stage=0 --demo-seed=0
- 結果: DEMO_SMOKE_COMPLETE events=320 turns=10 won=False。exit=0。
- git diff --check -- DemoApp/UiKit.cs: 成功。

## 検証時のログの留意点

Godot終了時にCanvasItem/ObjectDB/TextServerの解放警告が出た。画面キャプチャが戦闘中に終了した際は、MakeGroundRing/Linkからのadd_child「Parent node is busy setting up children」と、描画資源の解放メッセージも出た。画像表示とキャプチャは完了し、頭なし320イベントの通しではそのadd_childエラーは出なかった。今回の画像変更によるものかは切り分けていないため、既知・既存とは断定しない。終了処理は今回変更していない。
