using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public class HairController : MonoBehaviour
{
    [SerializeField]
    ComputeShader strandMeshBuilder;
    [SerializeField]
    int segments = 5;
    [SerializeField]
    float baseSize = 0.1f;
    [SerializeField]
    uint entityCount = 300;
    int verticesKernelId;
    int indicesKernelId;

    GraphicsBuffer vertexBuffer;
    GraphicsBuffer indexBuffer;

    Entity[] entities;
    EntityManager em;
    Mesh strandMesh;
    [SerializeField]
    Material hairMat;

    private void Start()
    {
        PrepareStructures();
        RebuildMesh();
    }

    void Update()
    {
        
    }

    /// <summary>
    /// This is called only once on start, to prepare all the structures we need.
    /// </summary>
    private void PrepareStructures()
    {
        //setting buffers to compute shader
        strandMesh = new Mesh();
        strandMesh.SetVertexBufferParams((segments + 1) * 4, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
        strandMesh.SetIndexBufferParams(segments * 6, IndexFormat.UInt32);
        strandMesh.SetSubMesh(0, new SubMeshDescriptor(0, segments * 6), MeshUpdateFlags.DontRecalculateBounds);

        strandMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        strandMesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;

        vertexBuffer = strandMesh.GetVertexBuffer(0);
        indexBuffer = strandMesh.GetIndexBuffer();

        verticesKernelId = strandMeshBuilder.FindKernel("BuildVertices");
        indicesKernelId = strandMeshBuilder.FindKernel("BuildIndices");

        //creating entities
        em = World.DefaultGameObjectInjectionWorld.EntityManager;

        var renderArray = new RenderMeshArray(
            new[] { hairMat },
            new[] { strandMesh }
        );
        var desc = new RenderMeshDescription(shadowCastingMode: ShadowCastingMode.Off, receiveShadows: true);

        entities = new Entity[entityCount];
        for (int i = 0; i < entityCount; i++)
        {
            entities[i] = em.CreateEntity();

            RenderMeshUtility.AddComponents(entities[i], em, desc, renderArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }
    }

    /// <summary>
    /// This is called every time when I need to rebuild the mesh.
    /// </summary>
    private void RebuildMesh()
    {
        strandMeshBuilder.SetInt("_Segments", segments);
        strandMeshBuilder.SetFloat("_BaseSize", baseSize);
        strandMeshBuilder.SetBuffer(verticesKernelId, "_Vertices", vertexBuffer);

        strandMeshBuilder.Dispatch(verticesKernelId, 1, 1, 1);
        strandMeshBuilder.SetBuffer(indicesKernelId, "_Indices", indexBuffer);
        strandMeshBuilder.Dispatch(indicesKernelId, 1, 1, 1);
    }
}
