extends SceneTree

# 鐘番の正面絵と右向き絵を本番の編成・戦場で撮影する。
func _initialize():
    call_deferred("capture")

func capture():
    for pair in [
        ["res://assets/portraits/kubi.png", "res://../design/art/kubi/kubi-front-v1.png"],
        ["res://assets/portraits/battle/kubi_idle_right.png", "res://../design/art/kubi/kubi-idle-right-v1.png"],
    ]:
        var image = Image.load_from_file(ProjectSettings.globalize_path(pair[0]))
        if image == null or image.get_size() != Vector2i(1024, 1536):
            push_error("クビの立ち絵の寸法が一致しない: " + pair[0])
            quit(1)
            return
        for corner in [Vector2i(0,0), Vector2i(1023,0), Vector2i(0,1535), Vector2i(1023,1535)]:
            if image.get_pixelv(corner).a > 0.001:
                push_error("クビの背景の四隅が透過されていない: " + pair[0])
                quit(1)
                return
        if FileAccess.get_sha256(pair[0]) != FileAccess.get_sha256(pair[1]):
            push_error("クビの原本とゲーム素材が一致しない: " + pair[0])
            quit(1)
            return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "wata")
    scene.call("DropUnit", 1, "mudo")
    scene.call("DropUnit", 2, "nel")
    scene.call("DropUnit", 3, "kubi")
    scene.call("DropUnit", 4, "utsu")
    scene.call("OnSetupSlotClicked", 3)
    await create_timer(0.4).timeout
    await RenderingServer.frame_post_draw
    if root.get_texture().get_image().save_png("res://../design/art/kubi/selection-game-check.png") != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.2).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/kubi/battle-game-check.png")
    print("KUBI_ART_CAPTURE result=", result, " size=True alpha=True source_hash=True")
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
