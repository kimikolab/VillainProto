extends SceneTree

# 実戦の拘束開始と解除を監視し、専用絵と糸を撮影する。
func _initialize():
    call_deferred("capture")

func find_field(node):
    if node.has_method("SetBinding"):
        return node
    for child in node.get_children():
        var result = find_field(child)
        if result != null:
            return result
    return null

func capture():
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    scene.call("DropUnit", 0, "kugu")
    scene.call("DropUnit", 1, "hisa")
    scene.call("DropUnit", 2, "kado")
    scene.call("DropUnit", 3, "nono")
    scene.call("DropUnit", 4, "nel")
    scene.call("StartBattle")
    var field = find_field(scene)
    var saw_binding = false
    var saw_release = false
    for i in range(600):
        await create_timer(0.05).timeout
        var count = field.get("ActiveBindingCount")
        if count == null:
            push_error("拘束数を取得できません")
            quit(1)
            return
        if count > 0 and not saw_binding:
            saw_binding = true
            await create_timer(0.18).timeout
            await RenderingServer.frame_post_draw
            root.get_texture().get_image().save_png("res://../design/art/kugu/binding-game-check.png")
        elif count == 0 and saw_binding:
            saw_release = true
            await create_timer(0.2).timeout
            await RenderingServer.frame_post_draw
            root.get_texture().get_image().save_png("res://../design/art/kugu/released-game-check.png")
            break
    field.call("ResetBindings")
    # 複数の書き手を同じ対象へ表示しても、一方の解除で他方を消さない。
    var pawns = []
    collect_pawns(scene, pawns)
    var live = pawns.filter(func(p): return p.get("Hp") > 0)
    assert(live.size() >= 3)
    field.call("SetBinding", live[0], live[2], true)
    field.call("SetBinding", live[1], live[2], true)
    assert(field.get("ActiveBindingCount") == 2)
    field.call("SetBinding", live[0], live[2], false)
    assert(field.get("ActiveBindingCount") == 1)
    assert(not live[0].get("IsBinding") and live[1].get("IsBinding"))
    field.call("ClearBindingsFor", live[2])
    assert(field.get("ActiveBindingCount") == 0)
    assert(not live[1].get("IsBinding"))
    field.call("SetBinding", live[0], live[2], true)
    field.call("ResetBindings")
    assert(not live[0].get("IsBinding"))
    print("KUGU_BINDING_CAPTURE start=", saw_binding, " release=", saw_release, " reset=", field.get("ActiveBindingCount"))
    print("KUGU_BINDING_LIFECYCLE multi_source=ok target_death_cleanup=ok reset_pose=ok")
    quit(0 if saw_binding and saw_release else 1)

func collect_pawns(node, result):
    if node.has_method("SetBinding") and node.has_method("SetHp"):
        result.append(node)
    for child in node.get_children():
        collect_pawns(child, result)
