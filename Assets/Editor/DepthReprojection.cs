using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DepthReprojection : EditorWindow
{
    Texture2D srcView;
    Vector3 srcPos;
    Texture2D dstView;
    Vector3 dstPos;

    [MenuItem("Tools/Depth Reprojection")]
    public static void ShowMyEditor()
    {
        EditorWindow window = GetWindow<DepthReprojection>();
        window.titleContent = new GUIContent("Depth Reprojection");
    }

    public void OnGUI()
    {
        //select desired src and target textures and positions
        srcView = (Texture2D)EditorGUILayout.ObjectField("Source View", srcView, typeof(Texture2D), false);
        srcPos = EditorGUILayout.Vector3Field("Source Position", srcPos);
        dstView = (Texture2D)EditorGUILayout.ObjectField("Destination View", dstView, typeof(Texture2D), false);
        dstPos = EditorGUILayout.Vector3Field("Destination Position", dstPos);

        if (GUILayout.Button("Reproject"))
        {
            Debug.Log("Reprojecting...");

            for (int i=0; i<srcView.width; i++)
            {
                for (int j=0; j<srcView.height; j++)
                {
                    dstView.SetPixel(i, j, Color.red);
                }
            }

            float maxSrcDist = 0;
            float maxDstDist = 0;

            for (int x=0; x<srcView.width; x++)
            {
                for (int y=0; y<srcView.height; y++)
                {
                    // int x = 624;
                    // int y = 31;

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
                    if (dstPixelColor > currentDstColor.g)
                        dstView.SetPixel(dstPixelScreenX, dstPixelScreenY, new Color(dstPixelColor,dstPixelColor,dstPixelColor)); //*Color.white);
                    // Debug.Log("dst sceen X and Y: "+dstPixelScreenX+", "+dstPixelScreenY+" | color: "+dstPixelColor);
                }
            }
            dstView.Apply();
            string path = Application.dataPath+"/../"+AssetDatabase.GetAssetPath(dstView);
            System.IO.File.WriteAllBytes(path, dstView.EncodeToPNG());
            Debug.Log("Done");
            Debug.Log("maxSrcDist: "+maxSrcDist+", maxSrcDist: "+maxDstDist);
        }

    }
}
