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
    Transform hairObject;
    float cellSize;
    int kernelId;
    Vector3 origin;
    public Vector3 GridOrigin => origin;
    public float CellSize => cellSize;
    public Vector3 GridSize => size * cellSize * Vector3.one;
    public int Size => size;

    public CollisionGrid(int kernelId, float cellSize, int maxSegments, float segmentLength, float[] capSizes, ComputeShader shader, Transform hairObject)
    {
        this.kernelId = kernelId;
        this.cellSize = cellSize;

        size = Mathf.CeilToInt((Mathf.Max(capSizes) + (maxSegments * segmentLength))/cellSize);
        this.cellSize = ((Mathf.Max(capSizes) + (maxSegments * segmentLength)) / size);
        gridBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size * size * size, 8);//support max 64 SDFs
        gridBuffer.SetData(new uint2[size*size*size]);
        this.hairObject = hairObject;
        sdfCollisionShader = shader;
    }

    /// <summary>
    /// Method to call every time I need to calculate collisions
    /// </summary>
    public void SetDataToShader()
    {
        sdfCollisionShader.SetBuffer(kernelId, "_MaskGrid", gridBuffer);
        sdfCollisionShader.SetVector("_gridSizes", new Vector4(size, size, size, 0));
        float shift = (size-1) * cellSize;
        shift /= 2;
        origin = hairObject.position - (Vector3.one * shift);
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
