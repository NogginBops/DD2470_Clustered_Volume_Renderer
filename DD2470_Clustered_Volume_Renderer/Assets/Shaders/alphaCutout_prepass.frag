#version 460 core

in vec2 v_uv0;

layout(binding=0) uniform sampler2D tex_Albedo;

layout(location=20) uniform float u_AlphaCutoff;

void main()
{
    vec4 albedo = texture(tex_Albedo, v_uv0).rgba;
    if (albedo.a < u_AlphaCutoff)
        discard;
}