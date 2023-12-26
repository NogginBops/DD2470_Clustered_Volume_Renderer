#version 460 core

in vec3 v_position;
in vec3 v_normal;
in vec3 v_tangent;
in vec2 v_uv0;

out vec4 f_color;

layout(binding=0) uniform sampler2D tex_Albedo;
layout(binding=1) uniform sampler2D tex_Normal;

layout(binding=5) uniform samplerCube tex_Irradiance;

layout(location=10) uniform vec3 u_CameraPosition;

const float PI = 3.14159265359;

struct PointLight
{
	vec4 PositionAndInvSqrRadius;
	vec4 Color;
};

layout(std430, row_major, binding=0) readonly buffer PointLights
{
	PointLight u_lights[];
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

float smoothDistanceAtt(float squaredDistance, float invSqrAttRadius)
{
	float factor = squaredDistance * invSqrAttRadius;
	float smoothFactor = clamp(1.0 - factor * factor, 0, 1);
	return smoothFactor * smoothFactor;
}

float getDistanceAtt(vec3 unormalizedLightVector, float invSqrAttRadius)
{
	float sqrDist = dot(unormalizedLightVector, unormalizedLightVector);
	float attenuation = 1.0 / (max(sqrDist, 0.01 * 0.01));
	attenuation *= smoothDistanceAtt(sqrDist, invSqrAttRadius);

	return attenuation;
}


// https://seblagarde.files.wordpress.com/2015/07/course_notes_moving_frostbite_to_pbr_v32.pdf
float CalcPointLightAttenuation5(float distance, float invRadius)
{
    float factor = clamp(1 - pow(distance * invRadius, 4), 0, 1);
    return (factor * factor) / (distance * distance);
}

vec3 ShadePointLight(Surface surface, PointLight light)
{
	vec3 lightDirection =  light.PositionAndInvSqrRadius.xyz - v_position;
	//float distance = length(lightDirection);
	float distanceSqr = dot(lightDirection, lightDirection);
	//float attenuation = getDistanceAtt(lightDirection, light.PositionAndInvSqrRadius.w);
	lightDirection =  normalize(lightDirection);
	vec3 halfwayDirection = normalize(lightDirection + surface.ViewDirection);

	float attenuation = 1.0 / distanceSqr;

	//return (light.Color.rgb * surface.Albedo) / distanceSqr;

	//float attenuation = CalcPointLightAttenuation5(length, light.PositionAndInvSqrRadius.w);
	
	vec3 radiance = light.Color.rgb * attenuation;

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

	//return halfwayDirection * attenuation;
	//return abs(lightDirection);
	//return vec3(1 - distance / 100, 1 - distance / 100, 1 - distance / 100) * light.Color.rgb * surface.Albedo;
	//return vec3(attenuation, attenuation, attenuation);
	//return surface.Albedo * radiance;
}

void main()
{
	vec3 normal = normalize(gl_FrontFacing ? v_normal : -v_normal);
	vec3 tangent = normalize(v_tangent);
	vec3 bitangent = cross(normal, tangent);

	mat3 tangentToWorld = mat3(tangent, bitangent, normal);
	vec3 texNormal;
	texNormal.xy = texture(tex_Normal, v_uv0).rg * 2.0 - 1.0;
	texNormal.z = sqrt(1 - dot(texNormal.xy, texNormal.xy));
	normal = normalize(tangentToWorld * texNormal);

	f_color = vec4(texNormal.xy, 0.0, 1.0);
	return;

	//if (any(isnan(normal)))
	//{
		//f_color= vec4(1,0,1,1);
	//	return;
	//}else{
		f_color = vec4(normal, 1.0);
		return;
	//}

	Surface surface;
	surface.TangentToWorld = tangentToWorld;
	surface.Albedo = texture(tex_Albedo, v_uv0).rgb;
	surface.Normal = normal;
	surface.UV0 = v_uv0;

	// FIXME: Get metallic and roughness from texture.
	surface.Metallic = 0.0;
	surface.Roughness = 0.2;
	surface.F0 = mix(vec3(0.04), surface.Albedo, surface.Metallic);

	surface.ViewDirection = normalize(u_CameraPosition - v_position);
	surface.ReflectionDirection = reflect(-surface.ViewDirection, surface.Normal);
	
	vec3 color = vec3(0, 0, 0);
	for (int i = 0; i < u_lights.length(); i++)
	{
		//color += ShadePointLight(surface, u_lights[i]);
	}

	{
		vec3 kS = FresnelSchlick(max(dot(surface.Normal, surface.ViewDirection), 0.0), surface.F0);
		vec3 kD = 1.0 - kS;
		kD *= 1.0 - surface.Metallic;
		vec3 irradiance = texture(tex_Irradiance, surface.Normal).rgb;
		vec3 diffuse = irradiance * surface.Albedo;

		color += diffuse * kD;
	}
	
	f_color = vec4(color, 1.0);
	//f_color = vec4(texNormal, 1.0);
}