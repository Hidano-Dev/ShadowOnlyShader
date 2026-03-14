using UnityEditor;
using UnityEngine;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// VirtualLightの方向・描画範囲をSceneビューでGizmoとして表示する。
    /// Orthographicの場合は矩形、Perspectiveの場合は錐台を描画する。
    /// Near/Farクリップ面の視覚化を含む。
    /// </summary>
    public static class VirtualLightGizmoDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawGizmo(VirtualLight virtualLight, GizmoType gizmoType)
        {
            if (virtualLight == null) return;

            // SourceLight設定時はSourceLightのTransformと投影パラメータで描画する
            bool useLightTransform = virtualLight.SourceLight != null;
            Transform drawTransform = useLightTransform
                ? virtualLight.SourceLight.transform
                : virtualLight.transform;

            Color gizmoColor = (gizmoType & GizmoType.Selected) != 0
                ? new Color(1f, 0.8f, 0f, 0.8f)
                : new Color(1f, 0.8f, 0f, 0.3f);

            Gizmos.color = gizmoColor;

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(drawTransform.position, drawTransform.rotation, Vector3.one);

            // SourceLight設定時はLightから投影パラメータを取得
            ProjectionMode projMode;
            float fov, orthoSize, near, far;

            if (useLightTransform)
            {
                Light light = virtualLight.SourceLight;
                near = virtualLight.NearClipPlane;

                switch (light.type)
                {
                    case LightType.Spot:
                        projMode = ProjectionMode.Perspective;
                        fov = light.spotAngle;
                        orthoSize = virtualLight.OrthographicSize;
                        far = light.range;
                        break;
                    case LightType.Point:
                        projMode = ProjectionMode.Perspective;
                        fov = virtualLight.FieldOfView;
                        orthoSize = virtualLight.OrthographicSize;
                        far = light.range;
                        break;
                    default: // Directional
                        projMode = ProjectionMode.Orthographic;
                        fov = virtualLight.FieldOfView;
                        orthoSize = virtualLight.OrthographicSize;
                        far = virtualLight.FarClipPlane;
                        break;
                }
            }
            else
            {
                projMode = virtualLight.ProjectionMode;
                fov = virtualLight.FieldOfView;
                orthoSize = virtualLight.OrthographicSize;
                near = virtualLight.NearClipPlane;
                far = virtualLight.FarClipPlane;
            }

            if (projMode == ProjectionMode.Orthographic)
            {
                DrawOrthographicGizmo(orthoSize, near, far);
            }
            else
            {
                DrawPerspectiveGizmo(fov, near, far);
            }

            Gizmos.matrix = oldMatrix;
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
