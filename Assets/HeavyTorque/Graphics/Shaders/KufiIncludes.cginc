#ifndef KUFI_COMMON
#define KUFI_COMMON

#define OBJ_TO_WORLD_VECTOR(v) normalize(mul(v.xyz, (float3x3)unity_WorldToObject))

#include "UnityStandardConfig.cginc"
#include "UnityCG.cginc"
#include "LightVolumes.cginc"

struct surfaceData {
	float3 worldPos;
	float3 normal;
	float3 viewDir;
};

struct light {
	float3 direction;
	float3 color;
};

struct lightingDots {
	float NdotL;
	float NdotV;
	float NdotH;
	float VdotH;
	float LdotH;
	float VdotL;
};

lightingDots CalculateLightingDots(float3 viewDirection, float3 normal, float3 lightDirection)
{
	lightingDots dots;
	float3       h = normalize(viewDirection + lightDirection);
	dots.NdotL = saturate(dot(normal, lightDirection));
	dots.NdotV = max(0, dot(normal, viewDirection));
	dots.NdotH = saturate(dot(normal, h));
	dots.VdotH = max(0, dot(viewDirection, h));
	dots.LdotH = saturate(dot(lightDirection, h));
	dots.VdotL = dot(viewDirection, lightDirection);
	return dots;
}


float3x3 CalculateTBN(float3 normal, float3 tangent, float tangentW)
{
	float3 bitangent = cross(normal, tangent) * tangentW;
	return float3x3(tangent, bitangent, normal);
}

float CalculateMipLevel(in float2 pixelCoordinate) // pixelCoordinate = uv_MainTex * _MainTex_TexelSize.zw
{
	float2 dx_vtc = ddx(pixelCoordinate);
	float2 dy_vtc = ddy(pixelCoordinate);
	float md = max(dot(dx_vtc, dx_vtc), dot(dy_vtc, dy_vtc));
	return 0.5 * log2(md);
}

float2 HeightOffset(
	float2    uv,
	float3    tangentView,
	sampler2D heightmap,
	float     depth,
	float     smoothing = 0.9,
	int       steps = 5,
	int       binarySearchSamples = 5
)
{
	if (abs(depth) < 1e-3) return uv;
	float2 newUv = uv;
	float2 offset = tangentView.xy / tangentView.z * depth;
	float  currentHeight = 0;
	float  velocity = 0;

	float sample;
	float delta;

	[loop]
	for (int s = 0; s < min(100, steps); s++) {
		sample = 1 - tex2D(heightmap, newUv).r;
		delta = sample - currentHeight;

		if (binarySearchSamples > 0 && delta < 0) {
			velocity *= -1;
			break;
		}

		velocity += 1 - smoothing;
		velocity *= delta;
		currentHeight += velocity;
		newUv = uv - offset * currentHeight;
	}

	[loop]
	for (int s2 = 0; s2 < min(10, binarySearchSamples); s2++) {
		velocity *= 0.5;
		if (abs(velocity) < 0.001) break;
		currentHeight += velocity;
		newUv = uv - offset * currentHeight;

		sample = 1 - tex2D(heightmap, newUv).r;
		delta = sample - currentHeight;

		velocity = abs(velocity) * sign(delta);
	}

	return newUv;
}

float2 HeightOffset(
	float2        uv,
	float3        tangentView,
	SamplerState samplerState,
	Texture2D     heightmap,
	float         depth,
	float         smoothing = 0.9,
	int           steps = 5,
	int           binarySearchSamples = 5
)
{
	float2 newUv = uv;
	float2 offset = tangentView.xy / tangentView.z * depth;
	float  currentHeight = 0;
	float  velocity = 0;

	float sample;
	float delta;

	[loop]
	for (int s = 0; s < min(100, steps); s++) {
		sample = 1 - heightmap.Sample(samplerState, newUv).r;
		delta = sample - currentHeight;

		// if (binarySearchSamples > 0 && delta < 0) {
		// 	velocity *= -1;
		// 	break;
		// }

		velocity += 1 - smoothing;
		velocity *= delta;
		currentHeight += velocity;
		newUv = uv - offset * currentHeight;
	}

	// [loop]
	// for (int s2 = 0; s2 < min(10, binarySearchSamples); s2++) {
	// 	velocity *= 0.5;
	// 	if (abs(velocity) < 0.001) break;
	// 	currentHeight += velocity;
	// 	newUv = uv - offset * currentHeight;
	//
	// 	sample = 1 - heightmap.Sample(samplerState, newUv).r;
	// 	delta = sample - currentHeight;
	//
	// 	velocity = abs(velocity) * sign(delta);
	// }

	return newUv;
}

// Geometric Shadowing Functions
float GSFCookTorrence(float NdotL, float NdotV, float VdotH, float NdotH)
{
	float Gs = min(1.0, min(2 * NdotH * NdotV / VdotH,
	                        2 * NdotH * NdotL / VdotH));
	return Gs;
}

float GSFSchlickGGX(float roughness, float NdotL, float NdotV)
{
	float k = roughness / 2;

	float SmithL = NdotL / (NdotL * (1 - k) + k);
	float SmithV = NdotV / (NdotV * (1 - k) + k);

	float Gs = SmithL * SmithV;
	return Gs;
}

// Hammon 2017, "PBR Diffuse Lighting for GGX+Smith Microsurfaces"
float GSFSmithGGXCorrelatedFast(float roughness, float NdotV, float NdotL)
{
	float v = 0.5 / lerp(2.0 * NdotL * NdotV, NdotL + NdotV, roughness);
	return saturate(v) + 1e-5;
}

// Fresnel
float FresnelSchlick(float cosTheta)
{
	return pow(1 - cosTheta, 5);
}

float3 FresnelSchlick(float cosTheta, float3 f0)
{
	return f0 + (1 - f0) * pow(1 - cosTheta, 5);
}

// GGX Normal Distribution Function
float DistributionGGX(float NdotH, float roughness)
{
	float oneMinusNoHSquared = 1.0f - NdotH * NdotH;
	float a = NdotH * roughness;
	float k = roughness / (oneMinusNoHSquared + a * a);
	return k * k * (1.0f / UNITY_PI);
}

// Geometry function
float GeometrySchlickGGX(float NdotV, float roughness)
{
	float r = roughness + 1.0;
	float k = (r * r) / 8.0;
	return NdotV / (NdotV * (1.0 - k) + k);
}

float GeometrySmith(float3 NdotV, float3 NdotL, float roughness)
{
	return GeometrySchlickGGX(max(NdotL, 0.0), roughness) *
	GeometrySchlickGGX(max(NdotV, 0.0), roughness);
}

// Environment mapping
float3 BoxProjection(
	float3 direction, float3       position,
	float4 cubemapPosition, float4 boxMin, float4 boxMax
)
{
	UNITY_BRANCH
	if (cubemapPosition.w > 0) {
		float3 factors =
		((direction > 0 ? boxMax.xyz : boxMin.xyz) - position) / direction;
		float scalar = min(min(factors.x, factors.y), factors.z);
		direction = direction * scalar + (position - cubemapPosition.xyz);
	}
	return direction;
}

float3 SampleEnvironment(float3 worldPos, float3 normal, float mip)
{
	float3 uvw = BoxProjection(
		normal, worldPos, unity_SpecCube0_ProbePosition,
		unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax
	);
	float3 color = UNITY_SAMPLE_TEXCUBE_LOD(
		unity_SpecCube0, uvw, mip
	);

	color = DecodeHDR(half4(color, 1), unity_SpecCube0_HDR);

	float blend = unity_SpecCube0_BoxMin.w;
	if (blend < 0.99999) {
		uvw = BoxProjection(
			normal, worldPos,
			unity_SpecCube1_ProbePosition,
			unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax
		);
		float3 sample = UNITY_SAMPLE_TEXCUBE_SAMPLER_LOD(
			unity_SpecCube1, unity_SpecCube0, uvw, mip
		);
		sample = DecodeHDR(half4(sample, 1), unity_SpecCube1_HDR);
		color = lerp(sample.rgb, color, blend);
	}
	return color;
}

float3 ShadeSpecular(lightingDots dots, float roughness, float3 f0)
{
	float       r = roughness * roughness + 1e-4f;
	float       D = DistributionGGX(dots.NdotH, r);
	const float limit = 1000;
	D = limit * (1 - exp(-D / limit));
	float  G = GSFSmithGGXCorrelatedFast(r, dots.NdotV, dots.NdotL);
	float3 F = FresnelSchlick(dots.LdotH, f0);

	float3 spec = max(0, D * G * UNITY_PI) * F;

	float3 total = spec * dots.NdotL;

	return total;
}

half3 EnvBRDFApprox(half3 specularColor, half roughness, half NdotV )
{
	const half4 c0 = { -1, -0.0275, -0.572, 0.022 };
	const half4 c1 = { 1, 0.0425, 1.04, -0.04 };
	half4 r = roughness * c0 + c1;
	half a004 = min( r.x * r.x, exp2( -9.28 * NdotV ) ) * r.x + r.y;
	half2 AB = half2( -1.04, 1.04 ) * a004 + r.zw;
	return specularColor * AB.x + AB.y;
}

float3 ShadeEnvironmentSpecular(surfaceData d, lightingDots dots, float roughness, float3 f0)
{
	float  environmentLodLevel = roughness * UNITY_SPECCUBE_LOD_STEPS;
	float3 reflections = SampleEnvironment(d.worldPos, reflect(-d.viewDir, d.normal), environmentLodLevel);
	return reflections * EnvBRDFApprox(f0, roughness, dots.NdotV);

	float3 f90 = 1 - roughness * roughness + f0;
	float3 F = lerp(f0, f90, FresnelSchlick(dots.NdotV));
	return reflections * F;
}

float RetroReflectionFactor(float VdotL, float diffusion)
{
	float r2 = 1 / (diffusion + 1e-5);
	return pow(VdotL, r2) * (r2 + 1) / 2;
}

float3 ShadeAmbient(surfaceData d)
{
	// return ShadeSH9(float4(d.normal, 1));
	float3 L0;
	float3 L1r;
	float3 L1g;
	float3 L1b;
	LightVolumeSH(d.worldPos, L0, L1r, L1g, L1b);

	#ifndef LIGHTMAP_OFF
	LightVolumeAdditiveSH(d.worldPos, L0, L1r, L1g, L1b);
	#endif

	return LightVolumeEvaluate(d.normal, L0, L1r, L1g, L1b);
}

float3 ShadeAmbient(surfaceData d, float roughness)
{
	float3 L0;
	float3 L1r;
	float3 L1g;
	float3 L1b;
	LightVolumeSH(d.worldPos, L0, L1r, L1g, L1b);
	float3 dominantLightDirection = (L1r + L1g + L1b) / 3;
	float  directionality = length(dominantLightDirection);
	lightingDots ambientDots = CalculateLightingDots(d.viewDir, d.normal, normalize(dominantLightDirection));
	float G = 1 - GSFSmithGGXCorrelatedFast(roughness, ambientDots.NdotV, ambientDots.NdotL);

	#ifndef LIGHTMAP_OFF
		LightVolumeAdditiveSH(d.worldPos, L0, L1r, L1g, L1b);
	#endif

	return LightVolumeEvaluate(d.normal, L0, L1r, L1g, L1b) * lerp(1, G, directionality);
}

float SimpleMie(float costh)
{
	return ((costh + 0.7) * costh * costh * costh + 0.2) * 2.;
}

float3 ShadeSubsurface(surfaceData d, float thick, float3 scatterCoefficients, light l)
{
	float3 subsurfaceLight = l.color;

	// At steep angles, the light can glance off of thin subsurface layers and exit sooner, so the optical depth will be smaller
	// Even thick materials when receiving light at a steep angle will have some amount of subsurface effect
	float falloff = dot(l.direction, d.normal);
	
	// float fresnelSubsurfaceEffect = pow(, 1 / subsurfaceLight + 1) + 0.01;
	// This line tries to approximate the thickness by assuming the surface is a sphere
	float thickness = sin(acos(1 - abs(falloff))) * thick;
	thickness = smoothstep(-0.5, 1, thickness);
	
	// Light will scatter less through thin materials
	// Lastly, light is scattered differently based on how much material we are passing through
	float3 opticalDepth = thickness;
	subsurfaceLight *= exp(-opticalDepth / scatterCoefficients) * (1 - exp(-opticalDepth * 2));
	// subsurfaceLight *= exp(-opticalDepth * (1 / scatterCoefficients)); // <-- Where the magic scattering happens

	// Light is more likely to scatter in the direction it was going when it entered the material
	float directionBias = SimpleMie(falloff);
	subsurfaceLight *= directionBias;
	
	return subsurfaceLight;
}

// These all assume you have passed lighting data out from the vertex / geometry shader
// i = the vertex output / fragment input (Vert to frag or geom to frag if you have a geometry shader)
// d = distance from surface to light
// r = the lights range
#if defined(POINT) || defined(POINT_COOKIE)
	#define LIGHT_DISTANCE_FALLOFF(d, r) (1 / pow(d / 10 + 1, 2) * pow(1 - min(1, d / r), 2))
#elif defined(SPOT)
	#define LIGHT_DISTANCE_FALLOFF(d, r) (1 / pow(d / 10 + 1, 2) * pow(1 - min(1, d / r), 2))
#endif

#ifdef POINT
	#define LIGHT_FALLOFF(i, d, r) (LIGHT_DISTANCE_FALLOFF(d, r))
#endif

#ifdef SPOT
	// Unity spot light falloff looks very linear and does not have a bright spot near the origin.
	// This approach is more physically based but needs a little nudge where the distance is divided by 5 to more closely match how Unity's falloff looks.
	#define LIGHT_FALLOFF(i, d, r) (LIGHT_DISTANCE_FALLOFF(d, r) * UnitySpotCookie(i._LightCoord))
#endif

#ifdef POINT_COOKIE
	#define LIGHT_FALLOFF(i, d, r) (LIGHT_DISTANCE_FALLOFF(d, r) * texCUBE(_LightTexture0, i._LightCoord).w)
#endif

#ifdef DIRECTIONAL_COOKIE
	#define GET_DIRECTIONAL_COOKIE(i) (tex2D(_LightTexture0, i._LightCoord).w)
#endif

#endif
