using UnityEditor;
using UnityEngine;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// VirtualLightの方向・描画範囲をSceneビューでGizmoとして表示する。
    /// Orthographicの場合は矩形、Perspectiveの場合は錐台、
    /// Pointの場合は全方向を示すワイヤー球を描画する。
    /// Near/Farクリップ面の視覚化を含む。
    /// </summary>
    public static class VirtualLightGizmoDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawGizmo(VirtualLight virtualLight, GizmoType gizmoType)
        {
            if (virtualLight == null) return;

            // 実効値（EffectiveXxx）で描画する。SourceLight設定時は
            // Editモード同期プレビュー・Play中の同期後のどちらでもLightの状態と一致し、
            // 実際の影の投影範囲とGizmoが常に対応する
            Vector3 drawPosition = virtualLight.EffectiveLightPosition;
            Quaternion drawRotation = virtualLight.EffectiveLightRotation;
            ProjectionMode projMode = virtualLight.EffectiveProjectionMode;
            float fov = virtualLight.EffectiveFieldOfView;
            float orthoSize = virtualLight.OrthographicSize;
            float near = virtualLight.NearClipPlane;
            float far = virtualLight.EffectiveFarClipPlane;

            Color gizmoColor = (gizmoType & GizmoType.Selected) != 0
                ? new Color(1f, 0.8f, 0f, 0.8f)
                : new Color(1f, 0.8f, 0f, 0.3f);

            Gizmos.color = gizmoColor;

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(drawPosition, drawRotation, Vector3.one);

            if (projMode == ProjectionMode.Point)
            {
                // Pointモードの投影はワールド軸基準で回転に依存しないため、
                // 回転を含まない行列で描画する
                Gizmos.matrix = Matrix4x4.Translate(drawPosition);
                DrawPointGizmo(near, far);
            }
            else if (projMode == ProjectionMode.Orthographic)
            {
                if (virtualLight.FitToCasters && TryGetFittedWindow(virtualLight, out Rect window))
                {
                    // Fitの投影ウィンドウは実効View空間（Editモード同期プレビュー中は
                    // SourceLight基準）で算出されるため、設定済みのGizmos.matrixと基準が一致する
                    DrawOffCenterOrthographicGizmo(window, near, far);
                }
                else
                {
                    DrawOrthographicGizmo(orthoSize, near, far);
                }
            }
            else
            {
                DrawPerspectiveGizmo(fov, near, far);
            }

            Gizmos.matrix = oldMatrix;
        }

        /// <summary>
        /// Fit To Castersで算出された投影ウィンドウを取得する。
        /// Editモードでは行列がLateUpdateで更新されないため、ここで明示的に再計算する。
        /// </summary>
        private static bool TryGetFittedWindow(VirtualLight light, out Rect window)
        {
            light.UpdateMatrices();
            return light.TryGetFittedOrthoWindow(out window);
        }

        private static void DrawOffCenterOrthographicGizmo(Rect window, float near, float far)
        {
            Vector2 center = window.center;
            float halfWidth = window.width * 0.5f;
            float halfHeight = window.height * 0.5f;

            DrawWireRect(new Vector3(center.x, center.y, near), halfWidth, halfHeight);
            DrawWireRect(new Vector3(center.x, center.y, far), halfWidth, halfHeight);

            Gizmos.DrawLine(
                new Vector3(center.x - halfWidth, center.y - halfHeight, near),
                new Vector3(center.x - halfWidth, center.y - halfHeight, far));
            Gizmos.DrawLine(
                new Vector3(center.x + halfWidth, center.y - halfHeight, near),
                new Vector3(center.x + halfWidth, center.y - halfHeight, far));
            Gizmos.DrawLine(
                new Vector3(center.x + halfWidth, center.y + halfHeight, near),
                new Vector3(center.x + halfWidth, center.y + halfHeight, far));
            Gizmos.DrawLine(
                new Vector3(center.x - halfWidth, center.y + halfHeight, near),
                new Vector3(center.x - halfWidth, center.y + halfHeight, far));
        }

        private static void DrawOrthographicGizmo(float size, float near, float far)
        {
            // Near clip plane (rectangle)
            float halfSize = size;
            Vector3 nearCenter = new Vector3(0f, 0f, near);
            Vector3 farCenter = new Vector3(0f, 0f, far);

            // Draw near plane wireframe
            DrawWireRect(nearCenter, halfSize, halfSize);

            // Draw far plane wireframe
            DrawWireRect(farCenter, halfSize, halfSize);

            // Draw connecting edges (near to far corners)
            Gizmos.DrawLine(new Vector3(-halfSize, -halfSize, near), new Vector3(-halfSize, -halfSize, far));
            Gizmos.DrawLine(new Vector3(halfSize, -halfSize, near), new Vector3(halfSize, -halfSize, far));
            Gizmos.DrawLine(new Vector3(halfSize, halfSize, near), new Vector3(halfSize, halfSize, far));
            Gizmos.DrawLine(new Vector3(-halfSize, halfSize, near), new Vector3(-halfSize, halfSize, far));
        }

        private static void DrawPerspectiveGizmo(float fov, float near, float far)
        {
            // 1:1 aspect ratio (square depth texture)
            float nearHalf = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float farHalf = far * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            // Draw near plane
            DrawWireRect(new Vector3(0f, 0f, near), nearHalf, nearHalf);

            // Draw far plane
            DrawWireRect(new Vector3(0f, 0f, far), farHalf, farHalf);

            // Draw connecting edges (frustum edges)
            Gizmos.DrawLine(new Vector3(-nearHalf, -nearHalf, near), new Vector3(-farHalf, -farHalf, far));
            Gizmos.DrawLine(new Vector3(nearHalf, -nearHalf, near), new Vector3(farHalf, -farHalf, far));
            Gizmos.DrawLine(new Vector3(nearHalf, nearHalf, near), new Vector3(farHalf, farHalf, far));
            Gizmos.DrawLine(new Vector3(-nearHalf, nearHalf, near), new Vector3(-farHalf, farHalf, far));
        }

        private static void DrawPointGizmo(float near, float far)
        {
            // 全方向投影の範囲をワイヤー球で表示（外側=Far、内側=Near）
            Gizmos.DrawWireSphere(Vector3.zero, far);

            // Near球は小さすぎると視認できないため、意味のある大きさの場合のみ描画
            if (near > 0.01f)
            {
                Gizmos.DrawWireSphere(Vector3.zero, near);
            }
        }

        private static void DrawWireRect(Vector3 center, float halfWidth, float halfHeight)
        {
            Vector3 tl = center + new Vector3(-halfWidth, halfHeight, 0f);
            Vector3 tr = center + new Vector3(halfWidth, halfHeight, 0f);
            Vector3 bl = center + new Vector3(-halfWidth, -halfHeight, 0f);
            Vector3 br = center + new Vector3(halfWidth, -halfHeight, 0f);

            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
            Gizmos.DrawLine(bl, tl);
        }
    }
}
