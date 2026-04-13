using System;
using Unity.Mathematics;
using UnityEngine;

public class HairController : MonoBehaviour
{
    const int ITERATION_COUNT = 15;
    [Range(0, 1)]
    public float collisionStiffnes;

    //TO DO get this automatically
    ComputeShader strandMeshBuilder;
    ComputeShader strandPositionShader;
    ComputeShader collisionShader;
    ComputeShader resizing;
    [SerializeField]
    SkinnedMeshRenderer meshRenderer;


    [SerializeField]
    int segments;
    //[SerializeField]
    //int maxSegments = 100;
    int previousSegments = 0;
    [SerializeField]
    float strandRadius = 0.1f;

    [SerializeField]
    int strandCount = 20;// add validating to be multiple of lines

    [Header("PDB")]
    const float VELOCITY_DUMPING = 0.98f;
    [Header("Distance Constraints")]
    [SerializeField]
    [Range(0, 1)]
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
    GraphicsBuffer pointsPositionData;//1
    GraphicsBuffer positions;//2
    GraphicsBuffer segmentsQuaternions;//3
    GraphicsBuffer angularV;//4
    GraphicsBuffer invertedMasses;//5
    GraphicsBuffer invertedIntertias;//6
    GraphicsBuffer predictedQuaternions;//7
    GraphicsBuffer collisionConstraints;//8
    #endregion

    #region changeLength
    int lastChangeLength = 7;//range 0-7

    int resizeKernelId;
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
        if (leftCut + rightCut >= capRadius * 2)
        {
            Debug.LogError("sum of left and right cut must be smaller then diameter of cap");
        }
        if (backCut + frontCut >= capRadius * 2)
        {
            Debug.LogError("sum of back and front cut must be smaller then diameter of cap");
        }
        if (strandCount % lines != 0)
        {
            Debug.LogError("Strand count must be a multiple of lines");
        }
    }

    private void Start()
    {
        LoadShaders();
        ValidateVars();
        PrepareStructures();
        collisionController.PrepareCollision(new float[] { capRadius, capHeight, capRadius }, this.transform, meshRenderer, collisionShader, false, minSphereSize);
    }

    private void Update()
    {
        renderParams.matProps = matProps;
        if (segments != 0)
        {
            Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, cmdBuffer);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))//this is my external logic, ignore this if
        {
            segments = Mathf.Max(0, segments - 1);
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            segments++;
        }
    }

    private void OnDestroy()
    {
        ClearBuffers();
    }

    void FixedUpdate()
    {
        if(segments != previousSegments)
        {
            ResizeHair();
            RebuildMesh();
        }
        CalcPositions();
        matProps.SetBuffer("_PointsPositions", positions);
        matProps.SetBuffer("_SegmentsQuaternions", segmentsQuaternions);
    }


    private void LoadShaders()
    {
        strandMeshBuilder = Resources.Load<ComputeShader>("ComputeShaders/RebuildMeshShader");
        strandPositionShader = Resources.Load<ComputeShader>("ComputeShaders/StrandsPositions");
        collisionShader = Resources.Load<ComputeShader>("ComputeShaders/SDFCollisions");
        resizing = Resources.Load<ComputeShader>("ComputeShaders/ResizingBuffer");

        verticesKernelId = strandMeshBuilder.FindKernel("BuildVertices");
        indicesKernelId = strandMeshBuilder.FindKernel("BuildIndices");
        closingIndicesKernelId = strandMeshBuilder.FindKernel("SetClosingIndices");

        pbdKernels[0] = strandPositionShader.FindKernel("Predictions");
        pbdKernels[1] = strandPositionShader.FindKernel("CalcConstraints");
        pbdKernels[2] = strandPositionShader.FindKernel("PostConstraints");
        startPositionKernelLinesId = strandPositionShader.FindKernel("CalcStartPositionLines");
        addPointKernelId = strandPositionShader.FindKernel("AddPoint");

        resizeKernelId = resizing.FindKernel("ResizeBuffer");
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
        previousSegments = segments;
        RebuildMesh();
    }

    private void PositionShaderSetup()
    {
        #region positions shader setup
        //I need to change this
        //positions =             new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (maxSegments + 1), sizeof(float) * 3);
        //pointsPositionData =    new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (maxSegments + 1)*2, sizeof(float) * 3);
        //invertedMasses =        new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (maxSegments + 1), sizeof(float));
        //segmentsQuaternions =   new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * maxSegments, sizeof(float)*4);
        //angularV =              new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * maxSegments, sizeof(float)*3);
        //invertedIntertias =     new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * maxSegments, sizeof(float));
        //predictedQuaternions =  new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * maxSegments, sizeof(float) * 4);
        //collisionConstraints =  new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (maxSegments + 1)*2, sizeof(float) * 4);

        pointsPositionData =    new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (segments + 1) * 2, sizeof(float) * 3);//this have original size, because is resized when rebuilding mesh
        positions =             new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (segments + 2), sizeof(float) * 3);
        segmentsQuaternions =   new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (segments +2), sizeof(float) * 4);
        angularV =              new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (segments+3), sizeof(float) * 3);
        invertedMasses =        new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * ((segments+4) + 1), sizeof(float));
        invertedIntertias =     new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (segments+5), sizeof(float));
        predictedQuaternions =  new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * (segments+6), sizeof(float) * 4);
        collisionConstraints =  new GraphicsBuffer(GraphicsBuffer.Target.Structured, strandCount * ((segments+7) + 1) * 2, sizeof(float) * 4);

        float4[] zeroArray = new float4[strandCount * (segments + 1) * 2];
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
        foreach (int kernel in pbdKernels)
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
        cmdBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, COMMAND_COUNT, GraphicsBuffer.IndirectDrawArgs.size);
        cmdArgsBuffer = new GraphicsBuffer.IndirectDrawArgs[COMMAND_COUNT];

        matProps = new MaterialPropertyBlock();

        renderParams = new(hairMat)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f),
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


    /// <summary>
    /// for resizing existing gruphics buffers
    /// </summary>
    /// <param name="old"></param>
    /// <param name="newSize"></param>
    private GraphicsBuffer ResizeBuffer(GraphicsBuffer old, int newSize)
    {
        GraphicsBuffer resized = new GraphicsBuffer(old.target, newSize, old.stride);
        int oldBytes = old.count * old.stride;
        int newBytes = resized.count * resized.stride;
        resizing.SetBuffer(resizeKernelId, "_OldBuffer", old);
        resizing.SetBuffer(resizeKernelId, "_NewBuffer", resized);
        resizing.SetInt("_OldBytes", oldBytes);
        resizing.SetInt("_NewBytes", newBytes);
        int xGroup = Mathf.CeilToInt((float)oldBytes / 64);
        resizing.Dispatch(resizeKernelId, xGroup, 1, 1);
        old.Dispose();
        return resized;
    }


    private void ResizeHair()
    {
        #region add simulation points
        if (previousSegments < segments)
        {
            //lenghtening
            lastChangeLength++;
            lastChangeLength = lastChangeLength % 8;//when I get 8, it return to 0
            int newSegmentSize = segments + 7;
            switch (lastChangeLength)
            {
                case 0:
                    pointsPositionData = ResizeBuffer(pointsPositionData, strandCount * (newSegmentSize + 1) * 2);
                    break;
                case 1:
                    positions = ResizeBuffer(positions, strandCount * (newSegmentSize + 1));
                    break;
                case 2:
                    segmentsQuaternions = ResizeBuffer(segmentsQuaternions, strandCount * (newSegmentSize));
                    break;
                case 3:
                    angularV = ResizeBuffer(angularV, strandCount * newSegmentSize);
                    break;
                case 4:
                    invertedMasses = ResizeBuffer(invertedMasses, strandCount * (newSegmentSize + 1));
                    break;
                case 5:
                    invertedIntertias = ResizeBuffer(invertedIntertias, strandCount * newSegmentSize);
                    break;
                case 6:
                    predictedQuaternions = ResizeBuffer(predictedQuaternions, strandCount * newSegmentSize);
                    break;
                case 7:
                    collisionConstraints = ResizeBuffer(collisionConstraints, strandCount * (newSegmentSize + 1) * 2);
                    break;
            }
        }
        else
        {
            //shortening
            int newSegmentSize = segments;
            switch (lastChangeLength)
            {
                case 0:
                    pointsPositionData = ResizeBuffer(pointsPositionData, strandCount * (newSegmentSize + 1) * 2);
                    break;
                case 1:
                    positions = ResizeBuffer(positions, strandCount * (newSegmentSize + 1));
                    break;
                case 2:
                    segmentsQuaternions = ResizeBuffer(segmentsQuaternions, strandCount * (newSegmentSize));
                    break;
                case 3:
                    angularV = ResizeBuffer(angularV, strandCount * newSegmentSize);
                    break;
                case 4:
                    invertedMasses = ResizeBuffer(invertedMasses, strandCount * (newSegmentSize + 1));
                    break;
                case 5:
                    invertedIntertias = ResizeBuffer(invertedIntertias, strandCount * newSegmentSize);
                    break;
                case 6:
                    predictedQuaternions = ResizeBuffer(predictedQuaternions, strandCount * newSegmentSize);
                    break;
                case 7:
                    collisionConstraints = ResizeBuffer(collisionConstraints, strandCount * (newSegmentSize + 1) * 2);
                    break;
            }
            lastChangeLength += 7;//adding 7 to go around
            lastChangeLength = lastChangeLength % 8;//when I get 8, it return to 0
        }

        strandPositionShader.SetInt("_Strands", strandCount);
        strandPositionShader.SetInt("_Segments", segments);

        if (previousSegments < segments)//skip this when segmenst are less-equal then previous
        {
            SetAddPointBuffer();
            strandPositionShader.Dispatch(addPointKernelId, (int)Mathf.Ceil(strandCount / 64.0f), 1, 1);
        }

        Debug.Log(lastChangeLength);
        ShowResults<float3>(pointsPositionData);
        ShowResults<float3>(positions);
        ShowResults<float4>(segmentsQuaternions);
        ShowResults<float3>(angularV);
        ShowResults<float>(invertedMasses);
        ShowResults<float>(invertedIntertias);
        ShowResults<float4>(predictedQuaternions);
        ShowResults<float4>(collisionConstraints);
        #endregion

    }


    private void RebuildMesh()
    {
        #region rebuild mesh
        int vertexCount = (segments + 1) * 4;
        int indexCount = (segments * 6 * 4) + 6;
        vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, sizeof(float) * 3);
        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indexCount, sizeof(int));
        matProps.SetBuffer("_Vertices", vertexBuffer);
        matProps.SetBuffer("_Indices", indexBuffer);

        //positions shader setup        
        strandMeshBuilder.SetInt("_Segments", segments);
        strandMeshBuilder.SetFloat("_BaseSize", strandRadius);
        strandMeshBuilder.SetInt("_verticesCount", vertexCount);
        strandMeshBuilder.SetInt("_indicesCount", indexCount);

        strandMeshBuilder.SetBuffer(verticesKernelId, "_Vertices", vertexBuffer);
        strandMeshBuilder.SetBuffer(indicesKernelId, "_Indices", indexBuffer);
        strandMeshBuilder.SetBuffer(closingIndicesKernelId, "_Indices", indexBuffer);

        float vertGroup = ((segments + 1) * 4) / 64.0f;
        float indGroup = ((segments + 1) * 4) / 64.0f;

        strandMeshBuilder.Dispatch(verticesKernelId, (int)Mathf.Ceil(vertGroup), 1, 1);
        strandMeshBuilder.Dispatch(indicesKernelId, (int)Mathf.Ceil(indGroup), 1, 1);
        strandMeshBuilder.Dispatch(closingIndicesKernelId, 1, 1, 1);

        cmdArgsBuffer[0].vertexCountPerInstance = (uint)segments * 6 * 4 + 6;//well, in reality this is index count
        cmdArgsBuffer[0].instanceCount = (uint)strandCount;
        #endregion

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
        for (int i = 0; i < data.Length; ++i)
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
            collisionController.CalculateCollisions(pointsPositionData, collisionConstraints, pointsPositionData.count / 2, strandRadius, strandCount);
        }
        strandPositionShader.Dispatch(pbdKernels[2], (int)Mathf.Ceil(strandCount / 64.0f), 1, 1);

        collisionController.ClearCollision(collisionConstraints);

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }
}