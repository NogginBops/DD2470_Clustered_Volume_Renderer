#version 460 core

layout(location = 0) in vec3 in_position;
layout(location = 1) in vec3 in_normal;
layout(location = 2) in vec3 in_tangent;
layout(location = 3) in vec2 in_uv0;

out vec3 v_position;
out vec3 v_normal;
out vec3 v_tangent;
out vec2 v_uv0;

//layout(location = 0) uniform mat4 u_mvp;
//layout(location = 1) uniform mat4 u_model;
//layout(location = 2) uniform mat3 u_normalMatrix;

struct InstanceData {
	mat4 ModelMatrix;
	mat4 MVP;
	mat4 NormalMatrix;
};

layout(std430, row_major, binding = 1) readonly buffer InstanceBlock {
	InstanceData[] ssbo_instanceData;
};

void main()
{
	InstanceData instanceData = ssbo_instanceData[gl_InstanceID];

	gl_Position = vec4(in_position, 1.0) * instanceData.MVP;
	v_position = (vec4(in_position, 1.0) * instanceData.ModelMatrix).xyz;
	v_normal = in_normal * mat3(instanceData.NormalMatrix);
	v_tangent = in_tangent * mat3(instanceData.NormalMatrix);
	v_uv0 = in_uv0;
}