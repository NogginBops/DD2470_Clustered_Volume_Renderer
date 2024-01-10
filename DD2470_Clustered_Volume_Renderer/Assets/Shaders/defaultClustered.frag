#version 460 core

in vec3 v_position;
in vec3 v_normal;
in vec4 v_tangent;
in vec2 v_uv0;

out vec4 f_color;

layout(binding=0) uniform sampler2D tex_Albedo;
layout(binding=1) uniform sampler2D tex_Normal;
layout(binding=2) uniform sampler2D tex_RoughnessMetallic;

layout(binding=5) uniform samplerCube tex_Irradiance;
layout(binding=6) uniform samplerCube tex_Radiance;
layout(binding=7) uniform sampler2D tex_brdfLUT;

layout(binding=10) uniform sampler3D tex_FogVolume;

layout(location=10) uniform vec3 u_CameraPosition;
layout(location=11) uniform float u_zNear;
layout(location=12) uniform float u_zFar;
layout(location=13) uniform float u_zScale;
layout(location=14) uniform float u_zBias;

layout(location=15) uniform float u_Exposure;

layout(location=20) uniform uvec3 u_ClusterCount;
layout(location=21) uniform uvec2 u_ScreenSize;
layout(location=22) uniform bool u_RenderFog;
layout(location=23) uniform bool u_UseIBL;

const float PI = 3.14159265359;

const vec3 COLORS[] = vec3[](
	vec3(0, 0, 0),
	vec3(1, 0, 0),
	vec3(0.5, 0, 1),
	vec3(0, 0.5, 1),
	vec3(0, 1, 0),
	vec3(1, 0.5, 0),
	vec3(1, 0, 0.5),
	vec3(0, 0, 1),
	vec3(1, 1, 0),
	vec3(0.5, 1, 0),
	vec3(0, 1, 0.5),
	vec3(1, 0, 1),
	vec3(0, 1, 1),
	vec3(1, 1, 1));

// FIXME: Define this in a single include type file...
struct PointLight
{
	vec4 PositionAndInvSqrRadius;
	vec4 ColorAndSqrRadius;
};

layout(std430, row_major, binding=0) readonly buffer PointLights
{
	PointLight ssbo_lights[];
};

layout(std430, binding=2) readonly buffer LightIndex
{
    uint ssbo_lightIndexList[];
};

struct LightGridCell
{
    uint Offset;
    uint Count;
};

layout(std430, binding=3) readonly buffer LightGrid
{
    LightGridCell ssbo_lightGrid[];
};

struct Surface
{
	mat3 TangentToWorld;
	vec3 Albedo;
	vec3 Normal;
	vec2 UV0;

	float Metallic;
	float Roughness;
	vec3 F0;

	vec3 ViewDirection;
	vec3 ReflectionDirection;
};

float DistributionGGX(vec3 N, vec3 H, float a)
{
	float a2     = a*a;
	float NdotH  = max(dot(N, H), 0.0);
	float NdotH2 = NdotH*NdotH;
	
	float nom    = a2;
	float denom  = (NdotH2 * (a2 - 1.0) + 1.0);
	denom        = PI * denom * denom;
	
	return nom / denom;
}

float GeometrySchlickGGX(float NdotV, float k)
{
	float nom   = NdotV;
	float denom = NdotV * (1.0 - k) + k;
	
	return nom / denom;
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float k)
{
	float NdotV = max(dot(N, V), 0.0);
	float NdotL = max(dot(N, L), 0.0);
	float ggx1 = GeometrySchlickGGX(NdotV, k);
	float ggx2 = GeometrySchlickGGX(NdotL, k);
	
	return ggx1 * ggx2;
}

vec3 FresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

// https://marmosetco.tumblr.com/post/81245981087
// R: Reflection vector
// N: Vertex normal
float HorizonOcclusion(vec3 R, vec3 N)
{
    const float horizonFade = 0.2;
    float horiz = 1.0 + horizonFade * dot(R, N);
    return clamp(horiz * horiz, 0, 1);
}

float SmoothDistanceAttenuation(float squareDistance, float invSquareRadius)
{
	float factor = squareDistance * invSquareRadius;
	float smoothFactor = clamp(1.0 - factor * factor, 0.0, 1.0);
	return smoothFactor * smoothFactor;
}

// https://seblagarde.files.wordpress.com/2015/07/course_notes_moving_frostbite_to_pbr_v32.pdf
float CalcPointLightAttenuation6(float squareDistance, float invSquareRadius)
{
	float attenuation = 1.0 / max(squareDistance, 0.1*0.1);
	attenuation *= SmoothDistanceAttenuation(squareDistance, invSquareRadius);
	return attenuation;
}

vec3 ShadePointLight(Surface surface, PointLight light)
{
	vec3 lightDirection =  light.PositionAndInvSqrRadius.xyz - v_position;
	float distanceSquare = dot(lightDirection, lightDirection);
	float attenuation = CalcPointLightAttenuation6(distanceSquare, light.PositionAndInvSqrRadius.w);
	lightDirection =  normalize(lightDirection);

	vec3 halfwayDirection = normalize(lightDirection + surface.ViewDirection);

	vec3 radiance = light.ColorAndSqrRadius.rgb * attenuation;

	float NDF = DistributionGGX(surface.Normal, halfwayDirection, surface.Roughness);
	float G   = GeometrySmith(surface.Normal, surface.ViewDirection, lightDirection, surface.Roughness);
	vec3 F    = FresnelSchlick(max(dot(halfwayDirection, surface.ViewDirection), 0.0), surface.F0);

	vec3 kS = F;
	vec3 kD = vec3(1.0) - kS;
	kD *= 1.0 - surface.Metallic;

	vec3 numerator    = NDF * G * F;
	float denominator = 4.0 * max(dot(surface.Normal, surface.ViewDirection), 0.0) * max(dot(surface.Normal, lightDirection), 0.0) + 0.0001;
	vec3 specular     = numerator / denominator;

	specular *= HorizonOcclusion(surface.ReflectionDirection, surface.Normal);

	float NdotL = max(dot(surface.Normal, lightDirection), 0.0);
	return (kD * surface.Albedo / PI + specular) * radiance * NdotL;
}

float linearDepth(float depthSample)
{
    float ndcDepth = 2.0 * depthSample - 1.0;
    float linear = 2.0 * u_zNear * u_zFar / (u_zFar + u_zNear - ndcDepth * (u_zFar - u_zNear));
    return linear;
}

vec3 ShadeFogOutScatter(vec3 color)
{
	vec2 uv = gl_FragCoord.xy / u_ScreenSize;

	// FIXME: Why is this log2???
	float zTile = max(log2(linearDepth(gl_FragCoord.z)) * (10*u_zScale) + (10*u_zBias), 0.0);

	// FIXME: Possibly a cubic or quadratic interpolation here...
	vec4 outScatterAndTransmittance = texture(tex_FogVolume, vec3(uv, zTile / 240.0));

	//return vec3(uv, zTile / 240.0);
	return color * outScatterAndTransmittance.aaa + outScatterAndTransmittance.rgb;
}

void main()
{
	vec3 normal = normalize(gl_FrontFacing ? v_normal : -v_normal);
	vec3 tangent = normalize(v_tangent.xyz);
	vec3 bitangent = cross(tangent, normal) * v_tangent.w;

	mat3 tangentToWorld = mat3(tangent, bitangent, normal);
	vec3 texNormal;
#if 1
	texNormal.xy = texture(tex_Normal, v_uv0).rg * 2.0 - 1.0;
	texNormal.z = sqrt(clamp(1 - dot(texNormal.xy, texNormal.xy), 0.0, 1.0));
#else
	texNormal = texture(tex_Normal, v_uv0).rgb * 2.0 - 1.0;
#endif
	texNormal = normalize(texNormal);
	normal = normalize(tangentToWorld * texNormal);

	//f_color = vec4(texNormal, 1.0);
	//return;

	Surface surface;
	surface.TangentToWorld = tangentToWorld;
	surface.Albedo = texture(tex_Albedo, v_uv0).rgb;
	surface.Normal = normal;
	surface.UV0 = v_uv0;

	// FIXME: At the moment we have roughtness in the G channel and metallic in the B channel
	// we might want to use or remove the first channel...?
	vec2 roughnessMetallic = texture(tex_RoughnessMetallic, v_uv0).yz;
	surface.Roughness = clamp(roughnessMetallic.x, 0.0, 1.0);
	surface.Metallic = clamp(roughnessMetallic.y, 0.0, 1.0);
	surface.F0 = mix(vec3(0.04), surface.Albedo, surface.Metallic);

	surface.ViewDirection = normalize(u_CameraPosition - v_position);
	surface.ReflectionDirection = reflect(-surface.ViewDirection, surface.Normal);
	
	// FIXME: Why is this log2?
	uint zTile = uint(max(log2(linearDepth(gl_FragCoord.z)) * u_zScale + u_zBias, 0.0));
	uvec3 tile = uvec3(gl_FragCoord.xy / vec2(1600/16, 900/9), zTile);
	uint tileIndex = tile.x + u_ClusterCount.x * tile.y + (u_ClusterCount.x * u_ClusterCount.y) * tile.z;
	
	uint lightCount = ssbo_lightGrid[tileIndex].Count;
	uint lightOffset = ssbo_lightGrid[tileIndex].Offset;

	//f_color = vec4(COLORS[lightCount % COLORS.length()], 1.0);
	//return;

	vec3 color = vec3(0, 0, 0);
	for (int i = 0; i < lightCount; i++)
	{
		uint lightIndex = ssbo_lightIndexList[lightOffset + i];
		color += ShadePointLight(surface, ssbo_lights[lightIndex]);
	}

	if (u_UseIBL)
	{
		vec3 F = FresnelSchlick(max(dot(surface.Normal, surface.ViewDirection), 0.0), surface.F0);
		vec3 kS = F;
		vec3 kD = 1.0 - kS;
		kD *= 1.0 - surface.Metallic;
		vec3 irradiance = texture(tex_Irradiance, surface.Normal).rgb * u_Exposure;
		vec3 diffuse = irradiance * surface.Albedo;

		const float MAX_REFLECTION_LOD = 9.0;
		vec3 prefilteredColor = textureLod(tex_Radiance, surface.ReflectionDirection,  surface.Roughness * MAX_REFLECTION_LOD).rgb * u_Exposure;
		vec2 brdf  = texture(tex_brdfLUT, vec2(max(dot(surface.Normal, surface.ViewDirection), 0.0), surface.Roughness)).rg;
		vec3 specular = prefilteredColor * (F * brdf.x + brdf.y);

		color += (diffuse * kD + specular);
	}

	if (u_RenderFog)
	{
		color = ShadeFogOutScatter(color);
	}
	
	f_color = vec4(color, 1.0);
}