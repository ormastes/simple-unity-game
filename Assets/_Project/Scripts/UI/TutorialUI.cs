using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ElementalSiege.UI
{
    /// <summary>
    /// Contextual tutorial system that displays step-by-step instructions
    /// with arrow pointers and focus-area masking. Tracks completion via SaveManager.
    /// </summary>
    public class TutorialUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Panel")]
        [SerializeField] private GameObject _tutorialPanel;
        [SerializeField] private CanvasGroup _panelCanvasGroup;

        [Header("Overlay Masking")]
        [SerializeField] private Image _overlayBackground;
        [SerializeField] private Color _overlayColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private RectTransform _highlightCutout;

        [Header("Message")]
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private RectTransform _messageBubble;

        [Header("Arrow Pointer")]
        [SerializeField] private RectTransform _arrowPointer;
        [SerializeField] private Image _arrowImage;
        [SerializeField] private float _arrowBobAmplitude = 10f;
        [SerializeField] private float _arrowBobSpeed = 3f;

        [Header("Navigation")]
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _dismissButton;
        [SerializeField] private TextMeshProUGUI _nextButtonText;
        [SerializeField] private TextMeshProUGUI _stepCounterText;

        [Header("Animation")]
        [SerializeField] private float _fadeInDuration = 0.3f;
        [SerializeField] private float _fadeOutDuration = 0.2f;

        #endregion

        #region Events

        /// <summary>Raised when the entire tutorial sequence is completed or dismissed.</summary>
        public event Action<string> OnTutorialCompleted;

        /// <summary>Raised when a specific step is shown.</summary>
        public event Action<int> OnStepShown;

        #endregion

        #region Private State

        private List<TutorialStep> _steps = new List<TutorialStep>();
        private int _currentStepIndex;
        private string _currentTutorialId;
        private Coroutine _animationCoroutine;
        private Vector3 _arrowBasePosition;
        private ISaveManager _saveManager;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_nextButton != null)
                _nextButton.onClick.AddListener(AdvanceStep);
            if (_dismissButton != null)
                _dismissButton.onClick.AddListener(DismissTutorial);

            if (_tutorialPanel != null)
                _tutorialPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_nextButton != null) _nextButton.onClick.RemoveAllListeners();
            if (_dismissButton != null) _dismissButton.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            AnimateArrow();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Injects the save manager for tracking which tutorials have been shown.
        /// </summary>
        public void SetSaveManager(ISaveManager saveManager)
        {
            _saveManager = saveManager;
        }

        /// <summary>
        /// Starts a tutorial sequence if it has not been shown before.
        /// </summary>
        /// <param name="tutorialId">Unique identifier for this tutorial.</param>
        /// <param name="steps">Ordered list of tutorial steps.</param>
        /// <returns>True if the tutorial was started, false if already completed.</returns>
        public bool TryStartTutorial(string tutorialId, List<TutorialStep> steps)
        {
            if (_saveManager != null && _saveManager.HasShownTutorial(tutorialId))
                return false;

            StartTutorial(tutorialId, steps);
            return true;
        }

        /// <summary>
        /// Forces a tutorial to start regardless of completion status.
        /// </summary>
        public void StartTutorial(string tutorialId, List<TutorialStep> steps)
        {
            if (steps == null || steps.Count == 0) return;

            _currentTutorialId = tutorialId;
            _steps = new List<TutorialStep>(steps);
            _currentStepIndex = 0;

            if (_tutorialPanel != null)
                _tutorialPanel.SetActive(true);

            ShowStep(_currentStepIndex);
        }

        /// <summary>
        /// Returns whether a tutorial is currently active.
        /// </summary>
        public bool IsActive => _tutorialPanel != null && _tutorialPanel.activeSelf;

        #endregion

        #region Step Management

        private void ShowStep(int index)
        {
            if (index < 0 || index >= _steps.Count) return;

            TutorialStep step = _steps[index];

            // Update message
            if (_messageText != null)
                _messageText.text = step.message;

            // Update step counter
            if (_stepCounterText != null)
                _stepCounterText.text = $"{index + 1} / {_steps.Count}";

            // Position arrow
            if (_arrowPointer != null)
            {
                _arrowPointer.gameObject.SetActive(step.showArrow);
                if (step.showArrow)
                {
                    _arrowPointer.anchoredPosition = step.arrowPosition;
                    _arrowPointer.localEulerAngles = new Vector3(0, 0, step.arrowRotation);
                    _arrowBasePosition = _arrowPointer.anchoredPosition;
                }
            }

            // Position highlight cutout
            if (_highlightCutout != null)
            {
                bool hasHighlight = step.highlightArea.size.sqrMagnitude > 0;
                _highlightCutout.gameObject.SetActive(hasHighlight);
                if (hasHighlight)
                {
                    _highlightCutout.anchoredPosition = step.highlightArea.position;
                    _highlightCutout.sizeDelta = step.highlightArea.size;
                }
            }

            // Update overlay
            if (_overlayBackground != null)
                _overlayBackground.color = _overlayColor;

            // Position message bubble near highlight if specified
            if (_messageBubble != null && step.messageBubblePosition != Vector2.zero)
                _messageBubble.anchoredPosition = step.messageBubblePosition;

            // Update button text
            bool isLastStep = index >= _steps.Count - 1;
            if (_nextButtonText != null)
                _nextButtonText.text = isLastStep ? "Got it!" : "Next";

            // Fade in
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(FadePanel(0f, 1f, _fadeInDuration));

            OnStepShown?.Invoke(index);
        }

        private void AdvanceStep()
        {
            _currentStepIndex++;
            if (_currentStepIndex >= _steps.Count)
            {
                CompleteTutorial();
            }
            else
            {
                ShowStep(_currentStepIndex);
            }
        }

        private void DismissTutorial()
        {
            CompleteTutorial();
        }

        private void CompleteTutorial()
        {
            if (_saveManager != null && !string.IsNullOrEmpty(_currentTutorialId))
                _saveManager.MarkTutorialShown(_currentTutorialId);

            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);

            _animationCoroutine = StartCoroutine(FadeOutAndClose());

            OnTutorialCompleted?.Invoke(_currentTutorialId);
        }

        #endregion

        #region Animation

        private void AnimateArrow()
        {
            if (_arrowPointer == null || !_arrowPointer.gameObject.activeSelf) return;

            float bob = Mathf.Sin(Time.unscaledTime * _arrowBobSpeed) * _arrowBobAmplitude;
            Vector3 pos = _arrowBasePosition;
            pos.y += bob;
            _arrowPointer.anchoredPosition = pos;
        }

        private IEnumerator FadePanel(float from, float to, float duration)
        {
            if (_panelCanvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _panelCanvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            _panelCanvasGroup.alpha = to;
            _animationCoroutine = null;
        }

        private IEnumerator FadeOutAndClose()
        {
            if (_panelCanvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < _fadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _panelCanvasGroup.alpha = Mathf.Lerp(1f, 0f,
                        Mathf.Clamp01(elapsed / _fadeOutDuration));
                    yield return null;
                }
                _panelCanvasGroup.alpha = 0f;
            }

            if (_tutorialPanel != null)
                _tutorialPanel.SetActive(false);

            _animationCoroutine = null;
        }

        #endregion
    }

    /// <summary>
    /// Data class representing a single tutorial step.
    /// Can also be used as a ScriptableObject for designer-driven tutorials.
    /// </summary>
    [Serializable]
    public class TutorialStep
    {
        /// <summary>The instruction message to display.</summary>
        [TextArea(2, 5)]
        public string message;

        /// <summary>Whether to show the arrow pointer.</summary>
        public bool showArrow = true;

        /// <summary>Screen-space position of the arrow pointer.</summary>
        public Vector2 arrowPosition;

        /// <summary>Rotation of the arrow (degrees, Z axis).</summary>
        public float arrowRotation;

        /// <summary>Screen-space rect of the highlighted area (position + size).</summary>
        public Rect highlightArea;

        /// <summary>Position for the message bubble.</summary>
        public Vector2 messageBubblePosition;

        /// <summary>Required action type the player must perform to auto-advance (empty = manual).</summary>
        public string requiredAction;
    }

    /// <summary>
    /// ScriptableObject variant of TutorialStep for asset-based tutorial authoring.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTutorialStep", menuName = "Elemental Siege/Tutorial Step")]
    public class TutorialStepAsset : ScriptableObject
    {
        /// <summary>The instruction message to display.</summary>
        [TextArea(3, 6)]
        public string message;

        /// <summary>Whether to show the arrow pointer.</summary>
        public bool showArrow = true;

        /// <summary>Screen-space position of the arrow pointer.</summary>
        public Vector2 arrowPosition;

        /// <summary>Rotation of the arrow (degrees, Z axis).</summary>
        public float arrowRotation;

        /// <summary>Screen-space rect of the highlighted area.</summary>
        public Rect highlightArea;

        /// <summary>Position for the message bubble.</summary>
        public Vector2 messageBubblePosition;

        /// <summary>Required action type the player must perform to auto-advance.</summary>
        public string requiredAction;

        /// <summary>
        /// Converts this asset to a runtime TutorialStep.
        /// </summary>
        public TutorialStep ToRuntimeStep()
        {
            return new TutorialStep
            {
                message = message,
                showArrow = showArrow,
                arrowPosition = arrowPosition,
                arrowRotation = arrowRotation,
                highlightArea = highlightArea,
                messageBubblePosition = messageBubblePosition,
                requiredAction = requiredAction,
            };
        }
    }
}
