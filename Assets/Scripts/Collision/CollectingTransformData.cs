using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

public struct CollectingTransformData : IJobParallelForTransform
{
    public NativeArray<Vector3> bonesPositions;
    public NativeArray<Vector4> bonesRotations;

    public void Execute(int index, TransformAccess transform)
    {
        bonesPositions[index] = transform.position;
        bonesRotations[index] = new Vector4(transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w);
    }
}