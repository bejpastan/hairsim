using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using System;
using System.Numerics;
using MathNet.Numerics.LinearAlgebra;


public class SDF : MonoBehaviour
{
    private Mesh characterMesh;
    [SerializeField]
    Avatar characterRigs;
    [SerializeField]
    SkinnedMeshRenderer skinnedMeshRenderer;

    List<HumanBone> bones;
    Dictionary<int, List<UnityEngine.Vector3>> boneVertices;

    List<UnityEngine.Vector4> sdfRotations;//rotation in localspace as quaternion
    List<UnityEngine.Vector3> sdfPositions;//position in world space
    List<UnityEngine.Vector3> sdfParameters;//this are XYZ sizes of elipsoid

    private void Start()
    {
        GetBones();
        characterMesh = skinnedMeshRenderer.sharedMesh;
        GetBonesVertices();
        sdfRotations = new();
        sdfParameters = new();
        sdfPositions = new();

        CalculateSDF(0);

        //for (int i = 0; i < bones.Count; i++)
        //{
        //    CalculateSDF(i);
        //}

    }

    private void GetBones()
    {
        bones = characterRigs.humanDescription.human.ToList();
    }

    private void GetBonesVertices()
    {
        BoneWeight[] boneWeights = characterMesh.boneWeights;
        boneVertices = new();
        foreach (BoneWeight boneWeight in boneWeights)
        {
            if (boneWeight.weight0 >= 0.5)
            {
                if (!boneVertices.ContainsKey(boneWeight.boneIndex0))
                {
                    boneVertices[boneWeight.boneIndex0] = new List<UnityEngine.Vector3>();
                }
                boneVertices[boneWeight.boneIndex0].Add(characterMesh.vertices[System.Array.IndexOf(boneWeights, boneWeight)]);
                continue;
            }
            if (boneWeight.weight1 >= 0.5)
            {
                if (!boneVertices.ContainsKey(boneWeight.boneIndex1))
                {
                    boneVertices[boneWeight.boneIndex1] = new List<UnityEngine.Vector3>();
                }
                boneVertices[boneWeight.boneIndex1].Add(characterMesh.vertices[System.Array.IndexOf(boneWeights, boneWeight)]);
                continue;
            }
            if (boneWeight.weight2 >= 0.5)
            {
                if (!boneVertices.ContainsKey(boneWeight.boneIndex2))
                {
                    boneVertices[boneWeight.boneIndex2] = new List<UnityEngine.Vector3>();
                }
                boneVertices[boneWeight.boneIndex2].Add(characterMesh.vertices[System.Array.IndexOf(boneWeights, boneWeight)]);
                continue;
            }
            if (boneWeight.weight3 >= 0.5)
            {
                if (!boneVertices.ContainsKey(boneWeight.boneIndex3))
                {
                    boneVertices[boneWeight.boneIndex3] = new List<UnityEngine.Vector3>();
                }
                boneVertices[boneWeight.boneIndex3].Add(characterMesh.vertices[System.Array.IndexOf(boneWeights, boneWeight)]);
                continue;
            }
        }
    }

    private void CalculateSDF(int boneId)
    {
        UnityEngine.Vector3 mean = UnityEngine.Vector3.zero;
        float[,] A = new float[3, boneVertices[boneId].Count];//change X nad Y

        foreach (UnityEngine.Vector3 vertex in boneVertices[boneId])
        {
            UnityEngine.Vector3 worldVertex = skinnedMeshRenderer.transform.TransformPoint(vertex);
            mean += worldVertex / boneVertices[boneId].Count;
        }
        foreach (UnityEngine.Vector3 vertex in boneVertices[boneId])
        {
            UnityEngine.Vector3 worldVertex = skinnedMeshRenderer.transform.TransformPoint(vertex);
            UnityEngine.Vector3 centered = worldVertex - mean;
            A[0, Array.IndexOf(boneVertices[boneId].ToArray(), vertex)] = centered.x;
            A[1, Array.IndexOf(boneVertices[boneId].ToArray(), vertex)] = centered.y;
            A[2, Array.IndexOf(boneVertices[boneId].ToArray(), vertex)] = centered.z;
        }
        sdfPositions.Add(mean);
        Debug.DrawLine(mean, mean + UnityEngine.Vector3.up * 0.5f, Color.red, 100f);
        Debug.DrawLine(mean, mean + UnityEngine.Vector3.up * -0.5f, Color.red, 100f);
        Debug.DrawLine(mean, mean + UnityEngine.Vector3.left * 0.5f, Color.red, 100f);
        Debug.DrawLine(mean, mean + UnityEngine.Vector3.left * -0.5f, Color.red, 100f);
        Debug.DrawLine(mean, mean + UnityEngine.Vector3.forward * 0.5f, Color.red, 100f);
        Debug.DrawLine(mean, mean + UnityEngine.Vector3.forward * -0.5f, Color.red, 100f);

        Debug.Log($"mean {mean}");

        UnityEngine.Vector3[] eigenvectors = CalcV(A);
    }

    private UnityEngine.Vector3[] CalcV(float[,] A)
    {
        float[,] AAtranspose = MatrixTransposeMultiplication(A);

        //I need to divide every row in V by length of UnityEngine.Vector to get covariance matrix
        float a = 1;
        float b = AAtranspose[0, 0] + AAtranspose[1, 1] + AAtranspose[2, 2];
        float c = AAtranspose[0, 0] * AAtranspose[1, 1] + AAtranspose[0, 0] * AAtranspose[2, 2] + AAtranspose[1, 1] * AAtranspose[2, 2] - AAtranspose[0, 1] * AAtranspose[1, 0] - AAtranspose[0, 2] * AAtranspose[2, 0] - AAtranspose[1, 2] * AAtranspose[2, 1];
        float d = AAtranspose[0, 0] * AAtranspose[1, 1] * AAtranspose[2, 2] + AAtranspose[0, 1] * AAtranspose[1, 2] * AAtranspose[2, 0] + AAtranspose[0, 2] * AAtranspose[1, 0] * AAtranspose[2, 1] - AAtranspose[0, 0] * AAtranspose[1, 2] * AAtranspose[2, 1] - AAtranspose[0, 1] * AAtranspose[1, 0] * AAtranspose[2, 2] - AAtranspose[0, 2] * AAtranspose[1, 1] * AAtranspose[2, 0];

        float[] eigenValues = SolveDuranKorner(new float[] { -a, b, -c, d });

        Debug.Log("Eigenvalues: " + eigenValues[0] + ", " + eigenValues[1] + ", " + eigenValues[2]);

        //now I need to calcualte eigenvectors
        UnityEngine.Vector3[] eigenVectors = new UnityEngine.Vector3[3];
        for(int i = 0; i<3; i++)
        {
            float[][] V = new float[3][];
            for(int j = 0; j<3; j++)
            {
                V[j] = new float[4];
                for(int k = 0; k<3; k++)
                {
                    if (j == k)
                    {
                        V[j][k] = AAtranspose[j, k] - eigenValues[i];
                    }
                    else
                    {
                        V[j][k] = AAtranspose[j, k];
                    }
                }
                V[j][3] = 0;
            }

            Debug.Log($"Matrix for eigenvector {i}: ");
            Debug.Log($"{V[0][0]}, {V[0][1]}, {V[0][2]}, {V[0][3]}");
            Debug.Log($"{V[1][0]}, {V[1][1]}, {V[1][2]}, {V[1][3]}");
            Debug.Log($"{V[2][0]}, {V[2][1]}, {V[2][2]}, {V[2][3]}");
        }

        return eigenVectors;
    }

    public float[] GaussElimination(float[][] A)
    {
        float multiplier = 0;

        int subtractedRow = 0;

        for (subtractedRow = 0; subtractedRow < A.Length - 1; subtractedRow++)
        {
            for (int index = subtractedRow + 1; index < A.Length; index++)
            {
                if (A[subtractedRow][subtractedRow] == 0)
                {
                    int swapInd = subtractedRow + 1;
                    while (A[swapInd][subtractedRow] == 0)
                    {
                        swapInd++;
                    }
                    var tmp = A[subtractedRow];
                    A[subtractedRow] = A[swapInd];
                    A[swapInd] = tmp;
                }
                if (A[index][subtractedRow] == 0)
                {
                    continue;
                }
                multiplier = A[index][subtractedRow] / A[subtractedRow][subtractedRow];
                for (int columnIndex = 0; columnIndex < A[subtractedRow].Length; columnIndex++)
                {
                    A[index][columnIndex] -= A[subtractedRow][columnIndex] * multiplier;
                }
            }
        }

        int indexCheck = 0;
        bool allZero = true;
        for (indexCheck = 0; indexCheck < A.Length; indexCheck++)
        {
            if (A[indexCheck][A[0].Length-1] != 0)
            {
                allZero = false;
                break;
            }
        }

        if (allZero)
        {
            A[A.Length-1][A[0].Length-1] = 1;
        }

        int aLen = A.Length;
        subtractedRow = aLen - 1;

        Debug.Log("Matrix befor Lower Echelon: ");
        Debug.Log($"{A[0][0]}, {A[0][1]}, {A[0][2]}, {A[0][3]}");
        Debug.Log($"{A[1][0]}, {A[1][1]}, {A[1][2]}, {A[1][3]}");
        Debug.Log($"{A[2][0]}, {A[2][1]}, {A[2][2]}, {A[2][3]}");

        for (; subtractedRow >= 0; subtractedRow--)
        {
            for (int rowIndex = subtractedRow - 1; rowIndex >= 0; rowIndex--)
            {
                multiplier = A[rowIndex][subtractedRow] / A[subtractedRow][subtractedRow];
                Debug.Log($"multiplier {multiplier} between {rowIndex}, and to subtract {subtractedRow}");
                for (int columnIndex = 0; columnIndex < A[0].Length; columnIndex++)
                {
                    A[rowIndex][columnIndex] -= A[subtractedRow][columnIndex] * multiplier;
                }
            }
        }

        Debug.Log("Matrix after Gauss elimination: ");
        Debug.Log($"{A[0][0]}, {A[0][1]}, {A[0][2]}, {A[0][3]}");
        Debug.Log($"{A[1][0]}, {A[1][1]}, {A[1][2]}, {A[1][3]}");
        Debug.Log($"{A[2][0]}, {A[2][1]}, {A[2][2]}, {A[2][3]}");

        float[] result = new float[A.Length];
        for (int i = 0; i < A.Length; i++)
        {
            result[i] = (A[i][A[0].Length-1] / A[i][i]);
        }

        return result;
    }

    private float[] SolveDuranKorner(float[] coefficients)
    {
        Complex[] roots = new Complex[coefficients.Length - 1];
        int n = coefficients.Length - 1;
        for (int i = 0; i < n; i++)
        {
            float theta = 2 * (float)Math.PI * i / n;
            roots[i] = Complex.FromPolarCoordinates(1, theta);
        }

        float epsilon = 1e-6f;

        float error = float.MaxValue;
        Complex[] newRoots = new Complex[roots.Length];

        //assume that I have polynomial of degree 3
        while (Mathf.Abs(error) > epsilon)
        {
            error = 0f;
            newRoots[0] = roots[0] - (PolynomialValue(roots[0], coefficients) / ((roots[0] - roots[1]) * (roots[0] - roots[2])));
            newRoots[1] = roots[1] - (PolynomialValue(roots[1], coefficients) / ((roots[1] - roots[2]) * (roots[1] - roots[0])));
            newRoots[2] = roots[2] - (PolynomialValue(roots[2], coefficients) / ((roots[2] - roots[0]) * (roots[2] - roots[1])));

            for (int i = 0; i < roots.Length; i++)
            {
                error += (float)Complex.Abs(newRoots[i] - roots[i]);
            }

            roots = newRoots;
        }

        //convert complex roots to float array
        float[] realRoots = new float[roots.Length];
        for (int i = 0; i < roots.Length; i++)
        {
            realRoots[i] = (float)roots[i].Real;
        }
        return realRoots;
    }

    private Complex PolynomialValue(Complex x, float[] coefficients)
    {
        Complex result = 0;
        for (int i = 0; i < coefficients.Length; i++)
        {
            result += coefficients[i] * Complex.Pow(x, coefficients.Length - 1 - i);
        }
        return result;
    }

    private float[,] MatrixTransposeMultiplication(float[,] A)
    {
        float[,] V = new float[A.GetLength(0), A.GetLength(0)];
        for (int x = 0; x < A.GetLength(0); x++)//rows in V matrix
        {
            for (int y = 0; y < A.GetLength(0); y++)//columns in V matrix
            {
                V[x, y] = 0;
                for (int k = 0; k < A.GetLength(1); k++)//sum over columns in A and rows in A
                {
                    V[x, y] += A[x, k] * A[y, k] / A.GetLength(1);
                }
            }
        }
        return V;
    }
}
