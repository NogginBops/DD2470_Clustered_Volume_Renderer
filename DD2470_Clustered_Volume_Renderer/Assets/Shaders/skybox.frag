#version 460 core

in vec3 v_uvw0;

out vec4 f_color;

layout(binding=0) uniform samplerCube tex_Skybox;

layout(binding=10) uniform sampler3D tex_FogVolume;

layout(location=15) uniform float u_Exposure;

layout(location=21) uniform uvec2 u_ScreenSize;

vec3 CalculateFogOutScatter()
{
	vec2 uv = gl_FragCoord.xy / u_ScreenSize;

	vec4 outScatterAndTransmittance = texture(tex_FogVolume, vec3(uv, gl_FragCoord.z));

	return outScatterAndTransmittance.rgb * outScatterAndTransmittance.a;
}

void main()
{
    f_color = vec4(texture(tex_Skybox, v_uvw0).rgb * u_Exposure, 1.0);
}