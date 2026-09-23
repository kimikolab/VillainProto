extends SceneTree

# ヒサの正面・戦闘素材を実画面で確認する。通常起動には関与しない。
func _initialize():
    call_deferred("capture")

func capture():
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "hisa")
    scene.call("DropUnit", 1, "gald")
    scene.call("DropUnit", 2, "kado")
    scene.call("DropUnit", 3, "nono")
    scene.call("DropUnit", 4, "nel")
    scene.call("OnSetupSlotClicked", 0)
    await create_timer(0.5).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/hisa/selection-game-check.png")
    if result != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await create_timer(0.5).timeout
    await RenderingServer.frame_post_draw
    result = root.get_texture().get_image().save_png("res://../design/art/hisa/battle-game-check.png")
    print("HISA_ART_CAPTURE result=", result)
    quit(0 if result == OK else 1)
