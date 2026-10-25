shader_type canvas_item;
render_mode unshaded, blend_add;
uniform vec4 tint : source_color = vec4(0.64,0.36,0.96,1.0);
uniform float phase=0.0;
uniform float strength=1.0;

void fragment() {
    vec2 p=UV;
    float h=1.0-p.y;
    float bend=sin(h*8.0-phase*1.3)*0.040+sin(h*17.0+phase)*0.016;
    float left=0.19+bend+h*0.025;
    float right=0.81+bend-h*0.025;
    float width=0.022+0.018*(1.0-h);
    float streams=exp(-pow((p.x-left)/width,2.0))+exp(-pow((p.x-right)/width,2.0));
    float inner=exp(-pow((p.x-(0.34+sin(h*10.0-phase)*0.04))/0.025,2.0))*0.35;
    float curl=0.45+0.55*pow(0.5+0.5*sin(h*19.0-phase*2.0),2.0);
    float fade=smoothstep(0.0,0.13,h)*(1.0-smoothstep(0.65,1.0,h));
    float alpha=(streams+inner)*fade*curl*strength*0.30;
    COLOR=vec4(mix(tint.rgb,vec3(0.8,0.87,1.0),0.15),alpha);
}
