using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class CameraControl : MonoBehaviour
{
    //path to directory to find screenshots in
    //structure of directory should look like:
    // root
    // - label/scene
    //   - Depth
    //   - RGB
    // - ...
    [SerializeField] private string trainingDirectory;

    //vector to move camera along for displacement
    [SerializeField] private Vector3 displacementVector;
    [SerializeField] private Material renderMaterial;

    private bool isRendering = false;
    private int renderIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //screenshot
        if (Input.GetKeyDown("space"))
        {
            Debug.Log("*snap!*");
            ScreenCapture.CaptureScreenshot(""+Time.time+".png", 4);
        }

        //generate reprojection data
        if (Input.GetKeyDown("return"))
        {
            isRendering = true;
            Debug.Log("Generating reprojections for files in "+trainingDirectory);
            GenerateReprojections();
        }

        if (isRendering)
        {

        }
    }

    void GenerateReprojections()
    {
        Debug.Log("Generating reprojection data...");
        //get reference to text file of the material and edit GUID?
        //use Material functions to set textures?
            //i.e. rend.material.SetTexture("_MainTex", myTexture);


        //get relevant directories
        var subfolders = AssetDatabase.GetSubFolders(trainingDirectory);

        int poo = 0;

        //each subfolder contains RGB, D
        foreach (string subfolder in subfolders)
        {
            //set the main and depth texture of the sphere
            string label = subfolder.Substring(subfolder.LastIndexOf("/")+1);
            Debug.Log("RGB image path: "+subfolder+"/"+label+"RGB.png");
            Debug.Log("Depth image path: "+subfolder+"/"+label+"Depth.png");
            Texture2D newRGBTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(subfolder+"/"+label+"RGB.png", typeof(Texture2D));
            Texture2D newDTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(subfolder+"/"+label+"Depth.png", typeof(Texture2D));
            renderMaterial.SetTexture("_MainTex", newRGBTexture);
            renderMaterial.SetTexture("_DepthTex", newDTexture);

            //move the camera and render it out
            Camera.main.Render();
            
            ScreenCapture.CaptureScreenshot(subfolder+"/"+label+"test.png", 4);
            AssetDatabase.Refresh();
            //create new texture, have camera weite to that texture?

            poo++;
            if (poo==3)
                return;
        }
    }
}
