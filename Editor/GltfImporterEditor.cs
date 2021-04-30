﻿// Copyright 2020-2021 Andreas Atteneder
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.IO;
using GLTFast.Editor;
using UnityEditor;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GLTFast {

    [CustomEditor(typeof(GltfImporter))]
    // [CanEditMultipleObjects]
    public class GltfImporterEditor : ScriptedImporterEditor
    {
        SerializedProperty m_AssetDependencies;
        SerializedProperty m_ReportItems;
        
        protected override bool needsApplyRevert => false;
        
        public override void OnEnable()
        {
            base.OnEnable();
            m_AssetDependencies = serializedObject.FindProperty("assetDependencies");
            m_ReportItems = serializedObject.FindProperty("reportItems");
        }
        
        public override VisualElement CreateInspectorGUI() {
            
            // Update the serializedObject in case it has been changed outside the Inspector.
            serializedObject.Update();

            const string mainMarkup = "GltfImporter";
            const string reportItemMarkup = "ReportItem";
            const string dependencyMarkup = "Dependency";
            var root = new VisualElement();
            
            var visualTree = Resources.Load(mainMarkup) as VisualTreeAsset;
            visualTree.CloneTree(root);
            
            var numDeps = m_AssetDependencies.arraySize;
            
            var maliciousTextureImporters = new List<TextureImporter>();
            
            var reportItemCount = m_ReportItems.arraySize;

            var reportRoot = root.Query<VisualElement>(name: "Report").First();
            
            if (reportItemCount > 0) {
                // var reportList = new List<ReportItem>
                var reportItemTree = Resources.Load(reportItemMarkup) as VisualTreeAsset;
                var reportList = reportRoot.Query<ListView>().First();
                // reportList.bindingPath = nameof(m_ReportItems);
                reportList.makeItem = () => reportItemTree.CloneTree();
                reportList.bindItem = (element, i) => {
                    if (i >= reportItemCount) {
                        element.style.display = DisplayStyle.None;
                        return;
                    }
                    var msg = element.Q<Label>("Message");
                    var item = m_ReportItems.GetArrayElementAtIndex(i);
                    
                    var typeProp = item.FindPropertyRelative("type");
                    var codeProp = item.FindPropertyRelative("code");
                    var messagesProp = item.FindPropertyRelative("messages");

                    var type = (LogType)typeProp.intValue;
                    var code = (ReportCode) codeProp.intValue;

                    var icon = element.Q<VisualElement>("Icon");
                    switch (type) {
                        case LogType.Error:
                        case LogType.Assert:
                        case LogType.Exception:
                            icon.RemoveFromClassList("info");
                            icon.AddToClassList("error");
                            break;
                        case LogType.Warning:
                            icon.RemoveFromClassList("info");
                            icon.AddToClassList("warning");
                            break;
                    }

                    var messages = GetStringValues(messagesProp);
                    var ritem = new ReportItem(type, code, messages);
                    msg.text = ritem.ToString();
                };
            } else {
                reportRoot.style.display = DisplayStyle.None;
            }
            
            for (int i = 0; i < numDeps; i++) {
                var x = m_AssetDependencies.GetArrayElementAtIndex(i);
                var assetPathProp = x.FindPropertyRelative("assetPath");
                
                var typeProp = x.FindPropertyRelative("type");
                var type = (GltfAssetDependency.Type)typeProp.enumValueIndex;
                if (type == GltfAssetDependency.Type.Texture) {
                    var importer = AssetImporter.GetAtPath(assetPathProp.stringValue) as TextureImporter;
                    if (importer!=null) {
                        if (importer.textureShape != TextureImporterShape.Texture2D) {
                            maliciousTextureImporters.Add(importer);
                        }
                    }
                }
            }
            
            if (maliciousTextureImporters.Count>0) {
                var dependencyTree = Resources.Load(dependencyMarkup) as VisualTreeAsset;

                root.Query<Button>("fixall").First().clickable.clicked += () => {
                    foreach (var maliciousTextureImporter in maliciousTextureImporters) {
                        maliciousTextureImporter.textureShape = TextureImporterShape.Texture2D;
                    }
                    
                    foreach (var maliciousTextureImporter in maliciousTextureImporters) {
                        maliciousTextureImporter.SaveAndReimport();
                    }
                };

                var foldout = root.Query<Foldout>().First();
                // var row = root.Query<VisualElement>(className: "fix-texture-row").First();
                foreach (var maliciousTextureImporter in maliciousTextureImporters) {
                    var row = dependencyTree.CloneTree();
                    var icon = row.Query<VisualElement>("Icon").First();
                    icon.AddToClassList("warning");
                    foldout.Add(row);
                    // textureRowTree.CloneTree(foldout);
                    var path = AssetDatabase.GetAssetPath(maliciousTextureImporter);
                    row.Query<Label>().First().text = Path.GetFileName(path);
                    row.Query<Button>().First().clickable.clicked += () => {
                        maliciousTextureImporter.textureShape = TextureImporterShape.Texture2D;
                        maliciousTextureImporter.SaveAndReimport();
                        row.style.display = DisplayStyle.None;
                    };
                }
            } else {
                var depRoot = root.Query<VisualElement>("Dependencies").First();
                depRoot.style.display = DisplayStyle.None;
            }
            
            root.Bind(serializedObject);
            
            // Apply the changes so Undo/Redo is working
            serializedObject.ApplyModifiedProperties();
            
            return root;
        }

        static string[] GetStringValues(SerializedProperty property) {
            if (!property.isArray || property.arraySize < 1) return null;
            var result = new string[property.arraySize];
            for (var i = 0; i < property.arraySize; i++) {
                result[i] = property.GetArrayElementAtIndex(i).stringValue;
            }
            return result;
        }
    }
}