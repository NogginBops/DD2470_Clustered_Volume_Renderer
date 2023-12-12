#version 460 core

layout(binding=0) uniform sampler2D tex_input;

in vec2 v_uv0;

out vec4 f_color;

void main()
{
	f_color = texture(tex_input, v_uv0);
}