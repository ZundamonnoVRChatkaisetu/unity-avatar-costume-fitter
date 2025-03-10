using System.Collections.Generic;
using System.Linq;
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
        private bool adjustByMesh = false; // メッシュで調整するオプション
        
        // メッシュ情報
        private Dictionary<string, Mesh> avatarMeshes = new Dictionary<string, Mesh>();
        private Dictionary<string, Mesh> costumeMeshes = new Dictionary<string, Mesh>();
        private Dictionary<string, string> meshPartMapping = new Dictionary<string, string>();
        
        // 部位ごとの参照位置とスケール
        private Dictionary<string, ReferencePoint> avatarReferencePoints = new Dictionary<string, ReferencePoint>();
        private Dictionary<string, ReferencePoint> costumeReferencePoints = new Dictionary<string, ReferencePoint>();
        
        // About情報
        private bool showAboutInfo = false;
        private string aboutInfo = "全アバター衣装自動調整ツール\nVersion 1.4.2\n\n衣装をアバターに自動的に合わせるツールです。\n\n使い方：\n1. アバターと衣装をドラッグ＆ドロップで選択\n2. 「ボーンマッピング」タブで対応関係を確認・調整\n3. 「衣装を着せる」ボタンをクリック\n4. 微調整バーで細かい調整を行う";
        
        // メッシュキャッシュ（リアルタイム調整用）
        private Dictionary<SkinnedMeshRenderer, Mesh> originalMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
        
        // アバターのボーンマッピング
        private Dictionary<string, Transform> avatarBoneMapping;
        
        // ボーン情報表示とマッピング
        private bool showBoneMapping = false;
        private Vector2 boneMappingScrollPos;
        private Dictionary<Transform, Transform> manualBoneMapping = new Dictionary<Transform, Transform>();
        private Dictionary<Transform, bool> ignoredBones = new Dictionary<Transform, bool>(); // マッピング対象外のボーン
        private List<Transform> avatarBones = new List<Transform>();
        private List<Transform> costumeBones = new List<Transform>();
        private string filterText = "";
        
        // 階層パス情報
        private Dictionary<Transform, string> avatarBoneHierarchyPaths = new Dictionary<Transform, string>();
        private Dictionary<Transform, string> costumeBoneHierarchyPaths = new Dictionary<Transform, string>();
        private Dictionary<string, List<Transform>> avatarBonesByPath = new Dictionary<string, List<Transform>>();
        private Dictionary<string, List<Transform>> costumeBonesByPath = new Dictionary<string, List<Transform>>();
        
        // UI全体のスクロール
        private Vector2 mainScrollPosition;
        
        // エディタ更新時間
        private double lastUpdateTime;
        
        // 除外キーワード
        private readonly string[] exclusionKeywords = {"wing", "tail", "eye", "ear", "hair", "tongue", "jaw"};
        
        // ボディパーツ識別キーワード
        private readonly Dictionary<string, string[]> bodyPartKeywords = new Dictionary<string, string[]>() {
            { "頭", new string[] { "head", "face", "顔", "頭", "helmet", "hat", "cap" } },
            { "胴体", new string[] { "body", "chest", "torso", "trunk", "spine", "胴", "胸", "体", "waist", "spine", "pelvis", "hips" } },
            { "左腕", new string[] { "leftarm", "left_arm", "l_arm", "左腕", "左手", "larm", "l.arm", "arm.l", "lhand", "left.arm" } },
            { "右腕", new string[] { "rightarm", "right_arm", "r_arm", "右腕", "右手", "rarm", "r.arm", "arm.r", "rhand", "right.arm" } },
            { "左脚", new string[] { "leftleg", "left_leg", "l_leg", "左脚", "左足", "lleg", "l.leg", "leg.l", "lfoot", "left.leg" } },
            { "右脚", new string[] { "rightleg", "right_leg", "r_leg", "右脚", "右足", "rleg", "r.leg", "leg.r", "rfoot", "right.leg" } }
        };
        
        // 部位ごとの参照点を表す構造体
        private struct ReferencePoint {
            public Vector3 center;      // 中心位置
            public Vector3 extents;     // 範囲の大きさ
            public Vector3 topPoint;    // 上端の位置
            public Vector3 bottomPoint; // 下端の位置
            public Vector3 leftPoint;   // 左端の位置
            public Vector3 rightPoint;  // 右端の位置
            public Vector3 frontPoint;  // 前端の位置 
            public Vector3 backPoint;   // 後端の位置
            
            public ReferencePoint(Vector3 center, Vector3 extents) {
                this.center = center;
                this.extents = extents;
                
                // 各端点を計算
                topPoint = center + new Vector3(0, extents.y, 0);
                bottomPoint = center - new Vector3(0, extents.y, 0);
                leftPoint = center - new Vector3(extents.x, 0, 0);
                rightPoint = center + new Vector3(extents.x, 0, 0);
                frontPoint = center + new Vector3(0, 0, extents.z);
                backPoint = center - new Vector3(0, 0, extents.z);
            }
        }
        
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
            // スクロール開始（ウィンドウ全体をスクロール可能に）
            mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);
            
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
                
                // ボーンリストを更新
                UpdateBoneLists();
                
                // アバターのメッシュ情報を解析
                if (avatarObject != null) {
                    AnalyzeAvatarMeshes();
                }
            }
            
            // ドラッグアンドドロップのヒント
            EditorGUILayout.HelpBox("アバターをここにドラッグ＆ドロップしてください", MessageType.Info);
            EditorGUILayout.Space();
            
            // コスチュームの選択
            EditorGUILayout.LabelField("衣装を選択");
            GameObject newCostumeObject = (GameObject)EditorGUILayout.ObjectField(costumeObject, typeof(GameObject), true);
            if (newCostumeObject != costumeObject) {
                costumeObject = newCostumeObject;
                
                // ボーンリストを更新
                UpdateBoneLists();
                
                // 衣装のメッシュ情報を解析
                if (costumeObject != null) {
                    AnalyzeCostumeMeshes();
                }
            }
            
            // ドラッグアンドドロップのヒント
            EditorGUILayout.HelpBox("衣装をここにドラッグ＆ドロップしてください", MessageType.Info);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // ボーンマッピング表示
            showBoneMapping = EditorGUILayout.Foldout(showBoneMapping, "ボーンマッピング");
            if (showBoneMapping) {
                DrawBoneMappingUI();
            }
            
            EditorGUILayout.Space();
            
            // 詳細設定の折りたたみ
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "詳細設定");
            if (showAdvancedSettings) {
                EditorGUILayout.BeginVertical("box");
                
                autoScale = EditorGUILayout.Toggle("自動サイズ調整", autoScale);
                adjustSkinWeights = EditorGUILayout.Toggle("ウェイト最適化", adjustSkinWeights);
                createBlendShapes = EditorGUILayout.Toggle("ブレンドシェイプ作成", createBlendShapes);
                
                // メッシュで調整オプションを追加
                bool prevAdjustByMesh = adjustByMesh;
                adjustByMesh = EditorGUILayout.Toggle("メッシュで調整", adjustByMesh);
                
                // オプションの変更がある場合は説明を表示
                if (adjustByMesh != prevAdjustByMesh && adjustByMesh) {
                    EditorGUILayout.HelpBox("メッシュで調整: ボーンマッピングの代わりにメッシュ形状を使って衣装を調整します。アバターのメッシュ形状に合わせて衣装のメッシュを変形します。", MessageType.Info);
                }
                
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
            
            // スクロール終了
            EditorGUILayout.EndScrollView();
            
            // マウス操作やキーボード操作があった場合、再描画をリクエスト
            if (Event.current.type == EventType.MouseMove || 
                Event.current.type == EventType.KeyDown) {
                Repaint();
            }
        }
        
        // アバターのメッシュを解析
        private void AnalyzeAvatarMeshes() {
            avatarMeshes.Clear();
            avatarReferencePoints.Clear();
            
            if (avatarObject == null) return;
            
            SkinnedMeshRenderer[] renderers = avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in renderers) {
                if (renderer.sharedMesh == null) continue;
                
                string partName = IdentifyBodyPart(renderer);
                avatarMeshes[partName] = renderer.sharedMesh;
                
                // 部位ごとの参照点を計算
                CalculateReferencePoints(renderer, partName, true);
                
                // デバッグ情報
                Debug.Log($"アバターメッシュを検出: {renderer.name} → {partName}");
            }
        }
        
        // 衣装のメッシュを解析
        private void AnalyzeCostumeMeshes() {
            costumeMeshes.Clear();
            meshPartMapping.Clear();
            costumeReferencePoints.Clear();
            
            if (costumeObject == null) return;
            
            SkinnedMeshRenderer[] renderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in renderers) {
                if (renderer.sharedMesh == null) continue;
                
                string partName = IdentifyBodyPart(renderer);
                costumeMeshes[renderer.name] = renderer.sharedMesh;
                meshPartMapping[renderer.name] = partName;
                
                // 部位ごとの参照点を計算
                CalculateReferencePoints(renderer, partName, false);
                
                // デバッグ情報
                Debug.Log($"衣装メッシュを検出: {renderer.name} → {partName}");
            }
        }
        
        // 部位ごとの参照点を計算
        private void CalculateReferencePoints(SkinnedMeshRenderer renderer, string partName, bool isAvatar) {
            if (renderer == null || renderer.sharedMesh == null) return;
            
            // メッシュの頂点を取得
            Vector3[] vertices = renderer.sharedMesh.vertices;
            if (vertices.Length == 0) return;
            
            // バウンディングボックスの計算
            Bounds bounds = renderer.sharedMesh.bounds;
            
            // ボーンスペースからワールドスペースへの変換行列
            Matrix4x4 localToWorld = renderer.transform.localToWorldMatrix;
            
            // 参照点の作成と保存
            ReferencePoint refPoint = new ReferencePoint(
                localToWorld.MultiplyPoint3x4(bounds.center),
                bounds.extents
            );
            
            if (isAvatar) {
                avatarReferencePoints[partName] = refPoint;
            } else {
                costumeReferencePoints[partName] = refPoint;
            }
        }
        
        // メッシュのボディパーツを特定
        private string IdentifyBodyPart(SkinnedMeshRenderer renderer) {
            string objName = renderer.name.ToLowerInvariant();
            string defaultPart = "その他";
            
            // メッシュ名やオブジェクト名、ボーン情報をもとに部位を推定
            foreach (var part in bodyPartKeywords) {
                foreach (string keyword in part.Value) {
                    if (objName.Contains(keyword)) {
                        return part.Key;
                    }
                }
            }
            
            // ボーン情報からも推定を試みる
            if (renderer.bones != null && renderer.bones.Length > 0) {
                Dictionary<string, int> partCounts = new Dictionary<string, int>();
                
                foreach (var bone in renderer.bones) {
                    if (bone == null) continue;
                    
                    string boneName = bone.name.ToLowerInvariant();
                    
                    foreach (var part in bodyPartKeywords) {
                        foreach (string keyword in part.Value) {
                            if (boneName.Contains(keyword)) {
                                if (!partCounts.ContainsKey(part.Key)) {
                                    partCounts[part.Key] = 0;
                                }
                                partCounts[part.Key]++;
                                break;
                            }
                        }
                    }
                }
                
                // 最も多く関連付けられた部位を採用
                if (partCounts.Count > 0) {
                    return partCounts.OrderByDescending(x => x.Value).First().Key;
                }
            }
            
            // 頂点分布からも推定を試みる
            if (renderer.sharedMesh != null) {
                Vector3[] vertices = renderer.sharedMesh.vertices;
                Vector3 center = Vector3.zero;
                
                foreach (Vector3 v in vertices) {
                    center += v;
                }
                
                if (vertices.Length > 0) {
                    center /= vertices.Length;
                    
                    // 位置に基づく簡易的な判定
                    if (center.y > 1.5f) return "頭";
                    if (center.y < 0.5f) {
                        if (center.x < -0.2f) return "左脚";
                        if (center.x > 0.2f) return "右脚";
                        return "胴体";
                    }
                    if (center.x < -0.3f) return "左腕";
                    if (center.x > 0.3f) return "右腕";
                    return "胴体";
                }
            }
            
            return defaultPart;
        }
        
        // ボーンマッピングUI
        private void DrawBoneMappingUI() {
            if (avatarObject == null || costumeObject == null) {
                EditorGUILayout.HelpBox("アバターと衣装を選択してください", MessageType.Info);
                return;
            }
            
            EditorGUILayout.BeginVertical("box");
            
            // マッピングの説明文
            EditorGUILayout.HelpBox("アバターと衣装のボーン対応関係を確認・調整できます。自動マッピングで対応できなかった場合は手動で調整してください。", MessageType.Info);
            
            // フィルタリング
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("フィルタ:", GUILayout.Width(50));
            string newFilterText = EditorGUILayout.TextField(filterText);
            if (newFilterText != filterText) {
                filterText = newFilterText;
            }
            if (GUILayout.Button("クリア", GUILayout.Width(60))) {
                filterText = "";
            }
            EditorGUILayout.EndHorizontal();
            
            // マッピングテーブルのヘッダー
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("アバターのボーン", EditorStyles.boldLabel, GUILayout.Width(180));
            EditorGUILayout.LabelField("⇔", EditorStyles.boldLabel, GUILayout.Width(30));
            EditorGUILayout.LabelField("衣装のボーン", EditorStyles.boldLabel, GUILayout.Width(180));
            EditorGUILayout.LabelField("状態", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            
            // スクロール開始
            boneMappingScrollPos = EditorGUILayout.BeginScrollView(boneMappingScrollPos, GUILayout.Height(200));
            
            // ボーンリストが空の場合は更新
            if (avatarBones.Count == 0 || costumeBones.Count == 0) {
                UpdateBoneLists();
            }
            
            // 除外されていないボーンのみ表示
            List<Transform> displayBones = new List<Transform>();
            foreach (Transform bone in avatarBones) {
                if (!ignoredBones.ContainsKey(bone) || !ignoredBones[bone]) {
                    displayBones.Add(bone);
                }
            }
            
            // 対応表示
            foreach (Transform avatarBone in displayBones) {
                // フィルタリング
                if (!string.IsNullOrEmpty(filterText) && 
                    !avatarBone.name.ToLowerInvariant().Contains(filterText.ToLowerInvariant())) {
                    continue;
                }
                
                EditorGUILayout.BeginHorizontal();
                
                // アバターボーン - 選択可能に
                if (GUILayout.Button(avatarBone.name, GUILayout.Width(180))) {
                    // ボーンをヒエラルキー上で選択
                    Selection.activeObject = avatarBone.gameObject;
                    EditorGUIUtility.PingObject(avatarBone.gameObject);
                }
                
                // ⇔ 記号表示
                EditorGUILayout.LabelField("⇔", GUILayout.Width(30));
                
                // 衣装ボーン選択ドロップダウン
                Transform mappedBone = null;
                if (manualBoneMapping.TryGetValue(avatarBone, out mappedBone)) {
                    // 手動マッピングがある場合
                } else {
                    // 自動マッピングを試行
                    mappedBone = FindCorrespondingBone(avatarBone);
                }
                
                // ドロップダウンの作成
                int selectedIndex = -1;
                List<string> options = new List<string>();
                options.Add("自動検出");
                options.Add("マッピングなし"); // マッピングなしのオプションを追加
                
                for (int j = 0; j < costumeBones.Count; j++) {
                    options.Add(costumeBones[j].name);
                    if (mappedBone == costumeBones[j]) {
                        selectedIndex = j + 2; // +2 は "自動検出" と "マッピングなし" の分
                    }
                }
                
                // 現在の選択がない場合は自動検出を選択
                if (selectedIndex == -1) {
                    selectedIndex = 0;
                }
                
                // ドロップダウン表示
                int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, options.ToArray(), GUILayout.Width(180));
                
                // 選択変更時の処理
                if (newSelectedIndex != selectedIndex) {
                    if (newSelectedIndex == 0) {
                        // 自動検出を選択した場合はマッピングから削除
                        if (manualBoneMapping.ContainsKey(avatarBone)) {
                            manualBoneMapping.Remove(avatarBone);
                        }
                    } else if (newSelectedIndex == 1) {
                        // マッピングなしを選択した場合
                        manualBoneMapping[avatarBone] = null;
                    } else {
                        // 特定のボーンを選択した場合はマッピングに追加
                        manualBoneMapping[avatarBone] = costumeBones[newSelectedIndex - 2];
                    }
                }
                
                // マッピング状態の表示
                string statusLabel = "未マッピング";
                Color originalColor = GUI.color;
                
                if (manualBoneMapping.ContainsKey(avatarBone)) {
                    if (manualBoneMapping[avatarBone] == null) {
                        statusLabel = "対象外";
                        GUI.color = Color.gray;
                    } else {
                        statusLabel = "手動設定";
                        GUI.color = Color.green;
                    }
                } else if (mappedBone != null) {
                    statusLabel = "自動検出";
                    GUI.color = Color.cyan;
                } else {
                    GUI.color = Color.yellow;
                }
                
                EditorGUILayout.LabelField(statusLabel, GUILayout.Width(80));
                GUI.color = originalColor;
                
                // ボーンを除外/復活するボタン
                if (ignoredBones.ContainsKey(avatarBone) && ignoredBones[avatarBone]) {
                    if (GUILayout.Button("復活", GUILayout.Width(60))) {
                        ignoredBones[avatarBone] = false;
                    }
                } else {
                    if (GUILayout.Button("除外", GUILayout.Width(60))) {
                        ignoredBones[avatarBone] = true;
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 衣装のボーンを選択した場合も同様に処理
                if (mappedBone != null && manualBoneMapping.ContainsKey(avatarBone) && manualBoneMapping[avatarBone] != null) {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20); // インデント
                    if (GUILayout.Button("↳ " + mappedBone.name, GUILayout.Width(180))) {
                        // 衣装のボーンをヒエラルキー上で選択
                        Selection.activeObject = mappedBone.gameObject;
                        EditorGUIUtility.PingObject(mappedBone.gameObject);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.BeginHorizontal();
            
            // マッピングのリセットボタン
            if (GUILayout.Button("マッピングをリセット", GUILayout.Height(30), GUILayout.Width(150))) {
                manualBoneMapping.Clear();
            }
            
            // 除外ボーンのリセットボタン
            if (GUILayout.Button("除外ボーンをリセット", GUILayout.Height(30), GUILayout.Width(150))) {
                ignoredBones.Clear();
                UpdateBoneLists(); // ボーンリストを再生成
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        // 対応するボーンを見つける（自動）
        private Transform FindCorrespondingBone(Transform avatarBone) {
            if (avatarBone == null || costumeBones.Count == 0) return null;
            
            // マッピングなしに設定されている場合はnullを返す
            if (manualBoneMapping.ContainsKey(avatarBone) && manualBoneMapping[avatarBone] == null) {
                return null;
            }
            
            // 1. 名前が完全一致するボーンを探す
            foreach (Transform bone in costumeBones) {
                if (bone.name == avatarBone.name) {
                    return bone;
                }
            }
            
            // 2. 正規化された名前で比較
            string normalizedAvatarName = AvatarUtility.NormalizeBoneName(avatarBone.name).ToLowerInvariant();
            foreach (Transform bone in costumeBones) {
                string normalizedCostumeName = AvatarUtility.NormalizeBoneName(bone.name).ToLowerInvariant();
                if (normalizedCostumeName == normalizedAvatarName || 
                    normalizedCostumeName.Contains(normalizedAvatarName) || 
                    normalizedAvatarName.Contains(normalizedCostumeName)) {
                    return bone;
                }
            }
            
            // 3. 階層ベースのマッピング
            if (avatarBoneHierarchyPaths.ContainsKey(avatarBone)) {
                string avatarPath = avatarBoneHierarchyPaths[avatarBone];
                string[] avatarPathSegments = avatarPath.Split('/');
                
                // ボーンの深さ（階層レベル）
                int avatarBoneDepth = avatarPathSegments.Length;
                
                // 同じ階層深さのボーンを候補として収集
                List<Transform> depthMatchedBones = new List<Transform>();
                
                foreach (var bone in costumeBones) {
                    if (costumeBoneHierarchyPaths.ContainsKey(bone)) {
                        string costumePath = costumeBoneHierarchyPaths[bone];
                        string[] costumePathSegments = costumePath.Split('/');
                        
                        // 階層の深さが一致した場合、候補に追加
                        if (costumePathSegments.Length == avatarBoneDepth) {
                            depthMatchedBones.Add(bone);
                        }
                    }
                }
                
                // 候補ボーンの中から、親の名前も似ているものを優先
                if (depthMatchedBones.Count > 0) {
                    Transform bestMatch = null;
                    float bestScore = 0;
                    
                    foreach (var bone in depthMatchedBones) {
                        string costumePath = costumeBoneHierarchyPaths[bone];
                        string[] costumePathSegments = costumePath.Split('/');
                        
                        // 親子関係の類似度スコアを計算
                        float score = CalculateHierarchySimilarityScore(avatarPathSegments, costumePathSegments);
                        
                        if (score > bestScore) {
                            bestScore = score;
                            bestMatch = bone;
                        }
                    }
                    
                    // 十分な類似度があればマッピング
                    if (bestScore > 0.5f) {
                        return bestMatch;
                    }
                }
            }
            
            // 4. 位置ベースのマッピング
            Vector3 avatarBonePos = avatarBone.position;
            Transform closestBone = null;
            float closestDistance = float.MaxValue;
            
            foreach (Transform bone in costumeBones) {
                float distance = Vector3.Distance(bone.position, avatarBonePos);
                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestBone = bone;
                }
            }
            
            // 距離が近すぎる場合のみ返す (一定以上離れていると関係ないボーン)
            if (closestDistance < 0.5f) {
                return closestBone;
            }
            
            return null;
        }
        
        // 階層構造の類似度を計算
        private float CalculateHierarchySimilarityScore(string[] avatarPathSegments, string[] costumePathSegments) {
            if (avatarPathSegments.Length != costumePathSegments.Length) return 0;
            
            float totalScore = 0;
            float maxPossibleScore = avatarPathSegments.Length;
            
            // 各階層レベルごとに名前の類似度を評価
            for (int i = 0; i < avatarPathSegments.Length; i++) {
                string avatarSegment = AvatarUtility.NormalizeBoneName(avatarPathSegments[i]).ToLowerInvariant();
                string costumeSegment = AvatarUtility.NormalizeBoneName(costumePathSegments[i]).ToLowerInvariant();
                
                // 完全一致
                if (avatarSegment == costumeSegment) {
                    totalScore += 1.0f;
                }
                // 部分一致（含む/含まれる）
                else if (avatarSegment.Contains(costumeSegment) || costumeSegment.Contains(avatarSegment)) {
                    totalScore += 0.7f;
                }
                // L/R, Left/Rightなどの対応
                else if ((avatarSegment.Contains("left") && costumeSegment.Contains("l")) ||
                         (avatarSegment.Contains("l") && costumeSegment.Contains("left")) ||
                         (avatarSegment.Contains("right") && costumeSegment.Contains("r")) ||
                         (avatarSegment.Contains("r") && costumeSegment.Contains("right"))) {
                    totalScore += 0.8f;
                }
                // 記号の違い (_/.など)
                else if (avatarSegment.Replace("_", "").Replace(".", "") == 
                         costumeSegment.Replace("_", "").Replace(".", "")) {
                    totalScore += 0.9f;
                }
            }
            
            // 正規化されたスコアを返す (0～1の範囲)
            return totalScore / maxPossibleScore;
        }
        
        // ボーンリストの更新
        private void UpdateBoneLists() {
            avatarBones.Clear();
            costumeBones.Clear();
            avatarBoneHierarchyPaths.Clear();
            costumeBoneHierarchyPaths.Clear();
            avatarBonesByPath.Clear();
            costumeBonesByPath.Clear();
            
            // アバターのボーンを取得
            if (avatarObject != null) {
                // Armature配下のボーンのみを選択対象とする
                Transform armatureTransform = FindArmatureTransform(avatarObject.transform);
                
                if (armatureTransform != null) {
                    // Armature配下のすべてのボーンを取得
                    ProcessBoneHierarchy(armatureTransform, "", true);
                } else {
                    // Armatureが見つからない場合は従来の方法を使用
                    foreach (Transform bone in avatarObject.GetComponentsInChildren<Transform>()) {
                        // 特定のキーワードを含むボーンは除外
                        bool shouldExclude = false;
                        foreach (string keyword in exclusionKeywords) {
                            if (bone.name.ToLowerInvariant().Contains(keyword.ToLowerInvariant())) {
                                shouldExclude = true;
                                break;
                            }
                        }
                        
                        if (!shouldExclude) {
                            avatarBones.Add(bone);
                            
                            // 階層パスを記録
                            string hierarchyPath = GetHierarchyPath(bone);
                            avatarBoneHierarchyPaths[bone] = hierarchyPath;
                            
                            // パスベースのルックアップテーブルに追加
                            if (!avatarBonesByPath.ContainsKey(hierarchyPath)) {
                                avatarBonesByPath[hierarchyPath] = new List<Transform>();
                            }
                            avatarBonesByPath[hierarchyPath].Add(bone);
                        } else {
                            // 除外されたボーンを記録
                            ignoredBones[bone] = true;
                        }
                    }
                }
                
                // Animatorがある場合はそのボーンも優先的に追加
                Animator avatarAnimator = avatarObject.GetComponent<Animator>();
                if (avatarAnimator != null && avatarAnimator.isHuman) {
                    // 主要なヒューマノイドボーンを追加（あれば上書き）
                    foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                        if (boneType == HumanBodyBones.LastBone) continue;
                        
                        Transform bone = avatarAnimator.GetBoneTransform(boneType);
                        if (bone != null && !avatarBones.Contains(bone)) {
                            avatarBones.Add(bone);
                            
                            // 階層パスを記録
                            string hierarchyPath = GetHierarchyPath(bone);
                            avatarBoneHierarchyPaths[bone] = hierarchyPath;
                            
                            // パスベースのルックアップテーブルに追加
                            if (!avatarBonesByPath.ContainsKey(hierarchyPath)) {
                                avatarBonesByPath[hierarchyPath] = new List<Transform>();
                            }
                            avatarBonesByPath[hierarchyPath].Add(bone);
                        }
                    }
                }
            }
            
            // 衣装のボーンを取得
            if (costumeObject != null) {
                // Armature配下のボーンのみを選択対象とする
                Transform costumeArmatureTransform = FindArmatureTransform(costumeObject.transform);
                
                if (costumeArmatureTransform != null) {
                    // Armature配下のすべてのボーンを取得
                    ProcessBoneHierarchy(costumeArmatureTransform, "", false);
                } else {
                    // スキンメッシュレンダラーのボーンを取得
                    SkinnedMeshRenderer[] renderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (SkinnedMeshRenderer renderer in renderers) {
                        if (renderer.bones != null) {
                            foreach (Transform bone in renderer.bones) {
                                if (bone != null && !costumeBones.Contains(bone)) {
                                    costumeBones.Add(bone);
                                    
                                    // 階層パスを記録
                                    string hierarchyPath = GetHierarchyPath(bone);
                                    costumeBoneHierarchyPaths[bone] = hierarchyPath;
                                    
                                    // パスベースのルックアップテーブルに追加
                                    if (!costumeBonesByPath.ContainsKey(hierarchyPath)) {
                                        costumeBonesByPath[hierarchyPath] = new List<Transform>();
                                    }
                                    costumeBonesByPath[hierarchyPath].Add(bone);
                                }
                            }
                        }
                    }
                    
                    // ルートボーンが含まれていない場合は追加
                    foreach (SkinnedMeshRenderer renderer in renderers) {
                        if (renderer.rootBone != null && !costumeBones.Contains(renderer.rootBone)) {
                            costumeBones.Add(renderer.rootBone);
                            
                            // 階層パスを記録
                            string hierarchyPath = GetHierarchyPath(renderer.rootBone);
                            costumeBoneHierarchyPaths[renderer.rootBone] = hierarchyPath;
                            
                            // パスベースのルックアップテーブルに追加
                            if (!costumeBonesByPath.ContainsKey(hierarchyPath)) {
                                costumeBonesByPath[hierarchyPath] = new List<Transform>();
                            }
                            costumeBonesByPath[hierarchyPath].Add(renderer.rootBone);
                        }
                    }
                    
                    // 他のボーンも追加（必要に応じて）
                    foreach (Transform bone in costumeObject.GetComponentsInChildren<Transform>()) {
                        if (!costumeBones.Contains(bone)) {
                            costumeBones.Add(bone);
                            
                            // 階層パスを記録
                            string hierarchyPath = GetHierarchyPath(bone);
                            costumeBoneHierarchyPaths[bone] = hierarchyPath;
                            
                            // パスベースのルックアップテーブルに追加
                            if (!costumeBonesByPath.ContainsKey(hierarchyPath)) {
                                costumeBonesByPath[hierarchyPath] = new List<Transform>();
                            }
                            costumeBonesByPath[hierarchyPath].Add(bone);
                        }
                    }
                }
            }
            
            // ボーンを階層順にソート
            avatarBones.Sort((a, b) => a.name.CompareTo(b.name));
            costumeBones.Sort((a, b) => a.name.CompareTo(b.name));
            
            // デバッグ情報
            Debug.Log($"アバターボーン数: {avatarBones.Count}, 衣装ボーン数: {costumeBones.Count}");
            Debug.Log($"アバター階層パス数: {avatarBoneHierarchyPaths.Count}, 衣装階層パス数: {costumeBoneHierarchyPaths.Count}");
        }
        
        // ボーン階層を再帰的に処理
        private void ProcessBoneHierarchy(Transform bone, string parentPath, bool isAvatar) {
            if (bone == null) return;
            
            // 現在のボーンのパスを構築
            string currentPath = string.IsNullOrEmpty(parentPath) ? bone.name : parentPath + "/" + bone.name;
            
            // 特定のキーワードを含むボーンは除外
            bool shouldExclude = false;
            foreach (string keyword in exclusionKeywords) {
                if (bone.name.ToLowerInvariant().Contains(keyword.ToLowerInvariant())) {
                    shouldExclude = true;
                    break;
                }
            }
            
            // アバターまたは衣装のボーンリストに追加
            if (!shouldExclude) {
                if (isAvatar) {
                    avatarBones.Add(bone);
                    avatarBoneHierarchyPaths[bone] = currentPath;
                    
                    if (!avatarBonesByPath.ContainsKey(currentPath)) {
                        avatarBonesByPath[currentPath] = new List<Transform>();
                    }
                    avatarBonesByPath[currentPath].Add(bone);
                } else {
                    costumeBones.Add(bone);
                    costumeBoneHierarchyPaths[bone] = currentPath;
                    
                    if (!costumeBonesByPath.ContainsKey(currentPath)) {
                        costumeBonesByPath[currentPath] = new List<Transform>();
                    }
                    costumeBonesByPath[currentPath].Add(bone);
                }
            } else if (isAvatar) {
                // 除外されたボーンを記録（アバターのみ）
                ignoredBones[bone] = true;
            }
            
            // 子ボーンも処理
            foreach (Transform child in bone) {
                ProcessBoneHierarchy(child, currentPath, isAvatar);
            }
        }
        
        // 階層パスを取得
        private string GetHierarchyPath(Transform bone) {
            if (bone == null) return "";
            
            List<string> pathSegments = new List<string>();
            Transform current = bone;
            
            // ルートが見つかるまで上に遡っていく
            while (current != null && current.parent != null) {
                pathSegments.Insert(0, current.name);
                current = current.parent;
                
                // Armatureレベルまで遡ったら停止
                if (current.name == "Armature") {
                    break;
                }
            }
            
            return string.Join("/", pathSegments.ToArray());
        }
        
        // Armatureトランスフォームを探す
        private Transform FindArmatureTransform(Transform root) {
            // 直接「Armature」という名前のものを探す
            if (root.name == "Armature") {
                return root;
            }
            
            // 子オブジェクトを再帰的に探索
            foreach (Transform child in root) {
                if (child.name == "Armature") {
                    return child;
                }
                
                Transform found = FindArmatureTransform(child);
                if (found != null) {
                    return found;
                }
            }
            
            return null; // 見つからなかった場合
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
            
            // メッシュで調整オプションに基づいて処理を分岐
            if (adjustByMesh) {
                // メッシュベースで衣装を適合させる
                AdjustCostumeByMesh(avatarAnimator, activeCostumeInstance);
            } else {
                // 従来のスキンメッシュの転送（ボーンのバインド）
                TransferSkinnedMeshes(avatarAnimator, activeCostumeInstance);
            }
            
            // 初期の微調整値を設定
            SetupAdjustmentValues();
            
            // 更新を強制
            EditorUpdateHelper.ForceUpdate(activeCostumeInstance);
            SceneView.RepaintAll();
            
            Debug.Log("衣装の適用が完了しました");
        }
        
        // メッシュベースで衣装を調整
        private void AdjustCostumeByMesh(Animator avatarAnimator, GameObject costumeInstance) {
            // 衣装のスキンメッシュレンダラーを取得
            SkinnedMeshRenderer[] costumeRenderers = costumeInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            
            // 各メッシュレンダラーごとに処理
            foreach (SkinnedMeshRenderer costumeRenderer in costumeRenderers) {
                if (costumeRenderer == null || costumeRenderer.sharedMesh == null) continue;
                
                // オリジナルのメッシュを保存
                if (!originalMeshes.ContainsKey(costumeRenderer)) {
                    originalMeshes[costumeRenderer] = costumeRenderer.sharedMesh;
                    
                    // 編集用のメッシュを複製
                    costumeRenderer.sharedMesh = Instantiate(costumeRenderer.sharedMesh);
                }
                
                // メッシュのボディパーツを特定
                string partName = IdentifyBodyPart(costumeRenderer);
                
                // 部位ごとに適切なアバターメッシュに合わせて変形
                if (avatarReferencePoints.ContainsKey(partName)) {
                    // 頂点ごとに調整
                    DeformMeshToMatchByPart(costumeRenderer.sharedMesh, partName, avatarReferencePoints[partName]);
                }
                
                // バウンディングボックスの更新
                costumeRenderer.sharedMesh.RecalculateBounds();
                costumeRenderer.sharedMesh.RecalculateNormals();
                
                // 更新を強制
                EditorUpdateHelper.ForceUpdate(costumeRenderer);
            }
        }
        
        // 部位ごとにメッシュを変形
        private void DeformMeshToMatchByPart(Mesh costumeMesh, string partName, ReferencePoint avatarRefPoint) {
            if (costumeMesh == null) return;
            
            // メッシュの頂点を取得
            Vector3[] costumeVertices = costumeMesh.vertices;
            if (costumeVertices.Length == 0) return;
            
            // 現在のバウンディングボックスを計算
            Bounds costumeBounds = costumeMesh.bounds;
            
            // 頂点ごとに部位に応じた変形を適用
            for (int i = 0; i < costumeVertices.Length; i++) {
                // 頂点の相対位置を計算（-1～1の範囲）
                Vector3 normalizedPos = (costumeVertices[i] - costumeBounds.center);
                normalizedPos.x /= (costumeBounds.size.x * 0.5f);
                normalizedPos.y /= (costumeBounds.size.y * 0.5f);
                normalizedPos.z /= (costumeBounds.size.z * 0.5f);
                
                // 部位ごとの適切なスケーリングを行う
                switch (partName) {
                    case "頭":
                        // 頭部の調整（頭の形に合わせて変形）
                        costumeVertices[i] = DeformVertex(normalizedPos, avatarRefPoint, costumeBounds, 1.0f, 1.0f, 1.0f);
                        break;
                        
                    case "胴体":
                        // 胴体の調整（胴体の形に合わせて変形）
                        costumeVertices[i] = DeformVertex(normalizedPos, avatarRefPoint, costumeBounds, 1.0f, 1.0f, 1.0f);
                        break;
                        
                    case "左腕":
                        // 左腕の調整（左腕の形に合わせて変形）
                        costumeVertices[i] = DeformVertex(normalizedPos, avatarRefPoint, costumeBounds, 1.0f, 1.0f, 1.0f);
                        break;
                        
                    case "右腕":
                        // 右腕の調整（右腕の形に合わせて変形）
                        costumeVertices[i] = DeformVertex(normalizedPos, avatarRefPoint, costumeBounds, 1.0f, 1.0f, 1.0f);
                        break;
                        
                    case "左脚":
                        // 左脚の調整（左脚の形に合わせて変形）
                        costumeVertices[i] = DeformVertex(normalizedPos, avatarRefPoint, costumeBounds, 1.0f, 1.0f, 1.0f);
                        break;
                        
                    case "右脚":
                        // 右脚の調整（右脚の形に合わせて変形）
                        costumeVertices[i] = DeformVertex(normalizedPos, avatarRefPoint, costumeBounds, 1.0f, 1.0f, 1.0f);
                        break;
                        
                    default:
                        // その他の部位（基本的な変形のみ）
                        costumeVertices[i] = DeformVertex(normalizedPos, avatarRefPoint, costumeBounds, 1.0f, 1.0f, 1.0f);
                        break;
                }
            }
            
            // 頂点位置の更新を適用
            costumeMesh.vertices = costumeVertices;
        }
        
        // 頂点を変形する処理
        private Vector3 DeformVertex(Vector3 normalizedPos, ReferencePoint refPoint, Bounds originalBounds, float xStrength, float yStrength, float zStrength) {
            // アバターの参照点に基づいて変形
            Vector3 targetSize = refPoint.extents * 2.0f; // アバターの部位サイズ
            
            // 正規化座標をアバターのサイズに合わせて変形
            Vector3 newPos = new Vector3(
                normalizedPos.x * targetSize.x * 0.5f * xStrength,
                normalizedPos.y * targetSize.y * 0.5f * yStrength,
                normalizedPos.z * targetSize.z * 0.5f * zStrength
            );
            
            // アバターの部位の中心に頂点を配置
            newPos += refPoint.center;
            
            return newPos;
        }
        
        private void TransferSkinnedMeshes(Animator avatarAnimator, GameObject costumeInstance) {
            // 衣装のスキンメッシュレンダラーを取得
            SkinnedMeshRenderer[] costumeRenderers = costumeInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            
            // 衣装のボーンインスタンスを取得
            Dictionary<string, Transform> costumeBonesDict = new Dictionary<string, Transform>();
            foreach (Transform bone in costumeInstance.GetComponentsInChildren<Transform>()) {
                costumeBonesDict[bone.name] = bone;
            }
            
            // 手動マッピングの逆引き辞書を作成
            Dictionary<Transform, Transform> reverseMappings = new Dictionary<Transform, Transform>();
            foreach (var pair in manualBoneMapping) {
                if (pair.Value != null) {
                    string boneName = pair.Value.name;
                    if (costumeBonesDict.ContainsKey(boneName)) {
                        Transform instanceBone = costumeBonesDict[boneName];
                        reverseMappings[instanceBone] = pair.Key;
                    }
                }
            }
            
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
                    
                    // 手動マッピングがあるか確認
                    if (reverseMappings.TryGetValue(originalBones[i], out Transform mappedAvatarBone)) {
                        newBones[i] = mappedAvatarBone;
                        continue;
                    }
                    
                    // 自動マッピングを試行
                    Transform avatarBone = FindCorrespondingAvatarBone(avatarAnimator, originalBones[i]);
                    
                    if (avatarBone != null) {
                        newBones[i] = avatarBone;
                    } else {
                        Debug.LogWarning($"ボーン '{originalBones[i].name}' がアバターに見つかりませんでした");
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
                                Debug.Log($"ボーン '{originalBones[i].name}' の代わりに '{boneType}' を使用します");
                                break;
                            }
                        }
                    }
                }
                
                // 新しいボーン配列を適用
                costumeRenderer.bones = newBones;
                
                // ルートボーンをアバターの対応するボーンに設定
                if (costumeRenderer.rootBone != null) {
                    // 手動マッピングがあるか確認
                    if (reverseMappings.TryGetValue(costumeRenderer.rootBone, out Transform mappedAvatarBone)) {
                        costumeRenderer.rootBone = mappedAvatarBone;
                    } else {
                        // 自動マッピングを試行
                        Transform avatarRootBone = FindCorrespondingAvatarBone(avatarAnimator, costumeRenderer.rootBone);
                        
                        if (avatarRootBone != null) {
                            costumeRenderer.rootBone = avatarRootBone;
                        } else {
                            // ルートボーンが見つからない場合は、Hipsなど主要なボーンを代わりに使用
                            costumeRenderer.rootBone = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
                        }
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
        
        // 階層とボーン名を考慮してアバターボーンを見つける
        private Transform FindCorrespondingAvatarBone(Animator avatarAnimator, Transform costumeBone) {
            if (costumeBone == null) return null;

            // 1. 名前が完全一致するボーンを探す
            foreach (Transform bone in avatarBones) {
                if (bone.name == costumeBone.name) {
                    return bone;
                }
            }
            
            // 2. 階層パスが登録されているか確認
            if (costumeBoneHierarchyPaths.ContainsKey(costumeBone)) {
                string costumePath = costumeBoneHierarchyPaths[costumeBone];
                string[] costumePathSegments = costumePath.Split('/');
                
                // 同じ階層深さのボーンを候補として検索
                List<Transform> depthMatchedBones = new List<Transform>();
                
                foreach (var bone in avatarBones) {
                    if (avatarBoneHierarchyPaths.ContainsKey(bone)) {
                        string avatarPath = avatarBoneHierarchyPaths[bone];
                        string[] avatarPathSegments = avatarPath.Split('/');
                        
                        // 階層の深さが一致した場合、候補に追加
                        if (avatarPathSegments.Length == costumePathSegments.Length) {
                            depthMatchedBones.Add(bone);
                        }
                    }
                }
                
                // 候補ボーンの中から、親の名前も似ているものを優先
                if (depthMatchedBones.Count > 0) {
                    Transform bestMatch = null;
                    float bestScore = 0;
                    
                    foreach (var bone in depthMatchedBones) {
                        string avatarPath = avatarBoneHierarchyPaths[bone];
                        string[] avatarPathSegments = avatarPath.Split('/');
                        
                        // 親子関係の類似度スコアを計算
                        float score = CalculateHierarchySimilarityScore(avatarPathSegments, costumePathSegments);
                        
                        if (score > bestScore) {
                            bestScore = score;
                            bestMatch = bone;
                        }
                    }
                    
                    // 十分な類似度があればマッピング
                    if (bestScore > 0.5f) {
                        return bestMatch;
                    }
                }
            }
            
            // 3. 正規化された名前で比較
            string normalizedCostumeName = AvatarUtility.NormalizeBoneName(costumeBone.name).ToLowerInvariant();
            foreach (Transform bone in avatarBones) {
                string normalizedAvatarName = AvatarUtility.NormalizeBoneName(bone.name).ToLowerInvariant();
                if (normalizedAvatarName == normalizedCostumeName || 
                    normalizedAvatarName.Contains(normalizedCostumeName) || 
                    normalizedCostumeName.Contains(normalizedAvatarName)) {
                    return bone;
                }
            }
            
            // 4. ヒューマノイドボーンから推定
            Transform humanoidMatch = AvatarUtility.GetHumanoidBone(avatarAnimator, costumeBone.name);
            if (humanoidMatch != null) {
                return humanoidMatch;
            }
            
            // 5. 位置ベースのマッピング（最終手段）
            Vector3 costumeBonePos = costumeBone.position;
            Transform closestBone = null;
            float closestDistance = float.MaxValue;
            
            foreach (Transform bone in avatarBones) {
                // 除外対象のボーンはスキップ
                if (ignoredBones.ContainsKey(bone) && ignoredBones[bone]) {
                    continue;
                }
                
                float distance = Vector3.Distance(bone.position, costumeBonePos);
                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestBone = bone;
                }
            }
            
            // 距離が近すぎる場合のみ返す (一定以上離れていると関係ないボーン)
            if (closestDistance < 0.5f) {
                return closestBone;
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