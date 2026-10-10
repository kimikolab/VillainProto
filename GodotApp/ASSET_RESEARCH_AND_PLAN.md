# Godot向けアセット調査と今後の計画案

調査日: 2026-10-10

この文書は、既存の素材・エフェクト・開発支援ツールを活用して、VillainProtoの表現と制作効率を改善するための相談メモ。
**候補の採用は未決定。購入・インストール・プロジェクトへの導入・動作検証は行っていない。**

保存先はユーザー指定の `GodotApp/`。今回、相性の確認に使った実装・画像は主に **`DemoApp/`** のもの。
以下のバージョンや描画方式はDemoAppの条件であり、GodotApp側で採用する場合はそちらの設定を別途確認する。

## 調査の前提

- DemoAppはGodot 4.7.2／C#／Forward+。2Dの駒を3Dの戦場に置く2.5D構成。
- 草原・城門などの背景と、炎・雷・毒などの専用演出がすでに実装されている。
- 戦闘の判定はBattleCore、Godot側は台本の再生と表示を担当する。アセット導入でもこの分離を維持する。
- 効果音には既存の素材と使用箇所の記録があり、追加するなら環境音やUI音の補強を優先する。
- 評価は配布元の説明・価格・利用条件、掲載画像、既存コードと保存済み戦闘画像に基づく。見た目の相性と導入負担は暫定的な判断。

確認した主なローカル資料:

- `DemoApp/project.godot`、`DemoApp/DemoApp.csproj`
- `DemoApp/README.md`
- `DemoApp/MeadowEnvironment3D.cs`、`DemoApp/FortressEnvironment3D.cs`
- `DemoApp/FireFx.cs`、`DemoApp/FireUltimateFx.Lance.cs`
- `DemoApp/assets/audio/se/SOURCES.md`
- `output/misa-arena-captures/misa-team0-speed1-idle.png`

## 探す場所

公式Asset Storeには素材だけでなく、カメラ制御・会話編集・地形作成などの機能もある。
旧Asset Libraryからの移行は作者ごとに行われ、自動移行ではないため、公式ストアと作者サイト・itch.ioを併用して調べた。

- [公式Asset Store](https://store.godotengine.org/)
- [公式説明: About the Asset Store](https://docs.godotengine.org/en/stable/community/asset_store/what_is_asset_store.html)

## 背景・エフェクトの候補

価格は調査時点の米ドル表示。セール価格は変動するため、購入判断時に再確認する。

| 候補 | 入手先・価格 | 内容と用途 | 暫定評価・確認事項 |
|---|---|---|---|
| [NatureBlocks](https://bukkbeek.itch.io/natureblocks) | 外部／通常$24.99、調査時$21.24 | 草木・岩・崖など500以上の素材と派生形、配置ツール、地形用素材、照明設定。草原・森林などの背景作りに使う。 | **有料背景の第一候補。** 掲載画像の柔らかい絵画調の表現は現在のキャラクターと合わせやすそう。Godot 4.6で制作、無料デモあり。新しい製品で、作者によるとチュートリアルは整備中。4.7.2での動作は未検証。 |
| [NatureForge: Stylized Meadow & Farm Kit](https://store.godotengine.org/asset/emace-art/natureforge-stylized-meadow-farm-kit/) | 公式ストア／無料 | 木・岩・草・柵・地面など121モデル。配置済みのデモと素材一覧シーンを含む。草原戦場の周囲を充実させる。 | **無料で最初に比較する候補。** Godot 4.7以上／Forward+、掲載対応範囲は4.7〜4.7.2。現在の条件と合うが、見た目は角張ったローポリ寄り。 |
| [EffectBlocks](https://bukkbeek.itch.io/effectblocks) | 外部／通常$9.99、調査時$8.49 | 炎・煙・毒煙・雷・着弾・衝撃波など100以上。Godot用シーンとして配置でき、色・大きさ・時間を調整できる。 | **既製エフェクトの第一候補。** 専用技の全置換より、着弾・火の粉・煙などの部分利用を想定。現行v4はGodot 4.6向けに更新済み。無料の閲覧用デモあり。4.7.2と現在の再生制御との相性は未検証。 |
| [Effekseer](https://effekseer.github.io/jp/index.html) | 外部／無料 | エフェクトを画面で編集する専用ツール。配布サンプルを改造し、魔法や必殺技を作り込む用途。 | **継続的に専用演出を制作する場合の有力候補。** Godot 4用プラグインあり。導入・制作手順の習得・再生制御への接続はEffectBlocksより手間がかかる見込み。 |
| [Free Fantasy Medieval Houses and Props Pack](https://store.godotengine.org/asset/emace-art/free-fantasy-medieval-houses-and-props-pack/) | 公式ストア／無料 | 中世風の家屋・宿・小物など80以上。村や街道沿いの戦場、作戦マップの拠点に使う。 | 必要な場面ができた時の候補。Godot 4.7以上向け。城壁の直接置換より、人が暮らす場所の情報を背景に足す用途。 |

NatureBlocksとEffectBlocksは独立したパック。調査時には、[2製品のセットが$27.99](https://itch.io/s/199061/godot-effectblocks-natureblocks-sale)で販売されていた。
購入を先に決めず、無料デモで画風と内容を確認する。

Effekseerの追加資料:

- [公式サンプルエフェクト](https://effekseer.github.io/jp/contribute.html)
- [Godot 4用プラグイン](https://github.com/effekseer/EffekseerForGodot4)
- [Godot用プラグインの日本語説明・動作環境](https://effekseer.github.io/Help_Godot/v4/ja/introduction.html)

## 素材以外の候補

| 候補 | 費用・ライセンス | 役立つ場面 | 優先度・導入条件 |
|---|---|---|---|
| [Phantom Camera](https://store.godotengine.org/asset/ramokz/phantom-camera/) | 無料／MIT | 滑らかなカメラ移動、複数対象を収める構図、カメラ間の切り替え。 | **中。** 必殺技の寄りや戦場への導入に有効。既存カメラ制御との役割整理が必要。 |
| [Sound FX Starter Pack Vol. 1](https://store.godotengine.org/asset/ovani-sound/sound-fx-starter-pack-vol/) | 無料配布／商用利用向け独自ライセンス | Ovani Soundの144音。環境音、魔法、中世風の音、UI音など。 | **中。** 既存の攻撃音を一括で置き換えず、森の風や画面操作音など足りない部分に使う。 |
| [Dialogue Manager](https://store.godotengine.org/asset/nathanhoad/dialogue-manager/) | 無料／MIT | 分岐会話、選択肢、翻訳対応。C#対応、Godot 4.6以上。 | **会話を追加する時に高。** 仲間加入や戦闘前後の掛け合いを作る時に検討。 |
| [Terrain3D](https://store.godotengine.org/asset/tokisangames/terrain3d/) | 無料／MIT | 地面の隆起、地表の塗り分け、草木の配置。C#からも利用可能。 | **現状は低〜中。** 作戦マップを広く作り込む段階で有力。現在の小さな戦場は、背景セットだけで改善できる可能性が高い。 |
| [Vfx Animation Player](https://store.godotengine.org/asset/mariano-javier-suligoy/vfx-animation-player/) | 無料／MIT | 編集画面の時間軸を動かし、粒子エフェクトの途中状態を確認する。Godot 4.7以上。 | **制作方法次第。** AnimationPlayerで演出を組む場合に便利。現在のC#とTween中心の演出への効果は限定的。ゲーム内の台本再生を自動で巻き戻せるツールではない。 |
| [Godot AI](https://store.godotengine.org/asset/dlight/godot-ai/) | 無料／MIT。接続するAIサービスの費用は別 | AIアシスタントとGodotエディタを接続し、シーン・ノード・素材の操作や画面確認を支援する。 | **別枠で検討。** エディタ上で素材を配置して試す作業の助けになりそう。現在の制作手順でどれだけ効果があるかを確認する。 |

行動AIや状態管理の大型プラグインは、今回の見た目改善では優先しない。
BattleCoreに戦闘判定がすでにあるため、Godot側に別のゲームロジックを持ち込む利点は小さい。

## 利用条件のメモ

以下は公開説明の要約であり、採用時は取得した版に同梱されたライセンスを確認・保存する。

- **NatureForge:** 商用利用可、クレジット不要。素材単体や素材パックとしての再販売・再配布は禁止。
- **Medieval Houses:** 独自のEmacEArt Asset License。掲載ページは同梱の`LICENSE.txt`を参照する形なので、採用前に全文を確認する。
- **NatureBlocks／EffectBlocks:** 商用・非商用のプロジェクトで利用可。素材自体の再販売・再配布は禁止され、改変した素材も対象。
- **Effekseer:** Godot用プラグインはMIT。公式配布サンプルにはCC0のものが多いが、旧作品には「Effekseerと一緒に使用する場合のみ」という別条件がある。ツールと個々の素材の条件を分けて確認する。
- **MITのツール:** 採用したコードの著作権表示・ライセンス文を保持する。
- **Ovani Sound:** 商用利用向けの独自ライセンス。素材を採用する時に同梱条件を確認する。

素材の公開リポジトリへの同梱可否は、ゲームに組み込んで配布できるかとは分けて確認する。
採用した素材は、製品名・作者・取得元URL・取得日・版・ライセンス・使用箇所を記録する。

## 改善の方向性

保存済みの戦闘画像からは、木の形、草のまとまり、地面の変化を整える効果が大きそうだと判断した。
背景の密度を上げる場所は画面の奥と左右を中心にし、戦闘域の中央は駒・足元の輪・HP表示を読みやすく保つ。

エフェクトは、現在の固有演出の意味を保ちながら、着弾・煙・火の粉・破片などの部品を補う。
派手さだけで選ばず、「誰が誰に何をしたか」が読み取れることを優先する。

## 今後の計画案

すべて未着手。順番は提案であり、採用や購入を決定したものではない。

### 1. 無料デモで背景とエフェクトを比較する

1. NatureForgeのデモとNatureBlocksの無料デモを見て、背景の方向性を比較する。
2. EffectBlocksの無料デモから、着弾・煙・雷を中心に確認する。
3. 配布デモの見栄えと、自分たちの画面で使える素材・設定を区別する。
4. 「無料背景で十分」「有料背景を試したい」「エフェクトだけ試したい」のどれに進むか決める。

比較する項目:

- キャラクターとの画風の馴染み。
- 木・草・地面などの質感と、色や明るさの調整しやすさ。
- 個々の素材を必要な場所だけに使えるか。
- 現在の制作方法で扱いやすいか、追加の制作手順がどれだけ必要か。

無料デモが閲覧用実行ファイルだけの場合は、プロジェクトへの組み込みまでは確認できない。
その段階の結論は「外観の候補選定」に留める。

### 2. 草原の戦場1つで背景を試す

対象はまずDemoAppの草原戦場1つ。別の検証シーンで、既存版と比較できる形にする。

- カメラ位置と駒の配置を揃える。
- 木・岩・草・地面を一度に全部替えず、効果を比較できる単位で試す。
- 背景に合わせて照明と色を調整する。配布デモの照明設定を重複して持ち込まない。
- 中央の見通しと、足元の輪・HP表示の読みやすさを確認する。
- 同じ条件で画像とフレーム時間を比較する。作者のデモの性能をそのまま自分たちの性能と扱わない。

この段階で、背景1セットの採否を決める。村・城門・作戦マップへの展開はその後にする。

### 3. エフェクトを少数だけ実戦に組み込んで比較する

初回は「通常攻撃の着弾」「炎の煙・火の粉」「雷の着弾」など、既存版と差を見やすい数例に絞る。

- 色・大きさ・持続時間を現在の戦闘に合わせる。
- 単発だけでなく、複数の駒が同時に発動する場面を確認する。
- 一時停止・再開・倍速に同期し、演出だけ進んだり残ったりしないか確認する。
- 駒の死亡、シーン終了、戦闘の再開始で演出が残らないか確認する。
- 駒やHP表示が隠れないこと、対象と発動者が読み取れることを確認する。
- 再生側だけで完結させ、戦闘結果やBattleCoreの判定を変えない。

EffectBlocksで不足する専用演出が具体化したら、Effekseerのサンプル1つを使った検証に進む。
最初から両方の制作方式を全面導入しない。

### 4. 必要が明確になった支援ツールを追加する

- カメラ演出の調整が増えたらPhantom Camera。
- 会話・仲間加入イベントを作るならDialogue Manager。
- 作戦マップの地形編集が必要になったらTerrain3D。
- AnimationPlayerによる粒子演出の編集を始めたらVfx Animation Player。
- 環境音・UI音が不足したらOvani Soundのパック。
- エディタ内の素材配置や画面確認が作業上の負担になったらGodot AI。

## 現時点の推奨

最初の比較対象は **NatureForge／NatureBlocks／EffectBlocks**。
無料デモで画風と内容を確認し、背景1つ・エフェクト数例に絞って改善量と導入負担を評価する。
Effekseerは、その後も専用の魔法・必殺技を継続的に制作する場合の候補として残す。

採否の基準は「素材の数」ではなく、実際の戦闘画面の見やすさと完成度が上がるか、今後の制作が楽になるか。
