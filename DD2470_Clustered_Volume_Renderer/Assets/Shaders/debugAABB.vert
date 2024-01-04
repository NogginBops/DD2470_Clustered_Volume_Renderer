#version 460 core

layout(location = 0) in vec3 in_position;
layout(location = 1) in vec3 in_normal;
layout(location = 2) in vec3 in_tangent;
layout(location = 3) in vec2 in_uv0;

layout(location = 4) in vec3 in_AABBMin;
layout(location = 5) in vec3 in_AABBMax;

out vec3 v_position;
out vec3 v_normal;
out vec3 v_tangent;
out vec2 v_uv0;
out flat uint instanceID;

layout(location = 0) uniform mat4 u_mvp;
layout(location = 1) uniform mat4 u_model;
layout(location = 2) uniform mat3 u_normalMatrix;

layout(location = 3) uniform mat4 u_inv_vp;

void main()
{
	vec3 AABBPos = mix(in_AABBMin, in_AABBMax, in_position * 0.5 + 0.5);
	vec4 p = vec4(AABBPos, 1.0) * u_inv_vp;
	p /= p.w;

	gl_Position = p * u_mvp;
	v_position = (p * u_model).xyz;
	v_normal = in_normal * u_normalMatrix;
	v_tangent = in_tangent * u_normalMatrix;
	v_uv0 = in_uv0;
	instanceID = gl_InstanceID;
}