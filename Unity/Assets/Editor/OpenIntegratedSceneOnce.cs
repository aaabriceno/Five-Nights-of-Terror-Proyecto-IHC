using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Opens the integrated gameplay scene once when the project was left on the empty template scene.</summary>
[InitializeOnLoad]
internal static class OpenIntegratedSceneOnce
{
    private const string SessionKey = "IHC.OpenedIntegratedSceneThisSession";
    private const string EmptyTemplateScene = "Assets/Scenes/SampleScene.unity";
    private const string GameplayScene = "Assets/MediaPipeUnity/Samples/Scenes/Face Landmark Detection/Face Landmark Detection.unity";

    static OpenIntegratedSceneOnce()
    {
        EditorApplication.delayCall += OpenIfTemplateSceneIsActive;
    }

    private static void OpenIfTemplateSceneIsActive()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        SessionState.SetBool(SessionKey, true);
        Scene activeScene = SceneManager.GetActiveScene();
        bool emptyUntitledScene = string.IsNullOrEmpty(activeScene.path) && activeScene.rootCount <= 2;
        bool emptyTemplateScene = activeScene.path == EmptyTemplateScene;
        if ((!emptyUntitledScene && !emptyTemplateScene) || activeScene.isDirty)
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScene) != null)
            EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
    }
}
