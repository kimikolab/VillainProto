# ソムの効果音一覧

ソムの演出8種類に、指定された音源を接続済みです。7本を `DemoApp/assets/audio/se/` に原形式のままコピーし、光の消失には渇きの回復無効音を共用しています。

音源は切り詰めや再エンコードをせず、指定ファイルの長さと余韻を保持しています。表の秒数はGodotで読み取った長さです。

| ファイル名 | 鳴る場面 | 長さ | 採用音源 |
|---|---|---|---|
| `som_summon.mp3` | ソムが腕を掲げ、敵陣に魔法陣が開く | 2.325秒 | Springin／モンスター召喚から出現.mp3 |
| `som_snub.mp3` | 獣がそっぽを向き、ソムが固まる | 0.289秒 | 効果音ラボ／演出／ニュッ2.mp3 |
| `som_gather.wav` | 連鎖で弾けた粒がソムの頭上へ集まる | 3.182秒 | Helton Yan／MAGAngl_BUFF-Metallic Plus Damage_HY_PC-002.wav |
| `som_rain.wav` | 集まった光が味方全員へ降る | 3.687秒 | Helton Yan／MAGAngl_BUFF-Healing Gusts_HY_PC-001.wav |
| `som_veil.wav` | 溢れた回復が光の衣になる | 2.795秒 | Helton Yan／MAGAngl_BUFF-Simple Heal_HY_PC-003.wav |
| `som_impact.wav` | 敵の攻撃を衣で受け、光が散る | 2.666秒 | Helton Yan／DSGNSynth_BUFF-Mecha Barrier Fail_HY_PC-002.wav |
| `som_break.mp3` | 衣が尽き、膜が割れる | 1.253秒 | 効果音ラボ／戦闘／ガラスが割れる2.mp3 |
| `rule_drought.mp3` | 粛や痺れで、戻る光が途中で消える | 2.652秒 | 既存の渇きによる回復無効音を直接参照 |

## 元ファイルの場所

共通ルートは `D:\Assets\SE\`。Springinは `Springin\`、効果音ラボは表の分類フォルダ、Helton Yanの4本は `Helton Yan's Pixel Combat - Single Files\` 内の同名ファイルです。コピー前後のSHA-256が7本とも一致することを確認しました。

## 再生設定と確認

- MP3とWAVを両方読み込み、ループせず再生する。
- 再生側は雨と被弾を −15 dB、ほかを −11 dBにしている。
- 倍速でも音程は変えない。同じ音の連打は100 ms以内を間引き、重なりを2音までにする。
- 戦闘終了・編成への帰還で再生を止める。
- 全8音源のGodotでの読み込みを確認。DemoAppビルドは警告0・エラー0。聴感での最終音量調整は未実施。

既存の感電・放電と粛のひびの音は再利用するため、追加制作は不要です。「いでよ、我が忠実なる下僕よ！！」「……え？」は文字で出し、音声の収録は必須にしていません。
