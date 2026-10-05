using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SousTension.EditorTools
{
    public static class V1AssetCapture
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory("scratch_out/preview/V1");
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.30f,.32f,.34f);
            var light=new GameObject("PreviewKey").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(42,-35,0);
            var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.orthographic=true;camera.aspect=1700f/1100;camera.farClipPlane=1500;camera.nearClipPlane=.01f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.17f,.20f,.22f);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            foreach(var path in new[] { "World/MolossExterior_Wear0", "World/FjordGantryCrane", "World/WreckCargo", "World/UnderwaterKelpCluster",
                "Fixtures/PeriscopeInterior", "Fixtures/MarineToiletSevenValve", "Cargo/SignalEmitterAssembled", "Cargo/NuclearFuelTransportCask",
                "CrewVariants/CommanderVarga", "CrewVariants/DivingSuit", "Torpedoes/CargoTorpedoTube" })
            {
                var ob=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Prefabs/"+path+".prefab"));
                try
                {
                    var lod=ob.GetComponent<LODGroup>();if(lod!=null)lod.ForceLOD(0);
                    var renderers=ob.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;
                    foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                    camera.transform.position=bounds.center+new Vector3(.8f,.6f,1f).normalized*Mathf.Max(bounds.size.magnitude*2,3);
                    camera.transform.LookAt(bounds.center);
                    float maxX=0,maxY=0;
                    foreach(var x in new[]{-1,1})foreach(var y in new[]{-1,1})foreach(var z in new[]{-1,1})
                    {
                        var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))-bounds.center;
                        maxX=Mathf.Max(maxX,Mathf.Abs(Vector3.Dot(p,camera.transform.right)));
                        maxY=Mathf.Max(maxY,Mathf.Abs(Vector3.Dot(p,camera.transform.up)));
                    }
                    camera.orthographicSize=Mathf.Max(maxY,maxX/camera.aspect)*1.1f;
                    Capture(camera,Path.GetFileName(path));
                }
                finally{UnityEngine.Object.DestroyImmediate(ob);}
            }
            Debug.Log("V1_CAPTURE_PASS: eleven native Unity asset renders");
        }
        private static void Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1700,1100,24);target.Create();
            var previous=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;var image=new Texture2D(1700,1100,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1700,1100),0,0);image.Apply();
                File.WriteAllBytes("scratch_out/preview/V1/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
