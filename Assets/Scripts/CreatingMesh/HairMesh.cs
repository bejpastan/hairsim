using UnityEngine;
using Unity.Entities;
using UnityEngine.Rendering;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Mathematics;

public class HairMesh : MonoBehaviour
{
    bool rebuild = false;

    public Material material;

    Entity[] entity = new Entity[0];

    private void Start()
    {

    }

    private void Rebuild()
    {
        World world = World.DefaultGameObjectInjectionWorld;
        EntityManager em = world.EntityManager;

        Mesh strandMesh = StrandSpawner.CreateMesh(30, 0.25f, 3.0f);

        var renderArray = new RenderMeshArray(
            new[] { material },
            new[] { strandMesh }
        );

        float3 pos;

        if(entity.Length == 0)
        {
            entity = new Entity[300];
            for (int i = 0; i < 300; i++)
            {
                entity[i] = em.CreateEntity();
            }
        }
        var desc = new RenderMeshDescription(shadowCastingMode: ShadowCastingMode.Off, receiveShadows: true);
        for (int i = 0; i < 300; i++)
        {
            pos = new float3(0.1f * i, 0, 0);
            RenderMeshUtility.AddComponents(entity[i], em, desc, renderArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            em.SetComponentData(entity[i], new LocalToWorld { Value = float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)) });
        }

        rebuild = true;
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
