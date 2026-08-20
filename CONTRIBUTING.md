# 作業手順

## バランス調整のたびにやること

1. 数値や特性を変える
2. `dotnet run --project BattleSim -c Release 0 compare` で全系統の勝率を見る
3. `dotnet run --project BattleSim -c Release 0 dump` で一覧を吐き直す
4. 差分をコミットする（勝率表もコミットメッセージに残すと後で追える）

3 を飛ばすと説明文と挙動がずれる。これまでに3回発生している。

## 触ってよい場所

- 数値だけの調整 → `UnitCatalog.cs`、各 Trait の `const`
- 新しい特性 → `Traits.cs` に Trait を継承したクラスを足し、`TraitId` と `TraitCatalog` に登録
- 盤面全体に効く効果（味方全体の被ダメージ増減など）→ `BattleEngine.ApplyDamage`

## 触ってはいけない場所

- `BattleCore` に UI の参照を足さない。`INotifyPropertyChanged` も `ObservableCollection` も不可
- `PrototypeApp` に戦闘ルールを書かない。ここに書いた瞬間 Godot / Unity へ運べなくなる
- `Def.Pattern` を直接読まない。必ず `UnitState.CurrentPattern` を経由する
