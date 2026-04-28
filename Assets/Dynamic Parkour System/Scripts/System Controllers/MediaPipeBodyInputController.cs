using Climbing;
using System;
using Mediapipe;
using Mediapipe.Unity.Sample.Holistic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Climbing
{
    /// <summary>
    /// Converts MediaPipe Holistic landmarks to the existing parkour controller input.
    /// </summary>
    [RequireComponent(typeof(InputCharacterController))]
    public class MediaPipeBodyInputController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HolisticTrackingSolution holisticSolution;
        [SerializeField] private InputCharacterController characterInput;

        [Header("Movement Mapping")]
        [Range(0.05f, 0.4f)][SerializeField] private float leanDeadzone = 0.12f;
        [Range(0.5f, 2f)][SerializeField] private float horizontalSensitivity = 1.2f;
        [Range(0f, 1f)][SerializeField] private float maxHorizontal = 1f;
        [Range(0f, 1f)][SerializeField] private float forwardValue = 1f;
        [SerializeField] private bool invertHorizontal = true;
        [Range(1f, 20f)][SerializeField] private float movementSmooth = 8f;

        [Header("Gesture Thresholds")]
        [Tooltip("Wrist must be this much higher (smaller y) than shoulder to trigger jump.")]
        [Range(0.03f, 0.3f)][SerializeField] private float jumpRaiseThreshold = 0.1f;
        [Tooltip("Wrist must be this much lower (larger y) than hip to trigger drop/slide.")]
        [Range(0.03f, 0.3f)][SerializeField] private float dropLowerThreshold = 0.1f;
        [Tooltip("Gesture lock cooldown (seconds), prevents repeated triggers every frame.")]
        [Range(0.05f, 1f)][SerializeField] private float gestureCooldown = 0.25f;

        [Header("Running")]
        [SerializeField] private bool autoRun = true;
        [SerializeField] private bool overrideKeyboardWhenTracking = true;

        [Header("Tracking Safety")]
        [Tooltip("If no new landmarks arrive within this time, fallback to normal keyboard/touch input.")]
        [Range(0.1f, 2f)][SerializeField] private float trackingLostTimeout = 0.6f;

        private readonly object _sync = new object();
        private Vector2 _targetMove;
        private Vector2 _smoothedMove;
        private bool _pendingJump;
        private bool _pendingDrop;
        private double _nextGestureRealtimeSec;
        private double _lastLandmarkRealtimeSec = -999d;

        private void Awake()
        {
            if (characterInput == null)
            {
                characterInput = GetComponent<InputCharacterController>();
            }
        }

        private void OnEnable()
        {
            if (holisticSolution == null)
            {
                holisticSolution = FindSolutionInActiveScene();
            }

            if (holisticSolution != null)
            {
                holisticSolution.OnHolisticLandmarksOutput += OnHolisticLandmarksOutput;
            }
            else
            {
                Debug.LogWarning("[MediaPipeBodyInputController] No HolisticTrackingSolution found in scene.");
            }
        }

        private void OnDisable()
        {
            if (holisticSolution != null)
            {
                holisticSolution.OnHolisticLandmarksOutput -= OnHolisticLandmarksOutput;
            }
        }

        private void Update()
        {
            var hasFreshTracking = (NowRealtimeSeconds() - _lastLandmarkRealtimeSec) <= trackingLostTimeout;
            if (!hasFreshTracking || !overrideKeyboardWhenTracking)
            {
                // No reliable body data: keep existing keyboard/gamepad/touch control path.
                return;
            }

            Vector2 targetMove;
            bool jump;
            bool drop;

            lock (_sync)
            {
                targetMove = _targetMove;
                jump = _pendingJump;
                drop = _pendingDrop;
                _pendingJump = false;
                _pendingDrop = false;
            }

            _smoothedMove = Vector2.Lerp(_smoothedMove, targetMove, Time.deltaTime * movementSmooth);

            characterInput.movement = _smoothedMove;
            characterInput.run = autoRun;

            // One-frame pulse inputs for actions.
            characterInput.jump = jump;
            characterInput.drop = drop;
        }

        private void OnHolisticLandmarksOutput(NormalizedLandmarkList poseLandmarks, NormalizedLandmarkList leftHandLandmarks, NormalizedLandmarkList rightHandLandmarks)
        {
            if (poseLandmarks == null || poseLandmarks.Landmark == null || poseLandmarks.Landmark.Count < 25)
            {
                return;
            }

            var leftShoulder = poseLandmarks.Landmark[11];
            var rightShoulder = poseLandmarks.Landmark[12];
            var leftHip = poseLandmarks.Landmark[23];
            var rightHip = poseLandmarks.Landmark[24];

            var shoulderCenterX = (leftShoulder.X + rightShoulder.X) * 0.5f;
            var hipCenterX = (leftHip.X + rightHip.X) * 0.5f;
            var leanX = shoulderCenterX - hipCenterX;
            if (invertHorizontal)
            {
                leanX = -leanX;
            }

            var horizontal = ApplyDeadzone(leanX, leanDeadzone) * horizontalSensitivity;
            horizontal = Mathf.Clamp(horizontal, -maxHorizontal, maxHorizontal);

            bool jump = false;
            bool drop = false;

            var nowSec = NowRealtimeSeconds();
            if (nowSec >= _nextGestureRealtimeSec && leftHandLandmarks != null && rightHandLandmarks != null &&
                leftHandLandmarks.Landmark != null && rightHandLandmarks.Landmark != null &&
                leftHandLandmarks.Landmark.Count > 0 && rightHandLandmarks.Landmark.Count > 0)
            {
                // Use wrist landmark index 0 from hand landmark list.
                var leftWrist = leftHandLandmarks.Landmark[0];
                var rightWrist = rightHandLandmarks.Landmark[0];

                var shoulderY = (leftShoulder.Y + rightShoulder.Y) * 0.5f;
                var hipY = (leftHip.Y + rightHip.Y) * 0.5f;
                var wristY = (leftWrist.Y + rightWrist.Y) * 0.5f;

                if (wristY < shoulderY - jumpRaiseThreshold)
                {
                    jump = true;
                    _nextGestureRealtimeSec = nowSec + gestureCooldown;
                }
                else if (wristY > hipY + dropLowerThreshold)
                {
                    drop = true;
                    _nextGestureRealtimeSec = nowSec + gestureCooldown;
                }
            }

            lock (_sync)
            {
                _targetMove = new Vector2(horizontal, forwardValue);
                if (jump) _pendingJump = true;
                if (drop) _pendingDrop = true;
                _lastLandmarkRealtimeSec = nowSec;
            }
        }

        private HolisticTrackingSolution FindSolutionInActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return null;
            }

            var roots = activeScene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var solution = root.GetComponentInChildren<HolisticTrackingSolution>(true);
                if (solution != null)
                {
                    return solution;
                }
            }

            return null;
        }

        private static float ApplyDeadzone(float value, float deadzone)
        {
            if (Mathf.Abs(value) < deadzone)
            {
                return 0f;
            }

            return Mathf.Sign(value) * ((Mathf.Abs(value) - deadzone) / (1f - deadzone));
        }

        private static double NowRealtimeSeconds()
        {
            return (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
        }
    }
}