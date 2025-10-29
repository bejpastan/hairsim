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
    float baseSize = 0.1f;
    [SerializeField]
    int entityCount = 2;
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

    int positionKernelId;

    private void Start()
    {
        PrepareStructures();
    }

    void Update()
    {
        Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, cmdBuffer);
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
        //positions shader setup
        positionKernelId = strandPositionShader.FindKernel("CalcPosition");
        positions = new ComputeBuffer((int)entityCount * segments, sizeof(float) * 3);

        //mesh shader setup
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

        renderParams.matProps.SetBuffer("_Vertices", vertexBuffer);
        renderParams.matProps.SetBuffer("_Indices", indexBuffer);
        renderParams.matProps.SetBuffer("_Positions", positions);
        renderParams.matProps.SetInt("_Segments", segments);

        RebuildMesh();
    }

    private async void RebuildMesh()
    {

        //positions shader setup
        strandPositionShader.SetInts("_Segments", segments);
        strandPositionShader.SetInts("_Strands", entityCount);
        strandPositionShader.SetBuffer(positionKernelId, "_PointsPositions", positions);

        rebuild = true;
        strandMeshBuilder.SetInt("_Segments", segments);
        strandMeshBuilder.SetFloat("_BaseSize", baseSize);
        strandMeshBuilder.SetInt("_verticesCount", (segments + 1) * 4);
        strandMeshBuilder.SetInt("_indicesCount", segments * 6 * 4);

        strandMeshBuilder.SetBuffer(verticesKernelId, "_Vertices", vertexBuffer);
        strandMeshBuilder.SetBuffer(indicesKernelId, "_Indices", indexBuffer);

        float vertGroup= ((segments + 1)*4)/32.0f;
        float indGroup = (segments * 6.0f*4.0f)/32.0f;

        strandMeshBuilder.Dispatch(verticesKernelId, (int)Mathf.Ceil(vertGroup), 1, 1);
        strandMeshBuilder.Dispatch(indicesKernelId, (int)Mathf.Ceil(indGroup), 1, 1);

        cmdArgsBuffer[0].vertexCountPerInstance = (uint)segments * 6 * 4;
        cmdArgsBuffer[0].instanceCount = (uint)entityCount;

        cmdBuffer.SetData(cmdArgsBuffer);
    }

    private void ShowResults()
    {
        float3[] vertices = new float3[(segments + 1) * 4];
        int[] indices = new int[segments * 6 * 4];
        vertexBuffer.GetData(vertices);
        indexBuffer.GetData(indices);

        for(int i = 0; i < vertices.Length; i++)
        {
            Debug.Log($"Vertex {i}: {vertices[i]}");
        }
        for(int i = 0; i < indices.Length; i++)
        {
            Debug.Log($"Index {i}: {indices[i]}");
        }
    }

    private void CalcPositions()
    {
        strandPositionShader.Dispatch(positionKernelId, (int)Mathf.Ceil(entityCount / 64.0f), (int)Mathf.Ceil(segments / 64.0f), 1);
    }
}