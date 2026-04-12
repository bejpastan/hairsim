using UnityEngine;
using Unity.Mathematics;

public class HairController : MonoBehaviour
{
    public int ITERATION_COUNT = 10;
    [Range(0,1)]
    public float collisionStiffnes;

    //TO DO get this automatically
    [SerializeField]
    ComputeShader strandMeshBuilder;
    [SerializeField]
    ComputeShader strandPositionShader;
    [SerializeField]
    ComputeShader collisionShader;
    [SerializeField]
    SkinnedMeshRenderer meshRenderer;


    [SerializeField]
    int segments;
    [SerializeField]
    int maxSegments = 100;
    int previousSegments = 0;
    [SerializeField]
    float strandRadius = 0.1f;

    [SerializeField]
    int strandCount = 20;// add validating to be multiple of lines

    [Header("PDB")]
    const float VELOCITY_DUMPING = 0.98f;
    [Header("Distance Constraints")]
    [SerializeField]
    [Range(0,1)]
    float stiffness = 0.9f;
    [SerializeField]
    float strandLength = 1.0f;
    [Header("Bend constraint")]
    [SerializeField]
    [Range(0, 1)]
    float bendStiffness = 0.5f;


    [Header("Cap settings")]
    [SerializeField]
    int lines = 4;
    [SerializeField]
    float capHeight = 2.0f;
    [SerializeField]
    float capRadius = 1.0f;
    [SerializeField]
    float zRandRange = 1.0f;
    [SerializeField]
    float xRandRange = 1.0f;

    [SerializeField]
    float leftCut = 0;
    [SerializeField]
    float rightCut = 0;
    [SerializeField]
    float backCut = 0;
    [SerializeField]
    float frontCut = 0;

    [Header("Collision settings")]
    [SerializeField]
    float minSphereSize = 0;

    int verticesKernelId;
    int indicesKernelId;
    int closingIndicesKernelId;

    GraphicsBuffer vertexBuffer;
    GraphicsBuffer indexBuffer;

    [SerializeField]
    Material hairMat;

    GraphicsBuffer cmdBuffer;
    GraphicsBuffer.IndirectDrawArgs[] cmdArgsBuffer;
    const int COMMAND_COUNT = 1;
    RenderParams renderParams;
    MaterialPropertyBlock matProps;

    #region Points buffers
    GraphicsBuffer pointsPositionData;
    GraphicsBuffer positions;
    GraphicsBuffer segmentsQuaternions;
    GraphicsBuffer angularV;
    GraphicsBuffer invertedMasses;
    GraphicsBuffer invertedIntertias;
    GraphicsBuffer predictedQuaternions;
    GraphicsBuffer collisionConstraints;
    #endregion

    int startPositionKernelLinesId;

    int[] pbdKernels = new int[3];//0-prediction, 1-constraints, 2-post constraints
    int addPointKernelId;

    #region position calculation
    Vector3 lastPosition = new(0, 0, 0);
    #endregion

    #region rotation calculation
    Quaternion lastRotation = Quaternion.identity;
    #endregion

    #region Collision calculations
    [SerializeField]//only for debugging
    CollisionController collisionController;
    
    #endregion

    private void ValidateVars()
    {
        if(leftCut + rightCut >= capRadius*2)
        {
            Debug.LogError("sum of left and right cut must be smaller then diameter of cap");
        }
        if(backCut + frontCut >= capRadius*2)
        {
            Debug.LogError("sum of back and front cut must be smaller then diameter of cap");
        }
        if(strandCount % lines != 0)
        {
            Debug.LogError("Strand count must be a multiple of lines");
        }
    }

    private void Start()
    {
        ValidateVars();
        PrepareStructures();
        collisionController.PrepareCollision(maxSegments, strandLength, new float[] {capRadius, capHeight, capRadius}, this.transform, meshRenderer, collisionShader, false, minSphereSize);
    }

    private void Update()
    {
        renderParams.matProps = matProps;
        if (segments != 0)
        {
            Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, cmdBuffer);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            segments = Mathf.Max(0, segments - 1);
            if(segments != 0)
            {
                RebuildMesh();
            }
        }
        if(Input.GetKeyDown(KeyCode.UpArrow))
        {
            segments = Mathf.Min(maxSegments, segments + 1);
            RebuildMesh();
        }
    }

    private void OnDestroy()
    {
        ClearBuffers();
    }

    void FixedUpdate()
    {
        CalcPositions();
        matProps.SetBuffer("_PointsPositions", positions);
        matProps.SetBuffer("_SegmentsQuaternions", segmentsQuaternions);
    }

    private void ClearBuffers()
    {
        collisionController.ClearBuffers();
        pointsPositionData.Dispose();
        positions.Dispose();
        segmentsQuaternions.Dispose();
        angularV.Dispose();
        invertedMasses.Dispose();
        invertedIntertias.Dispose();
        predictedQuaternions.Dispose();
        cmdBuffer.Dispose();
        vertexBuffer.Dispose();
        indexBuffer.Dispose();
        collisionConstraints.Dispose();
    }

    /// <summary>
    /// This is called only once on start, to prepare all the structures we need.
    /// </summary>
    private void PrepareStructures()
    {
        PositionShaderSetup();
        MeshShaderSetup();
        HairShaderSetup();
        strandPositionShader.Dispatch(startPositionKernelLinesId, (int)Mathf.Ceil(strandCount / 64.0f), 1, 1);
        RebuildMesh();
    }

    private void PositionShaderSetup()
    {
        #region positions shader setup
        pbdKernels[0] = strandPositionShader.FindKernel("Predictions");
        pbdKernels[1] = strandPositionShader.FindKernel("CalcConstraints");
        pbdKernels[2] = strandPositionShader.FindKernel("PostConstraints");
        startPositionKernelLinesId = strandPositionShader.FindKernel("CalcStartPositionLines");
        addPointKernelId = strandPositionShader.FindKernel("AddPoint");

        float4[] zeroArray = new float4[strandCount * (maxSegments+1)*2];

        positions =             new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (maxSegments + 1), sizeof(float) * 3);
        pointsPositionData =    new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (maxSegments + 1)*2, sizeof(float) * 3);
        invertedMasses =        new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (maxSegments + 1), sizeof(float));
        segmentsQuaternions =   new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * maxSegments, sizeof(float)*4);
        angularV =              new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * maxSegments, sizeof(float)*3);
        invertedIntertias =     new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * maxSegments, sizeof(float));
        predictedQuaternions =  new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * maxSegments, sizeof(float) * 4);
        collisionConstraints =  new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (maxSegments + 1)*2, sizeof(float) * 4);
        collisionConstraints.SetData(zeroArray);

        strandPositionShader.SetFloat("_TimeStep", Time.fixedDeltaTime);
        strandPositionShader.SetFloat("_IterationCount", ITERATION_COUNT);
        strandPositionShader.SetFloat("_VelocityDumping", VELOCITY_DUMPING);

        SetStartBuffer();
        lastPosition = transform.position;
        #endregion
    }

    private void SetStartBuffer()
    {
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_PointData", pointsPositionData);
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_PointsPositions", positions);
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_InvertedMasses", invertedMasses);
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_SegmentsQuaternions", segmentsQuaternions);
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_AngularVelocities", angularV);
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_PredictedQuaternions", predictedQuaternions);
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_InvertedInterias", invertedIntertias);
        strandPositionShader.SetFloat("_ZPosRand", zRandRange);
        strandPositionShader.SetFloat("_XPosRand", xRandRange);
        strandPositionShader.SetFloat("_LeftCut", leftCut);
        strandPositionShader.SetFloat("_RightCut", rightCut);
        strandPositionShader.SetFloat("_BackCut", backCut);
        strandPositionShader.SetFloat("_FrontCut", frontCut);   
        SetVariables();
    }
    private void SetSimulationsBuffer()
    {
        foreach(int kernel in pbdKernels)
        {
            strandPositionShader.SetBuffer(kernel, "_PointData", pointsPositionData);
            strandPositionShader.SetBuffer(kernel, "_PointsPositions", positions);
            strandPositionShader.SetBuffer(kernel, "_InvertedMasses", invertedMasses);
            strandPositionShader.SetBuffer(kernel, "_SegmentsQuaternions", segmentsQuaternions);
            strandPositionShader.SetBuffer(kernel, "_AngularVelocities", angularV);
            strandPositionShader.SetBuffer(kernel, "_PredictedQuaternions", predictedQuaternions);
            strandPositionShader.SetBuffer(kernel, "_InvertedInterias", invertedIntertias);
            strandPositionShader.SetBuffer(kernel, "_CollisionConstraint", collisionConstraints);
        }
        SetVariables();
    }
    private void SetAddPointBuffer()
    {
        strandPositionShader.SetBuffer(addPointKernelId, "_PointData", pointsPositionData);
        strandPositionShader.SetBuffer(addPointKernelId, "_PointsPositions", positions);
        strandPositionShader.SetBuffer(addPointKernelId, "_InvertedMasses", invertedMasses);
        strandPositionShader.SetBuffer(addPointKernelId, "_SegmentsQuaternions", segmentsQuaternions);
        strandPositionShader.SetBuffer(addPointKernelId, "_AngularVelocities", angularV);
        strandPositionShader.SetBuffer(addPointKernelId, "_PredictedQuaternions", predictedQuaternions);
        strandPositionShader.SetBuffer(addPointKernelId, "_InvertedInterias", invertedIntertias);
    }
    private void SetVariables()
    {
        strandPositionShader.SetInt("_Lines", lines);
        strandPositionShader.SetInt("_StrandsPerLine", strandCount / lines);
        strandPositionShader.SetFloat("_CapHeight", capHeight);
        strandPositionShader.SetFloat("_CapRadius", capRadius);
        strandPositionShader.SetFloat("_SegmentLength", strandLength);
        strandPositionShader.SetInts("_Segments", segments);
        strandPositionShader.SetInts("_Strands", strandCount);
        strandPositionShader.SetVector("_CapPosition", new float4(transform.position, 0));
        strandPositionShader.SetFloat("_Stiffness", stiffness);
        strandPositionShader.SetFloat("_BendStiffness", bendStiffness);
    }
    private void MeshShaderSetup()
    {
        #region mesh shader setup
        verticesKernelId = strandMeshBuilder.FindKernel("BuildVertices");
        indicesKernelId = strandMeshBuilder.FindKernel("BuildIndices");
        closingIndicesKernelId = strandMeshBuilder.FindKernel("SetClosingIndices");

        cmdBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, COMMAND_COUNT, GraphicsBuffer.IndirectDrawArgs.size);
        cmdArgsBuffer = new GraphicsBuffer.IndirectDrawArgs[COMMAND_COUNT];

        matProps = new MaterialPropertyBlock();

        renderParams = new(hairMat)
        {
            worldBounds = new Bounds(transform.position, Vector3.one * 100f),
        };
        #endregion
    }
    private void HairShaderSetup()
    {
        #region setting buffers to material
        matProps = new MaterialPropertyBlock();

        matProps.SetBuffer("_PointsPositions", positions);
        matProps.SetBuffer("_SegmentsQuaternions", segmentsQuaternions);
        matProps.SetInt("_Strands", strandCount);
        #endregion
    }

    private void RebuildMesh()
    {
        int vertexCount = (segments + 1) * 4;
        int indexCount = (segments * 6 * 4) + 6;
        vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, sizeof(float) * 3);
        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indexCount, sizeof(int));
        matProps.SetBuffer("_Vertices", vertexBuffer);
        matProps.SetBuffer("_Indices", indexBuffer);

        //positions shader setup
        strandPositionShader.SetInt("_Segments", segments);
        strandPositionShader.SetInt("_Strands", strandCount);
        
        strandMeshBuilder.SetInt("_Segments", segments);
        strandMeshBuilder.SetFloat("_BaseSize", strandRadius);
        strandMeshBuilder.SetInt("_verticesCount", vertexCount);
        strandMeshBuilder.SetInt("_indicesCount", indexCount);

        strandMeshBuilder.SetBuffer(verticesKernelId, "_Vertices", vertexBuffer);
        strandMeshBuilder.SetBuffer(indicesKernelId, "_Indices", indexBuffer);
        strandMeshBuilder.SetBuffer(closingIndicesKernelId, "_Indices", indexBuffer);

        float vertGroup = ((segments + 1)*4)/64.0f;
        float indGroup = ((segments+1)*4)/64.0f;

        strandMeshBuilder.Dispatch(verticesKernelId, (int)Mathf.Ceil(vertGroup), 1, 1);
        strandMeshBuilder.Dispatch(indicesKernelId, (int)Mathf.Ceil(indGroup), 1, 1);
        strandMeshBuilder.Dispatch(closingIndicesKernelId, 1, 1, 1);

        cmdArgsBuffer[0].vertexCountPerInstance = (uint)segments * 6 * 4 + 6;//well, in reality this is index count
        cmdArgsBuffer[0].instanceCount = (uint)strandCount;

        strandPositionShader.SetInt("_Segments", segments);

        if(previousSegments < segments)//skip this when segmenst are less-equal then previous
        {
            SetAddPointBuffer();
            strandPositionShader.Dispatch(addPointKernelId, (int)Mathf.Ceil(strandCount / 32.0f), 1, 1);
        }

        cmdBuffer.SetData(cmdArgsBuffer);
        previousSegments = segments;
        //ReadGraphicBuffer<uint>(indexBuffer);
    }

    private void ShowResults<T>(GraphicsBuffer buffer)
    {
        //get all data from the buffer
        T[] data = new T[buffer.count];
        buffer.GetData(data);
        //Debug.Log(data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            Debug.Log($"Point {i}: {data[i]}");
        }
    }

    private void DrawBuffer(GraphicsBuffer buffer)
    {
        Vector4[] data = new Vector4[buffer.count];
        buffer.GetData(data);
        for(int i = 0; i < data.Length; ++i)
        {
            if (data[i].magnitude != 0)
            {
                Vector3 pos = new Vector3(data[i].x, data[i].y, data[i].z);
                Drawing.DrawSphereoid(pos, Vector3.one * 0.05f, Color.red, quaternion.identity, Time.fixedDeltaTime);
            }
        }
    }

    private void CalcPositions()
    {
        strandPositionShader.SetVector("_CapTranslation", new float4(transform.position - lastPosition, 0));
        strandPositionShader.SetVector("_CapPosition", new float4(transform.position, 0));

        float4 capRotationDelta = new float4((transform.rotation * Quaternion.Inverse(lastRotation)).x,
                                                    (transform.rotation * Quaternion.Inverse(lastRotation)).y,
                                                    (transform.rotation * Quaternion.Inverse(lastRotation)).z,
                                                    (transform.rotation * Quaternion.Inverse(lastRotation)).w);
        strandPositionShader.SetVector("_CapRotationDelta", capRotationDelta);
        SetSimulationsBuffer();
        strandPositionShader.Dispatch(pbdKernels[0], (int)Mathf.Ceil(strandCount / 64.0f), 1, 1);
        for (int i = 1; i < ITERATION_COUNT; i++)
        {
            strandPositionShader.Dispatch(pbdKernels[1], (int)Mathf.Ceil(strandCount / 64.0f), 1, 1);
            collisionController.CalculateCollisions(pointsPositionData, collisionConstraints, pointsPositionData.count / 2, strandRadius, strandCount, collisionStiffnes);
        }
        strandPositionShader.Dispatch(pbdKernels[2], (int)Mathf.Ceil(strandCount / 64.0f), 1, 1);

        collisionController.ClearCollision(collisionConstraints);

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }
}