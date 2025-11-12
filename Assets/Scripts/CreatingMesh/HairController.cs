using UnityEngine;
using Unity.Mathematics;
using System.Threading.Tasks;
using System.Collections;
using Unity.VisualScripting;
using System.Collections.Generic;

public class HairController : MonoBehaviour
{
    [SerializeField]
    ComputeShader strandMeshBuilder;
    [SerializeField]
    ComputeShader strandPositionShader;
    [SerializeField]
    int segments;
    [SerializeField]
    float strandRadius = 0.1f;
    [SerializeField]
    float strandLength = 1.0f;
    [SerializeField]
    int strandCount = 20;// add validating to be multiple of lines

    [SerializeField]
    int lines = 4;
    int strandsInLine;

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
    ComputeBuffer positions;
    ComputeBuffer quaternion;
    ComputeBuffer velocities;

    int startPositionKernelLinesId;
    int positionKernelId;

    #region position calculation
    Vector3 lastPosition = new Vector3(0, 0, 0);
    #endregion


    private void Start()
    {
        PrepareStructures();
    }

    private void Update()
    {
        Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, cmdBuffer);
    }

    void FixedUpdate()
    {
        renderParams.matProps.SetBuffer("_Positions", positions);
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RebuildMesh();
        }
        CalcPositions();
        Debug.Log($"FPS: {1 / Time.deltaTime}");
    }

    /// <summary>
    /// This is called only once on start, to prepare all the structures we need.
    /// </summary>
    private void PrepareStructures()
    {
        #region positions shader setup
        positionKernelId = strandPositionShader.FindKernel("CalcPosition");
        startPositionKernelLinesId = strandPositionShader.FindKernel("CalcStartPositionLines");
        positions = new ComputeBuffer((int)strandCount * (segments+1), sizeof(float) * 3);
        quaternion = new ComputeBuffer((int)strandCount * (segments + 1), sizeof(float) * 4);
        velocities = new ComputeBuffer((int)strandCount * (segments + 1), sizeof(float) * 3);

        strandPositionShader.SetBuffer(positionKernelId, "_PointsPositions", positions);
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_PointsPositions", positions);
        strandPositionShader.SetBuffer(positionKernelId, "_StrandQuaternion", quaternion);
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_StrandQuaternion", quaternion);
        strandPositionShader.SetBuffer(positionKernelId, "_PointsVelocities", velocities);
        strandPositionShader.SetBuffer(startPositionKernelLinesId, "_PointsVelocities", velocities);

        strandPositionShader.SetInt("_Lines", lines);
        strandPositionShader.SetInt("_StrandsPerLine", strandCount/lines);
        strandPositionShader.SetFloat("_CapHeight", capHeight);
        strandPositionShader.SetFloat("_CapRadius", capRadius);
        strandPositionShader.SetFloat("_TimeStep", Time.fixedDeltaTime);
        strandPositionShader.SetFloat("_SegmentLength", strandLength);
        strandPositionShader.SetInts("_Segments", segments);
        strandPositionShader.SetInts("_Strands", strandCount);
        strandPositionShader.SetVector("_CapPosition", new float4(transform.position, 0));
        lastPosition = transform.position;
        #endregion

        #region mesh shader setup
        verticesKernelId = strandMeshBuilder.FindKernel("BuildVertices");
        indicesKernelId = strandMeshBuilder.FindKernel("BuildIndices");

        int vertexCount = (segments + 1) * 4;
        int indexCount = segments * 6 * 4;
        vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, vertexCount, sizeof(float) * 3);
        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, indexCount, sizeof(int));

        cmdBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, COMMAND_COUNT, GraphicsBuffer.IndirectDrawArgs.size);
        cmdArgsBuffer = new GraphicsBuffer.IndirectDrawArgs[COMMAND_COUNT];
        
        renderParams = new RenderParams(hairMat);
        renderParams.worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        renderParams.matProps = new MaterialPropertyBlock();
        #endregion

        #region setting buffers to material
        renderParams.matProps.SetBuffer("_Vertices", vertexBuffer);
        renderParams.matProps.SetBuffer("_Indices", indexBuffer);
        renderParams.matProps.SetBuffer("_Positions", positions);
        renderParams.matProps.SetBuffer("_Quternion", quaternion);
        renderParams.matProps.SetInt("_Segments", segments);
        #endregion

        //create start positions
        strandPositionShader.Dispatch(startPositionKernelLinesId, (int)Mathf.Ceil(strandCount / 64.0f), 1, 1);

        RebuildMesh();
    }

    private async void RebuildMesh()
    {

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

        cmdArgsBuffer[0].vertexCountPerInstance = (uint)segments * 6 * 4;
        cmdArgsBuffer[0].instanceCount = (uint)strandCount;

        cmdBuffer.SetData(cmdArgsBuffer);
    }

    private void ShowResults<T>(ComputeBuffer buffer)
    {
        //get all data from the buffer
        T[] data = new T[buffer.count];
        buffer.GetData(data);
        Debug.Log(data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            Debug.Log($"Point {i}: {data[i]}");
        }
        Debug.Log("all showed");
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
        //ShowResults<float3>(positions);
        strandPositionShader.Dispatch(positionKernelId, (int)Mathf.Ceil(strandCount / 64.0f), 1, 1);
        strandPositionShader.SetVector("_CapTranslation", new float4(transform.position - lastPosition, 0));
        //ShowResults<float3>(velocities);
        lastPosition = transform.position;
    }
}