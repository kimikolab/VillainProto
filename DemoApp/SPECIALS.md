# スペシャル演出の確認

`design/CODEX_BRIEF_SPECIALS.md` の表示専用イベントを読む。判定・HP・戦闘の乱数は追加しない。

- 紅蓮: `StatusRemaining` を花とアイコンの明るさへ反映。12以上で脈動を強める。見出しの後の着火と紅蓮由来の毒を一括表示し、通常の火・毒配布を含めない。奔流の待ち時間は最大0.64秒（2倍速で0.32秒）。
- 痺れ毒: `PoisonRoute.Spew` は口元から弧、`Venom` は袋から弾け返る。`Numbed` は毒とは別の痺れ模様。`Attack.NumbPercent` から濁り・震え・被弾時の後ずさりの弱さを引く。
- 剣の段: `LastStand.StatusRemaining` から白金の発光。盾を落とす間と抜剣を合わせて0.58秒以内。以後の攻撃・受け流しは刃の絵。斬り返しに通常の反撃カットインを重ねない。相打ち勝ちは敵の死亡を待ち、ガルドの死亡で専用の膝つき絵へ移る。死亡した駒の勝利アニメーションは既存の生存判定で抑止する。

## 実行

```powershell
dotnet build DemoApp/DemoApp.csproj
Godot_console.exe --headless --path DemoApp res://SpecialsCheck.tscn -- --verify
Godot_console.exe --path DemoApp res://SpecialsCheck.tscn
```

通常起動では1倍・2倍、左右両陣営を順に観察して終了する。`-- --capture-dir=<存在する絶対パス>` を付けると各場面をPNGで保存する。
`--verify` は実台本と各演出の件数、最終HP、再再生、代表編成の相打ち勝ちを検査する。既存の `BeniMioCheck.tscn -- --verify` で結界・啜り・濃縮との回帰も確認できる。

素材と生成プロンプト: `assets/portraits/battle/SPECIALS_ASSETS.md`。
