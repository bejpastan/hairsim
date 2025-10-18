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

    private void Start()
    {

    }

    private void Rebuild()
    {
        World world = World.DefaultGameObjectInjectionWorld;
        EntityManager em = world.EntityManager;

        Entity[] entity = new Entity[2000];

        Mesh strandMesh = StrandSpawner.CreateMesh(4, 0.25f, 3.0f);

        var renderArray = new RenderMeshArray(
            new[] { material },
            new[] { strandMesh }
        );

        var desc = new RenderMeshDescription(shadowCastingMode: ShadowCastingMode.Off, receiveShadows: true);

        float3 pos;

        for (int i = 0; i < 2000; i++)
        {
            entity[i] = em.CreateEntity();
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
