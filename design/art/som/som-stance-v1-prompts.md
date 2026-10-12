# ソムの臨戦態勢差分（2026-10-11）

組み込み image_gen で既存の小型猫・大型下僕を参照し、右向きの威嚇姿勢を作成。走り・出現の素材は保持し、敵陣への到着後に左右反転してソム側へ向ける。2枚とも生成されたアルファを保ったPNG。

- `DemoApp/assets/portraits/battle/fodder_stance_idle_right.png` — 1254×1254。小型猫、逆立つ毛・牙・低い構え。
- `DemoApp/assets/portraits/battle/fodder_majestic_stance_idle_right.png` — 1344×1170。大型下僕、逆立つ鬣・牙・踏ん張る四肢。
- 雷と宝石の点滅は画像に焼き込まず、Godot側で描く。首輪は壊さない。

## 小型猫のプロンプト

Use case: identity-preserve. Asset type: transparent full-body 2D game sprite, threatening combat stance. Reference Image 1 is the exact small chubby purple horned cat. Preserve its proportions and identity, dark violet fur, pale lilac muzzle and paws, golden horns, yellow single slit pupils, ornate gold amethyst collar, curled tail with gold tip, same crisp painted anime fantasy style. Change pose and expression only: entire body AND HEAD face RIGHT in side three-quarter view, four paws firmly planted widely on the ground, crouched shoulders, raised bristling back fur and fluffed tail, ears flattened, mouth partly open snarling with clearly visible two short white fangs, angry narrow yellow eyes. It has turned to threaten its former summoner. It is an unfriendly little fluffy cat, still small and round, not a lion and not a monster. Complete silhouette and all paws and tail fully inside frame, small transparent margins. Single character only, no text, no ground, no shadow, no magical effects or electricity baked into the image, collar intact. Actual transparent background.

## 大型下僕のプロンプト

Use case: identity-preserve. Asset type: transparent full-body 2D game sprite, threatening combat stance. Reference Image 1 is the exact majestic lionlike purple horned servant whose identity and size must be preserved. Change pose and expression only: entire body AND HEAD face RIGHT in side three-quarter view, four paws widely planted, tense crouched shoulders and lowered head, huge bristling violet mane, fluffed curled tail, snarling partly open mouth exposing two white upper canine fangs, angry narrow yellow eyes. The creature is threatening its former summoner after turning around. Preserve its adult muscular lionlike physique, purple and lilac fur, ornate gold curved horns, golden collar with large amethyst jewel and golden tail tip. Same clean detailed painted anime fantasy game illustration. Collar intact, no explosion or punishment. All four paws, tail and mane fully inside the canvas, small transparent margins. Single complete character only, no ground, no shadow, no text, no background or baked in electrical effects. Actual transparent background.
