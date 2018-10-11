using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DataProcess : MonoBehaviour {
    public Material alphaBlending;
    public Material transparencyBlending;
    public Material visibilityBlending;

    Material material;

    public enum MaterialChoice
    {
        ALPHA, TRANSPARENCY, VISIBILITY
    }

    public MaterialChoice materialChoice;

    byte[] depthBytes;
    byte[] webcamBytes;
    byte[] positionBytes;

    MemoryStream depthMs;
    MemoryStream webcamMs;
    MemoryStream positionMs;

    Vector3 position;
    Quaternion rotation;
    new Transform transform;

    Texture2D sceneDepthTexture;
    Texture2D webcamTexture;
    RenderTexture cgDepthTexture;
    RenderTexture shadowTexture;
    RenderTexture shadowDepthTexture;

    private Camera thisCamera;
    private Camera copyCamera;
    private GameObject copyCameraGameObject;

    int frame = 0;
    string folder;
    string folderout;
    string filename;

    List<string> fileList = new List<string>();
    public Vector3 trainPosition = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 trainRotation = new Vector3(0.0f, 0.0f, 0.0f);

    Texture2D screenShot;
    bool takePicture = false;
    bool takeVideo = false;
    List<string> blendingType;
    int blendingTypeIndex;
    int frameNumberCapture;

    // Use this for initialization
    void Start() {
        int dataNumber = 14; //14, 2,
        takePicture = false;
        takeVideo = true;
        //Set framenumber capture
        if (dataNumber == 2) frameNumberCapture = 32;
        else if (dataNumber == 7) frameNumberCapture = 60;
        else if (dataNumber == 10) frameNumberCapture = 13;
        else if (dataNumber == 4) frameNumberCapture = 61;
        else if (dataNumber == 5) frameNumberCapture = 5;
        else frameNumberCapture = 80;

        blendingType = new List<string>();
        blendingType.Add("alpha");
        blendingType.Add("transparency");
        blendingType.Add("visibility");

        if (materialChoice == MaterialChoice.ALPHA)
        {
            material = alphaBlending;
            blendingTypeIndex = 0;
        }
        else if (materialChoice == MaterialChoice.TRANSPARENCY)
        {
            material = transparencyBlending;
            blendingTypeIndex = 1;
        }
        else if (materialChoice == MaterialChoice.VISIBILITY)
        {
            material = visibilityBlending;
            blendingTypeIndex = 2;
        }

        // Populate fileList
        PopulateFileList();
        
        //Open files
        transform = this.gameObject.transform;
        folder = "D:/dev/data/VRSTDataVideos";
        folderout = "D:/dev/data/VRSTResultsForVideo";
        filename = fileList[dataNumber];

        SetTrainPose();

        // Train position


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
        shadowTexture = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.ARGB32);
        shadowDepthTexture = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.Depth);

        material.SetTexture("_SceneDepthTex", sceneDepthTexture);
        material.SetTexture("_CgDepthTex", cgDepthTexture);
        material.SetTexture("_WebcamTex", webcamTexture);
        material.SetTexture("_ShadowTex", shadowTexture);
        material.SetTexture("_ShadowDepthTex", shadowDepthTexture);

        thisCamera = this.gameObject.GetComponent<Camera>();
        copyCameraGameObject = new GameObject("Depth Renderer Camera");
        copyCamera = copyCameraGameObject.AddComponent<Camera>();

        screenShot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
    }

    private void OnPreRender()
    {
        //Load each frame
        FetchPositionAndRotation();
        transform.position = position;
        transform.rotation = rotation;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        copyCamera.CopyFrom(thisCamera);
        copyCamera.targetTexture = cgDepthTexture;
        RenderTexture.active = cgDepthTexture;
        copyCamera.Render();

        copyCamera.targetTexture = shadowDepthTexture;
        RenderTexture.active = shadowDepthTexture;
        copyCamera.cullingMask = 1 << 11;
        copyCamera.Render();

        copyCamera.targetTexture = shadowTexture;
        RenderTexture.active = shadowTexture;
        copyCamera.cullingMask = 1 << 10;
        copyCamera.Render();

        FetchDepth();
        FetchWebcam();
        frame++;
        Graphics.Blit(source, destination, material);
        //Debug.Log(frame.ToString());

        // Save one image
        //if (frame == frameNumberCapture)
        //{
        //    if (takePicture)
        //    {
        //        screenShot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        //        byte[] data = screenShot.EncodeToPNG();
        //        File.WriteAllBytes(folderout + "/" + filename + "output" + blendingType[blendingTypeIndex] + frame + ".png", data);
        //    }
        //}

        // Save all images
        if ((takeVideo) && (frame < 180))
        {
            screenShot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            byte[] data = screenShot.EncodeToPNG();
            File.WriteAllBytes(folderout + "/" + blendingType[blendingTypeIndex] +  "/" + filename + "output" + blendingType[blendingTypeIndex] + frame + ".png", data);
        }


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
        if (frame == frameNumberCapture)
        {
            if (takePicture)
            {
                byte[] data = sceneDepthTexture.EncodeToPNG();
                System.IO.File.WriteAllBytes(folderout + "/" + filename + "scenedepth.png", data);
            }
        }
        if (takeVideo && (frame < 180))
        {
            byte[] data = sceneDepthTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(folderout + "/scenedepth/" + filename + "scenedepth" + frame + ".png", data);
        }
    }

    private void FetchWebcam()
    {
        byte[] rgbData = new byte[Screen.width * Screen.height * 3];
        webcamMs.Read(rgbData, 0, Screen.width * Screen.height * 3);
        webcamTexture.LoadRawTextureData(rgbData);
        webcamTexture.Apply();
        
        if (frame == frameNumberCapture)
        {
            if (takePicture)
            {
                byte[] data = webcamTexture.EncodeToPNG();
                System.IO.File.WriteAllBytes(folderout + "/" + filename + "webcam.png", data);
            }

            
        }

        if (takeVideo && (frame < 180))
        {
            byte[] data = webcamTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(folderout + "/input/" + filename + "input" + frame + ".png", data);
        }

    }

    private float ConvertToFloat(byte[] data)
    {
        return BitConverter.ToSingle(data, 0);
    }

    private void PopulateFileList()
    {
        fileList.Add("07110859"); //0
        fileList.Add("07224247"); //1
        fileList.Add("07224813"); //2
        fileList.Add("07225019"); //3
        fileList.Add("07225152"); //4
        fileList.Add("07225633"); //5
        fileList.Add("07225855"); //6
        fileList.Add("07230222"); //7
        fileList.Add("07230549"); //8
        fileList.Add("07230729"); //9
        fileList.Add("07231338"); //10
        fileList.Add("07231631"); //11
        fileList.Add("07231832"); //12
        fileList.Add("07232017"); //13
        fileList.Add("07232926"); //14
    }


    private void SetTrainPose()
    {
        GameObject train = GameObject.Find("Train");
        if (filename == "07231832")
        {
            train.transform.position = new Vector3(-2.16f, -0.7f, -2.65f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -229.25f));
            train.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
        }
        if (filename == "07231338")
        {
            train.transform.position = new Vector3(-0.07f, -0.48f, 1.45f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -214.25f));
            train.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
        }
        if (filename == "07230729")
        {
            train.transform.position = new Vector3(0.01f, -0.539f, 1.94f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -86.17f));
            train.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
        }
        if (filename == "07230549")
        {
            train.transform.position = new Vector3(-0.03f, -0.539f, 1.376f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -86.17f));
            train.transform.localScale = new Vector3(0.078f, 0.078f, 0.078f);
        }
        if (filename == "07230222")
        {
            train.transform.position = new Vector3(0.15f, -0.523f, 1.6f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -54.99f));
            train.transform.localScale = new Vector3(0.078f, 0.078f, 0.078f);
        }
        if (filename == "07225855x")
        {
            train.transform.position = new Vector3(0.35f, -0.523f, 1.55f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -54.99f));
            train.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
        }
        if (filename == "07224247")
        {
            train.transform.position = new Vector3(0.11f, -0.523f, 1.73f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -54.99f));
            train.transform.localScale = new Vector3(0.078f, 0.078f, 0.078f);
        }
        if (filename == "07232926")
        {
            train.transform.position = new Vector3(0.071f, -0.297f, 1.171f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -141.40f));
            train.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        }
        if ((filename == "07224813")|| (filename == "07225019"))
        {
            train.transform.position = new Vector3(0.271f, -0.417f, 1.57f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -100.2f));
            train.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
        }
        if (filename == "07225152")
        {
            train.transform.position = new Vector3(-0.03f, -0.41f, 1.75f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -259.26f));
            train.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
        }
        if (filename == "07225633")
        {
            train.transform.position = new Vector3(0.123f, -0.407f, 0.991f);
            train.transform.rotation = Quaternion.Euler(new Vector3(-90f, 0.0f, -90.0f));
            train.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        }

    }
    
    // Update is called once per frame
    void Update () {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 20;
    }
}
