using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

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
    private string[] subfolders;

    //vector to move camera along for displacement
    [SerializeField][Tooltip("Displacement of camera from orign prior to reprojection.")] private Vector3 cameraDisplacement1;
    [SerializeField][Tooltip("Displacement of camera used in the reprojection itself.")] private Vector3 cameraDisplacement2;
    [SerializeField] private Material renderMaterial;

    private bool isRendering = false;
    private int renderIndex = 800;

    // Start is called before the first frame update
    void Start()
    {
        subfolders = AssetDatabase.GetSubFolders(trainingDirectory);
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
        }

        if (isRendering)
        {
            Debug.Log("Generating reprojections for files in "+trainingDirectory);
            GenerateReprojections();
            renderIndex++;
            if (renderIndex == subfolders.Length)
            {
                isRendering = false;
                AssetDatabase.Refresh();
            }
        }
    }

    void GenerateReprojections()
    {
        //each subfolder contains RGB, D
        //foreach (string subfolder in subfolders)
        string subfolder = subfolders[renderIndex];

        //set the main and depth texture of the sphere
        string label = subfolder.Substring(subfolder.LastIndexOf("/")+1);
        Debug.Log("RGB image path: "+subfolder+"/"+label+"RGB.png");
        Debug.Log("Depth image path: "+subfolder+"/"+label+"Depth.png");
        Texture2D newRGBTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(subfolder+"/"+label+"RGB.png", typeof(Texture2D));
        Texture2D newDTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(subfolder+"/"+label+"Depth.png", typeof(Texture2D));
        renderMaterial.SetTexture("_MainTex", newRGBTexture);
        renderMaterial.SetTexture("_DepthTex", newDTexture);

        //move the camera and render out initial view
        RenderTexture srcViewRT = new RenderTexture(newRGBTexture.width, newRGBTexture.height, 16, RenderTextureFormat.ARGB32); 
        Camera.main.targetTexture = srcViewRT;
        Camera.main.gameObject.transform.position = cameraDisplacement1;
        Camera.main.Render();
        
        //write RenderTexture to Texture2D
        Texture2D srcView = new Texture2D(newRGBTexture.width, newRGBTexture.height);
        RenderTexture.active = srcViewRT;
        srcView.ReadPixels(new Rect(0, 0, srcViewRT.width, srcViewRT.height), 0, 0);
        srcView.Apply();

        //create new texture to put reprojection in
        Texture2D dstView = new Texture2D(srcView.width, srcView.height);
        reproject(srcView, new Vector3(0,0,0), dstView, cameraDisplacement2);

        //save new reprojection in the asset directory
        string path = subfolder+"/"+label+"UncertaintyMask.png";
        System.IO.File.WriteAllBytes(path, dstView.EncodeToPNG());

        //ScreenCapture.CaptureScreenshot(subfolder+"/"+label+"test.png", 4);
    }

    void reproject(Texture2D srcView, Vector3 srcPos, Texture2D dstView, Vector3 dstPos)
    {
        for (int i=0; i<srcView.width; i++)
        {
            for (int j=0; j<srcView.height; j++)
            {
                dstView.SetPixel(i, j, Color.white); //Color.red
            }
        }

        float maxSrcDist = 0;
        float maxDstDist = 0;

        for (int x=0; x<srcView.width; x++)
        {
            for (int y=0; y<srcView.height; y++)
            {

                Color srcColor = srcView.GetPixel(x,y);
                float srcDistToPixel = 1-srcColor.r;
                //Vector3 srcColorVec = new Vector3(srcColor.r, srcColor.g, srcColor.b);

                float srcPixelA = ((float)x/srcView.width*360 - 180) * Mathf.PI/180; //(x - srcView.width / 2.0f) / srcView.width * 360 / 180.0f * Mathf.PI; //
                float srcPixelB = ((float)y/srcView.height*180 - 90) * Mathf.PI/180; //(y - srcView.height / 2.0f) / srcView.height * 180 / 180.0f * Mathf.PI; //
                // Debug.Log("srcPixel A and B: "+srcPixelA+", "+srcPixelB);
                float srcPixelZ = Mathf.Cos(srcPixelA) * Mathf.Cos(srcPixelB);
                float srcPixelX = Mathf.Sin(srcPixelA) * Mathf.Cos(srcPixelB);
                float srcPixelY = Mathf.Sin(srcPixelB);
                // Debug.Log("srcPixel X, Y and Z: "+srcPixelX+", "+srcPixelY+", "+srcPixelZ);
                Vector3 srcPixelDir = new Vector3(srcPixelX, srcPixelY, srcPixelZ);
                srcPixelDir = srcPixelDir.normalized;

                Vector3 srcPixelPos = srcPos + srcDistToPixel*(srcPixelDir); //how to find direction vector?
                //Vector3 dstPixelPos = srcPixelPos;// - (dstPos - srcPos);
                Vector3 srcCamToSrcPixel = srcPixelPos - srcPos;
                Vector3 dstCamToDstPixel = srcPixelPos - dstPos;
                float dstPixelColor = 1-((dstCamToDstPixel.magnitude/srcCamToSrcPixel.magnitude) * srcDistToPixel); //- 2;
                Vector3 dstPixelDir = dstCamToDstPixel.normalized;
                // Debug.Log("dstPixel X, Y and Z: "+dstPixelDir.x+", "+dstPixelDir.y+", "+dstPixelDir.z);
                if (srcCamToSrcPixel.magnitude > maxSrcDist) maxSrcDist = srcCamToSrcPixel.magnitude;
                if (dstCamToDstPixel.magnitude > maxDstDist) maxDstDist = dstCamToDstPixel.magnitude;

                float dstPixelB = Mathf.Asin(dstPixelDir.y) * 180/Mathf.PI;
                float dstPixelA = Vector3.Angle(new Vector3(dstPixelDir.x,0,dstPixelDir.z), Vector3.forward) * Mathf.Sign(dstPixelDir.x);//Mathf.Asin(dstPixelDir.z / Mathf.Cos(dstPixelB)) * 180/Mathf.PI;
                // Debug.Log("dstPixel A and B: "+dstPixelA+", "+dstPixelB);
                int dstPixelScreenX = (int)((dstPixelA+180)/360 * srcView.width);// % dstView.width;
                int dstPixelScreenY = (int)((dstPixelB+90)/180 * srcView.height);// % dstView.height;

                Color currentDstColor = dstView.GetPixel(dstPixelScreenX, dstPixelScreenY);
                //if (dstPixelColor > currentDstColor.g)
                    dstView.SetPixel(dstPixelScreenX, dstPixelScreenY, Color.black); //new Color(dstPixelColor,dstPixelColor,dstPixelColor)
                // Debug.Log("dst sceen X and Y: "+dstPixelScreenX+", "+dstPixelScreenY+" | color: "+dstPixelColor);
            }
        }
        dstView.Apply();
    }
}
