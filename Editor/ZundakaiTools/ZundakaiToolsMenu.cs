using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    public static class ZundakaiToolsMenu {
        // ツールバーにメニューを追加
        [MenuItem("ずん解/About")]
        public static void ShowAbout() {
            EditorUtility.DisplayDialog("ずん解ツール", "全アバター衣装自動調整ツール\nVersion 1.0\n\n衣装をアバターに自動的に合わせるツールです。", "OK");
        }
    }
}