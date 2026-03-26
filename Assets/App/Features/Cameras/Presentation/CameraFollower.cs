using System;
using R3;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Player.Domain;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Cameras.Presentation
{
    /// <summary>
    /// PlayerModel.Position を購読し、カメラ位置を Lerp 追従 + 境界クランプで計算する。
    /// pure C# クラス。Transform への反映は呼び出し元 (SplitScreenCameraSetup) が行う。
    /// </summary>
    public sealed class CameraFollower : IDisposable
    {
        private const float SmoothSpeed = 8f;
        private const float CameraZ = -10f;

        private readonly StageBounds _bounds;
        private readonly float _orthographicSize;
        private readonly float _viewportAspect;
        private readonly IDisposable _subscription;

        private Vector3 _targetPosition;
        private Vector3 _currentPosition;
        private bool _initialized;

        public Vector3 CurrentPosition => _currentPosition;

        /// <param name="model">追従対象の PlayerModel</param>
        /// <param name="bounds">ステージ境界 (縮小に追従)</param>
        /// <param name="orthographicSize">カメラの orthographicSize</param>
        /// <param name="viewportAspect">ビューポートの aspect 比 (width / height)</param>
        public CameraFollower(
            PlayerModel model,
            StageBounds bounds,
            float orthographicSize,
            float viewportAspect)
        {
            _bounds = bounds;
            _orthographicSize = orthographicSize;
            _viewportAspect = viewportAspect;

            _subscription = model.Position.Subscribe(OnPositionChanged);

            // 初期位置に即座にスナップ
            var initWorld = model.CurrentPosition.ToWorldCenter().ToVector3(CameraZ);
            _targetPosition = initWorld;
            _currentPosition = ClampToBounds(initWorld);
            _initialized = true;
        }

        private void OnPositionChanged(GridPos pos)
        {
            _targetPosition = pos.ToWorldCenter().ToVector3(CameraZ);
        }

        /// <summary>
        /// 毎フレーム呼び出し。Lerp で追従し、境界内にクランプした位置を返す。
        /// </summary>
        public Vector3 Tick(float deltaTime)
        {
            if (!_initialized) return _currentPosition;

            var clamped = ClampToBounds(_targetPosition);
            _currentPosition = Vector3.Lerp(_currentPosition, clamped, SmoothSpeed * deltaTime);
            _currentPosition.z = CameraZ;
            return _currentPosition;
        }

        private Vector3 ClampToBounds(Vector3 target)
        {
            var range = _bounds.Current;
            float halfHeight = _orthographicSize;
            float halfWidth = _orthographicSize * _viewportAspect;

            // グリッド座標のワールド範囲: MinX ~ MaxX+1, MinY ~ MaxY+1
            float worldMinX = range.MinX;
            float worldMaxX = range.MaxX + 1f;
            float worldMinY = range.MinY;
            float worldMaxY = range.MaxY + 1f;

            float clampedX = Mathf.Clamp(target.x, worldMinX + halfWidth, worldMaxX - halfWidth);
            float clampedY = Mathf.Clamp(target.y, worldMinY + halfHeight, worldMaxY - halfHeight);

            return new Vector3(clampedX, clampedY, target.z);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
