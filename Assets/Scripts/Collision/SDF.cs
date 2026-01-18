using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class SDF : MonoBehaviour
{
    private Mesh characterMesh;
    [SerializeField]
    Avatar characterRigs;
    [SerializeField]
    SkinnedMeshRenderer skinnedMeshRenderer;

    List<HumanBone> bones;
    Dictionary<int, Transform> boneTransforms;
    Dictionary<int, List<Vector3>> boneVertices;

    List<Vector4> sdfRotations;//rotation in localspace as quaternion
    List<Vector3> sdfPositions;//position in world space
    List<Vector3> sdfOffset;//position in local space
    List<Vector3> sdfParameters;//this are XYZ sizes of elipsoid

    [SerializeField]
    [Range(0f, 0.5f)]
    float weightThreshold = 0.2f;

    private void Start()
    {
        GetBones();
        characterMesh = skinnedMeshRenderer.sharedMesh;
        boneTransforms = new();
        GetBonesVertices();
        sdfRotations = new();
        sdfParameters = new();
        sdfPositions = new();

        for (int i = 0; i < bones.Count; i++)
        {
            CalculateSDF(i);
        }

    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CalcNext();
        }
    }

    int lastCalc = 0;
    private async void CalcNext()
    {
        if (lastCalc < bones.Count)
        {
            CalculateSDF(lastCalc);
            lastCalc++;
        }
        else
        {
            lastCalc = 0;
        }
    }

    private void GetBones()
    {
        bones = characterRigs.humanDescription.human.ToList();
    }

    private void GetBonesVertices()
    {
        BoneWeight[] boneWeights = characterMesh.boneWeights;
        boneVertices = new();

        for (int i = 0; i < boneWeights.Length; i++)
        {
            if (boneWeights[i].weight0 >= weightThreshold)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex0))
                {
                    boneVertices[boneWeights[i].boneIndex0] = new List<Vector3>();
                    boneTransforms.Add(boneWeights[i].boneIndex0, skinnedMeshRenderer.bones[boneWeights[i].boneIndex0].transform);
                }
                boneVertices[boneWeights[i].boneIndex0].Add(characterMesh.vertices[i]);
            }
            if (boneWeights[i].weight1 >= weightThreshold)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex1))
                {
                    boneVertices[boneWeights[i].boneIndex1] = new List<Vector3>();
                    boneTransforms.Add(boneWeights[i].boneIndex1, skinnedMeshRenderer.bones[boneWeights[i].boneIndex1].transform);
                }
                boneVertices[boneWeights[i].boneIndex1].Add(characterMesh.vertices[i]);
            }
            if (boneWeights[i].weight2 >= weightThreshold)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex2))
                {
                    boneVertices[boneWeights[i].boneIndex2] = new List<Vector3>();
                    boneTransforms.Add(boneWeights[i].boneIndex2, skinnedMeshRenderer.bones[boneWeights[i].boneIndex2].transform);

                }
                boneVertices[boneWeights[i].boneIndex2].Add(characterMesh.vertices[i]);
            }
            if (boneWeights[i].weight3 >= weightThreshold)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex3))
                {
                    boneVertices[boneWeights[i].boneIndex3] = new List<Vector3>();
                    boneTransforms.Add(boneWeights[i].boneIndex3, skinnedMeshRenderer.bones[boneWeights[i].boneIndex3].transform);

                }
                boneVertices[boneWeights[i].boneIndex3].Add(characterMesh.vertices[i]);
            }
        }

        //removing duplicates
        foreach (var key in boneVertices.Keys.ToList())
        {
            boneVertices[key] = boneVertices[key].Distinct().ToList();
        }
    }

    private void CalculateSDF(int boneId)
    {
        Vector3 mean = Vector3.zero;

        float[,] A = new float[boneVertices[boneId].Count, 3];//change X nad Y

        Debug.Log($"vertices count {boneVertices[boneId].Count}");

        foreach (Vector3 vertex in boneVertices[boneId])
        {
            Vector3 worldVertex = skinnedMeshRenderer.transform.TransformPoint(vertex);
            mean += worldVertex / boneVertices[boneId].Count;
        }

        foreach (Vector3 vertex in boneVertices[boneId])
        {
            Vector3 worldVertex = skinnedMeshRenderer.transform.TransformPoint(vertex);
            Vector3 centered = worldVertex - mean;
            A[Array.IndexOf(boneVertices[boneId].ToArray(), vertex), 0] = centered.x;
            A[Array.IndexOf(boneVertices[boneId].ToArray(), vertex), 1] = centered.y;
            A[Array.IndexOf(boneVertices[boneId].ToArray(), vertex), 2] = centered.z;
            //Debug.DrawLine(mean, worldVertex, Color.green, 5f);
            //Debug.LogWarning($"vertex {worldVertex}, centered {centered}");
        }
        sdfPositions.Add(mean);

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

        for (int i = 0; i < sVal.Length - 1; i++)
        {
            for (int j = i + 1; j < sVal.Length; j++)
            {
                if (sVal[i] < sVal[j])
                {
                    var tmpV = values[i];
                    values[i] = values[j];
                    values[j] = tmpV;
                    var tmpVec = sVec[i];
                    sVec[i] = sVec[j];
                    sVec[j] = tmpVec;
                }
            }
        }

        sVec[2] = Vector3.Cross(sVec[0], sVec[1]).normalized;//ensuring orthogonality and right hand system

        Debug.Log($"Sizes for bone {bones[boneId].boneName}: {sVal[0]}, {sVal[1]}, {sVal[2]}");
        //for (int i = 0; i < values.Length; i++)
        //{
        //    Debug.DrawLine(mean, mean + sVec[i] * sVal[i], Color.blue, 5f);
        //}

        Quaternion elementRotation = Quaternion.LookRotation(sVec[2], sVec[1]);
        Drawing.DrawSphereoid(mean, new Vector3(sVal[0], sVal[1], sVal[2]) * 2, Color.red, elementRotation, 5f);
        Debug.Log($"Calculating SDF for bone {bones[boneId].boneName} with {boneVertices[boneId].Count} vertices.");
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
}