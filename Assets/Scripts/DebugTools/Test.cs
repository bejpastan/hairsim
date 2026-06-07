using UnityEngine;

public class Test : MonoBehaviour
{
    private void Update()
    {
        Debug.Log($"Frame rate: {1 / Time.deltaTime}");
    }
}
