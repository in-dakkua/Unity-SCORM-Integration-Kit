using System;
using System.Collections.Generic;
using System.IO;
using Scorm.Scenarios;
using Scorm.Scenarios.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/SCORM/Demo/ScenarioDemo.unity (and the example scenario assets if missing) reproducibly, and puts
/// the scene first in the Build Settings.
///
///   Menu:      SCORM/Demo/Build Scenario Demo Scene
///   Batchmode: Unity -batchmode -quit -projectPath . -executeMethod ScenarioDemoSceneBuilder.BuildFromCommandLine
/// </summary>
public static class ScenarioDemoSceneBuilder
{
    public const string DemoRoot = "Assets/SCORM/Demo";
    public const string ScenePath = DemoRoot + "/ScenarioDemo.unity";
    public const string DataDir = DemoRoot + "/Data";
    public const string CatalogPath = DataDir + "/ScenarioCatalog.asset";
    public const string ManagerPrefabPath = "Assets/SCORM/Resources/ScormManager.prefab";
    public const string TestAppScenePath = "Assets/SCORM/_Scenes/TestApp.unity";

    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    private static readonly Color BackgroundColor = new Color(0.07f, 0.08f, 0.11f, 1f);
    private static readonly Color PanelColor = new Color(0.12f, 0.14f, 0.18f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.94f, 0.97f, 1f);
    private static readonly Color ButtonColor = new Color(0.86f, 0.89f, 0.95f, 1f);
    private static readonly Color ButtonTextColor = new Color(0.08f, 0.10f, 0.14f, 1f);

    private static Font _font;
    private static DefaultControls.Resources _resources;

    [MenuItem("SCORM/Demo/Build Scenario Demo Scene")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Build();
        EditorUtility.DisplayDialog("Scenario demo", "Scene generated: " + ScenePath + "\nIt is now the first scene in Build Settings.", "OK");
    }

    /// <summary>-executeMethod entry point: exits with 0 on success, 1 on failure.</summary>
    public static void BuildFromCommandLine()
    {
        bool ok = false;
        try
        {
            Build();
            ok = true;
        }
        catch (Exception e)
        {
            Debug.LogError("[ScenarioDemo] Scene build failed: " + e);
        }
        Debug.Log(ok ? "[ScenarioDemo] SCENARIO_DEMO_SCENE_OK " + ScenePath : "[ScenarioDemo] SCENARIO_DEMO_SCENE_FAILED");
        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    public static void Build()
    {
        GameObject managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
        if (managerPrefab == null)
            throw new FileNotFoundException("ScormManager prefab not found", ManagerPrefabPath);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Loaded after NewScene: a Single-mode NewScene unloads unused assets, which would leave a destroyed reference.
        ScenarioCatalog catalog = EnsureExampleData();
        managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            throw new InvalidOperationException("Built-in font LegacyRuntime.ttf not found.");
        _resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };

        CreateCamera();
        CreateEventSystem();

        GameObject manager = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab, scene);
        manager.name = "ScormManager";
        ScenarioTrackerHost host = manager.AddComponent<ScenarioTrackerHost>();
        Wire(host, "_catalog", catalog);

        BuildCanvas(manager.transform, host);

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Could not save " + ScenePath);
        int scenarioCount = catalog.Scenarios.Count;
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        VerifySavedScene();
        Debug.Log("[ScenarioDemo] Scene saved: " + ScenePath + " (catalog " + CatalogPath + ", " + scenarioCount + " scenarios)");
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Data
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>Creates the example scenarios and catalog if they do not exist (existing assets are never overwritten).</summary>
    public static ScenarioCatalog EnsureExampleData()
    {
        if (!AssetDatabase.IsValidFolder(DataDir))
            AssetDatabase.CreateFolder(DemoRoot, "Data");

        ScenarioCatalog catalog = AssetDatabase.LoadAssetAtPath<ScenarioCatalog>(CatalogPath);
        if (catalog != null)
            return catalog;

        List<ScenarioDefinition> scenarios = new List<ScenarioDefinition>
        {
            Scenario("Scenario01_EPI", "scenario-01", "EPI y acceso a planta",
                "Comprobar los equipos de protección individual antes de entrar en la nave de laminación.", 1f, 0f, 100f, 0.7f, 4),
            Scenario("Scenario02_LOTO", "scenario-02", "Bloqueo y etiquetado (LOTO)",
                "Consignar una máquina antes de una intervención de mantenimiento. Nota sobre 10.", 2f, 0f, 10f, 0.8f, 5),
            Scenario("Scenario03_Carretillas", "scenario-03", "Circulación de carretillas",
                "Moverse a pie por zonas con tráfico de carretillas y puentes grúa.", 1.5f, 0f, 100f, 0.7f, 4),
            Scenario("Scenario04_Incendio", "scenario-04", "Conato de incendio",
                "Actuar ante un conato de incendio en un cuadro eléctrico. Nota sobre 50.", 2f, 0f, 50f, 0.6f, 6),
            Scenario("Scenario05_EspaciosConfinados", "scenario-05", "Espacios confinados",
                "Preparar la entrada a un foso: permiso de trabajo, medición de gases y vigilancia.", 2.5f, 0f, 100f, 0.75f, 5)
        };

        catalog = ScriptableObject.CreateInstance<ScenarioCatalog>();
        catalog.Configure(scenarios, ScenarioScorePolicy.Best, GlobalScoreMethod.WeightedAverageAllScenarios, 0.7f, false);
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    private static ScenarioDefinition Scenario(string assetName, string id, string title, string description, float weight,
        float min, float max, float passing, int decisions)
    {
        string path = DataDir + "/" + assetName + ".asset";
        ScenarioDefinition existing = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);
        if (existing != null)
            return existing;
        ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
        scenario.Configure(id, title, description, weight, min, max, passing, decisions);
        AssetDatabase.CreateAsset(scenario, path);
        return scenario;
    }

    // Reopens the saved scene and checks the references survived serialization.
    private static void VerifySavedScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int missing = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Namespace == null || !behaviour.GetType().Namespace.StartsWith("Scorm.Scenarios", StringComparison.Ordinal))
                    continue;
                SerializedProperty property = new SerializedObject(behaviour).GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.objectReferenceValue == null)
                    {
                        Debug.LogError("[ScenarioDemo] Not wired after save: " + behaviour.GetType().Name + "." + property.name + " on " + behaviour.name);
                        missing++;
                    }
                }
            }
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.font == null)
                {
                    Debug.LogError("[ScenarioDemo] Text without font after save: " + text.name);
                    missing++;
                }
            }
        }
        if (missing > 0)
            throw new InvalidOperationException(missing + " references are missing in the saved scene.");
    }

    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        bool hasTestApp = false;
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (existing.path == ScenePath)
                continue;
            if (existing.path == TestAppScenePath)
                hasTestApp = true;
            scenes.Add(existing);
        }
        if (!hasTestApp && File.Exists(TestAppScenePath))
            scenes.Add(new EditorBuildSettingsScene(TestAppScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Scene objects
    // ----------------------------------------------------------------------------------------------------------------

    private static void CreateCamera()
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundColor;
        camera.orthographic = true;
        go.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static void CreateEventSystem()
    {
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static void BuildCanvas(Transform parent, ScenarioTrackerHost host)
    {
        GameObject canvasGo = new GameObject("ScenarioDemoCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(parent, false);
        canvasGo.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        Transform root = canvasGo.transform;

        Stretch(Panel("Background", root, BackgroundColor).rectTransform, 0f, 0f, 1f, 1f);

        Text header = Label("Header", root, "Demo Escenarios SCORM 2004  ·  un SCO, un objetivo (cmi.objectives) por escenario, nota global ponderada en cmi.score", 28, FontStyle.Bold, TextAnchor.MiddleLeft);
        Stretch(header.rectTransform, 0.01f, 0.94f, 0.99f, 0.995f);

        // Scenario list
        Image listPanel = Panel("ScenarioListPanel", root, PanelColor);
        Stretch(listPanel.rectTransform, 0.008f, 0.36f, 0.6f, 0.935f);
        RectTransform listContent;
        ScrollRect listScroll = ScrollView("ScenarioScroll", listPanel.transform, out listContent);
        Stretch((RectTransform)listScroll.transform, 0f, 0f, 1f, 1f, 6f);
        ConfigureVerticalContent(listContent, 8f, 6);
        ScenarioRowView rowTemplate = BuildRowTemplate(listContent);
        ScenarioListPanel list = listPanel.gameObject.AddComponent<ScenarioListPanel>();
        Wire(list, "_rowTemplate", rowTemplate);
        Wire(list, "_content", listContent);

        // Summary
        Image summaryPanel = Panel("SummaryPanel", root, PanelColor);
        Stretch(summaryPanel.rectTransform, 0.608f, 0.62f, 0.992f, 0.935f);
        VerticalLayoutGroup summaryLayout = summaryPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        summaryLayout.padding = new RectOffset(16, 16, 12, 12);
        summaryLayout.spacing = 6f;
        summaryLayout.childControlWidth = true;
        summaryLayout.childControlHeight = true;
        summaryLayout.childForceExpandWidth = true;
        summaryLayout.childForceExpandHeight = false;
        Text learner = Label("LearnerText", summaryPanel.transform, "Alumno: —", 22, FontStyle.Normal, TextAnchor.UpperLeft);
        Text global = Label("GlobalText", summaryPanel.transform, "", 22, FontStyle.Normal, TextAnchor.UpperLeft);
        Text status = Label("StatusText", summaryPanel.transform, "", 20, FontStyle.Normal, TextAnchor.UpperLeft);
        Text session = Label("SessionText", summaryPanel.transform, "", 18, FontStyle.Normal, TextAnchor.UpperLeft);
        SummaryPanel summary = summaryPanel.gameObject.AddComponent<SummaryPanel>();
        Wire(summary, "_learnerText", learner);
        Wire(summary, "_globalText", global);
        Wire(summary, "_statusText", status);
        Wire(summary, "_sessionText", session);

        // Actions
        Image actionsPanel = Panel("ActionsPanel", root, PanelColor);
        Stretch(actionsPanel.rectTransform, 0.608f, 0.36f, 0.992f, 0.612f);
        VerticalLayoutGroup actionsLayout = actionsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        actionsLayout.padding = new RectOffset(16, 16, 12, 12);
        actionsLayout.spacing = 8f;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = true;
        actionsLayout.childForceExpandHeight = false;

        Transform row1 = HorizontalRow("Row1", actionsPanel.transform, 52f);
        Button commit = ButtonControl("CommitButton", row1, "Commit");
        Button suspend = ButtonControl("SuspendButton", row1, "Salir (suspend)");
        Button finish = ButtonControl("FinishButton", row1, "Finalizar (normal)");
        Transform row2 = HorizontalRow("Row2", actionsPanel.transform, 52f);
        Button reset = ButtonControl("ResetButton", row2, "Reiniciar datos demo");
        Button relaunch = ButtonControl("RelaunchButton", row2, "Relanzar sesión (Editor)");
        Transform row3 = HorizontalRow("Row3", actionsPanel.transform, 46f);
        Text seedLabel = Label("SeedLabel", row3, "Semilla aleatoria:", 20, FontStyle.Normal, TextAnchor.MiddleLeft);
        seedLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 190f;
        InputField seed = InputFieldControl("SeedInput", row3, "número entero");
        Button applySeed = ButtonControl("ApplySeedButton", row3, "Aplicar semilla");
        Text message = Label("MessageText", actionsPanel.transform, "", 19, FontStyle.Italic, TextAnchor.UpperLeft);
        LayoutElement messageLayout = message.gameObject.AddComponent<LayoutElement>();
        messageLayout.flexibleHeight = 1f;
        messageLayout.minHeight = 50f;

        // Log
        Image logPanel = Panel("ScormLogPanel", root, PanelColor);
        Stretch(logPanel.rectTransform, 0.008f, 0.008f, 0.992f, 0.352f);
        GameObject toolbar = new GameObject("Toolbar", typeof(RectTransform));
        toolbar.transform.SetParent(logPanel.transform, false);
        RectTransform toolbarRect = (RectTransform)toolbar.transform;
        toolbarRect.anchorMin = new Vector2(0f, 1f);
        toolbarRect.anchorMax = new Vector2(1f, 1f);
        toolbarRect.pivot = new Vector2(0.5f, 1f);
        toolbarRect.offsetMin = new Vector2(10f, -50f);
        toolbarRect.offsetMax = new Vector2(-10f, -6f);
        HorizontalLayoutGroup toolbarLayout = toolbar.AddComponent<HorizontalLayoutGroup>();
        toolbarLayout.spacing = 16f;
        toolbarLayout.childControlWidth = true;
        toolbarLayout.childControlHeight = true;
        toolbarLayout.childForceExpandWidth = false;
        toolbarLayout.childForceExpandHeight = true;
        toolbarLayout.childAlignment = TextAnchor.MiddleLeft;
        Text logTitle = Label("LogTitle", toolbar.transform, "Log de llamadas SCORM", 22, FontStyle.Bold, TextAnchor.MiddleLeft);
        logTitle.gameObject.AddComponent<LayoutElement>().preferredWidth = 280f;
        Toggle errorsOnly = ToggleControl("ErrorsOnlyToggle", toolbar.transform, "Solo errores");
        Toggle setsOnly = ToggleControl("SetsOnlyToggle", toolbar.transform, "Solo SetValue");
        Button clear = ButtonControl("ClearButton", toolbar.transform, "Limpiar");
        clear.GetComponent<LayoutElement>().preferredWidth = 140f;
        clear.GetComponent<LayoutElement>().flexibleWidth = 0f;
        Text counter = Label("CounterText", toolbar.transform, "Llamadas: 0", 20, FontStyle.Normal, TextAnchor.MiddleLeft);
        counter.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        RectTransform logContent;
        ScrollRect logScroll = ScrollView("LogScroll", logPanel.transform, out logContent);
        RectTransform logScrollRect = (RectTransform)logScroll.transform;
        logScrollRect.anchorMin = Vector2.zero;
        logScrollRect.anchorMax = Vector2.one;
        logScrollRect.offsetMin = new Vector2(8f, 8f);
        logScrollRect.offsetMax = new Vector2(-8f, -56f);
        ConfigureVerticalContent(logContent, 0f, 4);
        Text lineTemplate = Label("LineTemplate", logContent, "", 16, FontStyle.Normal, TextAnchor.UpperLeft);
        lineTemplate.gameObject.SetActive(false);
        ScormLogPanel log = logPanel.gameObject.AddComponent<ScormLogPanel>();
        Wire(log, "_scrollRect", logScroll);
        Wire(log, "_content", logContent);
        Wire(log, "_lineTemplate", lineTemplate);
        Wire(log, "_errorsOnlyToggle", errorsOnly);
        Wire(log, "_setsOnlyToggle", setsOnly);
        Wire(log, "_clearButton", clear);
        Wire(log, "_counterText", counter);

        ScenarioDemoController controller = canvasGo.AddComponent<ScenarioDemoController>();
        Wire(controller, "_host", host);
        Wire(controller, "_scenarioList", list);
        Wire(controller, "_summary", summary);
        Wire(controller, "_messageText", message);
        Wire(controller, "_commitButton", commit);
        Wire(controller, "_suspendButton", suspend);
        Wire(controller, "_finishButton", finish);
        Wire(controller, "_resetButton", reset);
        Wire(controller, "_relaunchButton", relaunch);
        Wire(controller, "_seedInput", seed);
        Wire(controller, "_applySeedButton", applySeed);
    }

    private static ScenarioRowView BuildRowTemplate(RectTransform parent)
    {
        Image background = Panel("RowTemplate", parent, new Color(0.2f, 0.22f, 0.26f, 1f));
        LayoutElement layout = background.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 118f;
        layout.preferredHeight = 118f;
        Transform row = background.transform;

        Text title = Label("Title", row, "Escenario", 22, FontStyle.Bold, TextAnchor.MiddleLeft);
        Stretch(title.rectTransform, 0f, 0.64f, 0.62f, 1f, 6f);
        Text status = Label("Status", row, "", 18, FontStyle.Normal, TextAnchor.MiddleLeft);
        Stretch(status.rectTransform, 0f, 0.33f, 0.62f, 0.66f, 6f);
        Text scores = Label("Scores", row, "", 18, FontStyle.Normal, TextAnchor.MiddleLeft);
        Stretch(scores.rectTransform, 0f, 0f, 0.62f, 0.34f, 6f);

        Button play = ButtonControl("PlayButton", row, "Jugar (aleatorio)");
        UnityEngine.Object.DestroyImmediate(play.GetComponent<LayoutElement>());
        Stretch((RectTransform)play.transform, 0.63f, 0.53f, 0.99f, 0.95f);

        GameObject sliderGo = DefaultControls.CreateSlider(_resources);
        sliderGo.name = "ManualSlider";
        sliderGo.transform.SetParent(row, false);
        Stretch((RectTransform)sliderGo.transform, 0.635f, 0.12f, 0.80f, 0.42f);
        Slider slider = sliderGo.GetComponent<Slider>();

        Text value = Label("ManualValue", row, "0", 18, FontStyle.Normal, TextAnchor.MiddleCenter);
        Stretch(value.rectTransform, 0.80f, 0.08f, 0.855f, 0.46f);

        Button manual = ButtonControl("ManualButton", row, "Nota manual");
        UnityEngine.Object.DestroyImmediate(manual.GetComponent<LayoutElement>());
        Stretch((RectTransform)manual.transform, 0.86f, 0.06f, 0.99f, 0.47f);

        ScenarioRowView view = background.gameObject.AddComponent<ScenarioRowView>();
        Wire(view, "_background", background);
        Wire(view, "_titleText", title);
        Wire(view, "_statusText", status);
        Wire(view, "_scoresText", scores);
        Wire(view, "_playButton", play);
        Wire(view, "_manualSlider", slider);
        Wire(view, "_manualValueText", value);
        Wire(view, "_manualButton", manual);
        background.gameObject.SetActive(false);
        return view;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // UI helpers
    // ----------------------------------------------------------------------------------------------------------------

    private static Image Panel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = _resources.background;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    private static Text Label(string name, Transform parent, string text, int size, FontStyle style, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text label = go.AddComponent<Text>();
        label.font = _font;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = TextColor;
        label.supportRichText = true;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private static Button ButtonControl(string name, Transform parent, string text)
    {
        GameObject go = DefaultControls.CreateButton(_resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = ButtonColor;
        Text label = go.GetComponentInChildren<Text>(true);
        label.font = _font;
        label.fontSize = 20;
        label.color = ButtonTextColor;
        label.text = text;
        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minHeight = 40f;
        return go.GetComponent<Button>();
    }

    private static Toggle ToggleControl(string name, Transform parent, string text)
    {
        GameObject go = DefaultControls.CreateToggle(_resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        Toggle toggle = go.GetComponent<Toggle>();
        toggle.isOn = false;
        Text label = go.GetComponentInChildren<Text>(true);
        label.font = _font;
        label.fontSize = 20;
        label.color = TextColor;
        label.text = text;
        go.AddComponent<LayoutElement>().preferredWidth = 190f;
        return toggle;
    }

    private static InputField InputFieldControl(string name, Transform parent, string placeholder)
    {
        GameObject go = DefaultControls.CreateInputField(_resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        InputField input = go.GetComponent<InputField>();
        input.contentType = InputField.ContentType.IntegerNumber;
        foreach (Text text in go.GetComponentsInChildren<Text>(true))
        {
            text.font = _font;
            text.fontSize = 20;
        }
        ((Text)input.placeholder).text = placeholder;
        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        return input;
    }

    private static ScrollRect ScrollView(string name, Transform parent, out RectTransform content)
    {
        GameObject go = DefaultControls.CreateScrollView(_resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        ScrollRect scroll = go.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        if (scroll.horizontalScrollbar != null)
        {
            UnityEngine.Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
            scroll.horizontalScrollbar = null;
        }
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        content = scroll.content;
        return scroll;
    }

    private static void ConfigureVerticalContent(RectTransform content, float spacing, int padding)
    {
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, content.offsetMin.y);
        content.offsetMax = new Vector2(0f, 0f);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private static Transform HorizontalRow(string name, Transform parent, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        return go.transform;
    }

    private static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY, float padding = 0f)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static void Wire(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + " has no serialized field '" + propertyName + "'.");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
