extends SceneTree

# 採用画像を実際の編成・戦闘画面で確認する。通常起動には関与しない。
func _initialize():
    call_deferred("capture")

func capture():
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "borg")
    scene.call("DropUnit", 1, "kado")
    scene.call("DropUnit", 2, "susu")
    scene.call("DropUnit", 3, "nono")
    scene.call("DropUnit", 4, "gald")
    scene.call("OnSetupSlotClicked", 2)
    await create_timer(0.5).timeout
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://../design/art/susu/selection-game-check.png")
    if result != OK:
        quit(1)
        return
    scene.call("StartBattle")
    await create_timer(0.5).timeout
    await RenderingServer.frame_post_draw
    result = root.get_texture().get_image().save_png("res://../design/art/susu/battle-game-check.png")
    print("SUSU_ART_CAPTURE result=", result)
    quit(0 if result == OK else 1)
