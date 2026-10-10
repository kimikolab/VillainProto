# 標軸・号令・粛の演出確認と追加（2026-10-10）

正典は `PHASE291_CODEX_MEMO.md`、依頼後の決定（鼓舞なし・致死の庇いなし・叫びも粛で止まる）を優先。変更は DemoApp と本記録だけ。

## 先に確認した持ち越し

- `Main.Shutdown.cs` の Tween 停止・ノード削除・C# 参照解放は実装済み。循環×ボス seed 0 の頭なし再生は750イベント・T4で完走、終了コード0。大量の `Leaked unsafe reference` / Tween 警告と終了時クラッシュは再現しない。
- 未追跡と報告されていた `tou_idle_right.png.import` と `tou_powder_idle_right.png.import` は既にGit追跡済み。無視設定や重複素材は追加しない。
- Windows Godot 4.7.2 Monoで確認。Linuxの終了コード132自体は未検証。頭なしの終了には既存のFont / CanvasItem / Texture / ObjectDBの少数の解放警告が残る。

## 既存実装を維持した部分

標撃ちの常駐羽・濡れ衣・仇巡り・ドハの力配り・矢面の半減・ヒサの自己回復は実装済み（`HANDOFF_MARK_LOOP.md`）。
玉3枠・溢れの間引き・号令での玉の飛行と層の表示・庇い・沈黙の吹き出しも実装済み（`HISA_COMMAND_IMPLEMENTATION.md`）。
鼓舞は追加しない。庇いは `Cover` がある場合だけ再生し、致死判定は画面で作らない。古い演出検査の「庇って倒れる」は「庇い途中の終了でもHP不変・帰還」へ変更した。

## 粛で追加した部分

- 開戦から色が褪せ、音がこもる。伝令の上に法具と残りのひび数。上限は台本の `HushState.Slot` を読み、戦闘ルールの15回を画面で再実装しない。
- `Sealed` → `HushState`「ひび」で止められた駒から法具へ光線、亀裂を追加。騎士は剣を振り上げたところで固まり「封」。
- 「砕けた」で生存中の伝令から封印を解除。鎧と法衣の破片、破砕音、仮のコミカルな悲鳴SE、うずくまった仮絵と「ひゃあああっ！」の拡大表示。
- 報いの拍は1倍で1.35秒、2倍以上でも最低0.85秒。この待機中は次の台本へ進めず、直後の撃破で絵が見えなくなることを防ぐ。範囲攻撃の先行HP表示や既存の攻撃の束も `HushState` をまたがせない。
- 砕けた姿はその戦の間保持。砕ける前に倒れた場合は通常の死亡による解除。蘇生・複数保持者・再戦・途中離脱でも、他の保持者や新しい戦の膜を誤って消さない。
- 騎士の `Reaction Damage` を1ターン1回の斬り返しとして表示。肩代わりによる分割を1回にまとめ、剣は元の被弾先へ返す。砕けた後の各騎士の最初の反撃だけ赤い光と見出しで強調。
- 粛の間に手番の号令が来ても、ヒサの台詞・声は「…」。砕けた後は台本にある叫び・羽・仇討ちが通常どおり戻る。過去に止めた行動を再生成しない。
- 終了時は膜・法具・見せ場・鎖と自身の音響効果を片付ける。途中再戦では待機中の旧イベントを無効化する。
- 音のこもりは戦闘SE・BGM用の `BattleAtmosphere` バスに限定。既存の鎖の停止音（`hush_block.mp3`）と破裂音（`hush_break_1〜3.mp3`）はMasterへ直接流し、粛の下でも元の音色で鳴らす。

## 開戦時に伝令から広がる粛（2026-10-10）

- `BattlefieldView3D.HushOpening.cs`。保持者に青白い光と小さな輪、「粛・静まれ」の表示を出し、0.28秒の集光後、1.05秒で円状の波を画面全体へ広げ、0.16秒静止する。
- 既存の沈黙色を伝令の画面上の位置から塗り広げる。波の内側だけが褪せ、同じ進行度で戦闘音・BGMのローパスを20kHzから1.4kHzへ変える。停止音・鎖の破裂音が通るMasterは変えない。
- 追加指定の効果音ラボ「重力魔法2.mp3」を `assets/audio/se/hush_opening.mp3` として採用。集光0.28秒の直後、波の広がり開始に一度鳴らす。複数保持者でも一度。専用枠・-10 dB・原音程・全尺で、Masterへ送り粛のこもりをかけない。解除・終了・再戦で停止し、集光中の中断では予約も破棄する。
- 展開音追加・カットイン撤去後の検証: ビルド警告0・エラー0。左右×1倍/2倍で3.724秒のMP3・発音タイミング・一度だけの再生・中断時の予約破棄・別枠なし・破砕差分の復帰が合格。音源全体の復号・再生・停止も合格。音声検査は常設の `BattleAtmosphere` を初期バス数に含めるよう修正。`compare` / `dump` 差分0、`audit` ずれ0。ログ・実描画は `.tmp/hush/deploy-audio*`。
- `BeginBattle` で開始し、`BeginPlayback` は開戦波の完了を待ってからT0のイベントへ進む。約1.49秒の演出は倍速でも維持。粛の保持者がいない戦では追加の待機なし。
- 複数保持者はそれぞれから同時に広がる。再戦・終了でTweenと起点を破棄し、旧戦の待機を新戦へ持ち越さない。HPや戦闘の粛判定は変更しない。
- 検証: DemoAppビルド警告0・エラー0。左右×1倍/2倍で起点・途中の膜・全域への広がり・HP不変・粛なしの再戦が合格。`SealEffectCheck` の複数保持者・死亡・蘇生・移動・再戦も合格。
- 本編第2波×試遊・標 ボス台 seed 0を頭なし再生し、276イベント・T4・勝利・終了コード0。従来のFont/CanvasItem/ObjectDBの終了時解放警告は残る。`compare` / `dump` は既存生成物と差分0、`audit` はずれているファイル0件。
- 実描画は `.tmp/hush/opening-final-images/`。起点の光と表示、波の内外で色が変わる画面を確認。ログは `opening-final-console.log` / `opening-seals-console.log` / `opening-main-console.log`。

## 立ち絵の差し替え

通常は既存の `DemoApp/assets/portraits/battle/husher_idle_right.png` を利用。
砕けた後は `DemoApp/assets/portraits/battle/husher_shattered_idle_right.png`（既存通常絵と同じ成人女性が、下着と裂けた法衣だけになり、赤面・涙目で胸元を左腕で覆い、右手で腰の布を押さえながら立ち続ける差分）。手番で攻撃できる立ち姿を優先するという追加指示により、うずくまりから変更。剣は持たず、腕は二本。通常の身長を保ち、差分の足元の余白に合わせる。
現行の生成原本・プロンプトは `design/art/enemies/wave2/husher-shattered-right-v4.png` と `husher-shattered-v4-prompt.md`。1024×1536、RGBA透過。内蔵image_genで作成。旧版は比較用に保存。v3で胸元に残った腕の不整合をv4で修正。
`DemoApp/assets/fx/husher_shattered_placeholder.svg` は正式素材がない場合のフォールバックとして残す。
悲鳴はユーザー指定の効果音ラボ「きゃああーー！」を `assets/audio/se/husher_shatter_cry.mp3` へ無加工でコピー。`HushCry` は専用音声枠で再生し、倍速でも音程と全尺を保つ。既存の鎖の破裂音と重ねる。終了・再戦時は他の演出音と一緒に停止する。
指定元とコピーのSHA-256一致。DemoAppビルド警告0・エラー0。左右×1倍/2倍の頭なし検証でMP3（2.640秒）の再生・原音程・一度だけの発音・フェード短縮なしを確認し、`SHOCK_MARK_CHECK_OK`。ログは `.tmp/hush/cry-check-console.log`。

### 追加指示: 砕ける瞬間の差分（2026-10-10）

- `assets/portraits/battle/husher_shatter_burst_idle_right.png`。衝撃で肩当て・裂けた法衣が外へ飛び、両手を反射的に上げて口を開けて叫ぶ姿。剣なし・腕二本。通常絵と破砕後v4を参照した1024×1536のRGBA透過素材。
- 現行原本と生成プロンプトは `design/art/enemies/wave2/husher-shatter-burst-right-v2.png` / `husher-shatter-burst-v2-prompt.md`。v1の怒鳴るような顔を、上がった困り眉・丸く見開いた目・赤面で反射的な「キャー！」に修正。装備の弾け方は維持。v1原本も保存。
- 指定の悲鳴と同時に、戦場では実時間0.65秒表示して破砕後v4へ戻る。追加指示により右側の別枠カットインは撤去。戦場上の叫び姿・破片・破裂音・悲鳴、既存の報いの待機時間と平手攻撃は維持。
- 検証: ビルド成功（未変更のBattleCoreに警告2件）。左右×1倍/2倍の実描画で叫び姿→破砕後v4への復帰、MP3、平手、再戦を確認し `SHOCK_MARK_CHECK_OK`。画像は `.tmp/hush/burst-images/`。`compare` / `dump` は差分0、`audit` はずれているファイル0件。

### 追加指示: 破砕後の平手（2026-10-10）

- v2の腕の不整合を指摘され、剣を捨てて両手で体を覆う案へ変更。一度画像修正を保留したが、追加指示で剣の除去と腕の修正に絞った編集を行った。胸元の左腕をさらに描き直したv4を採用。表情と衣装の状態は維持。
- 演出のみ先行し、砕けた伝令の攻撃は通常攻撃音と剣閃から、横に払う平手の記号・乾いた合成SE・対象の短いのけぞりへ切り替える。判定は台本から受けた `HushShattered`、標的とHPは変更しない。
- `BattlefieldView3D.HushSlap.cs`、`assets/fx/hush_slap.svg`、`ShockMarkSound.HushSlap`。砕ける前は従来の攻撃、砕けた後の蘇生は平手を保持、次戦は初期化。
- 検証: DemoAppビルド警告0・エラー0。左右×1倍/2倍で通常攻撃→破砕→平手→蘇生→再戦の切替とHP不変が合格。実描画は `.tmp/hush/slap-advance-images/`、ログは `slap-advance-console.log`。この検証時のPNGはv2原本とハッシュ一致。
- `compare` は既存生成物と差分0、`audit` はずれているファイル0件。`dump` は今回未変更のBattleCore側にあるSparkRain/Store/Half/Doubleの4行が既存生成物より増えている。docs/は変更しない。通常のBattleSim出力先は別プロセスが使用中だったため、検証ビルドを `.tmp/hush/slap-sim/` へ分離して実行。

## 検証

- DemoAppビルド成功、警告0・エラー0。
- 左右×1倍/2倍の実描画を12枚撮影し、ひびの残数・色の復帰・破砕後の姿・斬り返しを確認。再戦、破砕待機中の中断、音響効果の除去も検査。
- 第2波×ボス台・標経済・三人組（seed 0）を各2回再生。ひび・破砕・反撃・既存演出の件数と最終HPが一致。ボス台は15ひび/1破砕/2斬り返し、標経済は15/1/0、三人組は12/0/2（砕ける前に伝令が倒れる）。
- ヒサの既存検証8条件×2回が合格。玉・号令・庇い・標層・回復・最終HPを照合。
- 標循環の既存検証（循環・三人組・守り型×ボス・近衛・大隊）9条件×2回と、左右×1倍/2倍が合格。力配り・矢面・標撃ち・仇巡りの件数も一致。
- 最終版の `SealEffectCheck`（複数保持者・死亡・蘇生・移動・再戦）が合格。第2波ボス台の頭なし実戦は276イベント・T4・勝利、終了コード0。音響効果の追加/除去の実数も確認。
- `compare` / `dump` の一時出力は既存 `docs/balance.md` / `docs/units.md` と差分0。`audit` はずれているファイル0件。BattleCore・docs/の変更なし。

ログ・画像は `.tmp/hush/`（作業出力）。再現コマンド:

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --hush --verify
Godot_console.exe --path DemoApp --rendering-method gl_compatibility res://ShockMarkCheck.tscn -- --hush --capture-dir=C:/works/VillainProto/.tmp/hush/images
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --hisa --verify
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --mark-loop --verify
```
