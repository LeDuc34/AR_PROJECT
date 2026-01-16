using UnityEngine;
using GeoscaleCadastre.Parcel;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;

namespace GeoscaleCadastre.Map
{
    /// <summary>
    /// Gère les interactions MRTK avec la carte
    /// Implémente les handlers de pointeur pour HoloLens 2
    /// Compatible avec les interactions main, regard et voix
    /// </summary>
    public class MapInteractionHandler : MonoBehaviour,
        IMixedRealityPointerHandler,
        IMixedRealityFocusHandler
    {
        [Header("Références")]
        [SerializeField]
        private ParcelSelectionHandler _parcelSelectionHandler;

        [SerializeField]
        private MapManager _mapManager;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Active les interactions de sélection")]
        private bool _enableSelection = true;

        [SerializeField]
        [Tooltip("Active le zoom par geste")]
        private bool _enableZoom = true;

        [SerializeField]
        [Tooltip("Active le pan par geste")]
        private bool _enablePan = true;

        [SerializeField]
        [Tooltip("Sensibilité du zoom")]
        private float _zoomSensitivity = 1f;

        [SerializeField]
        [Tooltip("Sensibilité du pan")]
        private float _panSensitivity = 0.001f;

        // État
        private bool _isManipulating;
        private bool _isFocused;
        private Vector3 _lastPointerPosition;

        /// <summary>Indique si la carte a le focus</summary>
        public bool IsFocused { get { return _isFocused; } }

        #region IMixedRealityPointerHandler Implementation

        void IMixedRealityPointerHandler.OnPointerDown(MixedRealityPointerEventData eventData)
        {
            if (eventData.Pointer.Result.CurrentPointerTarget == gameObject)
            {
                _lastPointerPosition = eventData.Pointer.Result.Details.Point;
                _isManipulating = true;
            }
        }

        void IMixedRealityPointerHandler.OnPointerUp(MixedRealityPointerEventData eventData)
        {
            _isManipulating = false;
        }

        void IMixedRealityPointerHandler.OnPointerClicked(MixedRealityPointerEventData eventData)
        {
            if (!_enableSelection) return;

            Vector3 hitPoint = eventData.Pointer.Result.Details.Point;

            if (_parcelSelectionHandler != null)
            {
                _parcelSelectionHandler.OnMRTKPointerClicked(hitPoint);
            }

            // Marquer l'événement comme utilisé
            eventData.Use();
        }

        void IMixedRealityPointerHandler.OnPointerDragged(MixedRealityPointerEventData eventData)
        {
            if (!_enablePan || !_isManipulating || _mapManager == null) return;

            Vector3 position = eventData.Pointer.Result.Details.Point;
            Vector3 delta = position - _lastPointerPosition;
            _lastPointerPosition = position;

            // Convertir le delta en mouvement de carte
            double latDelta = delta.z * _panSensitivity;
            double lngDelta = delta.x * _panSensitivity;

            // Déplacer la carte
            _mapManager.SetPosition(
                _mapManager.CurrentLatitude - latDelta,
                _mapManager.CurrentLongitude - lngDelta,
                _mapManager.CurrentZoom
            );
        }

        #endregion

        #region IMixedRealityFocusHandler Implementation

        void IMixedRealityFocusHandler.OnFocusEnter(FocusEventData eventData)
        {
            _isFocused = true;
            Debug.Log("[MapInteractionHandler] Focus Enter");
        }

        void IMixedRealityFocusHandler.OnFocusExit(FocusEventData eventData)
        {
            _isFocused = false;
            _isManipulating = false;
            Debug.Log("[MapInteractionHandler] Focus Exit");
        }

        #endregion

        #region MRTK Manipulation Handlers

        /// <summary>
        /// Appelé par MRTK lors d'un pinch zoom
        /// </summary>
        public void OnPinchZoom(float zoomDelta)
        {
            if (!_enableZoom || _mapManager == null) return;

            float newZoom = _mapManager.CurrentZoom + (zoomDelta * _zoomSensitivity);
            newZoom = Mathf.Clamp(newZoom, 10f, 20f);

            _mapManager.SetPosition(
                _mapManager.CurrentLatitude,
                _mapManager.CurrentLongitude,
                newZoom
            );
        }

        #endregion

        #region Voice Commands

        /// <summary>
        /// Commande vocale: "Zoom avant"
        /// </summary>
        public void OnVoiceZoomIn()
        {
            if (_mapManager == null) return;

            float newZoom = Mathf.Min(_mapManager.CurrentZoom + 1f, 20f);
            _mapManager.FlyTo(
                _mapManager.CurrentLatitude,
                _mapManager.CurrentLongitude,
                newZoom,
                0.5f
            );
        }

        /// <summary>
        /// Commande vocale: "Zoom arrière"
        /// </summary>
        public void OnVoiceZoomOut()
        {
            if (_mapManager == null) return;

            float newZoom = Mathf.Max(_mapManager.CurrentZoom - 1f, 10f);
            _mapManager.FlyTo(
                _mapManager.CurrentLatitude,
                _mapManager.CurrentLongitude,
                newZoom,
                0.5f
            );
        }

        /// <summary>
        /// Commande vocale: "Sélectionner ici"
        /// Sélectionne la parcelle au point de regard actuel
        /// </summary>
        public void OnVoiceSelectHere()
        {
            // Obtenir le point de regard actuel via MRTK
            var gazeProvider = CoreServices.InputSystem?.GazeProvider;
            if (gazeProvider != null && gazeProvider.GazeTarget != null)
            {
                Vector3 hitPosition = gazeProvider.HitPosition;
                Debug.Log(string.Format("[MapInteractionHandler] Sélection vocale à: {0}", hitPosition));

                if (_parcelSelectionHandler != null)
                {
                    _parcelSelectionHandler.OnMRTKPointerClicked(hitPosition);
                }
            }
            else
            {
                Debug.LogWarning("[MapInteractionHandler] Impossible d'obtenir le point de regard");
            }
        }

        /// <summary>
        /// Commande vocale: "Effacer sélection"
        /// </summary>
        public void OnVoiceClearSelection()
        {
            if (_parcelSelectionHandler != null)
            {
                _parcelSelectionHandler.ClearSelection();
            }
        }

        /// <summary>
        /// Commande vocale: "Retour à Paris" (ou autre ville)
        /// </summary>
        public void OnVoiceGoToDefault()
        {
            if (_mapManager != null)
            {
                _mapManager.FlyTo(48.8566, 2.3522, 15f, 2f);
            }
        }

        #endregion
    }
}
