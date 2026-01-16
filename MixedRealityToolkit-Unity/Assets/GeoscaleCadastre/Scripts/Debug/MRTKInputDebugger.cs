using UnityEngine;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;

namespace GeoscaleCadastre.Debugging
{
    /// <summary>
    /// Script de debug pour tracer TOUS les événements MRTK globalement
    /// Ajoute ce script sur un GameObject vide dans la scène
    /// </summary>
    public class MRTKInputDebugger : MonoBehaviour,
        IMixedRealityPointerHandler,
        IMixedRealityInputHandler,
        IMixedRealityInputHandler<float>
    {
        [Header("Configuration")]
        [SerializeField]
        private bool _logPointerEvents = true;

        [SerializeField]
        private bool _logInputEvents = true;

        private void OnEnable()
        {
            // S'enregistrer comme handler global pour TOUS les événements
            CoreServices.InputSystem?.RegisterHandler<IMixedRealityPointerHandler>(this);
            CoreServices.InputSystem?.RegisterHandler<IMixedRealityInputHandler>(this);
            CoreServices.InputSystem?.RegisterHandler<IMixedRealityInputHandler<float>>(this);

            Debug.Log("[MRTKInputDebugger] === ENREGISTRÉ COMME HANDLER GLOBAL ===");
        }

        private void OnDisable()
        {
            CoreServices.InputSystem?.UnregisterHandler<IMixedRealityPointerHandler>(this);
            CoreServices.InputSystem?.UnregisterHandler<IMixedRealityInputHandler>(this);
            CoreServices.InputSystem?.UnregisterHandler<IMixedRealityInputHandler<float>>(this);

            Debug.Log("[MRTKInputDebugger] === DÉSENREGISTRÉ ===");
        }

        #region IMixedRealityPointerHandler

        public void OnPointerDown(MixedRealityPointerEventData eventData)
        {
            if (!_logPointerEvents) return;

            string target = eventData.Pointer.Result?.CurrentPointerTarget != null
                ? eventData.Pointer.Result.CurrentPointerTarget.name
                : "NULL";

            Debug.Log(string.Format("[MRTKInputDebugger] *** GLOBAL POINTER DOWN *** Pointer: {0}, Target: {1}, Position: {2}",
                eventData.Pointer.PointerName,
                target,
                eventData.Pointer.Result?.Details.Point));
        }

        public void OnPointerUp(MixedRealityPointerEventData eventData)
        {
            if (!_logPointerEvents) return;

            Debug.Log(string.Format("[MRTKInputDebugger] *** GLOBAL POINTER UP *** Pointer: {0}",
                eventData.Pointer.PointerName));
        }

        public void OnPointerClicked(MixedRealityPointerEventData eventData)
        {
            if (!_logPointerEvents) return;

            string target = eventData.Pointer.Result?.CurrentPointerTarget != null
                ? eventData.Pointer.Result.CurrentPointerTarget.name
                : "NULL";

            Debug.Log(string.Format("[MRTKInputDebugger] *** GLOBAL POINTER CLICKED *** Pointer: {0}, Target: {1}, Position: {2}",
                eventData.Pointer.PointerName,
                target,
                eventData.Pointer.Result?.Details.Point));
        }

        public void OnPointerDragged(MixedRealityPointerEventData eventData)
        {
            // Pas de log pour éviter le spam
        }

        #endregion

        #region IMixedRealityInputHandler

        public void OnInputDown(InputEventData eventData)
        {
            if (!_logInputEvents) return;

            Debug.Log(string.Format("[MRTKInputDebugger] *** GLOBAL INPUT DOWN *** Action: {0}, Source: {1}, Handedness: {2}",
                eventData.MixedRealityInputAction.Description,
                eventData.InputSource.SourceName,
                eventData.Handedness));
        }

        public void OnInputUp(InputEventData eventData)
        {
            if (!_logInputEvents) return;

            Debug.Log(string.Format("[MRTKInputDebugger] *** GLOBAL INPUT UP *** Action: {0}, Source: {1}",
                eventData.MixedRealityInputAction.Description,
                eventData.InputSource.SourceName));
        }

        #endregion

        #region IMixedRealityInputHandler<float>

        public void OnInputChanged(InputEventData<float> eventData)
        {
            // Trigger/grip values - log only significant changes
            if (!_logInputEvents) return;

            if (eventData.InputData > 0.5f)
            {
                Debug.Log(string.Format("[MRTKInputDebugger] *** INPUT CHANGED *** Action: {0}, Value: {1:F2}",
                    eventData.MixedRealityInputAction.Description,
                    eventData.InputData));
            }
        }

        #endregion

        private void Start()
        {
            Debug.Log("=== [MRTKInputDebugger] DÉMARRÉ ===");
            Debug.Log("[MRTKInputDebugger] Ce script trace TOUS les événements MRTK globalement");
            Debug.Log("[MRTKInputDebugger] Si tu vois GLOBAL POINTER DOWN mais pas de log dans MapInteractionHandler,");
            Debug.Log("[MRTKInputDebugger] alors le problème est dans le routage vers l'objet spécifique.");
        }
    }
}
