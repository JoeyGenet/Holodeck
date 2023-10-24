Shader "Unlit/CustomDepth"
{
    //attach to the rendered scene material, ie responsible for deforming the sphere skybox
    //also needs to set up shadow casting stuff so it shows up on the depth map
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CamPos ("Camera Position", VECTOR) = (0,0,0)

        [Header(Rendering)]
        _Offset("Offset", float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Culling ("Cull Mode", Int) = 2
        [Enum(Off,0,On,1)] _ZWrite("ZWrite", Int) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Int) = 4
        [Enum(None,0,Alpha,1,Red,8,Green,4,Blue,2,RGB,14,RGBA,15)] _ColorMask("Color Mask", Int) = 15
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    float3 _CamPos;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        float4 color : COLOR;
    };

    struct v2f
    {
        float2 uv : TEXCOORD0;
        UNITY_FOG_COORDS(1)
        float4 vertex : SV_POSITION;
        float4 color : COLOR;
    };

    struct v2fShadow {
        V2F_SHADOW_CASTER;
        UNITY_VERTEX_OUTPUT_STEREO
        float4 color : COLOR;
    };

    v2f vert (appdata v)
    {
        v2f o;

        //alter vertex position data here
        //v.vertex -= v.vertex*min(tex2Dlod(_DepthTex, float4(v.uv.xy, 0.0, 0.0)).x,0.80f); //0.80f
        o.color = v.vertex;

        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        o.vertex = UnityObjectToClipPos(v.vertex);
        UNITY_TRANSFER_FOG(o,o.vertex);
        return o;
    }

    fixed4 frag (v2f i, out float outputDepth : SV_Depth) : SV_Target
    {
        // sample the texture
        fixed4 col = tex2D(_MainTex, i.uv);
        //add logic to calculate euclidean distance, write to depth channel?
        //should be same brightnes as source depth
        //float distanceFromCamera = length(i.color.xyz - mul(unity_WorldToObject, _WorldSpaceCameraPos)); //local space of mesh, ie scale agnostic
        float distanceFromCamera = length(mul(unity_ObjectToWorld, i.color.xyz) - _WorldSpaceCameraPos);// / _ProjectionParams.z;   //global space, ie scale matters, only works when scale=1 though
        //if (distanceFromCamera > 1) distanceFromCamera = 1;
        //UNITY_OUTPUT_DEPTH(i);
        //outputDepth = distanceFromCamera; //i.vertex.z / i.vertex.w;
        // apply fog
        UNITY_APPLY_FOG(i.fogCoord, col);
        return col;
    }

    float4 fragShadow( v2f i, out float outputDepth : SV_Depth) : SV_Target
    {
        //SHADOW_CASTER_FRAGMENT(i)
        float distanceFromCamera = length(mul(unity_ObjectToWorld, i.color.xyz) - _WorldSpaceCameraPos);// / _ProjectionParams.z;   //global space, ie scale matters, only works when scale=1 though
        //if (distanceFromCamera > 1) distanceFromCamera = 1;
        //UNITY_OUTPUT_DEPTH(i);
        outputDepth = distanceFromCamera; //i.vertex.z / i.vertex.w;
        SHADOW_CASTER_FRAGMENT(i)
    }

    ENDCG

    SubShader
    {
        Pass
        {
            Tags { "RenderType"="Opaque" }
            Cull [_Culling]
            LOD 100

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            LOD 80
            Cull [_Culling]
            Offset [_Offset], [_Offset]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
           
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragShadow
            #pragma target 2.0
            #pragma multi_compile_shadowcaster

            ENDCG

        }
    }
}
