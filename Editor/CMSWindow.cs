using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; 
using UnityEngine.Networking;

namespace Shosho.CMS
{

    public class CMSEditorWindow : EditorWindow
    {
        private ReorderableList reorderableList;
        private EditorCoroutine currentSync;

        private CMSSettings cmsSettings => CMS.cmsSettings;

        private string cmsSettingsPath => CMS.cmsSettingsPath;
        public int callbackOrder => 0;

        // Add menu item to open the CMS Editor window
        [MenuItem("CMS/Settings")]
        public static void ShowWindow()
        {
            GetWindow<CMSEditorWindow>("CMS Settings");
        }

        private void OnEnable()
        {

            reorderableList = new ReorderableList(cmsSettings.restEndpoints, typeof(RestEndpoint), true, true, true, true);

            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {

                DrawEndPointEntry(rect, index);
            };


            reorderableList.onRemoveCallback = (list) =>
            {
                cmsSettings.restEndpoints.RemoveAt(list.index);
                CMS.SaveSettings();
            };

            reorderableList.onReorderCallback = (list) =>
            {
                CMS.SaveSettings();
            };

        }

        private void DrawEndPointEntry(Rect rect, int index)
        {
            const float iconsSize = 20f;
            const float spacing = 5f;
            Rect iconRect = new Rect(rect.x, rect.y, iconsSize, EditorGUIUtility.singleLineHeight);
            Rect textRect = new Rect(rect.x + iconsSize + spacing, rect.y, rect.width - iconsSize - spacing, EditorGUIUtility.singleLineHeight);

            //safegaurd for when the list is modified
            if (index <0 || index >= cmsSettings.restEndpoints.Count) 
            {
                Debug.LogWarning("index out of range");
                return;
            }
            var status = cmsSettings.restEndpoints[index].status;
            GUIContent icon = status switch
            {
                urlStatus.Valid => EditorGUIUtility.IconContent("TestPassed"),
                urlStatus.Invalid => EditorGUIUtility.IconContent("TestFailed"),
                urlStatus.Unknown => EditorGUIUtility.IconContent("TestInconclusive"),
                urlStatus.Validating => EditorGUIUtility.IconContent(GetSpinnerIcon()),
                _ => EditorGUIUtility.IconContent("TestInconclusive"),
            };
            EditorGUI.LabelField(iconRect, icon);


            EditorGUI.BeginChangeCheck();
            string s = EditorGUI.TextField(textRect, cmsSettings.restEndpoints[index].name);
            if (EditorGUI.EndChangeCheck())
            {
                cmsSettings.restEndpoints[index].status = urlStatus.Unknown;
                cmsSettings.restEndpoints[index].name = s;
                CMS.SaveSettings();
            }



        }

        private void OnDisable()
        {

        }


        private void OnGUI()
        {
            GUI.enabled = !CMS.isSyncing;
            DrawApiTokenField();
            DrawURLFields();
       

            // Show and edit the list of REST endpoints
            EditorGUILayout.LabelField("REST Endpoints:");
            reorderableList.DoLayoutList();

            // Sync button
            if (GUILayout.Button("Sync Content"))
            {
                currentSync = EditorCoroutineUtility.StartCoroutine(CMS.Sync(), this);
            }

            if (CMS.isSyncing || CMS.isValidating)
            {
                Repaint();
            }
        }

        private void DrawApiTokenField()
        {
            EditorGUILayout.BeginHorizontal();
            /*
            var apiTokenStatus = cmsSettings.apiTokenStatus;
   
            GUIContent apiTokenIcon = apiTokenStatus switch
            {
                urlStatus.Valid => EditorGUIUtility.IconContent("TestPassed"),
                urlStatus.Invalid => EditorGUIUtility.IconContent("TestFailed"),
                urlStatus.Unknown => EditorGUIUtility.IconContent("TestInconclusive"),
                urlStatus.Validating => EditorGUIUtility.IconContent(GetSpinnerIcon()),
                _ => EditorGUIUtility.IconContent("TestInconclusive"),
            };
                  */                  
            GUILayout.Label(EditorGUIUtility.IconContent("AssemblyLock"), GUILayout.Width(18), GUILayout.Height(18));

            EditorGUI.BeginChangeCheck();
            string newApiToken = EditorGUILayout.TextField(
                "API Token",
                cmsSettings.apiToken
            );
            if (EditorGUI.EndChangeCheck())
            {

                cmsSettings.apiTokenStatus = urlStatus.Unknown;
                cmsSettings.apiToken = newApiToken;
                CMS.SaveSettings();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawURLFields()
        {
            EditorGUILayout.BeginHorizontal();

            var baseUrlStatus = cmsSettings.baseUrlStatus;
            GUIContent baseUrlIcon = baseUrlStatus switch
            {
                urlStatus.Valid => EditorGUIUtility.IconContent("TestPassed"),
                urlStatus.Invalid => EditorGUIUtility.IconContent("TestFailed"),
                urlStatus.Unknown => EditorGUIUtility.IconContent("TestInconclusive"),
                urlStatus.Validating => EditorGUIUtility.IconContent(GetSpinnerIcon()),
                _ => EditorGUIUtility.IconContent("TestInconclusive"),
            };

            GUILayout.Label(baseUrlIcon, GUILayout.Width(18), GUILayout.Height(18));

            EditorGUI.BeginChangeCheck();
            string newBaseUrl = EditorGUILayout.TextField(
                "CMS URL",
                cmsSettings.baseUrl
            );
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var endpoint in cmsSettings.restEndpoints)
                {
                    endpoint.status = urlStatus.Unknown;
                }
                cmsSettings.baseUrlStatus = urlStatus.Unknown;
                char[] trimChars = new char[] { '/', '\\'};
                newBaseUrl = newBaseUrl.TrimEnd(trimChars);
                cmsSettings.baseUrl = newBaseUrl;
                CMS.SaveSettings();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            var languageStatus = cmsSettings.languageUrlStatus;
            GUIContent languageUrlIcon = languageStatus switch
            {
                urlStatus.Valid => EditorGUIUtility.IconContent("TestPassed"),
                urlStatus.Invalid => EditorGUIUtility.IconContent("TestFailed"),
                urlStatus.Unknown => EditorGUIUtility.IconContent("TestInconclusive"),
                urlStatus.Validating => EditorGUIUtility.IconContent(GetSpinnerIcon()),
                _ => EditorGUIUtility.IconContent("TestInconclusive"),
            };

            GUILayout.Label(languageUrlIcon, GUILayout.Width(18), GUILayout.Height(18));

            EditorGUILayout.LabelField("Fetch Languages");


            EditorGUILayout.EndHorizontal();

        }

        string GetSpinnerIcon()
        {
            int frame = (int)(EditorApplication.timeSinceStartup * 10) % 12;
            return ("WaitSpin" + frame.ToString("00"));
        }

    }
}