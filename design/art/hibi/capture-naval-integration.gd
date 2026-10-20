extends SceneTree

func _initialize():
    call_deferred("capture")

func capture():
    var directory = ProjectSettings.globalize_path("res://../design/art/hibi/naval-integration-game")
    DirAccess.make_dir_recursive_absolute(directory)
    for pair in [
        ["res://assets/portraits/hibi.png", "res://../design/art/hibi/hibi-naval-bodyguard-front-v1.png"],
        ["res://assets/portraits/battle/hibi_idle_right.png", "res://../design/art/hibi/hibi-naval-bodyguard-idle-right-v4.png"],
    ]:
        if FileAccess.get_sha256(pair[0]) != FileAccess.get_sha256(pair[1]):
            push_error("ヒビの採用元とゲーム画像が不一致")
            quit(1)
            return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "gare")
    scene.call("DropUnit", 1, "hibi")
    scene.call("DropUnit", 2, "gald")
    scene.call("DropUnit", 3, "wata")
    scene.call("DropUnit", 4, "nel")
    scene.call("OnSetupSlotClicked", 1)
    await create_timer(0.4).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png(directory + "/selection.png")
    if result != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    await RenderingServer.frame_post_draw
    result = root.get_texture().get_image().save_png(directory + "/battle.png")
    print("HIBI_NAVAL_GAME_CAPTURE_OK result=", result)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
