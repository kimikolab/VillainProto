extends SceneTree

# 本編Mainの編成画面と盤面で、採用した待機の高さ・停止・再開を確認する。
func _initialize():
    call_deferred("capture")

func capture():
    var output = "res://../design/art/hagi/idle-loop-v2/integration"
    DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output))
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    var units = ["mio", "shio", "sasa", "rica", "hagi"]
    for slot in range(units.size()):
        scene.call("DropUnit", slot, units[slot])
    scene.call("OnSetupSlotClicked", 4)
    await create_timer(0.4).timeout
    await RenderingServer.frame_post_draw
    if root.get_texture().get_image().save_png(output + "/selection-game-check.png") != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(0.9).timeout
    var sprite: Sprite3D = null
    for candidate in scene.find_children("*", "Sprite3D", true, false):
        if candidate.texture != null and candidate.texture.get_size() == Vector2(640, 960):
            if abs(candidate.texture.get_height() * candidate.pixel_size - 2.25) < 0.0001:
                sprite = candidate
                break
    if sprite == null:
        push_error("ハギの採用連番または表示高2.25を確認できない")
        quit(1)
        return
    var before = sprite.texture
    await create_timer(0.3).timeout
    if before != sprite.texture:
        push_error("一時停止中にハギの待機連番が進んでいる")
        quit(1)
        return
    await RenderingServer.frame_post_draw
    if root.get_texture().get_image().save_png(output + "/battle-paused-game-check.png") != OK:
        quit(1)
        return
    scene.call("TogglePause")
    await create_timer(0.35).timeout
    if before == sprite.texture:
        push_error("一時停止解除後にハギの絵が進んでいない")
        quit(1)
        return
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png(output + "/battle-game-check.png")
    print("HAGI_IDLE_GAME_CAPTURE_OK height=2.25 pause resume result=", result)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
