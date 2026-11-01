class_name ChiriMaterialKit
extends Node3D

## チリ用の表示素材。対象・段数・爆発位置は呼び出し側の台本から渡す。
## 戦闘判定、隣接探索、導火線の自動カウントは持たない。
const BODY_PATH := "res://assets/fx/chiri/bomb_body.png"
const EFFECT_ROOT := "res://PolyBlocks/EffectBlocks/assets/"
const SOUND_ROOT := "res://assets/audio/se/chiri/"
var clock: Callable = func(): return Vector2(0, 1)
var camera: Camera3D
var sound_enabled := true
var _body: Texture2D

static func texture(path: String) -> Texture2D:
    return ImageTexture.create_from_image(Image.load_from_file(ProjectSettings.globalize_path(path)))

func _ready() -> void:
    _body = texture(BODY_PATH)

func marker(point: Vector3, remaining := 3, ally := false, delay := 0.0) -> BombMarker:
    var mark := BombMarker.new()
    mark.clock = clock
    mark.camera = camera
    mark.remaining = clampi(remaining, 0, 3)
    mark.ally = ally
    mark.age = -delay
    add_child(mark)
    mark.global_position = point
    mark.build(_body)
    return mark

func plant(from: Vector3, to: Vector3, ally := false) -> BombMarker:
    var flight := _effect("delivery", from, to, 0.36)
    flight.sfx = "plant"
    return marker(to, 3, ally, 0.36)

func shorten(mark: BombMarker, remaining: int) -> void:
    if not is_instance_valid(mark) or mark.is_queued_for_deletion():
        return
    mark.set_fuse(remaining)
    _effect("sparks", mark.tip(), mark.tip(), 0.24).sfx = "fuse"

func blast(point: Vector3, size := 1.0, delay := 0.0) -> Burst:
    var effect := _effect("blast", point, point, 1.18, delay)
    effect.effect_size = clampf(size, 0.5, 1.6)
    effect.sfx = "blast"
    return effect

func mirror(from: Vector3, to: Vector3) -> Burst:
    var effect := _effect("mirror", from, to, 0.48)
    effect.sfx = "mirror"
    return effect

func dud(mark: BombMarker) -> void:
    if is_instance_valid(mark) and not mark.is_queued_for_deletion():
        mark.extinguish()
        var effect := _effect("dud", mark.global_position, mark.global_position, 0.28)
        effect.sfx = "dud"

func clear() -> void:
    for child in get_children():
        if child is Burst and is_instance_valid(child.audio):
            child.audio.stop()
        child.hide()
        child.set_process(false)
        child.queue_free()

func active_count() -> int:
    var count := 0
    for child in get_children():
        if not child.is_queued_for_deletion():
            count += 1
    return count

func transient_count() -> int:
    var count := 0
    for child in get_children():
        if not child.is_queued_for_deletion() and (child is Burst or child is BombMarker and child.dud_age >= 0):
            count += 1
    return count

func _effect(kind: String, from: Vector3, to: Vector3, duration: float, delay := 0.0) -> Burst:
    var effect := Burst.new()
    effect.kind = kind
    effect.clock = clock
    effect.camera = camera
    effect.from = from
    effect.to = to
    effect.duration = duration
    effect.age = -delay
    effect.body_texture = _body
    effect.sound_enabled = sound_enabled
    add_child(effect)
    return effect


class BombMarker extends Node3D:
    var clock: Callable
    var camera: Camera3D
    var remaining := 3
    var ally := false
    var age := 0.0
    var dud_age := -1.0
    var body: Sprite3D
    var count: Label3D
    var cord := ImmediateMesh.new()
    var rope: MeshInstance3D
    var ember: MeshInstance3D
    var _built := false

    func build(image: Texture2D) -> void:
        body = Sprite3D.new()
        body.texture = image
        body.pixel_size = 0.58 / image.get_height()
        body.billboard = BaseMaterial3D.BILLBOARD_ENABLED
        body.shaded = false
        body.no_depth_test = true
        body.render_priority = 6
        add_child(body)
        var material := StandardMaterial3D.new()
        material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
        material.vertex_color_use_as_albedo = true
        material.cull_mode = BaseMaterial3D.CULL_DISABLED
        material.no_depth_test = true
        rope = MeshInstance3D.new()
        rope.mesh = cord
        rope.material_override = material
        rope.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
        add_child(rope)
        var sphere := SphereMesh.new()
        sphere.radius = 0.026
        sphere.height = 0.052
        ember = MeshInstance3D.new()
        ember.mesh = sphere
        var hot := StandardMaterial3D.new()
        hot.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
        hot.albedo_color = Color("fff0ac")
        hot.emission_enabled = true
        hot.emission = Color("ff9b35")
        hot.emission_energy_multiplier = 2.0
        hot.no_depth_test = true
        ember.material_override = hot
        add_child(ember)
        count = Label3D.new()
        count.font_size = 48
        count.pixel_size = 0.004
        count.outline_size = 12
        count.modulate = Color("a4e9ff") if ally else Color("ffd591")
        count.billboard = BaseMaterial3D.BILLBOARD_ENABLED
        count.no_depth_test = true
        add_child(count)
        _built = true
        draw_marker()

    func set_fuse(value: int) -> void:
        remaining = clampi(value, 0, 3)
        draw_marker()

    func extinguish() -> void:
        dud_age = 0
        remaining = 0
        draw_marker()

    func point(t: float) -> Vector3:
        return camera.global_basis.x * (sin(t * PI * 0.5) * 0.38) + camera.global_basis.y * (0.20 + t * 0.46)

    func tip() -> Vector3:
        return to_global(point(float(remaining) / 3))

    func _process(delta: float) -> void:
        var state: Vector2 = clock.call()
        if state.x > 0:
            return
        age += delta * state.y
        if dud_age >= 0:
            dud_age += delta * state.y
            if dud_age >= 0.48:
                hide()
                queue_free()
                return
        draw_marker()

    func draw_marker() -> void:
        if not _built:
            return
        visible = age >= 0
        var alpha := 1.0 if dud_age < 0 else maxf(0, 1 - dud_age / 0.48)
        body.modulate = Color(1, 1, 1, alpha) if dud_age < 0 else Color(0.38, 0.40, 0.43, alpha)
        body.position = camera.global_basis.y * (-dud_age * 0.12 if dud_age >= 0 else 0)
        count.text = str(remaining) if dud_age < 0 else ""
        count.position = camera.global_basis.y * -0.38
        ember.visible = remaining > 0 and dud_age < 0
        ember.position = point(float(remaining) / 3)
        ember.scale = Vector3.ONE * (0.88 + sin(age * 23) * 0.12)
        cord.clear_surfaces()
        if remaining <= 0:
            return
        cord.surface_begin(Mesh.PRIMITIVE_TRIANGLE_STRIP)
        for i in range(31):
            var t := float(i) / 30 * remaining / 3
            var p := point(t)
            var tangent := (point(t + 0.005) - p).normalized()
            var side := tangent.cross(camera.global_basis.z).normalized() * 0.014
            var color := Color("d1ac70") if i % 3 else Color("745337")
            cord.surface_set_color(color)
            cord.surface_add_vertex(p - side)
            cord.surface_set_color(color.darkened(0.15))
            cord.surface_add_vertex(p + side)
        cord.surface_end()


class Burst extends Node3D:
    signal arrived
    var kind := "sparks"
    var clock: Callable
    var camera: Camera3D
    var from := Vector3.ZERO
    var to := Vector3.ZERO
    var duration := 0.5
    var age := 0.0
    var effect_size := 1.0
    var sfx := ""
    var sound_enabled := true
    var body_texture: Texture2D
    var started := false
    var particles: Array[GPUParticles3D] = []
    var visual: Node3D
    var sparks: Array[MeshInstance3D] = []
    var body: Sprite3D
    var audio: AudioStreamPlayer
    var flash: ShaderMaterial
    var fireball: ShaderMaterial
    var firework_trails: ImmediateMesh
    var firework_stars: ImmediateMesh

    func _process(delta: float) -> void:
        var state: Vector2 = clock.call()
        for particle in particles:
            particle.speed_scale = 0 if state.x > 0 else state.y
        if is_instance_valid(audio):
            audio.stream_paused = state.x > 0
        if state.x > 0:
            return
        age += delta * state.y
        if age < 0:
            return
        if not started:
            started = true
            build()
        var t := clampf(age / duration, 0, 1)
        if flash:
            flash.set_shader_parameter("progress", t)
        if fireball:
            fireball.set_shader_parameter("seconds", age)
        if firework_trails:
            draw_fireworks(t)
        if kind == "delivery" or kind == "mirror":
            global_position = from.lerp(to, smoothstep(0, 1, t)) + camera.global_basis.y * sin(t * PI) * (0.32 if kind == "delivery" else 0.15)
            if body:
                body.rotation.z = -t * 1.6
        for i in range(sparks.size()):
            var angle := i * 2.399963
            var velocity := camera.global_basis.x * cos(angle) + camera.global_basis.y * sin(angle)
            sparks[i].position = velocity * (0.04 + t * 0.40) + Vector3.DOWN * t * t * 0.12
            sparks[i].scale = Vector3.ONE * maxf(0.01, 1 - t)
        if age >= duration:
            if kind == "mirror":
                arrived.emit()
            hide()
            queue_free()

    func build() -> void:
        global_position = from
        if kind == "delivery":
            body = Sprite3D.new()
            body.texture = body_texture
            body.pixel_size = 0.32 / body_texture.get_height()
            body.billboard = BaseMaterial3D.BILLBOARD_ENABLED
            body.no_depth_test = true
            body.shaded = false
            add_child(body)
        elif kind == "blast" or kind == "mirror":
            var relative := "explosions/explosion_heavy.tscn" if kind == "blast" else "fire/fire_light.tscn"
            var path := "res://PolyBlocks/EffectBlocks/assets/" + relative
            if ResourceLoader.exists(path):
                visual = load(path).instantiate()
                prepare(visual)
                add_child(visual)
                visual.scale = Vector3.ONE * (1.65 * effect_size if kind == "blast" else 0.52)
                if kind == "mirror":
                    var tail := (from - to).normalized()
                    var side := tail.cross(Vector3.FORWARD).normalized()
                    if side.length_squared() < 0.01:
                        side = Vector3.RIGHT
                    visual.basis = Basis(side, tail, side.cross(tail).normalized()).scaled(Vector3(0.48, 0.86, 0.48))
                for particle in particles:
                    particle.emitting = true
                    particle.restart()
            else:
                push_warning("チリの購入素材が未配置: " + path)
            # 飛ぶ火球には小さな芯だけを付ける。爆発は到達先で呼ぶ。
            if kind == "mirror":
                spark_ball(0.12, Color("ffbb51"))
            else:
                build_flash()
                build_fireball()
                build_fireworks()
        elif kind == "sparks":
            for i in range(10):
                var spark := spark_ball(0.012 + (i % 3) * 0.004, Color("ffd687"))
                sparks.append(spark)
        if sound_enabled and not sfx.is_empty():
            var path := ProjectSettings.globalize_path("res://assets/audio/se/chiri/" + sfx + ".wav")
            if FileAccess.file_exists(path):
                audio = AudioStreamPlayer.new()
                audio.stream = AudioStreamWAV.load_from_file(path)
                audio.volume_db = -9 if kind == "blast" else -18
                add_child(audio)
                audio.play()

    func spark_ball(radius: float, tint: Color) -> MeshInstance3D:
        var ball := MeshInstance3D.new()
        var mesh := SphereMesh.new()
        mesh.radius = radius
        mesh.height = radius * 2
        mesh.radial_segments = 8
        mesh.rings = 4
        ball.mesh = mesh
        var material := StandardMaterial3D.new()
        material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
        material.albedo_color = tint
        material.emission_enabled = true
        material.emission = tint
        material.emission_energy_multiplier = 1.4
        ball.material_override = material
        ball.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
        add_child(ball)
        return ball

    func build_flash() -> void:
        var shader := Shader.new()
        shader.code = """shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never,depth_test_disabled;
uniform float progress=0.0;
void vertex(){
    MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),
    INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),INV_VIEW_MATRIX[2]*length(MODEL_MATRIX[2].xyz),MODEL_MATRIX[3]);
}
void fragment(){
    vec2 p=(UV-0.5)*2.0; float r=length(p); float t=progress;
    float ring=exp(-pow((r-(0.10+min(t*6.0,1.0)*0.85))/0.045,2.0))*max(0.0,1.0-t*5.0);
    float core=exp(-r*r*12.0)*max(0.0,1.0-t*12.0);
    ALBEDO=vec3(1.0,0.82,0.46); EMISSION=ALBEDO*2.2;
    ALPHA=clamp(ring*0.9+core,0.0,1.0);
}"""
        flash = ShaderMaterial.new()
        flash.shader = shader
        var card := MeshInstance3D.new()
        var quad := QuadMesh.new()
        quad.size = Vector2.ONE * (4.0 * effect_size)
        card.mesh = quad
        card.material_override = flash
        card.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
        add_child(card)

    func build_fireball() -> void:
        # 最初の0.4秒に火炎の塊を立ち上げ、その外に短く煤煙を残す。
        # TIMEではなく本編の時計を渡すので、停止・倍速でも火炎と着弾がずれない。
        var shader := Shader.new()
        shader.code = """shader_type spatial;
render_mode unshaded,cull_disabled,blend_mix,depth_draw_never,depth_test_disabled;
uniform float seconds=0.0;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){
    vec2 i=floor(p); vec2 f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
float cloud(vec2 p,float growth){
    float d=length(p)-0.47*growth;
    for(int i=0;i<7;i++){
        float a=float(i)*0.897598+sin(float(i)*7.1)*0.08;
        vec2 center=vec2(cos(a),sin(a))*0.44*growth;
        float size=0.25+0.035*sin(float(i)*4.7);
        d=min(d,length(p-center)-size*growth);
    }
    return d;
}
void vertex(){
    MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),
    INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),INV_VIEW_MATRIX[2]*length(MODEL_MATRIX[2].xyz),MODEL_MATRIX[3]);
}
void fragment(){
    vec2 p=(UV-0.5)*2.0; float s=seconds; float r=length(p);
    float n=noise(p*7.0+vec2(s*2.0,-s*4.0))*0.65+noise(p*16.0-s*3.0)*0.35;
    float growth=0.15+0.85*(1.0-exp(-s*30.0)); float radius=growth*0.67;
    float edge=cloud(p,growth)-(n-0.5)*0.09;
    float flame=(1.0-smoothstep(-0.035,0.025,edge))*(1.0-smoothstep(0.23,0.43,s));
    float heat=clamp((1.0-r/max(radius,0.05))*0.85+n*0.75-s*0.85,0.0,1.0);
    vec3 hot=mix(vec3(0.65,0.08,0.008),vec3(1.0,0.36,0.015),smoothstep(0.12,0.48,heat));
    hot=mix(hot,vec3(1.0,0.88,0.30),smoothstep(0.45,0.75,heat));
    hot=mix(hot,vec3(1.0,0.98,0.77),smoothstep(0.77,0.96,heat));
    float smoke_edge=cloud(p,min(1.2,0.52+s*1.05))-(n-0.5)*0.12;
    float smoke=(1.0-smoothstep(-0.07,0.025,smoke_edge))*smoothstep(0.09,0.22,s)*(1.0-smoothstep(0.35,0.78,s))*0.62;
    vec3 soot=mix(vec3(0.10,0.075,0.06),vec3(0.30,0.23,0.16),n);
    ALBEDO=mix(soot,hot,flame); EMISSION=hot*flame*1.3;
    ALPHA=max(flame*0.96,smoke);
}"""
        fireball = ShaderMaterial.new()
        fireball.shader = shader
        fireball.render_priority = 2
        var card := MeshInstance3D.new()
        var quad := QuadMesh.new()
        quad.size = Vector2.ONE * (3.5 * effect_size)
        card.mesh = quad
        card.material_override = fireball
        card.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
        add_child(card)

    func build_fireworks() -> void:
        # 爆風で飛び出す短い火の粉。星形のきらめきは使わない。
        firework_trails = ImmediateMesh.new()
        firework_stars = ImmediateMesh.new()
        var shader := Shader.new()
        shader.code = """shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never,depth_test_disabled;
uniform bool textured=false;
void fragment(){
    float mask=textured ? exp(-dot((UV-0.5)*2.0,(UV-0.5)*2.0)*5.0) : 1.0;
    ALBEDO=COLOR.rgb; EMISSION=ALBEDO*1.8;
    ALPHA=COLOR.a*mask;
}"""
        var material := ShaderMaterial.new()
        material.shader = shader
        material.render_priority = 3
        add_firework_mesh(firework_trails, material)
        var stars := material.duplicate() as ShaderMaterial
        stars.set_shader_parameter("textured", true)
        add_firework_mesh(firework_stars, stars)

    func add_firework_mesh(mesh: ImmediateMesh, material: ShaderMaterial) -> void:
        var node := MeshInstance3D.new()
        node.mesh = mesh
        node.material_override = material
        node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
        # 大きく開く火花が生成時の空の境界で間引かれないようにする。
        node.custom_aabb = AABB(Vector3.ONE * -4, Vector3.ONE * 8)
        add_child(node)

    func firework_point(index: int, t: float) -> Vector2:
        var seed_value := fmod(index * 0.618034, 1.0)
        var angle := index * TAU / 28.0 + sin(index * 7.13) * 0.13
        var radius := (1.60 + seed_value * 1.0) * (1.0 - exp(-t * 10.0))
        return Vector2(cos(angle), sin(angle)) * radius + Vector2(0, -1.1 * t * t)

    func firework_vertex(mesh: ImmediateMesh, point: Vector2, tint: Color, uv := Vector2.ZERO) -> void:
        mesh.surface_set_color(tint)
        mesh.surface_set_uv(uv)
        mesh.surface_add_vertex((camera.global_basis.x * point.x + camera.global_basis.y * point.y) * effect_size)

    func draw_fireworks(t: float) -> void:
        firework_trails.clear_surfaces()
        firework_stars.clear_surfaces()
        firework_trails.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
        firework_stars.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
        for i in range(28):
            var u := t
            var seed_value := fmod(i * 0.618034, 1.0)
            var fade := (1.0 - smoothstep(0.12 + seed_value * 0.10, 0.40 + seed_value * 0.16, t)) * minf(u * 70, 1)
            var tint := Color("ffad29") if i % 3 else Color("ff5b0c")
            var head := firework_point(i, u)
            for segment in range(6):
                var a := float(segment) / 6
                var b := float(segment + 1) / 6
                var p := firework_point(i, maxf(0, u - (1 - a) * 0.095))
                var q := firework_point(i, maxf(0, u - (1 - b) * 0.095))
                var side := (q - p).orthogonal().normalized() * (0.022 + seed_value * 0.018) * (1.0 - t)
                var ca := Color(tint, fade * a * a * 0.85)
                var cb := Color(tint.lerp(Color("fff4cf"), b * 0.65), fade * b * b)
                for vertex in [[p - side, ca], [p + side, ca], [q + side, cb], [p - side, ca], [q + side, cb], [q - side, cb]]:
                    firework_vertex(firework_trails, vertex[0], vertex[1])
            var width := (0.06 + seed_value * 0.04) * (1.0 - t)
            var star_tint := Color(tint.lerp(Color("fff8db"), 0.30), fade)
            for corner in [Vector2(-1,-1), Vector2(1,-1), Vector2(1,1), Vector2(-1,-1), Vector2(1,1), Vector2(-1,1)]:
                firework_vertex(firework_stars, head + corner * width, star_tint, (corner + Vector2.ONE) * 0.5)
        firework_trails.surface_end()
        firework_stars.surface_end()

    func prepare(node: Node) -> void:
        # 配置前に作者の自動反復・音・ライトの制御を取り除く。
        node.set_script(null)
        if node is AudioStreamPlayer3D:
            node.autoplay = false
            node.volume_db = -80
        if node is Light3D:
            node.visible = false
        if node is GPUParticles3D:
            var particle := node as GPUParticles3D
            particle.emitting = false
            if kind == "blast" and particle.name != "Fire" and particle.name != "Smoke":
                # 重爆発の破片煙200粒などは使わず、火炎と少量の煙だけを借りる。
                particle.visible = false
                particle.amount = 1
                return
            particle.use_fixed_seed = true
            particle.seed = 429037 + particles.size() * 37
            particle.local_coords = true
            particle.one_shot = kind == "blast"
            particle.lifetime = 0.40 if kind == "blast" else 0.22
            particle.amount = 8 if particle.name == "Smoke" else 18
            particle.preprocess = 0 if kind == "blast" else 0.22
            particle.speed_scale = clock.call().y
            var material := particle.process_material.duplicate(true) as ParticleProcessMaterial
            particle.process_material = material
            if kind == "mirror":
                particle.visible = particle.name == "Flame"
                material.gravity = Vector3.ZERO
                material.direction = Vector3.UP
                material.spread = 18
                material.initial_velocity_min = 1.8
                material.initial_velocity_max = 3.0
                material.scale_min = 0.22
                material.scale_max = 0.34
            elif particle.name == "Smoke":
                particle.amount = 6
                particle.lifetime = 0.66
                particle.explosiveness = 1.0
                material.scale_min = 0.42
                material.scale_max = 0.65
                material.initial_velocity_min = 0.7
                material.initial_velocity_max = 1.3
                material.gravity = Vector3(0, 0.4, 0)
                material.color_ramp = null
                material.color = Color(0.24, 0.19, 0.15, 0.35)
            elif particle.name == "Fire":
                particle.amount = 32
                material.scale_min = 0.32
                material.scale_max = 0.52
                material.direction = Vector3.UP
                material.spread = 180
                material.initial_velocity_min = 1.5
                material.initial_velocity_max = 2.8
                material.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
                material.emission_sphere_radius = 0.22
                material.color = Color.WHITE
                var gradient := Gradient.new()
                gradient.offsets = PackedFloat32Array([0, 0.35, 0.8, 1])
                gradient.colors = PackedColorArray([Color("ffe9ac"), Color("ffa033"), Color("d6581c"), Color(0.3, 0.17, 0.1, 0)])
                var ramp := GradientTexture1D.new()
                ramp.gradient = gradient
                material.color_ramp = ramp
            particles.append(particle)
        for child in node.get_children():
            prepare(child)
