using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEditor.PlayerSettings;

[BurstCompile]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct StrandSpawner : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(entity, new LocalToWorld { Value = float4x4.identity });
    }

    public static void CreateMesh(int segments, float baseSize, float height, out NativeArray<float3> positions, out NativeArray<ushort> indices)
    {
        int vertexPerSegment = 4;
        int rings = segments + 1;
        int vertexCount = rings * vertexPerSegment;

        //Mesh newMesh = new Mesh();

        positions = new NativeArray<float3>(rings * 4, Allocator.TempJob);
        CreateVertices createVerticesJob = new CreateVertices
        {
            rings = rings,
            baseSize = baseSize,
            positions = positions
        };
        JobHandle jobHandle = createVerticesJob.Schedule();
        jobHandle.Complete();
        positions = createVerticesJob.positions;

        indices = new NativeArray<ushort>(segments * 4 * 6, Allocator.TempJob);
        CreateIndices createIndicesJob = new CreateIndices
        {
            segments = segments,
            indices = indices
        };
        JobHandle indexJobHandle = createIndicesJob.Schedule();
        indexJobHandle.Complete();
        indices = createIndicesJob.indices;

        //newMesh.SetVertices(positions);
        //newMesh.SetIndices(indices, MeshTopology.Triangles, 0);
        //newMesh.RecalculateNormals();

        /*
            Mesh.MeshDataArray meshData = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData data = meshData[0];
            data.SetVertexBufferParams(vertexCount, new VertexAttributeDescriptor(VertexAttribute.Position));
            var positions = data.GetVertexData<float3>();

            for (int i = 0; i < rings; i++)
            {
                float yPos = (height / segments) * i;
                positions[i * 4 + 1] = new float3(-baseSize / 2, yPos, -baseSize / 2);
                positions[i * 4 + 0] = new float3(baseSize / 2, yPos, -baseSize / 2);
                positions[i * 4 + 2] = new float3(baseSize / 2, yPos, baseSize / 2);
                positions[i * 4 + 3] = new float3(-baseSize / 2, yPos, baseSize / 2);
            }

            int quads = segments * 4;
            int indexCount = quads * 6;

            data.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
            var indices = data.GetIndexData<ushort>();

            int index = 0;

            for (int i = 0; i < segments; i++)
            {
                int vStart = i * vertexPerSegment;
                //iterate over all sides of segment
                for(int side = 0; side <4; side++)
                {
                    int next = (side +1) % 4;
                    ushort v0 = (ushort)(vStart + side);
                    ushort v1 = (ushort)(vStart + next);
                    ushort v2 = (ushort)(vStart + side + vertexPerSegment);
                    ushort v3 = (ushort)(vStart + next + vertexPerSegment);

                    indices[index++] = v0;
                    indices[index++] = v2;
                    indices[index++] = v1;
                    indices[index++] = v1;
                    indices[index++] = v2;
                    indices[index++] = v3;
                }
            }

            data.subMeshCount = 1;
            data.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));

            Mesh.ApplyAndDisposeWritableMeshData(meshData, newMesh);
            newMesh.RecalculateNormals();
        */
        //return newMesh;
    }
}

public struct CreateVertices : IJob
{
    public int rings;
    public float baseSize;
    [WriteOnly]
    public NativeArray<float3> positions;

    public void Execute()
    {
        for (int i = 0; i < rings; i++)
        {
            float yPos = i;
            positions[i * 4 + 1] = new float3(-baseSize / 2, yPos, -baseSize / 2);
            positions[i * 4 + 0] = new float3(baseSize / 2, yPos, -baseSize / 2);
            positions[i * 4 + 2] = new float3(baseSize / 2, yPos, baseSize / 2);
            positions[i * 4 + 3] = new float3(-baseSize / 2, yPos, baseSize / 2);
        }
    }
}

public struct CreateIndices : IJob
{
    const int vertexPerSegment = 4;
    public int segments;
    [WriteOnly]
    public NativeArray<ushort> indices;

    public void Execute()
    {
        int index = 0;
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
                indices[index++] = v0;
                indices[index++] = v2;
                indices[index++] = v1;
                indices[index++] = v1;
                indices[index++] = v2;
                indices[index++] = v3;
                //Debug.Log($"v0:{v0} v1:{v1} v2:{v2} v3:{v3}" );
            }
        }
    }
}