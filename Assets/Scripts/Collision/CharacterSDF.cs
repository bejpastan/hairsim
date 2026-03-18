using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;


public class CharacterSDF
{
    float minSphereSize;

    Dictionary<int, Transform> boneTransforms;//keys are bone indices in skinned mesh renderer
    List<int> boneArrayMap;//mapping from boneTransforms dictionary key to boneTransformArray index
    TransformAccessArray boneTransformArray;

    Dictionary<int, List<Vector3>> boneVertices;

    #region SDF DATA
    //use boneArrayMap indexes
    List<Vector4> sdfRotations;//rotation in localspace as quaternion
    public List<Vector4> SDFRotations { get { return sdfRotations; }set { } }
    List<Vector4> originBoneRotation;
    public List<Vector4> OriginBoneRotation { get { return originBoneRotation; } set { } }
    List<Vector3> sdfOffset;//position in local space
    public List<Vector3> SDFOffset { get { return sdfOffset; } set { } }

    List<Vector3> sdfParameters;//this are XYZ sizes of elipsoid
    public List<Vector3> SDFParameters { get { return sdfParameters; } set { } }
    #endregion

    #region transform Data for Shader
    GraphicsBuffer bonePositionBuffer;
    public GraphicsBuffer BonePositionsBuffer { get { return bonePositionBuffer; } private set { } }
    GraphicsBuffer boneRotationBuffer;
    public GraphicsBuffer BoneRotationBuffers { get { return boneRotationBuffer; } private set { } }
    NativeArray<Vector3> bonePositions;
    NativeArray<Vector4> boneRotations;
    #endregion

    private float largestRadius = 0;
    public float LargestRadius { get { return largestRadius; } }
    float BONE_WEIGHT_THRESHOLD = 0.5f;
    bool debugMode;

    public CharacterSDF(int bonesCount, SkinnedMeshRenderer skinnedMeshRenderer, float minSphereSize=0.05f, bool debugMode = false)
    {
        BONE_WEIGHT_THRESHOLD = minSphereSize;
        this.debugMode = debugMode;
        boneTransforms = new();
        GetBonesVertices(skinnedMeshRenderer);

        sdfParameters = new();
        sdfOffset = new();
        sdfRotations = new();
        originBoneRotation = new();
        boneArrayMap = new();
        for (int i = 0; i < bonesCount; i++)
        {
            CalculateSDF(i, skinnedMeshRenderer);
        }
        Debug.Log($"bones left: {boneTransforms.Count}");
        boneVertices.Clear();//remove unneeded data
        boneTransforms = boneTransforms.OrderBy(kvp=> kvp.Key).ToDictionary(pair=> pair.Key, pair=> pair.Value);

        boneTransformArray = new TransformAccessArray(boneTransforms.Values.ToArray());
        #region INITIALIZING BUFFERS
        bonePositions = new NativeArray<Vector3>(boneTransforms.Count, Allocator.Persistent);
        boneRotations = new NativeArray<Vector4>(boneTransforms.Count, Allocator.Persistent);
        bonePositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneTransforms.Count, sizeof(float) * 3);
        boneRotationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneTransforms.Count, sizeof(float) * 4);
        #endregion

    }

    private void GetBonesVertices(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        Mesh characterMesh = skinnedMeshRenderer.sharedMesh;
        BoneWeight[] boneWeights = characterMesh.boneWeights;
        boneVertices = new();

        #region collecting weights
        for (int i = 0; i < boneWeights.Length; i++)
        {
            if (boneWeights[i].weight0 >= BONE_WEIGHT_THRESHOLD)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex0))
                {
                    boneVertices[boneWeights[i].boneIndex0] = new List<Vector3>();
                    boneTransforms.Add(boneWeights[i].boneIndex0, skinnedMeshRenderer.bones[boneWeights[i].boneIndex0].transform);
                }
                boneVertices[boneWeights[i].boneIndex0].Add(characterMesh.vertices[i]);
            }
            if (boneWeights[i].weight1 >= BONE_WEIGHT_THRESHOLD)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex1))
                {
                    boneVertices[boneWeights[i].boneIndex1] = new List<Vector3>();
                    boneTransforms.Add(boneWeights[i].boneIndex1, skinnedMeshRenderer.bones[boneWeights[i].boneIndex1].transform);
                }
                boneVertices[boneWeights[i].boneIndex1].Add(characterMesh.vertices[i]);
            }
            if (boneWeights[i].weight2 >= BONE_WEIGHT_THRESHOLD)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex2))
                {
                    boneVertices[boneWeights[i].boneIndex2] = new List<Vector3>();
                    boneTransforms.Add(boneWeights[i].boneIndex2, skinnedMeshRenderer.bones[boneWeights[i].boneIndex2].transform);

                }
                boneVertices[boneWeights[i].boneIndex2].Add(characterMesh.vertices[i]);
            }
            if (boneWeights[i].weight3 >= BONE_WEIGHT_THRESHOLD)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex3))
                {
                    boneVertices[boneWeights[i].boneIndex3] = new List<Vector3>();
                    boneTransforms.Add(boneWeights[i].boneIndex3, skinnedMeshRenderer.bones[boneWeights[i].boneIndex3].transform);

                }
                boneVertices[boneWeights[i].boneIndex3].Add(characterMesh.vertices[i]);
            }
        }
        #endregion

        //removing duplicates
        foreach (var key in boneVertices.Keys.ToList())
        {
            boneVertices[key] = boneVertices[key].Distinct().ToList();
        }
    }

    private void CalculateSDF(int boneId, SkinnedMeshRenderer skinnedMeshRenderer)
    {
        Vector3 mean = Vector3.zero;
        boneVertices.TryGetValue(boneId, out List<Vector3> vertices);

        float[,] A = new float[vertices.Count, 3];//change X nad Y

        foreach (Vector3 vertex in vertices)
        {
            Vector3 worldVertex = skinnedMeshRenderer.transform.TransformPoint(vertex);
            mean += worldVertex / vertices.Count;
        }

        foreach (Vector3 vertex in vertices)
        {
            Vector3 worldVertex = skinnedMeshRenderer.transform.TransformPoint(vertex);
            Vector3 centered = worldVertex - mean;
            A[Array.IndexOf(vertices.ToArray(), vertex), 0] = centered.x;
            A[Array.IndexOf(vertices.ToArray(), vertex), 1] = centered.y;
            A[Array.IndexOf(vertices.ToArray(), vertex), 2] = centered.z;
            if(debugMode)
            {
                Debug.DrawLine(mean, worldVertex, Color.green, 3f);
                Debug.Log($"vertex {worldVertex}, centered {centered}");
            }
        }

        Vector4[] values = CalcV(A);
        Vector3[] sVec = new Vector3[values.Length];//singula vectors
        float[] sVal = new float[values.Length];//singula values

        for(int i=0;i< values.Length; i++)
        {
            for(int j=i+1;j< values.Length; j++)
            {
                if(values[i].w < values[j].w)
                {
                    var tmp = values[i];
                    values[i] = values[j];
                    values[j] = tmp;
                }
            }
        }

        for (int i = 0; i < values.Length; i++)
        {
            sVec[i] = new Vector3(values[i].x, values[i].y, values[i].z).normalized;
        }
        sVal = Sizes(A, sVec);

        bool isGreater = false;
        for (int i = 0; i < sVal.Length; i++)
        {
            if(sVal[i] > BONE_WEIGHT_THRESHOLD)
            {
                isGreater = true;
                break;
            }
        }
        if(!isGreater)
        {
            boneTransforms.Remove(boneId);
            return;
        }

        if (debugMode)
        {
            Debug.DrawRay(mean, sVec[0], Color.red, 3f);
            Debug.DrawRay(mean, sVec[1], Color.green, 3f);
            Debug.DrawRay(mean, sVec[2], Color.blue, 3f);
        }

        sVec[2] = Vector3.Cross(sVec[0], sVec[1]).normalized;//ensuring orthogonality and right hand system
        Quaternion elementRotation = Quaternion.LookRotation(sVec[2], sVec[1]);
        if(debugMode)
        {
            Drawing.DrawSphereoid(mean, new Vector3(sVal[0], sVal[1], sVal[2]) * 2, Color.red, elementRotation, 3);
        }
        //set offset, rotation and parameters
        boneTransforms.TryGetValue(boneId, out Transform boneTransform);
        sdfOffset.Add(mean - boneTransform.position);
        Quaternion relativeRotation = Quaternion.Inverse(boneTransform.rotation) * elementRotation;
        sdfRotations.Add(new Vector4(relativeRotation.x, relativeRotation.y, relativeRotation.z, relativeRotation.w));
        sdfParameters.Add(new Vector3(sVal[0], sVal[1], sVal[2]));
        originBoneRotation.Add(new Vector4(boneTransform.rotation.x, boneTransform.rotation.y, boneTransform.rotation.z, boneTransform.rotation.w));
        boneArrayMap.Add(boneId);
        largestRadius = Mathf.Max(Mathf.Max(sVal),largestRadius);
    }

    public float[] Sizes(float[,] A, Vector3[] basis)
    {
        float[] sizes = new float[3];
        float[,] basisF = new float[3, 3] {
            { basis[0].x, basis[1].x, basis[2].x },
            { basis[0].y, basis[1].y, basis[2].y },
            { basis[0].z, basis[1].z, basis[2].z }
        };

        int n = A.GetLength(1);
        int m = A.GetLength(0);

        for (int i = 0; i < m; i++)
        {
            Vector3 point = new Vector3(A[i, 0], A[i, 1], A[i, 2]);
            Vector3 projected = TranslateFromWorld(basisF, point);
            for (int k = 0; k < 3; k++)
            {
                sizes[k] = Mathf.Max(Mathf.Abs(projected[k]), sizes[k]);
            }
        }

        return sizes;
    }

    public Vector4[] CalcV(float[,] A)
    {
        Vector4[] eigenVectors = new Vector4[3];

        //float[,] ATA = MatrixTransposeMultiplication(A);
        float[,] ATA = MatrixATA(A);

        var matrix = Matrix<float>.Build.DenseOfArray(ATA);
        var evd = matrix.Evd();

        Matrix<float> vectors = evd.EigenVectors;

        eigenVectors[0] = new Vector4(vectors.Column(0)[0], vectors.Column(0)[1], vectors.Column(0)[2], (float)evd.EigenValues[0].Real);
        eigenVectors[1] = new Vector4(vectors.Column(1)[0], vectors.Column(1)[1], vectors.Column(1)[2], (float)evd.EigenValues[1].Real);
        eigenVectors[2] = new Vector4(vectors.Column(2)[0], vectors.Column(2)[1], vectors.Column(2)[2], (float)evd.EigenValues[2].Real);

        return eigenVectors;
    }

    /// <summary>
    /// Calculate mutliplication of given matrix with it's transpose version
    /// </summary>
    /// <param name="A"></param>
    /// <returns></returns>
    private float[,] MatrixATA(float[,] A)
    {
        int n = A.GetLength(1);
        int m = A.GetLength(0);

        float[,] result = new float[n, n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float sum = 0;
                for (int i = 0; i < m; i++)
                {
                    sum += A[i, x] * A[i, y];
                }
                result[x, y] = sum;
            }
        }
        return result;
    }

    private Vector3 TranslateFromWorld(float[,] newBasis, Vector3 point)
    {
        Vector3 newPoint = new Vector3();
        Matrix<float> matrix = Matrix<float>.Build.DenseOfArray(newBasis);
        matrix = matrix.Inverse();
        Matrix<float> pointMtx = Matrix<float>.Build.DenseOfArray(new float[,] { { point.x }, { point.y }, { point.z } });
        Matrix<float> result = matrix * pointMtx;
        newPoint = new Vector3(result[0, 0], result[1, 0], result[2, 0]);
        return newPoint;
    }

    Vector3 maxvalue = Vector3.zero;
    Vector3 minvalue = Vector3.zero * -1000;
    //only for debugging purposes
    public void MoveSDF(int boneId)
    {
        Transform boneTransform = boneTransforms[boneArrayMap[boneId]];
        //I should move this to shader
        Quaternion relativeRotation = new Quaternion(sdfRotations[boneId].x, sdfRotations[boneId].y, sdfRotations[boneId].z, sdfRotations[boneId].w);
        Quaternion elementRotation = boneTransform.rotation * relativeRotation;//this is new rotation
        //Quaternion newBoneRotation = new Quaternion(boneRotations[boneId].x, boneRotations[boneId].y, boneRotations[boneId].z, boneRotations[boneId].w);
        //Quaternion elementRotation = newBoneRotation * relativeRotation;//this is new rotation
        Quaternion origineBoneRotation = new Quaternion(originBoneRotation[boneId].x, originBoneRotation[boneId].y, originBoneRotation[boneId].z, originBoneRotation[boneId].w);
        Quaternion rotationChange = boneTransform.rotation * Quaternion.Inverse(origineBoneRotation);
        Vector3 newTranslation = rotationChange * sdfOffset[boneId];
        //Vector3 newPos2 = bonePositions[boneId] + newTranslation;
        Vector3 newPos = boneTransform.position + newTranslation;
        Drawing.DrawSphereoid(newPos, sdfParameters[boneId] * 2f, Color.green, elementRotation, 0.016f);
        maxvalue = new Vector3(Mathf.Max(maxvalue.x, newPos.x), Mathf.Max(maxvalue.y, newPos.y), Mathf.Max(maxvalue.z, newPos.z));
        minvalue = new Vector3(Mathf.Min(minvalue.x, newPos.x), Mathf.Min(minvalue.y, newPos.y), Mathf.Min(minvalue.z, newPos.z));
        //Debug.LogWarning($"max value {maxvalue.x}, {maxvalue.y}, {maxvalue.z} \n min value {minvalue.x}, {minvalue.y}, {minvalue.z}");
        //log newPos and newRot
        //Debug.Log($"SDF {boneId} new position: {newPos.x},{newPos.y},{newPos.z}\nnew rotation: {rotationChange.x}, {rotationChange.y}, {rotationChange.z}, {rotationChange.w}");
    }

    /// <summary>
    /// Update data for transforms
    /// </summary>
    public void UpdateSDFPositions()
    {
        var job = new CollectingTransformData { bonesPositions = bonePositions, bonesRotations = boneRotations };
        var handle = job.Schedule(boneTransformArray);
        handle.Complete();
        bonePositionBuffer.SetData(bonePositions);
        boneRotationBuffer.SetData(boneRotations);
    }
}