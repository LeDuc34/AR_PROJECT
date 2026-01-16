using System;
using UnityEngine;
using GeoscaleCadastre.Models;
using GeoscaleCadastre.Map;
using Microsoft.MixedReality.Toolkit;

namespace GeoscaleCadastre.Parcel
{
    /// <summary>
    /// Gère la sélection de parcelles sur la carte
    /// Intègre avec MRTK pour les interactions main/regard sur HoloLens 2
    /// </summary>
    public class ParcelSelectionHandler : MonoBehaviour
    {
        [Header("Références")]
        [SerializeField]
        private MapManager _mapManager;

        [SerializeField]
        private ParcelDataService _parcelService;

        [SerializeField]
        private ParcelHighlighter _highlighter;

        [SerializeField]
        [Tooltip("Collider de la carte pour le raycast")]
        private Collider _mapCollider;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Layer de la carte pour le raycast")]
        private LayerMask _mapLayer;

        [SerializeField]
        [Tooltip("Distance maximale du raycast")]
        private float _maxRaycastDistance = 10f;

        // État
        private ParcelModel _selectedParcel;
        private bool _isSelecting;

        // Events
        public event Action<ParcelModel> OnParcelSelected;
        public event Action OnSelectionCleared;

        /// <summary>Parcelle actuellement sélectionnée</summary>
        public ParcelModel SelectedParcel { get { return _selectedParcel; } }

        /// <summary>Indique si une sélection est en cours</summary>
        public bool IsSelecting { get { return _isSelecting; } }

        private void OnEnable()
        {
            // S'abonner aux événements du service parcelle
            if (_parcelService != null)
            {
                _parcelService.OnParcelLoaded += HandleParcelLoaded;
                _parcelService.OnError += HandleParcelError;
            }
        }

        private void OnDisable()
        {
            if (_parcelService != null)
            {
                _parcelService.OnParcelLoaded -= HandleParcelLoaded;
                _parcelService.OnError -= HandleParcelError;
            }
        }

        /// <summary>
        /// Sélectionne une parcelle à partir d'une position dans le monde
        /// Appelé par MRTK lors d'un tap/pinch sur la carte
        /// </summary>
        /// <param name="worldPosition">Position dans le monde Unity</param>
        public void SelectParcelAtWorldPosition(Vector3 worldPosition)
        {
            if (_isSelecting)
            {
                Debug.LogWarning("[ParcelSelectionHandler] Sélection déjà en cours");
                return;
            }

            // Convertir la position monde en coordonnées GPS
            double lat, lng;
            if (TryConvertWorldToGps(worldPosition, out lat, out lng))
            {
                SelectParcelAtCoordinates(lat, lng);
            }
            else
            {
                Debug.LogWarning("[ParcelSelectionHandler] Impossible de convertir les coordonnées");
            }
        }

        /// <summary>
        /// Sélectionne une parcelle à partir de coordonnées GPS
        /// </summary>
        /// <param name="latitude">Latitude</param>
        /// <param name="longitude">Longitude</param>
        public void SelectParcelAtCoordinates(double latitude, double longitude)
        {
            if (_parcelService == null)
            {
                Debug.LogError("[ParcelSelectionHandler] ParcelDataService non assigné");
                return;
            }

            _isSelecting = true;
            Debug.Log(string.Format("[ParcelSelectionHandler] Sélection parcelle à ({0}, {1})", latitude, longitude));

            _parcelService.FetchParcelAtCoordinates(latitude, longitude);
        }

        /// <summary>
        /// Traite un raycast depuis un pointeur MRTK
        /// </summary>
        /// <param name="rayOrigin">Origine du rayon</param>
        /// <param name="rayDirection">Direction du rayon</param>
        public void ProcessPointerRaycast(Vector3 rayOrigin, Vector3 rayDirection)
        {
            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, _maxRaycastDistance, _mapLayer))
            {
                SelectParcelAtWorldPosition(hit.point);
            }
        }

        /// <summary>
        /// Efface la sélection actuelle
        /// </summary>
        public void ClearSelection()
        {
            _selectedParcel = null;

            if (_highlighter != null)
            {
                _highlighter.ClearHighlight();
            }

            if (OnSelectionCleared != null)
                OnSelectionCleared();

            Debug.Log("[ParcelSelectionHandler] Sélection effacée");
        }

        private void HandleParcelLoaded(ParcelModel parcel)
        {
            _isSelecting = false;
            _selectedParcel = parcel;

            // Surligner la parcelle
            if (_highlighter != null)
            {
                _highlighter.HighlightParcel(parcel);
            }

            // Centrer la carte sur la parcelle (pattern Geoscale)
            if (_mapManager != null)
            {
                _mapManager.CenterOnParcel(parcel, 0.8f);
            }

            // Notifier les listeners
            if (OnParcelSelected != null)
                OnParcelSelected(parcel);

            Debug.Log(string.Format("[ParcelSelectionHandler] Parcelle sélectionnée: {0}", parcel));
        }

        private void HandleParcelError(string error)
        {
            _isSelecting = false;
            Debug.LogWarning(string.Format("[ParcelSelectionHandler] Erreur: {0}", error));
        }

        /// <summary>
        /// Convertit une position monde Unity en coordonnées GPS
        /// Note: Cette conversion dépend de la configuration Mapbox
        /// </summary>
        private bool TryConvertWorldToGps(Vector3 worldPos, out double latitude, out double longitude)
        {
            latitude = 0;
            longitude = 0;

            // Si nous avons un MapManager, utiliser sa position actuelle comme référence
            if (_mapManager != null)
            {
                // Conversion basique: position relative au centre de la carte
                // À ajuster selon l'échelle réelle de la carte Mapbox

                // Facteurs de conversion approximatifs (à calibrer)
                // Dépend de l'échelle de la carte dans Unity
                const float metersPerUnit = 1f; // À ajuster
                const float latDegreesPerMeter = 1f / 111000f;
                const float lngDegreesPerMeter = 1f / 75000f; // À ~46° latitude

                // Position relative au centre de la carte
                Vector3 relativePos = worldPos - transform.position;

                latitude = _mapManager.CurrentLatitude + (relativePos.z * metersPerUnit * latDegreesPerMeter);
                longitude = _mapManager.CurrentLongitude + (relativePos.x * metersPerUnit * lngDegreesPerMeter);

                return true;
            }

            return false;
        }

        #region MRTK Integration

        /// <summary>
        /// Appelé par MRTK IMixedRealityPointerHandler.OnPointerClicked
        /// </summary>
        public void OnMRTKPointerClicked(Vector3 hitPoint)
        {
            SelectParcelAtWorldPosition(hitPoint);
        }

        /// <summary>
        /// Appelé par MRTK pour les interactions vocales
        /// "Sélectionner ici" quand l'utilisateur regarde un point
        /// Utilise le GazeProvider de MRTK pour obtenir le point de regard
        /// </summary>
        public void OnVoiceSelectAtGaze()
        {
            // Obtenir le point de regard actuel via MRTK GazeProvider
            var gazeProvider = CoreServices.InputSystem?.GazeProvider;
            if (gazeProvider != null && gazeProvider.GazeTarget != null)
            {
                Vector3 hitPosition = gazeProvider.HitPosition;
                Debug.Log(string.Format("[ParcelSelectionHandler] Sélection vocale au regard: {0}", hitPosition));
                SelectParcelAtWorldPosition(hitPosition);
            }
            else
            {
                Debug.LogWarning("[ParcelSelectionHandler] Impossible d'obtenir le point de regard MRTK");
            }
        }

        /// <summary>
        /// Version avec paramètre pour compatibilité (deprecated)
        /// </summary>
        [Obsolete("Utiliser OnVoiceSelectAtGaze() sans paramètre")]
        public void OnVoiceSelectAtGaze(Vector3 gazePoint)
        {
            SelectParcelAtWorldPosition(gazePoint);
        }

        #endregion
    }
}
