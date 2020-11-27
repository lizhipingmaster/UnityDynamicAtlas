using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 可以自定义一个MonoBehaviour的子类。只处理那个子类
[CustomEditor(typeof(MonoBehaviour), true)]
public class CustomEditor4MonoBehaviour : Editor
{
    MonoBehaviour editorTarget { get { return target as MonoBehaviour; } }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();
        List<string> boolField = new List<string>(4);
        // 有序的
        var fields = editorTarget.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance /*| BindingFlags.Static*/ | BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly);
        foreach (var f in fields)
        {
            var attribute = f.GetCustomAttribute<DependAttirbute>(false);
            if (attribute != null)
            {
                if (boolField.Count <= 0 || boolField.Last() != attribute.name)
                    boolField.Add(attribute.name);
            }
        }

        // 绘制
        foreach (var f in fields)
        {
            // 如果是私有字段。就不能有SerializeField属性。公共字段就必须有HideInInspector属性
            bool unDraw = f.IsPrivate ? f.GetCustomAttribute<SerializeField>(false) == null : f.GetCustomAttribute<HideInInspector>(false) != null;

            var attribute = f.GetCustomAttribute<DependAttirbute>(false);
            if (boolField.IndexOf(f.Name) >= 0 || attribute != null && (bool)editorTarget.GetType().GetField(attribute.name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(editorTarget))
            {
                if (!unDraw) { Debug.Log($"<color=yellow>{f.Name} 字段属性不对!!!</color>"); continue; }

                switch (f.FieldType.Name)
                {
                    case "Boolean":
                        {
                            var val = EditorGUILayout.Toggle(f.Name, (bool)f.GetValue(editorTarget));
                            f.SetValue(editorTarget, val);
                        }
                        break;
                    case "Int32":
                        {
                            var val = EditorGUILayout.IntField(f.Name, (int)f.GetValue(editorTarget));
                            f.SetValue(editorTarget, val);
                        }
                        break;
                    case "Single":
                        {
                            var val = EditorGUILayout.FloatField(f.Name, (float)f.GetValue(editorTarget));
                            f.SetValue(editorTarget, val);
                        }
                        break;
                    case "Double":
                        {
                            var val = EditorGUILayout.DoubleField(f.Name, (double)f.GetValue(editorTarget));
                            f.SetValue(editorTarget, val);
                        }
                        break;
                    case "String":
                        {
                            var val = EditorGUILayout.TextField(f.Name, (string)f.GetValue(editorTarget));
                            f.SetValue(editorTarget, val);
                        }
                        break;
                    default:
                        {
                            if (!f.IsPublic) { Debug.Log($"<color=yellow>{f.Name} 字段必须非私有!!!</color>"); continue; }

                            var prop = serializedObject.FindProperty(f.Name);   // 私有字段访问不了 :(
                            EditorGUILayout.PropertyField(prop);
                        }
                        break;

                }
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
}