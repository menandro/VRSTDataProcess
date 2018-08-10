using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DataProcess : MonoBehaviour {
    public Material material;

    byte[] depthBytes;
    byte[] webcamBytes;
    byte[] positionBytes;

    MemoryStream depthMs;
    MemoryStream webcamMs;
    MemoryStream positionMs;

    Vector3 position;
    Quaternion rotation;
    Transform transform;

    Texture2D sceneDepthTexture;
    Texture2D webcamTexture;
    RenderTexture cgDepthTexture;

    private Camera thisCamera;
    private Camera copyCamera;
    private GameObject copyCameraGameObject;

    int frame = 0;
    string folder;
    string folderout;
    string filename;

    // Use this for initialization
    void Start() {
        //Open files
        transform = this.gameObject.transform;
        folder = "D:/dev/data/VRSTDataVideos";
        folderout = "D:/dev/data/VRSTDataScreenShot";
        filename = "07232926";

        //Read files
        positionBytes = File.ReadAllBytes(folder + "/" + filename + "position180.dat");
        depthMs = new MemoryStream();
        webcamMs = new MemoryStream();

        for (int i =0; i< 3; i++)
        {
            byte[] data = File.ReadAllBytes(folder + "/" + filename + "scenedepth" + i.ToString() + ".dat");
            depthMs.Write(data, 0, data.Length);
        }
        
        for (int i = 0; i < 3; i++)
        {
            byte[] data = File.ReadAllBytes(folder + "/" + filename + "webcam" + i.ToString() + ".dat");
            webcamMs.Write(data, 0, data.Length);
        }
        depthMs.Seek(0, 0);
        webcamMs.Seek(0, 0);

        positionMs = new MemoryStream(positionBytes);

        position = new Vector3(0.0f, 0.0f, 0.0f);
        rotation = new Quaternion(0.0f, 0.0f, 0.0f, 0.0f);

        sceneDepthTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.R8, false);
        webcamTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        cgDepthTexture = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.Depth);

        material.SetTexture("_SceneDepthTex", sceneDepthTexture);
        material.SetTexture("_CgDepthTex", cgDepthTexture);
        material.SetTexture("_WebcamTex", webcamTexture);

        thisCamera = this.gameObject.GetComponent<Camera>();
        copyCameraGameObject = new GameObject("Depth Renderer Camera");
        copyCamera = copyCameraGameObject.AddComponent<Camera>();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        //Load each frame
        FetchPositionAndRotation();
        transform.position = position;
        transform.rotation = rotation;

        copyCamera.CopyFrom(thisCamera);
        copyCamera.targetTexture = cgDepthTexture;
        RenderTexture.active = cgDepthTexture;
        copyCamera.Render();

        FetchDepth();
        FetchWebcam();
        frame++;
        Graphics.Blit(source, destination, material);
    }

    private void FetchPositionAndRotation()
    {
        byte[] floatData = new byte[4];

        positionMs.Read(floatData, 0, 4);
        position.x = ConvertToFloat(floatData);

        positionMs.Read(floatData, 0, 4);
        position.y = ConvertToFloat(floatData);

        positionMs.Read(floatData, 0, 4);
        position.z = ConvertToFloat(floatData);

        positionMs.Read(floatData, 0, 4);
        rotation.w = ConvertToFloat(floatData);

        positionMs.Read(floatData, 0, 4);
        rotation.x = ConvertToFloat(floatData);

        positionMs.Read(floatData, 0, 4);
        rotation.y = ConvertToFloat(floatData);

        positionMs.Read(floatData, 0, 4);
        rotation.z = ConvertToFloat(floatData);
    }

    private void FetchDepth()
    {
        byte[] depthData = new byte[Screen.width * Screen.height];
        depthMs.Read(depthData, 0, Screen.width * Screen.height);
        sceneDepthTexture.LoadRawTextureData(depthData);
        sceneDepthTexture.Apply();
        if (frame == 80)
        {
            byte[] data = sceneDepthTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(folderout + "/" + filename + "scenedepth.png", data);

        }
    }

    private void FetchWebcam()
    {
        byte[] rgbData = new byte[Screen.width * Screen.height * 3];
        webcamMs.Read(rgbData, 0, Screen.width * Screen.height * 3);
        webcamTexture.LoadRawTextureData(rgbData);
        webcamTexture.Apply();
        
        if (frame == 80)
        {
            byte[] data = webcamTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(folderout + "/" + filename + "webcam.png", data);
        }
        
    }

    private float ConvertToFloat(byte[] data)
    {
        return BitConverter.ToSingle(data, 0);
    }

    // Update is called once per frame
    void Update () {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

    }
}
