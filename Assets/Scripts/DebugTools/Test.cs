using UnityEngine;

public class Test : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        Drawing.DrawSphereoid(transform.position, new Vector3(0.5f, 1, 0.75f), Color.red, transform.rotation, 0.16f);
    }
}
