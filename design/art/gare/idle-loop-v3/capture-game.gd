extends SceneTree

func _initialize():
    call_deferred("capture")

func capture():
    var output_directory = ProjectSettings.globalize_path("res://../design/art/gare/idle-loop-v3/game")
    DirAccess.make_dir_recursive_absolute(output_directory)
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "gare")
    scene.call("DropUnit", 1, "hibi")
    scene.call("DropUnit", 2, "gald")
    scene.call("DropUnit", 3, "wata")
    scene.call("DropUnit", 4, "nel")
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    for frame_index in range(4):
        await RenderingServer.frame_post_draw
        var result = root.get_texture().get_image().save_png("%s/battle-%02d.png" % [output_directory, frame_index])
        if result != OK:
            push_error("ゲーム画面の保存に失敗: %s" % result)
            quit(1)
            return
        await create_timer(0.31).timeout
    print("GARE_IDLE_V3_GAME_CAPTURE_OK frames=4")
    scene.queue_free()
    await process_frame
    await process_frame
    quit()
