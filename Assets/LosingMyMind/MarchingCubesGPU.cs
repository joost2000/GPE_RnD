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

[Serializable]
struct ChunkBeginEnd
{
    public Vector2Int xBeginEnd;
    public Vector2Int yBeginEnd;
    public Vector2Int zBeginEnd;
    public GameObject chunk;
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

    [Header("Noise variables")]
    [SerializeField] float noiseScale;
    [SerializeField] float heightScale;

    [Header("Gameplay")]
    [SerializeField] GameObject planetPivot;
    [SerializeField] Transform planetContainer;

    [Header("ComputeShader")]
    public ComputeShader computeShader;
    public ComputeShader computeTriangles;

    [Header("Debugging")]
    Triangle[] tris;
    [SerializeField] List<Triangle[]> triangleChunks = new List<Triangle[]>();
    [SerializeField] List<ChunkBeginEnd> chunkBeginEnds = new List<ChunkBeginEnd>();
    [SerializeField] List<GameObject> chunkGameObjects = new List<GameObject>();
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

    void OnEnable()
    {
        PlayerScript.OnMarchingCubesEvent += UpdateMesh;
    }

    void OnDisable()
    {
        PlayerScript.OnMarchingCubesEvent -= UpdateMesh;
    }

    int indexFromCoord(int x, int y, int z)
    {
        return z * resolution * resolution + y * resolution + x;
    }

    void UpdateMesh(Vector3 voxel, int updatedIsoValue)
    {
        int index = indexFromCoord((int)voxel.z, (int)voxel.y, (int)voxel.x);
        Vector3 vectorData = new Vector3(voxelData[index].z, voxelData[index].y, voxelData[index].x);
        voxelData[index].w = updatedIsoValue;
        GameObject chunkToUpdate = null;

        for (int i = 0; i < chunkBeginEnds.Count; i++)
        {
            if (vectorData.x >= chunkBeginEnds[i].xBeginEnd.x && vectorData.x < chunkBeginEnds[i].xBeginEnd.y)
            {
                if (vectorData.y >= chunkBeginEnds[i].yBeginEnd.x && vectorData.y < chunkBeginEnds[i].yBeginEnd.y)
                {
                    if (vectorData.z >= chunkBeginEnds[i].zBeginEnd.x && vectorData.z < chunkBeginEnds[i].zBeginEnd.y)
                    {
                        CalculateTris(chunkBeginEnds[i].xBeginEnd.x,
                        chunkBeginEnds[i].xBeginEnd.y,
                        chunkBeginEnds[i].yBeginEnd.x,
                        chunkBeginEnds[i].yBeginEnd.y,
                        chunkBeginEnds[i].zBeginEnd.x,
                        chunkBeginEnds[i].zBeginEnd.y);

                        chunkToUpdate = chunkBeginEnds[i].chunk;
                        break;
                    }
                }
            }
        }

        // Vector3[] corners = new Vector3[8];

        // for (int i = 0; i < MarchingTable.Corners.Length; i++)
        // {
        //     Vector3 currentCorner = vectorData + MarchingTable.Corners[i];
        //     int cornerIndex = indexFromCoord((int)currentCorner.z, (int)currentCorner.y, (int)currentCorner.x);
        //     voxelData[cornerIndex].w = updatedIsoValue;
        //     corners[i] = currentCorner;
        // }

        // foreach (var item in corners)
        // {
        //     for (int i = 0; i < chunkBeginEnds.Count; i++)
        //     {
        //         if (item.x >= chunkBeginEnds[i].xBeginEnd.x && item.x < chunkBeginEnds[i].xBeginEnd.y)
        //         {
        //             if (item.y >= chunkBeginEnds[i].yBeginEnd.x && item.y < chunkBeginEnds[i].yBeginEnd.y)
        //             {
        //                 if (item.z >= chunkBeginEnds[i].zBeginEnd.x && item.z < chunkBeginEnds[i].zBeginEnd.y)
        //                 {
        //                     CalculateTris(chunkBeginEnds[i].xBeginEnd.x,
        //                     chunkBeginEnds[i].xBeginEnd.y,
        //                     chunkBeginEnds[i].yBeginEnd.x,
        //                     chunkBeginEnds[i].yBeginEnd.y,
        //                     chunkBeginEnds[i].zBeginEnd.x,
        //                     chunkBeginEnds[i].zBeginEnd.y);

        //                     chunkToUpdate = chunkBeginEnds[i].chunk;
        //                     print(i);
        //                     print("reached here, break");
        //                     break;
        //                 }
        //             }
        //         }
        //     }
        // }


        if (!chunkToUpdate)
        {
            print("yo you fucked up");
            return;
        }

        var vertices = new Vector3[tris.Length * 3];
        var meshTriangles = new int[tris.Length * 3];

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
        MeshFilter meshToUpdate = chunkToUpdate.GetComponent<MeshFilter>();
        MeshCollider meshColliderToUpdate = chunkToUpdate.GetComponent<MeshCollider>();
        meshToUpdate.mesh.Clear();
        meshToUpdate.mesh.vertices = vertices;
        meshToUpdate.mesh.triangles = meshTriangles;
        meshToUpdate.mesh.RecalculateNormals();
        meshColliderToUpdate.sharedMesh = meshToUpdate.mesh;
    }

    void CreateChunks()
    {
        for (int x = 0; x < (int)amountOfChunks / 4; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < (int)amountOfChunks / 4; z++)
                {
                    Vector2Int _xBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * x, resolution / ((int)amountOfChunks / 4) * (x + 1));
                    Vector2Int _yBeginEnd = new Vector2Int(resolution / 2 * y, resolution / 2 * (y + 1));
                    Vector2Int _zBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * z, resolution / ((int)amountOfChunks / 4) * (z + 1));
                    if (x == (int)amountOfChunks / 4 - 1)
                        _xBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * x, resolution / ((int)amountOfChunks / 4) * (x + 1) - 1);
                    if (y == 1)
                        _yBeginEnd = new Vector2Int(resolution / 2 * y, resolution / 2 * (y + 1) - 1);
                    if (z == (int)amountOfChunks / 4 - 1)
                        _zBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * z, resolution / ((int)amountOfChunks / 4) * (z + 1) - 1);

                    CalculateTris(_xBeginEnd.x, _xBeginEnd.y, _yBeginEnd.x, _yBeginEnd.y, _zBeginEnd.x, _zBeginEnd.y);
                    SetMesh();
                    chunkBeginEnds.Add(new ChunkBeginEnd()
                    {
                        xBeginEnd = _xBeginEnd,
                        yBeginEnd = _yBeginEnd,
                        zBeginEnd = _zBeginEnd,
                        chunk = planetContainer.GetChild(planetContainer.childCount - 1).gameObject
                    });
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
                    float value = SphereShape(x, y, z, center);
                    data.Add(new Vector4(x, y, z, value));
                }
            }
        }
        return data;
    }

    private float SphereShape(float x, float y, float z, Vector3 center)
    {
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

        // Add a MeshFilter
        MeshFilter meshFilter = newGameObject.AddComponent<MeshFilter>();

        // Add a MeshRenderer
        MeshRenderer meshRenderer = newGameObject.AddComponent<MeshRenderer>();

        MeshCollider meshCollider = newGameObject.AddComponent<MeshCollider>();

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
        meshCollider.sharedMesh = mesh;
    }

}
