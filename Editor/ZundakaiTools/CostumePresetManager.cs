using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    /// <summary>
    /// 衣装の調整設定をプリセットとして保存・読み込みするマネージャークラス
    /// </summary>
    public class CostumePresetManager : ScriptableObject {
        [System.Serializable]
        public class AdjustmentPreset {
            public string presetName;
            public string avatarName;
            public string costumeName;
            public Dictionary<string, float> adjustmentValues = new Dictionary<string, float>();
            public Dictionary<string, string> boneMapping = new Dictionary<string, string>();
        }
        
        // プリセットのリスト
        [SerializeField]
        private List<AdjustmentPreset> presets = new List<AdjustmentPreset>();
        
        // プリセットを保存するパス
        private const string PRESET_PATH = "Assets/ZundakaiTools/Presets";
        private const string PRESET_FILENAME = "CostumePresets.asset";
        
        // シングルトンインスタンス
        private static CostumePresetManager _instance;
        public static CostumePresetManager Instance {
            get {
                if (_instance == null) {
                    _instance = LoadOrCreateInstance();
                }
                return _instance;
            }
        }
        
        // インスタンスのロードまたは作成
        private static CostumePresetManager LoadOrCreateInstance() {
            string fullPath = Path.Combine(PRESET_PATH, PRESET_FILENAME);
            CostumePresetManager instance = AssetDatabase.LoadAssetAtPath<CostumePresetManager>(fullPath);
            
            if (instance == null) {
                // フォルダが存在しない場合は作成
                if (!Directory.Exists(PRESET_PATH)) {
                    Directory.CreateDirectory(PRESET_PATH);
                    AssetDatabase.Refresh();
                }
                
                // 新しいインスタンスを作成
                instance = CreateInstance<CostumePresetManager>();
                AssetDatabase.CreateAsset(instance, fullPath);
                AssetDatabase.SaveAssets();
            }
            
            return instance;
        }
        
        // プリセットを保存
        public void SavePreset(string presetName, string avatarName, string costumeName, 
                              Dictionary<string, float> adjustmentValues, 
                              Dictionary<string, string> boneMapping) {
            // 既存のプリセットを探す
            AdjustmentPreset existingPreset = presets.Find(p => p.presetName == presetName);
            
            if (existingPreset != null) {
                // 既存のプリセットを更新
                existingPreset.avatarName = avatarName;
                existingPreset.costumeName = costumeName;
                existingPreset.adjustmentValues = new Dictionary<string, float>(adjustmentValues);
                existingPreset.boneMapping = new Dictionary<string, string>(boneMapping);
            } else {
                // 新しいプリセットを作成
                AdjustmentPreset newPreset = new AdjustmentPreset {
                    presetName = presetName,
                    avatarName = avatarName,
                    costumeName = costumeName,
                    adjustmentValues = new Dictionary<string, float>(adjustmentValues),
                    boneMapping = new Dictionary<string, string>(boneMapping)
                };
                
                presets.Add(newPreset);
            }
            
            // アセットを保存
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
        
        // プリセットの読み込み
        public AdjustmentPreset LoadPreset(string presetName) {
            return presets.Find(p => p.presetName == presetName);
        }
        
        // プリセットの削除
        public void DeletePreset(string presetName) {
            presets.RemoveAll(p => p.presetName == presetName);
            
            // アセットを保存
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
        
        // すべてのプリセット名を取得
        public string[] GetAllPresetNames() {
            List<string> names = new List<string>();
            foreach (var preset in presets) {
                names.Add(preset.presetName);
            }
            return names.ToArray();
        }
        
        // 特定のアバターと衣装の組み合わせに対するプリセットを検索
        public AdjustmentPreset FindPresetForAvatarAndCostume(string avatarName, string costumeName) {
            return presets.Find(p => p.avatarName == avatarName && p.costumeName == costumeName);
        }
    }
}
