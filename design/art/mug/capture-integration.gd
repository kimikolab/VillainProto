extends SceneTree

# 本番画面でムグと、台本から召喚される胞子の絵・浮遊・体格を確認する。
func _initialize():
    call_deferred("capture")

func capture():
    var pairs = [
        ["res://assets/portraits/mug.png", "mug-floating-front-v2.png"],
        ["res://assets/portraits/battle/mug_idle_right.png", "mug-floating-idle-right-v2.png"],
        ["res://assets/portraits/spore.png", "spore-floating-front-v1.png"],
        ["res://assets/portraits/battle/spore_idle_right.png", "spore-floating-idle-right-v1.png"],
    ]
    for pair in pairs:
        var image = Image.load_from_file(ProjectSettings.globalize_path(pair[0]))
        if image == null or image.get_size() != Vector2i(1254, 1254):
            fail("画像の読み込み・寸法: " + pair[0])
            return
        for corner in [Vector2i(0, 0), Vector2i(1253, 0), Vector2i(0, 1253), Vector2i(1253, 1253)]:
            if image.get_pixelv(corner).a > 0.001:
                fail("背景の透過: " + pair[0])
                return
        if FileAccess.get_sha256(pair[0]) != FileAccess.get_sha256("res://../design/art/mug/" + pair[1]):
            fail("制作原本との一致: " + pair[0])
            return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    var keys = ["zoto", "golm", "mug", "vel", "rica"]
    for slot in range(keys.size()):
        scene.call("DropUnit", slot, keys[slot])
    scene.call("OnSetupSlotClicked", 2)
    await create_timer(0.5).timeout
    if not await save_frame("selection-game-check.png"):
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(0.4).timeout
    if find_portraits(scene, 1.90).is_empty():
        fail("本番Sprite3Dにムグの画像・表示高がない")
        return
    if not await save_frame("battle-game-check.png"):
        return
    scene.call("SetSpeed", 3)
    scene.call("TogglePause")
    var found_spore = false
    for attempt in range(300):
        await create_timer(0.05).timeout
        if not find_portraits(scene, 1.10).is_empty():
            found_spore = true
            break
    if not found_spore:
        fail("台本の召喚で胞子の画像・表示高を確認できない")
        return
    scene.call("TogglePause")
    await create_timer(0.4).timeout
    if not await save_frame("summoned-spores-game-check.png"):
        return
    scene.call("TogglePause")
    await create_timer(0.6).timeout
    scene.call("TogglePause")
    await create_timer(0.8).timeout
    if not await save_frame("spores-settled-game-check.png"):
        return
    print("MUG_ART_CAPTURE_OK originals alpha mug=1.90 spore=1.10 actual-summon")
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0)

func find_portraits(scene, world_height):
    var found = []
    for sprite in scene.find_children("*", "Sprite3D", true, false):
        if sprite.texture != null and sprite.texture.get_size() == Vector2(1254, 1254):
            if abs(sprite.texture.get_height() * sprite.pixel_size - world_height) < 0.0001:
                found.append(sprite)
    return found

func save_frame(filename):
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/mug/" + filename)
    if result != OK:
        fail("画面保存: " + filename)
        return false
    return true

func fail(message):
    push_error("ムグの立ち絵確認失敗: " + message)
    quit(1)
