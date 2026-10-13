# ソムの手番・割り込みを二種の雷精霊へ（2026-10-14）

ユーザーの「電球から顔と手足がある、半分電気の精霊へ」と、比較案をゲームへ適用する依頼に基づく。
旧 `CODEX_BRIEF_SOM_SPIRIT.md` の「光点だけ・口なし・四足の気配だけ」の見た目は、この最新の依頼で置き換える。
召喚、寝返り、投射、共鳴返し、打ち返し、暴発、光の回収は既存の台本のまま。

## 採用した姿

- 手番：B系の「契約の雷人形」。細身の浮いた仮面と身体の断片。静かに形を組み、低い弧で滑る。
- 緊急の割り込み：A系の「跳ねる雷精」。大きな顔、悪戯っぽい笑み、指差す手、膝を畳んだ姿。
  身体が素早くつながり、高い弧で跳ねる。以前の大型の光球とは違い、雷人形より小柄。
- 紫白の雷、胸の契約紋は共通。寝返りの拍で両方が顔と手をソム側へ向け、契約紋が割れる。
  仮面と身体の断片は紫白を保ち、水色の電気とその白い芯だけを描画シェーダーで黄白へ置換する。
  投射後は身体が25%縮み、消えると火花・光粒へ散る。
- 円形の光輪と向きだけの光点を外し、顔・手足の画像と、電気の腕・脚を結ぶ細い動く稲妻を使う。
  顔の手前を稲妻が横切らない密度に抑える。表示名はBattleCoreに従う。
- 出現・ちらつき・浮遊・飛行・構えは既存の再生時計で動かす。シェーダーは `TIME` を使わない。

本編の呼び分けは既存の `call.Emergency` を使用。`SomBeast3D` と旧単体召喚に接続。
`UiKit` の通常立ち絵も雷人形へ、旧 `fodder_majestic*` 差分は緊急の雷精へ向けた。
この作業はBattleCore、数値、イベントの種類・順序を編集しない。

## 素材・購入素材

| ファイル | 用途 |
|---|---|
| `DemoApp/assets/fx/som_spirit_turn.png` | 手番の透過画像、1024×1536 |
| `DemoApp/assets/fx/som_spirit_interrupt.png` | 緊急の透過画像、1254×1254 |
| `design/art/som/som-spirit-roles-concept.png` | ユーザーが適用を依頼した比較案 |

画像は内蔵ImageGenで作成。透過PNGのアルファを保持してコピーし、ピクセル編集をしていない。
参照元は同じ会話で作成したA/B/C案と二種の比較案。
原本は `$CODEX_HOME/generated_images/01a13094-2770-7573-b56a-7636c07766b5/`：
比較案 `exec-9f047691-6f97-4453-976b-323e3f587575.png`、
手番 `exec-cc1bbcc0-443a-4657-a992-c5fb661ce7b7.png`、
割り込み `exec-03c735f0-d566-4b1d-8bdb-ace008f85df8.png`。

購入素材一覧とEffectBlocks v4の `energy/electric_sparks_1.tscn` 等の実ファイルを確認。
顔と手足を持つキャラクター素材は該当しないため画像を作成し、帯電・着弾・散開の火花は既存の購入素材を継続採用。
購入原本・共有リソースを編集せず、購入素材本体はGitへ追加しない。

## 生成プロンプト

手番のベース生成：

> Use case: stylized-concept. Production transparent PNG game character sprite based strictly on the LEFT tall elegant spirit in the reference sheet. Extract/redraw ONLY that one character, with no headings or other characters, fully isolated on genuinely transparent background. Preserve the reference's slender floating rune-mask lightning marionette design, long angular flame-shaped head, narrow sly luminous eyes and faint confident smile, floating spectral mask/body fragments and two arms with recognizable hands/two long legs with pointed feet. Full-body, facing toward SCREEN RIGHT in a clear three-quarter view, slight poised spell gesture with one hand; long graceful silhouette and clear readable face at small game scale. Preserve the pale white-lilac translucent magical material and exposed white/cyan electric anatomy, at least half the torso, one arm and one leg made from branching lightning, with real transparent negative-space holes. Keep the body and face mostly ivory white/neutral pale silver with only very faint violet shading so the game can tint the sprite violet then gold; dark charcoal eye sockets and thin graphic edges help readability. Strong defined coherent fragments, restrained tight electric glow, no giant aura/haze or spark cloud. REMOVE the glowing circular chest contract symbol because the game overlays an animated separate contract sigil at the chest; the chest remains open electrical anatomy. No floor, background, shadows, text, logos or borders. Center full figure in a portrait canvas, evenly comfortable 8 percent transparent margins, never cut off head/hands/feet. Hand-painted anime fantasy 2.5D game sprite matching the reference. Not flesh, metal armor, cat, animal or lightbulb.

手番の最終透過編集（ベース生成画像を参照）：

> Precise game-sprite background extraction edit. Keep this exact lightning marionette character identity, pose, face, body fragments, limbs, white-cyan lightning lines and canvas dimensions. REMOVE the entire broad violet/gray diffuse background aura and fog behind and around the figure. Make all space outside the thin lightning filaments and spectral fragments fully transparent, including the large gaps between arms/torso/legs. Tight 1-3 pixel electric edge glow is fine, but no broad halo or smoke. Preserve dark eye sockets and the fine dark line shading on actual spectral fragments; do not delete character details. No other changes, no text, no ground. Result must be a clean fully isolated transparent PNG sprite that can sit on a game battlefield without a rectangular or cloudy background.

割り込みの最終生成：

> Use case: stylized-concept. Production transparent PNG game character sprite based strictly on the RIGHT small mischievous lightning spirit in the reference sheet. Redraw ONLY that single right character with no headings or other characters, fully isolated on genuinely transparent background. Preserve reference A style imp-like lightning elemental: large angular flame head, mischievous expressive slanted glowing eyes and small cocky grin, no animal face. Compact floating body in a bent-knee springing airborne pose, two distinct arms with visible hands and two bent legs with pointed lightning feet. Face aimed toward SCREEN RIGHT in a clear three-quarter view; one hand reaches/points screen right, so horizontal flipping later reads as turning back toward its summoner. More irregular and urgently half-formed than the tall spirit. At least HALF the body/one cheek/one arm/one leg made of exposed branching electricity, actual transparent negative-space holes in abdomen/joints and between coherent pale spectral fragments. Strong silhouette, larger face/hands than the tall marionette, readable at small game sprite scale. White/neutral silver ethereal fragments with faint pale lilac shadows and white-cyan electrical filaments, tight highlights with dark charcoal eye sockets for contrast. NO huge aura, NO outer fog/haze, only a few small nearby sparks. REMOVE the circular glowing chest contract symbol because the game overlays it separately. Center entire full body within square canvas with generous 10 percent transparent margins on all sides; do not cut off flame crest, hands or feet. Hand-painted anime fantasy 2.5D sprite matching reference. No opaque background of any color, no floor, shadows, text, logos, border, human skin, flesh, fur, real animal, collar, machinery, metal armor or lightbulb. Electricity must constitute anatomy, not merely decorate an opaque humanoid.

## 確認方法

`SomCheck.tscn -- --visual-only --roles-only --roles-captures` で二種を並べて召喚・飛行・寝返り・投射。
左右×1倍／4倍、一時停止、途中の速度変更、中断を本編の描画メソッドで確認する。
画像はGit除外の `design/art/som/game-check/roles/`。既存の群れ・緊急・共鳴・実台本再戦も確認する。

## 検証結果

- DemoAppビルド、Godotの素材インポート成功。既存 `BorgAttackClipCheck` のCS8602警告1件。
- 二種の並列表示：左右×1倍／4倍の4ケース。顔と手足、透過の身体、配色の反転、向き、投射での縮小を描画して確認。
  一時停止、飛行中の速度変更、中断後の消去も成功。
- 群れ5体・緊急：左右×1倍／4倍の4ケース成功。
- 旧い単体召喚・飛行・回収・衣：左右×1倍／4倍の4ケース成功。
- 共鳴・命中・糸・粛・打ち返し：左右×1倍／4倍の20ケース成功。本編画面の共鳴と打ち返しも成功。
- 実台本11ケースを初戦・再戦の22戦で完走。召喚、構え、投射、各分岐、消去、光、衣、瓦礫、最終HPが台本と一致。
- `compare` と `dump` は現行 `docs/balance.md`／`docs/units.md` と差分0行、`audit` はずれているファイル0件。
  生成物を上書きせず `output/som-roles-*` に確認用出力を保存。
- 終了時にFont／CanvasItem／共有テクスチャの解放警告が出る。上記の確認は完了印を出して終了コード0。

実台本の確認中に、蓄電を消費しないシガの鞭では「蓄電・使った」が出ず、
割り込み後の光を元の連鎖へ戻せない既存の描画不具合を検出した。
`SomPresentation` に未払いの連鎖へ戻す処理を追加し、光の数が異なる場合と同じ場合の回帰検査を追加。
粉の現行版ではクグへの雷が先に共鳴へ入るため、糸の分岐の実台本検査には粉のいない固定編成も加えた（計120戦）。
いずれもDemoAppの表示と検証だけで、BattleCoreの変更は含まない。

### 配色の追補（2026-10-14）

ユーザーの追加依頼により、全身の黄白化から、電気だけの黄白化へ変更。
既存PNGの水色の部分を色差で選び、その近傍の白い稲妻の芯も含める。
紫白の仮面・身体の断片・暗い目の縁は元の配色を残す。画像原本・購入火花の共有材は変更しない。
購入済みEffectBlocksの電気火花は継続使用し、今回の調整は自作の精霊描画シェーダーのみ。
DemoAppビルドと、二種の左右×1倍／4倍の4描画ケースが成功。寝返り後の画面で紫白と黄白の併存を確認。
確認ログは `output/som-spirit-palette.log`、変更前の画面は `game-check/roles/palette-before.png`。
