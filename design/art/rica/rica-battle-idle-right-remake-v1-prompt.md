# 墓守リィカ 戦闘待機立ち絵リメイク v1

生成日: 2026-10-24
生成方法: Codex 組み込み ImageGen（CLI 不使用）
出力: `rica-battle-idle-right-remake-v1.png`（1024×1536、透過PNG）
状態: 確認用の候補。ゲーム未適用。
キャラクター・衣装・絵柄の正: `rica-standing-remake-v3-shovel-lean.png`
構え・向きの参考: `DemoApp/assets/portraits/battle/rica_idle_right.png`（旧版）

通常立ち絵v3の白銀の片側三つ編み、静かな表情、墓守の作業着・補修した前掛け・鍵・作業手袋・ブーツとシャベルを引き継いだ。右を見据え、シャベルを両手で斜め下に構える戦闘待機姿勢。既存の通常・戦闘画像やゲームの参照先は変更していない。

## 最終プロンプト

```text
Use case: identity-preserve new battle pose.
Asset type: one full-body RIGHT-FACING BATTLE IDLE transparent character sprite for Japanese fantasy RPG, 墓守リィカ (Rica), remake battle v1.
INPUT 1 (rica-standing-remake-v3-shovel-lean.png) is the authoritative SAME character, exact updated costume, shovel design, head/body proportions, quiet mood and clean anime rendering. INPUT 2 (old rica_idle_right.png) is only a rough reference for right-facing battle orientation and two-handed low shovel guard. Do not copy its outdated face, short body proportions, simple old costume or heavy outlines.
MAIN REQUEST: turn the approved new Rica into a combat-ready WAITING stance facing SCREEN RIGHT, using the updated gravekeeper costume and spade. Preserve her subdued slightly tired, melancholy and quietly capable character. Poised and reserved, not snarling, yelling, smiling or wide-eyed frightened.
POSE: body, BOTH feet, face and eyes point SCREEN RIGHT, about 55-65 degrees away from frontal toward right profile. Head follows torso, gaze fixed on unseen enemy offscreen right, not viewer. A stable, modest staggered stance, front foot toward screen right, rear foot to screen left, both heels grounded, knees gently flexed, upper body leaning slightly forward. Not a huge martial-arts lunge. Hold the gravekeeper's shovel in a believable TWO-HANDED diagonal LOW GUARD across her body, blade toward screen right at knee/lower-shin height, D grip to screen left behind upper arm/shoulder, shaft sloping down toward right. Back hand grips the wooden shaft near rear D handle, front hand grips further down shaft near the forward hip; two hands comfortably separated with correct wrapping fingers and bent elbows. The tool has exactly one continuous straight shaft between closed D-shaped rear grip and reinforced spade socket. Do not lean chin on hands in this battle pose. No shovel overhead and no attack in progress. Cloak naturally trails slightly toward screen left; controlled folds, no dramatic wind.
IDENTITY LOCK: young adult slender female elf, pale skin, ice-blue eyes, white/silver side-swept bangs and face-framing hair, ONE thick silver braid from HER LEFT side dropping forward naturally; long pointed elf ears. Same approximately 7-7.5 head body proportions, slender shoulders and adult limbs. Recognizable face and braid remain visible in three-quarter right-facing view.
COSTUME LOCK FROM INPUT 1: hood DOWN charcoal/navy long cloak, small aged iron throat fastening, muted dark teal high-neck long-sleeved lower-calf work dress; tan/taupe-brown long work apron, same flat reinforced utility pocket, sparse repair cross stitches and stylized earth stains near hem; wide brown belt, rectangular iron buckle, hanging brown belt tail, brown leather flap pouch at HER LEFT hip, cemetery keyring and short secured cord loop; dark brown fingerless work gloves, opaque black stockings, sturdy brown folded-cuff work boots with reinforced toes and thick soles. Same coverage, color palette and modest long hem. Do not add armor, weapon belts, decorative skulls, jewelry or new accessories. Realistic drape adapted to battle stance, no skirt lifting or exposed thighs.
SHOVEL LOCK: proper gravedigger's spade with wooden straight shaft, closed dark iron D grip with wooden crossbar, reinforced metal socket, dark iron pointed broad blade, central strengthening rib, pair of boot-pressure shoulders at blade top, a few stylized earth marks and restrained edge wear. Still a recognizable digging tool used defensively, not an axe, spear, scythe, sword or fantastical polearm. Show whole D handle and whole blade with safe margins, no cutting off ends.
ART: match input 1's polished clean anime game illustration. Fine clear lines, deliberate two/three-tone cel shadows and restrained soft shading, lucid anime eyes, elegant silver hair highlights, clean broad fabric folds. Minimal material noise. Do not copy input 2's flat crude early shading or enlarged head. No photorealism, 3D, gritty texture, magic effects, ghosts or summoned companions.
COMPOSITION: vertical 1024x1536 portrait, full figure and full shovel safely inside generous 5% clear margins, single character, both boots visible near same ground plane. Comfortable compact silhouette for an auto-battler grid, no enormous billowing cloak. Eye-level gameplay camera, no extreme perspective.
BACKGROUND: genuinely transparent RGBA everywhere outside character and shovel, including limb/cloth gaps. Fully opaque solid character. No backdrop, colored gradient, vignette, halo, floor or cast shadow. No checkerboard, text, logo, signature, frame or watermark.
```
