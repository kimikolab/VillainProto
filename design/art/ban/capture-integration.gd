extends SceneTree

# バンの通常・戦闘用立ち絵を実際のゲーム画面で撮影する。
func _initialize():
    call_deferred("capture")

func capture():
    var portrait_pairs = [
        ["res://assets/portraits/ban.png", "res://../design/art/ban/ban-front-v2-carved-eyes.png"],
        ["res://assets/portraits/battle/ban_idle_right.png", "res://../design/art/ban/ban-idle-right-v2-carved-eyes.png"],
    ]
    for pair in portrait_pairs:
        var image = Image.load_from_file(ProjectSettings.globalize_path(pair[0]))
        if image == null or image.get_size() != Vector2i(1024, 1536):
            push_error("バンの立ち絵の読み込み・寸法不一致")
            quit(1)
            return
        if image.get_pixel(0, 0).a > 0.01 or FileAccess.get_sha256(pair[0]) != FileAccess.get_sha256(pair[1]):
            push_error("バンの透過・採用原本の一致を確認できない")
            quit(1)
            return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    var unit_ids = ["ban", "gald", "kado", "hisa", "lili"]
    for slot in range(unit_ids.size()):
        scene.call("DropUnit", slot, unit_ids[slot])
    scene.call("OnSetupSlotClicked", 0)
    await create_timer(0.5).timeout
    await RenderingServer.frame_post_draw
    if root.get_texture().get_image().save_png("res://../design/art/ban/selection-game-check-v2.png") != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/ban/battle-game-check-v2.png")
    print("BAN_ART_GAME_CAPTURE_OK result=", result)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
