using UnityEditor;
using UnityEngine;

namespace MStudio
{
    [InitializeOnLoad]
    public class StyleHierarchy
    {
        static string[] dataArray;//Find ColorPalette GUID
        static string path = null!;//Get ColorPalette(ScriptableObject) path
        static ColorPalette colorPalette = null!;

        static StyleHierarchy()
        {
            dataArray = AssetDatabase.FindAssets("t:ColorPalette");

            if (dataArray.Length >= 1)
            {    //We have only one color palette, so we use dataArray[0] to get the path of the file
                path = AssetDatabase.GUIDToAssetPath(dataArray[0]);

                colorPalette = AssetDatabase.LoadAssetAtPath<ColorPalette>(path);

                EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindow;
            }
        }

        private static void OnHierarchyWindow(int instanceID, Rect selectionRect)
        {
            //To make sure there is no error on the first time the tool imported in project
            if (dataArray.Length == 0) return;

#pragma warning disable CS0618 // 형식 또는 멤버는 사용되지 않습니다.
            UnityEngine.Object instance = EditorUtility.InstanceIDToObject(instanceID);
#pragma warning restore CS0618 // 형식 또는 멤버는 사용되지 않습니다.

            if (instance != null)
            {
                for (int i = 0; i < colorPalette.colorDesigns.Count; i++)
                {
                    var design = colorPalette.colorDesigns[i];

                    //Check if the name of each gameObject is begin with keyChar in colorDesigns list.
                    if (instance.name.StartsWith(design.keyChar))
                    {
                        //Remove the symbol(keyChar) from the name.
                        string newName = instance.name.Substring(design.keyChar.Length);
                        //Draw a rectangle as a background, and set the color.
                        EditorGUI.DrawRect(selectionRect, design.backgroundColor);

                        //Create a new GUIStyle to match the desing in colorDesigns list.
                        GUIStyle newStyle = new GUIStyle
                        {
                            alignment = design.textAlignment,
                            fontStyle = design.fontStyle,
                            normal = new GUIStyleState()
                            {
                                textColor = design.textColor,
                            }
                        };

                        //Draw a label to show the name in upper letters and newStyle.
                        //If you don't like all capital latter, you can remove ".ToUpper()".
                        EditorGUI.LabelField(selectionRect, newName.ToUpper(), newStyle);
                    }
                }
            }
        }
    }
}