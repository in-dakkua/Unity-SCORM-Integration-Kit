using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Scorm.Scenarios;
using Scorm.Scenarios.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The generated demo scene (ScenarioDemoSceneBuilder) is first in Build Settings, keeps TestApp, and every serialized
/// reference of the demo components is wired. Run "SCORM/Demo/Build Scenario Demo Scene" if these fail.
/// </summary>
public class ScenarioDemoSceneTests
{
    [Test]
    public void BuildSettings_ScenarioDemoFirstAndTestAppKept()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        Assert.GreaterOrEqual(scenes.Length, 2);
        Assert.AreEqual(ScenarioDemoSceneBuilder.ScenePath, scenes[0].path);
        Assert.IsTrue(scenes[0].enabled);
        Assert.IsTrue(System.Array.Exists(scenes, s => s.path == ScenarioDemoSceneBuilder.TestAppScenePath));
    }

    [Test]
    public void ExampleCatalog_IsValidWithStableIds()
    {
        ScenarioCatalog catalog = AssetDatabase.LoadAssetAtPath<ScenarioCatalog>(ScenarioDemoSceneBuilder.CatalogPath);
        Assert.IsNotNull(catalog);
        CollectionAssert.IsEmpty(catalog.Validate());
        Assert.AreEqual(5, catalog.Scenarios.Count);
        for (int i = 0; i < catalog.Scenarios.Count; i++)
            Assert.AreEqual("scenario-0" + (i + 1), catalog.Scenarios[i].Id);
    }

    [Test]
    public void Scene_HostUnderScormManagerAndAllReferencesWired()
    {
        Assert.IsTrue(File.Exists(ScenarioDemoSceneBuilder.ScenePath));
        Scene scene = EditorSceneManager.OpenScene(ScenarioDemoSceneBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
            foreach (GameObject root in scene.GetRootGameObjects())
                behaviours.AddRange(root.GetComponentsInChildren<MonoBehaviour>(true));

            ScenarioTrackerHost host = behaviours.Find(b => b is ScenarioTrackerHost) as ScenarioTrackerHost;
            Assert.IsNotNull(host);
            Assert.IsNotNull(host.GetComponent<ScormManager>(), "the host must receive Scorm_Initialize_Complete");
            Assert.IsNotNull(host.Catalog);
            Assert.IsNotNull(behaviours.Find(b => b is ScenarioDemoController));
            Assert.IsNotNull(behaviours.Find(b => b is ScormLogPanel));
            ScenarioDemoController controller = (ScenarioDemoController)behaviours.Find(b => b is ScenarioDemoController);
            Assert.IsTrue(controller.transform.IsChildOf(host.transform), "UI under the ScormManager (BroadcastMessage)");

            int checkedFields = 0;
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour.GetType().Namespace == null || !behaviour.GetType().Namespace.StartsWith("Scorm.Scenarios"))
                    continue;
                SerializedProperty property = new SerializedObject(behaviour).GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script")
                        continue;
                    Assert.IsNotNull(property.objectReferenceValue, behaviour.GetType().Name + "." + property.name + " on " + behaviour.name + " is not wired");
                    checkedFields++;
                }
            }
            Assert.Greater(checkedFields, 30);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [UnityTest]
    public IEnumerator PlayMode_SceneStartsTrackerAndRandomPlayCompletesAScenario()
    {
        EditorSceneManager.OpenScene(ScenarioDemoSceneBuilder.ScenePath, OpenSceneMode.Single);
        ScormEditorBackend.Reset();
        yield return new EnterPlayMode();
        for (int i = 0; i < 5; i++)
            yield return null;

        ScenarioTrackerHost host = Object.FindAnyObjectByType<ScenarioTrackerHost>();
        Assert.IsNotNull(host);
        Assert.IsNotNull(host.Tracker, "Scorm_Initialize_Complete did not reach the host: " + host.InitializationError);
        ScenarioRowView[] rows = Object.FindObjectsByType<ScenarioRowView>(FindObjectsSortMode.None);
        Assert.AreEqual(5, rows.Length, "one active row per scenario");
        Text counter = GameObject.Find("CounterText").GetComponent<Text>();
        StringAssert.Contains("Llamadas:", counter.text);
        Assert.AreNotEqual("Llamadas: 0", counter.text, "the log panel received the Initialize/GetValue calls");

        Button play = GameObject.Find("Row scenario-02").GetComponentsInChildren<Button>().First(b => b.name == "PlayButton");
        play.onClick.Invoke();
        yield return null;

        ScenarioProgress scenario = host.Tracker.Get("scenario-02");
        Assert.AreEqual(1, scenario.Attempts);
        Assert.IsTrue(host.Tracker.LastOperationSucceeded);
        Assert.GreaterOrEqual(ScormManager.FindObjectiveIndex(scenario.Id), 0);

        GameObject.Find("SuspendButton").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.IsTrue(host.Tracker.IsClosed);
        Assert.AreEqual("suspend", ScormEditorBackend.Data["cmi.exit"]);

        GameObject.Find("RelaunchButton").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.IsFalse(host.Tracker.IsClosed, "relaunch creates a new tracker");
        Assert.AreEqual(1, host.Tracker.Get("scenario-02").Attempts, "state resumed from suspend_data");

        yield return new ExitPlayMode();
    }

    // Leaves no ScormManager loaded in Edit Mode: ScormManager.Initialize in later tests would BroadcastMessage to it.
    [UnityTearDown]
    public IEnumerator CloseDemoScene()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}
