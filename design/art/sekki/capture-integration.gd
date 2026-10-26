extends SceneTree

# セッキの採用原本・透過・実画面の立ち絵と接地を確認する。
const ART_DIR = "res://../design/art/sekki/"
const BATTLE_PATH = "res://assets/portraits/battle/sekki_idle_right.png"
const WORLD_HEIGHT = 3.40

func _initialize():
    call_deferred("capture")

func capture():
    var pairs = [
        ["res://assets/portraits/sekki.png", "sekki-front-v1-final.png"],
        [BATTLE_PATH, "sekki-idle-right-v1.png"],
    ]
    for pair in pairs:
        var image = Image.load_from_file(ProjectSettings.globalize_path(pair[0]))
        if image == null or image.get_size() != Vector2i(1254, 1254) or image.get_format() != Image.FORMAT_RGBA8:
            fail("寸法・RGBA8: " + pair[0])
            return
        for corner in [Vector2i(0, 0), Vector2i(1253, 0), Vector2i(0, 1253), Vector2i(1253, 1253)]:
            if image.get_pixelv(corner).a != 0.0:
                fail("四隅alpha=0: " + pair[0])
                return
        if FileAccess.get_sha256(pair[0]) != FileAccess.get_sha256(ART_DIR + pair[1]):
            fail("原本のSHA-256: " + pair[0])
            return
        print("SEKKI_IMAGE_OK path=", pair[0], " size=1254x1254 format=RGBA8 corners=0,0,0,0 sha256=match")
        var data = image.get_data()
        for threshold in [0, 5, 8, 127]:
            var left = 1254
            var top = 1254
            var right = -1
            var bottom = -1
            var count = 0
            for y in range(1254):
                for x in range(1254):
                    if data[(y * 1254 + x) * 4 + 3] > threshold:
                        left = mini(left, x)
                        top = mini(top, y)
                        right = maxi(right, x)
                        bottom = maxi(bottom, y)
                        count += 1
            print("SEKKI_ALPHA threshold=", threshold, " bbox=", Vector4i(left, top, right, bottom),
                " pixels=", count, " bottom_padding_px=", 1253 - bottom)
    if "--sekki-assets-only" in OS.get_cmdline_user_args():
        print("SEKKI_ASSETS_CHECK_OK")
        quit(0)
        return
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    scene.call("OnSetupSlotClicked", 3)
    await create_timer(0.5).timeout
    if not await save_frame("selection-game-check.png"):
        return
    scene.call("StartBattle")
    await process_frame
    # 開戦バナーが消えた後、待機絵を一時停止して撮る。
    await create_timer(0.9).timeout
    scene.call("TogglePause")
    await create_timer(0.5).timeout
    var found = false
    var battle_image = Image.load_from_file(ProjectSettings.globalize_path(BATTLE_PATH))
    for sprite in scene.find_children("*", "Sprite3D", true, false):
        # UiKitはImageTextureを生成するためresource_pathではなく画素で照合する。
        if sprite.texture != null and sprite.texture.get_size() == Vector2(1254, 1254) and sprite.texture.get_image().get_data() == battle_image.get_data():
            var height = sprite.texture.get_height() * sprite.pixel_size
            if abs(height - WORLD_HEIGHT) > 0.0001 or sprite.flip_h:
                fail("味方の表示高・右向き: " + str(height))
                return
            print("SEKKI_SPRITE_OK height=", height, " position=", sprite.position, " flip_h=", sprite.flip_h)
            found = true
    if not found:
        fail("本番Sprite3Dにセッキの待機絵がない")
        return
    if not await save_frame("battle-game-check.png"):
        return
    print("SEKKI_ART_CAPTURE_OK originals alpha height=3.40 actual-preset")
    scene.queue_free()
    await process_frame
    await process_frame
    quit(0)

func save_frame(filename):
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png(ART_DIR + filename)
    if result != OK:
        fail("撮影: " + filename)
        return false
    return true

func fail(message):
    push_error("セッキ立ち絵確認失敗: " + message)
    quit(1)
