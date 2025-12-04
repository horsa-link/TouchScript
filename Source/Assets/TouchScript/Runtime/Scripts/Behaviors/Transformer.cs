/*
 * @author Valentin Simonov / http://va.lent.in/
 */

using System;
using TouchScript.Debugging.Loggers;
using TouchScript.Gestures;
using TouchScript.Gestures.TransformGestures;
using TouchScript.Gestures.TransformGestures.Base;
using TouchScript.Utils.Attributes;
using UnityEngine;
using UnityEngine.Events;

namespace TouchScript.Behaviors
{
    /// <summary>
    /// Component which transforms an object according to events from transform gestures: <see cref="TransformGesture"/>, <see cref="ScreenTransformGesture"/>, <see cref="PinnedTransformGesture"/> and others.
    /// </summary>
    [AddComponentMenu("TouchScript/Behaviors/Transformer")]
    [HelpURL("http://touchscript.github.io/docs/html/T_TouchScript_Behaviors_Transformer.htm")]
    public class Transformer : MonoBehaviour
    {
        // Here's how it works.
        //
        // If smoothing is not enabled, the component just gets gesture events in stateChangedHandler(), passes Changed event to manualUpdate() which calls applyValues() to sett updated values.
        // The value of transformMask is used to only set values which were changed not to interfere with scripts changing this values.
        //
        // If smoothing is enabled — targetPosition, targetScale, targetRotation are cached and a lerp from current position to these target positions is applied every frame in update() method. It also checks transformMask to change only needed values.
        // If none of the delta values pass the threshold, the component transitions to idle state.

        /// <summary>
        /// State for internal Transformer state machine.
        /// </summary>
        public enum TransformerState
        {
            /// <summary>
            /// Nothing is happening.
            /// </summary>
            Idle,

            /// <summary>
            /// The object is under manual control, i.e. user is transforming it.
            /// </summary>
            Manual,

            /// <summary>
            /// The object is under automatic control, i.e. it's being smoothly moved into target position when user lifted all fingers off.
            /// </summary>
            Automatic
        }

        /// <summary>
        /// Gets or sets a value indicating whether Smoothing is enabled. Smoothing allows to reduce jagged movements but adds some visual lag.
        /// </summary>
        /// <value>
        ///   <c>true</c> if Smoothing is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool EnableSmoothing
        {
            get => enableSmoothing;
            set => enableSmoothing = value;
        }

        /// <summary>
        /// Gets or sets the smoothing factor.
        /// </summary>
        /// <value>
        /// The smoothing factor. Indicates how much smoothing to apply. 0 - no smoothing, 100000 - maximum.
        /// </value>
        public float SmoothingFactor
        {
            get => smoothingFactor * 100000f;
            set => smoothingFactor = Mathf.Clamp(value / 100000f, 0, 1);
        }

        /// <summary>
        /// Gets or sets the position threshold.
        /// </summary>
        /// <value>
        /// Minimum distance between target position and smoothed position when to stop automatic movement.
        /// </value>
        public float PositionThreshold
        {
            get => Mathf.Sqrt(positionThreshold);
            set => positionThreshold = value * value;
        }

        /// <summary>
        /// Gets or sets the rotation threshold.
        /// </summary>
        /// <value>
        /// Minimum angle between target rotation and smoothed rotation when to stop automatic movement.
        /// </value>
        public float RotationThreshold
        {
            get => rotationThreshold;
            set => rotationThreshold = value;
        }

        /// <summary>
        /// Gets or sets the scale threshold.
        /// </summary>
        /// <value>
        /// Minimum difference between target scale and smoothed scale when to stop automatic movement.
        /// </value>
        public float ScaleThreshold
        {
            get => Mathf.Sqrt(scaleThreshold);
            set => scaleThreshold = value * value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this transform can be changed from another script.
        /// </summary>
        /// <value>
        /// <c>true</c> if this transform can be changed from another script; otherwise, <c>false</c>.
        /// </value>
        public bool AllowChangingFromOutside
        {
            get => allowChangingFromOutside;
            set => allowChangingFromOutside = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether Inertia is enabled. Inertia allows to add a force based on the velocity of the last Position and PreviousPosition of the Gesture.
        /// </summary>
        /// <value>
        ///   <c>true</c> if Inertia is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool EnableInertia
        {
            get => enableInertia;
            set => enableInertia = value;
        }

        /// <summary>
        /// Gets or sets the inertia factor.
        /// </summary>
        /// <value>
        /// The inertia factor. Indicates how much to modify the calculated inertia. 0 - no inertia, 100 - maximum.
        /// </value>
        public float InertiaFactor
        { 
            get => inertiaFactor;
            set
            {
                inertiaFactor = Math.Clamp(value, 0, 100);
            }
        }

        [SerializeField]
        [ToggleLeft]
        private bool enableSmoothing;

        [SerializeField]
        private float smoothingFactor = 1f / 100000f;

        [SerializeField]
        private float positionThreshold = 0.01f;

        [SerializeField]
        private float rotationThreshold = 0.1f;

        [SerializeField]
        private float scaleThreshold = 0.01f;

        [SerializeField]
        [ToggleLeft]
        private bool allowChangingFromOutside;

        [SerializeField]
        [ToggleLeft]
        private bool enableInertia;

        [SerializeField]
        private float inertiaFactor = 4f;

        private TransformerState state;

        private TransformGestureBase gesture;
        private Transform cachedTransform;

        private TransformGesture.TransformType transformMask;
        private Vector3 targetPosition, targetScale;
        private Quaternion targetRotation;

        // last* variables are needed to detect when Transform's properties were changed outside of this script
        private Vector3 lastPosition, lastScale;
        private Quaternion lastRotation;

        public Func<TransformGestureBase> OverrideGesture;
        public bool EnableOverrideTargetPosition;
        public Func<Vector3, Vector3> OverrideTargetPosition;
        public bool EnableOverrideTargetRotation;
        public Func<Quaternion, Quaternion> OverrideTargetRotation;
        public bool EnableOverrideTargetScale;
        public Func<Vector3, Vector3> OverrideTargetScale;
        /// <summary>
        /// Event that is invoked while the Transformer is lerping towards the targetPosition, it requires smoothing enabled
        /// </summary>
        public EventHandler OnSmoothingUpdate;
        /// <summary>
        /// Event fired at the end of the smoothing to manage repositions
        /// </summary>
        public UnityAction OnSmoothingEnded;
        /// <summary>
        /// Indicates if the Transformer is in its Inertia state; it requires smoothing enabled
        /// </summary>
        public bool IsInInertiaState { get; private set; } = false;
        /// <summary>
        /// Transformer state
        /// </summary>
        public TransformerState State { get => state; }
        /// <summary>
        /// Event that is invoked while the Transformer is in its Inertia state
        /// </summary>
        public EventHandler OnInertiaUpdate;

        private void Awake()
        {
            cachedTransform = transform;
        }

        private void OnEnable()
        {
            gesture = OverrideGesture != null ? OverrideGesture.Invoke() : GetComponent<TransformGestureBase>();
            gesture.StateChanged += stateChangedHandler;
            TouchManager.Instance.FrameFinished += frameFinishedHandler;

            stateIdle();
        }

        private void OnDisable()
        {
            if (gesture != null) gesture.StateChanged -= stateChangedHandler;
            if (TouchManager.Instance != null)
                TouchManager.Instance.FrameFinished -= frameFinishedHandler;

            stateIdle();
        }

        private void stateIdle()
        {
            var prevState = state;
            setState(TransformerState.Idle);

            IsInInertiaState = false;

            if (enableSmoothing && prevState == TransformerState.Automatic)
            {
                transform.position = lastPosition = targetPosition;
                var newLocalScale = lastScale = targetScale;
                // prevent recalculating colliders when no scale occurs
                if (newLocalScale != transform.localScale) transform.localScale = newLocalScale;
                transform.rotation = lastRotation = targetRotation;
                OnSmoothingEnded?.Invoke();
            }

            transformMask = TransformGesture.TransformType.None;
        }

        private void stateManual()
        {
            setState(TransformerState.Manual);

            targetPosition = lastPosition = cachedTransform.position;
            if (EnableOverrideTargetPosition && OverrideTargetPosition != null)
            {
                targetPosition = OverrideTargetPosition.Invoke(targetPosition);
            }
            targetRotation = lastRotation = cachedTransform.rotation;
            if (EnableOverrideTargetRotation && OverrideTargetRotation != null)
            {
                targetRotation = OverrideTargetRotation.Invoke(targetRotation);
            }
            targetScale = lastScale = cachedTransform.localScale;
            if (EnableOverrideTargetScale && OverrideTargetScale != null)
            {
                targetScale = OverrideTargetScale.Invoke(targetScale);
            }
            transformMask = TransformGesture.TransformType.None;
        }

        private void stateAutomatic()
        {
            setState(TransformerState.Automatic);

            if (!enableSmoothing || transformMask == TransformGesture.TransformType.None) stateIdle();
        }

        private void setState(TransformerState newState)
        {
            state = newState;
        }

        private void update()
        {
            if (state == TransformerState.Idle) return;

            if (!enableSmoothing) return;

            var fraction = 1 - Mathf.Pow(smoothingFactor, Time.unscaledDeltaTime);
            var changed = false;

            if ((transformMask & TransformGesture.TransformType.Scaling) != 0)
            {
                var scale = transform.localScale;
                if (allowChangingFromOutside)
                {
                    // Changed by someone else.
                    // Need to make sure to check per component here.
                    if (!Mathf.Approximately(scale.x, lastScale.x))
                        targetScale.x = scale.x;
                    if (!Mathf.Approximately(scale.y, lastScale.y))
                        targetScale.y = scale.y;
                    if (!Mathf.Approximately(scale.z, lastScale.z))
                        targetScale.z = scale.z;
                }
                
                var newLocalScale = Vector3.Lerp(scale, targetScale, fraction);
                if (newLocalScale.Equals(Vector3.positiveInfinity) || newLocalScale.Equals(Vector3.negativeInfinity))
                {
                    newLocalScale = lastScale;
                }
                // Prevent recalculating colliders when no scale occurs.
                if (newLocalScale != scale)
                {
                    transform.localScale = newLocalScale;
                    // Something might have adjusted our scale.
                    lastScale = transform.localScale;
                }

                if (state == TransformerState.Automatic && (targetScale - lastScale).sqrMagnitude > scaleThreshold)
                {
                    changed = true;
                }
            }

            if ((transformMask & TransformGesture.TransformType.Rotation) != 0)
            {
                if (allowChangingFromOutside)
                {
                    // Changed by someone else.
                    if (transform.rotation != lastRotation)
                    {
                        targetRotation = transform.rotation;
                    }
                }
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, fraction);
                // Something might have adjusted our rotation.
                lastRotation = transform.rotation;

                if (state == TransformerState.Automatic && !changed && Quaternion.Angle(targetRotation, lastRotation) > rotationThreshold)
                {
                    changed = true;
                }
            }

            if ((transformMask & TransformGesture.TransformType.Translation) != 0)
            {
                var pos = transform.position;
                /* With an anchor set different from center Mathf.Approximately reassigns the position with Unity v2020+
                if (allowChangingFromOutside)
                {
                    Changed by someone else.
                    Need to make sure to check per component here.
                    var temp = targetPosition;
                    if (!Mathf.Approximately(pos.x, lastPosition.x))
                        targetPosition.x = pos.x;
                    if (!Mathf.Approximately(pos.y, lastPosition.y))
                        targetPosition.y = pos.y;
                    if (!Mathf.Approximately(pos.z, lastPosition.z))
                        targetPosition.z = pos.z;
                    ConsoleLogger.Log($"Last position {lastPosition} pos x  {pos.x} state {state} target {targetPosition} fraction {fraction} approx {!Mathf.Approximately(pos.x, lastPosition.x)}");
                    if (Mathf.Approximately(targetPosition.x, pos.x))
                    {
                        ConsoleLogger.Log($"Position stays the same {pos.x} temp {targetPosition.x}");
                        targetPosition = temp;
                    }
                }
                */

                // if every pointer of the Gesture associated with this Transformer has been released
                // it means that we are still updating because we are in the Smoothing or Inertia state.
                // Only in this case we override the position / rotation / scale in the 'update' function
                if (gesture.NumPointers == 0)
                {
                    if (EnableOverrideTargetPosition && OverrideTargetPosition != null)
                    {
                        targetPosition = OverrideTargetPosition.Invoke(targetPosition);
                    }
                    if (EnableOverrideTargetRotation && OverrideTargetRotation != null)
                    {
                        targetRotation = OverrideTargetRotation.Invoke(targetRotation);
                    }
                    if (EnableOverrideTargetScale && OverrideTargetScale != null)
                    {
                        targetScale = OverrideTargetScale.Invoke(targetScale);
                    }
                }
                transform.position = Vector3.Lerp(pos, targetPosition, fraction);

                // Something might have adjusted our position (most likely Unity UI).
                lastPosition = transform.position;

                if (state == TransformerState.Automatic && !changed && (targetPosition - lastPosition).sqrMagnitude > positionThreshold)
                {
                    changed = true;
                }
            }

            OnSmoothingUpdate?.Invoke(this, null);  // if we are in the 'update' function the Smoothing is enabled for sure

            if (state == TransformerState.Automatic && !changed)
            {
                stateIdle();
            }
        }

        private void manualUpdate()
        {
            if (state != TransformerState.Manual) stateManual();

            var mask = gesture.TransformMask;
            if ((mask & TransformGesture.TransformType.Scaling) != 0)
            {
                targetScale *= gesture.DeltaScale;
                if (EnableOverrideTargetScale && OverrideTargetScale != null)
                {
                    targetScale = OverrideTargetScale.Invoke(targetScale);
                }
            }
            if ((mask & TransformGesture.TransformType.Rotation) != 0)
            {
                targetRotation = Quaternion.AngleAxis(gesture.DeltaRotation, gesture.RotationAxis) * targetRotation;
                if (EnableOverrideTargetRotation && OverrideTargetRotation != null)
                {
                    targetRotation = OverrideTargetRotation.Invoke(targetRotation);
                }
            }
            if ((mask & TransformGesture.TransformType.Translation) != 0)
            {
                targetPosition += gesture.DeltaPosition;
                if (EnableOverrideTargetPosition && OverrideTargetPosition != null)
                {
                    targetPosition = OverrideTargetPosition.Invoke(targetPosition);
                }
            }
            transformMask |= mask;

            gesture.OverrideTargetPosition(targetPosition);

            if (!enableSmoothing) applyValues();
        }

        private void applyValues()
        {
            if ((transformMask & TransformGesture.TransformType.Scaling) != 0) cachedTransform.localScale = targetScale;
            if ((transformMask & TransformGesture.TransformType.Rotation) != 0) cachedTransform.rotation = targetRotation;
            if ((transformMask & TransformGesture.TransformType.Translation) != 0) cachedTransform.position = targetPosition;
            transformMask = TransformGesture.TransformType.None;
        }

        private void stateChangedHandler(object sender, GestureStateChangeEventArgs gestureStateChangeEventArgs)
        {
            switch (gestureStateChangeEventArgs.State)
            {
                case Gesture.GestureState.Possible:
                    stateManual();
                    break;
                case Gesture.GestureState.Changed:
                    manualUpdate();
                    break;
                case Gesture.GestureState.Ended:
                    if (enableInertia && gesture.NumPointers == 0)  // making sure that inertia is triggered by the last pointer released from the gesture
                    {
                        Vector2 deltaPosition = targetPosition - lastPosition;
                        Vector3 velocity = (deltaPosition / (Time.deltaTime * 1000f)) * InertiaFactor;
                        velocity = TransformDirection(velocity);
                        var newPos = velocity + targetPosition;
                        targetPosition = newPos;
                        IsInInertiaState = true;
                    }
                    stateAutomatic();
                    break;
                case Gesture.GestureState.Cancelled:
                    stateAutomatic();
                    break;
                case Gesture.GestureState.Failed:
                case Gesture.GestureState.Idle:
                    if (gestureStateChangeEventArgs.PreviousState == Gesture.GestureState.Possible) stateAutomatic();
                    break;
            }
        }

        private void frameFinishedHandler(object sender, EventArgs eventArgs)
        {
            update();
        }

        /// <summary>
        /// Transforms <paramref name="vector"/> to convert it between different resolutions and dpi, taking FHD 96dpi as base reference
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        private Vector3 TransformDirection(Vector3 vector)
        {
            var referenceScreenSize = new Vector2(1920, 1080);
            float referenceDPI = 96;

            var screenDimensions = new Vector2(Screen.width, Screen.height);
            var dpi = Screen.dpi;

            var widthScale = screenDimensions.x / referenceScreenSize.x;
            var heightScale = screenDimensions.y / referenceScreenSize.y;
            var dpiScale = dpi / referenceDPI;

            var scaledVector = new Vector2(vector.x * widthScale * dpiScale, vector.y * heightScale * dpiScale);
            return scaledVector;
        }

        /// <summary>
        /// Resets transform values at their current cached one in <c>cachedTransform</c>
        /// </summary>
        public void ResetState()
        {
            if (cachedTransform != null)
            {
                targetPosition = lastPosition = cachedTransform.position;
                targetScale = lastScale = cachedTransform.localScale;
                targetRotation = lastRotation = cachedTransform.rotation;
            }
        }

        /// <summary>
        /// Forces idle state
        /// </summary>
        public void SetIdleState() => stateIdle();

        public void SetTransformGesture(TransformGestureBase gesture)
        {
            if (gesture != null && this.gesture != gesture)
            {
                // old
                if (this.gesture != null)
                {
                    this.gesture.StateChanged -= stateChangedHandler;
                }

                ConsoleLogger.Log($"[{GetInstanceID()}] SetTransformGesture, from {this.gesture?.GetInstanceID()} to {gesture.GetInstanceID()}");

                // new
                this.gesture = gesture;
                this.gesture.StateChanged += stateChangedHandler;

                stateIdle();

                ResetState();
            }
        }
    }
}