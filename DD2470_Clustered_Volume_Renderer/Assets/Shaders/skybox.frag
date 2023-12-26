#version 460 core

in vec3 v_uvw0;

out vec4 f_color;

layout(binding=0) uniform samplerCube tex_Skybox;

void main()
{
    f_color = vec4(texture(tex_Skybox, v_uvw0).rgb, 1.0);
}