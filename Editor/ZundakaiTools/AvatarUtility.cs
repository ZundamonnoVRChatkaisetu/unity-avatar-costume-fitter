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
            
            // これまでの方法で見つからなかった場合、階層から検索
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
                    // デフォルトのスケールを設定
                    costume.transform.localScale = Vector3.one;
                    Debug.LogWarning("適切なボーンが見つからないため、デフォルトスケールを適用しました。");
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
    }
}