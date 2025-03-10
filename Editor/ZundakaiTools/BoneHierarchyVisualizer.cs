using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    /// <summary>
    /// アバターと衣装のボーン階層を視覚化するクラス
    /// </summary>
    public class BoneHierarchyVisualizer : EditorWindow {
        // 表示対象のオブジェクト
        private GameObject targetObject;
        
        // スクロール位置
        private Vector2 scrollPosition;
        
        // 展開状態を記録
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
        
        // ボーン検索フィルター
        private string searchFilter = "";
        
        // 表示モード
        private enum DisplayMode {
            Tree,       // ツリー表示
            Flat,       // フラット表示
            Humanoid    // ヒューマノイドマッピング
        }
        private DisplayMode currentMode = DisplayMode.Tree;
        
        // 選択されたボーン
        private Transform selectedBone;
        
        // 対応ボーン表示用
        private Dictionary<Transform, Transform> boneCorrespondence;
        private GameObject correspondenceTarget;
        
        // メニュー項目を削除（内部からのみ利用可能）
        public static void ShowWindow() {
            GetWindow<BoneHierarchyVisualizer>("ボーン階層ビューア");
        }
        
        private void OnGUI() {
            EditorGUILayout.BeginVertical();
            
            // ヘッダー
            GUILayout.Label("ボーン階層ビューア", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // 表示対象の選択
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("表示対象", GUILayout.Width(80));
            GameObject newTarget = (GameObject)EditorGUILayout.ObjectField(targetObject, typeof(GameObject), true);
            
            if (newTarget != targetObject) {
                targetObject = newTarget;
                foldoutStates.Clear(); // 新しいオブジェクトに変更された場合は展開状態をリセット
                selectedBone = null;
            }
            EditorGUILayout.EndHorizontal();
            
            // 対応オブジェクトの選択（対応表示モード用）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("対応オブジェクト", GUILayout.Width(80));
            GameObject newCorrespondence = (GameObject)EditorGUILayout.ObjectField(correspondenceTarget, typeof(GameObject), true);
            
            if (newCorrespondence != correspondenceTarget) {
                correspondenceTarget = newCorrespondence;
                UpdateBoneCorrespondence();
            }
            EditorGUILayout.EndHorizontal();
            
            // 表示モードの選択
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("表示モード", GUILayout.Width(80));
            currentMode = (DisplayMode)EditorGUILayout.EnumPopup(currentMode);
            EditorGUILayout.EndHorizontal();
            
            // 検索フィルター
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("検索", GUILayout.Width(80));
            string newFilter = EditorGUILayout.TextField(searchFilter);
            if (newFilter != searchFilter) {
                searchFilter = newFilter;
                // 検索フィルターが変更された場合は、一致するボーンを自動的に展開する
                if (!string.IsNullOrEmpty(searchFilter)) {
                    ExpandMatchingBones(targetObject.transform, searchFilter.ToLowerInvariant());
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // コントロールボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("すべて展開", GUILayout.Height(25))) {
                ExpandAllBones(targetObject?.transform);
            }
            if (GUILayout.Button("すべて閉じる", GUILayout.Height(25))) {
                foldoutStates.Clear();
            }
            if (GUILayout.Button("ボーン対応を更新", GUILayout.Height(25))) {
                UpdateBoneCorrespondence();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // ボーン階層の表示
            if (targetObject != null) {
                EditorGUILayout.BeginVertical("box");
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                
                switch (currentMode) {
                    case DisplayMode.Tree:
                        DrawBoneTreeView(targetObject.transform, 0);
                        break;
                    case DisplayMode.Flat:
                        DrawBoneFlatView();
                        break;
                    case DisplayMode.Humanoid:
                        DrawHumanoidView();
                        break;
                }
                
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                
                // 選択されたボーンの情報表示
                if (selectedBone != null) {
                    DrawBoneDetails(selectedBone);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // ボーンの階層をツリー表示
        private void DrawBoneTreeView(Transform bone, int indent) {
            if (bone == null) return;
            
            string path = GetTransformPath(bone);
            bool matchesFilter = string.IsNullOrEmpty(searchFilter) || 
                                bone.name.ToLowerInvariant().Contains(searchFilter.ToLowerInvariant());
            
            // フィルターに一致しないボーンはスキップ（ただし、一致するボーンの祖先は表示）
            if (!matchesFilter && !HasMatchingDescendant(bone, searchFilter)) {
                return;
            }
            
            // インデント用のスペース
            GUILayout.BeginHorizontal();
            GUILayout.Space(indent * 20);
            
            // 展開状態の取得
            if (!foldoutStates.ContainsKey(path)) {
                foldoutStates[path] = false;
            }
            
            // 子がない場合は通常のラベル表示
            if (bone.childCount == 0) {
                GUIStyle style = new GUIStyle(EditorStyles.label);
                
                // 選択されたボーンはハイライト
                if (bone == selectedBone) {
                    style.normal.textColor = Color.green;
                    style.fontStyle = FontStyle.Bold;
                }
                
                // 対応ボーンがある場合は色を変更
                if (boneCorrespondence != null && boneCorrespondence.ContainsKey(bone)) {
                    style.normal.textColor = new Color(0.2f, 0.5f, 1.0f);
                }
                
                // ハイライトやフィルターに一致する場合は太字にする
                if (matchesFilter) {
                    style.fontStyle = FontStyle.Bold;
                    if (style.normal.textColor == Color.black) {
                        style.normal.textColor = Color.blue;
                    }
                }
                
                GUILayout.Label("○ " + bone.name, style);
            } else {
                // 子がある場合は折りたたみ表示
                GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
                
                // 選択されたボーンはハイライト
                if (bone == selectedBone) {
                    foldoutStyle.normal.textColor = Color.green;
                    foldoutStyle.fontStyle = FontStyle.Bold;
                }
                
                // 対応ボーンがある場合は色を変更
                if (boneCorrespondence != null && boneCorrespondence.ContainsKey(bone)) {
                    foldoutStyle.normal.textColor = new Color(0.2f, 0.5f, 1.0f);
                }
                
                // ハイライトやフィルターに一致する場合は太字にする
                if (matchesFilter) {
                    foldoutStyle.fontStyle = FontStyle.Bold;
                    if (foldoutStyle.normal.textColor == Color.black) {
                        foldoutStyle.normal.textColor = Color.blue;
                    }
                }
                
                bool expanded = foldoutStates[path];
                bool newExpanded = EditorGUILayout.Foldout(expanded, bone.name, foldoutStyle);
                
                if (expanded != newExpanded) {
                    foldoutStates[path] = newExpanded;
                }
            }
            
            // ボーンの選択
            if (GUILayout.Button("選択", GUILayout.Width(50))) {
                selectedBone = bone;
                
                // シーンビューで選択
                Selection.activeGameObject = bone.gameObject;
            }
            
            GUILayout.EndHorizontal();
            
            // 子ボーンの表示（展開されている場合のみ）
            if (foldoutStates[path]) {
                for (int i = 0; i < bone.childCount; i++) {
                    DrawBoneTreeView(bone.GetChild(i), indent + 1);
                }
            }
        }
        
        // ボーンをフラットに一覧表示
        private void DrawBoneFlatView() {
            if (targetObject == null) return;
            
            // すべてのボーンを取得
            Transform[] allBones = targetObject.GetComponentsInChildren<Transform>(true);
            
            foreach (Transform bone in allBones) {
                // フィルターに一致しないボーンはスキップ
                if (!string.IsNullOrEmpty(searchFilter) && 
                    !bone.name.ToLowerInvariant().Contains(searchFilter.ToLowerInvariant())) {
                    continue;
                }
                
                GUILayout.BeginHorizontal();
                
                GUIStyle style = new GUIStyle(EditorStyles.label);
                
                // 選択されたボーンはハイライト
                if (bone == selectedBone) {
                    style.normal.textColor = Color.green;
                    style.fontStyle = FontStyle.Bold;
                }
                
                // 対応ボーンがある場合は色を変更
                if (boneCorrespondence != null && boneCorrespondence.ContainsKey(bone)) {
                    style.normal.textColor = new Color(0.2f, 0.5f, 1.0f);
                }
                
                GUILayout.Label(bone.name, style);
                
                // パスを表示
                GUILayout.Label(GetTransformPath(bone), EditorStyles.miniLabel);
                
                // 対応ボーンの表示
                if (boneCorrespondence != null && boneCorrespondence.ContainsKey(bone)) {
                    GUILayout.Label("→ " + boneCorrespondence[bone].name, EditorStyles.boldLabel);
                }
                
                // ボーンの選択
                if (GUILayout.Button("選択", GUILayout.Width(50))) {
                    selectedBone = bone;
                    
                    // シーンビューで選択
                    Selection.activeGameObject = bone.gameObject;
                }
                
                GUILayout.EndHorizontal();
            }
        }
        
        // ヒューマノイドマッピングを表示
        private void DrawHumanoidView() {
            if (targetObject == null) return;
            
            Animator animator = targetObject.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) {
                EditorGUILayout.HelpBox("対象オブジェクトはHumanoidアバターではありません。", MessageType.Warning);
                return;
            }
            
            // すべてのヒューマノイドボーンタイプについて表示
            foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                if (boneType == HumanBodyBones.LastBone) continue;
                
                // フィルターに一致しないボーンはスキップ
                string boneTypeName = boneType.ToString();
                if (!string.IsNullOrEmpty(searchFilter) && 
                    !boneTypeName.ToLowerInvariant().Contains(searchFilter.ToLowerInvariant())) {
                    continue;
                }
                
                Transform bone = animator.GetBoneTransform(boneType);
                if (bone == null) continue;
                
                GUILayout.BeginHorizontal();
                
                GUIStyle typeStyle = new GUIStyle(EditorStyles.boldLabel);
                GUIStyle nameStyle = new GUIStyle(EditorStyles.label);
                
                // 選択されたボーンはハイライト
                if (bone == selectedBone) {
                    typeStyle.normal.textColor = Color.green;
                    nameStyle.normal.textColor = Color.green;
                }
                
                // 対応ボーンがある場合は色を変更
                if (boneCorrespondence != null && boneCorrespondence.ContainsKey(bone)) {
                    typeStyle.normal.textColor = new Color(0.2f, 0.5f, 1.0f);
                }
                
                GUILayout.Label(boneTypeName, typeStyle, GUILayout.Width(150));
                GUILayout.Label(bone.name, nameStyle);
                
                // 対応ボーンの表示
                if (boneCorrespondence != null && boneCorrespondence.ContainsKey(bone)) {
                    GUILayout.Label("→ " + boneCorrespondence[bone].name, EditorStyles.boldLabel);
                }
                
                // ボーンの選択
                if (GUILayout.Button("選択", GUILayout.Width(50))) {
                    selectedBone = bone;
                    
                    // シーンビューで選択
                    Selection.activeGameObject = bone.gameObject;
                }
                
                GUILayout.EndHorizontal();
            }
        }
        
        // ボーンの詳細情報を表示
        private void DrawBoneDetails(Transform bone) {
            EditorGUILayout.BeginVertical("box");
            
            GUILayout.Label("ボーン詳細情報", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField("名前", bone.name);
            EditorGUILayout.LabelField("パス", GetTransformPath(bone));
            
            EditorGUILayout.LabelField("ローカル位置", bone.localPosition.ToString());
            EditorGUILayout.LabelField("ローカル回転", bone.localRotation.eulerAngles.ToString());
            EditorGUILayout.LabelField("ローカルスケール", bone.localScale.ToString());
            
            // Humanoidボーンタイプを表示
            Animator animator = targetObject.GetComponent<Animator>();
            if (animator != null && animator.isHuman) {
                foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                    if (boneType == HumanBodyBones.LastBone) continue;
                    
                    Transform humanoidBone = animator.GetBoneTransform(boneType);
                    if (humanoidBone == bone) {
                        EditorGUILayout.LabelField("Humanoidタイプ", boneType.ToString());
                        break;
                    }
                }
            }
            
            // 対応ボーンの表示
            if (boneCorrespondence != null && boneCorrespondence.ContainsKey(bone)) {
                Transform correspondingBone = boneCorrespondence[bone];
                
                EditorGUILayout.Space();
                GUILayout.Label("対応ボーン情報", EditorStyles.boldLabel);
                
                EditorGUILayout.LabelField("名前", correspondingBone.name);
                EditorGUILayout.LabelField("パス", GetTransformPath(correspondingBone));
                
                // 対応ボーンの選択ボタン
                if (GUILayout.Button("対応ボーンを選択", GUILayout.Height(25))) {
                    selectedBone = correspondingBone;
                    Selection.activeGameObject = correspondingBone.gameObject;
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // トランスフォームのパスを取得
        private string GetTransformPath(Transform transform) {
            if (transform == null) return "";
            
            string path = transform.name;
            Transform parent = transform.parent;
            
            while (parent != null) {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }
        
        // すべてのボーンを展開
        private void ExpandAllBones(Transform root) {
            if (root == null) return;
            
            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(root);
            
            while (queue.Count > 0) {
                Transform current = queue.Dequeue();
                string path = GetTransformPath(current);
                
                // 展開状態を設定
                foldoutStates[path] = true;
                
                // 子を追加
                for (int i = 0; i < current.childCount; i++) {
                    queue.Enqueue(current.GetChild(i));
                }
            }
        }
        
        // 一致するボーンとその祖先を展開
        private void ExpandMatchingBones(Transform root, string filter) {
            if (root == null || string.IsNullOrEmpty(filter)) return;
            
            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(root);
            
            Dictionary<Transform, bool> hasMatchingDescendant = new Dictionary<Transform, bool>();
            
            // すべてのボーンをチェック
            while (queue.Count > 0) {
                Transform current = queue.Dequeue();
                
                // 自分自身がフィルターに一致するかチェック
                bool matchesSelf = current.name.ToLowerInvariant().Contains(filter);
                
                // 子孫にフィルターに一致するものがあるかチェック
                bool matchesDescendants = false;
                for (int i = 0; i < current.childCount; i++) {
                    Transform child = current.GetChild(i);
                    queue.Enqueue(child);
                    
                    if (hasMatchingDescendant.ContainsKey(child) && hasMatchingDescendant[child]) {
                        matchesDescendants = true;
                    }
                }
                
                // 結果を記録
                hasMatchingDescendant[current] = matchesSelf || matchesDescendants;
                
                // 一致する場合は展開
                if (matchesSelf || matchesDescendants) {
                    // すべての祖先を展開
                    Transform ancestor = current.parent;
                    while (ancestor != null) {
                        string ancestorPath = GetTransformPath(ancestor);
                        foldoutStates[ancestorPath] = true;
                        ancestor = ancestor.parent;
                    }
                }
            }
        }
        
        // 子孫にフィルターに一致するボーンがあるかチェック
        private bool HasMatchingDescendant(Transform bone, string filter) {
            if (string.IsNullOrEmpty(filter)) return true;
            filter = filter.ToLowerInvariant();
            
            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(bone);
            
            while (queue.Count > 0) {
                Transform current = queue.Dequeue();
                
                if (current.name.ToLowerInvariant().Contains(filter)) {
                    return true;
                }
                
                for (int i = 0; i < current.childCount; i++) {
                    queue.Enqueue(current.GetChild(i));
                }
            }
            
            return false;
        }
        
        // ボーンの対応関係を更新
        private void UpdateBoneCorrespondence() {
            if (targetObject == null || correspondenceTarget == null) {
                boneCorrespondence = null;
                return;
            }
            
            boneCorrespondence = new Dictionary<Transform, Transform>();
            
            // アニメーターを取得
            Animator targetAnimator = targetObject.GetComponent<Animator>();
            Animator correspondenceAnimator = correspondenceTarget.GetComponent<Animator>();
            
            if (targetAnimator != null && targetAnimator.isHuman && 
                correspondenceAnimator != null && correspondenceAnimator.isHuman) {
                // Humanoidボーンの対応関係を作成
                foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                    if (boneType == HumanBodyBones.LastBone) continue;
                    
                    Transform targetBone = targetAnimator.GetBoneTransform(boneType);
                    Transform correspondenceBone = correspondenceAnimator.GetBoneTransform(boneType);
                    
                    if (targetBone != null && correspondenceBone != null) {
                        boneCorrespondence[targetBone] = correspondenceBone;
                    }
                }
            } else {
                // 名前ベースの対応関係を作成
                Transform[] targetBones = targetObject.GetComponentsInChildren<Transform>(true);
                Transform[] correspondenceBones = correspondenceTarget.GetComponentsInChildren<Transform>(true);
                
                foreach (Transform targetBone in targetBones) {
                    // 同じ名前のボーンを探す
                    foreach (Transform correspondenceBone in correspondenceBones) {
                        if (targetBone.name == correspondenceBone.name) {
                            boneCorrespondence[targetBone] = correspondenceBone;
                            break;
                        }
                    }
                }
                
                // 正規化名前ベースでも探す
                foreach (Transform targetBone in targetBones) {
                    if (boneCorrespondence.ContainsKey(targetBone)) continue;
                    
                    string normalizedName = AvatarUtility.NormalizeBoneName(targetBone.name).ToLowerInvariant();
                    
                    foreach (Transform correspondenceBone in correspondenceBones) {
                        string normalizedCorrespondenceName = AvatarUtility.NormalizeBoneName(correspondenceBone.name).ToLowerInvariant();
                        
                        if (normalizedName == normalizedCorrespondenceName || 
                            normalizedName.Contains(normalizedCorrespondenceName) || 
                            normalizedCorrespondenceName.Contains(normalizedName)) {
                            boneCorrespondence[targetBone] = correspondenceBone;
                            break;
                        }
                    }
                }
            }
        }
    }
}