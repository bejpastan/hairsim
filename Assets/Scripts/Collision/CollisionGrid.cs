using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

public class CollisionGrid
{
    ComputeShader sdfCollisionShader;
    GraphicsBuffer gridBuffer;
    int size;
    Transform hairObject;
    float cellSize;
    int kernelId;

    #region Variables from SDF
    private Mesh characterMesh;
    [SerializeField]
    Avatar characterRigs;
    [SerializeField]
    SkinnedMeshRenderer skinnedMeshRenderer;
    [Tooltip("If all of SDF sizes are smaller, this part is skipped  in physics simulation")]
    [SerializeField]
    float minSphereSize = 0;


    List<HumanBone> bones;
    Dictionary<int, Transform> boneTransforms;//keys are bone indices in skinned mesh renderer
    List<int> boneArrayMap;//mapping from boneTransforms dictionary key to boneTransformArray index
    TransformAccessArray boneTransformArray;

    Dictionary<int, List<Vector3>> boneVertices;

    //use boneArrayMap indexes
    List<Vector4> sdfRotations;//rotation in localspace as quaternion
    List<Vector4> originBoneRotation;
    List<Vector3> sdfOffset;//position in local space
    List<Vector3> sdfParameters;//this are XYZ sizes of elipsoid


    GraphicsBuffer sdfRottationsBuffer;
    GraphicsBuffer sdfOffsetesBuffer;
    GraphicsBuffer sdfParametersBuffer;
    GraphicsBuffer originalBonesRotation;

    GraphicsBuffer bonePositionBuffer;
    GraphicsBuffer boneRotationBuffer;
    NativeArray<Vector3> bonePositions;
    NativeArray<Vector4> boneRotations;

    float largestRadius = 0;

    [SerializeField]
    bool debugMode = false;

    [SerializeField]
    [Range(0f, 0.5f)]
    float weightThreshold = 0.2f;//TO DO, set this to constant value
    #endregion

    public CollisionGrid(int kernelId, float cellSize, int maxSegments, float segmentLength, float[] capSizes, ComputeShader shader, Transform hairObject)
    {
        this.kernelId = kernelId;
        this.cellSize = cellSize;

        size = Mathf.CeilToInt((Mathf.Max(capSizes) + maxSegments * segmentLength)/cellSize);
        gridBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size * size * size, 64);//support max 64 SDFs
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
        sdfCollisionShader.SetVector("_gridOrigin", new Vector4(hairObject.position.x, hairObject.position.y, hairObject.position.z, 0));
        sdfCollisionShader.SetFloat("_cellSize", cellSize);
    }
}
