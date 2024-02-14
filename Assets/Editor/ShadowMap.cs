using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ShadowMap : EditorWindow
{
    string saveLocation;
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

            float maxSrcDist = 0;
            float maxDstDist = 0;

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

                    //determine where the pixel ends up in the new texture wrt the origin view
                    Vector3 originPixelDir = globalPixelPos.normalized;
                    float originPixelB = Mathf.Asin(originPixelDir.y) * 180/Mathf.PI;
                    float originPixelA = Vector3.Angle(new Vector3(originPixelDir.x,0,originPixelDir.z), Vector3.forward) * Mathf.Sign(originPixelDir.x);
                    int originPixelScreenX = (int)((originPixelA+180)/360 * displacedView.width);
                    int originPixelScreenY = (int)((originPixelB+90)/180 * displacedView.height);

                    //if depth of reprojected pixel lies behind a pixel in the original view,
                    //that pixel must be inpainted in the displaced view
                    float originPixelDepth = 1.0f-originView.GetPixel(originPixelScreenX,originPixelScreenY).r;
                    if (globalDisplacedPixelDepth-0.01f >= originPixelDepth)
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
