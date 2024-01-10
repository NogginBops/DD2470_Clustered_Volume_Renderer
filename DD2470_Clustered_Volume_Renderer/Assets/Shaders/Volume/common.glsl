#version 460 core

// FIXME: Figure out a good way to not define the binding in this file...
layout(std140, row_major, binding=0) uniform ViewData
{
    mat4 u_InverseProjectionMatrix;
    mat4 u_InverseViewMatrix;
    mat4 u_PrevViewProjectionMatrix;
    vec4 u_GlobalAlbedoAndGlobalExtinctionScale;
    vec4 u_GlobalEmissiveAndGlobalPhaseG;
    vec4 u_JitterOffsets[8];
    vec3 u_GridSize;
    uvec3 u_CellsPerCluster;
    uvec3 u_ClusterSize;
    uvec2 u_ScreenDimentions;
    float u_zNear;
    float u_zFar;
    float u_zScale;
    float u_zBias;
    float u_HistoryBlend;
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

float SliceToViewDepth(float zSlice)
{
    return -u_zNear * pow(u_zFar / u_zNear, zSlice / u_GridSize.z);
}

float ViewDepthToSlice(float view)
{
    // FIXME: Why log2?
    return max(log2(view) * u_zScale * u_CellsPerCluster.z + u_zBias * u_CellsPerCluster.z, 0.0);
}

float ViewDepthToNDC(float zView)
{
    float zRange = u_zFar - u_zNear;
    return -((-(u_zFar + u_zNear) / zRange) * zView + ((-2 * u_zFar * u_zNear) / zRange)) / zView;
}

float NDCToViewDepth(float ndc)
{
    // FIXME: Is this correct?
    return 2.0 * u_zNear * u_zFar / (u_zFar + u_zNear - ndc * (u_zFar - u_zNear));
}

vec3 GetCellPositionWorldSpace(uvec3 gridCoordinate, vec3 cellOffset, out float sceneDepth)
{
    vec2 volumeUV = (gridCoordinate.xy + cellOffset.xy) / u_GridSize.xy;
    sceneDepth = SliceToViewDepth(gridCoordinate.z + cellOffset.z);
    float ndcDepth = ViewDepthToNDC(sceneDepth);
    return GetWorldPos(volumeUV, ndcDepth);
    
}

vec3 GetCellPositionWorldSpace(uvec3 gridCoordinate, vec3 cellOffset)
{
    float sceneDepth = 0;
    return GetCellPositionWorldSpace(gridCoordinate, cellOffset, sceneDepth);
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
    float view = NDCToViewDepth(ndcPosition.z);
    float sliceUV = ViewDepthToSlice(view) / u_GridSize.z;
    return vec3(ndcPosition.xy * 0.5 + 0.5, sliceUV);
}

float luminance(vec3 color)
{
    return dot(color, vec3(0.299, 0.587, 0.114));
}
