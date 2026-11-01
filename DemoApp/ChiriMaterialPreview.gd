extends Control

## 素材の試写室。本編の台本接続や戦闘ルールはここでは実装しない。
const KIT = preload("res://assets/fx/chiri/chiri_material_kit.gd")
const TITLES := ["仕掛ける", "導火線を縮める", "爆発・誘爆", "向かいへ飛ぶ", "不発"]
const NOTES := [
    "手渡し差分 → 放した差分。敵と味方の頭上へ同じ爆弾を配置。",
    "被弾の出来事ごとに3 → 2 → 1 → 0。火花は一撃の瞬間だけ。",
    "閃光と火炎が一気に広がり、火の粉と短い煤煙を残す。",
    "味方から向かいの敵へ火球。爆発は到達先だけに発生。",
    "火花を止め、爆弾を暗くして静かに消す。爆発・ダメージなし。"
]
var paused := false
var speed := 1.0
var phase := 0
var age := 0.0
var auto_play := true
var _fuse_step := 3
var _pose_released := false
var _kit
var _world: Node3D
var _camera: Camera3D
var _portrait: Sprite3D
var _poses: Array[Texture2D] = []
var _markers: Array = []
var _title: Label
var _note: Label
var _clock_label: Label
var _pause_button: Button
var _viewport: SubViewport
var _status: Label
var _enemy_point := Vector3(1.5, 1.4, 0)
var _ally_point := Vector3(-1.1, 1.4, 0)

func _ready() -> void:
    get_window().title = "導火線のチリ — 演出素材プレビュー"
    theme = Theme.new()
    var font := SystemFont.new()
    font.font_names = PackedStringArray(["Yu Gothic UI", "Meiryo"])
    theme.default_font = font
    theme.default_font_size = 17
    build_stage()
    build_ui()
    _poses = [
        KIT.texture("res://assets/portraits/battle/chiri_idle_right.png"),
        KIT.texture("res://assets/portraits/battle/chiri_plant_idle_right.png"),
        KIT.texture("res://assets/portraits/battle/chiri_release_idle_right.png")
    ]
    _portrait = person(_poses[0], Vector3(-3.8, 0, 0), 2.65, false)
    person(KIT.texture("res://assets/portraits/battle/sero_idle_right.png"), Vector3(-1.1, 0, 0), 2.6, false)
    var knight := KIT.texture("res://assets/portraits/battle/knight_idle_right.png")
    person(knight, Vector3(1.5, 0, 0), 2.65, true)
    person(knight, Vector3(3.8, 0, 0), 2.65, true)
    for entry in [[-3.8, "導火線のチリ", false], [-1.1, "味方の持ち主", false], [1.5, "敵の持ち主", true], [3.8, "隣の敵", true]]:
        var label := Label3D.new()
        label.text = entry[1]
        label.font = font
        label.font_size = 40
        label.pixel_size = 0.005
        label.outline_size = 10
        label.modulate = Color("ffb49c") if entry[2] else Color("b3e7ee")
        label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
        label.position = Vector3(entry[0], -0.28, 0.2)
        _world.add_child(label)
    _kit = KIT.new()
    _kit.camera = _camera
    _kit.clock = func(): return Vector2(1 if paused else 0, speed)
    _world.add_child(_kit)
    var args := OS.get_cmdline_user_args()
    if args.has("--export-glyphs"):
        auto_play = false
        await export_glyphs()
    elif args.has("--verify"):
        auto_play = false
        await verify_materials()
    elif args.has("--record"):
        auto_play = false
        await record_materials()
    elif args.has("--capture"):
        auto_play = false
        await capture_materials()
    else:
        start_phase(0)
        print("CHIRI_MATERIAL_PREVIEW_READY")

func build_stage() -> void:
    var background := ColorRect.new()
    background.color = Color("101a20")
    background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    add_child(background)
    var container := SubViewportContainer.new()
    container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    container.offset_top = 200
    container.offset_bottom = -125
    container.stretch = true
    add_child(container)
    _viewport = SubViewport.new()
    _viewport.size = Vector2i(1440, 575)
    _viewport.own_world_3d = true
    _viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    container.add_child(_viewport)
    _world = Node3D.new()
    _viewport.add_child(_world)
    var environment := WorldEnvironment.new()
    environment.environment = Environment.new()
    environment.environment.background_mode = Environment.BG_COLOR
    environment.environment.background_color = Color("142129")
    environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
    environment.environment.ambient_light_color = Color("c4d6df")
    environment.environment.ambient_light_energy = 0.8
    _world.add_child(environment)
    var light := DirectionalLight3D.new()
    light.rotation_degrees = Vector3(-45, -25, 0)
    light.light_energy = 0.8
    _world.add_child(light)
    _camera = Camera3D.new()
    _camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    _camera.size = 5.0
    _camera.position = Vector3(0, 4.8, 13)
    _world.add_child(_camera)
    _camera.look_at(Vector3(0, 1.65, 0))
    var ground := MeshInstance3D.new()
    var plane := PlaneMesh.new()
    plane.size = Vector2(30, 20)
    ground.mesh = plane
    var material := StandardMaterial3D.new()
    material.albedo_color = Color("24353b")
    material.roughness = 0.95
    ground.material_override = material
    ground.position.y = -0.10
    _world.add_child(ground)
    for x in [-3.8, -1.1, 1.5, 3.8]:
        var ring := MeshInstance3D.new()
        var mesh := TorusMesh.new()
        mesh.inner_radius = 0.45
        mesh.outer_radius = 0.48
        mesh.rings = 32
        mesh.ring_segments = 8
        ring.mesh = mesh
        ring.position = Vector3(x, -0.07, 0)
        var tint := StandardMaterial3D.new()
        tint.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
        tint.albedo_color = Color("a96e52") if x > 0 else Color("4e929b")
        ring.material_override = tint
        _world.add_child(ring)

func person(image: Texture2D, point: Vector3, height: float, mirrored: bool) -> Sprite3D:
    var sprite := Sprite3D.new()
    sprite.texture = image
    sprite.pixel_size = height / image.get_height()
    sprite.billboard = BaseMaterial3D.BILLBOARD_ENABLED
    sprite.shaded = false
    sprite.flip_h = mirrored
    sprite.position = point + Vector3.UP * (height / 2 - 0.06)
    _world.add_child(sprite)
    return sprite

func build_ui() -> void:
    var stack := VBoxContainer.new()
    stack.position = Vector2(30, 18)
    stack.add_theme_constant_override("separation", 9)
    add_child(stack)
    var eyebrow := Label.new()
    eyebrow.text = "CHIRI / PRESENTATION ASSETS 01"
    eyebrow.add_theme_color_override("font_color", Color("e7b66b"))
    eyebrow.add_theme_font_size_override("font_size", 14)
    stack.add_child(eyebrow)
    _title = Label.new()
    _title.add_theme_font_size_override("font_size", 30)
    stack.add_child(_title)
    _note = Label.new()
    _note.add_theme_color_override("font_color", Color("b2c4ca"))
    stack.add_child(_note)
    var row := HBoxContainer.new()
    row.add_theme_constant_override("separation", 10)
    stack.add_child(row)
    for i in range(5):
        var index := i
        button(row, str(i + 1) + "  " + TITLES[i], func(): auto_play = false; start_phase(index))
    var footer := VBoxContainer.new()
    footer.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
    footer.offset_top = -111
    footer.offset_left = 30
    footer.offset_right = -30
    footer.add_theme_constant_override("separation", 8)
    add_child(footer)
    var tools := HBoxContainer.new()
    tools.add_theme_constant_override("separation", 12)
    footer.add_child(tools)
    _pause_button = button(tools, "一時停止", func():
        paused = not paused
        _pause_button.text = "再開" if paused else "一時停止")
    button(tools, "もう一度", func(): start_phase(phase))
    button(tools, "通し再生", func(): auto_play = true; start_phase(0))
    button(tools, "速さを変更", func(): speed = 2.0 if speed == 1 else (0.25 if speed == 2 else 1.0))
    button(tools, "音あり／なし", func(): _kit.sound_enabled = not _kit.sound_enabled; start_phase(phase))
    _clock_label = Label.new()
    tools.add_child(_clock_label)
    _status = Label.new()
    _status.text = "表示素材の確認用。導火線の段数・爆発先は台本の指定で変える。"
    _status.add_theme_color_override("font_color", Color("8ba6b1"))
    footer.add_child(_status)

func button(row: Node, text: String, action: Callable) -> Button:
    var control := Button.new()
    control.text = text
    control.custom_minimum_size = Vector2(140, 36)
    control.focus_mode = Control.FOCUS_NONE
    control.pressed.connect(action)
    row.add_child(control)
    return control

func start_phase(index: int) -> void:
    _kit.clear()
    phase = index
    age = 0
    _fuse_step = 3
    _pose_released = false
    _portrait.texture = _poses[0]
    _markers.clear()
    _title.text = "%02d  %s" % [phase + 1, TITLES[phase]]
    _note.text = NOTES[phase]
    var ally_head := _ally_point + Vector3.UP * 1.62
    var enemy_head := _enemy_point + Vector3.UP * 1.62
    match phase:
        0:
            _portrait.texture = _poses[1]
            var hand := Vector3(-2.96, 1.76, 0.1)
            _markers.append(_kit.plant(hand, enemy_head))
            _markers.append(_kit.plant(hand, ally_head, true))
        1:
            _markers.append(_kit.marker(enemy_head, 3))
        2:
            _kit.blast(_enemy_point)
            _kit.blast(Vector3(3.8, 1.4, 0), 0.85, 0.14)
        3:
            var missile = _kit.mirror(_ally_point, _enemy_point)
            missile.arrived.connect(func(): _kit.blast(_enemy_point))
        4:
            var mark = _kit.marker(enemy_head, 1)
            _markers.append(mark)
            _kit.dud(mark)

func _process(delta: float) -> void:
    if not is_instance_valid(_kit):
        return
    _clock_label.text = "×%s  /  %s" % [str(speed), "停止中" if paused else "再生中"]
    if paused:
        return
    age += delta * speed
    if phase == 0 and age > 0.18 and not _pose_released:
        _portrait.texture = _poses[2]
        _pose_released = true
    if phase == 1:
        var next := maxi(0, 3 - int(age / 0.75))
        if next < _fuse_step:
            _fuse_step = next
            _kit.shorten(_markers[0], next)
    if auto_play and age > 3.2:
        start_phase((phase + 1) % 5)

func capture_materials() -> void:
    var output := "res://../design/art/chiri/presentation-assets-v1/previews"
    for argument in OS.get_cmdline_user_args():
        if argument.begins_with("--capture-dir="):
            output = argument.trim_prefix("--capture-dir=")
    DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output))
    _kit.sound_enabled = false
    var moments := [0.50, 1.65, 0.24, 0.24, 0.20]
    for i in range(5):
        paused = false
        start_phase(i)
        await get_tree().create_timer(moments[i]).timeout
        paused = true
        await RenderingServer.frame_post_draw
        var path := output + "/%02d.png" % (i + 1)
        var error := get_viewport().get_texture().get_image().save_png(ProjectSettings.globalize_path(path))
        if error != OK:
            push_error("チリ素材のキャプチャに失敗: " + path)
            get_tree().quit(1)
            return
        print("CHIRI_MATERIAL_CAPTURE ", path)
        _kit.clear()
        await get_tree().process_frame
    print("CHIRI_MATERIAL_CAPTURE_COMPLETE")
    get_tree().quit()

func verify_materials() -> void:
    _kit.sound_enabled = false
    start_phase(1)
    paused = true
    var mark = _markers[0]
    mark._process(0.1)
    var old_age: float = mark.age
    mark._process(1.0)
    assert(mark.age == old_age, "停止中に導火線の時計が進んだ")
    paused = false
    speed = 2
    mark._process(0.1)
    assert(is_equal_approx(mark.age - old_age, 0.2), "倍速が反映されていない")
    for count in [2, 1, 0]:
        _kit.shorten(mark, count)
        assert(mark.remaining == count, "台本指定の導火線段数と異なる")
    _kit.dud(mark)
    assert(mark.dud_age == 0 and mark.remaining == 0, "不発が火花を残した")
    _kit.clear()
    assert(_kit.active_count() == 0, "リセット後に素材が残った")
    await get_tree().process_frame
    # 到達前に爆発を発生させず、停止・倍速も同じ時計へ従うこと。
    var arrivals: Array[int] = [0]
    var missile = _kit.mirror(_ally_point, _enemy_point)
    missile.arrived.connect(func(): arrivals[0] += 1)
    paused = true
    missile._process(1.0)
    assert(arrivals[0] == 0 and missile.age == 0, "停止中に火球が到達した")
    paused = false
    speed = 2
    missile._process(0.12)
    assert(arrivals[0] == 0, "火球の到達通知が早すぎる")
    missile._process(0.12)
    assert(arrivals[0] == 1, "火球の到達通知が欠けた")
    _kit.clear()
    await get_tree().process_frame
    for cycle in range(2):
        for index in range(5):
            speed = 1
            start_phase(index)
            await get_tree().process_frame
            assert(_kit.active_count() > 0, "素材が生成されていない")
            _kit.clear()
            await get_tree().process_frame
            assert(_kit.active_count() == 0, "再試写時に素材が残った")
    print("CHIRI_MATERIAL_VERIFY_COMPLETE phases=5 repeat=2 pause=ok speed=ok fuse=ok arrival=ok clear=ok")
    get_tree().quit()

func record_materials() -> void:
    var frames := [30, 72, 24, 30, 24]
    for index in range(5):
        start_phase(index)
        for frame in range(frames[index]):
            await RenderingServer.frame_post_draw
    _kit.clear()
    await get_tree().process_frame
    print("CHIRI_MATERIAL_RECORD_COMPLETE")
    get_tree().quit()

func export_glyphs() -> void:
    paused = true
    var viewport := SubViewport.new()
    viewport.size = Vector2i(256, 384)
    viewport.transparent_bg = true
    viewport.own_world_3d = true
    viewport.msaa_3d = Viewport.MSAA_4X
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    add_child(viewport)
    var scene := Node3D.new()
    viewport.add_child(scene)
    var camera := Camera3D.new()
    camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    camera.size = 1.4
    camera.position = Vector3(0, 0.15, 4)
    scene.add_child(camera)
    camera.look_at(Vector3(0, 0.15, 0))
    var kit = KIT.new()
    kit.camera = camera
    kit.clock = func(): return Vector2(1, 1)
    kit.sound_enabled = false
    scene.add_child(kit)
    var original := "res://../design/art/chiri/presentation-assets-v1/glyphs"
    DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(original))
    for stage in [3, 2, 1, 0, -1]:
        var mark = kit.marker(Vector3.ZERO, maxi(0, stage))
        if stage == -1:
            mark.extinguish()
        await get_tree().process_frame
        await RenderingServer.frame_post_draw
        var suffix := "dud" if stage == -1 else "fuse_%d" % stage
        var image := viewport.get_texture().get_image()
        for root in ["res://assets/fx/chiri", original]:
            var error := image.save_png(ProjectSettings.globalize_path(root + "/bomb_" + suffix + ".png"))
            assert(error == OK, "爆弾の印を書き出せない")
        kit.clear()
        await get_tree().process_frame
    print("CHIRI_GLYPH_EXPORT_COMPLETE count=5 size=256x384 rgba=ok")
    get_tree().quit()
