using FastLink.Util;
using UnityEngine;

namespace FastLink;

public partial class FastLinkPlugin
{
    private const int WindowId = -70;
    private Rect ContentWindowRect { get; set; }
    internal static Rect ScreenRect;
    internal static string EditorText = "";
    internal static Vector2 SettingWindowScrollPos;
    private void Update() => Functions.ShouldShowCursor();
    private void LateUpdate() => Functions.ShouldShowCursor();

    private void OnGUI()
    {
        if (EditorText.Length <= 0) return;
        Functions.ShouldShowCursor();
        /* Mimic the configuration manager window, but just a bit bigger */
        int width = Mathf.Min(Screen.width, 1000);
        int height = Screen.height < 560 ? Screen.height : Screen.height - 200;
        int offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
        int offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
        ContentWindowRect = new Rect(offsetX, offsetY, width, height);
        ScreenRect = new Rect(0, 0, Screen.width, Screen.height);
        GUILayout.Window(WindowId, ContentWindowRect, DoServerWindow, $"{ModName} Servers");
    }

    private void DoServerWindow(int id)
    {
        Functions.MakeShitDarkerInABadWay(); // Made the text inside a bit easier to read by adding this box behind shit, but still not perfect.

        // Heavily used reference https://docs.unity3d.com/2017.1/Documentation/ScriptReference/Event.html
        if (GUI.GetNameOfFocusedControl() == "FastLinkEditor" &&
            Event.current.type is EventType.KeyDown or EventType.KeyUp &&
            Event.current.isKey)
        {
            // Tried this, shit worked. https://stackoverflow.com/questions/55522192/how-to-use-unityengine-texteditor-class-no-documentation
            TextEditor editor =
                (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);

            if (Event.current.keyCode == KeyCode.Tab)
            {
                if (Event.current.type == EventType.KeyUp)
                {
                    editor.text += "    ";
                    editor.MoveLineEnd();
                    EditorText = editor.text;
                }

                Event.current
                    .Use(); // Sets the event as used (EventType.Used), so it won't be processed by other GUI elements.
            }
        }

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = Color.black;
        Functions.BuildContentScroller();
        GUI.backgroundColor = Color.green;
        GUI.contentColor = Color.white;
        Functions.BuildButtons();
        GUILayout.EndHorizontal();
    }
}