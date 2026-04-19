#ifndef ATMOSPHERE_NODE
#define ATMOSPHERE_NODE

Texture3D<float4> _Noise3D;
SamplerState sampler_Noise3D;

Texture3D<float4> _DetailNoise3D;
SamplerState sampler_DetailNoise3D;

float smootherstep(float edge0, float edge1, float x)
{
    // Scale, bias and saturate x to 0..1 range
    x = saturate((x - edge0) / (edge1 - edge0));
    
    // The Quintic Polynomial: 6x^5 - 15x^4 + 10x^3
    return x * x * x * (x * (x * 6 - 15) + 10);
}

float2 RaySphere(float3 sphereCenter, float radius, float3 rayOrigin, float3 rayDir, float sceneDepth)
{
    float3 viewVector = normalize(rayDir);
    float viewLength = length(rayDir);
	
    //float linearDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
	
	float3 offset = rayOrigin - sphereCenter;
	float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
	float b = 2 * dot(offset, viewVector);
	float c = dot(offset, offset) - radius * radius;
	float d = b * b - 4 * a * c; // Discriminant from quadratic formula

	float2 dsts = float2(-1, 0);

	// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
	if (d > 0) {
		float s = sqrt(d);
		float dstToSphereNear = max(0, (-b - s) / (2 * a));
		float dstToSphereFar = (-b + s) / (2 * a);
		// Ignore intersections that occur behind the ray
		if (dstToSphereFar >= 0) {
			dsts = float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
		}
	}

    dsts.y = min(dsts.y, sceneDepth - dsts.x);
    return dsts;
}

float RayleighPhaseFunction(float theta)
{
    float phase = 3 * (1 + (cos(theta) * cos(theta)) / 4);
    return phase;
}

float densityAtHeight(float3 pos)
{
	float heightAbovePlanet = length(pos - planetCenter) - planetRadius;
    float height01 = saturate(heightAbovePlanet / (atmosphereRadius - planetRadius));
    return exp(-height01 * heightScalar) * (1 - height01);
}

float opticalDepth(float3 rayDir, float rayLength, float3 rayOrigin)
{
    float3 samplePoint = rayOrigin;
    float stepSize = rayLength / (numOpticals - 1);
    float density = 0;
	
    for (int i = 0; i < numOpticals; i++)
	{
        density += (densityAtHeight(samplePoint) * stepSize);
        samplePoint += rayDir * stepSize;
    }
    return density;
}

float3 inScattering(float3 pos, float3 rayDir, float viewLength, float3 sunDir)
{
    float lengthSun = RaySphere(planetCenter, planetRadius, pos, sunDir, 99999).y;

    //float3 toCam = pos - rayOrigin;
    //float lengthCam = length(toCam);
    
    float density = densityAtHeight(pos);
    return density * exp(-(opticalDepth(sunDir, lengthSun, pos) + opticalDepth(rayDir, viewLength, pos)) * scattering);
}

float3 CalculateLight(float3 rayOrigin, float3 rayDir, float2 dsts, float3 sunDir, float3 sceneCol)
{
    float3 light = 0;
    
    float dstTo = dsts.x;
    float dstThrough = dsts.y;
    
    const float epsilon = 0.0001;
    float3 samplePoint = rayOrigin + rayDir * (dstTo + epsilon);
    dstThrough -= (epsilon * 2);
    float stepSize = dstThrough / (numScatters - 1);
    for (int i = 0; i < numScatters; i++)
    {
        light += (inScattering(samplePoint, -rayDir, stepSize * i, sunDir) * stepSize * scattering);
        samplePoint += rayDir * stepSize;
    }
    //float theta = acos(dot(rayDir, sunDir) / dstThrough);
    
    light *= /*RayleighPhaseFunction(theta) */ sunIntensity;
    
    return sceneCol + light;
}

float remap(float value, float minOld, float maxOld, float minNew, float maxNew)
{
    float ratio = (maxNew - minNew) / (maxOld - minOld);
    return minNew + (value - minOld) * ratio;
}

float MiePhaseFunction(float g, float cosTheta)//Change to be dual lob haley greenstein or whatever
{
    return (1 / (4 * 3.1415)) * ((1 - g * g) / pow(1.0 + g * g - 2.0 * g * cosTheta, 1.5));
}

float dualLobPhaseFunction(float forwardScattering, float backScattering, float cosTheta, float t)
{
    return baseBrightness + lerp(MiePhaseFunction(backScattering, cosTheta), MiePhaseFunction(forwardScattering, cosTheta), t) * phaseStrength;
}

float sampleCloudNoise(float3 pos)
{
    //determine coordinates for texture sampling
    float time = _Time.y;
    float3 baseCoord = pos * cloudNoiseScale + windDir * time * baseCloudSpeed;
    float3 detailCoord = pos * cloudDetailScale + windDir * time * detailCloudSpeed;
    
    float4 wp = worleyPersistance;
    float4 baseNoise = _Noise3D.SampleLevel(sampler_Noise3D, baseCoord, 0);
    float baseFBM = baseNoise.r * wp.r + baseNoise.g * wp.g + baseNoise.b * wp.b + baseNoise.a * wp.a;
    //return baseFBM;
    
    wp = detailPersistance;
    float4 detailNoise = _DetailNoise3D.SampleLevel(sampler_DetailNoise3D, detailCoord, 0);
    float detailFBM = detailNoise.r * wp.r + detailNoise.g * wp.g + detailNoise.b * wp.b + detailNoise.a * wp.a;
    //return baseFBM;
    float density = remap(baseFBM, detailFBM /* cloudRemap*/, 1.0, 0.0, 1.0);
    return saturate(density);
}

float cloudDensityAtStep(float3 pos)//Samples from our cloud-shaped texture
{
    float absorption = sampleCloudNoise(pos);
    //return absorption;

    float distToCenter = length(pos - planetCenter);
    float distFromEdge = outerCloudRadius - distToCenter;
    float bandRatio = saturate(distFromEdge / (outerCloudRadius - innerCloudRadius));
    float fallOff = smootherstep(1, 1 - cloudFalloff, bandRatio) * smootherstep(0, cloudFalloff, bandRatio);

    float finalDensity = max(0, absorption - cloudDensityThreshold);
    return finalDensity * cloudDensityMultiplier * fallOff;
}

float cloudOpticalDepth(float3 rayDir, float3 rayOrigin)
{
    float lengthSun = RaySphere(planetCenter, outerCloudRadius, rayOrigin, rayDir, 99999).y;
    
    float planetCheck = RaySphere(planetCenter, planetRadius, rayOrigin, rayDir, 9999).x;
    //check if the location is behind the planet.
    //if (planetCheck != -1)
    //    return 999;
    
    float3 samplePoint = rayOrigin;
    float stepSize = lengthSun / (numCloudOpticals - 1);
    float density = 0;
	
    for (int i = 0; i < numCloudOpticals; i++)
	{
        density += max(0, cloudDensityAtStep(samplePoint) * stepSize);
        samplePoint += rayDir * stepSize;
    }
    float transmittance = density * lightAbsorptionTowardSun;
    return transmittance;
}

float4 DetermineClouds(float3 rayOrigin, float3 rayDir, float2 dsts, float3 sunDir, float3 sceneCol, float numCloudScatters, float transmittance = 1)
{
    float light = 0;
    const float epsilon = 0.0001;
    float3 samplePoint = rayOrigin + rayDir * (dsts.x + epsilon);
    dsts.y -= (epsilon * 2);
    float stepSize = dsts.y / (numCloudScatters - 1); 
    
    float cosTheta = dot(rayDir, sunDir);
    float phase = dualLobPhaseFunction(cloudForwardScattering, cloudBackScattering,
    cosTheta, cloudScatteringInterpolant);
    
    for (int i = 0; i < numCloudScatters; i++)//Ray marching loop
    {
        float density = cloudDensityAtStep(samplePoint);
        if(density > 0)
        {
            float d = cloudOpticalDepth(sunDir, samplePoint);
            float lightTransmittance = exp(-d) * (1 - exp(-2 * d));
            light += density * stepSize * lightTransmittance * transmittance;
            transmittance *= exp(-density * stepSize * lightAbsorptionThroughCloud);
            if (transmittance < 0.01)
            {
                transmittance = 0;
                break;
            }
        }
        samplePoint += rayDir * stepSize;
    }
    
    float3 cloudCol = light * sunCol * phase;
    return float4(cloudCol, transmittance);
}

#endif //ATMOSPHERE_NODE