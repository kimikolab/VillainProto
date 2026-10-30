extends SceneTree

# カーズの採用画像と編成・戦闘での表示を確認する。通常起動には関与しない。
func _initialize():
    call_deferred("capture")

func capture():
    var portrait_pairs = [
        ["res://assets/portraits/kari.png", "res://../design/art/karz/karz-front-v1.png"],
        ["res://assets/portraits/battle/kari_idle_right.png", "res://../design/art/karz/karz-idle-right-v1.png"],
    ]
    for pair in portrait_pairs:
        var image = Image.load_from_file(ProjectSettings.globalize_path(pair[0]))
        if image == null or image.get_size() != Vector2i(1024, 1536):
            push_error("カーズの立ち絵の読み込み・寸法不一致")
            quit(1)
            return
        if image.get_pixel(0, 0).a > 0.08 or FileAccess.get_sha256(pair[0]) != FileAccess.get_sha256(pair[1]):
            push_error("カーズの透過・採用原本の一致を確認できない")
            quit(1)
            return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    var unit_ids = ["kari", "mudo", "nel", "wata", "utsu"]
    for slot in range(unit_ids.size()):
        scene.call("DropUnit", slot, unit_ids[slot])
    scene.call("OnSetupSlotClicked", 0)
    await create_timer(0.5).timeout
    await RenderingServer.frame_post_draw
    if root.get_texture().get_image().save_png("res://../design/art/karz/selection-game-check.png") != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/karz/battle-game-check.png")
    print("KARZ_GAME_CAPTURE_OK result=", result)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
