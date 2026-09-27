# 逃走兵セロ 戦闘立ち絵・右向き v2

生成方法: Codex 組み込み ImageGen
編集元: `sero-idle-right-v1.png`
出力: `sero-idle-right-v2.png`
変更: 戦闘中の笑みを抑え、口元を引き締めた集中した表情へ変更。
採用済み: `DemoApp/assets/portraits/battle/sero_attack_idle_right.png` に攻撃時の立ち絵として適用。
通常攻撃・追加攻撃の表示時に切り替え、0.40秒（再生速度で補正）後に弓下ろしへ戻す。連射時は復帰予約を延長し、死亡・蘇生・勝利時には解除。
検証: `dotnet build DemoApp/DemoApp.csproj --no-restore` 成功。`Godot_console.exe --headless --path DemoApp res://SeroPortraitCheck.tscn` で実際の攻撃→待機、連射、燃焼通知、死亡、蘇生、勝利の表示を確認（`SERO_PORTRAIT_CHECK_OK`）。

## プロンプト

```text
Localized facial-expression edit of this exact transparent full-body Sero combat archer portrait. Change ONLY his facial expression: remove the smiling upturned mouth and smug smirk. His lips should be closed in a small natural nearly straight firm neutral line with no upturned corners; expression is calm, serious, alert concentration while precisely aiming at an enemy off-screen RIGHT. Slightly focused brows and steady sharp eyes; composed capable young archer, not angry, grimacing, frightened, sad or scowling. Preserve his recognizable face shape, age, eye color, nose, hairstyle and exact head angle and rightward gaze. Preserve EVERYTHING outside the tiny facial expression area as closely as possible: full canvas framing, right-facing archery pose, hands, bow, continuous string and arrow, all clothing and leather details, colors, lighting, linework, texture, proportions, boots, quiver, flowing olive mantle and reddish sash. No redesign, no change in pose or crop. Retain genuine transparent alpha background, including internal gaps, no backdrop, no shadow, no effects, no text.
```
