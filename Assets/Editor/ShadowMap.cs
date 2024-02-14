using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ShadowMap : EditorWindow
{
    string saveLocation;
    Texture2D srcView;
    Vector3 srcPos;
    Texture2D dstView;
    Vector3 dstPos;
    Texture2D shadowMap;

    [MenuItem("Tools/Shadow Map")]
    public static void ShowMyEditor()
    {
        EditorWindow window = GetWindow<ShadowMap>();
        window.titleContent = new GUIContent("Shadow Map");
    }

    public void OnGUI()
    {
        //set location to save the shadow map
        saveLocation = EditorGUILayout.TextField("Save Location", saveLocation);
        //select desired src and target textures and positions
        srcView = (Texture2D)EditorGUILayout.ObjectField("Source View", srcView, typeof(Texture2D), false);
        srcPos = EditorGUILayout.Vector3Field("Source Position", srcPos);
        dstView = (Texture2D)EditorGUILayout.ObjectField("Destination View", dstView, typeof(Texture2D), false);
        dstPos = EditorGUILayout.Vector3Field("Destination Position", dstPos);

        if (GUILayout.Button("Reproject"))
        {
            Debug.Log("Reprojecting...");

            shadowMap = new Texture2D(srcView.width, srcView.height);
            //initialize output images
            for (int i=0; i<srcView.width; i++)
            {
                for (int j=0; j<srcView.height; j++)
                {
                    shadowMap.SetPixel(i, j, Color.black); //uncertainty mask
                }
            }

            float maxSrcDist = 0;
            float maxDstDist = 0;

            for (int x=0; x<srcView.width; x++)
            {
                for (int y=0; y<srcView.height; y++)
                {
                    //get color and depth of pixel in camera's current view pano
                    Color srcPixelDepth = srcView.GetPixel(x,y);
                    float srcDistToPixel = 1.0f-srcPixelDepth.r;

                    //calculate the world space coords of the pixel in source view
                    float srcPixelA = ((float)x/srcView.width*360.0f - 180.0f) * Mathf.PI/180;
                    float srcPixelB = ((float)y/srcView.height*180.0f - 90.0f) * Mathf.PI/180;
                    float srcPixelZ = Mathf.Cos(srcPixelA) * Mathf.Cos(srcPixelB);
                    float srcPixelX = Mathf.Sin(srcPixelA) * Mathf.Cos(srcPixelB);
                    float srcPixelY = Mathf.Sin(srcPixelB);
                    Vector3 srcPixelDir = new Vector3(srcPixelX, srcPixelY, srcPixelZ);
                    srcPixelDir = srcPixelDir.normalized;
                    Vector3 srcPixelPos = srcPos + srcPixelDir; //srcDistToPixel*srcPixelDir;

                    //determine where the pixel ends up in the new texture with the new view
                    Vector3 srcCamToSrcPixel = srcPixelPos - srcPos;
                    Vector3 dstCamToDstPixel = srcPixelPos - dstPos;
                    float dstPixelDepth = ((float)(dstCamToDstPixel.magnitude/srcCamToSrcPixel.magnitude) * srcDistToPixel);
                    Vector3 dstPixelDir = dstCamToDstPixel.normalized;
                    float dstPixelB = Mathf.Asin(dstPixelDir.y) * 180/Mathf.PI;
                    float dstPixelA = Vector3.Angle(new Vector3(dstPixelDir.x,0,dstPixelDir.z), Vector3.forward) * Mathf.Sign(dstPixelDir.x);
                    int dstPixelScreenX = (int)((dstPixelA+180.0f)/360.0f * srcView.width);
                    int dstPixelScreenY = (int)((dstPixelB+90.0f)/180.0f * srcView.height);

                    //if depth of reprojected pixel lies behind a pixel in the original view,
                    //that pixel must be inpainted in the displaced view
                    float currentDstDepth = 1.0f-dstView.GetPixel(dstPixelScreenX,dstPixelScreenY).r;
                    if (dstPixelDepth-0.01f >= currentDstDepth)
                    {
                        shadowMap.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        shadowMap.SetPixel(x, y, Color.black);
                    }

                }
            }
            shadowMap.Apply();
            System.IO.File.WriteAllBytes(saveLocation, shadowMap.EncodeToPNG());
            Debug.Log("Done");
            Debug.Log("maxSrcDist: "+maxSrcDist+", maxSrcDist: "+maxDstDist);
        }

    }
}
