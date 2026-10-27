extends SceneTree

# 本番MainのSprite3Dで、選択されたAの連番・表示高・足元・進行を確認する。
func _initialize():
    call_deferred("capture")

func capture():
    var expected_data = []
    for index in range(24):
        var path = "res://assets/portraits/battle/idle_loop/utsu/utsu_idle_%02d.png" % index
        var image = Image.load_from_file(ProjectSettings.globalize_path(path))
        if image == null or image.get_size() != Vector2i(640, 960):
            push_error("ウツの採用Aの連番・寸法不一致")
            quit(1)
            return
        expected_data.append(image.get_data())
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    var unit_ids = ["utsu", "gald", "nel", "wata", "hane"]
    for slot in range(unit_ids.size()):
        scene.call("DropUnit", slot, unit_ids[slot])
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.0).timeout
    var utsu_sprite: Sprite3D = null
    for candidate in scene.find_children("*", "Sprite3D", true, false):
        if candidate.texture == null or candidate.texture.get_size() != Vector2(640, 960):
            continue
        var pixels = candidate.texture.get_image()
        pixels.clear_mipmaps()
        if pixels.get_data() in expected_data:
            utsu_sprite = candidate
            break
    if utsu_sprite == null:
        push_error("本番Mainにウツの採用Aが表示されていない")
        quit(1)
        return
    var world_height = utsu_sprite.texture.get_height() * utsu_sprite.pixel_size
    var ground = utsu_sprite.position.y - world_height * (0.5 - 52.0 / 1536.0) * utsu_sprite.scale.y
    if abs(world_height - 2.50) > 0.0001 or abs(ground - 0.05) > 0.0001 or utsu_sprite.flip_h:
        push_error("ウツの表示高・接地・右向き不一致 height=%s ground=%s flip=%s" % [world_height, ground, utsu_sprite.flip_h])
        quit(1)
        return
    var first_texture = utsu_sprite.texture
    for index in range(3):
        await RenderingServer.frame_post_draw
        var path = "res://../design/art/utsu/idle-loop-v1/battle-idle-%s.png" % ["a", "b", "c"][index]
        if root.get_texture().get_image().save_png(path) != OK:
            quit(1)
            return
        if index < 2:
            await create_timer(0.20).timeout
    if utsu_sprite.texture == first_texture:
        push_error("ウツの待機連番が進んでいない")
        quit(1)
        return
    print("UTSU_IDLE_GAME_CAPTURE_OK frames=24 height=2.50 ground=0.05 advance=true")
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0)
