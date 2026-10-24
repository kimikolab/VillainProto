extends SceneTree

func _initialize():
    call_deferred("capture")

func capture():
    var small_roster = "--small-roster" in OS.get_cmdline_user_args()
    var idle_loop = "--idle-loop" in OS.get_cmdline_user_args()
    var output_prefix = "idle-loop-v2/" if idle_loop else ("size-" if small_roster else "")
    var portrait_pairs = [
        ["res://assets/portraits/rica.png", "res://../design/art/rica/rica-standing-remake-v3-shovel-lean.png"],
        ["res://assets/portraits/battle/rica_idle_right.png", "res://../design/art/rica/rica-battle-idle-right-remake-v6.png"],
    ]
    for portrait_pair in portrait_pairs:
        var image = Image.load_from_file(ProjectSettings.globalize_path(portrait_pair[0]))
        if image == null or image.get_size() != Vector2i(1024, 1536):
            push_error("リィカの立ち絵の読み込み・寸法不一致")
            quit(1)
            return
        if image.get_pixel(0, 0).a > 0.08 or FileAccess.get_sha256(portrait_pair[0]) != FileAccess.get_sha256(portrait_pair[1]):
            push_error("リィカの透過・採用原本の一致を確認できない")
            quit(1)
            return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    var unit_ids = ["mio", "shio", "sasa", "tou", "rica"] if small_roster else ["hibi", "gald", "rica", "wata", "nel"]
    for slot in range(unit_ids.size()):
        scene.call("DropUnit", slot, unit_ids[slot])
    scene.call("OnSetupSlotClicked", 4 if small_roster else 2)
    await create_timer(0.4).timeout
    await RenderingServer.frame_post_draw
    if root.get_texture().get_image().save_png("res://../design/art/rica/%sselection-game-check.png" % output_prefix) != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    if idle_loop:
        var loop_sprite: Sprite3D = null
        for candidate in scene.find_children("*", "Sprite3D", true, false):
            if candidate.texture != null and candidate.texture.get_size() == Vector2(640, 960):
                loop_sprite = candidate
                break
        if loop_sprite == null or abs(loop_sprite.texture.get_height() * loop_sprite.pixel_size - 2.10) > 0.0001:
            push_error("リィカの待機連番または表示高を確認できない")
            quit(1)
            return
        var previous_frame = loop_sprite.texture
        await create_timer(0.25).timeout
        if previous_frame == loop_sprite.texture:
            push_error("戦闘停止中の待機連番が進んでいない")
            quit(1)
            return
        print("RICA_IDLE_GAME_CAPTURE_OK height=2.10 paused_idle_advance")
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/rica/%sbattle-game-check.png" % output_prefix)
    print("RICA_REMAKE_GAME_CAPTURE_OK result=", result)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
