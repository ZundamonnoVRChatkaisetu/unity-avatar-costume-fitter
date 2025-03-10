using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    /// <summary>
    /// アバターと衣装のボーン問題を自動的に検出・修正するクラス
    /// </summary>
    public class BoneAutoFixer : EditorWindow {
        // 表示対象のオブジェクト
        private GameObject avatarObject;
        private GameObject costumeObject;
        
        // スクロール位置
        private Vector2 scrollPosition;
        
        // 検出された問題のリスト
        private List<BoneProblem> detectedProblems = new List<BoneProblem>();
        
        // 問題表示フィルター
        private bool showMissingBones = true;
        private bool showScalingIssues = true;
        private bool showRotationIssues = true;
        private bool showNamingIssues = true;
        
        // 実行中の修正
        private bool isFixing = false;
        private int currentFixIndex = 0;
        private float fixProgress = 0f;
        
        /// <summary>
        /// ボーンの問題を表すクラス
        /// </summary>
        private class BoneProblem {
            public enum ProblemType {
                MissingBone,      // ボーンが見つからない
                ScalingIssue,     // スケール問題
                RotationIssue,    // 回転問題
                NamingIssue       // 名前の問題
            }
            
            public ProblemType type;
            public string description;
            public Transform problemBone;
            public Transform referenceBone;
            public bool canAutoFix;
            public System.Action fixAction;
            
            public BoneProblem(ProblemType type, string description, Transform problemBone, Transform referenceBone = null) {
                this.type = type;
                this.description = description;
                this.problemBone = problemBone;
                this.referenceBone = referenceBone;
                this.canAutoFix = false;
                this.fixAction = null;
            }
            
            public BoneProblem(ProblemType type, string description, Transform problemBone, Transform referenceBone, System.Action fixAction) {
                this.type = type;
                this.description = description;
                this.problemBone = problemBone;
                this.referenceBone = referenceBone;
                this.canAutoFix = true;
                this.fixAction = fixAction;
            }
        }
        
        [MenuItem("ずん解/ボーン自動修正")]
        public static void ShowWindow() {
            GetWindow<BoneAutoFixer>("ボーン自動修正");
        }
        
        private void OnGUI() {
            EditorGUILayout.BeginVertical();
            
            // ヘッダー
            GUILayout.Label("ボーン自動修正", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // 対象オブジェクトの選択
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("アバター", GUILayout.Width(80));
            GameObject newAvatar = (GameObject)EditorGUILayout.ObjectField(avatarObject, typeof(GameObject), true);
            if (newAvatar != avatarObject) {
                avatarObject = newAvatar;
                detectedProblems.Clear(); // リセット
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("衣装", GUILayout.Width(80));
            GameObject newCostume = (GameObject)EditorGUILayout.ObjectField(costumeObject, typeof(GameObject), true);
            if (newCostume != costumeObject) {
                costumeObject = newCostume;
                detectedProblems.Clear(); // リセット
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 問題検出ボタン
            EditorGUI.BeginDisabledGroup(avatarObject == null || costumeObject == null || isFixing);
            if (GUILayout.Button("問題を検出", GUILayout.Height(30))) {
                DetectProblems();
            }
            EditorGUI.EndDisabledGroup();
            
            // フィルター設定
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("表示フィルター:", GUILayout.Width(100));
            showMissingBones = EditorGUILayout.ToggleLeft("不足ボーン", showMissingBones, GUILayout.Width(100));
            showScalingIssues = EditorGUILayout.ToggleLeft("スケール問題", showScalingIssues, GUILayout.Width(100));
            showRotationIssues = EditorGUILayout.ToggleLeft("回転問題", showRotationIssues, GUILayout.Width(100));
            showNamingIssues = EditorGUILayout.ToggleLeft("名前問題", showNamingIssues, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 問題リストの表示
            if (detectedProblems.Count > 0) {
                EditorGUILayout.BeginVertical("box");
                
                // 件数表示
                int visibleCount = CountVisibleProblems();
                EditorGUILayout.LabelField($"検出された問題: {visibleCount} / {detectedProblems.Count}件", EditorStyles.boldLabel);
                
                // 自動修正ボタン
                EditorGUI.BeginDisabledGroup(isFixing);
                if (GUILayout.Button("すべての問題を自動修正", GUILayout.Height(30))) {
                    StartAutoFix();
                }
                EditorGUI.EndDisabledGroup();
                
                // 進捗バー（修正中のみ表示）
                if (isFixing) {
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), fixProgress, $"修正中... ({currentFixIndex + 1}/{CountFixableProblems()})");
                }
                
                EditorGUILayout.Space();
                
                // 問題リスト
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                
                for (int i = 0; i < detectedProblems.Count; i++) {
                    BoneProblem problem = detectedProblems[i];
                    
                    // フィルターに基づいて表示/非表示
                    if (!ShouldShowProblem(problem)) {
                        continue;
                    }
                    
                    EditorGUILayout.BeginVertical("box");
                    
                    // 問題タイプに応じた色
                    Color typeColor = GetProblemTypeColor(problem.type);
                    GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
                    titleStyle.normal.textColor = typeColor;
                    
                    // 問題タイプ
                    EditorGUILayout.LabelField(GetProblemTypeString(problem.type), titleStyle);
                    
                    // 説明
                    EditorGUILayout.LabelField(problem.description, EditorStyles.wordWrappedLabel);
                    
                    // ボーン情報
                    if (problem.problemBone != null) {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("問題ボーン:", GUILayout.Width(80));
                        EditorGUILayout.LabelField(problem.problemBone.name);
                        if (GUILayout.Button("選択", GUILayout.Width(50))) {
                            Selection.activeGameObject = problem.problemBone.gameObject;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    if (problem.referenceBone != null) {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("参照ボーン:", GUILayout.Width(80));
                        EditorGUILayout.LabelField(problem.referenceBone.name);
                        if (GUILayout.Button("選択", GUILayout.Width(50))) {
                            Selection.activeGameObject = problem.referenceBone.gameObject;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    // 修正ボタン
                    EditorGUI.BeginDisabledGroup(!problem.canAutoFix || isFixing);
                    if (GUILayout.Button("この問題を修正")) {
                        FixProblem(problem);
                        DetectProblems(); // 問題リストを更新
                    }
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.EndVertical();
            } else if (avatarObject != null && costumeObject != null) {
                EditorGUILayout.HelpBox("問題は検出されていません。「問題を検出」ボタンを押すと、ボーンの問題を自動的に検出します。", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // 問題を検出
        private void DetectProblems() {
            if (avatarObject == null || costumeObject == null) return;
            
            detectedProblems.Clear();
            
            // アニメーターの取得
            Animator avatarAnimator = avatarObject.GetComponent<Animator>();
            Animator costumeAnimator = costumeObject.GetComponent<Animator>();
            
            if (avatarAnimator == null || !avatarAnimator.isHuman) {
                EditorUtility.DisplayDialog("エラー", "アバターオブジェクトはHumanoidモデルではありません。", "OK");
                return;
            }
            
            // 1. 不足ボーンの検出
            DetectMissingBones(avatarAnimator, costumeObject);
            
            // 2. スケール問題の検出
            DetectScalingIssues(avatarAnimator, costumeObject);
            
            // 3. 回転問題の検出
            DetectRotationIssues(avatarAnimator, costumeObject);
            
            // 4. 名前問題の検出
            DetectNamingIssues(avatarAnimator, costumeObject);
            
            // プログレスバーをクリア
            EditorUtility.ClearProgressBar();
        }
        
        // 不足ボーンの検出
        private void DetectMissingBones(Animator avatarAnimator, GameObject costumeObject) {
            // VRChatアバターに必要な主要ボーンリスト
            HumanBodyBones[] essentialBones = new HumanBodyBones[] {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.Neck,
                HumanBodyBones.Head,
                HumanBodyBones.LeftShoulder,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                HumanBodyBones.RightShoulder,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot
            };
            
            // 衣装内のすべてのボーンを取得
            Transform[] costumeBones = costumeObject.GetComponentsInChildren<Transform>(true);
            
            foreach (HumanBodyBones boneType in essentialBones) {
                Transform avatarBone = avatarAnimator.GetBoneTransform(boneType);
                if (avatarBone == null) continue;
                
                // アバターのボーン名を正規化
                string normalizedAvatarBoneName = AvatarUtility.NormalizeBoneName(avatarBone.name).ToLowerInvariant();
                
                bool foundInCostume = false;
                foreach (Transform costumeBone in costumeBones) {
                    string normalizedCostumeBoneName = AvatarUtility.NormalizeBoneName(costumeBone.name).ToLowerInvariant();
                    
                    // 名前が一致するか、含まれているかをチェック
                    if (normalizedCostumeBoneName == normalizedAvatarBoneName ||
                        normalizedCostumeBoneName.Contains(normalizedAvatarBoneName) ||
                        normalizedAvatarBoneName.Contains(normalizedCostumeBoneName)) {
                        foundInCostume = true;
                        break;
                    }
                }
                
                if (!foundInCostume) {
                    // 問題: 衣装に必要なボーンがない
                    BoneProblem problem = new BoneProblem(
                        BoneProblem.ProblemType.MissingBone,
                        $"衣装に必要なボーン '{boneType}' ({avatarBone.name}) が見つかりません。",
                        avatarBone
                    );
                    
                    // 修正: 衣装に同等のボーンを作成
                    problem.canAutoFix = true;
                    problem.fixAction = () => {
                        // 親ボーンを特定
                        Transform parentBone = null;
                        
                        // ボーンの階層関係に基づいて親を探す
                        switch (boneType) {
                            case HumanBodyBones.Spine:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.Hips));
                                break;
                            case HumanBodyBones.Chest:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.Spine));
                                break;
                            case HumanBodyBones.UpperChest:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.Chest));
                                break;
                            case HumanBodyBones.Neck:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.Chest)) ??
                                            FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.UpperChest));
                                break;
                            case HumanBodyBones.Head:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.Neck));
                                break;
                            case HumanBodyBones.LeftShoulder:
                            case HumanBodyBones.RightShoulder:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.Chest)) ??
                                            FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.UpperChest));
                                break;
                            case HumanBodyBones.LeftUpperArm:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder));
                                break;
                            case HumanBodyBones.RightUpperArm:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.RightShoulder));
                                break;
                            case HumanBodyBones.LeftLowerArm:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm));
                                break;
                            case HumanBodyBones.RightLowerArm:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm));
                                break;
                            case HumanBodyBones.LeftHand:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
                                break;
                            case HumanBodyBones.RightHand:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm));
                                break;
                            case HumanBodyBones.LeftUpperLeg:
                            case HumanBodyBones.RightUpperLeg:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.Hips));
                                break;
                            case HumanBodyBones.LeftLowerLeg:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg));
                                break;
                            case HumanBodyBones.RightLowerLeg:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg));
                                break;
                            case HumanBodyBones.LeftFoot:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg));
                                break;
                            case HumanBodyBones.RightFoot:
                                parentBone = FindBoneInCostume(costumeBones, avatarAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg));
                                break;
                        }
                        
                        if (parentBone != null) {
                            // 新しいボーンを作成
                            GameObject newBone = new GameObject(avatarBone.name);
                            newBone.transform.SetParent(parentBone);
                            
                            // アバターのボーンの位置を基にローカル位置を設定
                            Vector3 localPos = avatarBone.localPosition;
                            newBone.transform.localPosition = localPos;
                            newBone.transform.localRotation = avatarBone.localRotation;
                            newBone.transform.localScale = avatarBone.localScale;
                            
                            EditorUtility.DisplayDialog("修正完了", $"衣装に '{boneType}' ({avatarBone.name}) ボーンを作成しました。", "OK");
                        } else {
                            EditorUtility.DisplayDialog("修正失敗", $"'{boneType}' ボーンの親ボーンが見つからないため、修正できませんでした。", "OK");
                        }
                    };
                    
                    detectedProblems.Add(problem);
                }
            }
        }
        
        // 衣装内で対応するボーンを探す
        private Transform FindBoneInCostume(Transform[] costumeBones, Transform avatarBone) {
            if (avatarBone == null) return null;
            
            string normalizedAvatarBoneName = AvatarUtility.NormalizeBoneName(avatarBone.name).ToLowerInvariant();
            
            foreach (Transform costumeBone in costumeBones) {
                string normalizedCostumeBoneName = AvatarUtility.NormalizeBoneName(costumeBone.name).ToLowerInvariant();
                
                if (normalizedCostumeBoneName == normalizedAvatarBoneName ||
                    normalizedCostumeBoneName.Contains(normalizedAvatarBoneName) ||
                    normalizedAvatarBoneName.Contains(normalizedCostumeBoneName)) {
                    return costumeBone;
                }
            }
            
            return null;
        }
        
        // スケール問題の検出
        private void DetectScalingIssues(Animator avatarAnimator, GameObject costumeObject) {
            // 衣装内のすべてのボーンを取得
            Transform[] costumeBones = costumeObject.GetComponentsInChildren<Transform>(true);
            
            foreach (Transform costumeBone in costumeBones) {
                // 非常に大きなスケール値や非常に小さなスケール値を検出
                if (costumeBone.localScale.x > 5f || costumeBone.localScale.y > 5f || costumeBone.localScale.z > 5f ||
                    costumeBone.localScale.x < 0.1f || costumeBone.localScale.y < 0.1f || costumeBone.localScale.z < 0.1f) {
                    
                    // 対応するアバターのボーンを探す
                    Transform avatarBone = FindCorrespondingBone(avatarAnimator, costumeBone);
                    
                    BoneProblem problem = new BoneProblem(
                        BoneProblem.ProblemType.ScalingIssue,
                        $"ボーン '{costumeBone.name}' のスケールが異常です: {costumeBone.localScale}",
                        costumeBone,
                        avatarBone
                    );
                    
                    if (avatarBone != null) {
                        problem.canAutoFix = true;
                        problem.fixAction = () => {
                            // アバターのスケールに合わせる
                            costumeBone.localScale = avatarBone.localScale;
                            EditorUtility.SetDirty(costumeBone);
                            EditorUtility.DisplayDialog("修正完了", $"ボーン '{costumeBone.name}' のスケールを {avatarBone.localScale} に修正しました。", "OK");
                        };
                    } else {
                        problem.canAutoFix = true;
                        problem.fixAction = () => {
                            // 標準的なスケールに戻す
                            costumeBone.localScale = Vector3.one;
                            EditorUtility.SetDirty(costumeBone);
                            EditorUtility.DisplayDialog("修正完了", $"ボーン '{costumeBone.name}' のスケールを (1, 1, 1) に修正しました。", "OK");
                        };
                    }
                    
                    detectedProblems.Add(problem);
                }
                
                // 不均一なスケール値を検出
                if (Mathf.Abs(costumeBone.localScale.x - costumeBone.localScale.y) > 0.5f ||
                    Mathf.Abs(costumeBone.localScale.y - costumeBone.localScale.z) > 0.5f ||
                    Mathf.Abs(costumeBone.localScale.z - costumeBone.localScale.x) > 0.5f) {
                    
                    BoneProblem problem = new BoneProblem(
                        BoneProblem.ProblemType.ScalingIssue,
                        $"ボーン '{costumeBone.name}' のスケールが不均一です: {costumeBone.localScale}",
                        costumeBone
                    );
                    
                    problem.canAutoFix = true;
                    problem.fixAction = () => {
                        // 最大値で統一
                        float maxScale = Mathf.Max(costumeBone.localScale.x, costumeBone.localScale.y, costumeBone.localScale.z);
                        costumeBone.localScale = new Vector3(maxScale, maxScale, maxScale);
                        EditorUtility.SetDirty(costumeBone);
                        EditorUtility.DisplayDialog("修正完了", $"ボーン '{costumeBone.name}' のスケールを均一 ({maxScale}, {maxScale}, {maxScale}) に修正しました。", "OK");
                    };
                    
                    detectedProblems.Add(problem);
                }
            }
        }
        
        // 回転問題の検出
        private void DetectRotationIssues(Animator avatarAnimator, GameObject costumeObject) {
            // 衣装内のすべてのボーンを取得
            Transform[] costumeBones = costumeObject.GetComponentsInChildren<Transform>(true);
            
            foreach (Transform costumeBone in costumeBones) {
                // 対応するアバターのボーンを探す
                Transform avatarBone = FindCorrespondingBone(avatarAnimator, costumeBone);
                
                if (avatarBone != null) {
                    // 回転の差が大きい場合に問題として検出
                    Quaternion rotationDifference = Quaternion.Inverse(costumeBone.localRotation) * avatarBone.localRotation;
                    float angle = Quaternion.Angle(Quaternion.identity, rotationDifference);
                    
                    if (angle > 30f) {
                        BoneProblem problem = new BoneProblem(
                            BoneProblem.ProblemType.RotationIssue,
                            $"ボーン '{costumeBone.name}' の回転がアバターのボーン '{avatarBone.name}' と大きく異なります（差: {angle:F1}度）",
                            costumeBone,
                            avatarBone
                        );
                        
                        problem.canAutoFix = true;
                        problem.fixAction = () => {
                            // アバターの回転に合わせる
                            costumeBone.localRotation = avatarBone.localRotation;
                            EditorUtility.SetDirty(costumeBone);
                            EditorUtility.DisplayDialog("修正完了", $"ボーン '{costumeBone.name}' の回転をアバターのボーンに合わせて修正しました。", "OK");
                        };
                        
                        detectedProblems.Add(problem);
                    }
                }
            }
        }
        
        // 名前問題の検出
        private void DetectNamingIssues(Animator avatarAnimator, GameObject costumeObject) {
            // 衣装内のすべてのボーンを取得
            Transform[] costumeBones = costumeObject.GetComponentsInChildren<Transform>(true);
            
            // VRChatアバターに通常使われる標準的なボーン名パターン
            Dictionary<string, string> standardBoneNames = new Dictionary<string, string>() {
                { "hip", "Hips" },
                { "spine", "Spine" },
                { "chest", "Chest" },
                { "neck", "Neck" },
                { "head", "Head" },
                { "leftshoulder", "LeftShoulder" },
                { "leftupper", "LeftUpperArm" },
                { "leftlower", "LeftLowerArm" },
                { "lefthand", "LeftHand" },
                { "rightshoulder", "RightShoulder" },
                { "rightupper", "RightUpperArm" },
                { "rightlower", "RightLowerArm" },
                { "righthand", "RightHand" },
                { "leftupperleg", "LeftUpperLeg" },
                { "leftlowerleg", "LeftLowerLeg" },
                { "leftfoot", "LeftFoot" },
                { "rightupperleg", "RightUpperLeg" },
                { "rightlowerleg", "RightLowerLeg" },
                { "rightfoot", "RightFoot" }
            };
            
            foreach (Transform costumeBone in costumeBones) {
                string normalizedName = AvatarUtility.NormalizeBoneName(costumeBone.name).ToLowerInvariant();
                
                foreach (var pair in standardBoneNames) {
                    if (normalizedName.Contains(pair.Key) && !costumeBone.name.Contains(pair.Value)) {
                        // 対応するアバターのボーンを探す
                        Transform avatarBone = null;
                        foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                            if (boneType == HumanBodyBones.LastBone) continue;
                            
                            if (boneType.ToString() == pair.Value) {
                                avatarBone = avatarAnimator.GetBoneTransform(boneType);
                                break;
                            }
                        }
                        
                        BoneProblem problem = new BoneProblem(
                            BoneProblem.ProblemType.NamingIssue,
                            $"ボーン '{costumeBone.name}' の名前が標準的な命名規則 '{pair.Value}' と異なります。",
                            costumeBone,
                            avatarBone
                        );
                        
                        problem.canAutoFix = true;
                        problem.fixAction = () => {
                            string newName = pair.Value;
                            if (avatarBone != null) {
                                newName = avatarBone.name; // アバターのボーン名を使用
                            }
                            
                            string oldName = costumeBone.name;
                            costumeBone.name = newName;
                            EditorUtility.SetDirty(costumeBone);
                            EditorUtility.DisplayDialog("修正完了", $"ボーン名を '{oldName}' から '{newName}' に変更しました。", "OK");
                        };
                        
                        detectedProblems.Add(problem);
                        break;
                    }
                }
            }
        }
        
        // 対応するアバターのボーンを探す
        private Transform FindCorrespondingBone(Animator avatarAnimator, Transform costumeBone) {
            if (avatarAnimator == null || !avatarAnimator.isHuman) return null;
            
            string normalizedName = AvatarUtility.NormalizeBoneName(costumeBone.name).ToLowerInvariant();
            
            // Humanoidボーンから探す
            foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                if (boneType == HumanBodyBones.LastBone) continue;
                
                Transform avatarBone = avatarAnimator.GetBoneTransform(boneType);
                if (avatarBone != null) {
                    string normalizedAvatarName = AvatarUtility.NormalizeBoneName(avatarBone.name).ToLowerInvariant();
                    
                    if (normalizedAvatarName == normalizedName ||
                        normalizedAvatarName.Contains(normalizedName) ||
                        normalizedName.Contains(normalizedAvatarName)) {
                        return avatarBone;
                    }
                    
                    // ボーンタイプ名での比較も行う
                    string boneTypeName = boneType.ToString().ToLowerInvariant();
                    if (boneTypeName == normalizedName ||
                        boneTypeName.Contains(normalizedName) ||
                        normalizedName.Contains(boneTypeName)) {
                        return avatarBone;
                    }
                }
            }
            
            // 位置ベースで近いボーンを探す
            return AvatarUtility.FindBoneByPosition(avatarAnimator.transform, costumeBone.position, 0.3f);
        }
        
        // 問題を修正
        private void FixProblem(BoneProblem problem) {
            if (problem.canAutoFix && problem.fixAction != null) {
                problem.fixAction();
            }
        }
        
        // 自動修正を開始
        private void StartAutoFix() {
            int fixableCount = CountFixableProblems();
            if (fixableCount == 0) {
                EditorUtility.DisplayDialog("修正不可", "自動修正可能な問題がありません。", "OK");
                return;
            }
            
            isFixing = true;
            currentFixIndex = 0;
            fixProgress = 0f;
            
            // 最初の修正を実行
            ProcessNextFix();
        }
        
        // 次の修正を処理
        private void ProcessNextFix() {
            // 修正可能な問題を探す
            List<BoneProblem> fixableProblems = detectedProblems.FindAll(p => p.canAutoFix && ShouldShowProblem(p));
            
            if (currentFixIndex < fixableProblems.Count) {
                // 進捗を更新
                fixProgress = (float)currentFixIndex / fixableProblems.Count;
                
                // 進捗バーを表示
                EditorUtility.DisplayProgressBar("自動修正中", $"問題を修正中... ({currentFixIndex + 1}/{fixableProblems.Count})", fixProgress);
                
                // 問題を修正
                BoneProblem problem = fixableProblems[currentFixIndex];
                FixProblem(problem);
                
                // インデックスを進める
                currentFixIndex++;
                
                // 処理を続行するかユーザーに確認
                bool continueProcessing = EditorUtility.DisplayDialog(
                    "修正進行中",
                    $"問題 {currentFixIndex}/{fixableProblems.Count} を修正しました。\n\n次の問題の修正を続行しますか？",
                    "続行", "中止");
                
                if (continueProcessing) {
                    // 次の修正を処理
                    ProcessNextFix();
                } else {
                    // 中止
                    FinishAutoFix();
                }
            } else {
                // すべての修正が完了
                FinishAutoFix();
            }
        }
        
        // 自動修正を終了
        private void FinishAutoFix() {
            isFixing = false;
            EditorUtility.ClearProgressBar();
            DetectProblems(); // 問題リストを更新
            EditorUtility.DisplayDialog("修正完了", "自動修正が完了しました。", "OK");
        }
        
        // 表示すべき問題かどうかを判定
        private bool ShouldShowProblem(BoneProblem problem) {
            switch (problem.type) {
                case BoneProblem.ProblemType.MissingBone:
                    return showMissingBones;
                case BoneProblem.ProblemType.ScalingIssue:
                    return showScalingIssues;
                case BoneProblem.ProblemType.RotationIssue:
                    return showRotationIssues;
                case BoneProblem.ProblemType.NamingIssue:
                    return showNamingIssues;
                default:
                    return true;
            }
        }
        
        // 問題タイプに応じた色を取得
        private Color GetProblemTypeColor(BoneProblem.ProblemType type) {
            switch (type) {
                case BoneProblem.ProblemType.MissingBone:
                    return Color.red;
                case BoneProblem.ProblemType.ScalingIssue:
                    return new Color(1f, 0.6f, 0f); // オレンジ
                case BoneProblem.ProblemType.RotationIssue:
                    return new Color(0.8f, 0f, 0.8f); // 紫
                case BoneProblem.ProblemType.NamingIssue:
                    return new Color(0f, 0.6f, 1f); // 水色
                default:
                    return Color.black;
            }
        }
        
        // 問題タイプを文字列で取得
        private string GetProblemTypeString(BoneProblem.ProblemType type) {
            switch (type) {
                case BoneProblem.ProblemType.MissingBone:
                    return "不足ボーン";
                case BoneProblem.ProblemType.ScalingIssue:
                    return "スケール問題";
                case BoneProblem.ProblemType.RotationIssue:
                    return "回転問題";
                case BoneProblem.ProblemType.NamingIssue:
                    return "名前問題";
                default:
                    return "不明な問題";
            }
        }
        
        // 表示される問題の数をカウント
        private int CountVisibleProblems() {
            int count = 0;
            foreach (BoneProblem problem in detectedProblems) {
                if (ShouldShowProblem(problem)) {
                    count++;
                }
            }
            return count;
        }
        
        // 修正可能な問題の数をカウント
        private int CountFixableProblems() {
            int count = 0;
            foreach (BoneProblem problem in detectedProblems) {
                if (problem.canAutoFix && ShouldShowProblem(problem)) {
                    count++;
                }
            }
            return count;
        }
    }
}
