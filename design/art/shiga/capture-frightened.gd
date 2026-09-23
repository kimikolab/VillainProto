extends SceneTree

func _initialize():
    call_deferred("capture")

func find_shiga(node):
    if node.has_method("SetFrightened") and node.get("UnitId") == "shiga":
        return node
    for child in node.get_children():
        var found = find_shiga(child)
        if found != null:
            return found
    return null

func capture():
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "shiga")
    scene.call("DropUnit", 1, "hisa")
    scene.call("DropUnit", 2, "kado")
    scene.call("DropUnit", 3, "nono")
    scene.call("DropUnit", 4, "nel")
    scene.call("StartBattle")
    var pawn = find_shiga(scene)
    var seen = false
    var recovered = false
    for i in range(800):
        await create_timer(0.04).timeout
        if pawn.get("IsFrightened") and not seen:
            seen = true
            await RenderingServer.frame_post_draw
            root.get_texture().get_image().save_png("res://../design/art/shiga/frightened-game-check.png")
        elif seen and not pawn.get("IsFrightened"):
            recovered = pawn.get("Hp") > 0
            await RenderingServer.frame_post_draw
            root.get_texture().get_image().save_png("res://../design/art/shiga/recovered-game-check.png")
            break
    print("SHIGA_FRIGHT_CAPTURE start=", seen, " recovered_alive=", recovered)
    quit(0 if seen and recovered else 1)
