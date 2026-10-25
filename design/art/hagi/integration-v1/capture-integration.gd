extends SceneTree

const OUT = "res://../design/art/hagi/integration-v1/"
var scene: Node
var failed := false

func _initialize():
    call_deferred("capture")

func require_ok(ok: bool, message: String) -> bool:
    if not ok:
        failed = true
        push_error(message)
        quit(1)
        return false
    return true

func snapshot(name: String):
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png(OUT + name)
    require_ok(result == OK, "ハギの確認画面を保存できない: " + name)

func texture_matches(texture: Texture2D, image: Image) -> bool:
    if texture == null or texture.get_size() != Vector2(image.get_size()):
        return false
    return texture.get_image().get_data() == image.get_data()

func capture():
    var pairs = [
        ["res://assets/portraits/hagi.png", "res://../design/art/hagi/fresh-redraw-v1/hagi-standing-fresh-v1.png"],
        ["res://assets/portraits/battle/hagi_idle_right.png", "res://../design/art/hagi/fresh-redraw-v1/hagi-battle-idle-right-fresh-v2.png"],
    ]
    for pair in pairs:
        var image = Image.load_from_file(ProjectSettings.globalize_path(pair[0]))
        if not require_ok(image != null and image.get_size() == Vector2i(1024, 1536), "ハギの画像寸法"):
            return
        if not require_ok(image.get_pixel(0, 0).a < 0.08, "ハギの透過"):
            return
        if not require_ok(FileAccess.get_sha256(pair[0]) == FileAccess.get_sha256(pair[1]), "ハギの採用原本が違う"):
            return
    var normal = Image.load_from_file(ProjectSettings.globalize_path(pairs[0][0]))
    var battle = Image.load_from_file(ProjectSettings.globalize_path(pairs[1][0]))
    scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("ClearFormation")
    var units = ["hagi", "rica", "sasa", "mio", "tou"]
    for slot in range(units.size()):
        scene.call("DropUnit", slot, units[slot])
    scene.call("OnSetupSlotClicked", 0)
    await create_timer(0.4).timeout
    var normal_found := false
    for candidate in scene.find_children("*", "TextureRect", true, false):
        if texture_matches(candidate.texture, normal):
            normal_found = true
            break
    if not require_ok(normal_found, "編成画面がハギの新しい通常絵を使っていない"):
        return
    await snapshot("selection-game-check.png")
    scene.call("StartBattle")
    await process_frame
    scene.call("TogglePause")
    await create_timer(0.8).timeout
    var hagi: Node = null
    var hagi_sprite: Sprite3D = null
    for candidate in scene.find_children("*", "Sprite3D", true, false):
        if texture_matches(candidate.texture, battle):
            hagi_sprite = candidate
            hagi = candidate.get_parent()
            break
    if not require_ok(hagi_sprite != null, "戦闘画面がハギの新しい戦闘絵を使っていない"):
        return
    if not require_ok(not hagi_sprite.flip_h, "味方のハギが左右反転している"):
        return
    var height = hagi_sprite.pixel_size * hagi_sprite.texture.get_height()
    if not require_ok(abs(height - 2.25) < 0.0001, "ハギの表示高"):
        return
    var foot = hagi_sprite.position.y - height * (1506.0 / 1536.0 - 0.5) * hagi_sprite.scale.y
    if not require_ok(abs(foot - 0.05) < 0.03, "ハギの足元が接地していない"):
        return
    if not require_ok(hagi_sprite.material_override.get_shader_parameter("portrait_texture") == hagi_sprite.texture, "ハギのシェーダー画像"):
        return
    await snapshot("battle-game-check.png")
    hagi.call("SetBurning", true)
    hagi.call("SetBurning", false)
    if not require_ok(texture_matches(hagi_sprite.texture, battle), "燃焼解除後のハギの画像"):
        return
    hagi.call("AnimateVictory")
    await create_timer(0.65).timeout
    if not require_ok(texture_matches(hagi_sprite.texture, normal), "勝利時がハギの通常絵ではない"):
        return
    await snapshot("victory-game-check.png")
    print("HAGI_REAPER_GAME_CAPTURE_OK selection battle burning victory source_hash height=", height, " foot=", foot)
    scene.queue_free()
    await process_frame
    await process_frame
    quit(1 if failed else 0)
