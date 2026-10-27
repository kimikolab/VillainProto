extends SceneTree

# ウツの採用原本と実際の編成・戦闘表示を確認する。通常起動には関与しない。
func _initialize():
    call_deferred("capture")

func capture():
    var portrait_pairs = [
        ["res://assets/portraits/utsu.png", "res://../design/art/utsu/utsu-standing-dual-swords-remake-v2.png"],
        ["res://assets/portraits/battle/utsu_idle_right.png", "res://../design/art/utsu/utsu-battle-idle-right-dual-swords-remake-v1.png"],
    ]
    for portrait_pair in portrait_pairs:
        var image = Image.load_from_file(ProjectSettings.globalize_path(portrait_pair[0]))
        if image == null or image.get_size() != Vector2i(1024, 1536):
            push_error("ウツの立ち絵の読み込み・寸法不一致")
            quit(1)
            return
        if image.get_pixel(0, 0).a > 0.08 or FileAccess.get_sha256(portrait_pair[0]) != FileAccess.get_sha256(portrait_pair[1]):
            push_error("ウツの透過・採用原本の一致を確認できない")
            quit(1)
            return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    var unit_ids = ["utsu", "gald", "nel", "wata", "hane"]
    for slot in range(unit_ids.size()):
        scene.call("DropUnit", slot, unit_ids[slot])
    scene.call("OnSetupSlotClicked", 0)
    await create_timer(0.5).timeout
    await RenderingServer.frame_post_draw
    if root.get_texture().get_image().save_png("res://../design/art/utsu/selection-game-check.png") != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/utsu/battle-game-check.png")
    print("UTSU_REMAKE_GAME_CAPTURE_OK result=", result)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
