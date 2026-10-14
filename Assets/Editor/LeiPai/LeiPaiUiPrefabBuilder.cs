using System.IO;
using HtmlToUGUI;
using TMPro;
using SuperScrollView;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class LeiPaiUiPrefabBuilder
{
    // 原型源文件必须放在 Editor 目录，避免被 YooAsset 的 UI 收集器当成运行时资源。
    private const string JsonPath = "Assets/Editor/LeiPai/Html/LeiPaiMainUI.json";
    private const string PrefabPath = "Assets/AssetRaw/UI/LeiPaiMainUI.prefab";
    private const string CardPrefabPath = "Assets/AssetRaw/UI/LeiPaiCardItem.prefab";
    private const string FontSourcePath = "Assets/AssetRaw/UIRaw/Raw/Fonts/SimHei.ttf";
    private const string FontAssetPath = "Assets/AssetRaw/UIRaw/Raw/Fonts/SimHei-Dynamic.asset";

    [InitializeOnLoadMethod]
    private static void EnsureWindowPrefabIsValid()
    {
        EditorApplication.delayCall += () =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            TMP_FontAsset chineseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            TMP_Text sampleText = prefab == null ? null : prefab.GetComponentInChildren<TMP_Text>(true);
            LoopGridView grid = prefab == null ? null : prefab.GetComponentInChildren<LoopGridView>(true);
            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (prefab == null || prefab.GetComponent<Canvas>() == null || chineseFont == null || sampleText == null
                                   || sampleText.font != chineseFont || grid == null || cardPrefab == null
                                   || prefab.transform.localScale != Vector3.one
                                   || File.GetLastWriteTimeUtc(JsonPath) > File.GetLastWriteTimeUtc(PrefabPath))
                Rebuild();
        };
    }

    [MenuItem("Tools/LeiPai/Rebuild Main UI Prefab")]
    public static void Rebuild()
    {
        AssetDatabase.ImportAsset(FontSourcePath, ImportAssetOptions.ForceSynchronousImport);
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (json == null) throw new FileNotFoundException("请先生成雷牌 UI JSON。", JsonPath);

        GameObject canvasObject = new GameObject("LeiPaiUiBakeCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject root = HtmlToUGUIBaker.BakeAssetToCanvas(json, canvas);
        if (root == null) throw new UnityException("雷牌 UI 烘焙失败，未生成根节点。 ");
        // ScreenSpaceOverlay Canvas 必须是层级根节点；先脱离临时烘焙 Canvas 再挂组件。
        root.transform.SetParent(null, false);
        ConfigureWindowRoot(root);
        BindChineseFont(root, LoadOrCreateChineseFont());
        ConfigureCardGrid(root);
        Transform reward = root.transform.Find("m_go_RewardPanel");
        if (reward != null) reward.gameObject.SetActive(false);

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(canvasObject);
        FlipAgainUiStyler.Apply();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LeiPai] UI Prefab 已生成: {PrefabPath}");
    }

    private static void ConfigureWindowRoot(GameObject root)
    {
        // UIWindow.Handle_Completed 会直接从 Prefab 根节点取 Canvas 和 GraphicRaycaster。
        // 不能只依赖烘焙时使用的临时父 Canvas。
        Canvas windowCanvas = root.GetComponent<Canvas>();
        if (windowCanvas == null) windowCanvas = root.AddComponent<Canvas>();
        windowCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        windowCanvas.overrideSorting = true;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_FontAsset LoadOrCreateChineseFont()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset != null) return fontAsset;

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        if (sourceFont == null) throw new FileNotFoundException("缺少中文字体源文件。", FontSourcePath);

        fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
        if (fontAsset == null) throw new UnityException("TMP 无法从 SimHei.ttf 创建动态字体资产。");
        fontAsset.name = "SimHei-Dynamic";
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        const string requiredCharacters = "雷牌当前关卡分数目标剩余回合护盾侦察行动开启关闭模式重试局信息未翻卡收集器蓄能已激活核心最近结算等待首次入口左右键标记疑似地雷安全基础工具增幅共鸣倍频测绘回响奖励选择完成成功失败取消混合线索拆分操作执行三全部挑战胜利加入池下一开始当前种子布局保持不变";
        if (!fontAsset.TryAddCharacters(requiredCharacters, out string missingCharacters))
            Debug.LogWarning($"[LeiPai] 中文字体仍缺少字符: {missingCharacters}");
        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        foreach (Texture2D atlas in fontAsset.atlasTextures)
            if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    private static void BindChineseFont(GameObject root, TMP_FontAsset fontAsset)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = fontAsset;
            EditorUtility.SetDirty(text);
        }
    }

    private static void ConfigureCardGrid(GameObject root)
    {
        Transform gridTransform = root.transform.Find("m_go_BoardPanel/m_grid_Board");
        if (gridTransform == null || gridTransform.childCount == 0)
            throw new UnityException("雷牌棋盘缺少卡牌模板节点。");

        GameObject template = Object.Instantiate(gridTransform.GetChild(0).gameObject);
        template.name = "LeiPaiCardItem";
        template.SetActive(true);
        if (template.GetComponent<LoopGridViewItem>() == null) template.AddComponent<LoopGridViewItem>();
        Directory.CreateDirectory(Path.GetDirectoryName(CardPrefabPath));
        GameObject cardPrefab = PrefabUtility.SaveAsPrefabAsset(template, CardPrefabPath);
        Object.DestroyImmediate(template);

        for (int i = gridTransform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(gridTransform.GetChild(i).gameObject);
        GridLayoutGroup oldGrid = gridTransform.GetComponent<GridLayoutGroup>();
        if (oldGrid != null) Object.DestroyImmediate(oldGrid);

        GameObject gridObject = gridTransform.gameObject;
        ScrollRect scroll = gridObject.GetComponent<ScrollRect>() ?? gridObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = false;
        scroll.viewport = gridTransform as RectTransform;
        if (gridObject.GetComponent<RectMask2D>() == null) gridObject.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content", typeof(RectTransform));
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.SetParent(gridTransform, false);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        scroll.content = contentRect;

        LoopGridView loopGrid = gridObject.GetComponent<LoopGridView>() ?? gridObject.AddComponent<LoopGridView>();
        loopGrid.ItemPrefabDataList.Clear();
        loopGrid.ItemPrefabDataList.Add(new GridViewItemPrefabConfData
        {
            mItemPrefab = cardPrefab,
            mInitCreateCount = 36
        });
        EditorUtility.SetDirty(loopGrid);
    }

    public static void RebuildFromCommandLine()
    {
        Rebuild();
        EditorApplication.Exit(0);
    }
}
