using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SousTension.EditorTools
{
    public static class MvpAssetBuild
    {
        [MenuItem("Sous Tension/Art/Build and validate MVP assets")]
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            PrimaryAssetValidation.Run();
            MachinesAssetValidation.Run();
            CentralAssetValidation.Run();
            CentralAssetValidation.BuildPrefabs();
            ToolsDamageAssetValidation.Run();
            CrewAssetValidation.Run();
            MvpPresentationBuilder.Build();
            DetailAssetValidation.Run();
            BoatEnvironmentBuilder.Build();
            Directory.CreateDirectory("scratch_out");
            File.WriteAllText("scratch_out/mvp-assets-validation.txt", "MVP asset pipeline passed: static props, Central, Tools/Damage, rigs/animations, effect and lighting prefabs, six-compartment scene. Paper/art templates remain content placeholders; scene is an art preview, no gameplay/network binding.");
            Debug.Log("MVP_ASSET_BUILD_PASS");
        }
    }
}
