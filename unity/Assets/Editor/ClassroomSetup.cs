using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class ClassroomSetup
{
    [MenuItem("Classroom/Create Demo Scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Shared Room Client").AddComponent<SharedRoomClient>();
        EditorSceneManager.SaveScene(scene,"Assets/Classroom.unity");
        EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene("Assets/Classroom.unity",true)};
        Selection.activeGameObject=GameObject.Find("Shared Room Client");
    }
}
