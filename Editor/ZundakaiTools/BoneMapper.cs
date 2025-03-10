using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    /// <summary>
    /// 異なるボーン構造間でマッピングを行うための高度なボーンマッパー
    /// </summary>
    public class BoneMapper {
        // 一般的なボーン名の対応表（異なる命名規則への対応）
        private static readonly Dictionary<string, List<string>> boneNameMappings = new Dictionary<string, List<string>>() {
            // 頭部関連
            { "head", new List<string>() { "head", "face", "頭", "face", "头", "kubi", "atama", "头部", "頭部" } },
            { "neck", new List<string>() { "neck", "首", "颈", "kubi", "くび", "颈部", "首部" } },
            
            // 体幹部
            { "hips", new List<string>() { "hips", "pelvis", "hip", "腰", "臀部", "hüfte", "骨盤", "koshi", "腰部", "臀" } },
            { "spine", new List<string>() { "spine", "chest", "背骨", "脊椎", "背中", "背", "脊柱", "wirbelsäule", "sebone", "脊柱", "背部" } },
            { "chest", new List<string>() { "chest", "torso", "胸", "胸部", "躯干", "brustkorb", "mune", "躯干", "上半身" } },
            { "upperchest", new List<string>() { "upperchest", "upper_chest", "upper.chest", "upper_torso", "upper.torso", "upperbody", "上半身2", "上胸部" } },
            
            // 左腕関連
            { "leftshoulder", new List<string>() { "leftshoulder", "l_shoulder", "l.shoulder", "l_clavicle", "l.clavicle", "左肩", "左鎖骨", "left_shoulder", "left.shoulder", "shoulder.l", "shoulder_l", "肩.左", "肩_左", "鎖骨.左", "鎖骨_左", "hidari_kata" } },
            { "leftarm", new List<string>() { "leftarm", "l_arm", "l.arm", "l_upperarm", "l.upperarm", "左腕", "左上腕", "left_arm", "left.arm", "arm.l", "arm_l", "腕.左", "腕_左", "上腕.左", "上腕_左", "hidari_ude" } },
            { "leftforearm", new List<string>() { "leftforearm", "l_forearm", "l.forearm", "l_lowerarm", "l.lowerarm", "左前腕", "左下腕", "left_forearm", "left.forearm", "forearm.l", "forearm_l", "前腕.左", "前腕_左", "下腕.左", "下腕_左", "hidari_hiji" } },
            { "lefthand", new List<string>() { "lefthand", "l_hand", "l.hand", "左手", "left_hand", "left.hand", "hand.l", "hand_l", "手.左", "手_左", "hidari_te" } },
            
            // 右腕関連
            { "rightshoulder", new List<string>() { "rightshoulder", "r_shoulder", "r.shoulder", "r_clavicle", "r.clavicle", "右肩", "右鎖骨", "right_shoulder", "right.shoulder", "shoulder.r", "shoulder_r", "肩.右", "肩_右", "鎖骨.右", "鎖骨_右", "migi_kata" } },
            { "rightarm", new List<string>() { "rightarm", "r_arm", "r.arm", "r_upperarm", "r.upperarm", "右腕", "右上腕", "right_arm", "right.arm", "arm.r", "arm_r", "腕.右", "腕_右", "上腕.右", "上腕_右", "migi_ude" } },
            { "rightforearm", new List<string>() { "rightforearm", "r_forearm", "r.forearm", "r_lowerarm", "r.lowerarm", "右前腕", "右下腕", "right_forearm", "right.forearm", "forearm.r", "forearm_r", "前腕.右", "前腕_右", "下腕.右", "下腕_右", "migi_hiji" } },
            { "righthand", new List<string>() { "righthand", "r_hand", "r.hand", "右手", "right_hand", "right.hand", "hand.r", "hand_r", "手.右", "手_右", "migi_te" } },
            
            // 左脚関連
            { "leftupleg", new List<string>() { "leftupleg", "l_upleg", "l.upleg", "l_thigh", "l.thigh", "左大腿", "左太もも", "left_upleg", "left.upleg", "upleg.l", "upleg_l", "大腿.左", "大腿_左", "太もも.左", "太もも_左", "hidari_momo" } },
            { "leftleg", new List<string>() { "leftleg", "l_leg", "l.leg", "l_calf", "l.calf", "左下腿", "左脛", "left_leg", "left.leg", "leg.l", "leg_l", "下腿.左", "下腿_左", "脛.左", "脛_左", "hidari_sune" } },
            { "leftfoot", new List<string>() { "leftfoot", "l_foot", "l.foot", "左足", "left_foot", "left.foot", "foot.l", "foot_l", "足.左", "足_左", "hidari_ashi" } },
            { "lefttoebase", new List<string>() { "lefttoebase", "l_toe", "l.toe", "l_toebase", "l.toebase", "左つま先", "left_toe", "left.toe", "toe.l", "toe_l", "つま先.左", "つま先_左", "hidari_tsumasaki" } },
            
            // 右脚関連
            { "rightupleg", new List<string>() { "rightupleg", "r_upleg", "r.upleg", "r_thigh", "r.thigh", "右大腿", "右太もも", "right_upleg", "right.upleg", "upleg.r", "upleg_r", "大腿.右", "大腿_右", "太もも.右", "太もも_右", "migi_momo" } },
            { "rightleg", new List<string>() { "rightleg", "r_leg", "r.leg", "r_calf", "r.calf", "右下腿", "右脛", "right_leg", "right.leg", "leg.r", "leg_r", "下腿.右", "下腿_右", "脛.右", "脛_右", "migi_sune" } },
            { "rightfoot", new List<string>() { "rightfoot", "r_foot", "r.foot", "右足", "right_foot", "right.foot", "foot.r", "foot_r", "足.右", "足_右", "migi_ashi" } },
            { "righttoebase", new List<string>() { "righttoebase", "r_toe", "r.toe", "r_toebase", "r.toebase", "右つま先", "right_toe", "right.toe", "toe.r", "toe_r", "つま先.右", "つま先_右", "migi_tsumasaki" } },
            
            // 指関連
            { "leftthumb", new List<string>() { "leftthumb", "l_thumb", "l.thumb", "左親指", "left_thumb", "left.thumb", "thumb.l", "thumb_l", "親指.左", "親指_左", "hidari_oyayubi" } },
            { "leftindex", new List<string>() { "leftindex", "l_index", "l.index", "左人差し指", "left_index", "left.index", "index.l", "index_l", "人差し指.左", "人差し指_左", "hidari_hitosashiyubi" } },
            { "leftmiddle", new List<string>() { "leftmiddle", "l_middle", "l.middle", "左中指", "left_middle", "left.middle", "middle.l", "middle_l", "中指.左", "中指_左", "hidari_nakayubi" } },
            { "leftring", new List<string>() { "leftring", "l_ring", "l.ring", "左薬指", "left_ring", "left.ring", "ring.l", "ring_l", "薬指.左", "薬指_左", "hidari_kusuriyubi" } },
            { "leftlittle", new List<string>() { "leftlittle", "l_little", "l.little", "左小指", "left_little", "left.little", "little.l", "little_l", "小指.左", "小指_左", "hidari_koyubi" } },
            
            { "rightthumb", new List<string>() { "rightthumb", "r_thumb", "r.thumb", "右親指", "right_thumb", "right.thumb", "thumb.r", "thumb_r", "親指.右", "親指_右", "migi_oyayubi" } },
            { "rightindex", new List<string>() { "rightindex", "r_index", "r.index", "右人差し指", "right_index", "right.index", "index.r", "index_r", "人差し指.右", "人差し指_右", "migi_hitosashiyubi" } },
            { "rightmiddle", new List<string>() { "rightmiddle", "r_middle", "r.middle", "右中指", "right_middle", "right.middle", "middle.r", "middle_r", "中指.右", "中指_右", "migi_nakayubi" } },
            { "rightring", new List<string>() { "rightring", "r_ring", "r.ring", "右薬指", "right_ring", "right.ring", "ring.r", "ring_r", "薬指.右", "薬指_右", "migi_kusuriyubi" } },
            { "rightlittle", new List<string>() { "rightlittle", "r_little", "r.little", "右小指", "right_little", "right.little", "little.r", "little_r", "小指.右", "小指_右", "migi_koyubi" } }
        };
        
        // 階層的位置に基づくボーン識別用（親子関係のパターン）
        private static readonly Dictionary<string, string[]> hierarchyPatterns = new Dictionary<string, string[]>() {
            { "head", new string[] { "neck" } },
            { "neck", new string[] { "spine", "chest", "upperchest" } },
            { "chest", new string[] { "spine" } },
            { "upperchest", new string[] { "chest" } },
            { "spine", new string[] { "hips" } },
            
            { "leftshoulder", new string[] { "chest", "upperchest", "spine" } },
            { "leftarm", new string[] { "leftshoulder" } },
            { "leftforearm", new string[] { "leftarm" } },
            { "lefthand", new string[] { "leftforearm" } },
            
            { "rightshoulder", new string[] { "chest", "upperchest", "spine" } },
            { "rightarm", new string[] { "rightshoulder" } },
            { "rightforearm", new string[] { "rightarm" } },
            { "righthand", new string[] { "rightforearm" } },
            
            { "leftupleg", new string[] { "hips" } },
            { "leftleg", new string[] { "leftupleg" } },
            { "leftfoot", new string[] { "leftleg" } },
            { "lefttoebase", new string[] { "leftfoot" } },
            
            { "rightupleg", new string[] { "hips" } },
            { "rightleg", new string[] { "rightupleg" } },
            { "rightfoot", new string[] { "rightleg" } },
            { "righttoebase", new string[] { "rightfoot" } }
        };
        
        // 位置に基づく自動識別用の空間位置パターン（体の相対的な位置）
        private static readonly Dictionary<string, Vector3> spatialPatterns = new Dictionary<string, Vector3>() {
            // Y値は高さ、X値は左右位置、Z値は前後位置を示す (正規化された値)
            { "head", new Vector3(0.0f, 0.8f, 0.0f) },
            { "neck", new Vector3(0.0f, 0.7f, 0.0f) },
            { "chest", new Vector3(0.0f, 0.6f, 0.0f) },
            { "spine", new Vector3(0.0f, 0.5f, 0.0f) },
            { "hips", new Vector3(0.0f, 0.4f, 0.0f) },
            
            { "leftshoulder", new Vector3(-0.2f, 0.7f, 0.0f) },
            { "leftarm", new Vector3(-0.3f, 0.65f, 0.0f) },
            { "leftforearm", new Vector3(-0.4f, 0.5f, 0.0f) },
            { "lefthand", new Vector3(-0.5f, 0.4f, 0.0f) },
            
            { "rightshoulder", new Vector3(0.2f, 0.7f, 0.0f) },
            { "rightarm", new Vector3(0.3f, 0.65f, 0.0f) },
            { "rightforearm", new Vector3(0.4f, 0.5f, 0.0f) },
            { "righthand", new Vector3(0.5f, 0.4f, 0.0f) },
            
            { "leftupleg", new Vector3(-0.15f, 0.3f, 0.0f) },
            { "leftleg", new Vector3(-0.15f, 0.2f, 0.0f) },
            { "leftfoot", new Vector3(-0.15f, 0.05f, 0.1f) },
            { "lefttoebase", new Vector3(-0.15f, 0.0f, 0.2f) },
            
            { "rightupleg", new Vector3(0.15f, 0.3f, 0.0f) },
            { "rightleg", new Vector3(0.15f, 0.2f, 0.0f) },
            { "rightfoot", new Vector3(0.15f, 0.05f, 0.1f) },
            { "righttoebase", new Vector3(0.15f, 0.0f, 0.2f) }
        };
        
        /// <summary>
        /// 名前に基づいてボーンの正規化された種類を取得する
        /// </summary>
        public static string GetNormalizedBoneType(string boneName) {
            string loweredName = boneName.ToLowerInvariant();
            
            // 特殊文字を除去して正規化
            loweredName = loweredName.Replace(".", "").Replace("_", "").Replace(" ", "");
            
            foreach (var mapping in boneNameMappings) {
                foreach (string variant in mapping.Value) {
                    if (loweredName.Contains(variant)) {
                        return mapping.Key;
                    }
                }
            }
            
            return "";
        }
        
        /// <summary>
        /// 一連のボーンから指定したタイプのボーンを探す
        /// </summary>
        public static Transform FindBoneByType(Transform[] bones, string boneType) {
            foreach (Transform bone in bones) {
                if (bone == null) continue;
                
                string normalizedType = GetNormalizedBoneType(bone.name);
                if (normalizedType == boneType) {
                    return bone;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 階層構造に基づいてボーンを識別し、対応するボーンを見つける
        /// </summary>
        public static Transform FindBoneByHierarchy(Transform[] bones, Dictionary<Transform, string> boneTypes, string targetType) {
            // まず直接的な名前ベースの一致を試す
            foreach (Transform bone in bones) {
                if (bone == null) continue;
                
                if (boneTypes.ContainsKey(bone) && boneTypes[bone] == targetType) {
                    return bone;
                }
            }
            
            // 階層パターンに基づく探索
            if (hierarchyPatterns.ContainsKey(targetType)) {
                // ボーンの親子関係の辞書を構築
                Dictionary<Transform, Transform> childToParent = new Dictionary<Transform, Transform>();
                foreach (Transform bone in bones) {
                    if (bone == null || bone.parent == null) continue;
                    childToParent[bone] = bone.parent;
                }
                
                // 階層パターンに基づいて適切なボーンを探す
                foreach (string parentType in hierarchyPatterns[targetType]) {
                    // まず親となりうるボーンを探す
                    List<Transform> potentialParents = new List<Transform>();
                    foreach (Transform bone in bones) {
                        if (bone == null) continue;
                        if (boneTypes.ContainsKey(bone) && boneTypes[bone] == parentType) {
                            potentialParents.Add(bone);
                        }
                    }
                    
                    // 見つかった親から適切な子ボーンを探す
                    foreach (Transform parentBone in potentialParents) {
                        foreach (Transform bone in bones) {
                            if (bone == null || bone.parent == null) continue;
                            if (bone.parent == parentBone && !boneTypes.ContainsKey(bone)) {
                                // このボーンは適切な親を持ち、まだ識別されていない
                                return bone;
                            }
                        }
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 位置情報に基づいてボーンを識別する
        /// </summary>
        public static Transform FindBoneByPosition(Transform[] bones, string targetType, Transform rootBone) {
            if (!spatialPatterns.ContainsKey(targetType) || rootBone == null) return null;
            
            Vector3 targetPosition = spatialPatterns[targetType];
            Transform closestBone = null;
            float minDistance = float.MaxValue;
            
            // ボーンのバウンディングボックスを計算
            Vector3 minBound = Vector3.positiveInfinity;
            Vector3 maxBound = Vector3.negativeInfinity;
            
            foreach (Transform bone in bones) {
                if (bone == null) continue;
                
                Vector3 pos = bone.position;
                minBound = Vector3.Min(minBound, pos);
                maxBound = Vector3.Max(maxBound, pos);
            }
            
            Vector3 boundsSize = maxBound - minBound;
            Vector3 boundsCenter = (maxBound + minBound) / 2.0f;
            
            // ボーン位置を正規化し、最も近いボーンを探す
            foreach (Transform bone in bones) {
                if (bone == null) continue;
                
                // ボーン位置を正規化 (-1～1の範囲)
                Vector3 normalizedPos = (bone.position - boundsCenter);
                if (boundsSize.x > 0.0001f) normalizedPos.x /= (boundsSize.x * 0.5f);
                if (boundsSize.y > 0.0001f) normalizedPos.y /= (boundsSize.y * 0.5f);
                if (boundsSize.z > 0.0001f) normalizedPos.z /= (boundsSize.z * 0.5f);
                
                // 目標位置との距離を計算
                float distance = Vector3.Distance(normalizedPos, targetPosition);
                
                // 最も近いボーンを更新
                if (distance < minDistance) {
                    minDistance = distance;
                    closestBone = bone;
                }
            }
            
            // 閾値以内の場合のみ結果を返す
            if (minDistance < 0.5f) {
                return closestBone;
            }
            
            return null;
        }
        
        /// <summary>
        /// アバターと衣装のボーンをマッピングする
        /// </summary>
        public static Dictionary<Transform, Transform> MapBones(GameObject avatarObject, GameObject costumeObject) {
            if (avatarObject == null || costumeObject == null) return new Dictionary<Transform, Transform>();
            
            // 結果のマッピング
            Dictionary<Transform, Transform> boneMapping = new Dictionary<Transform, Transform>();
            
            // アバターボーンとコスチュームボーンを取得
            Transform[] avatarBones = avatarObject.GetComponentsInChildren<Transform>();
            Transform[] costumeBones = costumeObject.GetComponentsInChildren<Transform>();
            
            // ボーンの種類を識別する
            Dictionary<Transform, string> avatarBoneTypes = new Dictionary<Transform, string>();
            Dictionary<Transform, string> costumeBoneTypes = new Dictionary<Transform, string>();
            
            // ステップ1: 名前に基づいて基本的な識別を行う
            foreach (Transform bone in avatarBones) {
                if (bone == null) continue;
                string boneType = GetNormalizedBoneType(bone.name);
                if (!string.IsNullOrEmpty(boneType)) {
                    avatarBoneTypes[bone] = boneType;
                }
            }
            
            foreach (Transform bone in costumeBones) {
                if (bone == null) continue;
                string boneType = GetNormalizedBoneType(bone.name);
                if (!string.IsNullOrEmpty(boneType)) {
                    costumeBoneTypes[bone] = boneType;
                }
            }
            
            // ステップ2: 名前の一致に基づいてマッピングを作成
            foreach (var avatarEntry in avatarBoneTypes) {
                Transform avatarBone = avatarEntry.Key;
                string boneType = avatarEntry.Value;
                
                Transform costumeBone = FindBoneByType(costumeBones, boneType);
                if (costumeBone != null) {
                    boneMapping[avatarBone] = costumeBone;
                }
            }
            
            // ステップ3: 階層構造に基づいて未マッピングのボーンを処理
            foreach (var avatarEntry in avatarBoneTypes) {
                Transform avatarBone = avatarEntry.Key;
                string boneType = avatarEntry.Value;
                
                // まだマッピングされていないボーンのみ処理
                if (!boneMapping.ContainsKey(avatarBone)) {
                    Transform costumeBone = FindBoneByHierarchy(costumeBones, costumeBoneTypes, boneType);
                    if (costumeBone != null) {
                        boneMapping[avatarBone] = costumeBone;
                    }
                }
            }
            
            // ステップ4: 位置情報に基づいて残りのボーンを処理
            Transform avatarRoot = FindRootBone(avatarObject);
            Transform costumeRoot = FindRootBone(costumeObject);
            
            foreach (var avatarEntry in avatarBoneTypes) {
                Transform avatarBone = avatarEntry.Key;
                string boneType = avatarEntry.Value;
                
                // まだマッピングされていないボーンのみ処理
                if (!boneMapping.ContainsKey(avatarBone)) {
                    Transform costumeBone = FindBoneByPosition(costumeBones, boneType, costumeRoot);
                    if (costumeBone != null) {
                        boneMapping[avatarBone] = costumeBone;
                    }
                }
            }
            
            // ステップ5: 必要なボーンに対してフォールバックマッピングを適用
            ApplyFallbackMapping(avatarObject, costumeObject, boneMapping);
            
            return boneMapping;
        }
        
        /// <summary>
        /// ルートボーンを見つける
        /// </summary>
        private static Transform FindRootBone(GameObject obj) {
            // スキンメッシュレンダラーからルートボーンを探す
            SkinnedMeshRenderer[] renderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in renderers) {
                if (renderer.rootBone != null) {
                    return renderer.rootBone;
                }
            }
            
            // Humanoidモデルの場合はHipsボーンを探す
            Animator animator = obj.GetComponent<Animator>();
            if (animator != null && animator.isHuman) {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null) {
                    return hips;
                }
            }
            
            // 一般的なルートボーン名を探す
            string[] commonRootNames = { "root", "skeleton", "armature", "rig", "hips", "pelvis" };
            foreach (string name in commonRootNames) {
                Transform[] transforms = obj.GetComponentsInChildren<Transform>();
                foreach (Transform t in transforms) {
                    if (t.name.ToLowerInvariant().Contains(name)) {
                        return t;
                    }
                }
            }
            
            // 何も見つからなければオブジェクトのルートを返す
            return obj.transform;
        }
        
        /// <summary>
        /// 重要なボーンに対してフォールバックマッピングを適用
        /// </summary>
        private static void ApplyFallbackMapping(GameObject avatarObject, GameObject costumeObject, Dictionary<Transform, Transform> mapping) {
            Animator avatarAnimator = avatarObject.GetComponent<Animator>();
            if (avatarAnimator == null || !avatarAnimator.isHuman) return;
            
            // 重要なヒューマノイドボーンのリスト
            HumanBodyBones[] criticalBones = new HumanBodyBones[] {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
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
            
            foreach (HumanBodyBones boneType in criticalBones) {
                Transform avatarBone = avatarAnimator.GetBoneTransform(boneType);
                if (avatarBone == null) continue;
                
                // マッピングが既に存在するかチェック
                if (mapping.ContainsKey(avatarBone)) continue;
                
                // まだマッピングがない場合、最良の推測を行う
                string normalizedType = boneType.ToString().ToLowerInvariant();
                
                // 位置ベースでマッチング
                Transform[] costumeBones = costumeObject.GetComponentsInChildren<Transform>();
                Transform costumeRoot = FindRootBone(costumeObject);
                
                Transform bestMatch = FindBoneByPosition(costumeBones, normalizedType, costumeRoot);
                if (bestMatch != null) {
                    mapping[avatarBone] = bestMatch;
                }
            }
        }
        
        /// <summary>
        /// スキンメッシュレンダラーのボーンを再マッピングする
        /// </summary>
        public static void RemapSkinnedMeshRenderer(SkinnedMeshRenderer renderer, Dictionary<Transform, Transform> boneMapping) {
            if (renderer == null || renderer.bones == null || renderer.bones.Length == 0) return;
            
            // オリジナルボーンの配列
            Transform[] originalBones = renderer.bones;
            
            // 新しいボーン配列（マッピングされたボーンに置き換える）
            Transform[] newBones = new Transform[originalBones.Length];
            
            // 各ボーンをマッピングに基づいて置き換え
            for (int i = 0; i < originalBones.Length; i++) {
                Transform originalBone = originalBones[i];
                
                // マッピングからボーンを探す
                if (originalBone != null && boneMapping.ContainsKey(originalBone)) {
                    newBones[i] = boneMapping[originalBone];
                } else {
                    // マッピングがない場合は元のボーンを維持
                    newBones[i] = originalBone;
                }
            }
            
            // 新しいボーン配列を適用
            renderer.bones = newBones;
            
            // ルートボーンも更新
            if (renderer.rootBone != null && boneMapping.ContainsKey(renderer.rootBone)) {
                renderer.rootBone = boneMapping[renderer.rootBone];
            }
        }
    }
}