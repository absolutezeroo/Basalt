using UnityEngine;
using UnityEngine.InputSystem;

namespace Basalt.Client
{
    /// <summary>
    /// Noclip fly camera for testing terrain generation.
    /// WASD to move, mouse to look, Space/Shift to go up/down, scroll to change speed.
    /// Attach to a GameObject with a Camera; assign its Transform to ChunkManager._playerTransform.
    /// </summary>
    public class FlyCamera : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 20f;
        [SerializeField] private float _fastMultiplier = 3f;
        [SerializeField] private float _scrollStep = 5f;

        [Header("Look")]
        [SerializeField] private float _sensitivity = 2f;
        [SerializeField] private float _maxPitch = 89f;

        [Header("Start Position")]
        [SerializeField] private Vector3 _spawnPosition = new(0f, 80f, 0f);

        private float _yaw;
        private float _pitch;
        private bool _cursorLocked;

        private Keyboard _kb;
        private Mouse _mouse;

        private void Start()
        {
            transform.position = _spawnPosition;
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
            SetCursorLock(true);
        }

        private void Update()
        {
            _kb = Keyboard.current;
            _mouse = Mouse.current;
            if (_kb == null || _mouse == null) return;

            HandleCursorToggle();

            if (_cursorLocked)
            {
                HandleLook();
                HandleMovement();
                HandleSpeedScroll();
            }
        }

        private void HandleCursorToggle()
        {
            if (_kb.escapeKey.wasPressedThisFrame)
            {
                SetCursorLock(!_cursorLocked);
            }

            if (!_cursorLocked && _mouse.leftButton.wasPressedThisFrame)
            {
                SetCursorLock(true);
            }
        }

        private void HandleLook()
        {
            Vector2 delta = _mouse.delta.ReadValue();
            float mx = delta.x * _sensitivity * 0.1f;
            float my = delta.y * _sensitivity * 0.1f;

            _yaw += mx;
            _pitch -= my;
            _pitch = Mathf.Clamp(_pitch, -_maxPitch, _maxPitch);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleMovement()
        {
            float speed = _moveSpeed;
            if (_kb.leftCtrlKey.isPressed)
            {
                speed *= _fastMultiplier;
            }

            Vector3 dir = Vector3.zero;

            if (_kb.wKey.isPressed) dir += transform.forward;
            if (_kb.sKey.isPressed) dir -= transform.forward;
            if (_kb.dKey.isPressed) dir += transform.right;
            if (_kb.aKey.isPressed) dir -= transform.right;
            if (_kb.spaceKey.isPressed) dir += Vector3.up;
            if (_kb.leftShiftKey.isPressed) dir -= Vector3.up;

            if (dir.sqrMagnitude > 0f)
            {
                transform.position += dir.normalized * (speed * Time.deltaTime);
            }
        }

        private void HandleSpeedScroll()
        {
            float scroll = _mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                _moveSpeed = Mathf.Max(1f, _moveSpeed + Mathf.Sign(scroll) * _scrollStep);
            }
        }

        private void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
