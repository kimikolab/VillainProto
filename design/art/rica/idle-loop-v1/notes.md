# リィカ 戦闘待機モーション v1

作成日: 2026-10-24。ユーザーの待機モーション作成依頼。
状態: 正しい周期のプレビューをユーザーが承認し、Bをゲームへ適用済み。固定64コマの初版比較GIFには不具合がある。採用プレビューとゲーム検証は `../idle-loop-v2/` を参照。

## 元絵と動き

採用済みの `DemoApp/assets/portraits/battle/rica_idle_right.png`（戦闘リメイクv6）をそのまま使用。1024×1536、表示高2.10、足元余白40/1536。SHA-256は `prompt.json` に保存。

浅い膝の曲げ伸ばしに胴・肩・腕を連動させる待機。両足の位置、両手の握り、画面右側の一本の三つ編みを維持し、シャベルの持ち手・柄・ソケット・刃は一本の剛体として動くよう指定。深いしゃがみ、前傾の増大、攻撃や掘削を避ける。

## 再現

`design/IDLE_LOOP_COMFYUI_GUIDE.md` と `tools/comfy_idle_loop/idle_loop.py` の既存手順を使用。Wan2.2 I2V 14B、640×960、81コマ、16fps、20steps、CFG3.5、shift8、seed2022/5055。Lightning・始点終点一致・固定レイヤーの上貼りは使わない。

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 tools/comfy_idle_loop/idle_loop.py gen rica --seeds 2022,5055
```

- `prompt.json`: 元絵の指紋、キャラ設定、組み立て済み正負プロンプト。
- `workflow-s2022.json` / `workflow-s5055.json`: 投入グラフ。
- `jobs.json`: ComfyUIジョブID。
- `package_previews.py`: 生成連番・元動画・チェックシートの保存と静止/A/Bの比較GIF作成。

購入済みNatureBlocksとEffectBlocksの一覧・実ファイルを確認した。いずれも背景・エフェクト用で、この人物の関節を動かす待機アニメーションには適用しない。

## 候補と確認結果

| 候補 | タグ | 区間・コマ数 | 頭頂の上下 | 靴底の行 | 継ぎ目 |
|---|---|---|---:|---|---:|
| A・控えめ | `rica_s2022_final` | [39,77)・38コマ / 2.375秒 | 6px | 933固定（元絵934） | 1.21x |
| B・Aより動きが分かりやすい | `rica_s5055` | [22,61)・39コマ / 2.4375秒 | 9px | 934固定（元絵934） | 1.02x |

両方640×960 RGBA、16fps、緑の抜け残り0。頭と腰の横ずれ幅はA13.7px、B19.6px。Aは元の継ぎ目1.89xが目立ったため、既存のrecutで末尾6コマを混ぜた。Bは生成コマをそのまま使用。幾何変形・固定レイヤーの上貼り・往復再生は使っていない。

生成ノードは両方に「動きが小さすぎる」警告（A元動画1コマ差1.22、B2.22、閾値2.5）を出した。完全な静止ではなく、頭・胴・腕と衣服の動きは確認できるため、控えめな待機として比較に残した。ゲーム表示サイズで十分に動いて見えるか、自然かはユーザーの動画評価で決める。静止画のシートだけで採用を断定しない。

- `comparison.gif`: 不具合のある初版資料。固定64コマの境界で周期の途中から先頭へ飛ぶため、採用確認には使わない。`../idle-loop-v2/B-loop.gif` を使用する。
- `A/` / `B/`: 採用可能な連番、静止との比較GIF、ループWebP、確認シート、元動画、候補情報。
- `result-rica_*.json`: 生成2本とAの切り直しの実行履歴。生成は各951秒・660秒で正常終了。切り直しの待ち時間にはBの生成待ちを含む。

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 tools/comfy_idle_loop/idle_loop.py check rica_s2022_final rica
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 tools/comfy_idle_loop/idle_loop.py check rica_s5055 rica
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/rica/idle-loop-v1/package_previews.py rica_s2022_final rica_s5055
```

初回提示時点ではゲーム配置・コード変更・ゲーム検証は行っていなかった。承認後のゲーム適用は `../idle-loop-v2/notes.md` に記録。
