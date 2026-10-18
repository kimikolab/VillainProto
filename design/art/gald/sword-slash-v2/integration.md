# ガルドの一刀両断・ゲーム適用（2026-10-18）

## 適用内容

- 承認された6姿勢・28コマ・25fpsの上段からの一刀両断を、`DemoApp/assets/portraits/battle/attack_clip/gald_cleave/` に配置。
- `BattlefieldView3D.GaldSword.cs` が抜剣済みの通常攻撃と斬り返しの両方を接続する。敵味方どちらのガルドも、刃が当たる15コマ目まで待って剣閃・音・通常攻撃の接触通知へ進む。
- 抜剣条件は既存の台本の `SwordDrawn` のまま。味方の生存判定、ダメージ、対象、攻撃回数、戦闘ルールは追加していない。
- 相打ちは台本の `LastStandRiposte.Slot == 1` を受け、HP更新後・Death前の最後の一刀も見せる。画面側で相打ちの成立を判定しない。
- 斬り終えるまで姿勢を保持し、その後は盾なしの構えへ戻る。振り切り中に次の斬り返しへ切り替わった場合は、新しい一刀が終わるまで席へ戻さない。

## 位置合わせ

`clip.json` の `referencePortrait` は `gald_sword`。腰と足元を `[318,572]` → 待機絵の `[470,1432]` へ合わせ、倍率3.25で身長を保つ。攻撃開始直後から足元を合わせ、1フレームだけ待機絵の中心位置が残るずれも解消した。

出力の再現は `sheet_animation.py` で `clean-six-poses.png` から連番を生成し、続けて `export_game_clip.py` でゲームへ配置する。前者はPythonのPillow・numpy・scipy・opencv-python・avを使用する。ゲーム用PNGは承認済みの `frames/` と同一で、モーションの描き直しは行っていない。

## 大剣閃

`GaldSwordSlashFx.cs` は、対象の上から下へ走る一本の白金の刃を表示する。最低幅4.6・高さ5.0の大きな面に、尖った両端、白い芯、金と青白い縁、残光を描く。複数対象は対象の広がりに合わせて一枚を広げ、命中点へ短い閃光・放射状の光・衝撃の輪を重ねる。

購入済み EffectBlocks v4 の `assets/impacts/impact_4.tscn` を各命中点の火花に採用。原本 `D:/Assets/GodotAssets/EffectBlocks v4/` と導入済みの `DemoApp/PolyBlocks/EffectBlocks/` を確認した。自動スクリプトを外し、発動ごとに粒子処理材・描画材を複製して、一回だけ白金色の火花を散らす。購入素材本体はGitへ追加しない。素材がない環境でも主役の大剣閃・命中閃光・衝撃の輪は表示する。

進行は独自の経過値で行い、ShaderのTIMEは使わない。通常の一時停止に従い、途中の再生速度変更は剣閃と粒子にも反映する。非表示・再戦・ツリー破棄では古い接触待ちとFXをキャンセルする。単発・待機の手動ImageTextureキャッシュはRenderingServerの終了前に解放する。

## 検証

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://GaldSwordCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://BorgAttackClipCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://GaldReactionCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://SpecialsCheck.tscn -- --verify
```

- `GALD_SWORD_CHECK_OK checks=56`: 左右・1倍/2倍・命中同期・対象ごと1回通知・上段の足元・待機への復帰・一時停止・途中倍速変更・非表示/再表示・再戦・命中前の死亡・接近して斬り返した後の帰還。
- `BORG_ATTACK_CLIP_CHECK_OK`: 共用プレイヤーの既存ボルグ連番も再生、接触、復帰、死亡停止が通る。
- `GALD_REACTION_CHECK_OK`: 盾の被弾・受け流し・抜剣・膝つき等も通る。
- `SPECIALS_CHECK_OK`: 実台本の相打ち・再戦、剣の斬り返し回数、最終HPの一致を確認。
- Vulkan / Forward+ で描画し、左右・1倍/2倍・接近後の上段と命中を目視確認。描画付きの検査は画像保存を含め `checks=66`。

画像は `output/gald-sword-integration/`。`melee-hit.png` が接近後の大剣閃。未適用候補の比較GIFとは別に、ゲーム内の表示を保存している。

ビルドは成功。作業外のBattleCoreと既存Borgチェックに警告がある。頭なし検証の終了時にはObjectDB/Resource/TextServerの解放警告が出る実行もある。最終Vulkan描画も終了コード0で機能検査66件が通り、描画中のShaderエラーはないが、終了時にTextureのRID解放警告が残る。機能検査の成功と終了処理の警告を区別し、ログは `render-verified.log` / `render-verified-error.log` に保存した。この変更では画像キャッシュをRenderingServer終了前に明示解放したが、アプリ全体の終了時警告が全て解消したとは扱わない。
