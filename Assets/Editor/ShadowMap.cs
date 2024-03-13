using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ShadowMap : EditorWindow
{
    string saveLocation = "C:\\Joey\\School\\S7_2023_Fall\\Research\\TestingData\\BlueOffice\\debugShadowMap.png";
    Texture2D displacedView;
    Vector3 displacedPos;
    Texture2D originView;
    Vector3 originPos;
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
        displacedView = (Texture2D)EditorGUILayout.ObjectField("Displaced View", displacedView, typeof(Texture2D), false);
        displacedPos = EditorGUILayout.Vector3Field("Displaced Position", displacedPos);
        originView = (Texture2D)EditorGUILayout.ObjectField("Origin View", originView, typeof(Texture2D), false);
        originPos = EditorGUILayout.Vector3Field("Origin Position", originPos);

        if (GUILayout.Button("Create Shadow Map"))
        {
            Debug.Log("Creating shadow map...");

            shadowMap = new Texture2D(displacedView.width, displacedView.height);
            //initialize output images
            for (int i=0; i<displacedView.width; i++)
            {
                for (int j=0; j<displacedView.height; j++)
                {
                    shadowMap.SetPixel(i, j, Color.black); //uncertainty mask
                }
            }

            int maxXCoord = 0;
            int minXCoord = 10000000;
            int maxYCoord = 0;
            int minYCoord = 10000000;

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
                    Vector3 globalPixelPos = displacedPos + displacedPixelDepth*displacedPixelDir; //this might not be correct???
                    float globalDisplacedPixelDepth = globalPixelPos.magnitude; //this seems to be correct

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
                        if (originPixelScreenX < minXCoord) minXCoord = originPixelScreenX;
                        if (originPixelScreenX > maxXCoord) maxXCoord = originPixelScreenX;
                        if (originPixelScreenY < minYCoord) minYCoord = originPixelScreenY;
                        if (originPixelScreenY > maxYCoord) maxYCoord = originPixelScreenY;

                        float originPixelDepth = 1.0f-originView.GetPixel(originPixelScreenX,originPixelScreenY).r;
                        // debug suite
                        // if (x==1000 && y==1045)
                        // {
                        //     shadowMap.SetPixel(x, y, Color.green);
                        //     float recreatedPixelA = ((float)originPixelScreenX/originView.width*360 - 180) * Mathf.PI/180;
                        //     float recreatedPixelB = ((float)originPixelScreenY/originView.height*180 - 90) * Mathf.PI/180;
                        //     float recreatedPixelZ = Mathf.Cos(recreatedPixelA) * Mathf.Cos(recreatedPixelB);
                        //     float recreatedPixelX = Mathf.Sin(recreatedPixelA) * Mathf.Cos(recreatedPixelB);
                        //     float recreatedPixelY = Mathf.Sin(recreatedPixelB);
                        //     Vector3 recreatedPixelDir = new Vector3(recreatedPixelX, recreatedPixelY, recreatedPixelZ);
                        //     recreatedPixelDir = recreatedPixelDir.normalized;
                        //     Vector3 recreatedGlobalPixelPos = originPos + originPixelDepth*recreatedPixelDir;
                            
                        //     Debug.Log("global position of pixel from novel view: "+globalPixelPos);
                        //     Debug.Log("recreated global position of pixel: "+recreatedGlobalPixelPos);
                        //     Debug.Log("------------------------");
                        //     Debug.Log("novel pixel image coords: ("+x+", "+y+")");
                        //     Debug.Log("depth of pixel from novel view: "+globalDisplacedPixelDepth);
                        //     Debug.Log("------------------------");
                        //     Debug.Log("origin pixel image coords: ("+originPixelScreenX+", "+originPixelScreenY+")");
                        //     Debug.Log("depth of corresponding pixel in origin view: "+originPixelDepth);
                        // }
                        if (globalDisplacedPixelDepth-0.03f >= originPixelDepth && 
                            globalDisplacedPixelDepth<0.95f && originPixelDepth<0.95f) //TODO: account for forced-deep pixels
                        {
                            shadowMap.SetPixel(x, y, Color.white);
                        }
                    }

                }
            }
            shadowMap.Apply();
            System.IO.File.WriteAllBytes(saveLocation, shadowMap.EncodeToPNG());
            Debug.Log("Done");
            // Debug.Log("min coords: ("+minXCoord+", : "+minYCoord+")");
            // Debug.Log("max coords: ("+maxXCoord+", : "+maxYCoord+")");
        }

    }
}
