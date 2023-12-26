#version 460 core

layout(location = 0) in vec3 in_position;

out vec3 v_uvw0;

layout(location=0) uniform mat4 vp;

void main()
{
    // HACK: Invert position to make the winding order correct.
    v_uvw0 = -in_position;
    vec4 pos = vec4(-in_position, 1.0) * vp;
    gl_Position = pos.xyww;
}