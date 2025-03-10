using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace ZundakaiTools {
    /// <summary>
    /// 異なるボーン構造間でマッピングを行うための高度なボーンマッパー
    /// </summary>
    public class BoneMapper {
        // 一般的なボーン名の対応表（異なる命名規則への対応）
        private static readonly Dictionary<string, List<string>> boneNameMappings = new Dictionary<string, List<string>>() {
            // 頭部関連
            { "head", new List<string>() { "head", "face", "頭", "face", "头", "kubi", "atama", "头部", "頭部", "顔", "かお", "カオ", "kao" } },
            { "neck", new List<string>() { "neck", "首", "颈", "kubi", "くび", "颈部", "首部", "クビ", "くび" } },
            
            // 体幹部
            { "hips", new List<string>() { "hips", "pelvis", "hip", "腰", "臀部", "hüfte", "骨盤", "koshi", "腰部", "臀", "腰骨", "pelv", "ヒップ", "ヒプ", "hipbone" } },
            { "spine", new List<string>() { "spine", "chest", "背骨", "脊椎", "背中", "背", "脊柱", "wirbelsäule", "sebone", "脊柱", "背部", "せぼね", "セボネ", "sebone", "spine1", "胴体", "torso", "体", "karada" } },
            { "chest", new List<string>() { "chest", "torso", "胸", "胸部", "躯干", "brustkorb", "mune", "躯干", "上半身", "chest1", "mune", "むね", "ムネ", "胸骨", "上半身1", "上半身2" } },
            { "upperchest", new List<string>() { "upperchest", "upper_chest", "upper.chest", "upper_torso", "upper.torso", "upperbody", "上半身2", "上胸部", "chest2", "spine2", "spine3", "胸上部", "mune_ue" } },
            
            // 左腕関連
            { "leftshoulder", new List<string>() { "leftshoulder", "l_shoulder", "l.shoulder", "l_clavicle", "l.clavicle", "左肩", "左鎖骨", "left_shoulder", "left.shoulder", "shoulder.l", "shoulder_l", "肩.左", "肩_左", "鎖骨.左", "鎖骨_左", "hidari_kata", "ひだり肩", "ヒダリカタ", "left_collar", "leftcollar", "leftarm0", "l_collar" } },
            { "leftarm", new List<string>() { "leftarm", "l_arm", "l.arm", "l_upperarm", "l.upperarm", "左腕", "左上腕", "left_arm", "left.arm", "arm.l", "arm_l", "腕.左", "腕_左", "上腕.左", "上腕_左", "hidari_ude", "ひだり腕", "ヒダリウデ", "leftarm1", "l_upper_arm", "larm" } },
            { "leftforearm", new List<string>() { "leftforearm", "l_forearm", "l.forearm", "l_lowerarm", "l.lowerarm", "左前腕", "左下腕", "left_forearm", "left.forearm", "forearm.l", "forearm_l", "前腕.左", "前腕_左", "下腕.左", "下腕_左", "hidari_hiji", "ひだりひじ", "ヒダリヒジ", "leftarm2", "l_elbow", "lelbow", "l_lower_arm" } },
            { "lefthand", new List<string>() { "lefthand", "l_hand", "l.hand", "左手", "left_hand", "left.hand", "hand.l", "hand_l", "手.左", "手_左", "hidari_te", "ひだり手", "ヒダリテ", "lhand", "l_wrist", "leftarm3", "lwrist" } },
            
            // 右腕関連
            { "rightshoulder", new List<string>() { "rightshoulder", "r_shoulder", "r.shoulder", "r_clavicle", "r.clavicle", "右肩", "右鎖骨", "right_shoulder", "right.shoulder", "shoulder.r", "shoulder_r", "肩.右", "肩_右", "鎖骨.右", "鎖骨_右", "migi_kata", "みぎ肩", "ミギカタ", "right_collar", "rightcollar", "rightarm0", "r_collar" } },
            { "rightarm", new List<string>() { "rightarm", "r_arm", "r.arm", "r_upperarm", "r.upperarm", "右腕", "右上腕", "right_arm", "right.arm", "arm.r", "arm_r", "腕.右", "腕_右", "上腕.右", "上腕_右", "migi_ude", "みぎ腕", "ミギウデ", "rightarm1", "r_upper_arm", "rarm" } },
            { "rightforearm", new List<string>() { "rightforearm", "r_forearm", "r.forearm", "r_lowerarm", "r.lowerarm", "右前腕", "右下腕", "right_forearm", "right.forearm", "forearm.r", "forearm_r", "前腕.右", "前腕_右", "下腕.右", "下腕_右", "migi_hiji", "みぎひじ", "ミギヒジ", "rightarm2", "r_elbow", "relbow", "r_lower_arm" } },
            { "righthand", new List<string>() { "righthand", "r_hand", "r.hand", "右手", "right_hand", "right.hand", "hand.r", "hand_r", "手.右", "手_右", "migi_te", "みぎ手", "ミギテ", "rhand", "r_wrist", "rightarm3", "rwrist" } },
            
            // 左脚関連
            { "leftupleg", new List<string>() { "leftupleg", "l_upleg", "l.upleg", "l_thigh", "l.thigh", "左大腿", "左太もも", "left_upleg", "left.upleg", "upleg.l", "upleg_l", "大腿.左", "大腿_左", "太もも.左", "太もも_左", "hidari_momo", "ひだり太もも", "ヒダリモモ", "leftleg0", "l_upper_leg", "l_hip", "lhip" } },
            { "leftleg", new List<string>() { "leftleg", "l_leg", "l.leg", "l_calf", "l.calf", "左下腿", "左脛", "left_leg", "left.leg", "leg.l", "leg_l", "下腿.左", "下腿_左", "脛.左", "脛_左", "hidari_sune", "ひだり脛", "ヒダリスネ", "leftleg1", "l_knee", "lknee", "l_lower_leg", "l_calf" } },
            { "leftfoot", new List<string>() { "leftfoot", "l_foot", "l.foot", "左足", "left_foot", "left.foot", "foot.l", "foot_l", "足.左", "足_左", "hidari_ashi", "ひだり足", "ヒダリアシ", "l_ankle", "lankle", "leftleg2", "l_foot_base" } },
            { "lefttoebase", new List<string>() { "lefttoebase", "l_toe", "l.toe", "l_toebase", "l.toebase", "左つま先", "left_toe", "left.toe", "toe.l", "toe_l", "つま先.左", "つま先_左", "hidari_tsumasaki", "ひだりつま先", "ヒダリツマサキ", "l_toe_end", "l_toe_tip", "l_ball", "leftball", "lball" } },
            
            // 右脚関連
            { "rightupleg", new List<string>() { "rightupleg", "r_upleg", "r.upleg", "r_thigh", "r.thigh", "右大腿", "右太もも", "right_upleg", "right.upleg", "upleg.r", "upleg_r", "大腿.右", "大腿_右", "太もも.右", "太もも_右", "migi_momo", "みぎ太もも", "ミギモモ", "rightleg0", "r_upper_leg", "r_hip", "rhip" } },
            { "rightleg", new List<string>() { "rightleg", "r_leg", "r.leg", "r_calf", "r.calf", "右下腿", "右脛", "right_leg", "right.leg", "leg.r", "leg_r", "下腿.右", "下腿_右", "脛.右", "脛_右", "migi_sune", "みぎ脛", "ミギスネ", "rightleg1", "r_knee", "rknee", "r_lower_leg", "r_calf" } },
            { "rightfoot", new List<string>() { "rightfoot", "r_foot", "r.foot", "右足", "right_foot", "right.foot", "foot.r", "foot_r", "足.右", "足_右", "migi_ashi", "みぎ足", "ミギアシ", "r_ankle", "rankle", "rightleg2", "r_foot_base" } },
            { "righttoebase", new List<string>() { "righttoebase", "r_toe", "r.toe", "r_toebase", "r.toebase", "右つま先", "right_toe", "right.toe", "toe.r", "toe_r", "つま先.右", "つま先_右", "migi_tsumasaki", "みぎつま先", "ミギツマサキ", "r_toe_end", "r_toe_tip", "r_ball", "rightball", "rball" } },
            
            // 指関連
            { "leftthumb", new List<string>() { "leftthumb", "l_thumb", "l.thumb", "左親指", "left_thumb", "left.thumb", "thumb.l", "thumb_l", "親指.左", "親指_左", "hidari_oyayubi", "l_thumb0", "l_thumb1", "l_thumb2", "l_thumb3", "ひだり親指", "ヒダリオヤユビ" } },
            { "leftindex", new List<string>() { "leftindex", "l_index", "l.index", "左人差し指", "left_index", "left.index", "index.l", "index_l", "人差し指.左", "人差し指_左", "hidari_hitosashiyubi", "l_index0", "l_index1", "l_index2", "l_index3", "ひだり人差し指", "ヒダリヒトサシユビ" } },
            { "leftmiddle", new List<string>() { "leftmiddle", "l_middle", "l.middle", "左中指", "left_middle", "left.middle", "middle.l", "middle_l", "中指.左", "中指_左", "hidari_nakayubi", "l_middle0", "l_middle1", "l_middle2", "l_middle3", "ひだり中指", "ヒダリナカユビ" } },
            { "leftring", new List<string>() { "leftring", "l_ring", "l.ring", "左薬指", "left_ring", "left.ring", "ring.l", "ring_l", "薬指.左", "薬指_左", "hidari_kusuriyubi", "l_ring0", "l_ring1", "l_ring2", "l_ring3", "ひだり薬指", "ヒダリクスリユビ" } },
            { "leftlittle", new List<string>() { "leftlittle", "l_little", "l.little", "左小指", "left_little", "left.little", "little.l", "little_l", "小指.左", "小指_左", "hidari_koyubi", "l_little0", "l_little1", "l_little2", "l_little3", "ひだり小指", "ヒダリコユビ", "l_pinky", "leftpinky", "lpinky" } },
            
            { "rightthumb", new List<string>() { "rightthumb", "r_thumb", "r.thumb", "右親指", "right_thumb", "right.thumb", "thumb.r", "thumb_r", "親指.右", "親指_右", "migi_oyayubi", "r_thumb0", "r_thumb1", "r_thumb2", "r_thumb3", "みぎ親指", "ミギオヤユビ" } },
            { "rightindex", new List<string>() { "rightindex", "r_index", "r.index", "右人差し指", "right_index", "right.index", "index.r", "index_r", "人差し指.右", "人差し指_右", "migi_hitosashiyubi", "r_index0", "r_index1", "r_index2", "r_index3", "みぎ人差し指", "ミギヒトサシユビ" } },
            { "rightmiddle", new List<string>() { "rightmiddle", "r_middle", "r.middle", "右中指", "right_middle", "right.middle", "middle.r", "middle_r", "中指.右", "中指_右", "migi_nakayubi", "r_middle0", "r_middle1", "r_middle2", "r_middle3", "みぎ中指", "ミギナカユビ" } },
            { "rightring", new List<string>() { "rightring", "r_ring", "r.ring", "右薬指", "right_ring", "right.ring", "ring.r", "ring_r", "薬指.右", "薬指_右", "migi_kusuriyubi", "r_ring0", "r_ring1", "r_ring2", "r_ring3", "みぎ薬指", "ミギクスリユビ" } },
            { "rightlittle", new List<string>() { "rightlittle", "r_little", "r.little", "右小指", "right_little", "right.little", "little.r", "little_r", "小指.右", "小指_右", "migi_koyubi", "r_little0", "r_little1", "r_little2", "r_little3", "みぎ小指", "ミギコユビ", "r_pinky", "rightpinky", "rpinky" } }
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
            if (string.IsNullOrEmpty(boneName)) return "";
            
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
            // 名前完全一致を優先的に探す
            foreach (Transform bone in bones) {
                if (bone == null) continue;
                
                string normalizedType = GetNormalizedBoneType(bone.name);
                if (normalizedType == boneType) {
                    return bone;
                }
            }
            
            // 部分一致も検索（より広範囲に）
            foreach (Transform bone in bones) {
                if (bone == null) continue;
                
                string normalizedBoneName = bone.name.ToLowerInvariant().Replace(".", "").Replace("_", "").Replace(" ", "");
                
                if (boneNameMappings.TryGetValue(boneType, out List<string> variants)) {
                    foreach (string variant in variants) {
                        if (normalizedBoneName.Contains(variant)) {
                            return bone;
                        }
                    }
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
                        // 2段階の子までチェック（直接の子と孫）
                        foreach (Transform bone in bones) {
                            if (bone == null || bone.parent == null) continue;
                            
                            // 親が一致するか
                            if (bone.parent == parentBone && !boneTypes.ContainsKey(bone)) {
                                // このボーンは適切な親を持ち、まだ識別されていない
                                return bone;
                            }
                            
                            // 親の親が一致するか（孫レベル）
                            if (bone.parent.parent != null && bone.parent.parent == parentBone && !boneTypes.ContainsKey(bone)) {
                                // 孫ボーンも候補に含める
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
            
            // バウンディングボックスがフラットにならないように調整
            boundsSize.x = Mathf.Max(boundsSize.x, 0.1f);
            boundsSize.y = Mathf.Max(boundsSize.y, 0.1f);
            boundsSize.z = Mathf.Max(boundsSize.z, 0.1f);
            
            // ボーン位置を正規化し、最も近いボーンを探す
            foreach (Transform bone in bones) {
                if (bone == null) continue;
                
                // ボーン位置を正規化 (-1～1の範囲)
                Vector3 normalizedPos = (bone.position - boundsCenter);
                normalizedPos.x /= (boundsSize.x * 0.5f);
                normalizedPos.y /= (boundsSize.y * 0.5f);
                normalizedPos.z /= (boundsSize.z * 0.5f);
                
                // 目標位置との距離を計算
                float distance = Vector3.Distance(normalizedPos, targetPosition);
                
                // 最も近いボーンを更新
                if (distance < minDistance) {
                    minDistance = distance;
                    closestBone = bone;
                }
            }
            
            // 閾値以内の場合のみ結果を返す
            if (minDistance < 0.6f) {  // 閾値を少し緩和
                return closestBone;
            }
            
            return null;
        }