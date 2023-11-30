#version 460 core

in vec2 v_uv0;

out vec4 f_color;

layout(binding=0) uniform sampler2D tex_HDR;

vec3 aces_approx(vec3 v)
{
    v *= 0.6f;
    float a = 2.51;
    float b = 0.03;
    float c = 2.43;
    float d = 0.59;
    float e = 0.14;
    return clamp((v*(a*v+b))/(v*(c*v+d)+e), 0.0, 1.0);
}

vec3 linear_to_srgb(vec3 linear)
{
    bvec3 cutoff = lessThan(linear, vec3(0.0031308));
    vec3 higher = vec3(1.055)*pow(linear, vec3(1.0/2.4)) - vec3(0.055);
    vec3 lower = linear * vec3(12.92);

    return mix(higher, lower, cutoff);
}

void main()
{
	vec3 scene = texture(tex_HDR, v_uv0).rgb;

	f_color = vec4(linear_to_srgb(aces_approx(scene)), 1.0);
}