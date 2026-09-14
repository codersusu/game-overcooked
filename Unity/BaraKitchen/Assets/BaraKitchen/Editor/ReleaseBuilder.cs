using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static partial class ArtProductionBuilder {
    static void ApplyReleaseBrand() {
        const string iconPath = Base + "/Brand/BaraKitchenIcon.png";
        AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath) }, IconKind.Any);
        PlayerSettings.companyName = "Bara Kitchen";
        PlayerSettings.productName = "Bara Kitchen";
        PlayerSettings.bundleVersion = "0.1.0";
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Bara Kitchen/Build macOS demo")]
    public static void BuildMacDemo() {
        ApplyReleaseBrand();
        PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1);
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        var destination = Path.Combine(Root, ".local/release/Bara Kitchen.app");
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { Base + "/Scenes/BaraKitchen_Game.unity" },
            locationPathName = destination,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("macOS build failed: " + report.summary.result);
        Debug.Log("BARA_MAC_BUILD_OK bytes=" + report.summary.totalSize);
    }
}
