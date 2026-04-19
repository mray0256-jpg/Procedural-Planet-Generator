#ifndef ATMOSPHERE_NODE
#define ATMOSPHERE_NODE

float2 RaySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir)
{
	float3 offset = rayOrigin - sphereCentre;
	float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
	float b = 2 * dot(offset, rayDir);
	float c = dot(offset, offset) - sphereRadius * sphereRadius;
	float d = b * b - 4 * a * c; // Discriminant from quadratic formula

	float2 intersection = float2(-1, 0);

	// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
	if (d > 0) {
		float s = sqrt(d);
		float dstToSphereNear = max(0, (-b - s) / (2 * a));
		float dstToSphereFar = (-b + s) / (2 * a);
		// Ignore intersections that occur behind the ray
		if (dstToSphereFar >= 0) {
			intersection = float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
		}
	}


	return intersection;
}

#endif //ATMOSPHERE_NODE