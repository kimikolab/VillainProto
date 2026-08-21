# 作業手順

## バランス調整のたびにやること

1. 数値や特性を変える
2. `dotnet run --project BattleSim -c Release 0 compare > docs/balance.md` で勝率を測り直す
3. `dotnet run --project BattleSim -c Release 0 dump > docs/units.md` で一覧を吐き直す
4. `dotnet run --project BattleSim -c Release 0 layout > docs/layout.md` で配置を探索し直す
5. `git diff docs/` で何が動いたかを確認する
6. docs/ の差分も含めてコミットし、動いた行をコミットメッセージにも書く

`docs/` の3ファイルは生成物。手で編集しても次の生成で消える。

3 を飛ばすと説明文と挙動がずれる。これまでに3回発生している。
2 を飛ばすと勝率表が嘘になる。差分が出ないこと自体が
「触ったがバランスは動いていない」という情報になるので、必ず測り直す。

4 は重い（1コアで40分前後）が、**飛ばすと変更を過大にも過小にも読み違える。**
配置が旧ルール前提のまま残ると、その編成は変更に対応できていないだけなのに
「変更が過剰だった」ように見える（実例: 後衛特化+後備えが旧配置3.5% / 再探索後26.0%）。
仕様変更は必ず配置の再探索とセットにする。

## 受け入れ条件のひな型

変更を測るときは、**動くはずのない編成が動いていないこと**を必ず確認する。
実装ミスの検出器として機能する。`compare` の差分が変更対象を含む編成だけに出ていれば合格。

    例: シオのバフ順序を直した → 変化したのは 移動改(バサ×ヨミ×シオ) のみ、他26編成は完全一致

## コミットメッセージ

全表は `docs/balance.md` の差分としてコミットに入る。
メッセージには**動いた行だけ**を書く（24行の表を毎回貼ると `git log` が読めなくなる）。

    カドの反撃量を被弾量参照から攻撃力参照に変更

    第3波: 反撃 (ヒサ×カド)   0.0% → 34.0%
    第4波: 惨禍×被弾強化      0.5% → 99.5%
    （全表は docs/balance.md）

## 触ってよい場所

- 数値だけの調整 → `UnitCatalog.cs`、各 Trait の `const`
- 新しい特性 → `Traits.cs` に Trait を継承したクラスを足し、`TraitId` と `TraitCatalog` に登録
- 盤面全体に効く効果（味方全体の被ダメージ増減など）→ `BattleEngine.ApplyDamage`

## 触ってはいけない場所

- `BattleCore` に UI の参照を足さない。`INotifyPropertyChanged` も `ObservableCollection` も不可
- `PrototypeApp` に戦闘ルールを書かない。ここに書いた瞬間 Godot / Unity へ運べなくなる
- `Def.Pattern` を直接読まない。必ず `UnitState.CurrentPattern` を経由する
- `docs/balance.md` と `docs/units.md` を手で書き換えない。BattleSim の出力をそのまま置く場所
