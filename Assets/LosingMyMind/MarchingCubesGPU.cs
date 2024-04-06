using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
struct Triangle
{
    public Vector3 p1;
    public Vector3 p2;
    public Vector3 p3;
}
[Serializable]
struct ChunkData
{
    public List<Triangle> chunk;
}

enum AmountOfChunks
{
    Chunk4 = 4,
    Chunk8 = 8,
    Chunk16 = 16,
    Chunk32 = 32
}

enum Resolution
{
    Resolution16 = 16,
    Resolution32 = 32,
    Resolution64 = 64,
    Resolution128 = 128,
    Resolution256 = 256,
    Resolution512 = 512
}

[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer))]
public class MarchingCubesGPU : MonoBehaviour
{
    [Header("Variables")]
    public int resolution;
    public float isoLevel;
    [SerializeField] AmountOfChunks amountOfChunks;
    [SerializeField] Material material;
    [SerializeField] bool visualizeGizmos;

    [Header("Gameplay")]
    [SerializeField] GameObject planetPivot;
    [SerializeField] Transform planetContainer;

    [Header("ComputeShader")]
    public ComputeShader computeShader;
    public ComputeShader computeTriangles;

    [Header("Debugging")]
    Triangle[] tris;
    [SerializeField] List<Triangle[]> triangleChunks = new List<Triangle[]>();
    Vector4[] voxelData;
    Vector4[] testing;

    ComputeBuffer triangleBuffer;
    ComputeBuffer voxelDataBuffer;
    ComputeBuffer positionalDataBuffer;
    ComputeBuffer triCountBuffer;

    private void Start()
    {
        voxelData = MakeVoxelDataCPU().ToArray();
        CreateChunks();

    }

    void CreateChunks()
    {
        for (int x = 0; x < (int)amountOfChunks / 4; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < (int)amountOfChunks / 4; z++)
                {
                    Vector2Int xBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * x, resolution / ((int)amountOfChunks / 4) * (x + 1));
                    Vector2Int yBeginEnd = new Vector2Int(resolution / 2 * y, resolution / 2 * (y + 1));
                    Vector2Int zBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * z, resolution / ((int)amountOfChunks / 4) * (z + 1));
                    if (x == (int)amountOfChunks / 4 - 1)
                        xBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * x, resolution / ((int)amountOfChunks / 4) * (x + 1) - 1);
                    if (y == 1)
                        yBeginEnd = new Vector2Int(resolution / 2 * y, resolution / 2 * (y + 1) - 1);
                    if (z == (int)amountOfChunks / 4 - 1)
                        zBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * z, resolution / ((int)amountOfChunks / 4) * (z + 1) - 1);

                    print($"{xBeginEnd} | {yBeginEnd} | {zBeginEnd}");
                    CalculateTris(xBeginEnd.x, xBeginEnd.y, yBeginEnd.x, yBeginEnd.y, zBeginEnd.x, zBeginEnd.y);
                    SetMesh();
                }
            }
        }
    }

    List<Vector4> MakeVoxelDataCPU()
    {
        List<Vector4> data = new List<Vector4>();
        Vector3 center = Vector3.one * (resolution / 2);
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    data.Add(new Vector4(x, y, z, Vector3.Distance(center, new Vector3(x, y, z))));
                }
            }
        }
        return data;
    }

    void MakeVoxelData()
    {
        int totalSize = (int)resolution * (int)resolution * (int)resolution;
        voxelDataBuffer = new ComputeBuffer(totalSize, sizeof(float) * 4, ComputeBufferType.Default);

        voxelDataBuffer.SetData(new Vector4[totalSize]);

        computeShader.SetBuffer(0, "voxelData", voxelDataBuffer);
        computeShader.SetInt("size", (int)resolution);
        computeShader.Dispatch(0, (int)resolution / 8, (int)resolution / 8, (int)resolution / 8);

        voxelData = new Vector4[totalSize];

        voxelDataBuffer.GetData(voxelData);

        voxelDataBuffer.Release();
    }

    private void OnDrawGizmos()
    {
        if (!visualizeGizmos)
            return;

        foreach (var item in voxelData)
        {
            Gizmos.DrawWireSphere(new Vector3(item.x, item.y, item.z), 0.1f);
        }
    }

    void CalculateTris(int beginChunkX, int endChunkX, int beginChunkY, int endChunkY, int beginChunkZ, int endChunkZ)
    {
        int totalSize = (int)resolution * (int)resolution * (int)resolution;
        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        positionalDataBuffer = new ComputeBuffer(totalSize, sizeof(float) * 4, ComputeBufferType.Default);
        positionalDataBuffer.SetData(voxelData);

        triangleBuffer = new ComputeBuffer(5 * totalSize, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        triangleBuffer.SetCounterValue(0);

        computeTriangles.SetBuffer(0, "triangleBuffer", triangleBuffer);
        computeTriangles.SetBuffer(0, "positionalData", positionalDataBuffer);
        computeTriangles.SetInt("size", (int)resolution);
        computeTriangles.SetFloat("isoLevel", isoLevel);
        computeTriangles.SetInt("width", (int)resolution / 3 * 2);
        computeTriangles.SetInt("beginChunkX", beginChunkX);
        computeTriangles.SetInt("endChunkX", endChunkX);
        computeTriangles.SetInt("beginChunkY", beginChunkY);
        computeTriangles.SetInt("endChunkY", endChunkY);
        computeTriangles.SetInt("beginChunkZ", beginChunkZ);
        computeTriangles.SetInt("endChunkZ", endChunkZ);
        computeTriangles.SetInt("height", resolution);
        computeTriangles.SetInt("depth", resolution);

        computeTriangles.Dispatch(0, (int)resolution / 8, (int)resolution / 8, (int)resolution / 8);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        // Get triangle data from shader
        tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris);

        triangleChunks.Add(tris);

        testing = new Vector4[totalSize];
        positionalDataBuffer.GetData(testing);

        positionalDataBuffer.Release();
        triangleBuffer.Release();
        triCountBuffer.Release();
    }


    void SetMesh()
    {
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var vertices = new Vector3[tris.Length * 3];
        var meshTriangles = new int[tris.Length * 3];

        // Create a new GameObject
        GameObject newGameObject = new GameObject("Chunk");

        newGameObject.transform.parent = planetContainer;
        newGameObject.transform.position = Vector3.one * -(resolution / 2);

        // Add a MeshFilter
        MeshFilter meshFilter = newGameObject.AddComponent<MeshFilter>();

        // Add a MeshRenderer
        MeshRenderer meshRenderer = newGameObject.AddComponent<MeshRenderer>();

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int i = 0; i < tris.Length; i++)
        {
            vertices[vertexIndex] = tris[i].p3;
            vertices[vertexIndex + 1] = tris[i].p2;
            vertices[vertexIndex + 2] = tris[i].p1;

            meshTriangles[triangleIndex] = vertexIndex;
            meshTriangles[triangleIndex + 1] = vertexIndex + 1;
            meshTriangles[triangleIndex + 2] = vertexIndex + 2;

            vertexIndex += 3;
            triangleIndex += 3;
        }

        mesh.vertices = vertices;
        mesh.triangles = meshTriangles;
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
        meshRenderer.material = material;
    }

}
