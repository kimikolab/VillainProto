# 導火線のチリ — 演出素材 v1

**本編接続済み:** 5つの爆弾イベントと行動差分をゲームへ接続した。現在の挙動・検証は [本編接続の記録](../../../CHIRI_PRESENTATION.md)。

**現在の立ち絵:** ユーザー指定の新衣装をゲームへ適用済み。原本・配置・確認結果は [game-portraits-v2](../game-portraits-v2/README.md)。以下は素材初版の制作記録。

2026-11-01。元のCを基準に、演出メモ `design/CODEX_BRIEF_CHIRI.md` の5動作を表現する素材を制作した。
今回は素材と試写室まで。本編の `Main.cs` の爆弾イベント処理にはまだ接続していない。
本編への接続時には、既存の汎用StatusGain表示を回避し、BattleCoreが指定した対象・段数・件数だけを再生する。

## 素材

| 種類 | ゲーム内の配置先 | 内容 |
|---|---|---|
| 仕掛ける差分 | DemoApp/assets/portraits/battle/chiri_plant_idle_right.png | 右向きで爆弾を差し出す接触姿勢。1024×1536・RGBA |
| 手を離す差分 | DemoApp/assets/portraits/battle/chiri_release_idle_right.png | 同じ姿勢で前の手だけ空にした解放姿勢。1024×1536・RGBA |
| 爆弾本体 | DemoApp/assets/fx/chiri/bomb_body.png | 元のCの黒鉄・真鍮・星飾り。1254×1254・RGBA。導火線は別描画 |
| 導火線の印 | DemoApp/assets/fx/chiri/bomb_fuse_3.png〜bomb_fuse_0.png | 同じ本体・位置・キャンバスで導火線を縮めた4枚。256×384・RGBA |
| 不発の印 | DemoApp/assets/fx/chiri/bomb_dud.png | 火花・数字を消して暗くした同一爆弾。256×384・RGBA |
| 再生素材のシーン | DemoApp/assets/fx/chiri/chiri_material_kit.tscn | 設置の飛翔、導火線と火花、爆発、火球、不発の消失を再利用可能なNode3Dとしてまとめた |
| 効果音5種 | DemoApp/assets/audio/se/chiri/*.wav | 設置・火花・爆発・転送・不発。標準ライブラリによるオリジナル合成音 |

原本の画像3枚はこのフォルダにも保存した。静止マーカー原本は `glyphs/`。
画像生成は内蔵image_gen。プロンプト全文は `prompts.md`。
導火線の静止画像は、生成した爆弾本体とコードで描いた導火線をGodotで透過レンダリングして書き出したもの。
通常・待機立ち絵は上書きしていない。新差分の靴底余白は49/1536と48/1536で、UiKitへ登録した。

## 行動との対応

| メモの出来事 | 素材と呼び出し | 接続時に守ること |
|---|---|---|
| bomb_plant | 仕掛ける→手を離す→待機。kit.plant(from, head_position, ally) | 段数3。敵も味方も同じ爆弾。味方の数字は水色、敵は金色 |
| bomb_fuse | kit.shorten(marker, remaining) | 渡された2→1→0だけを表示。一撃に火花を添える。時間では縮めない |
| bomb_blast | markerを除去しkit.blast(position, size, delay) | 台本が指定した爆発だけ。誘爆は明示した短いdelayでずらす |
| bomb_mirror | kit.mirror(from, to)。arrivedでkit.blast(to) | FromIdで指定された到達先だけを使う。味方の出発点では爆発しない |
| bomb_dud | kit.dud(marker) | 0.48秒で暗く下がって消える。火花・爆発・ダメージは出さない |

仕掛ける差分は約0.16〜0.20秒、手を離す差分は約0.20秒を目安に待機へ戻す。
素材の時刻はプレビュー用であり、本編の命中時刻・件数・ダメージの定義ではない。
爆弾本体の絵には導火線がないため、解放後に飛ばす弾・印へ使う際は付属の導火線描画か静止マーカーを合わせる。

## 購入素材の確認と採否

原本: `D:/Assets/GodotAssets/EffectBlocks v4/PolyBlocks/EffectBlocks/`。
既存の導入手順: `DemoApp/PrepareAssetPreview.ps1 -AssetRoot 'D:/Assets/GodotAssets'`。
採用した配置先の依存は以下の2本。素材本体はGitへ追加しない。

- `res://PolyBlocks/EffectBlocks/assets/explosions/explosion_light.tscn`: 爆発に採用。Fireの球メッシュをfire_lightのFlame描画へ替え、薄い炎にした。煙は3粒・短命へ削減。火花・環状の閃光を重ね、顔を残す。
- `res://PolyBlocks/EffectBlocks/assets/fire/fire_light.tscn`: 転送火球の尾と、爆発の薄い炎に採用。Smokeは転送では非表示。短い尾と小さい芯で進行方向を示す。
- explosion_heavy / fireballs: 重い煙や複数方向の発射は今回の単発・向かい1体の表現と合わず不採用。impact系は導火線の小さな火花には大きいため、自作の短い粒を使う。

原本・共有ProcessMaterialは変更しない。インスタンスのProcessMaterialを深い複製にして調整する。
配置前に作者の反復スクリプト・音・ライトを外し、独立したTIMEやTweenではなく、呼び出し側の時計で制御する。
購入素材が未配置の場合は警告を出す。爆発・炎の完成した表示には購入素材が必要。
作者・取得・利用条件の記録は `GodotApp/ASSET_RESEARCH_AND_PLAN.md` の購入素材の節を参照。

## 使い方

```powershell
Godot_console.exe --path DemoApp res://ChiriMaterialPreview.tscn
```

画面の5つのボタンで個別試写。一時停止、0.25/1/2倍速、もう一度、通し再生、音の切替が使える。
GDScriptのみの独立した素材シーンなので、この試写室だけを使う場合はC#の再ビルドは不要。

```gdscript
var kit = preload("res://assets/fx/chiri/chiri_material_kit.tscn").instantiate()
kit.camera = battle_camera
kit.clock = func(): return Vector2(1 if playback_paused else 0, playback_speed)
add_child(kit)
var mark = kit.plant(chiri_hand_position, holder_head_position, true)
kit.shorten(mark, 2) # bomb_fuseのAmountだけを渡す
kit.dud(mark)       # bomb_dudが来た場合
kit.clear()         # 死亡後の対象整理・中断・再戦・シーン終了に合わせて呼ぶ
```

キットの位置引数はワールド座標。持ち主が移動する場合はmarker.global_positionを持ち主の頭上へ追従させる。
対象の生死・InstanceIdとmarkerの対応・待機差分への復帰は本編接続側が管理する。キットは戦闘判定を持たない。
火球のarrivedは視覚素材の到達通知で、ダメージを発生させる通知ではない。

## プレビューと検証

- `previews/chiri-materials.mp4`: 音付き7.54秒。5動作の録画。動画・音声各1ストリーム、181フレームの復号を確認。
- `previews/01.png`〜`05.png`: 動作別の静止画。
- `--capture`: 上記5枚を再出力。`--export-glyphs`: 透過マーカー5枚を再出力。
- `--record`とGodotの`--fixed-fps 24 --write-movie <path>.avi`: 動画の収録。MP4化の手順はencode_preview.py。
- `Godot_console.exe --headless --path DemoApp res://ChiriMaterialPreview.tscn -- --verify`: 停止中の時計、2倍速、3→2→1→0、火球の到達前後、不発、5動作の2回反復、clear後の残留0件を確認。

実行結果: `CHIRI_MATERIAL_VERIFY_COMPLETE phases=5 repeat=2 pause=ok speed=ok fuse=ok arrival=ok clear=ok`。
`dotnet build DemoApp/DemoApp.csproj --no-restore`も成功（警告0・エラー0）。
描画ありの5枚書き出し、動画収録、透過マーカー書き出しも完了。素材の見た目・透過を目視確認した。
BattleCore・Main.cs・バランス生成物は今回変更していない。本編の爆弾5イベントの表示接続と実台本の通し検証は、この素材制作には含めていない。
