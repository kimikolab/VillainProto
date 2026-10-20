extends SceneTree

func _initialize():
    call_deferred("capture")

func capture():
    var scattergun = "--scattergun" in OS.get_cmdline_user_args()
    var source_prefix = "gare-pirate-scattergun" if scattergun else "gare-pirate-captain"
    var output_prefix = "pirate-scattergun" if scattergun else "pirate"
    var portrait_pairs = [
        ["res://assets/portraits/gare.png", "res://../design/art/gare/%s-front-v1.png" % source_prefix],
        ["res://assets/portraits/battle/gare_idle_right.png", "res://../design/art/gare/%s-idle-right-v1.png" % source_prefix],
    ]
    for portrait_pair in portrait_pairs:
        var portrait_path = portrait_pair[0]
        var portrait_image = Image.load_from_file(ProjectSettings.globalize_path(portrait_path))
        if portrait_image == null or portrait_image.get_size() != Vector2i(1024, 1536):
            push_error("立ち絵の読み込み・寸法不一致: " + portrait_path)
            quit(1)
            return
        if portrait_image.get_pixel(0, 0).a > 0.08:
            push_error("立ち絵の背景が透過されていない: " + portrait_path)
            quit(1)
            return
        if FileAccess.get_sha256(portrait_path) != FileAccess.get_sha256(portrait_pair[1]):
            push_error("海賊版の原本とゲーム素材が不一致: " + portrait_path)
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
    scene.call("OnSetupSlotClicked", 2)
    await create_timer(0.4).timeout
    await RenderingServer.frame_post_draw
    if root.get_texture().get_image().save_png("res://../design/art/gare/%s-selection-game-check.png" % output_prefix) != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(1.5).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/gare/%s-battle-game-check.png" % output_prefix)
    print("GARE_PIRATE_SCATTERGUN_ART_CAPTURE result=" if scattergun else "GARE_PIRATE_ART_CAPTURE result=", result)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0 if result == OK else 1)
