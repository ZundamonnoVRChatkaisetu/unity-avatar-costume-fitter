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
        private Dictionary<string, float> prevAdjustmentValues = new Dictionary<string, float>();
        private Vector2 scrollPosition;
        private bool showAdjustments = false;
        
        // 詳細設定
        private bool showAdvancedSettings = false;
        private bool createBlendShapes = false;
        private bool adjustSkinWeights = true;
        private bool autoScale = true;
        
        // About情報
        private bool showAboutInfo = false;
        private string aboutInfo = "全アバター衣装自動調整ツール\nVersion 1.1\n\n衣装をアバターに自動的に合わせるツールです。\n\n使い方：\n1. アバターと衣装をドラッグ＆ドロップで選択\n2. 「衣装を着せる」ボタンをクリック\n3. 微調整バーで細かい調整を行う";
        
        // メッシュキャッシュ（リアルタイム調整用）
        private Dictionary<SkinnedMeshRenderer, Mesh> originalMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
        
        // アバターのボーンマッピング
        private Dictionary<string, Transform> avatarBoneMapping;
        
        // エディタ更新時間
        private double lastUpdateTime;
        
        [MenuItem("ずん解/衣装調整ツール")]
        public static void ShowWindow() {
            GetWindow<CostumeFitter>("衣装調整ツール");
        }
        
        private void OnEnable() {
            // ウィンドウがフォーカスを得たときに実行
            wantsMouseMove = true;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            
            // エディタ更新イベントに登録
            EditorApplication.update += OnEditorUpdate;
        }
        
        private void OnDisable() {
            // ウィンドウが閉じられるときの処理
            EditorApplication.update -= OnEditorUpdate;
            
            // 元のメッシュに戻す
            RestoreOriginalMeshes();
        }
        
        private void OnEditorUpdate() {
            // 現在の時間
            double currentTime = EditorApplication.timeSinceStartup;
            
            // 前回の更新から0.1秒経過したら更新
            if (currentTime - lastUpdateTime > 0.1) {
                // 微調整値に変更があれば適用
                if (showAdjustments && HasAdjustmentValuesChanged()) {
                    ApplyAdjustments();
                }
                
                // 時間を更新
                lastUpdateTime = currentTime;
            }
        }
        
        // 微調整値に変更があるかチェック
        private bool HasAdjustmentValuesChanged() {
            foreach (var key in adjustmentValues.Keys) {
                if (!prevAdjustmentValues.ContainsKey(key) || 
                    !Mathf.Approximately(prevAdjustmentValues[key], adjustmentValues[key])) {
                    return true;
                }
            }
            return false;
        }
        
        // 元のメッシュを復元
        private void RestoreOriginalMeshes() {
            foreach (var pair in originalMeshes) {
                if (pair.Key != null && pair.Value != null) {
                    pair.Key.sharedMesh = pair.Value;
                }
            }
            originalMeshes.Clear();
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
            GameObject newAvatarObject = (GameObject)EditorGUILayout.ObjectField(avatarObject, typeof(GameObject), true);
            if (newAvatarObject != avatarObject) {
                avatarObject = newAvatarObject;
                // アバターが変更された場合、既存の衣装を削除
                RemoveCurrentCostume();
                
                // アバターのボーンマッピングを更新
                UpdateAvatarBoneMapping();
            }
            
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
            
            // マウス操作やキーボード操作があった場合、再描画をリクエスト
            if (Event.current.type == EventType.MouseMove || 
                Event.current.type == EventType.KeyDown) {
                Repaint();
            }
        }
        
        // アバターのボーンマッピングを更新
        private void UpdateAvatarBoneMapping() {
            if (avatarObject == null) {
                avatarBoneMapping = null;
                return;
            }
            
            Animator avatarAnimator = avatarObject.GetComponent<Animator>();
            if (avatarAnimator == null || !avatarAnimator.isHuman) {
                avatarBoneMapping = null;
                return;
            }
            
            avatarBoneMapping = AvatarUtility.GetAvatarBoneMapping(avatarAnimator);
        }
        
        // 既存の衣装を削除
        private void RemoveCurrentCostume() {
            if (activeCostumeInstance != null) {
                DestroyImmediate(activeCostumeInstance);
                activeCostumeInstance = null;
                showAdjustments = false;
            }
            
            // オリジナルメッシュを復元
            RestoreOriginalMeshes();
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
            
            // アバターのボーンマッピングを更新
            UpdateAvatarBoneMapping();
            
            // 既存の衣装インスタンスがある場合は削除
            RemoveCurrentCostume();
            
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
            activeCostumeInstance.transform.localScale = Vector3.one;
            
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
            
            // 更新を強制
            EditorUpdateHelper.ForceUpdate(activeCostumeInstance);
            SceneView.RepaintAll();
            
            Debug.Log("衣装の適用が完了しました");
        }
        
        private void TransferSkinnedMeshes(Animator avatarAnimator, GameObject costumeInstance) {
            // 衣装のスキンメッシュレンダラーを取得
            SkinnedMeshRenderer[] costumeRenderers = costumeInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            
            foreach (SkinnedMeshRenderer costumeRenderer in costumeRenderers) {
                // オリジナルのメッシュを保存
                if (costumeRenderer.sharedMesh != null && !originalMeshes.ContainsKey(costumeRenderer)) {
                    originalMeshes[costumeRenderer] = costumeRenderer.sharedMesh;
                    
                    // 編集用のメッシュを複製
                    costumeRenderer.sharedMesh = Instantiate(costumeRenderer.sharedMesh);
                }
                
                // オリジナルのボーン配列を保存
                Transform[] originalBones = costumeRenderer.bones;
                
                // 新しいボーン配列（アバターのボーンに対応）
                Transform[] newBones = new Transform[originalBones.Length];
                
                // 各ボーンについて、アバターの対応するボーンを見つける
                for (int i = 0; i < originalBones.Length; i++) {
                    if (originalBones[i] == null) {
                        newBones[i] = null;
                        continue;
                    }
                    
                    string boneName = originalBones[i].name;
                    
                    // マッピングテーブルから対応するボーンを取得
                    Transform avatarBone = FindCorrespondingAvatarBone(avatarAnimator, boneName, originalBones[i]);
                    
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
                costumeRenderer.bones = newBones;
                
                // ルートボーンをアバターの対応するボーンに設定
                if (costumeRenderer.rootBone != null) {
                    string rootBoneName = costumeRenderer.rootBone.name;
                    Transform avatarRootBone = FindCorrespondingAvatarBone(avatarAnimator, rootBoneName, costumeRenderer.rootBone);
                    
                    if (avatarRootBone != null) {
                        costumeRenderer.rootBone = avatarRootBone;
                    } else {
                        // ルートボーンが見つからない場合は、Hipsなど主要なボーンを代わりに使用
                        costumeRenderer.rootBone = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
                    }
                }
                
                // オプション：スキンウェイトの最適化
                if (adjustSkinWeights) {
                    AvatarUtility.AdjustSkinWeights(costumeRenderer);
                }
                
                // オプション：ブレンドシェイプの作成
                if (createBlendShapes) {
                    AvatarUtility.CreateBasicBlendShapes(costumeRenderer);
                }
                
                // バウンディングボックスの更新
                if (costumeRenderer.sharedMesh != null) {
                    costumeRenderer.sharedMesh.RecalculateBounds();
                }
                
                // 更新を強制
                EditorUpdateHelper.ForceUpdate(costumeRenderer);
            }
        }
        
        // 対応するアバターのボーンを見つける（改良版）
        private Transform FindCorrespondingAvatarBone(Animator avatarAnimator, string boneName, Transform originalBone) {
            // 1. 名前が完全に一致する場合
            if (avatarBoneMapping != null && avatarBoneMapping.TryGetValue(boneName, out Transform exactMatch)) {
                return exactMatch;
            }
            
            // 2. 正規化された名前で検索
            string normalizedName = AvatarUtility.NormalizeBoneName(boneName);
            if (avatarBoneMapping != null && avatarBoneMapping.TryGetValue(normalizedName, out Transform normalizedMatch)) {
                return normalizedMatch;
            }
            
            // 3. ヒューマノイドボーンから推定
            Transform humanoidMatch = AvatarUtility.GetHumanoidBone(avatarAnimator, boneName);
            if (humanoidMatch != null) {
                return humanoidMatch;
            }
            
            // 4. 位置ベースのマッピング（最終手段）
            if (originalBone != null) {
                // ボーンの相対位置を計算
                Vector3 localPos = originalBone.localPosition;
                float distance = float.MaxValue;
                Transform bestMatch = null;
                
                // アバターのすべてのボーンから最も近い位置のものを探す
                foreach (var bone in avatarAnimator.transform.GetComponentsInChildren<Transform>()) {
                    float currentDist = Vector3.Distance(bone.localPosition, localPos);
                    if (currentDist < distance) {
                        distance = currentDist;
                        bestMatch = bone;
                    }
                }
                
                if (bestMatch != null) {
                    return bestMatch;
                }
            }
            
            // それでも見つからない場合は標準的な検索を試す
            return FindBoneInAvatar(avatarAnimator, boneName);
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
            prevAdjustmentValues.Clear();
            
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
            
            // 現在の値を保存
            foreach (var key in adjustmentValues.Keys) {
                prevAdjustmentValues[key] = adjustmentValues[key];
            }
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
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            
            // 調整を適用するボタン
            if (GUILayout.Button("調整を適用", GUILayout.Height(30))) {
                ApplyAdjustmentsWithRebuild();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // 小さな変更用の軽量な調整適用（リアルタイム用）
        private void ApplyAdjustments() {
            if (avatarObject == null || activeCostumeInstance == null) {
                return;
            }
            
            Animator avatarAnimator = avatarObject.GetComponent<Animator>();
            if (avatarAnimator == null) return;
            
            try {
                // 全体スケールの適用
                float globalScale = adjustmentValues["全体スケール"];
                Vector3 currentScale = activeCostumeInstance.transform.localScale;
                float uniformScale = (currentScale.x + currentScale.y + currentScale.z) / 3.0f;
                
                // スケールが0でないことを確認
                if (uniformScale < 0.001f) uniformScale = 1.0f;
                
                float scaleRatio = globalScale / uniformScale;
                activeCostumeInstance.transform.localScale = new Vector3(
                    currentScale.x * scaleRatio,
                    currentScale.y * scaleRatio,
                    currentScale.z * scaleRatio
                );
                
                // アバターのボーンを取得
                Transform upperBody = avatarAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? 
                                   avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
                
                Transform lowerBody = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
                Transform chest = avatarAnimator.GetBoneTransform(HumanBodyBones.UpperChest) ?? 
                               avatarAnimator.GetBoneTransform(HumanBodyBones.Chest);
                Transform spine = avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
                
                // すべてのSkinnedMeshRendererを取得
                SkinnedMeshRenderer[] renderers = activeCostumeInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
                
                // 各ボーンに対応する部分を一時的に修正
                foreach (SkinnedMeshRenderer renderer in renderers) {
                    if (renderer == null || renderer.sharedMesh == null) continue;
                    
                    // 上半身の調整
                    if (upperBody != null) {
                        AvatarUtility.ModifyMeshTemporarily(renderer, 
                            new Vector3(
                                adjustmentValues["上半身_X"] - prevAdjustmentValues["上半身_X"],
                                adjustmentValues["上半身_Y"] - prevAdjustmentValues["上半身_Y"],
                                adjustmentValues["上半身_Z"] - prevAdjustmentValues["上半身_Z"]
                            ), 
                            upperBody.name);
                    }
                    
                    // 下半身の調整
                    if (lowerBody != null) {
                        AvatarUtility.ModifyMeshTemporarily(renderer, 
                            new Vector3(
                                adjustmentValues["下半身_X"] - prevAdjustmentValues["下半身_X"],
                                adjustmentValues["下半身_Y"] - prevAdjustmentValues["下半身_Y"],
                                adjustmentValues["下半身_Z"] - prevAdjustmentValues["下半身_Z"]
                            ), 
                            lowerBody.name);
                    }
                    
                    // 胸部の調整
                    if (chest != null) {
                        AvatarUtility.ModifyMeshTemporarily(renderer, 
                            new Vector3(
                                adjustmentValues["胸部_X"] - prevAdjustmentValues["胸部_X"],
                                adjustmentValues["胸部_Y"] - prevAdjustmentValues["胸部_Y"],
                                adjustmentValues["胸部_Z"] - prevAdjustmentValues["胸部_Z"]
                            ), 
                            chest.name);
                    }
                    
                    // 腹部の調整
                    if (spine != null) {
                        AvatarUtility.ModifyMeshTemporarily(renderer, 
                            new Vector3(
                                adjustmentValues["腹部_X"] - prevAdjustmentValues["腹部_X"],
                                adjustmentValues["腹部_Y"] - prevAdjustmentValues["腹部_Y"],
                                adjustmentValues["腹部_Z"] - prevAdjustmentValues["腹部_Z"]
                            ), 
                            spine.name);
                    }
                    
                    // メッシュを更新
                    renderer.sharedMesh.RecalculateBounds();
                }
                
                // 腕と脚のスケール調整
                AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.LeftUpperArm, adjustmentValues["左腕_スケール"]);
                AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.RightUpperArm, adjustmentValues["右腕_スケール"]);
                AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.LeftUpperLeg, adjustmentValues["左脚_スケール"]);
                AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.RightUpperLeg, adjustmentValues["右脚_スケール"]);
                
                // 現在の値を保存
                foreach (var key in adjustmentValues.Keys) {
                    prevAdjustmentValues[key] = adjustmentValues[key];
                }
                
                // 強制的に更新
                SceneView.RepaintAll();
            }
            catch (System.Exception e) {
                Debug.LogError($"調整の適用中にエラーが発生しました: {e.Message}");
            }
        }
        
        // 完全な再構築を伴う調整適用（ボタン押下時用）
        private void ApplyAdjustmentsWithRebuild() {
            if (avatarObject == null || activeCostumeInstance == null) {
                Debug.LogError("アバターまたは衣装が見つかりません");
                return;
            }
            
            // オリジナルのメッシュに戻す
            RestoreOriginalMeshes();
            
            Animator avatarAnimator = avatarObject.GetComponent<Animator>();
            if (avatarAnimator == null) return;
            
            // 全体スケールの適用
            float globalScale = adjustmentValues["全体スケール"];
            activeCostumeInstance.transform.localScale = new Vector3(globalScale, globalScale, globalScale);
            
            // アバターのボーンを取得して部位ごとに調整
            Transform upperBody = avatarAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? 
                               avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
            
            Transform lowerBody = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform chest = avatarAnimator.GetBoneTransform(HumanBodyBones.UpperChest) ?? 
                           avatarAnimator.GetBoneTransform(HumanBodyBones.Chest);
            Transform spine = avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
            
            // 衣装のすべてのSkinnedMeshRenderer
            SkinnedMeshRenderer[] renderers = activeCostumeInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (SkinnedMeshRenderer renderer in renderers) {
                if (renderer == null || renderer.sharedMesh == null) continue;
                
                // メッシュを複製して編集可能にする
                renderer.sharedMesh = Instantiate(renderer.sharedMesh);
                
                // 上半身の調整
                if (upperBody != null) {
                    AdjustCostumePart(renderer, upperBody.name, 
                        adjustmentValues["上半身_X"], 
                        adjustmentValues["上半身_Y"], 
                        adjustmentValues["上半身_Z"]);
                }
                
                // 下半身の調整
                if (lowerBody != null) {
                    AdjustCostumePart(renderer, lowerBody.name, 
                        adjustmentValues["下半身_X"], 
                        adjustmentValues["下半身_Y"], 
                        adjustmentValues["下半身_Z"]);
                }
                
                // 胸部の調整
                if (chest != null) {
                    AdjustCostumePart(renderer, chest.name, 
                        adjustmentValues["胸部_X"], 
                        adjustmentValues["胸部_Y"], 
                        adjustmentValues["胸部_Z"]);
                }
                
                // 腹部の調整
                if (spine != null) {
                    AdjustCostumePart(renderer, spine.name, 
                        adjustmentValues["腹部_X"], 
                        adjustmentValues["腹部_Y"], 
                        adjustmentValues["腹部_Z"]);
                }
                
                // バウンディングボックスを更新
                renderer.sharedMesh.RecalculateBounds();
                renderer.sharedMesh.RecalculateNormals();
            }
            
            // 腕と脚のスケール調整
            AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.LeftUpperArm, adjustmentValues["左腕_スケール"]);
            AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.RightUpperArm, adjustmentValues["右腕_スケール"]);
            AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.LeftUpperLeg, adjustmentValues["左脚_スケール"]);
            AdjustLimbScale(avatarAnimator, activeCostumeInstance, HumanBodyBones.RightUpperLeg, adjustmentValues["右脚_スケール"]);
            
            // 現在の値を保存
            foreach (var key in adjustmentValues.Keys) {
                prevAdjustmentValues[key] = adjustmentValues[key];
            }
            
            // オリジナルのメッシュリストをクリア（新しいメッシュを保存するため）
            originalMeshes.Clear();
            
            // 更新を強制
            EditorUpdateHelper.ForceUpdate(activeCostumeInstance);
            SceneView.RepaintAll();
            
            Debug.Log("調整を完全に適用しました");
        }
        
        private void AdjustCostumePart(SkinnedMeshRenderer renderer, string boneName, float offsetX, float offsetY, float offsetZ) {
            if (renderer == null || renderer.sharedMesh == null) return;
            
            // ボーンIDを特定
            int targetBoneIndex = -1;
            string normalizedBoneName = AvatarUtility.NormalizeBoneName(boneName).ToLowerInvariant();
            
            for (int i = 0; i < renderer.bones.Length; i++) {
                if (renderer.bones[i] != null) {
                    string currentBoneName = AvatarUtility.NormalizeBoneName(renderer.bones[i].name).ToLowerInvariant();
                    if (currentBoneName.Contains(normalizedBoneName) || normalizedBoneName.Contains(currentBoneName)) {
                        targetBoneIndex = i;
                        break;
                    }
                }
            }
            
            // メッシュを編集
            Vector3[] vertices = renderer.sharedMesh.vertices;
            BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            
            if (targetBoneIndex >= 0 && weights.Length == vertices.Length) {
                // 特定のボーンに関連する頂点だけを変更
                for (int i = 0; i < vertices.Length; i++) {
                    BoneWeight weight = weights[i];
                    float influence = 0f;
                    
                    // 各ボーンのウェイトをチェック
                    if (weight.boneIndex0 == targetBoneIndex) influence += weight.weight0;
                    if (weight.boneIndex1 == targetBoneIndex) influence += weight.weight1;
                    if (weight.boneIndex2 == targetBoneIndex) influence += weight.weight2;
                    if (weight.boneIndex3 == targetBoneIndex) influence += weight.weight3;
                    
                    // ウェイトがある頂点のみオフセット
                    if (influence > 0.01f) {
                        vertices[i] += new Vector3(offsetX, offsetY, offsetZ) * influence;
                    }
                }
            } else {
                // ボーンが見つからない場合は、全ての頂点に小さなオフセットを適用
                for (int i = 0; i < vertices.Length; i++) {
                    vertices[i] += new Vector3(offsetX, offsetY, offsetZ) * 0.1f;
                }
            }
            
            // 修正した頂点を適用
            renderer.sharedMesh.vertices = vertices;
            renderer.sharedMesh.RecalculateBounds();
        }
        
        private Transform FindChildByName(Transform parent, string name) {
            if (parent.name.Contains(name)) {
                return parent;
            }
            
            for (int i = 0; i < parent.childCount; i++) {
                Transform child = parent.GetChild(i);
                Transform result = FindChildByName(child, name);
                if (result != null) {
                    return result;
                }
            }
            
            return null;
        }
        
        private void AdjustLimbScale(Animator avatarAnimator, GameObject costumeInstance, HumanBodyBones boneName, float scale) {
            Transform avatarBone = avatarAnimator.GetBoneTransform(boneName);
            if (avatarBone == null) return;
            
            // 衣装内の対応するボーンを検索
            string normalizedName = AvatarUtility.NormalizeBoneName(avatarBone.name).ToLowerInvariant();
            Transform costumeBone = null;
            
            // 階層から探す
            foreach (Transform child in costumeInstance.GetComponentsInChildren<Transform>()) {
                string childName = AvatarUtility.NormalizeBoneName(child.name).ToLowerInvariant();
                if (childName.Contains(normalizedName) || normalizedName.Contains(childName)) {
                    costumeBone = child;
                    break;
                }
            }
            
            // SkinnedMeshRendererのボーンからも探す
            if (costumeBone == null) {
                SkinnedMeshRenderer[] renderers = costumeInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (SkinnedMeshRenderer renderer in renderers) {
                    for (int i = 0; i < renderer.bones.Length; i++) {
                        if (renderer.bones[i] != null) {
                            string boneName2 = AvatarUtility.NormalizeBoneName(renderer.bones[i].name).ToLowerInvariant();
                            if (boneName2.Contains(normalizedName) || normalizedName.Contains(boneName2)) {
                                costumeBone = renderer.bones[i];
                                break;
                            }
                        }
                    }
                    if (costumeBone != null) break;
                }
            }
            
            // 発見したボーンにスケールを適用
            if (costumeBone != null) {
                costumeBone.localScale = new Vector3(scale, scale, scale);
                
                // 更新を強制
                EditorUpdateHelper.ForceUpdate(costumeBone);
            }
        }
    }
}