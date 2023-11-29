#version 460 core

in vec3 v_position;
in vec3 v_normal;
in vec3 v_tangent;
in vec2 v_uv0;

out vec4 f_color;

layout(binding=0) uniform sampler2D tex_Albedo;
layout(binding=1) uniform sampler2D tex_Normal;

uniform vec3 u_CameraPosition;

struct Surface
{
	mat3 TangentToWorld;
	vec3 Albedo;
	vec3 Normal;
	vec2 UV0;

	vec3 ViewDirection;
};

struct PointLight
{
	vec4 PositionAndInvSqrRadius;
	vec4 Color;
};

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
	float distance = length(lightDirection);
	float attenuation = getDistanceAtt(lightDirection, light.PositionAndInvSqrRadius.w);
	lightDirection =  normalize(lightDirection);
	vec3 halfwayDirection = normalize(lightDirection + surface.ViewDirection);

	//float attenuation = CalcPointLightAttenuation5(length, light.PositionAndInvSqrRadius.w);
	
	vec3 radiance = light.Color.rgb * attenuation;

	//return abs(lightDirection);
	return vec3(1 - distance / 100, 1 - distance / 100, 1 - distance / 100) * light.Color.rgb * surface.Albedo;
	//return vec3(attenuation, attenuation, attenuation);
	return surface.Albedo * radiance;
}

void main()
{
	vec3 normal = normalize(gl_FrontFacing ? v_normal : -v_normal);
	vec3 tangent = normalize(v_tangent);
	vec3 bitangent = cross(normal, tangent);

	mat3 tangentToWorld = mat3(tangent, bitangent, normal);
	vec3 texNormal = texture(tex_Normal, v_uv0).rgb * 2.0 - 1.0;
	normal = normalize(tangentToWorld * texNormal);

	Surface surface;
	surface.TangentToWorld = tangentToWorld;
	surface.Albedo = texture(tex_Albedo, v_uv0).rgb;
	surface.Normal = normal;
	surface.UV0 = v_uv0;

	surface.ViewDirection = normalize(u_CameraPosition - v_position);
	
	vec3 color = vec3(0, 0, 0);
	color += ShadePointLight(surface, PointLight(vec4(0, 1, 0, 1 / (100 * 100)), vec4(1, 1, 1, 1)));

	f_color = vec4(color, 1.0);
}