using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    /// <summary>
    /// 異なるボーン構造間の互換性を処理するクラス
    /// </summary>
    public class BoneStructureAdapter {
        // アバターと衣装のボーン階層を保存
        private Dictionary<string, BoneNode> avatarBoneHierarchy = new Dictionary<string, BoneNode>();
        private Dictionary<string, BoneNode> costumeBoneHierarchy = new Dictionary<string, BoneNode>();
        
        // ボーンのマッピングテーブル（アバターボーン名 -> 衣装ボーン名）
        private Dictionary<string, string> boneNameMapping = new Dictionary<string, string>();
        
        // ボーンのマッピングテーブル（アバターボーン -> 衣装ボーン）
        private Dictionary<Transform, Transform> directBoneMapping = new Dictionary<Transform, Transform>();
        
        // ボーンノード（階層構造を表現）
        public class BoneNode {
            public Transform transform;
            public string name;
            public string normalizedName;
            public BoneNode parent;
            public List<BoneNode> children = new List<BoneNode>();
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public Vector3 worldPosition;
            public string boneType; // Hips, Spine, Head などHumanoid分類
            
            public BoneNode(Transform t, BoneNode parent = null) {
                this.transform = t;
                this.name = t.name;
                this.normalizedName = AvatarUtility.NormalizeBoneName(t.name).ToLowerInvariant();
                this.parent = parent;
                this.localPosition = t.localPosition;
                this.localRotation = t.localRotation;
                this.localScale = t.localScale;
                this.worldPosition = t.position;
            }
        }
        
        /// <summary>
        /// アバターと衣装のボーン階層を解析
        /// </summary>
        public void AnalyzeBoneStructures(GameObject avatar, GameObject costume, Animator avatarAnimator = null) {
            avatarBoneHierarchy.Clear();
            costumeBoneHierarchy.Clear();
            boneNameMapping.Clear();
            directBoneMapping.Clear();
            
            // アバターのボーン階層を解析
            if (avatar != null) {
                BuildBoneHierarchy(avatar.transform, null, avatarBoneHierarchy, true, avatarAnimator);
            }
            
            // 衣装のボーン階層を解析
            if (costume != null) {
                BuildBoneHierarchy(costume.transform, null, costumeBoneHierarchy, false, null);
            }
            
            // Humanoidボーン情報を割り当て（アバターにAnimatorがある場合）
            if (avatarAnimator != null && avatarAnimator.isHuman) {
                AssignHumanoidBoneTypes(avatarAnimator, avatarBoneHierarchy);
            }
            
            // 基本的なボーンマッピングを生成
            GenerateBoneMapping();
            
            // 構造的な分析とマッピングの改善
            AnalyzeStructuralDifferences();
            
            // 空間的位置に基づくマッピングの追加
            GeneratePositionalMapping(avatar, costume);
            
            // ダイレクトマッピングの作成
            CreateDirectBoneMapping(avatar, costume);
        }
        
        /// <summary>
        /// ボーン階層を構築
        /// </summary>
        private void BuildBoneHierarchy(Transform current, BoneNode parent, Dictionary<string, BoneNode> hierarchy, bool isAvatar, Animator animator) {
            // ボーンノードを作成
            BoneNode node = new BoneNode(current, parent);
            
            // Armatureの下のボーンのみを処理（アバターの場合）
            if (isAvatar && parent == null && current.name != "Armature") {
                foreach (Transform child in current) {
                    if (child.name == "Armature") {
                        BuildBoneHierarchy(child, null, hierarchy, isAvatar, animator);
                        return;
                    }
                }
            }
            
            // 親ノードに子として登録
            if (parent != null) {
                parent.children.Add(node);
            }
            
            // 階層に追加
            hierarchy[current.name] = node;
            
            // 子ノードを再帰的に処理
            foreach (Transform child in current) {
                BuildBoneHierarchy(child, node, hierarchy, isAvatar, animator);
            }
        }
        
        /// <summary>
        /// Humanoidボーン情報を割り当て
        /// </summary>
        private void AssignHumanoidBoneTypes(Animator animator, Dictionary<string, BoneNode> hierarchy) {
            foreach (HumanBodyBones boneType in System.Enum.GetValues(typeof(HumanBodyBones))) {
                if (boneType == HumanBodyBones.LastBone) continue;
                
                Transform bone = animator.GetBoneTransform(boneType);
                if (bone != null && hierarchy.ContainsKey(bone.name)) {
                    hierarchy[bone.name].boneType = boneType.ToString();
                }
            }
        }
        
        /// <summary>
        /// 基本的なボーンマッピングを生成
        /// </summary>
        private void GenerateBoneMapping() {
            // 1. 名前が完全一致するボーンをマッピング
            foreach (var avatarEntry in avatarBoneHierarchy) {
                string avatarBoneName = avatarEntry.Key;
                if (costumeBoneHierarchy.ContainsKey(avatarBoneName)) {
                    boneNameMapping[avatarBoneName] = avatarBoneName;
                }
            }
            
            // 2. 正規化された名前で一致するボーンをマッピング
            foreach (var avatarEntry in avatarBoneHierarchy) {
                if (boneNameMapping.ContainsKey(avatarEntry.Key)) continue;
                
                string avatarNormalizedName = avatarEntry.Value.normalizedName;
                
                foreach (var costumeEntry in costumeBoneHierarchy) {
                    string costumeNormalizedName = costumeEntry.Value.normalizedName;
                    
                    if (avatarNormalizedName == costumeNormalizedName ||
                        avatarNormalizedName.Contains(costumeNormalizedName) ||
                        costumeNormalizedName.Contains(avatarNormalizedName)) {
                        boneNameMapping[avatarEntry.Key] = costumeEntry.Key;
                        break;
                    }
                }
            }
            
            // 3. Humanoidボーンタイプが同じボーンをマッピング
            foreach (var avatarEntry in avatarBoneHierarchy) {
                if (boneNameMapping.ContainsKey(avatarEntry.Key) || string.IsNullOrEmpty(avatarEntry.Value.boneType)) continue;
                
                foreach (var costumeEntry in costumeBoneHierarchy) {
                    if (avatarEntry.Value.boneType == costumeEntry.Value.boneType && !string.IsNullOrEmpty(costumeEntry.Value.boneType)) {
                        boneNameMapping[avatarEntry.Key] = costumeEntry.Key;
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// 構造的な差異を分析し、マッピングを改善
        /// </summary>
        private void AnalyzeStructuralDifferences() {
            // 1. 位置ベースのマッピング（まだマッピングされていないボーン用）
            foreach (var avatarEntry in avatarBoneHierarchy) {
                if (boneNameMapping.ContainsKey(avatarEntry.Key)) continue;
                
                Vector3 avatarPos = avatarEntry.Value.worldPosition;
                float closestDistance = float.MaxValue;
                string closestBoneName = null;
                
                foreach (var costumeEntry in costumeBoneHierarchy) {
                    if (boneNameMapping.ContainsValue(costumeEntry.Key)) continue;
                    
                    float distance = Vector3.Distance(avatarPos, costumeEntry.Value.worldPosition);
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        closestBoneName = costumeEntry.Key;
                    }
                }
                
                // 距離が近い場合のみマッピング
                if (closestBoneName != null && closestDistance < 0.5f) {
                    boneNameMapping[avatarEntry.Key] = closestBoneName;
                }
            }
            
            // 2. 階層関係に基づくマッピング
            foreach (var avatarEntry in avatarBoneHierarchy) {
                if (boneNameMapping.ContainsKey(avatarEntry.Key) || avatarEntry.Value.parent == null) continue;
                
                // 親がマッピングされている場合、その子をマッピング
                if (avatarEntry.Value.parent != null && 
                    boneNameMapping.ContainsKey(avatarEntry.Value.parent.name)) {
                    
                    string parentCostumeBoneName = boneNameMapping[avatarEntry.Value.parent.name];
                    
                    if (costumeBoneHierarchy.ContainsKey(parentCostumeBoneName)) {
                        BoneNode parentCostumeNode = costumeBoneHierarchy[parentCostumeBoneName];
                        
                        // 子の中で最も名前が似ているものを探す
                        foreach (BoneNode childCostumeNode in parentCostumeNode.children) {
                            if (boneNameMapping.ContainsValue(childCostumeNode.name)) continue;
                            
                            string avatarChildName = avatarEntry.Value.normalizedName;
                            string costumeChildName = childCostumeNode.normalizedName;
                            
                            if (avatarChildName.Contains(costumeChildName) || 
                                costumeChildName.Contains(avatarChildName)) {
                                boneNameMapping[avatarEntry.Key] = childCostumeNode.name;
                                break;
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 空間的位置に基づくマッピングを生成
        /// </summary>
        private void GeneratePositionalMapping(GameObject avatar, GameObject costume) {
            if (avatar == null || costume == null) return;
            
            Transform[] avatarBones = avatar.GetComponentsInChildren<Transform>();
            Transform[] costumeBones = costume.GetComponentsInChildren<Transform>();
            
            // アバターの骨格バウンディングボックスを計算
            Vector3 avatarMin = Vector3.positiveInfinity;
            Vector3 avatarMax = Vector3.negativeInfinity;
            foreach (Transform bone in avatarBones) {
                if (bone == null) continue;
                avatarMin = Vector3.Min(avatarMin, bone.position);
                avatarMax = Vector3.Max(avatarMax, bone.position);
            }
            Vector3 avatarSize = avatarMax - avatarMin;
            Vector3 avatarCenter = (avatarMax + avatarMin) / 2f;
            
            // 衣装の骨格バウンディングボックスを計算
            Vector3 costumeMin = Vector3.positiveInfinity;
            Vector3 costumeMax = Vector3.negativeInfinity;
            foreach (Transform bone in costumeBones) {
                if (bone == null) continue;
                costumeMin = Vector3.Min(costumeMin, bone.position);
                costumeMax = Vector3.Max(costumeMax, bone.position);
            }
            Vector3 costumeSize = costumeMax - costumeMin;
            Vector3 costumeCenter = (costumeMax + costumeMin) / 2f;
            
            // サイズが0にならないよう防止
            if (avatarSize.magnitude < 0.001f) avatarSize = Vector3.one;
            if (costumeSize.magnitude < 0.001f) costumeSize = Vector3.one;
            
            // まだマッピングされていないアバターボーンについて処理
            foreach (var avatarEntry in avatarBoneHierarchy) {
                if (boneNameMapping.ContainsKey(avatarEntry.Key)) continue;
                
                BoneNode avatarNode = avatarEntry.Value;
                
                // アバターボーンの正規化座標を計算（-1～1の範囲）
                Vector3 normalizedAvatarPos = (avatarNode.worldPosition - avatarCenter);
                normalizedAvatarPos.x /= avatarSize.x * 0.5f;
                normalizedAvatarPos.y /= avatarSize.y * 0.5f;
                normalizedAvatarPos.z /= avatarSize.z * 0.5f;
                
                // 最も近い衣装ボーンを探す
                float bestSimilarity = 0;
                string bestMatch = null;
                
                foreach (var costumeEntry in costumeBoneHierarchy) {
                    if (boneNameMapping.ContainsValue(costumeEntry.Key)) continue;
                    
                    BoneNode costumeNode = costumeEntry.Value;
                    
                    // 衣装ボーンの正規化座標を計算
                    Vector3 normalizedCostumePos = (costumeNode.worldPosition - costumeCenter);
                    normalizedCostumePos.x /= costumeSize.x * 0.5f;
                    normalizedCostumePos.y /= costumeSize.y * 0.5f;
                    normalizedCostumePos.z /= costumeSize.z * 0.5f;
                    
                    // 座標の類似度を計算（1に近いほど似ている）
                    float similarity = 1.0f - (Vector3.Distance(normalizedAvatarPos, normalizedCostumePos) / 3.5f);
                    
                    // 名前の類似度も考慮
                    float nameSimilarity = 0;
                    if (avatarNode.normalizedName.Contains(costumeNode.normalizedName) || 
                        costumeNode.normalizedName.Contains(avatarNode.normalizedName)) {
                        nameSimilarity = 0.5f;
                    }
                    
                    // 総合スコア
                    float totalScore = similarity * 0.6f + nameSimilarity * 0.4f;
                    
                    if (totalScore > bestSimilarity && totalScore > 0.5f) {
                        bestSimilarity = totalScore;
                        bestMatch = costumeEntry.Key;
                    }
                }
                
                // 良い一致が見つかった場合はマッピングに追加
                if (bestMatch != null) {
                    boneNameMapping[avatarEntry.Key] = bestMatch;
                }
            }
        }
        
        /// <summary>
        /// トランスフォーム間の直接マッピングを作成
        /// </summary>
        private void CreateDirectBoneMapping(GameObject avatar, GameObject costume) {
            if (avatar == null || costume == null) return;
            
            Transform[] avatarBones = avatar.GetComponentsInChildren<Transform>();
            
            foreach (Transform avatarBone in avatarBones) {
                if (avatarBone == null) continue;
                
                // 名前ベースのマッピングからトランスフォームを取得
                if (boneNameMapping.TryGetValue(avatarBone.name, out string costumeBoneName)) {
                    Transform costumeTransform = FindBoneInHierarchy(costume.transform, costumeBoneName);
                    if (costumeTransform != null) {
                        directBoneMapping[avatarBone] = costumeTransform;
                    }
                }
            }
        }
        
        /// <summary>
        /// アバターボーンに対応する衣装のボーンを取得
        /// </summary>
        public Transform GetCorrespondingCostumeBone(string avatarBoneName, GameObject costumeObject) {
            if (boneNameMapping.TryGetValue(avatarBoneName, out string costumeBoneName)) {
                return FindBoneInHierarchy(costumeObject.transform, costumeBoneName);
            }
            return null;
        }
        
        /// <summary>
        /// アバターボーンに対応する衣装のボーンを直接取得（トランスフォーム参照）
        /// </summary>
        public Transform GetCorrespondingCostumeBone(Transform avatarBone) {
            if (directBoneMapping.TryGetValue(avatarBone, out Transform costumeBone)) {
                return costumeBone;
            }
            return null;
        }
        
        /// <summary>
        /// 階層内でボーンを検索
        /// </summary>
        private Transform FindBoneInHierarchy(Transform root, string boneName) {
            if (root.name == boneName) return root;
            
            foreach (Transform child in root) {
                Transform found = FindBoneInHierarchy(child, boneName);
                if (found != null) return found;
            }
            
            return null;
        }
        
        /// <summary>
        /// 欠落ボーンを検出して生成
        /// </summary>
        public Dictionary<string, Transform> GenerateMissingBones(GameObject avatar, GameObject costumeInstance, Animator avatarAnimator) {
            Dictionary<string, Transform> generatedBones = new Dictionary<string, Transform>();
            
            // 主要なHumanoidボーンを確認
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
            
            foreach (HumanBodyBones boneType in essentialBones) {
                Transform avatarBone = avatarAnimator.GetBoneTransform(boneType);
                if (avatarBone == null) continue;
                
                // 対応するボーンを探す
                Transform costumeBone = GetCorrespondingCostumeBone(avatarBone.name, costumeInstance);
                
                if (costumeBone == null) {
                    // 直接マッピングでも試す
                    costumeBone = GetCorrespondingCostumeBone(avatarBone);
                }
                
                if (costumeBone == null) {
                    // 対応するボーンがない場合は生成
                    costumeBone = GenerateBone(avatarBone, costumeInstance, boneType, generatedBones);
                    if (costumeBone != null) {
                        generatedBones[avatarBone.name] = costumeBone;
                        
                        // 新しく生成したボーンをマッピングに追加
                        boneNameMapping[avatarBone.name] = costumeBone.name;
                        directBoneMapping[avatarBone] = costumeBone;
                    }
                }
            }
            
            return generatedBones;
        }
        
        /// <summary>
        /// 不足ボーンを生成
        /// </summary>
        private Transform GenerateBone(Transform avatarBone, GameObject costumeInstance, HumanBodyBones boneType, Dictionary<string, Transform> generatedBones) {
            // 親ボーンを特定
            Transform parentBone = null;
            
            // ボーンの階層関係に基づいて親を探す
            switch (boneType) {
                case HumanBodyBones.Spine:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.Hips, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.Chest:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.Spine, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.UpperChest:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.Chest, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.Neck:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.Chest, costumeInstance, generatedBones) ??
                                FindOrCreateParentBone(HumanBodyBones.UpperChest, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.Head:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.Neck, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.LeftShoulder:
                case HumanBodyBones.RightShoulder:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.Chest, costumeInstance, generatedBones) ??
                                FindOrCreateParentBone(HumanBodyBones.UpperChest, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.LeftUpperArm:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.LeftShoulder, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.RightUpperArm:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.RightShoulder, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.LeftLowerArm:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.LeftUpperArm, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.RightLowerArm:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.RightUpperArm, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.LeftHand:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.LeftLowerArm, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.RightHand:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.RightLowerArm, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.RightUpperLeg:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.Hips, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.LeftLowerLeg:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.LeftUpperLeg, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.RightLowerLeg:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.RightUpperLeg, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.LeftFoot:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.LeftLowerLeg, costumeInstance, generatedBones);
                    break;
                case HumanBodyBones.RightFoot:
                    parentBone = FindOrCreateParentBone(HumanBodyBones.RightLowerLeg, costumeInstance, generatedBones);
                    break;
            }
            
            if (parentBone != null) {
                // 新しいボーンを作成
                GameObject newBone = new GameObject(avatarBone.name);
                newBone.transform.SetParent(parentBone);
                
                // アバターのボーンの位置を基にローカル位置を設定
                newBone.transform.localPosition = avatarBone.localPosition;
                newBone.transform.localRotation = avatarBone.localRotation;
                newBone.transform.localScale = avatarBone.localScale;
                
                Debug.Log($"生成したボーン: {avatarBone.name}");
                return newBone.transform;
            }
            
            return null;
        }
        
        /// <summary>
        /// 親ボーンを見つけるか作成
        /// </summary>
        private Transform FindOrCreateParentBone(HumanBodyBones parentBoneType, GameObject costumeInstance, Dictionary<string, Transform> generatedBones) {
            Animator avatarAnimator = costumeInstance.GetComponentInParent<Animator>();
            if (avatarAnimator == null) return null;
            
            Transform avatarParentBone = avatarAnimator.GetBoneTransform(parentBoneType);
            if (avatarParentBone == null) return null;
            
            // 既に生成されたボーンを探す
            if (generatedBones.ContainsKey(avatarParentBone.name)) {
                return generatedBones[avatarParentBone.name];
            }
            
            // 対応するボーンを探す - まず名前ベースで
            Transform costumeParentBone = GetCorrespondingCostumeBone(avatarParentBone.name, costumeInstance);
            
            if (costumeParentBone == null) {
                // 直接マッピングでも試す
                costumeParentBone = GetCorrespondingCostumeBone(avatarParentBone);
            }
            
            if (costumeParentBone != null) {
                // ボーンが見つかった場合
                return costumeParentBone;
            } else {
                // 再帰的に親を生成
                return GenerateBone(avatarParentBone, costumeInstance, parentBoneType, generatedBones);
            }
        }
        
        /// <summary>
        /// マッピングのデバッグ情報を表示
        /// </summary>
        public void DebugMappingInfo() {
            Debug.Log("--- ボーンマッピング情報 ---");
            foreach (var entry in boneNameMapping) {
                Debug.Log($"アバターボーン: {entry.Key} -> 衣装ボーン: {entry.Value}");
            }
            
            Debug.Log("--- 直接ボーンマッピング情報 ---");
            foreach (var entry in directBoneMapping) {
                Debug.Log($"アバターボーン: {entry.Key.name} -> 衣装ボーン: {entry.Value.name}");
            }
            
            Debug.Log($"マッピング率: {boneNameMapping.Count}/{avatarBoneHierarchy.Count} ({(float)boneNameMapping.Count / avatarBoneHierarchy.Count * 100:F1}%)");
        }
    }
}