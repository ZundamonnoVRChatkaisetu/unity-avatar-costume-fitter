using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    public class CostumeFitter : EditorWindow {
        // アバターとコスチュームの参照
        private GameObject avatarObject;
        private GameObject costumeObject;
        private GameObject activeCostumeInstance;
        
        // 微調整用の設定
        private Dictionary<string, float> adjustmentValues = new Dictionary<string, float>();
        private Vector2 scrollPosition;
        private bool showAdjustments = false;
        
        // 詳細設定
        private bool showAdvancedSettings = false;
        private bool createBlendShapes = false;
        private bool adjustSkinWeights = true;
        private bool autoScale = true;
        
        // About情報
        private bool showAboutInfo = false;
        private string aboutInfo = "全アバター衣装自動調整ツール\nVersion 1.0\n\n衣装をアバターに自動的に合わせるツールです。\n\n使い方：\n1. アバターと衣装をドラッグ＆ドロップで選択\n2. 「衣装を着せる」ボタンをクリック\n3. 微調整バーで細かい調整を行う";
        
        [MenuItem("ずん解/衣装調整ツール")]
        public static void ShowWindow() {
            GetWindow<CostumeFitter>("衣装調整ツール");
        }
        
        private void OnGUI() {
            // ヘッダー
            GUILayout.Label("全アバター衣装自動調整ツール", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // About情報
            showAboutInfo = EditorGUILayout.Foldout(showAboutInfo, "ツールについて");
            if (showAboutInfo) {
                EditorGUILayout.HelpBox(aboutInfo, MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            // アバターとコスチュームの選択エリア
            EditorGUILayout.BeginVertical("box");
            
            // アバターの選択
            EditorGUILayout.LabelField("アバターを選択");
            avatarObject = (GameObject)EditorGUILayout.ObjectField(avatarObject, typeof(GameObject), true);
            
            // ドラッグアンドドロップのヒント
            EditorGUILayout.HelpBox("アバターをここにドラッグ＆ドロップしてください", MessageType.Info);
            EditorGUILayout.Space();
            
            // コスチュームの選択
            EditorGUILayout.LabelField("衣装を選択");
            costumeObject = (GameObject)EditorGUILayout.ObjectField(costumeObject, typeof(GameObject), true);
            
            // ドラッグアンドドロップのヒント
            EditorGUILayout.HelpBox("衣装をここにドラッグ＆ドロップしてください", MessageType.Info);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // 詳細設定の折りたたみ
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "詳細設定");
            if (showAdvancedSettings) {
                EditorGUILayout.BeginVertical("box");
                
                autoScale = EditorGUILayout.Toggle("自動サイズ調整", autoScale);
                adjustSkinWeights = EditorGUILayout.Toggle("ウェイト最適化", adjustSkinWeights);
                createBlendShapes = EditorGUILayout.Toggle("ブレンドシェイプ作成", createBlendShapes);
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space();
            
            // 「衣装を着せる」ボタン
            GUI.enabled = avatarObject != null && costumeObject != null;
            if (GUILayout.Button("衣装を着せる", GUILayout.Height(40))) {
                FitCostume();
                showAdjustments = true;
            }
            GUI.enabled = true;
            
            EditorGUILayout.Space();
            
            // 衣装の微調整セクション（「衣装を着せる」ボタンが押された後に表示）
            if (showAdjustments) {
                DrawAdjustmentControls();
            }
        }
        
        private void FitCostume() {
            if (avatarObject == null || costumeObject == null) {
                Debug.LogError("アバターと衣装の両方を選択してください");
                return;
            }
            
            // アバターのボーン構造を確認
            Animator avatarAnimator = avatarObject.GetComponent<Animator>();
            if (avatarAnimator == null || !avatarAnimator.isHuman) {
                Debug.LogError("選択されたアバターはHumanoidアバターではありません");
                return;
            }
            
            // 既存の衣装インスタンスがある場合は削除
            if (activeCostumeInstance != null) {
                DestroyImmediate(activeCostumeInstance);
            }
            
            // 新しい衣装インスタンスを作成
            activeCostumeInstance = Instantiate(costumeObject);
            activeCostumeInstance.name = costumeObject.name + "_Instance";
            
            // 衣装の位置をリセットしてからアバターの子に設定
            activeCostumeInstance.transform.position = avatarObject.transform.position;
            activeCostumeInstance.transform.rotation = avatarObject.transform.rotation;
            activeCostumeInstance.transform.SetParent(avatarObject.transform);
            
            // 衣装の位置とスケールをリセット
            activeCostumeInstance.transform.localPosition = Vector3.zero;
            activeCostumeInstance.transform.localRotation = Quaternion.identity;
            
            // 自動スケーリングを適用（オプション）
            if (autoScale) {
                AvatarUtility.AutoScaleCostumeToBones(avatarObject, activeCostumeInstance);
            } else {
                // 衣装のスケールをアバターに合わせて調整
                Vector3 avatarScale = avatarObject.transform.localScale;
                activeCostumeInstance.transform.localScale = new Vector3(
                    1.0f / avatarScale.x,
                    1.0f / avatarScale.y,
                    1.0f / avatarScale.z
                );
            }
            
            // スキンメッシュの転送（ボーンのバインド）
            TransferSkinnedMeshes(avatarAnimator, activeCostumeInstance);
            
            // 初期の微調整値を設定
            SetupAdjustmentValues();
            
            Debug.Log("衣装の適用が完了しました");
        }
        
        private void TransferSkinnedMeshes(Animator avatarAnimator, GameObject costumeInstance) {
            // 衣装のスキンメッシュレンダラーを取得
            SkinnedMeshRenderer[] costumeRenderers = costumeInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            
            foreach (SkinnedMeshRenderer costumeRenderer in costumeRenderers) {
                // コピーを作成して作業する
                SkinnedMeshRenderer rendererCopy = costumeRenderer;
                
                // オリジナルのボーン配列を保存
                Transform[] originalBones = rendererCopy.bones;
                
                // 新しいボーン配列（アバターのボーンに対応）
                Transform[] newBones = new Transform[originalBones.Length];
                
                // 各ボーンについて、アバターの対応するボーンを見つける
                for (int i = 0; i < originalBones.Length; i++) {
                    if (originalBones[i] == null) {
                        newBones[i] = null;
                        continue;
                    }
                    
                    string boneName = originalBones[i].name;
                    
                    // アバターにある同名のボーンを検索 (ユーティリティクラスを使用)
                    Transform avatarBone = AvatarUtility.GetHumanoidBone(avatarAnimator, boneName);
                    if (avatarBone == null) {
                        avatarBone = FindBoneInAvatar(avatarAnimator, boneName);
                    }
                    
                    if (avatarBone != null) {
                        newBones[i] = avatarBone;
                    } else {
                        Debug.LogWarning($"ボーン '{boneName}' がアバターに見つかりませんでした");
                        // 見つからない場合はnullではなく近いボーンを探す
                        HumanBodyBones[] commonBones = {
                            HumanBodyBones.Hips,
                            HumanBodyBones.Spine,
                            HumanBodyBones.Chest,
                            HumanBodyBones.UpperChest
                        };
                        
                        foreach (HumanBodyBones boneType in commonBones) {
                            Transform commonBone = avatarAnimator.GetBoneTransform(boneType);
                            if (commonBone != null) {
                                newBones[i] = commonBone;
                                Debug.Log($"ボーン '{boneName}' の代わりに '{boneType}' を使用します");
                                break;
                            }
                        }
                    }
                }
                
                // 新しいボーン配列を適用
                rendererCopy.bones = newBones;
                
                // ルートボーンをアバターの対応するボーンに設定
                if (rendererCopy.rootBone != null) {
                    string rootBoneName = rendererCopy.rootBone.name;
                    Transform avatarRootBone = AvatarUtility.GetHumanoidBone(avatarAnimator, rootBoneName);
                    if (avatarRootBone == null) {
                        avatarRootBone = FindBoneInAvatar(avatarAnimator, rootBoneName);
                    }
                    
                    if (avatarRootBone != null) {
                        rendererCopy.rootBone = avatarRootBone;
                    } else {
                        // ルートボーンが見つからない場合は、Hipsなど主要なボーンを代わりに使用
                        rendererCopy.rootBone = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
                    }
                }
                
                // オプション：スキンウェイトの最適化
                if (adjustSkinWeights) {
                    AvatarUtility.AdjustSkinWeights(rendererCopy);
                }
                
                // オプション：ブレンドシェイプの作成
                if (createBlendShapes) {
                    AvatarUtility.CreateBasicBlendShapes(rendererCopy);
                }
                
                // バウンディングボックスの更新
                if (rendererCopy.sharedMesh != null) {
                    rendererCopy.sharedMesh.RecalculateBounds();
                }
            }
        }
        
        private Transform FindBoneInAvatar(Animator avatarAnimator, string boneName) {
            // まず、Humanoidボーンとしての検索を試行
            foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                if (boneType == HumanBodyBones.LastBone) continue;
                
                Transform bone = avatarAnimator.GetBoneTransform(boneType);
                if (bone != null && bone.name.Contains(boneName)) {
                    return bone;
                }
            }
            
            // 再帰的にボーン階層から検索
            return SearchBoneRecursively(avatarAnimator.transform, boneName);
        }
        
        private Transform SearchBoneRecursively(Transform parent, string boneName) {
            if (parent.name.Contains(boneName)) {
                return parent;
            }
            
            for (int i = 0; i < parent.childCount; i++) {
                Transform found = SearchBoneRecursively(parent.GetChild(i), boneName);
                if (found != null) {
                    return found;
                }
            }
            
            return null;
        }
        
        private void SetupAdjustmentValues() {
            adjustmentValues.Clear();
            
            // 代表的な部位の調整値を初期化
            adjustmentValues["全体スケール"] = 1.0f;
            adjustmentValues["上半身_X"] = 0.0f;
            adjustmentValues["上半身_Y"] = 0.0f;
            adjustmentValues["上半身_Z"] = 0.0f;
            adjustmentValues["下半身_X"] = 0.0f;
            adjustmentValues["下半身_Y"] = 0.0f;
            adjustmentValues["下半身_Z"] = 0.0f;
            adjustmentValues["左腕_スケール"] = 1.0f;
            adjustmentValues["右腕_スケール"] = 1.0f;
            adjustmentValues["左脚_スケール"] = 1.0f;
            adjustmentValues["右脚_スケール"] = 1.0f;
            
            // 詳細調整用の追加項目
            adjustmentValues["胸部_X"] = 0.0f;
            adjustmentValues["胸部_Y"] = 0.0f;
            adjustmentValues["胸部_Z"] = 0.0f;
            adjustmentValues["腹部_X"] = 0.0f;
            adjustmentValues["腹部_Y"] = 0.0f;
            adjustmentValues["腹部_Z"] = 0.0f;
        }
        
        private void DrawAdjustmentControls() {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("衣装の微調整", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            // 各部位の調整スライダー
            List<string> keys = new List<string>(adjustmentValues.Keys);
            foreach (string key in keys) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(key, GUILayout.Width(100));
                
                // スケールかオフセットかによって調整方法を変える
                float prevValue = adjustmentValues[key];
                if (key.Contains("スケール")) {
                    adjustmentValues[key] = EditorGUILayout.Slider(adjustmentValues[key], 0.5f, 2.0f);
                } else {
                    adjustmentValues[key] = EditorGUILayout.Slider(adjustmentValues[key], -0.5f, 0.5f);
                }
                
                if (GUILayout.Button("リセット", GUILayout.Width(60))) {
                    if (key.Contains("スケール")) {
                        adjustmentValues[key] = 1.0f;
                    } else {
                        adjustmentValues[key] = 0.0f;
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                // 値が変わったら自動的に適用する
                if (prevValue != adjustmentValues[key]) {
                    ApplyAdjustments();
                }
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            
            // 調整を適用するボタン
            if (GUILayout.Button("調整を適用", GUILayout.Height(30))) {
                ApplyAdjustments();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void ApplyAdjustments() {
            if (avatarObject == null || activeCostumeInstance == null) {
                Debug.LogError("アバターまたは衣装が見つかりません");
                return;
            }
            
            Animator avatarAnimator = avatarObject.GetComponent<Animator>();
            if (avatarAnimator == null) return;
            
            // 全体スケールの適用
            float globalScale = adjustmentValues["全体スケール"];
            Vector3 currentScale = activeCostumeInstance.transform.localScale;
            float uniformScale = (currentScale.x + currentScale.y + currentScale.z) / 3.0f;
            float scaleRatio = globalScale / uniformScale;
            activeCostumeInstance.transform.localScale = new Vector3(
                currentScale.x * scaleRatio,
                currentScale.y * scaleRatio,
                currentScale.z * scaleRatio
            );
            
            // アバターのボーンを取得して部位ごとに調整
            Transform upperBody = avatarAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? 
                              avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
            
            Transform lowerBody = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform chest = avatarAnimator.GetBoneTransform(HumanBodyBones.UpperChest) ?? 
                          avatarAnimator.GetBoneTransform(HumanBodyBones.Chest);
            Transform spine = avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
            
            // 上半身の調整
            if (upperBody != null) {
                // 衣装の対応する部分を見つけて調整
                AdjustCostumePart(activeCostumeInstance, upperBody.name, 
                    adjustmentValues["上半身_X"], 
                    adjustmentValues["上半身_Y"], 
                    adjustmentValues["上半身_Z"]);
            }
            
            // 下半身の調整
            if (lowerBody != null) {
                AdjustCostumePart(activeCostumeInstance, lowerBody.name, 
                    adjustmentValues["下半身_X"], 
                    adjustmentValues["下半身_Y"], 
                    adjustmentValues["下半身_Z"]);
            }
            
            // 胸部の調整
            if (chest != null) {
                AdjustCostumePart(activeCostumeInstance, chest.name, 
                    adjustmentValues["胸部_X"], 
                    adjustmentValues["胸部_Y"], 
                    adjustmentValues["胸部_Z"]);
            }
            
            // 腹部の調整
            if (spine != null) {
                AdjustCostumePart(activeCostumeInstance, spine.name, 
                    adjustmentValues["腹部_X"], 
                    adjustmentValues["腹部_Y"], 
                    adjustmentValues["腹部_Z"]);
            }
            
            // 腕と脚のスケール調整
            AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.LeftUpperArm, adjustmentValues["左腕_スケール"]);
            AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.RightUpperArm, adjustmentValues["右腕_スケール"]);
            AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.LeftUpperLeg, adjustmentValues["左脚_スケール"]);
            AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.RightUpperLeg, adjustmentValues["右脚_スケール"]);
            
            // 更新を強制
            EditorUtility.SetDirty(activeCostumeInstance);
            
            Debug.Log("調整が適用されました");
        }
        
        private void AdjustCostumePart(GameObject costumeInstance, string boneName, float offsetX, float offsetY, float offsetZ) {
            // 衣装内の対応するボーンを検索
            Transform targetPart = FindCostumePart(costumeInstance.transform, boneName);
            if (targetPart != null) {
                // オフセットを適用
                SkinnedMeshRenderer renderer = targetPart.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null && renderer.sharedMesh != null) {
                    // メッシュを複製して編集可能にする
                    Mesh sharedMesh = renderer.sharedMesh;
                    Mesh newMesh = Instantiate(sharedMesh);
                    Vector3[] vertices = newMesh.vertices;
                    
                    // 各頂点にオフセットを適用
                    for (int i = 0; i < vertices.Length; i++) {
                        vertices[i] += new Vector3(offsetX, offsetY, offsetZ);
                    }
                    
                    newMesh.vertices = vertices;
                    newMesh.RecalculateBounds();
                    newMesh.RecalculateNormals();
                    
                    // 新しいメッシュを適用
                    renderer.sharedMesh = newMesh;
                }
            }
        }
        
        private Transform FindCostumePart(Transform costumeTransform, string boneName) {
            // 正規化された名前で検索
            string normalizedBoneName = AvatarUtility.NormalizeBoneName(boneName);
            
            // 名前で直接マッチするものを探す
            foreach (Transform child in costumeTransform.GetComponentsInChildren<Transform>()) {
                string normalizedChildName = AvatarUtility.NormalizeBoneName(child.name);
                if (normalizedChildName.Contains(normalizedBoneName) || normalizedBoneName.Contains(normalizedChildName)) {
                    return child;
                }
            }
            
            // SkinnedMeshRendererのボーンを探す
            SkinnedMeshRenderer[] renderers = costumeTransform.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in renderers) {
                for (int i = 0; i < renderer.bones.Length; i++) {
                    if (renderer.bones[i] != null) {
                        string normalizedRendererBoneName = AvatarUtility.NormalizeBoneName(renderer.bones[i].name);
                        if (normalizedRendererBoneName.Contains(normalizedBoneName) || normalizedBoneName.Contains(normalizedRendererBoneName)) {
                            return renderer.transform;
                        }
                    }
                }
            }
            
            // 見つからなかった場合は最初のSkinnedMeshRendererを返す
            if (renderers.Length > 0) {
                return renderers[0].transform;
            }
            
            return null;
        }
        
        private void AdjustLimbScale(Animator avatarAnimator, GameObject costumeInstance, HumanBodyBones boneName, float scale) {
            Transform avatarBone = avatarAnimator.GetBoneTransform(boneName);
            if (avatarBone == null) return;
            
            // 衣装内の対応するボーンを検索
            Transform costumeBone = FindCostumePart(costumeInstance.transform, avatarBone.name);
            if (costumeBone != null) {
                // スケールを適用
                Vector3 currentScale = costumeBone.localScale;
                float avgScale = (currentScale.x + currentScale.y + currentScale.z) / 3.0f;
                float scaleRatio = scale / avgScale;
                
                costumeBone.localScale = new Vector3(
                    currentScale.x * scaleRatio,
                    currentScale.y * scaleRatio,
                    currentScale.z * scaleRatio
                );
                
                // 子のSkinnedMeshRendererも更新
                SkinnedMeshRenderer[] renderers = costumeBone.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (SkinnedMeshRenderer renderer in renderers) {
                    if (renderer.sharedMesh != null) {
                        renderer.sharedMesh.RecalculateBounds();
                    }
                }
            }
        }
    }
}