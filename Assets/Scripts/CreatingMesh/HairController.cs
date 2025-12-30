using UnityEngine;
using Unity.Mathematics;

public class HairController : MonoBehaviour
{
    [SerializeField]
    ComputeShader strandMeshBuilder;
    [SerializeField]
    ComputeShader strandPositionShader;
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
    [SerializeField]
    [Range(0, 1)]
    float velocityDumping = 0.98f;
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
    readonly int strandsInLine;

    [SerializeField]
    float capHeight = 2.0f;
    [SerializeField]
    float capRadius = 1.0f;

    int verticesKernelId;
    int indicesKernelId;

    GraphicsBuffer vertexBuffer;
    GraphicsBuffer indexBuffer;

    [SerializeField]
    Material hairMat;

    bool rebuild = false;

    GraphicsBuffer cmdBuffer;
    GraphicsBuffer.IndirectDrawArgs[] cmdArgsBuffer;
    const int COMMAND_COUNT = 1;
    RenderParams renderParams;

    #region Points buffers
    ComputeBuffer pointsPositionData;
    ComputeBuffer positions;
    ComputeBuffer segmentsQuaternions;//new
    ComputeBuffer angularV;//new
    ComputeBuffer invertedMasses;
    ComputeBuffer invertedIntertias;//new
    ComputeBuffer predictedQuaternions; //new
    #endregion

    int startPositionKernelLinesId;
    int positionKernelId;
    int addPointKernelId;

    #region position calculation
    Vector3 lastPosition = new(0, 0, 0);
    #endregion

    #region rotation calculation
    Quaternion lastRotation = Quaternion.identity;
    #endregion

    private void Start()
    {
        //previousSegments = segments;
        PrepareStructures();
    }

    private void Update()
    {
        Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, cmdBuffer);
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RebuildMesh();
        }
    }

    void FixedUpdate()
    {
        CalcPositions();
        renderParams.matProps.SetBuffer("_PointsPositions", positions);
        renderParams.matProps.SetBuffer("_SegmentsQuaternions", segmentsQuaternions);
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
        positionKernelId = strandPositionShader.FindKernel("CalcPosition");
        startPositionKernelLinesId = strandPositionShader.FindKernel("CalcStartPositionLines");
        addPointKernelId = strandPositionShader.FindKernel("AddPoint");

        positions = new ComputeBuffer((int)strandCount * (maxSegments + 1), sizeof(float) * 3);
        pointsPositionData = new ComputeBuffer((int)strandCount * (maxSegments + 1)*2, sizeof(float) * 3);
        invertedMasses = new ComputeBuffer((int)strandCount * (maxSegments + 1), sizeof(float));
        segmentsQuaternions = new ComputeBuffer(strandCount*maxSegments, sizeof(float)*4);
        angularV = new ComputeBuffer(strandCount*maxSegments, sizeof(float)*3);
        invertedIntertias = new ComputeBuffer(strandCount * maxSegments, sizeof(float));
        predictedQuaternions = new ComputeBuffer(strandCount * maxSegments, sizeof(float) * 4);

        strandPositionShader.SetFloat("_TimeStep", Time.fixedDeltaTime);
        strandPositionShader.SetFloat("_IterationCount", 5);
        strandPositionShader.SetFloat("_VelocityDumping", velocityDumping);

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
        SetVariables();
    }
    private void SetSimulationsBuffer()
    {
        strandPositionShader.SetBuffer(positionKernelId, "_PointData", pointsPositionData);
        strandPositionShader.SetBuffer(positionKernelId, "_PointsPositions", positions);
        strandPositionShader.SetBuffer(positionKernelId, "_InvertedMasses", invertedMasses);
        strandPositionShader.SetBuffer(positionKernelId, "_SegmentsQuaternions", segmentsQuaternions);
        strandPositionShader.SetBuffer(positionKernelId, "_AngularVelocities", angularV);
        strandPositionShader.SetBuffer(positionKernelId, "_PredictedQuaternions", predictedQuaternions);
        strandPositionShader.SetBuffer(positionKernelId, "_InvertedInterias", invertedIntertias);
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

        cmdBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, COMMAND_COUNT, GraphicsBuffer.IndirectDrawArgs.size);
        cmdArgsBuffer = new GraphicsBuffer.IndirectDrawArgs[COMMAND_COUNT];

        renderParams = new(hairMat)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f),
            matProps = new MaterialPropertyBlock()
        };
        #endregion
    }

    private void HairShaderSetup()
    {
        #region setting buffers to material
        renderParams.matProps.SetBuffer("_PointsPositions", positions);
        renderParams.matProps.SetBuffer("_SegmentsQuaternions", segmentsQuaternions);
        renderParams.matProps.SetInt("_Strands", strandCount);
        #endregion
    }

    private void RebuildMesh()
    {
        int vertexCount = (segments + 1) * 4;
        int indexCount = segments * 6 * 4;
        vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, vertexCount, sizeof(float) * 3);
        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, indexCount, sizeof(int));
        renderParams.matProps.SetBuffer("_Vertices", vertexBuffer);
        renderParams.matProps.SetBuffer("_Indices", indexBuffer);

        //positions shader setup
        strandPositionShader.SetInts("_Segments", segments);
        strandPositionShader.SetInts("_Strands", strandCount);
        
        rebuild = true;
        strandMeshBuilder.SetInt("_Segments", segments);
        strandMeshBuilder.SetFloat("_BaseSize", strandRadius);
        strandMeshBuilder.SetInt("_verticesCount", (segments + 1) * 4);
        strandMeshBuilder.SetInt("_indicesCount", segments * 6 * 4);

        strandMeshBuilder.SetBuffer(verticesKernelId, "_Vertices", vertexBuffer);
        strandMeshBuilder.SetBuffer(indicesKernelId, "_Indices", indexBuffer);

        float vertGroup= ((segments + 1)*4)/32.0f;
        float indGroup = (segments * 6.0f*4.0f)/32.0f;

        strandMeshBuilder.Dispatch(verticesKernelId, (int)Mathf.Ceil(vertGroup), 1, 1);
        strandMeshBuilder.Dispatch(indicesKernelId, (int)Mathf.Ceil(indGroup), 1, 1);

        cmdArgsBuffer[0].vertexCountPerInstance = (uint)segments * 6 * 4;//well, in reality this is index count
        cmdArgsBuffer[0].instanceCount = (uint)strandCount;

        strandPositionShader.SetInt("_Segments", segments);

        if(previousSegments < segments)//skip this when segmenst are less-equal then previous
        {
            SetAddPointBuffer();
            strandPositionShader.Dispatch(addPointKernelId, (int)Mathf.Ceil(strandCount / 32.0f), 1, 1);
        }

        cmdBuffer.SetData(cmdArgsBuffer);
        ShowResults<float>(invertedIntertias);
        previousSegments = segments;
    }

    private void ShowResults<T>(ComputeBuffer buffer)
    {
        //get all data from the buffer
        T[] data = new T[buffer.count];
        buffer.GetData(data);
        //Debug.Log(data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            Debug.Log($"Point {i}: {data[i]}");
        }

        //Debug.Log("all showed");
    }

    private void ReadGraphicBuffer<T>(GraphicsBuffer buffer)
    {
        T[] data = new T[buffer.count];
        buffer.GetData(data);
        Debug.Log(data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            Debug.Log($"Point {i}: {data[i]}");
        }
        Debug.Log("all showed");
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
        strandPositionShader.Dispatch(positionKernelId, (int)Mathf.Ceil(strandCount / 64.0f), 1, 1);

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }
}