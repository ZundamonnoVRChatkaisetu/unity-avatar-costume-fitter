using UnityEngine;
using UnityEditor;

namespace ZundakaiTools {
    /// <summary>
    /// エディタ更新を自動的に処理するヘルパークラス
    /// </summary>
    [InitializeOnLoad]
    public static class EditorUpdateHelper {
        // 最後の更新時間
        private static double lastUpdateTime;
        
        // コンストラクタ
        static EditorUpdateHelper() {
            // エディタ更新イベントに登録
            EditorApplication.update += OnEditorUpdate;
            lastUpdateTime = EditorApplication.timeSinceStartup;
        }
        
        // 更新処理
        private static void OnEditorUpdate() {
            // 現在の時間
            double currentTime = EditorApplication.timeSinceStartup;
            
            // 前回の更新から0.1秒経過したら更新
            if (currentTime - lastUpdateTime > 0.1) {
                // シーンビューを強制的に更新
                SceneView.RepaintAll();
                
                // ゲームビューも更新
                EditorApplication.QueuePlayerLoopUpdate();
                
                // 時間を更新
                lastUpdateTime = currentTime;
            }
        }
        
        /// <summary>
        /// 指定されたオブジェクトを強制的に更新する
        /// </summary>
        /// <param name="obj">更新するオブジェクト</param>
        public static void ForceUpdate(Object obj) {
            if (obj == null) return;
            
            // オブジェクトをダーティとしてマーク
            EditorUtility.SetDirty(obj);
            
            // Meshの場合は特別な処理
            if (obj is Mesh mesh) {
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
            }
            
            // SkinnedMeshRendererの場合は特別な処理
            if (obj is SkinnedMeshRenderer renderer) {
                if (renderer.sharedMesh != null) {
                    renderer.sharedMesh.RecalculateBounds();
                }
            }
            
            // GameObjectの場合は子も含めて更新
            if (obj is GameObject gameObject) {
                SkinnedMeshRenderer[] renderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (SkinnedMeshRenderer renderer in renderers) {
                    if (renderer.sharedMesh != null) {
                        renderer.sharedMesh.RecalculateBounds();
                        EditorUtility.SetDirty(renderer.sharedMesh);
                    }
                    EditorUtility.SetDirty(renderer);
                }
                
                // Transformも更新
                foreach (Transform transform in gameObject.GetComponentsInChildren<Transform>()) {
                    EditorUtility.SetDirty(transform);
                }
            }
            
            // シーンを更新
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}