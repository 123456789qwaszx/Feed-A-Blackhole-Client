using UnityEngine;

namespace BlackHole.Unity
{
    // 전투 카메라의 화면비 맞춤. 저작한 카메라 크기(세로 반 폭)와 기준 화면비(16:9)가 보여 주는 가로 폭을 좁은 화면에서도 보장한다.
    // - 기준보다 넓은 화면(20:9 등): 저작한 크기 그대로. 세로는 같고 옆이 더 보인다.
    // - 기준보다 좁은 화면(4:3 등): 가로 폭이 기준과 같아지도록 크기를 키운다. 세로가 더 보인다.
    // 화면 크기가 바뀐 프레임에만 크기를 고친다. 카메라 위치는 건드리지 않는다(카메라 흔들림 등과 함께 쓸 수 있다).
    // 전투 카메라(Main Camera)에 붙인다. 씬에 없으면 GameBootstrap이 붙인다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class BattleCameraFit : MonoBehaviour
    {
        private const float ReferenceAspect = 16f / 9f;

        private Camera _camera;
        // 저작한 크기. 붙는 순간(Awake)의 크기다.
        private float _authoredSize;
        private int _width;
        private int _height;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _authoredSize = _camera.orthographicSize;
            Fit();
        }

        private void Update() => Fit();

        private void Fit()
        {
            if (!_camera.orthographic)
                return;

            int width = _camera.pixelWidth;
            int height = _camera.pixelHeight;

            if (width <= 0 || height <= 0 || width == _width && height == _height)
                return;

            _width = width;
            _height = height;
            _camera.orthographicSize = SizeFor(_authoredSize, (float)width / height);
        }

        // 화면비 aspect(가로/세로)에서의 카메라 크기.
        internal static float SizeFor(float authoredSize, float aspect) =>
            aspect >= ReferenceAspect ? authoredSize : authoredSize * ReferenceAspect / aspect;
    }
}
