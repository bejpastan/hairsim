using UnityEngine;

public class Test : MonoBehaviour
{
    private void Update()
    {
        Drawing.DrawSphereoid(transform.position, transform.localScale, Color.red, transform.rotation, 0.016f);
    }
}
