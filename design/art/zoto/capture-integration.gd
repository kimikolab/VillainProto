extends SceneTree

# ゾトの通常・戦闘素材を実画面で確認する。通常起動には関与しない。
func _initialize():
    call_deferred("capture")

func capture():
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "golm")
    scene.call("DropUnit", 1, "zoto")
    scene.call("DropUnit", 2, "mug")
    scene.call("DropUnit", 3, "vel")
    scene.call("DropUnit", 4, "rica")
    scene.call("OnSetupSlotClicked", 1)
    await create_timer(0.5).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/zoto/selection-game-check.png")
    if result != OK:
        quit(1)
        return
    scene.call("StartBattle")
    scene.call("TogglePause")
    await create_timer(1.8).timeout
    await RenderingServer.frame_post_draw
    result = root.get_texture().get_image().save_png("res://../design/art/zoto/battle-game-check.png")
    print("ZOTO_ART_CAPTURE result=", result)
    quit(0 if result == OK else 1)
