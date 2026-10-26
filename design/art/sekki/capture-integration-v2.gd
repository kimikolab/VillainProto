extends SceneTree

const ART_DIR = "res://../design/art/sekki/"

func _initialize():
    call_deferred("capture")

func capture():
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("OnSetupSlotClicked", 3)
    await create_timer(0.5).timeout
    await save_frame("selection-v2-game-check.png")
    scene.call("StartBattle")
    await process_frame
    await create_timer(0.9).timeout
    scene.call("TogglePause")
    await create_timer(0.4).timeout
    await save_frame("battle-idle-v2-game-check.png")
    var sekki_image = Image.load_from_file(ProjectSettings.globalize_path("res://assets/portraits/battle/sekki_idle_right.png"))
    for sprite in scene.find_children("*", "Sprite3D", true, false):
        if sprite.texture != null and sprite.texture.get_size() == Vector2(1024, 1536) and sprite.texture.get_image().get_data() == sekki_image.get_data():
            var camera = sprite.get_viewport().get_camera_3d()
            var foot = sprite.to_global(Vector3(0.0, (768.0 - 1373.5) * sprite.pixel_size, 0.0))
            var ring_center = sprite.get_parent().to_global(Vector3(0.0, 0.055, 0.0))
            print("SEKKI_V2_SPRITE height=", sprite.texture.get_height() * sprite.pixel_size, " position=", sprite.position, " flip_h=", sprite.flip_h)
            print("SEKKI_V2_GROUND foot=", camera.unproject_position(foot), " ring=", camera.unproject_position(ring_center), " distancePx=", camera.unproject_position(foot).distance_to(camera.unproject_position(ring_center)))
    print("SEKKI_V2_IDLE_CAPTURE_COMPLETE")
    if "--sekki-v2-check" in OS.get_cmdline_user_args():
        scene.call("RunSekkiV2Check")
        return
    scene.queue_free()
    await process_frame
    await process_frame
    quit()

func save_frame(filename):
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png(ART_DIR + filename)
    assert(result == OK, "撮影失敗: " + filename)
