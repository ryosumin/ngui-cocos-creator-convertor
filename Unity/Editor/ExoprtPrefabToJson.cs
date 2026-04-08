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

    public static void RunExportCLI()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { "Assets/Demo" }
        );

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            Debug.Log($"[RunExport] Path: {path}");

            if (prefab != null)
            {
                // ⭐ 取得該 prefab 所在資料夾
                string exportPath = Path.GetDirectoryName(path);
                ExportJson(prefab, exportPath);
            }
        }
    }

    static void ExportJson(GameObject prefab, string savePath)
    {
        JsonData jd = GetNodeJson(prefab);
        WriteJsonFile(JsonMapper.ToJson(jd), savePath, prefab.name);
    }

    static JsonData DumpRawComponent(Component comp)
    {
        JsonData data = new JsonData();
        // data.SetJsonType(JsonType.Object);

        SerializedObject so = new SerializedObject(comp);
        SerializedProperty prop = so.GetIterator();

        bool enterChildren = true;
        bool hasData = false;

        // // ⭐ 9-slice
        if(comp is Image)
        {
            Image img = (Image) comp;
            Vector4 border = img.sprite != null ? img.sprite.border : Vector4.zero;
            if (border != Vector4.zero)
            {
                data["border"] = new JsonData();
                data["border"]["left"] = border.x;
                data["border"]["right"] = border.z;
                data["border"]["top"] = border.w;
                data["border"]["bottom"] = border.y;
            }
        }

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            string path = prop.propertyPath;

            // ⭐ 跳過 script reference
            if (path == "m_Script") continue;

            // ⭐ 跳過不該序列化的（超重要）
            if (path.StartsWith("m_OnClick") ||
                path.StartsWith("m_Navigation") ||
                path.StartsWith("m_AnimationTriggers"))
            {
                continue;
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    data[path] = prop.intValue;
                    hasData = true;
                    break;

                case SerializedPropertyType.Boolean:
                    data[path] = prop.boolValue;
                    hasData = true;
                    break;

                case SerializedPropertyType.Float:
                    data[path] = prop.floatValue;
                    hasData = true;
                    break;

                case SerializedPropertyType.String:
                    data[path] = prop.stringValue;
                    hasData = true;
                    break;

                case SerializedPropertyType.Color:
                    data[path] = ColorUtility.ToHtmlStringRGBA(prop.colorValue);
                    hasData = true;
                    break;

                case SerializedPropertyType.Enum:
                    data[path] = prop.enumNames[prop.enumValueIndex];
                    hasData = true;
                    break;

                case SerializedPropertyType.ObjectReference:
                    if (prop.objectReferenceValue != null)
                    {
                        data[path] = prop.objectReferenceValue.name;
                        data[path + "_path"] = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
                        hasData = true;
                    }
                    break;

                case SerializedPropertyType.Vector2:
                    data[path] = $"{prop.vector2Value.x},{prop.vector2Value.y}";
                    hasData = true;
                    break;

                case SerializedPropertyType.Vector3:
                    data[path] = $"{prop.vector3Value.x},{prop.vector3Value.y},{prop.vector3Value.z}";
                    hasData = true;
                    break;

                case SerializedPropertyType.Vector4:
                    data[path] = $"{prop.vector4Value.x},{prop.vector4Value.y},{prop.vector4Value.z},{prop.vector4Value.w}";
                    hasData = true;
                    break;

                case SerializedPropertyType.Rect:
                    var r = prop.rectValue;
                    data[path] = $"{r.x},{r.y},{r.width},{r.height}";
                    hasData = true;
                    break;

                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    data[path] = $"{q.x},{q.y},{q.z},{q.w}";
                    hasData = true;
                    break;

                case SerializedPropertyType.Generic:
                    {
                        try{
                            JsonData sub = DumpGenericProperty(prop);
                            if (sub != null)
                            {
                                data[path] = sub;
                                hasData = true;
                            }
                        }catch{

                        }
                    }
                    break;

                default:
                    // ❌ 不要再用 ToString()
                    break;
            }
        }

        // return hasData ? data : null;
        return data;
    }

    static JsonData DumpGenericProperty(SerializedProperty prop)
    {
        SerializedProperty copy = prop.Copy();
        SerializedProperty end = copy.GetEndProperty();

        JsonData sub = new JsonData();
        sub.SetJsonType(JsonType.Object);

        bool hasData = false;
        bool enter = true;

        while (copy.NextVisible(enter) && !SerializedProperty.EqualContents(copy, end))
        {
            enter = false;

            string subPath = copy.propertyPath.Replace(prop.propertyPath + ".", "");

            switch (copy.propertyType)
            {
                case SerializedPropertyType.Float:
                    sub[subPath] = copy.floatValue;
                    hasData = true;
                    break;

                case SerializedPropertyType.Boolean:
                    sub[subPath] = copy.boolValue;
                    hasData = true;
                    break;

                case SerializedPropertyType.Integer:
                    sub[subPath] = copy.intValue;
                    hasData = true;
                    break;

                case SerializedPropertyType.Color:
                    sub[subPath] = ColorUtility.ToHtmlStringRGBA(copy.colorValue);
                    hasData = true;
                    break;

                case SerializedPropertyType.Enum:
                    int index = copy.enumValueIndex;
                    string[] names = copy.enumNames;

                    if (names != null && index >= 0 && index < names.Length)
                    {
                        sub[subPath] = names[index];
                        sub[subPath + "_index"] = index;
                    }
                    else
                    {
                        sub[subPath] = index;
                    }

                    hasData = true;
                    break;
            }
        }

        return hasData ? sub : null;
    }

    static JsonData DumpRawComponents(GameObject go)
    {
        Component[] components = go.GetComponents<Component>();
        JsonData rawCompents = new JsonData();

        foreach(var cmp in components)
        {
            if (cmp == null) continue;
            string compName = cmp.GetType().Name;
            // string path = cmp.propertyPath;
            rawCompents[compName] = DumpRawComponent(cmp);
        }
        
        return rawCompents;
    }

    static JsonData DumpComponents(GameObject go)
    {
        // ===== UI 元件 =====
        JsonData components = new JsonData();
        components.SetJsonType(JsonType.Array); // 一定要這行

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

            components.Add(cj);
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

            components.Add(cj);
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

            components.Add(cj);
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

            components.Add(cj);
        }

        // Button
        Button btn = go.GetComponent<Button>();
        if (btn)
        {
            // node["button"] = true;
            JsonData cj = new JsonData();
            cj["type"] = "Button";
            components.Add(cj);
        }
        // Toggle
        // Slider
        // ScrollRect
        ScrollRect sr = go.GetComponent<ScrollRect>();
        if (sr)
        {
            // node["scrollView"] = new JsonData();
            // node["scrollView"]["horizontal"] = sr.horizontal;
            // node["scrollView"]["vertical"] = sr.vertical;
        }

        // GridLayoutGroup
        GridLayoutGroup grid = go.GetComponent<GridLayoutGroup>();
        if (grid)
        {
            // node["grid"] = new JsonData();
            // node["grid"]["cellSizeX"] = grid.cellSize.x;
            // node["grid"]["cellSizeY"] = grid.cellSize.y;
            // node["grid"]["spacingX"] = grid.spacing.x;
            // node["grid"]["spacingY"] = grid.spacing.y;
        }

        return components;
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

        /*
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
        */
        node["active"] = go.activeSelf;

        node["components"] = DumpRawComponents(go);

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