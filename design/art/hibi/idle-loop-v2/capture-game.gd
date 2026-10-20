extends SceneTree

func _initialize():
    call_deferred("capture")

func capture():
    var directory = ProjectSettings.globalize_path("res://../design/art/hibi/idle-loop-v2/game")
    DirAccess.make_dir_recursive_absolute(directory)
    var hashes = {}
    for frame_index in range(30):
        var image = Image.load_from_file("res://assets/portraits/battle/idle_loop/hibi/hibi_idle_%02d.png" % frame_index)
        hashes[image_fingerprint(image)] = frame_index
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "gare")
    scene.call("DropUnit", 1, "hibi")
    scene.call("DropUnit", 2, "gald")
    scene.call("DropUnit", 3, "nel")
    scene.call("DropUnit", 4, "utsu")
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    var frames_seen = {}
    for frame_index in range(4):
        await RenderingServer.frame_post_draw
        var sprites = find_hibi_sprites(root, hashes)
        if sprites.is_empty():
            push_error("実戦画面にヒビの採用連番がありません")
            quit(1)
            return
        frames_seen[sprites[0][1]] = true
        var result = root.get_texture().get_image().save_png("%s/battle-%02d.png" % [directory, frame_index])
        if result != OK:
            push_error("実戦画面の保存に失敗")
            quit(1)
            return
        await create_timer(0.31).timeout
    if frames_seen.size() < 3:
        push_error("実戦画面でヒビの連番が進んでいません")
        quit(1)
        return
    print("HIBI_IDLE_V2_GAME_CAPTURE_OK frames=4 distinct_hibi_frames=", frames_seen.size())
    scene.queue_free()
    await process_frame
    await process_frame
    quit()

func find_hibi_sprites(node: Node, hashes: Dictionary) -> Array:
    var result = []
    if node is Sprite3D and node.texture != null and node.texture.get_width() == 640:
        var image = node.texture.get_image()
        image.clear_mipmaps()
        var fingerprint = image_fingerprint(image)
        if hashes.has(fingerprint):
            result.append([node, hashes[fingerprint]])
    for child in node.get_children():
        result.append_array(find_hibi_sprites(child, hashes))
    return result

func image_fingerprint(image: Image) -> String:
    var context = HashingContext.new()
    context.start(HashingContext.HASH_SHA256)
    context.update(image.get_data())
    return context.finish().hex_encode()
