#version 460 core

in vec3 v_position;
in vec3 v_normal;
in vec3 v_tangent;
in vec2 v_uv0;

out vec4 f_color;

layout(binding=0) uniform sampler2D tex_Albedo;
layout(binding=1) uniform sampler2D tex_Normal;

layout(location=10) uniform vec3 u_CameraPosition;

void main()
{
	f_color = vec4(v_uv0, 0.0, 1.0);
}