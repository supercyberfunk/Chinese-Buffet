using System.Collections.Generic;
using System.IO;
using BuffetSim.Bootstrap;
using BuffetSim.Economy;
using BuffetSim.Food;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuffetSim.EditorTools
{
    /// <summary>
    /// Editor conveniences: regenerate the demo scene, and turn the runtime default configs into
    /// real ScriptableObject assets designers can tune in the Inspector.
    /// </summary>
    public static class BuffetSimMenu
    {
        private const string ScenePath = "Assets/Scenes/BuffetDemo.unity";
        private const string DataFolder = "Assets/Data";
        private const string FoodsFolder = DataFolder + "/Foods";

        [MenuItem("Buffet Sim/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var builder = new GameObject("Demo Scene Builder").AddComponent<DemoSceneBuilder>();
            AssignExistingAssets(builder);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Debug.Log($"[Buffet] Demo scene written to {ScenePath}. Press Play.");
        }

        [MenuItem("Buffet Sim/Create Default Config Assets")]
        public static void CreateDefaultConfigAssets()
        {
            Directory.CreateDirectory(FoodsFolder);
            AssetDatabase.Refresh();

            EconomyConfig economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(DataFolder + "/EconomyConfig.asset");
            if (economy == null)
            {
                economy = EconomyConfig.CreateDefault();
                economy.name = "EconomyConfig";
                AssetDatabase.CreateAsset(economy, DataFolder + "/EconomyConfig.asset");
            }

            FoodCatalog catalog = AssetDatabase.LoadAssetAtPath<FoodCatalog>(DataFolder + "/FoodCatalog.asset");
            if (catalog == null)
            {
                FoodCatalog defaults = FoodCatalog.CreateDefault();
                var foods = new List<FoodDefinition>();
                foreach (FoodDefinition food in defaults.Unlocked)
                {
                    string path = $"{FoodsFolder}/{Sanitize(food.DisplayName)}.asset";
                    FoodDefinition existing = AssetDatabase.LoadAssetAtPath<FoodDefinition>(path);
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(food, path);
                        existing = food;
                    }
                    foods.Add(existing);
                }

                catalog = ScriptableObject.CreateInstance<FoodCatalog>();
                catalog.name = "FoodCatalog";
                catalog.SetFoods(foods);
                AssetDatabase.CreateAsset(catalog, DataFolder + "/FoodCatalog.asset");
            }

            Material baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(DataFolder + "/BuffetBase.mat");
            if (baseMaterial == null)
            {
                baseMaterial = new Material(MaterialLibrary.Get(Color.white)) { name = "BuffetBase" };
                AssetDatabase.CreateAsset(baseMaterial, DataFolder + "/BuffetBase.mat");
            }

            AssetDatabase.SaveAssets();

            var builder = Object.FindFirstObjectByType<DemoSceneBuilder>();
            if (builder != null)
            {
                var serialized = new SerializedObject(builder);
                serialized.FindProperty("economyConfig").objectReferenceValue = economy;
                serialized.FindProperty("foodCatalog").objectReferenceValue = catalog;
                serialized.FindProperty("baseMaterial").objectReferenceValue = baseMaterial;
                serialized.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
            }

            Debug.Log($"[Buffet] Config assets ready in {DataFolder}. Tune them in the Inspector.");
        }

        private static void AssignExistingAssets(DemoSceneBuilder builder)
        {
            var serialized = new SerializedObject(builder);
            serialized.FindProperty("economyConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EconomyConfig>(DataFolder + "/EconomyConfig.asset");
            serialized.FindProperty("foodCatalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FoodCatalog>(DataFolder + "/FoodCatalog.asset");
            serialized.FindProperty("baseMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(DataFolder + "/BuffetBase.mat");
            serialized.ApplyModifiedProperties();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
