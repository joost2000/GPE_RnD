using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

[Serializable]
public struct helper
{
    public List<PointAndValue> chunk;
}

public class MarchingCubesGPU : MonoBehaviour
{
    [Header("Grid Size")]
    public int CubicSize;
    public int chunkSize;
    private int width;
    private int height;

    [Header("Variables")]
    [SerializeField] float noiseScale = 1;
    [SerializeField] float heightScale = 1;
    [SerializeField] private float isoValue;

    [Space(10)]
    public ComputeShader MarchingCubesShader;

    private ComputeBuffer pointsAndValuesBuffer;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();

    private MeshFilter meshFilter;

    private PointAndValue[] pointAndValues;
    private Triangle[] triangleData;

    private int totalSize;
    List<helper> helpers = new List<helper>();

    void Start()
    {
        width = CubicSize;
        height = CubicSize;

        int totalSize = width * height * width;
        pointAndValues = new PointAndValue[totalSize];

        meshFilter = GetComponent<MeshFilter>();
        MarchingCubes();

        print(SendToGPU(helpers[0].chunk.ToArray(), chunkSize));
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            Reset();
        }
    }

    private void Reset()
    {
        vertices.Clear();
        triangles.Clear();
        MarchingCubes();
    }

    void MarchingCubes()
    {
        InitializeVertices();
        // SendToGPU();
        // Triangulate();
    }

    void InitializeVertices()
    {
        Vector3 center = new Vector3(width / 2, height / 2, width / 2);

        for (int cx = 0; cx < width; cx += chunkSize)
        {
            for (int cy = 0; cy < height; cy += chunkSize)
            {
                for (int cz = 0; cz < width; cz += chunkSize)
                {
                    int index = 0;
                    PointAndValue[] chunk = new PointAndValue[chunkSize * chunkSize * chunkSize];

                    for (int x = cx; x < Mathf.Min(cx + chunkSize, width); x++)
                    {
                        for (int y = cy; y < Mathf.Min(cy + chunkSize, height); y++)
                        {
                            for (int z = cz; z < Mathf.Min(cz + chunkSize, width); z++)
                            {
                                chunk[index++] = new PointAndValue
                                {
                                    x = x,
                                    y = y,
                                    z = z,
                                    value = SphereShape(new Vector3(x, y, z), center)
                                };
                            }
                        }
                    }
                    // do something here
                    helpers.Add(new helper { chunk = new List<PointAndValue>(chunk) });
                }
            }
        }
    }

    void SendToGPU()
    {
        // Create a new ComputeBuffer to hold the triangles
        int numTriangles = totalSize * 10;
        ComputeBuffer triangleBuffer = new ComputeBuffer(numTriangles, sizeof(double) * 3 * 3, ComputeBufferType.Append);
        triangleBuffer.SetCounterValue(0);

        pointsAndValuesBuffer = new ComputeBuffer(totalSize, sizeof(double) * 4);
        pointsAndValuesBuffer.SetData(pointAndValues);

        MarchingCubesShader.SetBuffer(0, "pointAndValue", pointsAndValuesBuffer);
        MarchingCubesShader.SetBuffer(0, "triangles", triangleBuffer);

        MarchingCubesShader.SetInt("width", width);
        MarchingCubesShader.SetInt("height", height);
        MarchingCubesShader.SetFloat("isoValue", isoValue);

        MarchingCubesShader.Dispatch(0, width / 8, height / 8, width / 8);

        // Create a buffer to hold the count of triangles
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        int[] countArray = new int[1];

        // Copy the count of triangles from the triangleBuffer to the countBuffer
        ComputeBuffer.CopyCount(triangleBuffer, countBuffer, 0);
        countBuffer.GetData(countArray);
        int count = countArray[0];

        // Create an array to hold the data
        triangleData = new Triangle[count];

        // Get the data from the buffer
        triangleBuffer.GetData(triangleData, 0, 0, count);

        // Release the buffers
        triangleBuffer.Release();
        countBuffer.Release();
    }

    Triangle[] SendToGPU(PointAndValue[] pointData, int chunkSize)
    {
        int numTriangles = chunkSize * 5;
        int totalChunkSize = chunkSize * chunkSize * chunkSize;

        ComputeBuffer triangleBuffer = new ComputeBuffer(numTriangles, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        triangleBuffer.SetCounterValue(0);

        pointsAndValuesBuffer = new ComputeBuffer(totalChunkSize, sizeof(float) * 4);
        pointsAndValuesBuffer.SetData(pointData);

        MarchingCubesShader.SetBuffer(0, "pointAndValue", pointsAndValuesBuffer);
        MarchingCubesShader.SetBuffer(0, "triangles", triangleBuffer);

        MarchingCubesShader.SetInt("width", chunkSize);
        MarchingCubesShader.SetInt("height", chunkSize);
        MarchingCubesShader.SetFloat("isoValue", isoValue);

        MarchingCubesShader.Dispatch(0, chunkSize / 8, height / 8, width / 8);

        // Create a buffer to hold the count of triangles
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        int[] countArray = new int[1];

        // Copy the count of triangles from the triangleBuffer to the countBuffer
        ComputeBuffer.CopyCount(triangleBuffer, countBuffer, 0);
        countBuffer.GetData(countArray);
        int count = countArray[0];

        // Create an array to hold the data
        triangleData = new Triangle[count];

        // Get the data from the buffer
        triangleBuffer.GetData(triangleData, 0, 0, count);

        // Release the buffers
        triangleBuffer.Release();
        countBuffer.Release();

        return triangleData;
    }

    void Triangulate()
    {
        for (int i = 0; i < triangleData.Length; i++)
        {
            Triangle triangle = triangleData[i];
            vertices.Add(triangle.vertexA);
            vertices.Add(triangle.vertexB);
            vertices.Add(triangle.vertexC);

            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 2);
            triangles.Add(vertices.Count - 1);
        }

        SetMesh();
    }

    private float SphereShape(Vector3 point, Vector3 center)
    {
        float distance = Vector3.Distance(point, center);

        distance += GenerateNoise(point, noiseScale, heightScale);

        return distance;
    }

    private float GenerateNoise(Vector3 position, float scale, float height)
    {
        float x = position.x * scale;
        float y = position.y * scale;
        float z = position.z * scale;

        return (Mathf.PerlinNoise(x, y) * height) + (Mathf.PerlinNoise(y, z) * height) + (Mathf.PerlinNoise(z, x) * height);
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

    private void OnDrawGizmosSelected()
    {
        foreach (var item in helpers)
        {
            Vector3 startPoint;
            Vector3 endPoint;

            startPoint = new Vector3(item.chunk[0].x, item.chunk[0].y, item.chunk[0].z);
            endPoint = new Vector3(item.chunk[item.chunk.Count - 1].x, item.chunk[item.chunk.Count - 1].y, item.chunk[item.chunk.Count - 1].z);

            Vector3 center = (startPoint + endPoint) / 2;
            Gizmos.DrawWireCube(center, new Vector3(chunkSize, chunkSize, chunkSize));
        }
    }
}
