Shader "Custom/AlphaBlending" {
	Properties{
		_MainTex("Texture", 2D) = "white" {}
	_SceneDepthTex("Scene Depth Texture", 2D) = "white"{}
	_CgDepthTex("Cg Depth Texture", 2D) = "white"{}
	_WebcamTex("Webcam Texture", 2D) = "white"{}
	_ShadowTex("Shadow Texture", 2D) = "white"{}
	_ShadowDepthTex("Shadow Depth Texture", 2D) = "white" {}

	_VisibilityComplex("Visiblity Complex", Range(0, 1.0)) = 0.1
		_VisibilitySimple("Visibility Simple", Range(0, 1.0)) = 0.01

	}

		SubShader{
		Pass{
		CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc" // required for v2f_img

		// Properties
		uniform Texture2D _MainTex;
	uniform SamplerState sampler_MainTex;
	float4 _MainTex_TexelSize;

	uniform Texture2D _SceneDepthTex;
	uniform SamplerState sampler_SceneDepthTex;
	float4 _SceneDepthTex_TexelSize;

	uniform Texture2D _CgDepthTex;
	uniform SamplerState sampler_CgDepthTex;
	float4 _CgDepthTex_TexelSize;

	uniform Texture2D _WebcamTex;
	uniform SamplerState sampler_WebcamTex;
	float4 _WebcamTex_TexelSize;

	uniform Texture2D _ShadowTex;
	uniform SamplerState sampler_ShadowTex;
	float4 _ShadowTex_TexelSize;

	uniform Texture2D _ShadowDepthTex;
	uniform SamplerState sampler_ShadowDepthTex;
	float4 _ShadowDepthTex_TexelSize;

	float _VisibilityComplex;
	float _VisibilitySimple;

	struct vertexInput {
		float4 pos : POSITION;
		float4 tex0 : TEXCOORD0;
		float4 tex1 : TEXCOORD1;
	};

	struct vertexOutput {
		float4 pos: SV_POSITION;
		float4 tex0: TEXCOORD0;
		float4 tex1: TEXCOORD1;
	};

	float4 frag(vertexOutput input) : COLOR{

		//Parameters:
		int windowSize = 5;
	int maxBin = (2 * windowSize + 1)*(2 * windowSize + 1);
	int windowScale = 2; // if Complex, set to high, if Simple, set to low
	float xm = (float)((2 * windowSize + 1)*(2 * windowSize + 1));
	float xd = (xm / 2);
	float pm = 0.01f;
	float pd = 0.9f;
	float k = (log(pm / (1 - pm)) - log(pd / (1 - pd))) / (xd - xm);
	float x0 = log(pm / (1 - pm)) / k + xm;

	float sceneDepth;
	float4 cgDepthC;
	int binTotal = 0;

	//float4 base = _MainTex.Sample(sampler_MainTex, input.tex0);
	float4 centerCgDepth = _CgDepthTex.Sample(sampler_CgDepthTex, input.tex0);
	float centerSceneDepth = _SceneDepthTex.Sample(sampler_SceneDepthTex, input.tex0);
	float4 webcamCenter = _WebcamTex.Sample(sampler_WebcamTex, input.tex0);
	float4 shadow = _ShadowTex.Sample(sampler_ShadowTex, input.tex0);
	float4 shadowDepth = _ShadowDepthTex.Sample(sampler_ShadowDepthTex, input.tex0);

	// Smooth/sharpen the CG to match the scene
	float sharpenKernel[9] = { 0.0f, -1.0f, 0.0f, -1.0f, 5.0f, -1.0f, 0.0f, -1.0f, 0.0f };
	float smoothKernel[9] = { 0.1111f, 0.1111f, 0.1111f, 0.1111f, 0.1111f, 0.1111f, 0.1111f, 0.1111f, 0.1111f };

	int smoothWindowSize = 1;
	int smoothWindowScale = 1;
	int smoothBin = (2 * smoothWindowSize + 1)*(2 * smoothWindowSize + 1);
	float4 baseC;
	float4 base;
	for (int j = -smoothWindowSize; j <= smoothWindowSize; j++) {
		for (int i = -smoothWindowSize; i <= smoothWindowSize; i++) {
			float kernel = smoothKernel[(j + smoothWindowSize)*(2 * smoothWindowSize + 1) + (i + smoothWindowSize)];
			base += kernel * _MainTex.Sample(sampler_MainTex, input.tex0 +
				fixed2(_MainTex_TexelSize.x * i * smoothWindowScale, _MainTex_TexelSize.y * j * smoothWindowScale));
		}
	}
	//base = base / (float)smoothBin;

	// Calculate weight
	if (centerCgDepth.x != 0.0f) {
		for (int j = -windowSize; j <= windowSize; j++) {
			for (int i = -windowSize; i <= windowSize; i++) {
				sceneDepth = _SceneDepthTex.Sample(sampler_SceneDepthTex, input.tex0 +
					fixed2(_SceneDepthTex_TexelSize.x * i * windowScale,
						_SceneDepthTex_TexelSize.y * j * windowScale));

				cgDepthC = _CgDepthTex.Sample(sampler_CgDepthTex, input.tex0 +
					fixed2(_CgDepthTex_TexelSize.x * i * windowScale,
						_CgDepthTex_TexelSize.y * j * windowScale));

				if ((sceneDepth > cgDepthC.x) && (sceneDepth != 0.0f)) {
					if (cgDepthC.x == 0.0f) {
						if (centerSceneDepth > centerCgDepth.x) {
							binTotal++;
						}
					}
					else {
						binTotal++;
					}

				}
			}
		}
	}

	float4 webcamC;
	float4 webcamTotal = float4(0.0f, 0.0f, 0.0f, 0.0f);
	float webcamGrayTotal = 0.0f;
	int webcamWindowSize = 2;
	int maxBinWebcam = (2 * webcamWindowSize + 1)*(2 * webcamWindowSize + 1);
	int webcamWindowScale = 1;

	for (int j = -webcamWindowSize; j <= webcamWindowSize; j++) {
		for (int i = -webcamWindowSize; i <= webcamWindowSize; i++) {
			webcamC = _WebcamTex.Sample(sampler_WebcamTex, input.tex0 +
				fixed2(_WebcamTex_TexelSize.x * i * webcamWindowScale, _WebcamTex_TexelSize.y * j * webcamWindowScale));
			//webcamTotal += webcamC;
			webcamGrayTotal += (webcamC.x + webcamC.y + webcamC.z) / 3.0f;
		}
	}

	_VisibilityComplex = 0.0f;
	webcamGrayTotal = webcamGrayTotal / (float)maxBinWebcam;
	float maxValue = 1.0f;
	float minValue = 0.0f;
	float L = maxValue - minValue;
	float weight = minValue + L * (1.0f - (1.0f / (1.0f + exp(-k * ((float)binTotal - x0)))));

	float4 output;
	if ((centerCgDepth.x == 0)) {
		if (shadowDepth.x > centerSceneDepth) {
			output = webcamCenter * (1.0f - 0.4f * shadow.w);
		}
		else {
			output = webcamCenter;
		}
	}
	else {
		float vv = weight;
		output = vv * base + (1.0f - vv) * webcamCenter;
	}

	return output;
	
	}
		ENDCG
	}
	}
}
