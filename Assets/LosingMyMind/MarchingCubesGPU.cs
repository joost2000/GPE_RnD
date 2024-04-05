using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
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
    public List<Vector4> chunk;
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
    [SerializeField] int resolution;
    public float isoLevel;
    [SerializeField] AmountOfChunks amountOfChunks;
    [SerializeField] Material material;

    [Header("ComputeShader")]
    public ComputeShader computeShader;
    public ComputeShader computeTriangles;

    [Header("Debugging")]
    [SerializeField] Triangle[] tris;
    Vector4[] voxelData;
    Vector4[] testing;
    [SerializeField] List<ChunkData> chunks;
    [SerializeField] Vector4[] chunkData;

    ComputeBuffer triangleBuffer;
    ComputeBuffer voxelDataBuffer;
    ComputeBuffer positionalDataBuffer;
    ComputeBuffer triCountBuffer;

    private void Start()
    {
        MakeVoxelData();
        SliceVoxelData(voxelData);
        // SetMeshChunk(CalculateTrisChunk(chunks[0].chunk.ToArray(), 0, resolution / 2, 1));
        // SetMeshChunk(CalculateTrisChunk(chunks[1].chunk.ToArray(), 0, resolution, 1));
        SetMeshChunk(CalculateTrisChunk(chunks[0].chunk.ToArray(), 0, resolution / 2, 1));
        //CalculateTris();

    }

    private void FixedUpdate()
    {
    }

    void SetMeshChunk(Triangle[] chunkData)
    {
        int index = 0;
        foreach (var item in chunkData)
        {
            if (item.p1.z < 12 || item.p2.z < 12 || item.p3.z < 12)
            {
                print($"p1: {item.p1} p2: {item.p2} p3: {item.p3} index: {index}");
            }
            index++;
        }

        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var vertices = new Vector3[chunkData.Length * 3];
        var meshTriangles = new int[chunkData.Length * 3];

        // Create a new GameObject
        GameObject newGameObject = new GameObject("Chunk");

        // Add a MeshFilter component to the GameObject
        MeshFilter meshFilter = newGameObject.AddComponent<MeshFilter>();

        // Add a MeshRenderer component to the GameObject
        MeshRenderer meshRenderer = newGameObject.AddComponent<MeshRenderer>();

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int i = 0; i < chunkData.Length; i++)
        {
            vertices[vertexIndex] = chunkData[i].p1;
            vertices[vertexIndex + 1] = chunkData[i].p2;
            vertices[vertexIndex + 2] = chunkData[i].p3;

            meshTriangles[triangleIndex] = vertexIndex;
            meshTriangles[triangleIndex + 1] = vertexIndex + 1;
            meshTriangles[triangleIndex + 2] = vertexIndex + 2;

            vertexIndex += 3;
            triangleIndex += 3;
        }

        mesh.vertices = vertices;
        mesh.triangles = meshTriangles;
        mesh.RecalculateNormals();
        meshRenderer.sharedMaterial = material;
        meshFilter.mesh = mesh;
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


    void SliceVoxelData(Vector4[] data)
    {
        int chunkLength = resolution / 2;
        if (chunkLength % 3 == 0)
        {
            for (int i = 0; i < data.Length; i++)
            {
                Vector4 currentData = data[i];
                if (currentData.z >= chunkLength) chunks[0].chunk.Add(voxelData[i]);
                if (currentData.z < chunkLength + 1) chunks[1].chunk.Add(voxelData[i]);
                // if (currentData.x < chunkLength && currentData.z >= chunkLength) chunks[1].chunk.Add(i);
                // if (currentData.x >= chunkLength && currentData.z < chunkLength) chunks[2].chunk.Add(i);
                // if (currentData.x < chunkLength && currentData.z < chunkLength) chunks[3].chunk.Add(i);
            }
        }
    }

    Triangle[] CalculateTrisChunk(Vector4[] chunkData, float beginChunk, float endChunk, int offset)
    {
        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        print(chunkData.Length);

        positionalDataBuffer = new ComputeBuffer(chunkData.Length, sizeof(float) * 4, ComputeBufferType.Default);
        positionalDataBuffer.SetData(chunkData);

        triangleBuffer = new ComputeBuffer(5 * chunkData.Length, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        triangleBuffer.SetCounterValue(0);

        computeTriangles.SetBuffer(0, "triangleBuffer", triangleBuffer);
        computeTriangles.SetBuffer(0, "positionalData", positionalDataBuffer);
        computeTriangles.SetInt("size", resolution);
        computeTriangles.SetFloat("isoLevel", isoLevel);
        computeTriangles.SetFloat("beginChunk", beginChunk);
        computeTriangles.SetFloat("endChunk", endChunk);
        computeTriangles.Dispatch(0, (int)resolution / 8, (int)resolution / 8, (int)resolution / 8);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        // Get triangle data from shader
        Triangle[] tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris);

        // testing = new Vector4[chunkSize];
        // positionalDataBuffer.GetData(testing);

        positionalDataBuffer.Release();
        triangleBuffer.Release();
        triCountBuffer.Release();

        return tris;
    }


    private void OnDrawGizmos()
    {
        if (voxelData == null)
            return;

        foreach (var item in chunks)
        {
            foreach (var chunk in item.chunk)
            {
                if (chunk.w < isoLevel)
                {
                    Gizmos.DrawSphere(chunk, 0.1f);
                }
                else
                {
                    Gizmos.DrawWireSphere(chunk, 0.1f);
                }
            }
        }
    }

    void CalculateTris()
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
        computeTriangles.Dispatch(0, (int)resolution / 8, (int)resolution / 8, (int)resolution / 8);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        // Get triangle data from shader
        tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris);

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

        // Add a MeshFilter component to the GameObject
        MeshFilter meshFilter = newGameObject.AddComponent<MeshFilter>();

        // Add a MeshRenderer component to the GameObject
        MeshRenderer meshRenderer = newGameObject.AddComponent<MeshRenderer>();

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int i = 0; i < tris.Length; i++)
        {
            vertices[vertexIndex] = tris[i].p1;
            vertices[vertexIndex + 1] = tris[i].p2;
            vertices[vertexIndex + 2] = tris[i].p3;

            meshTriangles[triangleIndex] = vertexIndex;
            meshTriangles[triangleIndex + 1] = vertexIndex + 1;
            meshTriangles[triangleIndex + 2] = vertexIndex + 2;

            vertexIndex += 3;
            triangleIndex += 3;
        }

        mesh.vertices = vertices;
        mesh.triangles = meshTriangles;
        mesh.RecalculateNormals();
        gameObject.GetComponent<MeshFilter>().mesh = mesh;
    }

}
