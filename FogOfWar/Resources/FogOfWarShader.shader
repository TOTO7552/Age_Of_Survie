﻿Shader "Hidden/FogOfWar"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_FogTex ("Fog", 2D) = "white" {}
		_FogColorTex ("Fog Color", 2D) = "white" {}
	}

	CGINCLUDE

	#include "UnityCG.cginc"
	
	uniform sampler2D _MainTex;
	uniform sampler2D_float _FogTex;
	uniform sampler2D_float _CameraDepthTexture;
	uniform float4 _MainTex_TexelSize;
	uniform sampler2D _FogColorTex;
	
	// for fast world space reconstruction
	uniform float4x4 _FrustumCornersWS;
	uniform float4 _CameraWS;
	uniform float4 _CameraDir; // xyz = camera direction, w = near plane distance

	uniform float _FogTextureSize;
	uniform float _MapSize;
	uniform float4 _MapOffset;
	uniform float4 _FogColor;

	struct v2f
	{
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
		float2 uv_depth : TEXCOORD1;
		float4 interpolatedRay : TEXCOORD2;
	};
	
	v2f vert (appdata_img v)
	{
		v2f o;
		half index = v.vertex.z;
		v.vertex.z = 0.1;
		o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
		o.uv = v.texcoord.xy;
		o.uv_depth = v.texcoord.xy;
		
		#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0)
			o.uv.y = 1-o.uv.y;
		#endif				
		
		o.interpolatedRay = _FrustumCornersWS[(int)index];
		o.interpolatedRay.w = index;
		
		return o;
	}

	ENDCG

	SubShader
	{
		ZTest Always
		Cull Off
		ZWrite Off
		Fog { Mode Off }

		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile CAMERA_ORTHOGRAPHIC CAMERA_PERSPECTIVE
			#pragma multi_compile MODE2D MODE3D
			#pragma multi_compile _ TEXTUREFOG

			half4 frag (v2f i) : SV_Target
			{
				// Reconstruct world space position and direction
				float rawdpth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv_depth);
				#ifdef CAMERA_ORTHOGRAPHIC
					float3 nearPlaneDist = _CameraDir.w; // from camera
					float3 camDir = _CameraDir.xyz;
					float3 nearPlaneOffset = camDir * nearPlaneDist; // relative to camera pos
					float3 camPos = _CameraWS + nearPlaneOffset; // pretend the camera pos is at the near plane
					float3 rayFar = i.interpolatedRay - nearPlaneOffset; // relative to camera near plane
				
					float3 rayVec = camDir * dot(rayFar, camDir); // relative to rayOrigin
					float3 rayOrigin = rayFar - rayVec; // relative to camera
					float3 rayCast = (rayFar - rayOrigin) * rawdpth; // just use the raw depth texture for ortho
					float3 wsPos = camPos + rayOrigin + rayCast;
				
				#else
					float3 wsDir = i.interpolatedRay * Linear01Depth(rawdpth); // for PERSPECTIVE
					float3 wsPos = _CameraWS + wsDir;
				#endif
				
				#ifdef MODE2D
					float2 modepos = wsPos.xy;
				#else
					float2 modepos = wsPos.xz;
				#endif

				float2 mapPos = (modepos - _MapOffset.xy) / _MapSize + float2(0.5f, 0.5f);
				
				// if it is beyond the map
				float fog;
				if (mapPos.x < 0 || mapPos.x > 1 ||
					mapPos.y < 0 || mapPos.y > 1)
					fog = 1;
				else
					fog = tex2D(_FogTex, mapPos).a;

				#ifdef TEXTUREFOG
					float4 fogcolor = tex2D(_FogColorTex, i.uv);
				#else
					float4 fogcolor = _FogColor;
				#endif
		
				fog *= step(rawdpth, 0.999); // don't show fog on the back plane
				half4 sceneColor = tex2D(_MainTex, i.uv);
				return lerp(sceneColor, fogcolor, fog);
			}

			ENDCG
		}
	}

	Fallback off
}
