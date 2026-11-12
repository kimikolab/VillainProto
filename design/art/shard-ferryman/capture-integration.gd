extends SceneTree

func _initialize():
    call_deferred("capture")

func capture():
    var unit_ids = ["gare", "hibi", "wata"]
    for unit_id in unit_ids:
        var portrait_paths = [
            "res://assets/portraits/%s.png" % unit_id,
            "res://assets/portraits/battle/%s_idle_right.png" % unit_id,
        ]
        for portrait_path in portrait_paths:
            var portrait_image = Image.load_from_file(portrait_path)
            if portrait_image == null or portrait_image.get_size() != Vector2i(1024, 1536):
                push_error("立ち絵の読み込み・寸法不一致: " + portrait_path)
                quit(1)
                return
            if portrait_image.get_pixel(0, 0).a > 0.08:
                push_error("立ち絵の背景が透過されていない: " + portrait_path)
                quit(1)
                return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "hibi")
    scene.call("DropUnit", 1, "gald")
    scene.call("DropUnit", 2, "gare")
    scene.call("DropUnit", 3, "wata")
    scene.call("DropUnit", 4, "nel")
    var selection_slots = {"gare": 2, "hibi": 0, "wata": 3}
    for unit_id in unit_ids:
        scene.call("OnSetupSlotClicked", selection_slots[unit_id])
        await create_timer(0.25).timeout
        await RenderingServer.frame_post_draw
        var selection_path = "res://../design/art/shard-ferryman/%s-selection-game-check.png" % unit_id
        if root.get_texture().get_image().save_png(selection_path) != OK:
            quit(1)
            return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/shard-ferryman/battle-game-check.png")
    print("SHARD_FERRYMAN_ART_CAPTURE result=", result)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
