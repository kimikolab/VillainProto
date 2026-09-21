# 第五波の戦闘立ち絵適用

承認済みv1の5枚を `DemoApp/assets/portraits/battle/` に採用。

| 素材 | 戦闘画像の駒ID |
|---|---|
| martyr-idle-right-v1.png | axeman_g |
| hero_v-idle-right-v1.png | hero_v |
| accuser-idle-right-v1.png | accuser |
| seer-idle-right-v1.png | seer |
| lancer-idle-right-v1.png | lancer |

殉教者のIDは表示名と異なり `axeman_g`。`UiKit` の既存ファイル探索に合わせた。
足元余白は各画像のアルファ128以上の最下行から測定。殉教者の表示高さは2.65。
戦闘ルールは変更していない。DemoAppビルドは警告0・エラー0。
