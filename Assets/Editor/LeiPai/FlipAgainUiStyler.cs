using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Non-destructive presentation pass: keeps existing UI bindings and rules.</summary>
public static class FlipAgainUiStyler
{
    private const string Sheet = "Assets/AssetRaw/UIRaw/FlipAgain/FlipAgainIcons.png";
    private static readonly string[] Names = { "card_basic", "card_mine", "card_tool", "card_collector", "card_amplifier", "card_resonator", "card_multiplier", "card_mapper", "card_echo", "card_gold", "card_shield", "card_back" };

    [MenuItem("Tools/再翻一张/应用轻纸感界面")]
    public static void Apply()
    {
        var importer = AssetImporter.GetAtPath(Sheet) as TextureImporter;
        if (importer == null) throw new System.IO.FileNotFoundException("Missing icon sheet", Sheet);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        var slices = new SpriteMetaData[Names.Length];
        for (int i = 0; i < Names.Length; i++)
            slices[i] = new SpriteMetaData { name = Names[i], pivot = new Vector2(.5f, .5f), alignment = 0,
                rect = new Rect(i % 4 * width / 4f, (2 - i / 4) * height / 3f, width / 4f, height / 3f) };
        importer.spritesheet = slices;
        importer.SaveAndReimport();
        StylePrefab("Assets/AssetRaw/UI/LeiPaiMainUI.prefab", false);
        StylePrefab("Assets/AssetRaw/UI/LeiPaiCardItem.prefab", true);
        AssetDatabase.SaveAssets();
        Debug.Log("[再翻一张] 轻纸感界面已应用。规则、节点绑定与布局未改变。");
    }

    private static void StylePrefab(string path, bool card)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                bool button = image.GetComponent<Button>() != null;
                image.color = button ? new Color(.88f, .92f, .85f) : new Color(.97f, .95f, .90f);
                if (image.transform == root.transform) image.color = new Color(.95f, .93f, .87f);
                if (button)
                {
                    var colors = image.GetComponent<Button>().colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1f, .96f, .82f);
                    colors.pressedColor = new Color(.82f, .87f, .78f);
                    image.GetComponent<Button>().colors = colors;
                }
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = new Color(.23f, .24f, .21f);
                text.text = text.text.Replace("雷牌", "再翻一张");
            }
            if (card) AddArtwork(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void AddArtwork(GameObject root)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToDictionary(s => s.name);
        Transform existing = root.transform.Find("m_go_Artwork");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var artwork = new GameObject("m_go_Artwork", typeof(RectTransform));
        var rect = artwork.GetComponent<RectTransform>();
        rect.SetParent(root.transform, false);
        rect.anchorMin = new Vector2(.16f, .25f);
        rect.anchorMax = new Vector2(.84f, .88f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        foreach (string name in Names)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            var child = node.GetComponent<RectTransform>();
            child.SetParent(rect, false);
            child.anchorMin = Vector2.zero; child.anchorMax = Vector2.one;
            child.offsetMin = child.offsetMax = Vector2.zero;
            var image = node.GetComponent<Image>();
            image.sprite = sprites[name]; image.preserveAspect = true; image.raycastTarget = false;
            node.SetActive(name == "card_back");
        }
    }
}
