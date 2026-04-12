using System;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class CollisionController : MonoBehaviour
{
    SkinnedMeshRenderer skinnedMeshRenderer;

    #region Compute shader Settings
    ComputeShader collisionsShader;
    int prepareCollisionDataKernel;
    int clearMaskKernel;
    int calcCollisionKernel;
    int clearCollisionKernel;
    #endregion

    #region Buffers
    GraphicsBuffer sdfDataBuffer; //+0 rotation, +1 offset, +2 parameters

    GraphicsBuffer originalBonesRotation;

    GraphicsBuffer bonePositionBuffer;
    GraphicsBuffer boneRotationBuffer;

    #endregion

    CollisionGrid grid;
    CharacterSDF sdfData;

    int boneCount;
    int sdfCount;

    float[] capSize = { 2, 2, 2 };

    public void PrepareCollision(float[] capSizes, Transform hairObject, SkinnedMeshRenderer skinnedMesh, ComputeShader collisionShader, bool debugMode, float minSphereSize)
    {
        this.collisionsShader = collisionShader;
        this.skinnedMeshRenderer =skinnedMesh;
        this.capSize = capSizes;
        boneCount = skinnedMeshRenderer.bones.Length;

        prepareCollisionDataKernel  = collisionsShader.FindKernel("PreprocessSDF");
        clearMaskKernel             = collisionsShader.FindKernel("ClearMask");
        calcCollisionKernel         = collisionsShader.FindKernel("CalcCollisions");
        clearCollisionKernel        = collisionsShader.FindKernel("ClearCollisions");

        sdfData = new CharacterSDF(boneCount, skinnedMeshRenderer, minSphereSize, debugMode);
        boneCount = sdfData.SDFOffset.Count;
        SetBuffers(sdfData);

        grid = new CollisionGrid(prepareCollisionDataKernel, sdfData.LargestRadius*2,capSize, collisionsShader, transform, skinnedMesh);
    }

    private void SetBuffers(CharacterSDF sdfData)
    {
        sdfCount = sdfData.SDFOffset.Count;

        sdfDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sdfCount*3, sizeof(float)*4);
        float4[] sdfDataArray = new float4[sdfCount*3];
        for(int i=0; i < sdfCount; i++)
        {
            sdfDataArray[i*3] = sdfData.SDFRotations[i];
            sdfDataArray[i*3+1] = new float4(sdfData.SDFOffset[i], 0);
            sdfDataArray[i*3+2] = new float4(sdfData.SDFParameters[i], 0);
        }
        sdfDataBuffer.SetData(sdfDataArray);
        sdfDataArray = null;

        originalBonesRotation = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount, sizeof(float) * 4);
        originalBonesRotation.SetData(sdfData.OriginBoneRotation.ToArray());

        boneRotationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount, sizeof(float) * 4);
        boneRotationBuffer = sdfData.BoneRotationBuffers;
        bonePositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount, sizeof(float) * 3);
        bonePositionBuffer = sdfData.BonePositionsBuffer;
    }

    public void CalculateCollisions(GraphicsBuffer pointsData, GraphicsBuffer collisionConstraints, int pointsCount, float strandRadius, int strandCount)
    {
        sdfData.UpdateSDFPositions();
        grid.SetDataToShader();
        collisionsShader.SetInt("_sdfCount", boneCount);
        collisionsShader.SetInt("_pointCount", pointsCount);
        collisionsShader.SetInt("_strands", strandCount);
        collisionsShader.SetInt("_segmentsCount", pointsCount / strandCount);
        collisionsShader.SetFloat("_strandRadius", strandRadius);

        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_sdfData", sdfDataBuffer);
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_originalBonesRotation", originalBonesRotation);
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_bonePositionBuffer", bonePositionBuffer);
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_boneRotationBuffer", boneRotationBuffer);

        collisionsShader.Dispatch(prepareCollisionDataKernel, Mathf.CeilToInt(sdfCount / 32f), 1, 1);

        collisionsShader.SetBuffer(calcCollisionKernel, "_sdfData", sdfDataBuffer);
        collisionsShader.SetBuffer(calcCollisionKernel, "_originalBonesRotation", originalBonesRotation);
        collisionsShader.SetBuffer(calcCollisionKernel, "_bonePositionBuffer", bonePositionBuffer);
        collisionsShader.SetBuffer(calcCollisionKernel, "_boneRotationBuffer", boneRotationBuffer);
        collisionsShader.SetBuffer(calcCollisionKernel, "_MaskGrid", grid.GridBuffer);
        collisionsShader.SetBuffer(calcCollisionKernel, "_collisionConstraints", collisionConstraints);
        collisionsShader.SetBuffer(calcCollisionKernel, "_PointData", pointsData);

        collisionsShader.Dispatch(calcCollisionKernel, Mathf.CeilToInt(strandCount / 32f), 1, 1);

        grid.SetDataToClear();

        //for (int i = 0; i < sdfCount; i++)
        //{
        //    sdfData.MoveSDF(i);
        //}
        collisionsShader.SetBuffer(clearMaskKernel, "_MaskGrid", grid.GridBuffer);
        collisionsShader.Dispatch(clearMaskKernel, Mathf.CeilToInt(Mathf.Pow(grid.Size, 3) / 32f), 1, 1);
    }

    public void ClearBuffers()
    {
        grid.ClearBuffer();
        originalBonesRotation.Dispose();
        bonePositionBuffer.Dispose();
        boneRotationBuffer.Dispose();
        sdfDataBuffer.Dispose();
    }


    public void ClearCollision(GraphicsBuffer collisionBuffer)
    {
        collisionsShader.SetBuffer(clearCollisionKernel, "_collisionConstraints", collisionBuffer);
        collisionsShader.Dispatch(clearCollisionKernel, Mathf.CeilToInt(collisionBuffer.count / 32f), 1, 1);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="itemSize">in bytes</param>
    private void LogBinary(GraphicsBuffer buffer, int itemSize)
    {
        var data = new byte[buffer.count*itemSize];
        try
        {
            buffer.GetData(data);
        }
        catch (System.Exception e)
        {
            Debug.Log($"LogBinary: failed to GetData from buffer: {e.Message}");
            return;
        }
        for (int i = 0; i < data.Length; i+=itemSize)
        {
            string logString = $"id:{i}, ";
            bool haveOne = false;
            for (int j = 0; j < itemSize; j++)
            {

                logString += $"{Convert.ToString(data[i + j], 2)}, ";
                haveOne = haveOne || data[i + j] > 0;
            }

            int index = i/itemSize;
            int x = index % grid.Size;
            int y = ((index-x)/grid.Size)%grid.Size;
            int z = ((index-x - (y*grid.Size))/(grid.Size*grid.Size));
            Vector3 translation = new(x*grid.CellSize, y*grid.CellSize, z*grid.CellSize);
            if(haveOne)
            {
                //Debug.Log(logString);
                Drawing.DrawCube(grid.GridOrigin + translation, grid.CellSize * Vector3.one, Color.red, 0.016f);
            }
            else
            {
                //Drawing.DrawCube(grid.GridOrigin + translation, grid.CellSize * Vector3.one, Color.green, 2);
            }
        }

    }

    private void LogData<T>(GraphicsBuffer buffer)
    {
        var data = new T[buffer.count];
        try
        {
            buffer.GetData(data);
        }
        catch (System.Exception e)
        {
            Debug.Log($"LogData: failed to GetData from buffer: {e.Message}");
            return;
        }
        for (int i = 0; i < data.Length; i++)
        {
            Debug.Log($"id:{i}, data:{data[i]}");
        }
    }
}