extends SceneTree

# 編成画面の表示確認用。起動引数 --demo-preset=ボルグ×ホタ と併用する。
func _initialize():
    call_deferred("capture")

func capture():
    var scene = load("res://Main.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    await process_frame
    await RenderingServer.frame_post_draw
    root.get_texture().get_image().save_png("res://../design/art/hota/selection-front-check.png")
    print("SELECTION_CAPTURE_OK")
    quit()
