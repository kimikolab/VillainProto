# 標の循環・演出追加（2026-10-09）

`CODEX_BRIEF_MARK_LOOP.md` と `PHASE291_CODEX_MEMO.md` §8 に対応。
変更は DemoApp の台本再生・演出・素材・検証だけ。BattleCore と docs/ は変更していない。

## 追加した表示

- **標撃ち**: `FeatherMark` の対象へ、最寄りの浮遊羽が旋回して短い光線を撃つ。
  規定のミサ（FeatherMark / FeatherMarkLayer）の羽は開戦から空中に残り、在庫は `Feather` の通知だけで更新する。
  敵への標撃ちと味方への誤射を色・ポーズで分ける。手番の連射も同じ羽を使用する。
- **濡れ衣**: `Framed.Accuse` で紫の「あいつがやった！」とヒサの指差し、
  `Framed.Vendetta` で敵に「？」。続く仇討ちは既存の専用演出へ流す。
  引き金の線は Accuse の PartnerId（撃たれた味方）へ結ぶ。
- **仇巡り**: `VendettaRound.Start` で「仇巡り ×N」。Slash 1件に1閃。
  同じ敵の前では留まり、次の敵へは赤い軌跡と残像を伴って移る。最後の実際の太刀を大きくし、席へ帰る。
  予定数に届かず終了しても、台本にある最後の太刀で締める。仇討ちの集計に太刀を混ぜない。
- **力配り**: `ShareGive.Power` で味方からドハへ暗い痛みの筋が入り、光った札が味方へ返る。
  「力 +N」は台本の Amount。攻撃力を画面側で計算・変更しない。
- **矢面の半減**: `BeckonFeather` で標の位置が光り、羽の光線が逸れる小さな効果。
  表示する減少量は防いだ Amount。続く Damage の HP 同期を変更しない。
- **自分への叫び**: 既存の MarkRally の宛先としてヒサ自身も表示する。Amount 0 は回復光を出さない。

## 実装と素材

- `DemoApp/MarkLoopPresentation.cs`: 標撃ち・太刀の見出しと Attack / Damage を関連付ける。
  通常攻撃との二重再生を防ぎ、実在する終太刀と帰還位置を索引化する。
- `DemoApp/Main.MarkLoop.cs`: 新しい出来事の振り分けと、終太刀後の帰還。
- `DemoApp/BattlefieldView3D.MarkLoop.cs`: 標撃ち・濡れ衣・力配り・半減・仇巡りの描画。
- `DemoApp/MisaFeathers3D.cs`: 常駐在庫・独立した標撃ち・既存の連射を共存させる。
- `DemoApp/BattlePawn3D.Zan.cs`: 席を変えない巡りと帰還。終了・再戦で残像も消去。
- **新規素材** `DemoApp/assets/fx/doha_power_tag.svg`: 鎖の輪と淡金色の札。
  ヒサ／ミサ／ザンの差分と剣閃、指定SEは既存素材を使用。

FeatherMark / Framed を既存の早送り・仇討ち束の境界に追加した。
これにより羽の連射間の濡れ衣を飛ばさず、異なる指差しの仇討ちを束ねない。
未知の出来事は従来どおり素通りする。

## 検証

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/Tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://ShockMarkCheck.tscn -- --mark-loop --verify
& 'C:/Tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --rendering-method gl_compatibility --resolution 1440x900 res://ShockMarkCheck.tscn -- --mark-loop --capture-dir=D:/src/VillainProto/output/mark-loop/captures-final
& 'C:/Tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://MisaCheck.tscn -- --verify
& 'C:/Tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://ShockMarkAudioCheck.tscn
```

- DemoApp 最終ビルド: 警告0・エラー0。
- 試遊・標 循環／三人組／守り型 × ボス／近衛／大隊 × seed 0 × 再戦2回 = **18再生合格**。
  新イベント全件、仇討ち・叫びの独立集計、最終HP・標層・最大HP、終了処理を照合。
  `output/mark-loop/verify-final.log`。
- 左右 × 1倍速／2倍速 = **4描画ケース合格**。ボス5／8太刀、7体8太刀、
  敵味方への標撃ち、近い羽の選択、在庫不変、自己回復、再戦・途中終了を確認。
  `output/mark-loop/visual-final.log` と `captures-final/`（44画像）。
  残像消去の追加検証は `output/mark-loop/cleanup.log`。
- 旧版の帰還する羽と指定効果音の回帰検査も合格。
  旧版の帰還検査は明示的に TomeMb を使い、規定の常駐羽は新しい検査で扱う。
  `output/mark-loop/misa-regression.log`、`output/mark-loop/audio.log`。
- 規定のミサでもボス台／道中を各2回、計4再生合格。標撃ちと手番の連射を別計数にし、
  連射間隔・全発再生・最終在庫・HPを検証。通常連射の間隔は1倍換算平均0.055秒。
  `output/mark-loop/misa-final.log`。
- compare / dump を `output/mark-loop/balance.md` / `units.md` に再生成し、
  `docs/balance.md` / `docs/units.md` と差分0。audit はずれ0件。

Godot の頭なし18再生は終了コード0・合格マーカーあり。ただし終了時に Font / CanvasItem / ObjectDB の
解放警告が残る（既存の演出検証でも記録されている種類）。ミサの実台本検証でも終了時に同種の警告が出る。
通常描画の4ケースと残像消去検査にはこの警告は出ていない。
BattleSim の Release ビルドには既存コードの警告が33件あった。生成結果は一致。
