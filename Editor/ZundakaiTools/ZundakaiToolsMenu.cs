using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    public static class ZundakaiToolsMenu {
        // 衣装調整ツールのみを表示（他のメニュー項目は削除）
        [MenuItem("ずん解/衣装調整ツール")]
        public static void ShowCostumeFitter() {
            CostumeFitter.ShowWindow();
        }
    }
}