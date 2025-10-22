using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[MaterialProperty("_MyEntityScale")]
public struct StrandData : IComponentData
{
    public float _MyEntityScale;
}