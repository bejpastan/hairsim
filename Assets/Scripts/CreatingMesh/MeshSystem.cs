using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.HableCurve;

[BurstCompile]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct StrandSpawner : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(entity, new LocalToWorld { Value = float4x4.identity });
    }

    public static Mesh CreateMesh(int segments, float baseSize, float height)
    {
        int vertexPerSegment = 4;
        int rings = segments + 1;
        int vertexCount = rings * vertexPerSegment;

        Mesh newMesh = new Mesh();

        GenerateVertices generateVertices = new GenerateVertices { baseSize = baseSize, segments = segments, vertices = new NativeArray<float3>(vertexPerSegment * (segments+1), Allocator.TempJob) };
        var verticesHandle = generateVertices.Schedule();
        verticesHandle.Complete();

        
        GenerateIndices generateIndices = new GenerateIndices{ segments = segments, indices = new NativeArray<ushort>(segments * 4 * 6, Allocator.TempJob)};
        var indexHandle = generateIndices.Schedule();
        indexHandle.Complete();

        newMesh.SetVertices(generateVertices.vertices);
        newMesh.SetIndices(generateIndices.indices, MeshTopology.Triangles, 0);
        newMesh.RecalculateNormals();

        return newMesh;
    }
}

[BurstCompile]
public struct GenerateIndices : IJob
{
    const int vertexPerSegment = 4;
    public int segments;
    [WriteOnly]
    public NativeArray<ushort> indices;

    public void Execute()
    {
        for (int i = 0; i < segments; i++)
        {
            int vStart = i * vertexPerSegment;
            //iterate over all sides of segment
            for (int side = 0; side < 4; side++)
            {
                int next = (side + 1) % 4;
                ushort v0 = (ushort)(vStart + side);
                ushort v1 = (ushort)(vStart + next);
                ushort v2 = (ushort)(vStart + side + vertexPerSegment);
                ushort v3 = (ushort)(vStart + next + vertexPerSegment);

                indices[i*6] = v0;
                indices[i*6+1] = v2;
                indices[i*6+2] = v1;
                indices[i*6+3] = v1;
                indices[i*6+4] = v2;
                indices[i*6+5] = v3;
            }
        }
    }
}

[BurstCompile]
public struct GenerateVertices : IJob
{
    const int vertexPerSegment = 4;
    [ReadOnly]
    public int segments;
    [ReadOnly]
    public float baseSize;
    [WriteOnly]
    public NativeArray<float3> vertices;

    public void Execute()
    {
        for (int i = 0; i < segments + 1; i++)
        {
            vertices[i * 4 + 1] = new float3(-baseSize / 2, i, -baseSize / 2);
            vertices[i * 4 + 0] = new float3(baseSize / 2, i, -baseSize / 2);
            vertices[i * 4 + 2] = new float3(baseSize / 2, i, baseSize / 2);
            vertices[i * 4 + 3] = new float3(-baseSize / 2, i, baseSize / 2);
        }
    }
}