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
            { "chest", "Chest" },
            { "neck", "Neck" },
            { "head", "Head" },
            { "leftarm", "LeftUpperArm" },
            { "left_arm", "LeftUpperArm" },
            { "leftforearm", "LeftLowerArm" },
            { "left_forearm", "LeftLowerArm" },
            { "lefthand", "LeftHand" },
            { "left_hand", "LeftHand" },
            { "rightarm", "RightUpperArm" },
            { "right_arm", "RightUpperArm" },
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
            { "rightupleg", "RightUpperLeg" },
            { "right_upleg", "RightUpperLeg" },
            { "rightleg", "RightLowerLeg" },
            { "right_leg", "RightLowerLeg" },
            { "rightfoot", "RightFoot" },
            { "right_foot", "RightFoot" }
        };

        // ボーン名の正規化（大文字小文字や特殊文字を無視して比較）
        public static string NormalizeBoneName(string boneName) {
            if (string.IsNullOrEmpty(boneName)) return boneName;
            
            // 空白、アンダースコア、ハイフンを除去し、小文字に変換
            string normalized = boneName.ToLowerInvariant()
                                       .Replace(" ", "")
                                       .Replace("_", "")
                                       .Replace("-", "");
            
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
            
            return null;
        }
        
        // メッシュのスキンウェイトを調整する関数
        public static void AdjustSkinWeights(SkinnedMeshRenderer renderer) {
            if (renderer == null || renderer.sharedMesh == null) return;
            
            Mesh mesh = renderer.sharedMesh;
            
            // ボーンウェイトをバランス良く調整
            BoneWeight[] weights = mesh.boneWeights;
            
            for (int i = 0; i < weights.Length; i++) {
                // ウェイトの合計が1になるように正規化
                float total = weights[i].weight0 + weights[i].weight1 + weights[i].weight2 + weights[i].weight3;
                
                if (total > 0) {
                    weights[i].weight0 /= total;
                    weights[i].weight1 /= total;
                    weights[i].weight2 /= total;
                    weights[i].weight3 /= total;
                }
            }
            
            mesh.boneWeights = weights;
        }
        
        // 衣装用の基本的なブレンドシェイプを作成
        public static void CreateBasicBlendShapes(SkinnedMeshRenderer renderer, float fatAmount = 0.1f, float thinAmount = 0.1f) {
            if (renderer == null || renderer.sharedMesh == null) return;
            
            Mesh mesh = renderer.sharedMesh;
            Vector3[] baseVertices = mesh.vertices;
            
            // 「太い」ブレンドシェイプ
            Vector3[] fatVertices = new Vector3[baseVertices.Length];
            for (int i = 0; i < baseVertices.Length; i++) {
                // 中心から外側に向かって膨らませる
                Vector3 direction = (baseVertices[i] - mesh.bounds.center).normalized;
                fatVertices[i] = baseVertices[i] + direction * fatAmount;
            }
            
            // 「細い」ブレンドシェイプ
            Vector3[] thinVertices = new Vector3[baseVertices.Length];
            for (int i = 0; i < baseVertices.Length; i++) {
                // 中心に向かって縮める
                Vector3 direction = (baseVertices[i] - mesh.bounds.center).normalized;
                thinVertices[i] = baseVertices[i] - direction * thinAmount;
            }
            
            // ブレンドシェイプの追加
            mesh.AddBlendShapeFrame("Fat", 100f, fatVertices, null, null);
            mesh.AddBlendShapeFrame("Thin", 100f, thinVertices, null, null);
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
                
                // スケール比率を計算
                float scaleRatio = avatarHeight / costumeHeight;
                
                // 衣装全体にスケールを適用
                costume.transform.localScale *= scaleRatio;
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