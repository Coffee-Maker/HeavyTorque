Shader "Kufi/Diffuse"
{
    Properties
    {
        [Header(Color)]
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Surface)]
        _Roughness("Roughness", Range(0, 1)) = 1.0
        [NoScaleOffset] _SpecularMap("Specular Map", 2D) = "white" {}
        _SpecularTint("Specular Tint", Color) = (0.21, 0.21, 0.21, 1)

        [NoScaleOffset] _MetallicMap("Metallic Map", 2D) = "white" {}
        _Metallicity("Metallicity", Range(0, 1)) = 0.0

        _DetailMap("Detail Map", 2D) = "white" {}
        _DetailPower("Detail Power", Range(0, 5)) = 1.0

        [Header(Subsurface)]
        _ScatteringCoefficients("Scattering Coefficients", Vector) = (1, 1, 1, 0)
        _Thickness("Thickness", Range(0, 1)) = 1.0
        _SubsurfaceTint("Subsurface Tint", Color) = (1, 1, 1, 1)

        [Header(Normal)]
        _NormalMap0("Normal Map 0", 2D) = "bump" {}
        _NormalStrength0 ("Normal Strength 0", Float) = 1
        _NormalMap1("Normal Map 1", 2D) = "bump" {}
        _NormalStrength1 ("Normal Strength 1", Float) = 1

        [Header(Emission)]
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "black" {}
        [HDR] _EmissionTint("Emission Tint", Color) = (1, 1, 1, 1)

        [Header(Technical shit)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", int) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Toggle] _ZWrite ("ZWrite", Float) = 1

        _Tiling("Tiling", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "LightMode" = "ForwardBase" "RenderType" = "Opaque"
        }

        // Forward Base
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]

            CGPROGRAM
            #pragma vertex baseVert
            #pragma fragment baseFrag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "UnityLightingCommon.cginc"
            #include "KufiIncludes.cginc"

            sampler2D _MainTex;
            float     _Cutoff;
            float4    _Tint;
            float     _Roughness;
            float3    _SpecularTint;
            sampler2D _SpecularMap;
            sampler2D _MetallicMap;
            float     _Metallicity;
            
            sampler2D _DetailMap;
            float4   _DetailMap_ST;
            float _DetailPower;

            float3 _ScatteringCoefficients;
            float  _Thickness;
            float3 _SubsurfaceTint;

            sampler2D _NormalMap0;
            float4    _NormalMap0_ST;
            float     _NormalStrength0;
            sampler2D _NormalMap1;
            float4    _NormalMap1_ST;
            float     _NormalStrength1;

            sampler2D _EmissionMap;
            float     _MipLimit;

            float4 _EmissionTint;

            float4 _Tiling;

            struct appdata {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 color : COLOR;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float4 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 uvs : TEXCOORD0_centroid;
                float3 worldPos : TEXCOORD2;
                float3 objectPos : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                UNITY_FOG_COORDS(5)
                UNITY_LIGHTING_COORDS(6, 7)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f baseVert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_LIGHTING(o, v.uv1);

                o.uvs = float4(v.uv, v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw);
                o.color = float4(GammaToLinearSpace(v.color), v.color.a);
                o.normal = float4(normalize(OBJ_TO_WORLD_VECTOR(v.normal)), 0);
                o.tangent = float4(normalize(OBJ_TO_WORLD_VECTOR(v.tangent.xyz)), v.tangent.w);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.objectPos = v.vertex.xyz;
                o.screenPos = ComputeScreenPos(o.pos);

                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 baseFrag(v2f i, int orientation: VFACE) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                UNITY_SETUP_INSTANCE_ID(i);

                float3x3 worldToTangent = CalculateTBN(normalize(i.normal) * sign(orientation), normalize(i.tangent.xyz), sign(i.tangent.w));
                float3x3 tangentToWorld = transpose(worldToTangent);

                surfaceData d;
                d.worldPos = i.worldPos;
                d.viewDir = normalize(UnityWorldSpaceViewDir(d.worldPos));

                float2 uv = i.uvs.xy * _Tiling.xy + _Tiling.zw;

                // Albedo
                float4 diffuseColor = tex2D(_MainTex, uv) * i.color * _Tint;
                clip(diffuseColor.a - _Cutoff);
                
                // Detail
                float4 detailColor = tex2D(_DetailMap, uv * _DetailMap_ST.xy + _DetailMap_ST.zw);
                diffuseColor.rgb *= pow(detailColor.rgb, _DetailPower);

                // Normals
                float3 tangentNormal0 = UnpackNormalWithScale(tex2D(_NormalMap0, uv * _NormalMap0_ST.xy + _NormalMap0_ST.zw), _NormalStrength0);
                float3 tangentNormal1 = UnpackNormalWithScale(tex2D(_NormalMap1, uv * _NormalMap1_ST.xy + _NormalMap1_ST.zw), _NormalStrength1);
                float3 finalTangentNormal = normalize(tangentNormal0 + tangentNormal1);
                d.normal = mul(tangentToWorld, finalTangentNormal);
                // d.normal = i.normal;
                // return float4(d.normal, 1);

                float4 specularMap = tex2D(_SpecularMap, uv);
                float  roughness = _Roughness * specularMap.a;
                float3 specularColor = specularMap.rgb * _SpecularTint;

                // Metallics (Cheating)
                float metallicity = tex2D(_MetallicMap, uv).r * _Metallicity;
                specularColor *= 1 - metallicity + diffuseColor.rgb * metallicity;
                diffuseColor.rgb *= 1 - metallicity;

                // Lighting
                float shadowAtten = UnityComputeForwardShadows(i.uvs.zw, d.worldPos, i.screenPos);

                light light;
                light.direction = length(_WorldSpaceLightPos0) > 0.001 ? _WorldSpaceLightPos0 : float3(0.5, 1, 0);
                light.color = _LightColor0.rgb * shadowAtten;

                lightingDots dots = CalculateLightingDots(d.viewDir, d.normal, light.direction);
                float3       finalColor = 0;

                float  diffuse = (1 - FresnelSchlick(dots.VdotH)) * dots.NdotL;
                float3 analyticalSpecularLight = ShadeSpecular(dots, roughness, specularColor) * light.color;
                float3 analyticalDiffuseLight = diffuseColor * diffuse * light.color;
                float3 analyticalSubsurfaceLight = ShadeSubsurface(d, _Thickness, _ScatteringCoefficients, light) * diffuseColor.a * diffuseColor.rgb *
                _SubsurfaceTint;

                float3 ambientDiffuse = ShadeAmbient(d) * diffuseColor;
                float3 environmentSpecularLight = ShadeEnvironmentSpecular(d, dots, roughness, specularColor);

                #ifndef LIGHTMAP_OFF
                float3 lightmapColor = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, i.uvs.zw));
                environmentSpecularLight *= smoothstep(_BakedSpecularRejection, _BakedSpecularRejection + 0.01, length(lightmapColor));
                #ifdef DIRLIGHTMAP_COMBINED
                float3 boostedNormal = normalize(float3(tangentNormal.x * 1, tangentNormal.y * 1, tangentNormal.z));
                boostedNormal = normalize(mul(tangentToWorld, boostedNormal));
                fixed4 bakedDir = UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd, unity_Lightmap, i.uvs.zw);
                lightmapColor = DecodeDirectionalLightmap(lightmapColor, bakedDir, boostedNormal);
                // float3 dominantDirection = bakedDir.xyz * 2 - 1;
                // lightingDots bakedDots = CalculateLightingDots(d.viewDir, d.normal, dominantDirection);
                // float3 bakedSpecularLight = ShadeSpecular(bakedDots, roughness, specularColor);
                // finalColor += (bakedSpecularLight * lightmapColor / bakedDir.a) * roughness * roughness;
                #endif
                // environmentSpecularLight *= 1 - max(0, dot(float3(0, -1, 0), d.normal) * 0.5 + 0.5); // Dimming upside down normals because they look weird sometimes
                // environmentSpecularLight *= saturate(dot(lightmapColor, 1)); // Faking ambient occlusion and dimming environment reflections
                finalColor += lightmapColor * diffuseColor;
                #endif

                finalColor += ambientDiffuse;
                finalColor += analyticalDiffuseLight;
                finalColor += analyticalSpecularLight;
                finalColor += environmentSpecularLight;
                finalColor += analyticalSubsurfaceLight;

                // Emission
                finalColor += tex2D(_EmissionMap, uv).rgb * _EmissionTint * i.color;

                float4 result = float4(finalColor, diffuseColor.a);
                UNITY_APPLY_FOG(i.fogCoord, result);

                return result;
            }
            ENDCG
        }

        // Additional lights
        Pass
        {
            Name "AdditionalLight"
            Tags
            {
                "LightMode" = "ForwardAdd"
            }
            Blend One One
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex additiveVert
            #pragma fragment additiveFrag
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "UnityLightingCommon.cginc"
            #include "KufiIncludes.cginc"

            sampler2D _MainTex;
            float     _Cutoff;
            float4    _Tint;
            sampler2D _SpecularMap;
            sampler2D _MetallicMap;
            float     _Metallicity;
            float     _Roughness;
            float3    _SpecularTint;

            float3 _ScatteringCoefficients;
            float  _Thickness;
            float3 _SubsurfaceTint;

            sampler2D _NormalMap;
            float4    _NormalMap_ST;
            float     _NormalStrength;

            float4 _Tiling;

            struct appdata {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 color : COLOR;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float4 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 uvs : TEXCOORD0_centroid;
                float3 worldPos : TEXCOORD2;
                float3 objectPos : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                UNITY_FOG_COORDS(5)
                UNITY_LIGHTING_COORDS(6, 7)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f additiveVert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_LIGHTING(o, v.uv1);

                o.uvs = float4(v.uv, v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw);
                o.color = float4(GammaToLinearSpace(v.color), v.color.a);
                o.normal = float4(normalize(OBJ_TO_WORLD_VECTOR(v.normal)), 0);
                o.tangent = float4(normalize(OBJ_TO_WORLD_VECTOR(v.tangent.xyz)), v.tangent.w);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.objectPos = v.vertex.xyz;
                o.screenPos = ComputeScreenPos(o.pos);

                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 additiveFrag(v2f i, int orientation: VFACE) : COLOR
            {
                float2 uv = i.uvs.xy * _Tiling.xy + _Tiling.zw;
                float4 diffuseColor = tex2D(_MainTex, uv) * i.color * _Tint;
                clip(diffuseColor.a - _Cutoff);

                surfaceData d;
                d.worldPos = i.worldPos;
                d.viewDir = normalize(UnityWorldSpaceViewDir(d.worldPos));

                // Normals
                float3x3 TBN = CalculateTBN(normalize(i.normal) * sign(orientation), normalize(i.tangent.xyz), i.tangent.w);
                float3   tangentNormal = UnpackNormalWithScale(tex2D(_NormalMap, uv * _NormalMap_ST.xy + _NormalMap_ST.zw), _NormalStrength);
                d.normal = mul(transpose(TBN), tangentNormal);

                float4 specularMap = tex2D(_SpecularMap, uv);
                float  roughness = _Roughness * specularMap.a;
                float3 specularColor = specularMap.rgb * _SpecularTint;

                float metallicity = tex2D(_MetallicMap, uv).r * _Metallicity;
                specularColor *= 1 - metallicity + diffuseColor.rgb * metallicity;
                diffuseColor.rgb *= 1 - metallicity;

                light light;
                fixed atten = SHADOW_ATTENUATION(i);
                light.color = _LightColor0 * atten;

                #if defined (POINT) || defined (SPOT) || defined(POINT_COOKIE)
                light.direction = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                float range = 1 / length(mul(float3(1, 0, 0), (float3x3)unity_WorldToLight));
                float distance = length(i.worldPos - _WorldSpaceLightPos0);
                light.color *= LIGHT_FALLOFF(i, distance, range);
                #elif defined(DIRECTIONAL_COOKIE)
                light.direction = _WorldSpaceLightPos0.xyz;
                light.color *= GET_DIRECTIONAL_COOKIE(i);
                #elif defined(DIRECTIONAL)
                light.direction = _WorldSpaceLightPos0.xyz;
                #endif

                #if defined(SPOT)
                DECLARE_LIGHT_COORD(i, i.worldPos);
                light.color *= lightCoord.z > 0;
                #endif

                lightingDots dots = CalculateLightingDots(d.viewDir, d.normal, light.direction);

                float3 analyticalDiffuseLight = dots.NdotL * diffuseColor * diffuseColor.a;
                float3 analyticalSubsurfaceLight = ShadeSubsurface(d, _Thickness, _ScatteringCoefficients, light) * diffuseColor.a * diffuseColor.rgb *
                _SubsurfaceTint;

                float3 analyticalSpecularLight = ShadeSpecular(dots, roughness, specularColor) * light.color;
                float3 totalAnalyticalLight = (analyticalDiffuseLight + analyticalSpecularLight) * light.color;

                float3 finalColor = totalAnalyticalLight + analyticalSubsurfaceLight;

                return float4(finalColor, 1);
            }
            ENDCG
        }

        // Shadow Caster
        Pass
        {
            Name "Shadow"
            Tags
            {
                "LightMode"="ShadowCaster"
            }
            Cull [_Cull]
            ZWrite On

            CGPROGRAM
            // #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "UnityCG.cginc"

            struct shadowCasterAppdata {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 uv : TEXCOORD0;
            };

            struct shadowCasterV2f {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float     _Cutoff;
            float4    _Tiling;

            shadowCasterV2f shadowVert(shadowCasterAppdata v)
            {
                shadowCasterV2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(shadowCasterV2f, o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = v.uv * _Tiling.xy + _Tiling.zw;
                return o;
            }

            float4 shadowFrag(shadowCasterV2f i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                clip(color.a - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }

        // Meta Pass
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode"="Meta"
            }
            Cull Off
            CGPROGRAM
            #pragma vertex metaVert
            #pragma fragment metaFrag

            #include "KufiIncludes.cginc"
            #include"UnityMetaPass.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

            struct metaAppdata {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float4 color : COLOR;
                half3  normal : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                half4  tangent : TANGENT;
            };

            struct metaV2f {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _EmissionMap;
            sampler2D _SpecularMap;
            float4    _Tint;
            float4    _Tiling;

            float4 _EmmisionTint;

            metaV2f metaVert(metaAppdata v)
            {
                metaV2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityMetaVertexPosition(v.vertex, v.uv1.xy, v.uv2.xy, unity_LightmapST, unity_DynamicLightmapST);
                o.uv = float4(v.uv0 * _Tiling.xy + _Tiling.zw, 0, 0);
                o.color = v.color;
                return o;
            }

            float4 metaFrag(metaV2f i) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UnityMetaInput surfaceData;

                float3 emissionTint = _EmmisionTint;
                surfaceData.Emission = i.color * tex2D(_EmissionMap, i.uv.xy) * emissionTint;
                surfaceData.Albedo = i.color * tex2D(_MainTex, i.uv.xy) * _Tint;
                float3 specularColor = tex2D(_SpecularMap, i.uv.xy).rgb;
                surfaceData.SpecularColor = specularColor;
                return UnityMetaFragment(surfaceData);
            }
            ENDCG
        }
    }
}