# 第415期 指示書 —— 【小さな期・表示専用】デバフ軸の演出に要る台本の出来事を足す（カーズの印・ウツの手数・ワタの引き受け元）

この指示書は自己完結している。リポジトリ（`design/` の過去報告・`docs/`・コード）以外は参照しないこと。
前期の報告は `design/PHASE414_AXES_RECHECK.md`。演出の発注メモは `design/CODEX_BRIEF_DEBUFF.md` §4（Codex から「カーズの刻印の対象は台本に記録されていない」と返ってきた）。

**盤面は1ビットも動かさない。** 足すのは台本（`verbose` ／ 台本限定）の出来事だけ。受け入れは `compare` 0 セル・`docs/` 差 0。第182期（呪いが付いた瞬間の `StatusGain`）・HEX メモ §3 と同じ作法。

## 1. 足す出来事（3 つ）

1. **カーズの印が付いた瞬間**: `StatusGain`（`ActorId` ＝ カーズ・`TargetId` ＝ 印の敵・`Text` ＝ `"karz_mark"`・量 1）。出す場所は `KariMarkDeepen` の札が印を付ける1箇所だけ。印が消える条件（カーズが倒れる）は、カーズの `Death` から DemoApp が読めるので出来事は足さない
   - 併せて、手番の `Action`「呪いを刻む」の `TargetId` に印の敵が入っていることを確かめる（入っていなければ入れる）
2. **ウツの手数が変わった瞬間**: 下げ幅が 3 の境を越えて手数が増えた／減ったとき `StatusGain`（`ActorId` ＝ ウツ・`TargetId` ＝ ウツ・`Text` ＝ `"utsu_hits"`・量 ＝ 新しい手数 1〜5）。強化で半減したときも同じ口で出す。出す場所は `Perverse` が手数を計算する箇所（`ModifyHitCount` の結果が前回と違ったとき）。初期値（開戦時）も1件出す
3. **ワタの引き受け元**: ワタが味方の弱体を引き受けた `StatusGain`（受け手ワタ）に、**元の受け手**の Id を載せる（既存の `Damage.ShareFromId` と同じく表示専用の欄・名前は任せる・例 `FromId`）。配り（ウツ／ムドへ）と押し付け（敵へ）の `StatusGain` は書き手がワタになっているので、そのままでよい

## 2. 確かめ

- `compare` 0 セル（`docs/balance.md` と全セル一致）・`docs/` 差 0・`audit` ずれ 0
- 自己検査: 「試遊・カーズ 呪い」× ボス・seed 0 の台本で、`karz_mark` の件数 ＝ カーズが印を付けた回数（`kari410` の計数と同じ）／ `utsu_hits` の件数 ≥ 1 で最後の量 ＝ 決着時のウツの手数／ ワタの引き受けの `StatusGain` すべてに `FromId` が入る。`verbose` の有無で勝敗・T が変わらない
- `light -Units kari,utsu,wata,mudo,nel`（HEAD は第414期の `promote`・d94a49a4）を本コミットの前 → コミット → `promote`。台本の出来事が増えるので、台本の件数を数える器具の差が出たら「件数の情報行だけ」であることを確かめる
- `sweep fast` のみ
- DemoApp: ビルド 0 エラー・門と 3 つの check（`NineWaveCheck` は Codex の修正が入っていれば `ok=True`・まだなら「Codex 待ち」と1行）・「試遊・呪い」「試遊・カーズ 呪い」の単発の再生が最後まで通る（新しい出来事を DemoApp が読み飛ばしても止まらないこと）
- `design/ENGINE_HOOKS.md` に台本の出来事3つを1行ずつ。期の一行は `design/PHASE_INDEX.md`・要約は `design/HISTORY_PHASES.md` 冒頭。`CLAUDE.md` には書かない
- **コミットは自分の変更パスだけ**（DemoApp は Codex が作業中なので触らない）。push の前にローカルの master に自分以外の未 push のコミットが無いか確かめ、**あればポンの判断待ちにして push しない**。無ければ push まで行う

## 3. 報告書（`design/PHASE415_SCRIPT_EVENTS.md`・短く）

1. 足した出来事と出す場所（3 行）
2. 確かめ（`compare` 0・自己検査・`light`）
3. Codex への一言（`CODEX_BRIEF_DEBUFF.md` §4 の3つが台本に揃った旨・欄の名前）
