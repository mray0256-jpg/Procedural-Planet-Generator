Shader "Planets/AtmosphereShader"
{
    Properties
    {
        //GLOBAL VARIABLE

        //atmosphere
        planetCenter("Planet Center", Vector) = (0, 0, 0)
        sunCenter("Sun Center", Vector) = (5, 5, 5)
        planetRadius("Planet Radius", float) = 5.0
        atmosphereRadius("atmosphere Radius", float) = 6.0
        heightScalar("height scalar", float) = 4.0
        numOpticals("optics", int) = 5
        numScatters("scatters", int) = 5
        sunIntensity("intense", float) = 1.0
        scattering("scattering", Vector) = (0, 0, 0)

        //clouds
        innerCloudRadius("InnerCloudRadius", float) = 5.5
        outerCloudRadius("OuterCloudRadius", float) = 5.8
        cloudFalloff("cloudFallOff", float) = 0.2
        cloudDensityThreshold("cloudDensityThreshold", float) = 0.5
        cloudDensityMultiplier("cloudDensityMultiplier", float) = 2
        sunCol("Sun Color", Vector) = (1, 1, 1)
        numCloudOpticals("num cloud opticals", float) = 6
        numCloudScatters("num cloud scatters", float) = 10
        lightAbsorptionThroughCloud("light absorption through cloud", float) = 1
        lightAbsorptionTowardSun("light absorption toward sun", float) = 1
        worleyPersistance("Worley Octave persistance", Vector) = (0.525, 0.25, 0.125, 0.1)
        detailPersistance("Detail Octave persistance", Vector) = (0.525, 0.25, 0.125, 0.1)
        windDir("Wind direction", Vector) = (0, 0, 0)
        baseCloudSpeed("base Cloud Speed", float) = 0.5
        detailCloudSpeed("detail Cloud Speed", float) = 0.1
        cloudNoiseScale("cloud Noise Scale", float) = 0.2
        cloudDetailScale("cloud Detail Scale", float) = 1
        cloudRemap("cloud Remap Constant", float) = 0.2
        cloudForwardScattering("cloud Forward Scattering", float) = 0.8
        cloudBackScattering("cloud Back Scattering", float) = 0.2
        cloudScatteringInterpolant("cloud Scattering Interpolant", float) = 0.3
        baseBrightness("scattering base brightness", float) = 0.8
        phaseStrength("scattering phase strength", float) = 0.15
        blueNoise ("Blue Noise", 2D) = "white" {}
        jitterStrength("Jitter strength", float) = 20
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        
        Cull Off ZWrite Off ZTest Always

        Pass
        {

            Name "AtmospherePass"

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            //#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Noise.hlsl"
            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Noise.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float3 planetCenter;
                float3 sunCenter;
                float planetRadius;
                float atmosphereRadius;
                float heightScalar;
                float scatteringConstant;
                int numOpticals;
                int numScatters;
                float sunIntensity;
                float3 wavelengths;
                float3 scattering;
                float innerCloudRadius;
                float outerCloudRadius;
                float cloudFalloff;
                float cloudDensityThreshold;
                float darknessThreshold;
                float cloudDensityMultiplier;
                float3 sunCol;
                float lightAbsorptionThroughCloud;
                float lightAbsorptionTowardSun;
                float phaseVal;
                float numCloudOpticals;
                float numCloudScatters;
                float4 worleyPersistance;
                float4 detailPersistance;
                float3 windDir;
                float baseCloudSpeed;
                float detailCloudSpeed;
                float cloudNoiseScale;
                float cloudDetailScale;
                float cloudRemap;
                float cloudForwardScattering;
                float cloudBackScattering;
                float cloudScatteringInterpolant;
                float baseBrightness;
                float phaseStrength;
                float4 blueNoise_ST;
                float jitterStrength;
            CBUFFER_END

            #include "AtmosphereNode.hlsl"

            struct appdata
            {
                uint vertex : SV_VertexID;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
            };

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            v2f vert(appdata i)
            {
                v2f o = (v2f)0;
                o.positionCS = GetFullScreenTriangleVertexPosition(i.vertex);
                o.uv = GetFullScreenTriangleTexCoord(i.vertex);
                float3 viewVector = mul(unity_CameraInvProjection, float4(o.uv * 2 - 1, 0, -1));
			    o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return o;
            }

            TEXTURE2D(blueNoise);
            SAMPLER(sampler_blueNoise);

            float GetBlueNoiseJitter(float2 screenUV)
            {
                float2 noiseUV = screenUV * (_ScreenParams.xy)/50;
                return blueNoise.SampleLevel(sampler_blueNoise, noiseUV, 0).r * 0.1;
            }

            float4 frag(v2f i) : SV_Target 
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
                float3 rayOrigin = GetCameraPositionWS();
                float jitter = GetBlueNoiseJitter(uv);
                float3 rayDir = normalize(i.viewVector);
                float rawDepth = SampleSceneDepth(uv);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                
                float3 sunDir = normalize(sunCenter - planetCenter);
                float4 returnColor = sceneColor;
                float3 zero = float3(0, 0, 0);

                float3 c = 0;

                float2 outerCloudDists = RaySphere(planetCenter, outerCloudRadius, rayOrigin, rayDir, 99999);
                if(outerCloudDists.x != -1 && outerCloudDists.x < sceneDepth)
                {
                    
                    float2 frontCloudData = float2(outerCloudDists.x + jitter * jitterStrength, min(sceneDepth - outerCloudDists.x, outerCloudDists.y));
                    float4 front = DetermineClouds(rayOrigin, rayDir, frontCloudData, sunDir, sceneColor, numCloudScatters * 2);
                    c = front.xyz;
                    returnColor *= front.a;
                }
                returnColor += float4(c.xyz, 0);
                //return float4(returnColor.xyz, 0);

                //atmosphere
                float2 dsts = RaySphere(planetCenter, atmosphereRadius, rayOrigin, rayDir, sceneDepth);
                float3 light = 1;
                if(dsts.y > 0)
                {
                    light = CalculateLight(rayOrigin, rayDir, dsts, sunDir, zero);
                    //return float4(light, 0);
                    returnColor += float4(light, 0);
                }
                return returnColor;
            }


            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask R

            HLSLPROGRAM

            #pragma vertex depthOnlyVert
            #pragma fragment depthOnlyFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
            };

            v2f depthOnlyVert(appdata v)
            {
                v2f o = (v2f)0;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);

                return o;
            }

            float depthOnlyFrag(v2f i) : SV_Target
            {
                return i.positionCS.z;
            }

            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            ZWrite On

            HLSLPROGRAM
            
            #pragma vertex depthNormalsVert
            #pragma fragment depthNormalsFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            v2f depthNormalsVert(appdata v)
            {
                v2f o = (v2f)0;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.normalWS = NormalizeNormalPerVertex(normalWS);

                return o;
            }

            float4 depthNormalsFrag(v2f i) : SV_Target
            {
                float3 normalWS = NormalizeNormalPerPixel(i.normalWS);

                return float4(normalWS, 0.0f);
            }


            ENDHLSL
        }
     }
}