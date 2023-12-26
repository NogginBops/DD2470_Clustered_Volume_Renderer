#version 460 core

in vec3 v_uvw0;

out vec4 f_color;

layout(binding=0) uniform samplerCube tex_Skybox;

layout(location=15) uniform float u_Exposure;

void main()
{
    f_color = vec4(texture(tex_Skybox, v_uvw0).rgb * u_Exposure, 1.0);
}