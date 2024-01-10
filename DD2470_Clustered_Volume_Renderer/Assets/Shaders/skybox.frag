#version 460 core

in vec3 v_uvw0;

out vec4 f_color;

layout(binding=0) uniform samplerCube tex_Skybox;

layout(binding=10) uniform sampler3D tex_FogVolume;

layout(location=15) uniform float u_Exposure;

layout(location=21) uniform uvec2 u_ScreenSize;
layout(location=22) uniform bool u_RenderFog;


// https://gist.github.com/Fewes/59d2c831672040452aa77da6eaab2234
vec4 tex3DTricubic(sampler3D tex, vec3 coord)
{
    vec3 texSize = textureSize(tex, 0);

    // Shift the coordinate from [0,1] to [-0.5, texSize-0.5]
    vec3 coord_grid = coord * texSize - 0.5;
    vec3 index = floor(coord_grid);
    vec3 fraction = coord_grid - index;
    vec3 one_frac = 1.0 - fraction;

    vec3 w0 = 1.0/6.0 * one_frac*one_frac*one_frac;
    vec3 w1 = 2.0/3.0 - 0.5 * fraction*fraction*(2.0-fraction);
    vec3 w2 = 2.0/3.0 - 0.5 * one_frac*one_frac*(2.0-one_frac);
    vec3 w3 = 1.0/6.0 * fraction*fraction*fraction;

    vec3 g0 = w0 + w1;
    vec3 g1 = w2 + w3;
    vec3 mult = 1.0 / texSize;
    vec3 h0 = mult * ((w1 / g0) - 0.5 + index); //h0 = w1/g0 - 1, move from [-0.5, texSize-0.5] to [0,1]
    vec3 h1 = mult * ((w3 / g1) + 1.5 + index); //h1 = w3/g1 + 1, move from [-0.5, texSize-0.5] to [0,1]

    // Fetch the eight linear interpolations
    // Weighting and fetching is interleaved for performance and stability reasons
    vec4 tex000 = textureLod(tex, vec3(h0), 0);
    vec4 tex100 = textureLod(tex, vec3(h1.x, h0.y, h0.z), 0);
    tex000 = mix(tex100, tex000, g0.x); // Weigh along the x-direction

    vec4 tex010 = textureLod(tex, vec3(h0.x, h1.y, h0.z), 0);
    vec4 tex110 = textureLod(tex, vec3(h1.x, h1.y, h0.z), 0);
    tex010 = mix(tex110, tex010, g0.x); // Weigh along the x-direction
    tex000 = mix(tex010, tex000, g0.y); // Weigh along the y-direction

    vec4 tex001 = textureLod(tex, vec3(h0.x, h0.y, h1.z), 0);
    vec4 tex101 = textureLod(tex, vec3(h1.x, h0.y, h1.z), 0);
    tex001 = mix(tex101, tex001, g0.x); // Weigh along the x-direction

    vec4 tex011 = textureLod(tex, vec3(h0.x, h1.y, h1.z), 0);
    vec4 tex111 = textureLod(tex, vec3(h1), 0);
    tex011 = mix(tex111, tex011, g0.x); // Weigh along the x-direction
    tex001 = mix(tex011, tex001, g0.y); // Weigh along the y-direction

    return mix(tex001, tex000, g0.z); // Weigh along the z-direction
}

vec3 ShadeFogOutScatter(vec3 color)
{
    vec2 uv = gl_FragCoord.xy / u_ScreenSize;

    vec4 outScatterAndTransmittance = tex3DTricubic(tex_FogVolume, vec3(uv, 1));
    //vec4 outScatterAndTransmittance = texture(tex_FogVolume, vec3(uv, 1));

    return color * clamp(outScatterAndTransmittance.aaa, 0.0, 1.0) + outScatterAndTransmittance.rgb;
}

void main()
{
    f_color = vec4(texture(tex_Skybox, v_uvw0).rgb * u_Exposure, 1.0);
    if (u_RenderFog)
    {
        f_color.rgb = ShadeFogOutScatter(f_color.rgb);
    }
}