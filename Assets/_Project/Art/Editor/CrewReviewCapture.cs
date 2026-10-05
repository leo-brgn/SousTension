using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SousTension.EditorTools
{
    public static class CrewReviewCapture
    {
        public static void Run()
        {
            AssetDatabase.Refresh();
            const string crewMaterialPath = "Assets/_Project/Art/Materials/CrewSurface.mat";
            var crewShader = Shader.Find("SousTension/CrewVertexLit");
            if (crewShader == null || ShaderUtil.ShaderHasError(crewShader)) throw new InvalidOperationException("Crew shader missing or invalid");
            if (!File.Exists(crewMaterialPath)) AssetDatabase.CreateAsset(new Material(crewShader), crewMaterialPath);
            foreach (var name in new[] { "SailorBase", "FirstPersonArms" })
                AssetDatabase.ImportAsset("Assets/_Project/Art/Models/Crew/" + name + ".fbx", ImportAssetOptions.ForceUpdate);
            CrewAssetValidation.Run();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Prefabs/Crew/SailorBase.prefab");
            var sailor = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (sailor.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh == null)
                throw new InvalidOperationException("Sailor prefab mesh binding missing");
            foreach (var animator in sailor.GetComponentsInChildren<Animator>()) animator.enabled = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.45f, .48f, .52f);
            RenderSettings.ambientEquatorColor = new Color(.3f, .3f, .28f);
            RenderSettings.ambientGroundColor = new Color(.18f, .17f, .15f);
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.18f,.17f,.15f));
            RenderSettings.ambientProbe = ambient;
            var sun = new GameObject("ReviewKey").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f; sun.transform.rotation = Quaternion.Euler(35,155,0);
            sun.shadows = LightShadows.Soft;
            sun.GetUniversalAdditionalLightData().usePipelineSettings = false;
            sun.shadowBias = .1f; sun.shadowNormalBias = .05f;
            var fill = new GameObject("ReviewFill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .6f; fill.transform.rotation = Quaternion.Euler(25,215,0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "ReviewFloor";
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = new Color(.66f,.65f,.59f);
            const string materialPath = "Assets/_Project/Art/Materials/CrewReviewFloor.mat";
            if (!File.Exists(materialPath)) AssetDatabase.CreateAsset(mat, materialPath);
            else { UnityEngine.Object.DestroyImmediate(mat); mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath); }
            floor.GetComponent<Renderer>().sharedMaterial = mat;
            var camera = new GameObject("MainCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 1.03f; camera.nearClipPlane = .01f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.66f,.69f,.71f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camera.farClipPlane = 10;
            Directory.CreateDirectory("scratch_out/preview/CrewRework");
            Capture(camera, new Vector3(0,.91f,.0f), new Vector3(0,.025f, -1), "Unity_front");
            Capture(camera, new Vector3(0,.91f,.0f), new Vector3(-.36f,.08f, -1), "Unity_three_quarter");
            EditorSceneManager.SaveScene(scene,"Assets/_Project/Art/Scenes/CrewReview.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("CREW_REVIEW_UNITY_PASS: prefab bindings, avatar, bones, clips and captures verified");
        }
        private static void Capture(Camera camera, Vector3 target, Vector3 direction, string name)
        {
            // Blender -Y front exports to Unity +Z.
            direction.z = -direction.z;
            camera.transform.position = target + direction.normalized * 7;
            camera.transform.LookAt(target); camera.aspect = 1200f/1100;
            var rt = new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32); rt.Create();
            var previous = RenderTexture.active;
            var request = new UniversalRenderPipeline.SingleCameraRequest {destination=rt};
            RenderPipeline.SubmitRenderRequest(camera,request); RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active = rt;
            var image = new Texture2D(1200,1100,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1200,1100),0,0);image.Apply();
            File.WriteAllBytes("scratch_out/preview/CrewRework/"+name+".png",image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
