extends SceneTree

# 通常・戦闘立ち絵と召喚獣の体格を実画面で確認する。通常起動には関与しない。
func _initialize():
    call_deferred("capture")

func has_beast_sprite(node: Node) -> bool:
    if node is Sprite3D and node.texture != null and node.texture.get_width() == 1254:
        return true
    for child in node.get_children():
        if has_beast_sprite(child):
            return true
    return false

func save_frame(file_name: String) -> int:
    await RenderingServer.frame_post_draw
    return root.get_texture().get_image().save_png("res://../design/art/som/" + file_name)

func capture():
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "kado")
    scene.call("DropUnit", 1, "kugu")
    scene.call("DropUnit", 2, "tou")
    scene.call("DropUnit", 3, "kata")
    scene.call("DropUnit", 4, "som")
    scene.call("OnSetupSlotClicked", 4)
    await create_timer(0.5).timeout
    var result = await save_frame("selection-game-check.png")
    if result != OK:
        quit(1)
        return
    scene.call("StartBattle")
    var beast_visible = false
    for step in range(120):
        await create_timer(0.1).timeout
        if has_beast_sprite(scene):
            beast_visible = true
            break
    if beast_visible:
        scene.call("TogglePause")
        await create_timer(0.6).timeout
    result = await save_frame("battle-game-check.png")
    print("SOM_ART_CAPTURE beast_visible=", beast_visible, " result=", result)
    quit(0 if result == OK and beast_visible else 1)
