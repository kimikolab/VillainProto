extends SceneTree

# 採用したエレの正面・右向き立ち絵を実画面で確認する。通常起動には関与しない。
func _initialize():
    call_deferred("capture")

func capture():
    var portrait_pairs = [
        ["res://assets/portraits/ere.png", "res://../design/art/ere/ere-front-v1.png"],
        ["res://assets/portraits/battle/ere_idle_right.png", "res://../design/art/ere/ere-master-bard-right-v3a-still.png"],
        ["res://assets/portraits/battle/ere_singing_idle_right.png", "res://../design/art/ere/ere-master-bard-right-v3c-song.png"],
    ]
    for pair in portrait_pairs:
        var image = Image.load_from_file(ProjectSettings.globalize_path(pair[0]))
        if image == null or image.get_size() != Vector2i(1024, 1536):
            push_error("エレの立ち絵を読み込めないか、寸法が一致しない: " + pair[0])
            quit(1)
            return
        for corner in [Vector2i(0, 0), Vector2i(1023, 0), Vector2i(0, 1535), Vector2i(1023, 1535)]:
            if image.get_pixelv(corner).a > 0.001:
                push_error("エレの立ち絵の背景が透過されていない: " + pair[0])
                quit(1)
                return
        if FileAccess.get_sha256(pair[0]) != FileAccess.get_sha256(pair[1]):
            push_error("エレの採用原本とゲーム素材が一致しない: " + pair[0])
            quit(1)
            return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "yomi")
    scene.call("DropUnit", 1, "borg")
    scene.call("DropUnit", 2, "lili")
    scene.call("DropUnit", 3, "ere")
    scene.call("DropUnit", 4, "nel")
    scene.call("OnSetupSlotClicked", 3)
    await create_timer(0.4).timeout
    await RenderingServer.frame_post_draw
    if root.get_texture().get_image().save_png("res://../design/art/ere/selection-game-check.png") != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/ere/battle-game-check.png")
    print("ERE_ART_CAPTURE result=", result)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
