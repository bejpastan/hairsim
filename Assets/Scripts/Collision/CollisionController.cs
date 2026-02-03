using System.Data.SqlTypes;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [SerializeField]
    float minSphereSize = 0.5f;
    [SerializeField]
    bool debugMode = false;
    [SerializeField]
    SkinnedMeshRenderer skinnedMeshRenderer;

    #region Compute shader Settings
    [SerializeField]
    ComputeShader collisionsShader;
    int prepareCollisionDataKernel;
    #endregion

    #region Buffers
    GraphicsBuffer sdfRotationsBuffer;
    GraphicsBuffer sdfOffsetesBuffer;
    GraphicsBuffer sdfParametersBuffer;
    GraphicsBuffer originalBonesRotation;

    GraphicsBuffer bonePositionBuffer;
    GraphicsBuffer boneRotationBuffer;
    GraphicsBuffer debugBuffer;
    #endregion

    CollisionGrid grid;
    CharacterSDF sdfData;

    int boneCount;
    int sdfCount;

    #region TMP FOR DEBUG
    [SerializeField]
    int maxSegments = 100;
    [SerializeField]
    float segmentLength = 0.1f;
    float[] capSize = { 2, 2, 2 };
    [SerializeField]
    Transform hairObject;
    #endregion

    private void Start()
    {
        PrepareCollision();
        CalculateCollisions();
        //LogData();
    }

    private void Update()
    {
        CalculateCollisions();
    }

    public void PrepareCollision()
    {
        boneCount = skinnedMeshRenderer.bones.Length;
        prepareCollisionDataKernel = collisionsShader.FindKernel("PreprocessSDF");
        sdfData = new CharacterSDF(boneCount, skinnedMeshRenderer, minSphereSize, debugMode);
        boneCount = sdfData.SDFOffset.Count;
        debugBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount*2, sizeof(float) * 4);
        SetBuffers(sdfData);
        grid = new CollisionGrid(prepareCollisionDataKernel, sdfData.LargestRadius*2, maxSegments, segmentLength,capSize, collisionsShader, hairObject);
    }

    private void SetBuffers(CharacterSDF sdfData)
    {
        sdfRotationsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount, sizeof(float)*4);
        sdfRotationsBuffer.SetData(sdfData.SDFRotations.ToArray());
        originalBonesRotation = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount, sizeof(float)*4);
        originalBonesRotation.SetData(sdfData.OriginBoneRotation.ToArray());
        sdfOffsetesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount, sizeof(float)*3);
        sdfOffsetesBuffer.SetData(sdfData.SDFOffset.ToArray());
        sdfParametersBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount, sizeof(float)*3);
        sdfParametersBuffer.SetData(sdfData.SDFParameters.ToArray());
        sdfCount = sdfOffsetesBuffer.count;

        boneRotationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount, sizeof(float) * 4);
        boneRotationBuffer = sdfData.BoneRotationBuffers;
        bonePositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneCount, sizeof(float) * 3);
        bonePositionBuffer = sdfData.BonePositionsBuffer;
    }

    public async Task CalculateCollisions()
    {
        sdfData.UpdateSDFPositions();
        grid.SetDataToShader();
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_sdfRottationsBuffer", sdfRotationsBuffer);
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_sdfOffsetesBuffer", sdfOffsetesBuffer);
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_sdfParametersBuffer", sdfParametersBuffer);
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_originalBonesRotation", originalBonesRotation);
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_bonePositionBuffer", bonePositionBuffer);
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_boneRotationBuffer", boneRotationBuffer);
        collisionsShader.SetBuffer(prepareCollisionDataKernel, "_DebugBuffer", debugBuffer);
        collisionsShader.SetInt("_sdfCount", boneCount);

        collisionsShader.Dispatch(prepareCollisionDataKernel, Mathf.CeilToInt(sdfCount / 32f), 1, 1);
        for (int i=0; i< sdfCount; i++)
        {
            sdfData.MoveSDF(i);
        }
    }

    private void LogData<T>(GraphicsBuffer bufferToRead)
    {
        if (bufferToRead == null)
        {
            Debug.Log("LogData: buffer is null");
            return;
        }

        int count = bufferToRead.count;
        if (count ==0)
        {
            Debug.Log("LogData: buffer is empty");
            return;
        }

        T[] data = new T[count];
        try
        {
            bufferToRead.GetData(data);
        }
        catch (System.Exception e)
        {
            Debug.Log($"LogData: failed to GetData from buffer: {e.Message}");
            return;
        }

        for (int i =0; i < count; i++)
        {
            object obj = data[i] as object;
            string valueString;

            if (obj is UnityEngine.Vector3 v3)
            {
                valueString = $"{v3.x}, {v3.y}, {v3.z}";
            }
            else if (obj is UnityEngine.Vector4 v4)
            {
                valueString = $"{v4.x}, {v4.y}, {v4.z}, {v4.w}";
            }
            else if (obj is UnityEngine.Quaternion q)
            {
                valueString = $"{q.x}, {q.y}, {q.z}, {q.w}";
            }
            else if (obj is float f)
            {
                valueString = f.ToString();
            }
            else if (obj is double d)
            {
                valueString = d.ToString();
            }
            else if (obj is int n)
            {
                valueString = n.ToString();
            }
            else
            {
                valueString = obj?.ToString() ?? "null";
            }

            Debug.Log($"id:{i}, value:{valueString}");
        }
    }
}