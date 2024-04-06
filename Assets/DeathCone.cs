using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathCone : MonoBehaviour
{
    List<Vector3> vertices = new List<Vector3>();
    public int resolution;
    public float radius;
    public int distance;

    void Start()
    {
        transform.localPosition = new Vector3(0, -distance, 0);
        vertices.Add(Vector3.up * distance);
        for (int i = 0; i < resolution; i++)
        {
            float angle = Mathf.PI * 2 * i / resolution;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, 0, z));
        }

        Mesh Cone = new Mesh();

        List<int> triangles = new List<int>();
        for (int i = 1; i < vertices.Count; i++)
        {
            if (i == vertices.Count - 1)
            {
                triangles.Add(1);
                triangles.Add(i);
                triangles.Add(0);
            }
            else
            {
                triangles.Add(i + 1);
                triangles.Add(i);
                triangles.Add(0);
            }
        }

        Cone.vertices = vertices.ToArray();
        Cone.triangles = triangles.ToArray();
        GetComponent<MeshFilter>().mesh = Cone;
    }
}
