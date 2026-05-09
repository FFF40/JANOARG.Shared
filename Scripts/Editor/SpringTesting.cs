using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using JANOARG.Shared.Utils.Animation;
using UnityEditor;
using UnityEngine;

namespace JANOARG.Shared.Editor
{

    public class SpringTesting : EditorWindow
    {

        [MenuItem("JANOARG/Debug Panes/Spring Testing", priority = 110)]
        public static void ShowWindow() 
        {
            SpringTesting window = GetWindow<SpringTesting>();
            window.titleContent = new GUIContent(window.name = "Spring Testing");
            window.Show();
        }

        float bounciness = 0;
        float perceptiveDuration = 1;
        float startingVelocity = 0;

        public void OnGUI()
        {
            float cellHeight = EditorGUIUtility.singleLineHeight;
            float cellGap = EditorGUIUtility.standardVerticalSpacing;
            float cellHeightGap = cellHeight + cellGap;

            RectOffset padding = GUI.skin.box.margin;

            bounciness = EditorGUI.Slider(
                new Rect(padding.left, padding.top, 320, cellHeight),
                "Bounciness", bounciness, -1, 1
            );
            perceptiveDuration = EditorGUI.Slider(
                new Rect(padding.left, padding.top + cellHeightGap, 320, cellHeight),
                "Perspective Duration", perceptiveDuration, 0.1f, 5
            );
            startingVelocity = EditorGUI.Slider(
                new Rect(padding.left, padding.top + cellHeightGap * 2, 320, cellHeight),
                "Starting Velocity", startingVelocity, -5, 5
            );
            
            for (float t = 0; t <= 10; t += 0.05f)
            {
                float value = SpringEase.Get(t, bounciness, perceptiveDuration, startingVelocity);
                EditorGUI.DrawRect(new Rect(t * Screen.width / 10, Screen.height / 2 + 100 * value - 5, 2, 2), Color.white);
            }
        }
    }
}