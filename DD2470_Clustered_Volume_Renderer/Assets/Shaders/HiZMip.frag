#version 460 core

layout(binding=0) uniform sampler2D tex_input;

in vec2 v_uv0;

out vec4 f_color;

void main()
{
	vec4 samples = textureGather(tex_input, v_uv0, 0);
	float m = min(min(samples.x, samples.y), min(samples.z, samples.w));
	f_color = vec4(m, m, m, m);
}