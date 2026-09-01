using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StartMenuSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/StartMenu.unity";
    private const string IntroCutsceneScenePath = "Assets/Scenes/IntroCutscene.unity";
    private const string FirstScenePath = "Assets/Scenes/1.unity";
    private const string TypoBannerPath = "Assets/Assets/typoBanner.aseprite";
    private const string CutsceneOnePath = "Assets/Assets/Cutscene1-1.aseprite";
    private const string CutsceneTwoPath = "Assets/Assets/Cutscene1-2.aseprite";
    private const string CutsceneThreePath = "Assets/Assets/Cutscene2-1.aseprite";
    private const string CutsceneFourPath = "Assets/Assets/Cutscene3-1.aseprite";
    private const string CutsceneFivePath = "Assets/Assets/Cutscene3-2.aseprite";

    [InitializeOnLoadMethod]
    private static void QueueAutoBuild()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += () =>
        {
            BuildIfMissingScenes();
            RefreshAllCutsceneFramesIfNeeded();
        };
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += RefreshAllCutsceneFramesIfNeeded;
        }
    }

    [DidReloadScripts]
    private static void BuildAfterScriptsReload()
    {
        BuildIfMissingScenes();
        EditorApplication.delayCall += RefreshAllCutsceneFramesIfNeeded;
    }

    [MenuItem("Tools/Oops It Ate/Build Start Menu")]
    public static void BuildStartMenu()
    {
        BuildMenuScene();
        BuildIntroCutscene();
        AddScenesToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Oops It Ate/Refresh All Cutscene Frames")]
    public static void RefreshAllCutsceneFramesIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Sprite[] cutsceneOneFrames = LoadSpritesInFrameOrder(CutsceneOnePath);
        Sprite[] cutsceneTwoFrames = LoadSpritesInFrameOrder(CutsceneTwoPath);
        Sprite[] cutsceneThreeFrames = LoadSpritesInFrameOrder(CutsceneThreePath);
        Sprite[] cutsceneFourFrames = LoadSpritesInFrameOrder(CutsceneFourPath);
        Sprite[] cutsceneFiveFrames = LoadSpritesInFrameOrder(CutsceneFivePath);
        if (cutsceneOneFrames.Length == 0 || cutsceneTwoFrames.Length == 0 || cutsceneThreeFrames.Length == 0 ||
            cutsceneFourFrames.Length == 0 || cutsceneFiveFrames.Length == 0)
        {
            Debug.LogWarning("Could not refresh cutscene frames because at least one Aseprite asset has no imported sprites.");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(IntroCutsceneScenePath);
        bool openedForAssignment = !scene.IsValid() || !scene.isLoaded;
        if (openedForAssignment)
        {
            scene = EditorSceneManager.OpenScene(IntroCutsceneScenePath, OpenSceneMode.Additive);
        }

        IntroCutsceneController controller = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<IntroCutsceneController>(true))
            .FirstOrDefault();

        if (controller != null)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            bool changed = SetSpriteArrayIfDifferent(
                serializedController.FindProperty("cutsceneOneFrames"), cutsceneOneFrames);
            changed |= SetSpriteArrayIfDifferent(
                serializedController.FindProperty("cutsceneTwoFrames"), cutsceneTwoFrames);
            changed |= SetSpriteArrayIfDifferent(
                serializedController.FindProperty("cutsceneThreeFrames"), cutsceneThreeFrames);
            changed |= SetSpriteArrayIfDifferent(
                serializedController.FindProperty("cutsceneFourFrames"), cutsceneFourFrames);
            changed |= SetSpriteArrayIfDifferent(
                serializedController.FindProperty("cutsceneFiveFrames"), cutsceneFiveFrames);

            if (changed)
            {
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Refreshed IntroCutscene frames: {cutsceneOneFrames.Length}, {cutsceneTwoFrames.Length}, {cutsceneThreeFrames.Length}, {cutsceneFourFrames.Length}, {cutsceneFiveFrames.Length}.");
            }
        }

        if (openedForAssignment)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void BuildIfMissingScenes()
    {
        bool needsMenu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null;
        bool needsCutscene = AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroCutsceneScenePath) == null;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && (needsMenu || needsCutscene))
        {
            BuildStartMenu();
        }
    }

    private static void BuildMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Camera camera = CreateCamera(new Color(0.08f, 0.06f, 0.05f, 1f));
        Canvas canvas = CreateCanvas(camera);
        CreateEventSystem();

        Sprite[] bannerFrames = LoadSpritesInFrameOrder(TypoBannerPath);

        GameObject controllerObject = new GameObject("Start Menu Controller");
        StartMenuController controller = controllerObject.AddComponent<StartMenuController>();

        Image background = CreatePanel(canvas.transform, "Background", new Color(0.08f, 0.06f, 0.05f, 1f));
        Stretch(background.rectTransform);

        Image banner = CreateImage(canvas.transform, "Typo Banner", bannerFrames.FirstOrDefault());
        banner.preserveAspect = true;
        RectTransform bannerRect = banner.rectTransform;
        bannerRect.anchorMin = new Vector2(0.5f, 0.5f);
        bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRect.pivot = new Vector2(0.5f, 0.5f);
        bannerRect.anchoredPosition = new Vector2(0f, 90f);
        bannerRect.sizeDelta = new Vector2(768f, 432f);

        Button startButton = CreateStartButton(canvas.transform);
        UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartGame);

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("bannerImage").objectReferenceValue = banner;
        SetSpriteArray(serializedController.FindProperty("bannerFrames"), bannerFrames);
        serializedController.FindProperty("bannerTransform").objectReferenceValue = bannerRect;
        serializedController.FindProperty("startButtonTransform").objectReferenceValue = startButton.transform;
        serializedController.FindProperty("firstSceneName").stringValue = "IntroCutscene";
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    private static void BuildIntroCutscene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Camera camera = CreateCamera(Color.black);
        Canvas canvas = CreateCanvas(camera);

        Image background = CreatePanel(canvas.transform, "Background", Color.black);
        Stretch(background.rectTransform);

        Sprite[] cutsceneOneFrames = LoadSpritesInFrameOrder(CutsceneOnePath);
        Sprite[] cutsceneTwoFrames = LoadSpritesInFrameOrder(CutsceneTwoPath);
        Sprite[] cutsceneThreeFrames = LoadSpritesInFrameOrder(CutsceneThreePath);
        Sprite[] cutsceneFourFrames = LoadSpritesInFrameOrder(CutsceneFourPath);
        Sprite[] cutsceneFiveFrames = LoadSpritesInFrameOrder(CutsceneFivePath);

        Image cutsceneImage = CreateImage(canvas.transform, "Cutscene Image", cutsceneOneFrames.FirstOrDefault());
        cutsceneImage.preserveAspect = true;
        PositionCutsceneImage(cutsceneImage.rectTransform);

        GameObject controllerObject = new GameObject("Intro Cutscene Controller");
        IntroCutsceneController controller = controllerObject.AddComponent<IntroCutsceneController>();

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("cutsceneImage").objectReferenceValue = cutsceneImage;
        SetSpriteArray(serializedController.FindProperty("cutsceneOneFrames"), cutsceneOneFrames);
        SetSpriteArray(serializedController.FindProperty("cutsceneTwoFrames"), cutsceneTwoFrames);
        SetSpriteArray(serializedController.FindProperty("cutsceneThreeFrames"), cutsceneThreeFrames);
        SetSpriteArray(serializedController.FindProperty("cutsceneFourFrames"), cutsceneFourFrames);
        SetSpriteArray(serializedController.FindProperty("cutsceneFiveFrames"), cutsceneFiveFrames);
        serializedController.FindProperty("gameplaySceneName").stringValue = "1";
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, IntroCutsceneScenePath);
    }

    private static Camera CreateCamera(Color backgroundColor)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = backgroundColor;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        return camera;
    }

    private static Canvas CreateCanvas(Camera camera)
    {
        GameObject canvasObject = new GameObject("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static Image CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panelObject = new GameObject(name);
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        return image;
    }

    private static Button CreateStartButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("Start Button");
        buttonObject.transform.SetParent(parent, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.95f, 0.78f, 0.35f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.88f, 0.48f, 1f);
        colors.pressedColor = new Color(0.82f, 0.56f, 0.22f, 1f);
        button.colors = colors;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -260f);
        buttonRect.sizeDelta = new Vector2(360f, 104f);

        GameObject labelObject = new GameObject("Text");
        labelObject.transform.SetParent(buttonObject.transform, false);
        Text label = labelObject.AddComponent<Text>();
        label.text = "START";
        label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 50;
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(0.12f, 0.08f, 0.05f, 1f);
        Stretch(label.rectTransform);

        return button;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void PositionCutsceneImage(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0f, 120f);
        rectTransform.sizeDelta = new Vector2(1280f, 720f);
    }

    private static void AddScenesToBuildSettings()
    {
        string[] requiredScenes = { MenuScenePath, IntroCutsceneScenePath, FirstScenePath, "Assets/Scenes/2.unity", "Assets/Scenes/3.unity", "Assets/Scenes/4.unity" };
        EditorBuildSettings.scenes = requiredScenes
            .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();
    }

    private static Sprite[] LoadSpritesInFrameOrder(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(sprite => GetFrameNumber(sprite.name))
            .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetFrameNumber(string spriteName)
    {
        int underscoreIndex = spriteName.LastIndexOf('_');
        if (underscoreIndex >= 0 && int.TryParse(spriteName.Substring(underscoreIndex + 1), out int frameNumber))
        {
            return frameNumber;
        }

        return int.MaxValue;
    }

    private static void SetSpriteArray(SerializedProperty property, Sprite[] sprites)
    {
        property.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    private static bool SetSpriteArrayIfDifferent(SerializedProperty property, Sprite[] sprites)
    {
        if (property == null)
        {
            return false;
        }

        bool changed = property.arraySize != sprites.Length;
        if (!changed)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue != sprites[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            SetSpriteArray(property, sprites);
        }

        return changed;
    }
}
