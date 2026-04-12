using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class CollisionGrid
{
    ComputeShader sdfCollisionShader;
    GraphicsBuffer gridBuffer;
    public GraphicsBuffer GridBuffer => gridBuffer;
    int size;
    Transform character;
    float cellSize;
    int kernelId;
    Vector3 origin;
    public Vector3 GridOrigin => origin;
    public float CellSize => cellSize;
    public Vector3 GridSize => size * cellSize * Vector3.one;
    public int Size => size;

    public CollisionGrid(int kernelId, float cellSize, float[] capSizes, ComputeShader shader, Transform character, SkinnedMeshRenderer skinnedMesh)
    {
        this.kernelId = kernelId;
        this.cellSize = cellSize;

        float skinnedMeshSize = Mathf.Max(skinnedMesh.bounds.size.x, skinnedMesh.bounds.size.y, skinnedMesh.bounds.size.z);

        size = Mathf.CeilToInt((Mathf.Max(capSizes) + (skinnedMeshSize))/cellSize);
        this.cellSize = ((Mathf.Max(capSizes) + (skinnedMeshSize)) / size);
        gridBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size * size * size, 8);//support max 64 SDFs
        gridBuffer.SetData(new uint2[size*size*size]);
        this.character = character;
        sdfCollisionShader = shader;

        float shift = (size) * cellSize;
        shift /= 2;
        origin = character.position - (Vector3.one * shift);
        origin.y = character.position.y;
        Drawing.DrawGrid(Vector3.one * cellSize, GridSize, origin, Color.green, 10f);
    }

    /// <summary>
    /// Method to call every time I need to calculate collisions
    /// </summary>
    public void SetDataToShader()
    {
        sdfCollisionShader.SetBuffer(kernelId, "_MaskGrid", gridBuffer);
        sdfCollisionShader.SetVector("_gridSizes", new Vector4(size, size, size, 0));
        float shift = (size) * cellSize;
        shift /= 2;
        origin = character.position - (Vector3.one * shift);
        origin.y = character.position.y;
        sdfCollisionShader.SetVector("_gridOrigin", new Vector4(origin.x, origin.y, origin.z , 0));
        sdfCollisionShader.SetFloat("_cellSize", cellSize);
    }

    public void SetDataToClear()
    {
        sdfCollisionShader.SetBuffer(kernelId, "_MaskGrid", gridBuffer);
    }

    public void ClearBuffer()
    {
        gridBuffer.Dispose();
    }
}
