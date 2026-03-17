using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class UdpPointCloudSaver : MonoBehaviour
{
    public int listenPort = 5005;
    private UdpClient udpClient;
    private Thread receiveThread;
    private const int FRAG_HEADER_SIZE = 8;

    private Dictionary<uint, List<byte[]>> frameFragments = new Dictionary<uint, List<byte[]>>();
    private Dictionary<uint, int> frameExpectedCounts = new Dictionary<uint, int>();
    private object udpLock = new object();

    private byte[] latestCompositeFrame = null;
    private object frameLock = new object();

    private Mesh pointCloudMesh;

    void Start()
    {
        pointCloudMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        GetComponent<MeshFilter>().mesh = pointCloudMesh;

        // Test point (small red dot)
        pointCloudMesh.vertices = new Vector3[] { Vector3.zero };
        pointCloudMesh.colors = new Color[] { Color.red };
        pointCloudMesh.SetIndices(new int[] { 0 }, MeshTopology.Points, 0);
        Debug.Log("Test point added at origin.");

        udpClient = new UdpClient(listenPort);
        receiveThread = new Thread(ReceiveUdpData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void Update()
    {
        byte[] composite = null;
        lock (frameLock)
        {
            if (latestCompositeFrame != null)
            {
                composite = latestCompositeFrame;
                latestCompositeFrame = null;
            }
        }
        if (composite != null)
        {
            float startTime = Time.realtimeSinceStartup;
            UpdatePointCloudMesh(composite);
            Debug.Log($"Processed frame in {(Time.realtimeSinceStartup - startTime):F3}s");
        }
    }

    void OnDisable()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }

    private void ReceiveUdpData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, listenPort);
        while (true)
        {
            try
            {
                byte[] packet = udpClient.Receive(ref remoteEP);
                if (packet.Length < FRAG_HEADER_SIZE) continue;

                uint frameID = BitConverter.ToUInt32(packet, 0);
                ushort totalChunks = BitConverter.ToUInt16(packet, 4);
                ushort chunkIndex = BitConverter.ToUInt16(packet, 6);

                if (chunkIndex >= totalChunks) continue;

                byte[] fragment = new byte[packet.Length - FRAG_HEADER_SIZE];
                Array.Copy(packet, FRAG_HEADER_SIZE, fragment, 0, fragment.Length);

                lock (udpLock)
                {
                    if (!frameFragments.ContainsKey(frameID))
                    {
                        frameFragments[frameID] = new List<byte[]>(new byte[totalChunks][]);
                        frameExpectedCounts[frameID] = totalChunks;
                    }

                    frameFragments[frameID][chunkIndex] = fragment;

                    bool complete = frameFragments[frameID].TrueForAll(frag => frag != null);
                    if (complete)
                    {
                        int totalLength = 0;
                        foreach (var frag in frameFragments[frameID]) totalLength += frag.Length;

                        byte[] frameData = new byte[totalLength];
                        int offset = 0;
                        foreach (var frag in frameFragments[frameID])
                        {
                            Array.Copy(frag, 0, frameData, offset, frag.Length);
                            offset += frag.Length;
                        }

                        Debug.Log($"Frame {frameID}: Reassembled {totalLength} bytes");

                        lock (frameLock)
                        {
                            latestCompositeFrame = frameData;
                        }
                        frameFragments.Remove(frameID);
                        frameExpectedCounts.Remove(frameID);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("UDP error: " + ex.Message);
            }
        }
    }

    private void UpdatePointCloudMesh(byte[] composite)
    {
        int pointSize = 15;
        int pointCount = composite.Length / pointSize;
        Vector3[] vertices = new Vector3[pointCount];
        Color[] colors = new Color[pointCount];
        int[] indices = new int[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            int offset = i * pointSize;
            float x = BitConverter.ToSingle(composite, offset);
            float y = BitConverter.ToSingle(composite, offset + 4);
            float z = BitConverter.ToSingle(composite, offset + 8);
            byte r = composite[offset + 12];
            byte g = composite[offset + 13];
            byte b = composite[offset + 14];

            vertices[i] = new Vector3(x, y, z);
            colors[i] = new Color(r / 255f, g / 255f, b / 255f);
            indices[i] = i;
        }

        pointCloudMesh.Clear();
        pointCloudMesh.vertices = vertices;
        pointCloudMesh.colors = colors;
        pointCloudMesh.SetIndices(indices, MeshTopology.Points, 0);
        Debug.Log($"Rendered {pointCount} points");
    }
}
