using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using LitJson;
using TMPro;

public class ExportUGUIToJson : Editor
{
    [MenuItem("Assets/Export UGUI To Json")]
    static void ExportSelect()
    {
        Object[] prefabs = Selection.GetFiltered(typeof(GameObject), SelectionMode.TopLevel);

        string savePath = EditorUtility.SaveFolderPanel("选择导出位置", null, "");
        if (string.IsNullOrEmpty(savePath)) return;

        foreach (Object obj in prefabs)
        {
            ExportJson(obj as GameObject, savePath);
        }
    }

    static void ExportJson(GameObject prefab, string savePath)
    {
        JsonData jd = GetNodeJson(prefab);
        WriteJsonFile(JsonMapper.ToJson(jd), savePath, prefab.name);
    }

    static JsonData GetNodeJson(GameObject go)
    {
        JsonData node = new JsonData();
        node["name"] = go.name;

        // Is Prefab?
        if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);

            // JsonData node = new JsonData();
            node["prefab"] = new JsonData();
            node["prefab"]["path"] = AssetDatabase.GetAssetPath(source);

            JsonData overrides = GetOverrides(go);
            if (overrides != null)
                node["overrides"] = overrides;

            return node; // ⭐ 不展開 children
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        // 位置
        node["pos"] = new JsonData();
        node["pos"]["x"] = System.Math.Round((double)rt.anchoredPosition.x, 2);
        node["pos"]["y"] = System.Math.Round((double)rt.anchoredPosition.y, 2);

        // 尺寸
        node["size"] = new JsonData();
        node["size"]["w"] = System.Math.Round((double)rt.sizeDelta.x, 2);
        node["size"]["h"] = System.Math.Round((double)rt.sizeDelta.y, 2);

        // scale
        node["scale"] = new JsonData();
        node["scale"]["x"] = System.Math.Round((double)rt.localScale.x, 2);
        node["scale"]["y"] = System.Math.Round((double)rt.localScale.y, 2);

        node["rotation"] = System.Math.Round((double)rt.localEulerAngles.z, 2);
        node["active"] = go.activeSelf;

        // ===== UI 元件 =====
        node["components"] = new JsonData();
        node["components"].SetJsonType(JsonType.Array); // 一定要這行

        // Image
        Image img = go.GetComponent<Image>();
        if (img)
        {
            JsonData cj = new JsonData();
            cj["type"] = "Image";
            cj["imageType"] = img.type.ToString();
            cj["color"] = ColorUtility.ToHtmlStringRGBA(img.color);

            if (img.sprite != null)
            {
                cj["sprite"] = img.sprite.name;

                string path = AssetDatabase.GetAssetPath(img.sprite);
                cj["spritePath"] = path;
            }
            // ⭐ 9-slice
            Vector4 border = img.sprite != null ? img.sprite.border : Vector4.zero;
            if (border != Vector4.zero)
            {
                cj["border"] = new JsonData();
                cj["border"]["left"] = border.x;
                cj["border"]["right"] = border.z;
                cj["border"]["top"] = border.w;
                cj["border"]["bottom"] = border.y;
            }

            // ⭐ ProgressBar（關鍵）
            if (img.type == Image.Type.Filled)
            {
                cj["fill"] = new JsonData();
                cj["fill"]["method"] = img.fillMethod.ToString();
                cj["fill"]["origin"] = img.fillOrigin;
                cj["fill"]["amount"] = img.fillAmount;
                cj["fill"]["clockwise"] = img.fillClockwise;
            }

            node["components"].Add(cj);
        }

        // RawImage
        RawImage raw = go.GetComponent<RawImage>();
        if (raw)
        {
            JsonData cj = new JsonData();
            cj["type"] = "RawImage";

            if (raw.texture != null)
            {
                cj["texture"] = raw.texture.name;
            }

            node["components"].Add(cj);
        }

        // Text (舊版)
        Text txt = go.GetComponent<Text>();
        if (txt)
        {
            JsonData cj = new JsonData();
            cj["type"] = "Text";
            cj["text"] = txt.text;
            cj["fontSize"] = txt.fontSize;
            cj["color"] = ColorUtility.ToHtmlStringRGBA(txt.color);
            cj["alignment"] = txt.alignment.ToString();

            if (txt.font != null)
                cj["font"] = txt.font.name;

            node["components"].Add(cj);
        }

        // TMP_Text
        TMP_Text tmp = go.GetComponent<TMP_Text>();
        if (tmp)
        {
            JsonData cj = new JsonData();
            cj["type"] = "TMP_Text";
            cj["text"] = tmp.text;
            cj["fontSize"] = tmp.fontSize;
            cj["color"] = ColorUtility.ToHtmlStringRGBA(tmp.color);

            if (tmp.font != null)
                cj["font"] = tmp.font.name;

            node["components"].Add(cj);
        }

        // Button
        Button btn = go.GetComponent<Button>();
        if (btn)
        {
            // node["button"] = true;
            JsonData cj = new JsonData();
            cj["type"] = "Button";
            node["components"].Add(cj);
        }
        // Toggle
        // Slider
        // ScrollRect
        ScrollRect sr = go.GetComponent<ScrollRect>();
        if (sr)
        {
            node["scrollView"] = new JsonData();
            node["scrollView"]["horizontal"] = sr.horizontal;
            node["scrollView"]["vertical"] = sr.vertical;
        }

        // GridLayoutGroup
        GridLayoutGroup grid = go.GetComponent<GridLayoutGroup>();
        if (grid)
        {
            node["grid"] = new JsonData();
            node["grid"]["cellSizeX"] = grid.cellSize.x;
            node["grid"]["cellSizeY"] = grid.cellSize.y;
            node["grid"]["spacingX"] = grid.spacing.x;
            node["grid"]["spacingY"] = grid.spacing.y;
        }

        // ===== children =====
        if (go.transform.childCount > 0)
        {
            node["children"] = new JsonData();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                node["children"].Add(GetNodeJson(go.transform.GetChild(i).gameObject));
            }
        }

        return node;
    }

    static void WriteJsonFile(string content, string savePath, string name)
    {
        string fullPath = $"{savePath}/{name}.json";

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        File.WriteAllText(fullPath, content);
        Debug.Log("Exported: " + fullPath);
    }
    
    static JsonData GetOverrides(GameObject go)
    {
        var mods = PrefabUtility.GetPropertyModifications(go);

        if (mods == null || mods.Length == 0)
            return null;

        JsonData overrides = new JsonData();

        foreach (var mod in mods)
        {
            if (mod.target == null) continue;

            string compName = mod.target.GetType().Name;
            string path = mod.propertyPath;

            JsonData compData;

            // ⭐ 安全取得 / 建立
            try
            {
                compData = overrides[compName];
            }
            catch
            {
                compData = new JsonData();
                overrides[compName] = compData;
            }

            // ⭐ value
            if (!string.IsNullOrEmpty(mod.value))
            {
                compData[path] = mod.value;
            }
            // ⭐ object reference
            else if (mod.objectReference != null)
            {
                compData[path] = mod.objectReference.name;

                string assetPath = AssetDatabase.GetAssetPath(mod.objectReference);
                compData[path + "_path"] = assetPath;
            }
        }

        return overrides;
    }
}