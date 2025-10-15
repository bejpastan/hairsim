using UnityEngine;

public class HairController : MonoBehaviour
{
    ComputeShader strandMeshBuilder;
    [SerializeField]
    int segments = 5;
    int verticesKernelId;
    int indicesKernelId;

    // Update is called once per frame
    void Update()
    {
        
    }

    private void PrepareDate()
    {
        verticesKernelId = strandMeshBuilder.FindKernel("BuildVertices");
        indicesKernelId = strandMeshBuilder.FindKernel("BuildIndices");
    }
}
