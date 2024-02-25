using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using BodhiDonselaar;

public class NovelViewGeneration : MonoBehaviour
{
    //path to directory to find screenshots in
    //structure of directory should look like:
    // root
    // - label/scene
    //   - Depth
    //   - RGB
    // - ...
    [SerializeField] private string trainingDirectory;
    [SerializeField] private string saveLocation;
    private string[] subfolders;
    [SerializeField] private Material renderMaterial;

    private bool isRendering = false;
    private int renderIndex = 0;

    DepthEquiCam depthEquiCam;

    // Start is called before the first frame update
    void Start()
    {
        subfolders = AssetDatabase.GetSubFolders(trainingDirectory);
        depthEquiCam = GetComponent<DepthEquiCam>();
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
            Debug.Log("Generating novel view information...");
            isRendering = true;
        }

        if (isRendering)
        {
            Debug.Log("Generating reprojections for files in "+trainingDirectory);
            GenerateReprojections();
            renderIndex++;
            if (renderIndex >= subfolders.Length) // 5) //
            {
                isRendering = false;
                AssetDatabase.Refresh();
                Debug.Log("Done generating novel view information!");
            }
        }
    }

    // Generates reprojections for one scene in the directory.
    // One set of reprojections is generated per step in-game.
    void GenerateReprojections()
    {
        //each subfolder contains RGB and depth ground truth
        string subfolder = subfolders[renderIndex];

        //set the main and depth texture of the sphere
        string label = subfolder.Substring(subfolder.LastIndexOf("/")+1);
        Debug.Log("RGB image path: "+subfolder+"/"+label+"RGB.png");
        Debug.Log("Depth image path: "+subfolder+"/"+label+"Depth.png");
        Texture2D originRGB = (Texture2D)AssetDatabase.LoadAssetAtPath(subfolder+"/"+label+"RGB.jpg", typeof(Texture2D));
        Texture2D originDepth = (Texture2D)AssetDatabase.LoadAssetAtPath(subfolder+"/"+label+"Depth.png", typeof(Texture2D));
        renderMaterial.SetTexture("_MainTex", originRGB);
        renderMaterial.SetTexture("_DepthTex", originDepth);

        // TODO: loop through and generate multiple novel views
        Vector3[] testShiftVectors = new []
        {
            new Vector3(0.15f, 0.00f, 0.00f),
            new Vector3(0.00f, 0.15f, 0.00f),
            new Vector3(0.00f, 0.00f, 0.15f),
            new Vector3(-0.15f, 0.00f, 0.00f),
            new Vector3(0.00f, -0.15f, 0.00f),
            new Vector3(0.00f, 0.00f, -0.15f)
        };
        string[] shiftDirections = new [] {"PosX", "PosY", "PosZ", "NegX", "NegY", "NegZ"};

        for (int displacementIndex=0; displacementIndex<6; displacementIndex++)
        {
            //generate 6 novel views--one for each direction from the origin
            Vector3 displacementVector = testShiftVectors[displacementIndex];

            //move the camera and render out displaced depth
            depthEquiCam.enabled = true;
            RenderTexture displacedDepthRT = new RenderTexture(originRGB.width, originRGB.height, 16, RenderTextureFormat.ARGB32); 
            Camera.main.targetTexture = displacedDepthRT;
            Camera.main.gameObject.transform.position = displacementVector;
            Camera.main.Render();
            //save displaced depth texture
            Texture2D displacedDepth = new Texture2D(originRGB.width, originRGB.height);
            RenderTexture.active = displacedDepthRT;
            displacedDepth.ReadPixels(new Rect(0, 0, displacedDepthRT.width, displacedDepthRT.height), 0, 0);
            displacedDepth.Apply();

            //Render out displaced color
            depthEquiCam.enabled = false;
            RenderTexture displacedRGBRT = new RenderTexture(originRGB.width, originRGB.height, 16, RenderTextureFormat.ARGB32); 
            Camera.main.targetTexture = displacedRGBRT;
            Camera.main.gameObject.transform.position = displacementVector;
            Camera.main.Render();
            //save displaced rgb texture
            Texture2D displacedRGB = new Texture2D(originRGB.width, originRGB.height);
            RenderTexture.active = displacedRGBRT;
            displacedRGB.ReadPixels(new Rect(0, 0, displacedRGBRT.width, displacedRGBRT.height), 0, 0);
            displacedRGB.Apply();

            //fill out shadow map based on origin and displaced depth
            Texture2D shadowMap = new Texture2D(displacedDepth.width, displacedDepth.height);
            CreateShadowMap(originDepth, displacedDepth, displacementVector, shadowMap);

            //save the shadow map
            string path = saveLocation+"/"+label+"/"+label+"ShadowMask"+shiftDirections[displacementIndex]+".png";
            System.IO.File.WriteAllBytes(path, shadowMap.EncodeToPNG());
            //save displaced RGB
            path = saveLocation+"/"+label+"/"+label+"DisplacedRGB"+shiftDirections[displacementIndex]+".jpg";
            System.IO.File.WriteAllBytes(path, displacedRGB.EncodeToPNG());
            //save displaced Depth
            path = saveLocation+"/"+label+"/"+label+"DisplacedDepth"+shiftDirections[displacementIndex]+".png";
            System.IO.File.WriteAllBytes(path, displacedDepth.EncodeToPNG());
        }

    }

    void CreateShadowMap(Texture2D originView, Texture2D displacedView, Vector3 displacedPos, Texture2D shadowMap)
    {
        // shadowMap = new Texture2D(displacedView.width, displacedView.height);
        //initialize output images
        for (int i=0; i<displacedView.width; i++)
        {
            for (int j=0; j<displacedView.height; j++)
            {
                shadowMap.SetPixel(i, j, Color.black); //uncertainty mask
            }
        }

        for (int x=0; x<displacedView.width; x++)
        {
            for (int y=0; y<displacedView.height; y++)
            {
                //get depth of pixel in displaced camera's view pano
                float displacedPixelDepth = 1.0f-displacedView.GetPixel(x,y).r;

                //calculate the world space coords of the pixel in displaced view
                float displacedPixelA = ((float)x/displacedView.width*360 - 180) * Mathf.PI/180;
                float displacedPixelB = ((float)y/displacedView.height*180 - 90) * Mathf.PI/180;
                float displacedPixelZ = Mathf.Cos(displacedPixelA) * Mathf.Cos(displacedPixelB);
                float displacedPixelX = Mathf.Sin(displacedPixelA) * Mathf.Cos(displacedPixelB);
                float displacedPixelY = Mathf.Sin(displacedPixelB);
                Vector3 displacedPixelDir = new Vector3(displacedPixelX, displacedPixelY, displacedPixelZ);
                displacedPixelDir = displacedPixelDir.normalized;
                Vector3 globalPixelPos = displacedPos + displacedPixelDepth*displacedPixelDir;
                float globalDisplacedPixelDepth = globalPixelPos.magnitude;

                //test areas just around the origin to better cast shadows on uncertain areas
                Vector3[] orignTestShiftVectors = new []
                {
                    new Vector3(0.01f, 0.0f, 0.0f),
                    new Vector3(0.0f, 0.01f, 0.0f),
                    new Vector3(0.01f, 0.0f, 0.01f),
                    new Vector3(-0.01f, 0.0f, 0.0f),
                    new Vector3(0.0f, -0.01f, 0.0f),
                    new Vector3(0.01f, 0.0f, -0.01f)
                };

                for (int i=0; i<6; i++)
                {
                    //TODO: issue could be in here?
                    //need to account for displacement of original origin?
                    //determine where the pixel ends up in the new texture wrt the origin view
                    Vector3 originPixelDir = globalPixelPos.normalized - orignTestShiftVectors[i]; //test out slightly different views from the origin
                    float originPixelB = Mathf.Asin(originPixelDir.y) * 180/Mathf.PI;
                    // float originPixelA = Mathf.Acos(originPixelDir.x / Mathf.Cos(originPixelB)) * 180/Mathf.PI;
                    float originPixelA = Vector3.Angle(new Vector3(originPixelDir.x,0,originPixelDir.z), Vector3.forward) * Mathf.Sign(originPixelDir.x);
                    int originPixelScreenX = (int)((originPixelA+180)/360 * originView.width);
                    int originPixelScreenY = (int)((originPixelB+90)/180 * originView.height);

                    //if depth of reprojected pixel lies behind a pixel in the original view,
                    //that pixel must be inpainted in the displaced view
                    float originPixelDepth = 1.0f-originView.GetPixel(originPixelScreenX,originPixelScreenY).r;
                    if (globalDisplacedPixelDepth-0.03f >= originPixelDepth)
                    {
                        shadowMap.SetPixel(x, y, Color.white);
                    }
                }

            }
        }
        shadowMap.Apply();
    }

}