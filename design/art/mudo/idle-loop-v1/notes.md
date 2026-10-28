# ムド：泥の外套型の戦闘待機モーション v1

2026-10-28。ユーザー依頼。現在のゲーム用通常立ち絵
`DemoApp/assets/portraits/battle/mudo_idle_right.png` を元にComfyUIで制作する。
怒りの巨大腕姿は通常待機に混ぜない。静止画と内側の陰を保持する。

## 制作条件

- リポジトリ指定の `design/IDLE_LOOP_COMFYUI_GUIDE.md` と `tools/comfy_idle_loop/idle_loop.py` を使用。
- Wan2.2 I2V 14B high/low fp8、640×960、81コマ、16fps、20steps、CFG3.5、shift8。
- seed2022/5055、タグ `mudo_mantle_idle_v1_s2022` / `mudo_mantle_idle_v1_s5055`。
- フードと外套が一緒に沈んで戻り、小さな腕と裾が遅れて揺れる。泥だまりの位置を固定し、顔・丸い開口・陰・灰色の泥を維持する。
- 通常の人物用プロンプトには膝や足の指定があるため、今回は `full` で脚のない泥の外套に合わせたプロンプトを指定。
- 元絵のハッシュ・寸法とプロンプトは `source.json`、APIワークフローは `*-workflow.json`、実行IDは `jobs.json`。
- 購入済みEffectBlocks/NatureBlocksの一覧・実ファイルは前の立ち絵適用時に確認済み。今回の全身待機連番には該当しないため、採用立ち絵から生成。
- ComfyUIの共有サーバーは停止していたためlocalhost限定で起動。既存モデル・ノードを使用し、更新・再インストールは行っていない。
- 事前検査で `SaveVideo` の新しい動的codec入力不足を検出。`idle_loop.py` の同ノードへ `format.codec=auto` を追加し、ワークフロー検査でエラー0を確認してから生成。

```powershell
$env:PYTHONUTF8='1'
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py gen mudo --seeds 2022,5055 --prefix mudo_mantle_idle_v1
```

## 候補の状態

候補A（seed2022）は生成成功。81コマのうち[49,73)の24コマ・1.50秒を自動抽出。
継ぎ目は通常のコマ差の0.55倍、接地920行固定（元絵920行）、頭頂上下22px、頭と腰の横ずれ幅9.5px、緑の残り0px。
暗背景と市松背景の4コマ検品シートで内側の陰・顔・小腕・裾を確認。継ぎ目の混合処理は不要。
候補B（seed5055）も生成成功。初版38コマの継ぎ目1.33倍を、末尾6コマの混合で補修した39コマ・2.44秒を提示する。
修正版の継ぎ目は通常のコマ差の0.48倍、接地920〜921行（元絵920行）、頭頂上下66px、頭と腰の横ずれ幅26.7px、緑の残り0px。
脚のない外套全体がへたる動きであり、腕を上げながら大きく沈む。姿勢の変化はAより強いため、好みは比較GIFで判断する。

| 候補 | 内容 | コマ／秒 | 継ぎ目 | 下端 | 容量（PNG連番） |
|---|---|---|---|---|---|
| A | 控えめな沈み込みと小腕の揺れ | 24／1.50 | 0.55倍 | 920固定 | 約21.2MB |
| B | 腕を持ち上げ、大きくへたる | 39／2.44 | 0.48倍 | 920〜921 | 約25.1MB |

いずれも顔・丸い口・内側の陰が残ることを暗背景と市松背景の検品シートで確認。
Aは泥だまりの左端のみがキャンバスの端に7〜9画素接触する。頭・腕・外套の上部に接触はない。
Bは四辺とも不透明な画素0。候補ごとの数値は `candidates.json` に記録。

`candidates/A/` と `candidates/B/` に透過連番・比較GIF（左が元絵／右が動作）・透過WebP・検品シート・生成元動画を保存。
ワークフローと終了履歴を含めて再現可能な状態で保存した。
ユーザーがAを選択したため、同日の追記のとおりゲームへ導入した。Bは比較用に保存。

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py recut mudo_mantle_idle_v1_s5055 --crossfade 6 --min-length 24 --max-length 48
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py check mudo_mantle_idle_v1_s2022 mudo
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py check mudo_mantle_idle_v1_s5055_final mudo
& 'D:/comfyui/venv/Scripts/python.exe' design/art/mudo/idle-loop-v1/package-candidates.py mudo_mantle_idle_v1_s2022 mudo_mantle_idle_v1_s5055_final
```

生成2本と切り直しは成功。プロンプトJSON／Python構文の検査、ワークフロー事前検査、差分の空白検査も合格。
候補作成時点ではゲームへ未導入。コミットは未実施。

## Aのゲーム適用（2026-10-28）

ユーザーの「Aで」を受け、`idle_loop.py install mudo_mantle_idle_v1_s2022 mudo` で
`DemoApp/assets/portraits/battle/idle_loop/mudo/mudo_idle_00.png`〜`mudo_idle_23.png` に導入。
640×960・24コマ・16fps・1.50秒、約19.3MB。全コマが選択された候補Aと画素単位で一致することを確認した。
小さな左端の接触を含め、承認された動きを変更する変形や静止レイヤーの合成は行っていない。

読み込み・巡回・暴発中の停止・終了後の再開は既存の待機再生を使用。
ムドは従来静止画だったため、停止と速度の接続を追加：既存 `SetMudoClock` がムドの待機時計も更新し、`ProcessIdleLoop` が停止中のコマを維持する。
描画高2.25・足元余白61/1515・怒り姿の内側の陰は維持。BattleCore・BattleSim・生成docsには変更なし。

### 検証

- 導入全24コマが保存済みAと画素単位で一致：`MUDO_IDLE_INSTALL_OK`。
- DemoAppビルド：警告0・エラー0。
- `IdleLoopCheck`：ムドの開戦直後の採用A・全24コマ・表示高・停止と倍速・暴発姿の保持と待機復帰・燃焼・死亡・蘇生・勝利・敵側反転が合格。
- `PortraitStateCheck`、`FireCheck --replay`、`MovementPortraitCheck`：全て成功マーカーと終了コード0。
- `MudoCheck`：停止していた攻撃力0の手番、ボス・本編第4波の再戦、暴発10/4件・弱体30/10件、台本HP、変身時の胴の高さ・足元・内側の陰、停止・速度・中断が合格。
- Godot終了時の既存Font/CanvasItem/ObjectDB解放警告は残る。
- GPU実戦：規定ボスseed0は416通知・65攻撃・6ターン勝利で完走。31.33秒の `output/mudo-mantle-applied/idle-a-boss-seed0.mp4` と、拡大半速 `idle-a-focus-half-speed.mp4` を保存。接地・通常Aの動き・内側の陰を保った巨大腕姿・待機復帰を映像の検品シートと実画面で確認。
- `compare` は `docs/balance.md` と差分0、`audit` はずれているファイル0件。
- コミットは未実施。
