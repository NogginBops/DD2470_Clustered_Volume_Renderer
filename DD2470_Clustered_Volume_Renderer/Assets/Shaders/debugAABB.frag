#version 460 core

in vec3 v_position;
in vec3 v_normal;
in vec3 v_tangent;
in vec2 v_uv0;
in flat uint instanceID;

out vec4 f_color;

layout(binding=0) uniform sampler2D tex_Albedo;
layout(binding=1) uniform sampler2D tex_Normal;

layout(location=10) uniform vec3 u_CameraPosition;

struct LightGridCell
{
    uint Offset;
    uint Count;
};

layout(std430, binding=3) readonly buffer LightGrid
{
    LightGridCell ssbo_lightGrid[];
};

const vec3 COLORS[8] = vec3[8](
	vec3(0, 0, 0),
	vec3(1, 0, 0),
	vec3(0, 1, 0),
	vec3(0, 0, 1),
	vec3(1, 1, 0),
	vec3(1, 0, 1),
	vec3(0, 1, 1),
	vec3(1, 1, 1));

void main()
{
	uint count = ssbo_lightGrid[instanceID].Count;
	if (count == 0)
		discard;
	f_color = vec4(COLORS[count % COLORS.length()], 1.0);
}