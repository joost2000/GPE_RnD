using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubes : MonoBehaviour
{
    [Header("Grid Size")]
    public int CubicSize;
    private int width;
    private int height;

    [Header("Variables")]
    [SerializeField] float noiseScale = 1;
    [SerializeField] float heightScale = 1;
    [SerializeField] private float isoValue;

    [Header("Options")]
    [SerializeField] bool useSphere;
    [SerializeField] bool useInterpolation;

    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    float[,,] heights;

    private MeshFilter meshFilter;

    void Start()
    {
        width = CubicSize;
        height = CubicSize;
        meshFilter = GetComponent<MeshFilter>();
        Reset();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            Reset();
        }
    }

    void Reset()
    {
        SetHeights();
        MarchCubes();
        SetMesh();
    }

    private void SetMesh()
    {
        Mesh mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }

    private void SetHeights()
    {
        heights = new float[width + 1, height + 1, width + 1];

        for (int x = 0; x < width + 1; x++)
        {
            for (int y = 0; y < height + 1; y++)
            {
                for (int z = 0; z < width + 1; z++)
                {
                    if (useSphere)
                    {
                        heights[x, y, z] = SphereShape(x, y, z);
                    }
                }
            }
        }
    }

    private float SphereShape(float x, float y, float z)
    {
        Vector3 center = new Vector3(width / 2, height / 2, width / 2);
        float distance = Vector3.Distance(new Vector3(x, y, z), center);

        // Add noise to the distance
        float noise = GenerateNoise(new Vector3(x, y, z), noiseScale, heightScale);
        distance += noise;

        return distance;
    }

    private float GenerateNoise(Vector3 position, float scale, float height)
    {
        float x = position.x * scale;
        float y = position.y * scale;
        float z = position.z * scale;

        return (Mathf.PerlinNoise(x, y) * height) + (Mathf.PerlinNoise(y, z) * height) + (Mathf.PerlinNoise(z, x) * height);
    }

    private int GetConfigIndex(float[] cubeCorners)
    {
        int configIndex = 0;

        for (int i = 0; i < 8; i++)
        {
            if (cubeCorners[i] > isoValue)
            {
                configIndex |= 1 << i;
            }
        }

        return configIndex;
    }

    private void MarchCubes()
    {
        vertices.Clear();
        triangles.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    float[] cubeCorners = new float[8];

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];
                        cubeCorners[i] = heights[corner.x, corner.y, corner.z];
                    }

                    MarchCube(new Vector3(x, y, z), cubeCorners);
                }
            }
        }
    }

    private void MarchCube(Vector3 position, float[] cubeCorners)
    {
        int configIndex = GetConfigIndex(cubeCorners);

        if (configIndex == 0 || configIndex == 255)
        {
            return;
        }

        int edgeIndex = 0;
        for (int t = 0; t < 5; t++)
        {
            for (int v = 0; v < 3; v++)
            {
                int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];

                if (triTableValue == -1)
                {
                    return;
                }

                Vector3 edgeStart = position + MarchingTable.Edges[triTableValue, 0];
                Vector3 edgeEnd = position + MarchingTable.Edges[triTableValue, 1];

                float A = GetValueAtPoint(edgeStart);
                float B = GetValueAtPoint(edgeEnd);

                float mu = (isoValue - A) / (B - A);

                Vector3 vertex;

                if (useInterpolation)
                    vertex = Vector3.Lerp(edgeStart, edgeEnd, mu);
                else
                    vertex = (edgeStart + edgeEnd) / 2;


                vertices.Add(vertex);
                triangles.Add(vertices.Count - 1);

                edgeIndex++;
            }
        }
    }

    float GetValueAtPoint(Vector3 point)
    {
        return heights[(int)point.x, (int)point.y, (int)point.z];
    }
}
