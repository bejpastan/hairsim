using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unity.Mathematics;
using UnityEngine;


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

    [SerializeField]
    [Range(0f, 0.5f)]
    float weightThreshold = 0.2f;

    private void Start()
    {
        GetBones();
        characterMesh = skinnedMeshRenderer.sharedMesh;
        GetBonesVertices();
        sdfRotations = new();
        sdfParameters = new();
        sdfPositions = new();

        //for (int i = 0; i < bones.Count; i++)
        //{
        //    CalculateSDF(i);
        //}

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
        if(lastCalc<bones.Count)
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

        for(int i=0; i<boneWeights.Length; i++)
        {
            if (boneWeights[i].weight0 >= weightThreshold)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex0))
                {
                    boneVertices[boneWeights[i].boneIndex0] = new List<UnityEngine.Vector3>();
                }
                boneVertices[boneWeights[i].boneIndex0].Add(characterMesh.vertices[i]);
            }
            if (boneWeights[i].weight1 >= weightThreshold)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex1))
                {
                    boneVertices[boneWeights[i].boneIndex1] = new List<UnityEngine.Vector3>();
                }
                boneVertices[boneWeights[i].boneIndex1].Add(characterMesh.vertices[i]);
            }
            if (boneWeights[i].weight2 >= weightThreshold)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex2))
                {
                    boneVertices[boneWeights[i].boneIndex2] = new List<UnityEngine.Vector3>();
                }
                boneVertices[boneWeights[i].boneIndex2].Add(characterMesh.vertices[i]);
            }
            if (boneWeights[i].weight3 >= weightThreshold)
            {
                if (!boneVertices.ContainsKey(boneWeights[i].boneIndex3))
                {
                    boneVertices[boneWeights[i].boneIndex3] = new List<UnityEngine.Vector3>();
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
        UnityEngine.Vector3 mean = UnityEngine.Vector3.zero;
        float[,] A = new float[boneVertices[boneId].Count, 3];//change X nad Y

        Debug.Log($"vertices count {boneVertices[boneId].Count}");

        foreach (UnityEngine.Vector3 vertex in boneVertices[boneId])
        {
            UnityEngine.Vector3 worldVertex = skinnedMeshRenderer.transform.TransformPoint(vertex);
            mean += worldVertex / boneVertices[boneId].Count;
        }
        foreach (UnityEngine.Vector3 vertex in boneVertices[boneId])
        {
            UnityEngine.Vector3 worldVertex = skinnedMeshRenderer.transform.TransformPoint(vertex);
            UnityEngine.Vector3 centered = worldVertex - mean;
            A[Array.IndexOf(boneVertices[boneId].ToArray(), vertex), 0] = centered.x;
            A[Array.IndexOf(boneVertices[boneId].ToArray(), vertex), 1] = centered.y;
            A[Array.IndexOf(boneVertices[boneId].ToArray(), vertex), 2] = centered.z;
            Debug.DrawLine(mean, worldVertex, Color.green, 2f);
            Debug.LogWarning($"vertex {worldVertex}, centered {centered}");
        }
        

        
        sdfPositions.Add(mean);

        UnityEngine.Vector4[] values = CalcV(A);
        UnityEngine.Vector3[] sVec = new UnityEngine.Vector3[values.Length];//singula vectors
        float[] sVal = new float[values.Length];//singula values

        //sorting values and vectors
        for(int i =0; i<values.Length-1; i++)
        {
            for(int j = i+1; j<values.Length; j++)
            {
                if(values[i].w<values[j].w)
                {
                    var tmpV = values[i];
                    values[i] = values[j];
                    values[j] = tmpV;
                }
            }
        }

        for (int i = 0; i < values.Length; i++)
        {
            sVec[i] = new UnityEngine.Vector3(values[i].x, values[i].y, values[i].z).normalized;
            sVal[i] = values[i].w;
            //Debug.DrawLine(mean, mean + sVec[i] * sVal[i], Color.red, 100f);
        }

        //calculating rotation, and size
        UnityEngine.Quaternion elementRotation = MatrixToQuaternion(sVec);

        //Drawing.DrawSphereoid(mean, new UnityEngine.Vector3(sVal[0], sVal[1], sVal[2]), Color.red, elementRotation, 100f);
        Debug.Log($"Calculating SDF for bone {bones[boneId].boneName} with {boneVertices[boneId].Count} vertices.");
    }

    public UnityEngine.Vector4[] CalcV(float[,] A)
    {
        UnityEngine.Vector4[] eigenVectors = new UnityEngine.Vector4[3];

        //float[,] ATA = MatrixTransposeMultiplication(A);
        float[,] ATA = MatrixATA(A);

        var matrix = Matrix<float>.Build.DenseOfArray(ATA);
        var evd = matrix.Evd();

        Matrix<float> vectors = evd.EigenVectors;

        //I need to divide every row in V by length of UnityEngine.Vector to get covariance matrix
        float a = 1;
        float b = ATA[0, 0] + ATA[1, 1] + ATA[2, 2];
        float c = ATA[0, 0] * ATA[1, 1] + ATA[0, 0] * ATA[2, 2] + ATA[1, 1] * ATA[2, 2] - ATA[0, 1] * ATA[1, 0] - ATA[0, 2] * ATA[2, 0] - ATA[1, 2] * ATA[2, 1];
        float d = ATA[0, 0] * ATA[1, 1] * ATA[2, 2] + ATA[0, 1] * ATA[1, 2] * ATA[2, 0] + ATA[0, 2] * ATA[1, 0] * ATA[2, 1] - ATA[0, 0] * ATA[1, 2] * ATA[2, 1] - ATA[0, 1] * ATA[1, 0] * ATA[2, 2] - ATA[0, 2] * ATA[1, 1] * ATA[2, 0];

        float[] eigenValues = TrignometricCardano(new float[] {a, -b, c, -d });

        //to this moment works fine, I have eigenvalues

        ////now I need to calcualte eigenvectors
        //for (int i = 0; i < 3; i++)
        //{
        //    float[][] V = new float[3][];
        //    for (int j = 0; j < 3; j++)
        //    {
        //        V[j] = new float[3];
        //        for (int k = 0; k < 3; k++)
        //        {
        //            if (j == k)
        //            {
        //                V[j][k] = ATA[j, k] - eigenValues[i];
        //            }
        //            else
        //            {
        //                V[j][k] = ATA[j, k];
        //            }
        //        }
        //        //V[j][3] = 0;
        //    }

        //    Debug.Log($"Matrix for eigenvector {i}: ");
        //    Debug.Log($"{V[0][0]}, {V[0][1]}, {V[0][2]}");
        //    Debug.Log($"{V[1][0]}, {V[1][1]}, {V[1][2]}");
        //    Debug.Log($"{V[2][0]}, {V[2][1]}, {V[2][2]}");

        //    UnityEngine.Vector3 row1 = new UnityEngine.Vector3(V[0][0], V[0][1], V[0][2]);
        //    UnityEngine.Vector3 row2 = new UnityEngine.Vector3(V[1][0], V[1][1], V[1][2]);
        //    UnityEngine.Vector3 row3 = new UnityEngine.Vector3(V[2][0], V[2][1], V[2][2]);

        //    eigenVectors[i] = UnityEngine.Vector3.Cross(row1, row2);
        //    if (eigenVectors[i].magnitude < UnityEngine.Vector3.Cross(row2, row3).magnitude)
        //    {
        //        eigenVectors[i] = UnityEngine.Vector3.Cross(row2, row3);
        //    }
        //    if (eigenVectors[i].magnitude < UnityEngine.Vector3.Cross(row1, row3).magnitude)
        //    {
        //        eigenVectors[i] = UnityEngine.Vector3.Cross(row1, row3);
        //    }



        //    eigenVectors[i] = eigenVectors[i].normalized * eigenValues[i];
        //}
        
        eigenVectors[0] = new UnityEngine.Vector4(vectors.Column(0)[0], vectors.Column(0)[1], vectors.Column(0)[2], (float)evd.EigenValues[0].Real).normalized;
        eigenVectors[1] = new UnityEngine.Vector4(vectors.Column(1)[0], vectors.Column(1)[1], vectors.Column(1)[2], (float)evd.EigenValues[1].Real).normalized;
        eigenVectors[2] = new UnityEngine.Vector4(vectors.Column(2)[0], vectors.Column(2)[1], vectors.Column(2)[2], (float)evd.EigenValues[2].Real).normalized;

        //how to make them to have appropriate lenght

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
            if (Mathf.Abs(A[indexCheck][A[0].Length - 2]) < 0.000001)
            {
                allZero = false;
                break;
            }
        }

        if (allZero)
        {
            A[A.Length - 1][A[0].Length - 1] = 1;
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
            result[i] = (A[i][A[0].Length - 1] / A[i][i]);
        }
        Debug.Log($"Eigenvector solution: {result[0]}, {result[1]}, {result[2]}");
        return result;
    }

    public float[] TrignometricCardano(float[] coeffs)
    {
        //a coeffs[0]
        //b coeffs[1]
        //c coeffs[2]
        //d coeffs[3]
        //(3ac-b^2)/(3a^2)
        float p = (3 * coeffs[0] * coeffs[2] - Mathf.Pow(coeffs[1], 2)) / (3 * Mathf.Pow(coeffs[0], 2));
        float q = (2 * Mathf.Pow(coeffs[1], 3) - 9 * coeffs[0] * coeffs[1] * coeffs[2] + 27 * Mathf.Pow(coeffs[0], 2) * coeffs[3]) / (27*Mathf.Pow(coeffs[0], 3));
        float r = 2*Mathf.Sqrt(-p/3);
        float cosPhi = (Mathf.Sqrt(-3 / p) * 3 * q) / (2 * p);
        float theta = Mathf.Acos(cosPhi)/3;
        float[] results = new float[3];
        float shift = coeffs[1] / (3 * coeffs[0]);
        results[0] = r * Mathf.Cos(theta) - shift;
        results[1] = r * Mathf.Cos(theta + 2 * Mathf.PI / 3) - shift;
        results[2] = r * Mathf.Cos(theta + 4 * Mathf.PI / 3) - shift;
        return results;
    }

    public float[] SolveDuranKorner(float[] coefficients)
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

    private UnityEngine.Quaternion MatrixToQuaternion(UnityEngine.Vector3[] inputMatrix)
    {
        float[,] matrix = new float[3, 3];
        for (int i = 0; i < 3; i++)//iteruje po wierszach
        {
            matrix[i, 0] = inputMatrix[i].x;
            matrix[i, 1] = inputMatrix[i].y;
            matrix[i, 2] = inputMatrix[i].z;
        }
        return MatrixToQuaternion(matrix);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rMtx">input rotation matrix</param>
    /// <returns></returns>
    private UnityEngine.Quaternion MatrixToQuaternion(float[,] rMtx)
    {
        const float eta = 0.25f;

        float trace = rMtx[0, 0] + rMtx[1, 1] + rMtx[2, 2];//this is not always trace
        UnityEngine.Vector4 q = new UnityEngine.Vector4();
        if(trace>eta)
        { 
            q.w = Mathf.Sqrt(1 + trace) / 2;
        }
        else
        {
            q.w = Mathf.Sqrt(
                (Mathf.Pow(rMtx[2, 1] - rMtx[1, 2], 2) + Mathf.Pow(rMtx[0, 2] - rMtx[2, 0], 2) + Mathf.Pow(rMtx[1, 0] - rMtx[0, 1], 2))
                /
                (3 - rMtx[0, 0] - rMtx[1, 1] - rMtx[2, 2])
            );
            q.w /= 2;
        }
        
        trace = rMtx[0,0]-rMtx[2,2]-rMtx[1,1];
        if(trace>eta)
        {
            q.x = Mathf.Sqrt(1 + trace) / 2;
        }
        else
        {
            q.x = Mathf.Sqrt(
                (Mathf.Pow(rMtx[2, 1] - rMtx[1,2], 2) + Mathf.Pow(rMtx[0, 1] + rMtx[1, 0], 2) + Mathf.Pow(rMtx[2,0] - rMtx[0, 2], 2))
                /
                (3 - rMtx[0, 0] + rMtx[1, 1] + rMtx[2, 2])
            );
            q.x /= 2;
        }
        
        trace = rMtx[1,1]-rMtx[2,2]-rMtx[0,0];
        if( trace>eta)
        {
            q.y = Mathf.Sqrt(1 + trace) / 2;
        }
        else
        {
            q.y = Mathf.Sqrt(
                (Mathf.Pow(rMtx[0, 2] - rMtx[2, 0], 2) + Mathf.Pow(rMtx[0, 1] + rMtx[1, 0], 2) + Mathf.Pow(rMtx[1, 2] + rMtx[2, 1], 2))
                /
                (3 + rMtx[0, 1] - rMtx[1, 1] + rMtx[2, 2])
            );
            q.x /= 2;
        }

        trace = -rMtx[0,0]-rMtx[1,1]+rMtx[2,2];
        if(trace>eta)
        {
            q.z = Mathf.Sqrt(1 + trace) / 2;
        }
        else
        {
            q.z = Mathf.Sqrt(
                (Mathf.Pow(rMtx[1, 0] - rMtx[0, 1], 2) + Mathf.Pow(rMtx[2, 0] + rMtx[0, 2], 2) + Mathf.Pow(rMtx[2, 1] + rMtx[1, 2], 2))
                /
                (3 + rMtx[0, 0] + rMtx[1, 1] - rMtx[2, 2])
            );
            q.z /= 2;
        }

        return new UnityEngine.Quaternion(q.x, q.y, q.z, q.w);
    }

    /// <summary>
    /// Calculate mutliplication of given matrix with it's transpose version
    /// </summary>
    /// <param name="A"></param>
    /// <returns></returns>
    public float[,] MatrixATA(float[,] A)
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
}
