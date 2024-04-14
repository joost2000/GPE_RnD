using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
struct ChunkInfo
{
    public int chunkId;
    public Vector3[] voxelPoints;
}

public class SetHeightsParallel : MonoBehaviour
{
    public int gridSize;
    public int chunkSize;

    List<Vector3> voxelData = new List<Vector3>();
    List<Vector3> parallelVoxelData = new List<Vector3>();
    List<Vector3> GPUvoxelData = new List<Vector3>();

    public ComputeShader generateVoxelDataShader;

    void Start()
    {
        GenerateVoxelData();
        //GenerateVoxelDataParallel();
        //GenerateVoxelDataGPU();
        //GenerateVoxelDataChunk();
    }

    void GenerateVoxelData()
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    voxelData.Add(new Vector3(x, y, z));
                }
            }
        }

        print(voxelData.Count);

        ChunkInfo _ = new ChunkInfo()
        {
            chunkId = 0,
            voxelPoints = voxelData.ToArray()
        };

        Debug.Log(JsonUtility.ToJson(_));
        System.IO.File.WriteAllText(Application.persistentDataPath + "/data.json",
        System.IO.File.ReadAllText(Application.persistentDataPath + "/data.json") + JsonUtility.ToJson(_));

        stopwatch.Stop();
        Debug.Log("Time taken: " + stopwatch.ElapsedMilliseconds + " ms");
    }

    void GenerateVoxelDataParallel()
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        object lockObject = new object();
        int totalSize = gridSize * gridSize * gridSize;
        Parallel.For(0, totalSize, i =>
        {
            int x = i / (gridSize * gridSize);
            int y = (i / gridSize) % gridSize;
            int z = i % gridSize;

            lock (lockObject)
            {
                parallelVoxelData.Add(new Vector3(x, y, z));
            }
        });

        print(parallelVoxelData.Count);

        stopwatch.Stop();
        Debug.Log("Time taken: " + stopwatch.ElapsedMilliseconds + " ms");
    }

    void GenerateVoxelDataGPU()
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        ComputeBuffer voxelDataBuffer = new ComputeBuffer(gridSize * gridSize * gridSize, sizeof(float) * 3, ComputeBufferType.Default);
        generateVoxelDataShader.SetBuffer(0, "voxelData", voxelDataBuffer);
        generateVoxelDataShader.SetInt("gridSize", gridSize);
        generateVoxelDataShader.Dispatch(0, gridSize / 8, gridSize / 8, gridSize / 8);
        Vector3[] _ = new Vector3[gridSize * gridSize * gridSize];
        voxelDataBuffer.GetData(_);
        GPUvoxelData = _.ToList();

        voxelDataBuffer.Dispose();
        print(GPUvoxelData.Count);

        stopwatch.Stop();
        Debug.Log("Time taken: " + stopwatch.ElapsedMilliseconds + " ms");
    }

    void GenerateVoxelDataChunk()
    {
        int slices = 2;
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        for (int i = 0; i < slices; i++)
        {
            ComputeBuffer voxelDataBuffer = new ComputeBuffer((gridSize * gridSize * gridSize) / slices, sizeof(float) * 3, ComputeBufferType.Default);
            generateVoxelDataShader.SetBuffer(0, "voxelData", voxelDataBuffer);
            generateVoxelDataShader.SetInt("gridSize", gridSize);
            generateVoxelDataShader.Dispatch(0, gridSize / 8, gridSize / 8, gridSize / 8);
            Vector3[] _ = new Vector3[gridSize * gridSize * gridSize / 2];
            voxelDataBuffer.GetData(_);
            GPUvoxelData = _.ToList();

            voxelDataBuffer.Dispose();
            print(GPUvoxelData.Count);

            stopwatch.Stop();
            Debug.Log("Time taken: " + stopwatch.ElapsedMilliseconds + " ms");
        }
    }
}
