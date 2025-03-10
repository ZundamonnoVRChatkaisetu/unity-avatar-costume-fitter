using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    public class CostumeFitter : EditorWindow {
        // アバターとコスチュームの参照
        private GameObject avatarObject;
        private GameObject costumeObject;
        
        // 微調整用の設定
        private Dictionary<string, float> adjustmentValues = new Dictionary<string, float>();
        private Vector2 scrollPosition;
        private bool showAdjustments = false;
        
        [MenuItem("ずん解/衣装調整ツール")]
        public static void ShowWindow() {
            GetWindow<CostumeFitter>("衣装調整ツール");
        }
        
        private void OnGUI() {
            // ヘッダー
            GUILayout.Label("全アバター衣装自動調整ツール", EditorStyles.boldLabel);
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
            
            // 衣装のコピーを作成し、アバターの子オブジェクトとして配置
            GameObject costumeInstance = Instantiate(costumeObject);
            costumeInstance.name = costumeObject.name + "_Instance";
            costumeInstance.transform.SetParent(avatarObject.transform);
            
            // 衣装の位置とスケールをリセット
            costumeInstance.transform.localPosition = Vector3.zero;
            costumeInstance.transform.localRotation = Quaternion.identity;
            
            // 衣装のスケールをアバターに合わせて調整
            Vector3 avatarScale = avatarObject.transform.localScale;
            costumeInstance.transform.localScale = new Vector3(
                costumeInstance.transform.localScale.x / avatarScale.x,
                costumeInstance.transform.localScale.y / avatarScale.y,
                costumeInstance.transform.localScale.z / avatarScale.z
            );
            
            // スキンメッシュの転送（ボーンのバインド）
            TransferSkinnedMeshes(avatarAnimator, costumeInstance);
            
            // 初期の微調整値を設定
            SetupAdjustmentValues();
            
            Debug.Log("衣装の適用が完了しました");
        }
        
        private void TransferSkinnedMeshes(Animator avatarAnimator, GameObject costumeInstance) {
            // 衣装のスキンメッシュレンダラーを取得
            SkinnedMeshRenderer[] costumeRenderers = costumeInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            
            foreach (SkinnedMeshRenderer costumeRenderer in costumeRenderers) {
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
                    
                    // アバターにある同名のボーンを検索
                    Transform avatarBone = FindBoneInAvatar(avatarAnimator, boneName);
                    if (avatarBone != null) {
                        newBones[i] = avatarBone;
                    } else {
                        Debug.LogWarning($"ボーン '{boneName}' がアバターに見つかりませんでした");
                        newBones[i] = null;
                    }
                }
                
                // 新しいボーン配列を適用
                costumeRenderer.bones = newBones;
                
                // ルートボーンをアバターの対応するボーンに設定
                if (costumeRenderer.rootBone != null) {
                    string rootBoneName = costumeRenderer.rootBone.name;
                    Transform avatarRootBone = FindBoneInAvatar(avatarAnimator, rootBoneName);
                    if (avatarRootBone != null) {
                        costumeRenderer.rootBone = avatarRootBone;
                    } else {
                        // ルートボーンが見つからない場合は、Hipsなど主要なボーンを代わりに使用
                        costumeRenderer.rootBone = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
                    }
                }
            }
        }
        
        private Transform FindBoneInAvatar(Animator avatarAnimator, string boneName) {
            // まず、Humanoidボーンとしての検索を試行
            foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                if (boneType == HumanBodyBones.LastBone) continue;
                
                Transform bone = avatarAnimator.GetBoneTransform(boneType);
                if (bone != null && bone.name == boneName) {
                    return bone;
                }
            }
            
            // 再帰的にボーン階層から検索
            return SearchBoneRecursively(avatarAnimator.transform, boneName);
        }
        
        private Transform SearchBoneRecursively(Transform parent, string boneName) {
            if (parent.name == boneName) {
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
                ApplyAdjustments();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void ApplyAdjustments() {
            if (avatarObject == null) return;
            
            // 適用した衣装のインスタンスを探す
            Transform costumeTransform = null;
            foreach (Transform child in avatarObject.transform) {
                if (child.name.Contains(costumeObject.name)) {
                    costumeTransform = child;
                    break;
                }
            }
            
            if (costumeTransform == null) {
                Debug.LogError("適用済みの衣装が見つかりません");
                return;
            }
            
            Animator avatarAnimator = avatarObject.GetComponent<Animator>();
            if (avatarAnimator == null) return;
            
            // 全体スケールの適用
            float globalScale = adjustmentValues["全体スケール"];
            costumeTransform.localScale = new Vector3(globalScale, globalScale, globalScale);
            
            // アバターのボーンを取得して部位ごとに調整
            Transform upperBody = avatarAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? 
                              avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
            
            Transform lowerBody = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
            
            // 上半身の調整
            if (upperBody != null) {
                // 衣装の対応する部分を見つけて調整
                AdjustCostumePart(costumeTransform, upperBody.name, 
                    adjustmentValues["上半身_X"], 
                    adjustmentValues["上半身_Y"], 
                    adjustmentValues["上半身_Z"]);
            }
            
            // 下半身の調整
            if (lowerBody != null) {
                AdjustCostumePart(costumeTransform, lowerBody.name, 
                    adjustmentValues["下半身_X"], 
                    adjustmentValues["下半身_Y"], 
                    adjustmentValues["下半身_Z"]);
            }
            
            // 腕と脚のスケール調整
            AdjustLimbScale(avatarAnimator, costumeTransform, HumanBodyBones.LeftUpperArm, adjustmentValues["左腕_スケール"]);
            AdjustLimbScale(avatarAnimator, costumeTransform, HumanBodyBones.RightUpperArm, adjustmentValues["右腕_スケール"]);
            AdjustLimbScale(avatarAnimator, costumeTransform, HumanBodyBones.LeftUpperLeg, adjustmentValues["左脚_スケール"]);
            AdjustLimbScale(avatarAnimator, costumeTransform, HumanBodyBones.RightUpperLeg, adjustmentValues["右脚_スケール"]);
            
            Debug.Log("調整が適用されました");
        }
        
        private void AdjustCostumePart(Transform costumeTransform, string boneName, float offsetX, float offsetY, float offsetZ) {
            // 衣装内の対応するボーンを検索
            Transform targetPart = FindCostumePart(costumeTransform, boneName);
            if (targetPart != null) {
                // オフセットを適用
                SkinnedMeshRenderer renderer = targetPart.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null) {
                    Mesh mesh = renderer.sharedMesh;
                    Vector3[] vertices = mesh.vertices;
                    
                    // 各頂点にオフセットを適用
                    for (int i = 0; i < vertices.Length; i++) {
                        vertices[i] += new Vector3(offsetX, offsetY, offsetZ);
                    }
                    
                    mesh.vertices = vertices;
                    mesh.RecalculateBounds();
                }
            }
        }
        
        private Transform FindCostumePart(Transform costumeTransform, string boneName) {
            // 名前で直接マッチするものを探す
            foreach (Transform child in costumeTransform.GetComponentsInChildren<Transform>()) {
                if (child.name.Contains(boneName)) {
                    return child;
                }
            }
            
            // SkinnedMeshRendererのボーンを探す
            SkinnedMeshRenderer[] renderers = costumeTransform.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in renderers) {
                for (int i = 0; i < renderer.bones.Length; i++) {
                    if (renderer.bones[i] != null && renderer.bones[i].name.Contains(boneName)) {
                        return renderer.transform;
                    }
                }
            }
            
            return null;
        }
        
        private void AdjustLimbScale(Animator avatarAnimator, Transform costumeTransform, HumanBodyBones boneName, float scale) {
            Transform avatarBone = avatarAnimator.GetBoneTransform(boneName);
            if (avatarBone == null) return;
            
            // 衣装内の対応するボーンを検索
            Transform costumeBone = FindCostumePart(costumeTransform, avatarBone.name);
            if (costumeBone != null) {
                // スケールを適用
                costumeBone.localScale = new Vector3(scale, scale, scale);
            }
        }
    }
}