using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using BodhiDonselaar;

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
    [SerializeField] private string saveLocation;
    private string[] subfolders;

    //vector to move camera along for displacement
    [SerializeField][Tooltip("Displacement of camera from orign prior to reprojection.")] private Vector3 cameraDisplacement1;
    [SerializeField][Tooltip("Displacement of camera used in the reprojection itself.")] private Vector3 cameraDisplacement2;
    [SerializeField][Tooltip("If set to true, will create new color map reprojections in the dataset.")] private bool createUncertaintyMasks;
    [SerializeField][Tooltip("If set to true, will create new color map reprojections in the dataset.")] private bool createColorReprojections;
    [SerializeField][Tooltip("If set to true, will create new depth map reprojections in the dataset.")] private bool createDepthReprojections;
 
    [SerializeField] private Material renderMaterial;

    private bool isRendering = false;
    private int renderIndex = 800;

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
        if (Input.GetKeyDown("return") && (createUncertaintyMasks||createColorReprojections||createDepthReprojections))
        {
            isRendering = true;
        }
        else if (!Input.GetKeyDown("return") && !createUncertaintyMasks && !createColorReprojections && !createDepthReprojections)
        {
            Debug.LogWarning("You must select creation options to generate reprojections!");
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
            }
        }
    }

    void GenerateReprojections()
    {
        //each subfolder contains RGB and depth ground truth
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

        //Render out depth and color separately
        depthEquiCam.enabled = false;
        RenderTexture srcRGBRT = new RenderTexture(newRGBTexture.width, newRGBTexture.height, 16, RenderTextureFormat.ARGB32); 
        Camera.main.targetTexture = srcRGBRT;
        Camera.main.gameObject.transform.position = cameraDisplacement1;
        Camera.main.Render();
        depthEquiCam.enabled = true;
        
        //write RenderTexture to Texture2D
        Texture2D srcRGB = new Texture2D(newRGBTexture.width, newRGBTexture.height);
        RenderTexture.active = srcRGBRT;
        srcRGB.ReadPixels(new Rect(0, 0, srcRGBRT.width, srcRGBRT.height), 0, 0);
        srcRGB.Apply();

        //create new texture to put reprojection in
        Texture2D dstUncertainty = new Texture2D(srcView.width, srcView.height);
        Texture2D dstRGB = new Texture2D(srcView.width, srcView.height);
        Texture2D dstDepth = new Texture2D(srcView.width, srcView.height);
        Texture2D[] dstViews = {dstUncertainty, dstRGB, dstDepth};
        reproject(srcView, srcRGB, dstViews, new Vector3(0,0,0), cameraDisplacement2);

        //save new masks and reprojections in the asset directory
        //TODO: save the new images in a different location outside of asset directory
        if (createUncertaintyMasks)
        {
            //string path = subfolder+"/"+label+"UncertaintyMask.png";
            string path = saveLocation+"/"+label+"/"+label+"UncertaintyMask.png";
            System.IO.File.WriteAllBytes(path, dstViews[0].EncodeToPNG());
        }
        if (createColorReprojections)
        {
            //string path = subfolder+"/"+label+"RGBReprojection.png";
            string path = saveLocation+"/"+label+"/"+label+"RGBReprojection.png";
            System.IO.File.WriteAllBytes(path, dstViews[1].EncodeToPNG());
        }
        if (createDepthReprojections)
        {
            //string path = subfolder+"/"+label+"DepthReprojection.png";
            string path = saveLocation+"/"+label+"/"+label+"DepthReprojection.png";
            System.IO.File.WriteAllBytes(path, dstViews[2].EncodeToPNG());
        }
    }

    void reproject(Texture2D srcView, Texture2D srcRGB, Texture2D[] dstViews, Vector3 srcPos, Vector3 dstPos)
    {
        //initialize output images
        for (int i=0; i<srcView.width; i++)
        {
            for (int j=0; j<srcView.height; j++)
            {
                dstViews[0].SetPixel(i, j, Color.white); //uncertainty mask
                dstViews[1].SetPixel(i, j, Color.clear); //color reprojection
                dstViews[2].SetPixel(i, j, Color.clear); //depth reprojection
            }
        }

        //track depths of reprojected pixels to maintain order
        float[,] reprojectedPixelDepths = new float[srcView.height+1, srcView.width+1];

        for (int x=0; x<srcView.width; x++)
        {
            for (int y=0; y<srcView.height; y++)
            {
                //get color and depth of pixrl in camera's current view pano
                Color srcPixelDepth = srcView.GetPixel(x,y);
                Color srcPixelRGB = srcRGB.GetPixel(x,y);
                float srcDistToPixel = 1-srcPixelDepth.r;

                //calculate the world space coords of the pixel in source view
                float srcPixelA = ((float)x/srcView.width*360 - 180) * Mathf.PI/180;
                float srcPixelB = ((float)y/srcView.height*180 - 90) * Mathf.PI/180;
                float srcPixelZ = Mathf.Cos(srcPixelA) * Mathf.Cos(srcPixelB);
                float srcPixelX = Mathf.Sin(srcPixelA) * Mathf.Cos(srcPixelB);
                float srcPixelY = Mathf.Sin(srcPixelB);
                Vector3 srcPixelDir = new Vector3(srcPixelX, srcPixelY, srcPixelZ);
                srcPixelDir = srcPixelDir.normalized;
                Vector3 srcPixelPos = srcPos + srcDistToPixel*srcPixelDir;

                //determine where the pixel ends up in the new texture with the new view
                Vector3 srcCamToSrcPixel = srcPixelPos - srcPos;
                Vector3 dstCamToDstPixel = srcPixelPos - dstPos;
                float dstPixelDepth = 1-((dstCamToDstPixel.magnitude/srcCamToSrcPixel.magnitude) * srcDistToPixel);
                Vector3 dstPixelDir = dstCamToDstPixel.normalized;
                float dstPixelB = Mathf.Asin(dstPixelDir.y) * 180/Mathf.PI;
                float dstPixelA = Vector3.Angle(new Vector3(dstPixelDir.x,0,dstPixelDir.z), Vector3.forward) * Mathf.Sign(dstPixelDir.x);
                int dstPixelScreenX = (int)((dstPixelA+180)/360 * srcView.width);
                int dstPixelScreenY = (int)((dstPixelB+90)/180 * srcView.height);

                //if creating uncertainty mask, uncertainty is binary
                if (createUncertaintyMasks)
                {
                    dstViews[0].SetPixel(dstPixelScreenX, dstPixelScreenY, Color.black);
                }
                //if reprojecting color or depth image, test depth of pixels
                if (createColorReprojections || createDepthReprojections)
                {
                    float currentDstDepth = reprojectedPixelDepths[dstPixelScreenY, dstPixelScreenX];
                    if (dstPixelDepth > currentDstDepth)
                    {
                        //if creating color reprojection, use source pixel color in new pos
                        if (createColorReprojections)
                        {
                            dstViews[1].SetPixel(dstPixelScreenX, dstPixelScreenY, srcPixelRGB);
                        }
                        //if depth reprojection, use new depth of pixel
                        if (createDepthReprojections)
                        {
                            dstViews[2].SetPixel(dstPixelScreenX, dstPixelScreenY, new Color(dstPixelDepth,dstPixelDepth,dstPixelDepth));
                        }
                        reprojectedPixelDepths[dstPixelScreenY, dstPixelScreenX] = dstPixelDepth;
                    }
                }

            }
        }
        dstViews[0].Apply();
        dstViews[1].Apply();
        dstViews[2].Apply();
    }
}
