using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Player.Domain;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Shared.Application.Interfaces;
using DeviceType = FloorBreaker.Shared.Application.Interfaces.DeviceType;

namespace FloorBreaker.Cameras.Presentation
{
    /// <summary>
    /// 観戦カメラ。パン・ズーム・プレイヤー追従切り替えに対応。
    /// 全員 CPU 時または Human 死亡後にアクティブになる。
    /// </summary>
    public sealed class SpectatorCamera
    {
        private const float CameraZ = -10f;
        private const float PanSpeed = 108f;
        private const float ZoomSpeed = 8f;
        private const float MinZoom = 3f;
        private const float MaxZoom = 15f;
        private const float SmoothSpeed = 6f;

        private readonly Camera _camera;
        private readonly StageBounds _bounds;
        private readonly IReadOnlyList<PlayerModel> _allPlayers;
        private readonly DeviceType _deviceType;
        private readonly int _gamepadIndex;

        private Vector3 _position;
        private float _zoom;
        private int _followTargetIndex = -1; // -1 = フリーカメラ
        private Vector2 _panInput;
        private float _zoomInput;

        public DeviceType DeviceType => _deviceType;
        public int GamepadIndex => _gamepadIndex;

        public SpectatorCamera(
            Camera camera, StageBounds bounds, IReadOnlyList<PlayerModel> allPlayers,
            DeviceType deviceType = DeviceType.None, int gamepadIndex = -1)
        {
            _camera = camera;
            _bounds = bounds;
            _allPlayers = allPlayers;
            _deviceType = deviceType;
            _gamepadIndex = gamepadIndex;
            _zoom = camera.orthographicSize;

            // ステージ中央に初期配置
            var range = bounds.Current;
            float cx = (range.MinX + range.MaxX + 1) * 0.5f;
            float cy = (range.MinY + range.MaxY + 1) * 0.5f;
            _position = new Vector3(cx, cy, CameraZ);
            camera.transform.position = _position;
        }

        /// <summary>パン入力 (WASD / 左スティック)。</summary>
        public void SetPanInput(Vector2 input)
        {
            _panInput = input;
            // パン操作したら追従解除
            if (input.sqrMagnitude > 0.1f)
                _followTargetIndex = -1;
        }

        /// <summary>ズーム入力 (マウスホイール / 右スティック Y)。</summary>
        public void SetZoomInput(float input) => _zoomInput = input;

        /// <summary>次の生存プレイヤーに追従切り替え。</summary>
        public void CycleFollowTarget()
        {
            int start = _followTargetIndex + 1;
            for (int i = 0; i < _allPlayers.Count; i++)
            {
                int idx = (start + i) % _allPlayers.Count;
                if (!_allPlayers[idx].Stats.IsDead)
                {
                    _followTargetIndex = idx;
                    return;
                }
            }
            _followTargetIndex = -1; // 全員死亡
        }

        public void Tick(float deltaTime)
        {
            // ズーム
            _zoom = Mathf.Clamp(_zoom + _zoomInput * ZoomSpeed * deltaTime, MinZoom, MaxZoom);
            _camera.orthographicSize = _zoom;

            // ターゲット位置の決定
            Vector3 target;
            if (_followTargetIndex >= 0 && _followTargetIndex < _allPlayers.Count)
            {
                var player = _allPlayers[_followTargetIndex];
                if (player.Stats.IsDead)
                {
                    // 追従先が死亡した場合、次に切り替え
                    CycleFollowTarget();
                }

                if (_followTargetIndex >= 0)
                    target = _allPlayers[_followTargetIndex].CurrentPosition.ToWorldCenter().ToVector3(CameraZ);
                else
                    target = _position; // フリーカメラ
            }
            else
            {
                // フリーカメラ: パン入力で移動
                target = _position + (Vector3)(_panInput * PanSpeed * deltaTime);
            }

            // 境界クランプ
            target = ClampToBounds(target);

            // スムーズ移動
            _position = Vector3.Lerp(_position, target, SmoothSpeed * deltaTime);
            _position.z = CameraZ;
            _camera.transform.position = _position;
        }

        private Vector3 ClampToBounds(Vector3 pos)
        {
            var range = _bounds.Current;
            float aspect = _camera.aspect;
            float halfH = _zoom;
            float halfW = _zoom * aspect;

            float worldMinX = range.MinX;
            float worldMaxX = range.MaxX + 1f;
            float worldMinY = range.MinY;
            float worldMaxY = range.MaxY + 1f;

            float cx = Mathf.Clamp(pos.x, worldMinX + halfW, worldMaxX - halfW);
            float cy = Mathf.Clamp(pos.y, worldMinY + halfH, worldMaxY - halfH);
            return new Vector3(cx, cy, pos.z);
        }
    }
}
