#version 460 core

// FIXME: Figure out a good way to not define the binding in this file...
layout(std140, row_major, binding=0) uniform ViewData
{
    mat4 u_InverseProjectionMatrix;
    mat4 u_InverseViewMatrix;
    mat4 PrevViewProjectionMatrix;
    vec4 u_JitterOffsets[8];
    vec3 u_GridSize;
    uvec3 u_CellsPerCluster;
    uvec3 u_ClusterSize;
    uvec2 u_ScreenDimentions;
    float u_zNear;
    float u_zFar;
};

vec3 GetViewPos(vec2 uv, float ndcDepth)
{
    vec4 clipCoord = vec4(uv * 2 - 1, ndcDepth, 1.0);
    vec4 viewCoord = clipCoord * u_InverseProjectionMatrix;
    return viewCoord.xyz / viewCoord.w;
}

vec3 GetWorldPos(vec2 uv, float ndcDepth)
{
    vec3 viewCoord = GetViewPos(uv, ndcDepth);
    return (vec4(viewCoord, 1.0) * u_InverseViewMatrix).xyz;
}

float GetSliceDepth(float zSlice)
{
    return -u_zNear * pow(u_zFar / u_zNear, zSlice / u_GridSize.z);
}

float ViewDepth2NDC(float zView)
{
    float zRange = u_zFar - u_zNear;
    return -((-(u_zFar + u_zNear) / zRange) * zView + ((-2 * u_zFar * u_zNear) / zRange)) / zView;
}

vec3 GetCellPositionWorldSpace(uvec3 gridCoordinate, vec3 cellOffset, out float sceneDepth)
{
    vec2 volumeUV = (gridCoordinate.xy + cellOffset.xy) / u_GridSize.xy;
    sceneDepth = GetSliceDepth(gridCoordinate.z + cellOffset.z);
    float ndcDepth = ViewDepth2NDC(sceneDepth);
    return GetWorldPos(volumeUV, ndcDepth);
    
}

uvec3 CellPositionToCuster(uvec3 gridCoordinate)
{
    return gridCoordinate / u_CellsPerCluster;
}

uint CellPositionToCusterIndex(uvec3 gridCoordinate)
{
    uvec3 cluster = CellPositionToCuster(gridCoordinate);
    return cluster.x +
           cluster.y * u_ClusterSize.x +
           cluster.z * (u_ClusterSize.x * u_ClusterSize.y);
}

vec3 GetVolumeUV(vec3 worldPosition, mat4 worldToClip)
{
	vec4 ndcPosition = vec4(worldPosition, 1.0) * worldToClip;
	ndcPosition.xyz /= ndcPosition.w;
	return vec3(ndcPosition.xyz * 0.5 + 0.5);
}
