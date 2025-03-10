using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    public static class AvatarUtility {
        // ボーンの対応関係を定義した辞書（汎用的な名前から一般的な名前への変換）
        private static readonly Dictionary<string, string> BoneNameMap = new Dictionary<string, string>() {
            // 基本的なボーン対応表（必要に応じて拡張）
            { "hip", "Hips" },
            { "hips", "Hips" },
            { "pelvis", "Hips" },
            { "spine", "Spine" },
            { "spine1", "Spine" },
            { "spine2", "Chest" },
            { "chest", "Chest" },
            { "upperchest", "UpperChest" },
            { "spine3", "UpperChest" },
            { "neck", "Neck" },
            { "head", "Head" },
            { "leftarm", "LeftUpperArm" },
            { "left_arm", "LeftUpperArm" },
            { "leftshoulder", "LeftShoulder" },
            { "left_shoulder", "LeftShoulder" },
            { "leftforearm", "LeftLowerArm" },
            { "left_forearm", "LeftLowerArm" },
            { "lefthand", "LeftHand" },
            { "left_hand", "LeftHand" },
            { "rightarm", "RightUpperArm" },
            { "right_arm", "RightUpperArm" },
            { "rightshoulder", "RightShoulder" },
            { "right_shoulder", "RightShoulder" },
            { "rightforearm", "RightLowerArm" },
            { "right_forearm", "RightLowerArm" },
            { "righthand", "RightHand" },
            { "right_hand", "RightHand" },
            { "leftupleg", "LeftUpperLeg" },
            { "left_upleg", "LeftUpperLeg" },
            { "leftleg", "LeftLowerLeg" },
            { "left_leg", "LeftLowerLeg" },
            { "leftfoot", "LeftFoot" },
            { "left_foot", "LeftFoot" },
            { "lefttooe", "LeftToes" },
            { "left_toe", "LeftToes" },
            { "rightupleg", "RightUpperLeg" },
            { "right_upleg", "RightUpperLeg" },
            { "rightleg", "RightLowerLeg" },
            { "right_leg", "RightLowerLeg" },
            { "rightfoot", "RightFoot" },
            { "right_foot", "RightFoot" },
            { "righttoe", "RightToes" },
            { "right_toe", "RightToes" }
        };

        // 標準ボーンの相対位置マップ（人型アバターの一般的な階層構造を表現）
        private static readonly Dictionary<HumanBodyBones, Vector3> StandardBonePositions = new Dictionary<HumanBodyBones, Vector3>() {
            { HumanBodyBones.Hips, new Vector3(0, 1.0f, 0) },
            { HumanBodyBones.Spine, new Vector3(0, 1.2f, 0) },
            { HumanBodyBones.Chest, new Vector3(0, 1.4f, 0) },
            { HumanBodyBones.UpperChest, new Vector3(0, 1.6f, 0) },
            { HumanBodyBones.Neck, new Vector3(0, 1.7f, 0) },
            { HumanBodyBones.Head, new Vector3(0, 1.8f, 0) },
            { HumanBodyBones.LeftShoulder, new Vector3(-0.2f, 1.6f, 0) },
            { HumanBodyBones.LeftUpperArm, new Vector3(-0.3f, 1.6f, 0) },
            { HumanBodyBones.LeftLowerArm, new Vector3(-0.5f, 1.4f, 0) },
            { HumanBodyBones.LeftHand, new Vector3(-0.7f, 1.2f, 0) },
            { HumanBodyBones.RightShoulder, new Vector3(0.2f, 1.6f, 0) },
            { HumanBodyBones.RightUpperArm, new Vector3(0.3f, 1.6f, 0) },
            { HumanBodyBones.RightLowerArm, new Vector3(0.5f, 1.4f, 0) },
            { HumanBodyBones.RightHand, new Vector3(0.7f, 1.2f, 0) },
            { HumanBodyBones.LeftUpperLeg, new Vector3(-0.1f, 0.9f, 0) },
            { HumanBodyBones.LeftLowerLeg, new Vector3(-0.1f, 0.5f, 0) },
            { HumanBodyBones.LeftFoot, new Vector3(-0.1f, 0.1f, 0.1f) },
            { HumanBodyBones.LeftToes, new Vector3(-0.1f, 0.0f, 0.2f) },
            { HumanBodyBones.RightUpperLeg, new Vector3(0.1f, 0.9f, 0) },
            { HumanBodyBones.RightLowerLeg, new Vector3(0.1f, 0.5f, 0) },
            { HumanBodyBones.RightFoot, new Vector3(0.1f, 0.1f, 0.1f) },
            { HumanBodyBones.RightToes, new Vector3(0.1f, 0.0f, 0.2f) }
        };

        // ボーン名の正規化（大文字小文字や特殊文字を無視して比較）
        public static string NormalizeBoneName(string boneName) {
            if (string.IsNullOrEmpty(boneName)) return boneName;
            
            // 空白、アンダースコア、ハイフン、数字を除去し、小文字に変換
            string normalized = boneName.ToLowerInvariant()
                                       .Replace(" ", "")
                                       .Replace("_", "")
                                       .Replace("-", "")
                                       .Replace("0", "")
                                       .Replace("1", "")
                                       .Replace("2", "")
                                       .Replace("3", "")
                                       .Replace("4", "")
                                       .Replace("5", "")
                                       .Replace("6", "")
                                       .Replace("7", "")
                                       .Replace("8", "")
                                       .Replace("9", "");
            
            // 一般的な対応表から変換
            if (BoneNameMap.TryGetValue(normalized, out string mappedName)) {
                return mappedName;
            }
            
            return boneName;
        }
        
        // アバターのHumanoidボーンを取得（名前の正規化を使用）
        public static Transform GetHumanoidBone(Animator animator, string boneName) {
            if (animator == null || !animator.isHuman) return null;
            
            string normalizedName = NormalizeBoneName(boneName).ToLowerInvariant();
            
            // すべてのHumanoidボーンタイプをチェック
            foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                if (boneType == HumanBodyBones.LastBone) continue;
                
                Transform bone = animator.GetBoneTransform(boneType);
                if (bone != null) {
                    string currentBoneName = NormalizeBoneName(bone.name).ToLowerInvariant();
                    
                    // 名前が一致するか確認
                    if (currentBoneName == normalizedName || 
                        currentBoneName.Contains(normalizedName) || 
                        normalizedName.Contains(currentBoneName)) {
                        return bone;
                    }
                    
                    // ボーンタイプの名前との比較も試す
                    string boneTypeName = boneType.ToString().ToLowerInvariant();
                    if (boneTypeName == normalizedName || 
                        boneTypeName.Contains(normalizedName) || 
                        normalizedName.Contains(boneTypeName)) {
                        return bone;
                    }
                }
            }
            
            // 位置に基づいたボーンマッピング（名前で見つからない場合）
            Transform possibleBone = FindBoneByPosition(animator, normalizedName);
            if (possibleBone != null) {
                return possibleBone;
            }
            
            // 階層から検索
            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(animator.transform);
            
            while (queue.Count > 0) {
                Transform current = queue.Dequeue();
                string currentNormalized = NormalizeBoneName(current.name).ToLowerInvariant();
                
                if (currentNormalized.Contains(normalizedName) || normalizedName.Contains(currentNormalized)) {
                    return current;
                }
                
                // 子を追加
                foreach (Transform child in current) {
                    queue.Enqueue(child);
                }
            }
            
            return null;
        }

        // ボーンの推定位置から最も近いボーンを探す
        private static Transform FindBoneByPosition(Animator animator, string normalizedBoneName) {
            // ボーン名から推定されるボーンタイプを取得
            HumanBodyBones estimatedBoneType = EstimateBoneTypeFromName(normalizedBoneName);
            if (estimatedBoneType == HumanBodyBones.LastBone) return null;

            // 標準ボーンの位置を取得
            if (!StandardBonePositions.TryGetValue(estimatedBoneType, out Vector3 standardPosition)) {
                return null;
            }

            // アバターのスケールを考慮
            float avatarHeight = GetAvatarHeight(animator);
            float scale = avatarHeight / 2.0f; // 標準的なアバターの高さを2.0とする

            // ルート座標でのターゲット位置を計算
            Vector3 targetPosition = standardPosition * scale;

            // 最も近いボーンを探す
            Transform closestBone = null;
            float closestDistance = float.MaxValue;

            // 再帰的にすべてのボーンを検索
            SearchClosestBoneByPosition(animator.transform, targetPosition, ref closestBone, ref closestDistance);

            return closestBone;
        }
        
        // 位置を指定して最も近いボーンを探す（パブリックメソッド）
        public static Transform FindBoneByPosition(Transform root, Vector3 position, float maxDistance = float.MaxValue) {
            Transform closestBone = null;
            float closestDistance = maxDistance;
            
            // 再帰的に検索
            SearchClosestBoneByPosition(root, position, ref closestBone, ref closestDistance);
            
            return closestBone;
        }

        // 名前からボーンタイプを推定
        private static HumanBodyBones EstimateBoneTypeFromName(string normalizedBoneName) {
            normalizedBoneName = normalizedBoneName.ToLowerInvariant();

            if (normalizedBoneName.Contains("hip")) return HumanBodyBones.Hips;
            if (normalizedBoneName.Contains("spine") && !normalizedBoneName.Contains("spine2") && !normalizedBoneName.Contains("spine3")) return HumanBodyBones.Spine;
            if (normalizedBoneName.Contains("spine2") || normalizedBoneName.Contains("chest")) return HumanBodyBones.Chest;
            if (normalizedBoneName.Contains("spine3") || normalizedBoneName.Contains("upperchest")) return HumanBodyBones.UpperChest;
            if (normalizedBoneName.Contains("neck")) return HumanBodyBones.Neck;
            if (normalizedBoneName.Contains("head")) return HumanBodyBones.Head;
            
            if (normalizedBoneName.Contains("leftshoulder")) return HumanBodyBones.LeftShoulder;
            if (normalizedBoneName.Contains("leftarm") || normalizedBoneName.Contains("leftupper")) return HumanBodyBones.LeftUpperArm;
            if (normalizedBoneName.Contains("leftforearm") || normalizedBoneName.Contains("leftlower")) return HumanBodyBones.LeftLowerArm;
            if (normalizedBoneName.Contains("lefthand")) return HumanBodyBones.LeftHand;
            
            if (normalizedBoneName.Contains("rightshoulder")) return HumanBodyBones.RightShoulder;
            if (normalizedBoneName.Contains("rightarm") || normalizedBoneName.Contains("rightupper")) return HumanBodyBones.RightUpperArm;
            if (normalizedBoneName.Contains("rightforearm") || normalizedBoneName.Contains("rightlower")) return HumanBodyBones.RightLowerArm;
            if (normalizedBoneName.Contains("righthand")) return HumanBodyBones.RightHand;
            
            if (normalizedBoneName.Contains("leftupleg") || normalizedBoneName.Contains("leftthigh")) return HumanBodyBones.LeftUpperLeg;
            if (normalizedBoneName.Contains("leftleg") || normalizedBoneName.Contains("leftcalf")) return HumanBodyBones.LeftLowerLeg;
            if (normalizedBoneName.Contains("leftfoot")) return HumanBodyBones.LeftFoot;
            if (normalizedBoneName.Contains("lefttoe")) return HumanBodyBones.LeftToes;
            
            if (normalizedBoneName.Contains("rightupleg") || normalizedBoneName.Contains("rightthigh")) return HumanBodyBones.RightUpperLeg;
            if (normalizedBoneName.Contains("rightleg") || normalizedBoneName.Contains("rightcalf")) return HumanBodyBones.RightLowerLeg;
            if (normalizedBoneName.Contains("rightfoot")) return HumanBodyBones.RightFoot;
            if (normalizedBoneName.Contains("righttoe")) return HumanBodyBones.RightToes;

            return HumanBodyBones.LastBone;
        }

        // アバターの身長を取得
        private static float GetAvatarHeight(Animator animator) {
            if (animator == null) return 2.0f;

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

            if (hips != null && head != null) {
                return Vector3.Distance(head.position, hips.position) * 2.0f;
            }

            return 2.0f; // デフォルト値
        }

        // 位置ベースで最も近いボーンを再帰的に探す
        private static void SearchClosestBoneByPosition(Transform current, Vector3 targetPosition, ref Transform closestBone, ref float closestDistance) {
            // 現在のボーンとターゲット位置の距離を計算
            float distance = Vector3.Distance(current.position, targetPosition);
            
            // より近いボーンが見つかった場合は更新
            if (distance < closestDistance) {
                closestDistance = distance;
                closestBone = current;
            }
            
            // 子ボーンも検索
            foreach (Transform child in current) {
                SearchClosestBoneByPosition(child, targetPosition, ref closestBone, ref closestDistance);
            }
        }
        
        // メッシュのスキンウェイトを調整する関数
        public static void AdjustSkinWeights(SkinnedMeshRenderer renderer) {
            if (renderer == null || renderer.sharedMesh == null) return;
            
            // メッシュを複製して編集可能にする
            Mesh originalMesh = renderer.sharedMesh;
            Mesh newMesh = Object.Instantiate(originalMesh);
            renderer.sharedMesh = newMesh;
            
            // ボーンウェイトをバランス良く調整
            BoneWeight[] weights = newMesh.boneWeights;
            
            for (int i = 0; i < weights.Length; i++) {
                // ウェイトの合計が1になるように正規化
                float total = weights[i].weight0 + weights[i].weight1 + weights[i].weight2 + weights[i].weight3;
                
                if (total > 0) {
                    weights[i].weight0 /= total;
                    weights[i].weight1 /= total;
                    weights[i].weight2 /= total;
                    weights[i].weight3 /= total;
                } else {
                    // ウェイトがない場合は、最初のボーンに100%ウェイトを設定
                    weights[i].weight0 = 1.0f;
                    weights[i].weight1 = 0.0f;
                    weights[i].weight2 = 0.0f;
                    weights[i].weight3 = 0.0f;
                }
            }
            
            newMesh.boneWeights = weights;
            newMesh.RecalculateBounds();
        }
        
        // 衣装用の基本的なブレンドシェイプを作成
        public static void CreateBasicBlendShapes(SkinnedMeshRenderer renderer, float fatAmount = 0.1f, float thinAmount = 0.1f) {
            if (renderer == null || renderer.sharedMesh == null) return;
            
            // メッシュを複製して編集可能にする
            Mesh originalMesh = renderer.sharedMesh;
            Mesh newMesh = Object.Instantiate(originalMesh);
            renderer.sharedMesh = newMesh;
            
            Vector3[] baseVertices = newMesh.vertices;
            Vector3[] normals = newMesh.normals;
            
            // 「太い」ブレンドシェイプ
            Vector3[] fatVertices = new Vector3[baseVertices.Length];
            Vector3[] fatNormals = new Vector3[normals.Length];
            
            for (int i = 0; i < baseVertices.Length; i++) {
                // 中心から外側に向かって膨らませる（法線方向を利用）
                if (i < normals.Length) {
                    fatVertices[i] = baseVertices[i] + normals[i] * fatAmount;
                    fatNormals[i] = normals[i];
                } else {
                    // 中心から半径方向に膨らませる（法線がない場合）
                    Vector3 direction = (baseVertices[i] - newMesh.bounds.center).normalized;
                    fatVertices[i] = baseVertices[i] + direction * fatAmount;
                    fatNormals[i] = direction;
                }
            }
            
            // 「細い」ブレンドシェイプ
            Vector3[] thinVertices = new Vector3[baseVertices.Length];
            Vector3[] thinNormals = new Vector3[normals.Length];
            
            for (int i = 0; i < baseVertices.Length; i++) {
                // 中心に向かって縮める（法線と逆方向を利用）
                if (i < normals.Length) {
                    thinVertices[i] = baseVertices[i] - normals[i] * thinAmount;
                    thinNormals[i] = normals[i];
                } else {
                    // 中心方向に縮める（法線がない場合）
                    Vector3 direction = (baseVertices[i] - newMesh.bounds.center).normalized;
                    thinVertices[i] = baseVertices[i] - direction * thinAmount;
                    thinNormals[i] = direction;
                }
            }
            
            // ブレンドシェイプの追加（既存のブレンドシェイプをクリア）
            newMesh.ClearBlendShapes();
            
            // 体型ブレンドシェイプの追加
            newMesh.AddBlendShapeFrame("Fat", 100f, fatVertices, fatNormals, null);
            newMesh.AddBlendShapeFrame("Thin", 100f, thinVertices, thinNormals, null);
        }
        
        // アバターのサイズに基づいて衣装を自動スケーリング
        public static void AutoScaleCostumeToBones(GameObject avatar, GameObject costume) {
            if (avatar == null || costume == null) return;
            
            Animator avatarAnimator = avatar.GetComponent<Animator>();
            if (avatarAnimator == null || !avatarAnimator.isHuman) return;
            
            // 主要なボーンの位置から衣装のスケールを推定
            Transform avatarHips = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform avatarHead = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
            
            if (avatarHips == null || avatarHead == null) return;
            
            // アバターの身長を計算
            float avatarHeight = Vector3.Distance(avatarHips.position, avatarHead.position);
            
            // 衣装内の類似ボーンを検索
            Transform costumeHips = FindSimilarBone(costume.transform, "hip");
            Transform costumeHead = FindSimilarBone(costume.transform, "head");
            
            if (costumeHips != null && costumeHead != null) {
                // 衣装の身長を計算
                float costumeHeight = Vector3.Distance(costumeHips.position, costumeHead.position);
                
                if (costumeHeight > 0.001f) {  // 0除算を防ぐ
                    // スケール比率を計算
                    float scaleRatio = avatarHeight / costumeHeight;
                    
                    // 衣装全体にスケールを適用（ユニフォームスケール）
                    costume.transform.localScale = new Vector3(scaleRatio, scaleRatio, scaleRatio);
                    Debug.Log($"衣装のスケールを調整しました: {scaleRatio}");
                }
            } else {
                // Hips-Headの距離が計算できない場合は、体の幅で試す
                Transform avatarLeftUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform avatarRightUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                
                if (avatarLeftUpperArm != null && avatarRightUpperArm != null) {
                    float avatarWidth = Vector3.Distance(avatarLeftUpperArm.position, avatarRightUpperArm.position);
                    
                    Transform costumeLeftArm = FindSimilarBone(costume.transform, "leftarm");
                    Transform costumeRightArm = FindSimilarBone(costume.transform, "rightarm");
                    
                    if (costumeLeftArm != null && costumeRightArm != null) {
                        float costumeWidth = Vector3.Distance(costumeLeftArm.position, costumeRightArm.position);
                        
                        if (costumeWidth > 0.001f) {  // 0除算を防ぐ
                            float scaleRatio = avatarWidth / costumeWidth;
                            costume.transform.localScale = new Vector3(scaleRatio, scaleRatio, scaleRatio);
                            Debug.Log($"衣装のスケールを幅に基づいて調整しました: {scaleRatio}");
                        }
                    }
                } else {
                    // スキンメッシュレンダラーのバウンディングボックスを利用
                    SkinnedMeshRenderer[] avatarRenderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>();
                    SkinnedMeshRenderer[] costumeRenderers = costume.GetComponentsInChildren<SkinnedMeshRenderer>();
                    
                    if (avatarRenderers.Length > 0 && costumeRenderers.Length > 0) {
                        Bounds avatarBounds = avatarRenderers[0].bounds;
                        Bounds costumeBounds = costumeRenderers[0].bounds;
                        
                        for (int i = 1; i < avatarRenderers.Length; i++) {
                            avatarBounds.Encapsulate(avatarRenderers[i].bounds);
                        }
                        
                        for (int i = 1; i < costumeRenderers.Length; i++) {
                            costumeBounds.Encapsulate(costumeRenderers[i].bounds);
                        }
                        
                        float avatarSize = avatarBounds.size.y;
                        float costumeSize = costumeBounds.size.y;
                        
                        if (costumeSize > 0.001f) {
                            float scaleRatio = avatarSize / costumeSize;
                            costume.transform.localScale = new Vector3(scaleRatio, scaleRatio, scaleRatio);
                            Debug.Log($"衣装のスケールをバウンディングボックスに基づいて調整しました: {scaleRatio}");
                        }
                    } else {
                        // デフォルトのスケールを設定
                        costume.transform.localScale = Vector3.one;
                        Debug.LogWarning("適切なボーンが見つからないため、デフォルトスケールを適用しました。");
                    }
                }
            }
        }
        
        // 類似したボーンを検索
        private static Transform FindSimilarBone(Transform root, string boneName) {
            string normalizedName = NormalizeBoneName(boneName).ToLowerInvariant();
            
            // 幅優先探索でボーンを探索
            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(root);
            
            while (queue.Count > 0) {
                Transform current = queue.Dequeue();
                
                // 名前を比較
                string currentName = NormalizeBoneName(current.name).ToLowerInvariant();
                if (currentName.Contains(normalizedName) || normalizedName.Contains(currentName)) {
                    return current;
                }
                
                // 子をキューに追加
                foreach (Transform child in current) {
                    queue.Enqueue(child);
                }
            }
            
            return null;
        }

        // アバターボーンのマッピングを取得（ボーン構造を分析）
        public static Dictionary<string, Transform> GetAvatarBoneMapping(Animator avatarAnimator) {
            Dictionary<string, Transform> boneMapping = new Dictionary<string, Transform>();
            
            if (avatarAnimator == null || !avatarAnimator.isHuman) {
                return boneMapping;
            }
            
            // すべてのHumanoidボーンをマッピング
            foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                if (boneType == HumanBodyBones.LastBone) continue;
                
                Transform bone = avatarAnimator.GetBoneTransform(boneType);
                if (bone != null) {
                    string boneName = bone.name;
                    string normalizedName = NormalizeBoneName(boneName).ToLowerInvariant();
                    
                    // 名前が被らないように注意
                    if (!boneMapping.ContainsKey(boneName)) {
                        boneMapping[boneName] = bone;
                    }
                    
                    // 正規化された名前でも登録
                    if (!boneMapping.ContainsKey(normalizedName)) {
                        boneMapping[normalizedName] = bone;
                    }
                    
                    // ボーンタイプの名前でも登録
                    string boneTypeName = boneType.ToString();
                    if (!boneMapping.ContainsKey(boneTypeName)) {
                        boneMapping[boneTypeName] = bone;
                    }
                }
            }
            
            return boneMapping;
        }

        // 衣装のメッシュを一時的に編集する（微調整用）
        public static void ModifyMeshTemporarily(SkinnedMeshRenderer renderer, Vector3 offset, string boneName = null) {
            if (renderer == null || renderer.sharedMesh == null) return;
            
            // 頂点オフセットを適用するための一時データ
            Vector3[] vertices = renderer.sharedMesh.vertices;
            Vector3[] modifiedVertices = new Vector3[vertices.Length];
            
            // ボーンウェイトを取得
            BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            
            // ボーンIDを特定（指定されたボーン名がある場合）
            int targetBoneIndex = -1;
            if (!string.IsNullOrEmpty(boneName)) {
                string normalizedBoneName = NormalizeBoneName(boneName).ToLowerInvariant();
                for (int i = 0; i < renderer.bones.Length; i++) {
                    if (renderer.bones[i] != null) {
                        string currentBoneName = NormalizeBoneName(renderer.bones[i].name).ToLowerInvariant();
                        if (currentBoneName.Contains(normalizedBoneName) || normalizedBoneName.Contains(currentBoneName)) {
                            targetBoneIndex = i;
                            break;
                        }
                    }
                }
            }
            
            // 頂点ごとに処理
            for (int i = 0; i < vertices.Length; i++) {
                if (targetBoneIndex >= 0) {
                    // 特定のボーンに関連する頂点のみを変更
                    float influenceWeight = 0f;
                    
                    if (i < weights.Length) {
                        BoneWeight weight = weights[i];
                        
                        if (weight.boneIndex0 == targetBoneIndex) influenceWeight = weight.weight0;
                        else if (weight.boneIndex1 == targetBoneIndex) influenceWeight = weight.weight1;
                        else if (weight.boneIndex2 == targetBoneIndex) influenceWeight = weight.weight2;
                        else if (weight.boneIndex3 == targetBoneIndex) influenceWeight = weight.weight3;
                    }
                    
                    // ウェイトに基づいてオフセットを適用
                    modifiedVertices[i] = vertices[i] + offset * influenceWeight;
                } else {
                    // すべての頂点を変更
                    modifiedVertices[i] = vertices[i] + offset;
                }
            }
            
            // 変更済みの頂点を適用
            renderer.sharedMesh.vertices = modifiedVertices;
            renderer.sharedMesh.RecalculateBounds();
        }
    }
}