using UnityEngine;
using Core.Managers;
using System.Collections.Generic;

namespace Core.Base
{
    /// <summary>
    /// 공용 유틸리티 함수 모음
    /// - 화면 경계/좌표/표시 여부 계산, 숫자 포맷, 셔플 등
    /// </summary>
    public static class Utils
    {
        // 화면 경계 오프셋 설정
        private static float _screenBoundaryOffset = 0.5f;
        private static float _defaultScreenWidth = 10f;
        
        public static float GetScreenWidth()
        {
            if (Camera.main == null) return _defaultScreenWidth; // 기본값
            return Camera.main.orthographicSize * 2 * Camera.main.aspect;
        }

        public static float GetMinX()
        {
            return -GetScreenWidth() / 2 + _screenBoundaryOffset;
        }

        public static float GetMaxX()
        {
            return GetScreenWidth() / 2 - _screenBoundaryOffset;
        }

        // 화면 경계 내 안전한 위치인지 확인
        public static bool IsWithinScreenBounds(Vector3 position)
        {
            float minX = GetMinX();
            float maxX = GetMaxX();
            return position.x >= minX && position.x <= maxX;
        }

        // 위치를 화면 경계 내로 클램프
        public static Vector3 ClampToScreenBounds(Vector3 position)
        {
            float minX = GetMinX();
            float maxX = GetMaxX();
            position.x = Mathf.Clamp(position.x, minX, maxX);
            return position;
        }
        
        public static string FormatNumber(int number)
        {
            if (number >= 1000000000)
                return (number / 1000000000f).ToString("0.#") + " B";
            else if (number >= 1000000)
                return (number / 1000000f).ToString("0.#") + " M";
            else if (number >= 1000)
                return (number / 1000f).ToString("0.#") + " K";
            else
                return number.ToString();
        }

        public static bool IsInCameraView(Vector3 worldPos)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector3 viewportPos = cam.WorldToViewportPoint(worldPos);
            return viewportPos.x >= 0 && viewportPos.x <= 1 && viewportPos.y >= 0 && viewportPos.y <= 1;
        }

        public static void ResetToCanvasBottomCenter(Transform obj)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            float z = obj.position.z;
            float centerX = cam.ViewportToWorldPoint(new Vector3(0.5f, 0, Mathf.Abs(cam.transform.position.z - z))).x;
            float bottomY = WindowManager.Instance.GetCanvasBottomY();
            obj.position = new Vector3(centerX, bottomY, z);
        }

        public static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}