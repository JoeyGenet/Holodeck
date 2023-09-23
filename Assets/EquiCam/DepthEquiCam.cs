using UnityEngine;
namespace BodhiDonselaar
{
	[RequireComponent(typeof(Camera))]
	public class DepthEquiCam : MonoBehaviour
	{
		private static Material equi;
		public Size RenderResolution = Size.Default;
		private RenderTexture cubemap;
		private RenderTexture cubemapSide;
		private Camera cam;
		private GameObject child;
		public enum Size
		{
			High = 2048,
			Default = 1024,
			Low = 512,
			Minimum = 256
		}

		Quaternion GetFaceRotation(CubemapFace face)
        {
			//float cameraYRotation = cam.transform.eulerAngles.y;
            switch (face)
            {
                case CubemapFace.NegativeX: return Quaternion.Euler(0, -90, 0);
                case CubemapFace.PositiveX: return Quaternion.Euler(0, 90, 0);
                case CubemapFace.PositiveY: return Quaternion.Euler(90, 0, 0);
                case CubemapFace.NegativeY: return Quaternion.Euler(-90, 0, 0);
                case CubemapFace.PositiveZ: return Quaternion.Euler(0, 0, 0);
                case CubemapFace.NegativeZ: return Quaternion.Euler(0, -180, 0);
            }
            return Quaternion.identity;
        }

		void OnEnable()
		{
			if (equi == null) equi = new Material(Resources.Load<Shader>("DepthEquiCam"));
			child = new GameObject();
			child.hideFlags = HideFlags.HideInHierarchy;
			child.transform.SetParent(transform);
			child.transform.localPosition = Vector3.zero;
			child.transform.localEulerAngles = Vector3.zero;
			cam = child.AddComponent<Camera>();
			cam.CopyFrom(GetComponent<Camera>());
			cam.depthTextureMode = DepthTextureMode.Depth;
			child.SetActive(false);
			New();
		}
		void OnDisable()
		{
			if (child != null) DestroyImmediate(child);
			if (cubemap != null)
			{
				cubemap.Release();
				DestroyImmediate(cubemap);
			}
		}
		void OnRenderImage(RenderTexture src, RenderTexture des)
		{
			if (cubemap.width != (int)RenderResolution) New();
			cam.aspect = 1f;
			cam.fieldOfView = 90;
			cam.targetTexture = cubemapSide;

			for (int faceIdx = 0; faceIdx < 6; faceIdx++)
			{
				cam.transform.rotation = GetFaceRotation((CubemapFace)faceIdx);
				cam.Render();
				Graphics.CopyTexture(cubemapSide, 0, cubemap, faceIdx); //null source texture
			}
			//cam.RenderToCubemap(cubemap);
			Shader.SetGlobalFloat("FORWARD", cam.transform.eulerAngles.y * 0.01745f);
			Graphics.Blit(cubemap, des, equi);
		}
		private void New()
		{
			cam.targetTexture = null;
			if (cubemap != null)
			{
				cubemap.Release();
				DestroyImmediate(cubemap);
			}
			cubemap = new RenderTexture((int)RenderResolution, (int)RenderResolution, 16, RenderTextureFormat.Depth); 
			cubemap.antiAliasing = 1;
			cubemap.filterMode = FilterMode.Bilinear;
			cubemap.anisoLevel = 0;
			cubemap.dimension = UnityEngine.Rendering.TextureDimension.Cube;
			cubemap.autoGenerateMips = false;
			cubemap.useMipMap = false;
			cam.targetTexture = cubemap;
			//
			cubemapSide = new RenderTexture((int)RenderResolution, (int)RenderResolution, 16, RenderTextureFormat.Depth); 
			cubemapSide.antiAliasing = 1;
			cubemapSide.filterMode = FilterMode.Bilinear;
			cubemapSide.anisoLevel = 0;
			cubemapSide.dimension = UnityEngine.Rendering.TextureDimension.Cube;
			cubemapSide.autoGenerateMips = false;
			cubemapSide.useMipMap = false;
		}
	}
}