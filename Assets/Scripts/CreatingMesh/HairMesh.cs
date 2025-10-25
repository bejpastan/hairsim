using UnityEngine;
using Unity.Entities;
using UnityEngine.Rendering;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

public class HairMesh : MonoBehaviour
{
    bool rebuild = false;
    [SerializeField]
    Material material;
    [SerializeField]
    int segments;
    [SerializeField]
    int entiyCount = 300;

    Entity[] entity = new Entity[0];

    //mesh data
    Mesh strandMesh;
    RenderMeshArray renderArray;

    EntityManager em;
    private void Start()
    {
        PrepareData();
    }

    private void PrepareData()
    {
        World world = World.DefaultGameObjectInjectionWorld;
        em = world.EntityManager;
        strandMesh = new Mesh();
        Rebuild();
        renderArray = new RenderMeshArray(
            new[] { material },
            new[] { strandMesh }
        );
        SpawnEntities();
    }

    private void Rebuild()
    {
        NativeArray<float3> positions;
        NativeArray<ushort> indices;

        StrandSpawner.CreateMesh(segments, 0.25f, 3.0f, out positions, out indices);

        strandMesh.Clear();
        strandMesh.SetVertices(positions);
        strandMesh.SetIndices(indices, MeshTopology.Triangles, 0);
        rebuild = true;
    }

    private void SpawnEntities()
    {
        float3 pos;

        if (entity.Length == 0)
        {
            entity = new Entity[entiyCount];
            for (int i = 0; i < entiyCount; i++)
            {
                entity[i] = em.CreateEntity();
            }
        }
        var desc = new RenderMeshDescription(shadowCastingMode: ShadowCastingMode.Off, receiveShadows: true);
        for (int i = 0; i < entiyCount; i++)
        {
            pos = new float3(0.1f * i, 0, 0);
            RenderMeshUtility.AddComponents(entity[i], em, desc, renderArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            em.SetComponentData(entity[i], new LocalToWorld { Value = float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)) });
        }
    }

    private void Update()
    {
        if(rebuild)
        {
            Debug.Log($"FPS: {1 / Time.deltaTime}");
            rebuild = false;
        }

        if(Input.GetKeyDown(KeyCode.Space))
        {
            Rebuild();
        }
    }
}
