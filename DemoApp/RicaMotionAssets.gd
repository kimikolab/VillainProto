extends Control

const SOURCE = "res://assets/portraits/battle/motion_sources/rica/v1/"
const FX = "res://assets/fx/rica_motion_v1/"
const OUTPUT = "res://../design/art/rica/motion-source-v1/"
var manifest: Dictionary
var textures: Dictionary = {}
var page: Control
var materials: Array = []
var effect_nodes: Array = []
var motes: Array = []
var phase = 0.0
var paused = false
var effects = true
var stage = -1
var pose_index = -1
var body_shader: Shader
var weapon_shader: Shader
var veil_shader: Shader
var light_texture: Texture2D
var font: SystemFont
var auto_step = false
var step_age = 0.0

func _ready():
    get_window().content_scale_size = Vector2i(2000,1250)
    font = SystemFont.new()
    font.font_names = PackedStringArray(["Yu Gothic UI", "Meiryo", "Noto Sans CJK JP"])
    manifest = JSON.parse_string(FileAccess.get_file_as_string(SOURCE+"manifest.json"))
    body_shader = Shader.new()
    body_shader.code = FileAccess.get_file_as_string(FX+"body_aura.gdshader")
    weapon_shader = Shader.new()
    weapon_shader.code = FileAccess.get_file_as_string(FX+"weapon_aura.gdshader")
    veil_shader = Shader.new()
    veil_shader.code = FileAccess.get_file_as_string(FX+"soul_veil.gdshader")
    var purchased = "res://PolyBlocks/EffectBlocks/source_files/textures/sparkle.png"
    if ResourceLoader.exists(purchased):
        light_texture = load(purchased)
    else:
        var light_image = Image.new()
        light_image.load_svg_from_string(FileAccess.get_file_as_string(FX+"soul_light.svg"))
        light_texture = ImageTexture.create_from_image(light_image)
    for entry in manifest.poses:
        textures[_id(entry)] = load(SOURCE+entry.file)
    _show_page()
    var args = OS.get_cmdline_user_args()
    if "--verify-assets" in args or "--capture-assets" in args:
        call_deferred("_verify", "--capture-assets" in args)

func _id(entry: Dictionary) -> String:
    return str(int(entry.stage))+"/"+entry.key

func _vec(values: Array) -> Vector2:
    return Vector2(values[0],values[1])

func _label(text: String, at: Vector2, font_size: int, tint: Color = Color.WHITE):
    var label = Label.new()
    label.text = text
    label.position = at
    label.add_theme_font_override("font",font)
    label.add_theme_font_size_override("font_size",font_size)
    label.modulate = tint
    page.add_child(label)

func _show_page():
    if is_instance_valid(page):
        page.free()
    page = Control.new()
    add_child(page)
    materials.clear()
    effect_nodes.clear()
    motes.clear()
    var bg = ColorRect.new()
    bg.color = Color("222d38")
    bg.size = Vector2(2000,1250)
    page.add_child(bg)
    var title = "リィカ｜待機4段階・身体と武器のオーラ"
    if stage >= 0:
        title = "リィカ｜%s：キーポーズ素材" % manifest.stages[stage].name
    _label(title,Vector2(35,20),34)
    _label("0：待機比較　1–4：各段　← →：1枚ずつ　E：効果切替　P：効果停止　Space：キー順送り",Vector2(35,75),22,Color("b4c5d3"))
    _label("キーポーズの確認用です。補間された攻撃モーションはまだ生成していません。",Vector2(35,112),20,Color("b4c5d3"))
    var entries: Array = []
    for entry in manifest.poses:
        if (stage < 0 and entry.key=="idle") or (stage>=0 and int(entry.stage)==stage):
            entries.append(entry)
    if pose_index >= 0:
        var entry = entries[pose_index % entries.size()]
        _card(entry,Rect2(80,165,1840,1000))
    else:
        var rows = 1 if entries.size()<=4 else 2
        var card_height = 1030.0/rows
        for i in range(entries.size()):
            _card(entries[i],Rect2(20+(i%4)*495,170+(i/4)*card_height,485,card_height-12))
    _label("効果 %s / %s" % ["ON" if effects else "OFF", "停止" if paused else "再生"],Vector2(35,1210),20,Color("d0baff"))

func _card(entry: Dictionary, box: Rect2):
    var texture: Texture2D = textures[_id(entry)]
    var dimensions = texture.get_size()
    var info: Dictionary = manifest.stages[int(entry.stage)]
    var socket = _vec(entry.weaponSocket)*dimensions
    var tip = _vec(entry.weaponTip)*dimensions
    var delta = tip-socket
    var length = delta.length()*float(info.length)
    var width = delta.length()*(2.1 if entry.weaponShape=="greatsword" else float(info.width)*2.0)
    var direction = delta.normalized()
    var side = direction.orthogonal()
    var bounds = Rect2(-dimensions*0.10,dimensions*1.2)
    if int(entry.stage)>0:
        for corner in [socket-direction*length*0.08-side*width*0.5,socket+direction*length*0.92-side*width*0.5,socket+direction*length*0.92+side*width*0.5,socket-direction*length*0.08+side*width*0.5]:
            bounds = bounds.expand(corner)
    var ratio = min((box.size.x-30)/bounds.size.x,(box.size.y-72)/bounds.size.y)
    var subject = Node2D.new()
    subject.scale = Vector2.ONE*ratio
    subject.position = box.position+(box.size-Vector2(bounds.size.x*ratio,bounds.size.y*ratio))*0.5-bounds.position*ratio-Vector2(0,15)
    page.add_child(subject)
    if int(entry.stage)>0:
        var aura = ColorRect.new()
        aura.position = -dimensions*0.09
        aura.size = dimensions*1.18
        var mat = ShaderMaterial.new()
        mat.shader = body_shader
        mat.set_shader_parameter("portrait",texture)
        mat.set_shader_parameter("portrait_size",dimensions)
        mat.set_shader_parameter("eye_anchor",_vec(entry.eyeAnchor))
        mat.set_shader_parameter("tint",Color(info.color))
        mat.set_shader_parameter("strength",info.bodyStrength)
        aura.material = mat
        subject.add_child(aura)
        materials.append(mat)
        effect_nodes.append(aura)
        var veil = ColorRect.new()
        veil.position = Vector2(0.0,0.18)*dimensions
        veil.size = Vector2(1.0,0.82)*dimensions
        var veil_mat = ShaderMaterial.new()
        veil_mat.shader = veil_shader
        veil_mat.set_shader_parameter("tint",Color(info.color))
        veil_mat.set_shader_parameter("strength",info.bodyStrength)
        veil.material = veil_mat
        subject.add_child(veil)
        materials.append(veil_mat)
        effect_nodes.append(veil)
        var weapon_root = Node2D.new()
        weapon_root.position = socket
        weapon_root.rotation = delta.angle()
        subject.add_child(weapon_root)
        var blade = ColorRect.new()
        blade.position = Vector2(-length*0.08,-width*0.5)
        blade.size = Vector2(length,width)
        var blade_mat = ShaderMaterial.new()
        blade_mat.shader = weapon_shader
        blade_mat.set_shader_parameter("shape",{"crescent":1,"greatsword":2,"lance":3}[entry.weaponShape])
        blade_mat.set_shader_parameter("tint",Color(info.color))
        blade.material = blade_mat
        weapon_root.add_child(blade)
        materials.append(blade_mat)
        effect_nodes.append(weapon_root)
    var sprite = Sprite2D.new()
    sprite.centered = false
    sprite.texture = texture
    subject.add_child(sprite)
    if int(entry.stage)>0:
        var eye = Sprite2D.new()
        eye.texture = light_texture
        eye.position = _vec(entry.eyeAnchor)*dimensions
        eye.scale = Vector2.ONE*28.0/light_texture.get_width()
        eye.modulate = Color(info.color)
        subject.add_child(eye)
        effect_nodes.append(eye)
        for k in range(3+int(entry.stage)*2):
            var mote = Sprite2D.new()
            mote.texture = light_texture
            mote.scale = Vector2(0.7,1.45)*32.0/light_texture.get_width()
            mote.modulate = Color(info.color)*Color(1,1,1,0.72)
            subject.add_child(mote)
            effect_nodes.append(mote)
            motes.append({"node":mote,"center":Vector2(0.50,0.63)*dimensions,"radius":Vector2(0.38,0.29)*dimensions,"index":k})
    for node in effect_nodes:
        node.visible = effects
    var caption = "%s層 / %s" % [["0–2","3–5","6–8","9以上"][int(entry.stage)],entry.key]
    _label(caption,box.position+Vector2(12,box.size.y-44),22,Color("dfd9f2"))

func _process(delta):
    if not paused:
        phase += delta
    for mat in materials:
        mat.set_shader_parameter("phase",phase)
    for item in motes:
        var a = phase*0.72+item.index*2.39996
        item.node.position = item.center+Vector2(cos(a)*item.radius.x,sin(a*1.37)*item.radius.y)
        item.node.modulate.a = 0.32+0.40*(0.5+0.5*sin(a*1.7))
    if auto_step and not paused:
        step_age += delta
        if step_age>0.8:
            step_age = 0
            pose_index += 1
            _show_page()

func _unhandled_key_input(event):
    if not event.is_pressed() or event.echo:
        return
    match event.keycode:
        KEY_0:
            stage=-1;pose_index=-1;_show_page()
        KEY_1,KEY_2,KEY_3,KEY_4:
            stage=event.keycode-KEY_1;pose_index=-1;_show_page()
        KEY_RIGHT:
            pose_index+=1;_show_page()
        KEY_LEFT:
            pose_index=max(0,pose_index-1);_show_page()
        KEY_E:
            effects=not effects
            _show_page()
        KEY_P:
            paused=not paused
            _show_page()
        KEY_SPACE:
            auto_step=not auto_step
            if pose_index<0:
                pose_index=0;_show_page()
        KEY_ESCAPE:
            get_tree().quit()

func _verify(capture: bool):
    assert(manifest.poses.size()==20,"待機4枚と攻撃16枚")
    for material_name in ["crescent","greatsword","lance"]:
        assert(load(FX+material_name+".tres") is ShaderMaterial,"霊刃のプリセットを読み込める")
    for entry in manifest.poses:
        var image = textures[_id(entry)].get_image()
        assert(image.get_format()==Image.FORMAT_RGBA8,"RGBA形式")
        var dims = image.get_size()
        assert(dims==Vector2i(1024,1536) if entry.key=="idle" else dims==Vector2i(1254,1254),"キャンバスの宣言と一致")
        assert(image.get_pixel(0,0).a<0.02,"隅は透過")
        for key in ["weaponSocket","weaponTip","rearHand","frontHand","groundAnchor","eyeAnchor"]:
            var v = _vec(entry[key])
            assert(v.x>=0 and v.y>=0 and v.x<=1 and v.y<=1,"基準点が画像内")
        assert(_vec(entry.weaponSocket).distance_to(_vec(entry.weaponTip))>0.1,"武器の軸が存在")
    var before = phase
    paused=true;_process(0.5)
    assert(phase==before,"停止中は位相を進めない")
    paused=false;_process(0.5)
    assert(phase>before,"再開すると進む")
    if capture:
        for page_stage in [-1,0,1,2,3]:
            stage=page_stage;pose_index=-1;phase=1.15;paused=true;effects=true
            _show_page()
            await get_tree().process_frame
            await RenderingServer.frame_post_draw
            var file = "idle-stages-fx.png" if stage<0 else "stage%d-keys-fx.png"%stage
            get_viewport().get_texture().get_image().save_png(ProjectSettings.globalize_path(OUTPUT+file))
        stage=-1;effects=false;_show_page()
        await get_tree().process_frame
        await RenderingServer.frame_post_draw
        get_viewport().get_texture().get_image().save_png(ProjectSettings.globalize_path(OUTPUT+"idle-stages-base.png"))
    print("RICA_MOTION_ASSETS_OK poses=20 rgba anchors weapons phase pause layers separate")
    get_tree().quit()
