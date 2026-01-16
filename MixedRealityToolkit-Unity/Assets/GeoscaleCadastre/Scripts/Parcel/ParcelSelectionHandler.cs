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

        private void Start()
        {
            // === DEBUG: Vérification des références au démarrage ===
            Debug.Log("=== [ParcelSelectionHandler] DEBUG START ===");
            Debug.Log(string.Format("[ParcelSelectionHandler] _mapManager: {0}", _mapManager != null ? "OK" : "NULL"));
            Debug.Log(string.Format("[ParcelSelectionHandler] _parcelService: {0}", _parcelService != null ? "OK" : "NULL"));
            Debug.Log(string.Format("[ParcelSelectionHandler] _highlighter: {0}", _highlighter != null ? "OK" : "NULL"));
            Debug.Log(string.Format("[ParcelSelectionHandler] _mapCollider: {0}", _mapCollider != null ? "OK" : "NULL"));
            Debug.Log("=== [ParcelSelectionHandler] DEBUG END ===");
        }

        private void OnEnable()
        {
            // S'abonner aux événements du service parcelle
            if (_parcelService != null)
            {
                _parcelService.OnParcelLoaded += HandleParcelLoaded;
                _parcelService.OnError += HandleParcelError;
                Debug.Log("[ParcelSelectionHandler] Abonné aux événements ParcelService");
            }
            else
            {
                Debug.LogWarning("[ParcelSelectionHandler] OnEnable: _parcelService est NULL");
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
            Debug.Log("=== [ParcelSelectionHandler] DÉBUT SelectParcelAtWorldPosition ===");
            Debug.Log(string.Format("[ParcelSelectionHandler] >>> SelectParcelAtWorldPosition - worldPosition: {0}", worldPosition));

            if (_isSelecting)
            {
                Debug.LogWarning("[ParcelSelectionHandler] Sélection déjà en cours, ignorée");
                return;
            }

            // Convertir la position monde en coordonnées GPS
            double lat, lng;
            Debug.Log("[ParcelSelectionHandler] Tentative de conversion World -> GPS...");
            Debug.Log(string.Format("[ParcelSelectionHandler] _mapManager: {0}", _mapManager != null ? "OK" : "NULL"));

            if (TryConvertWorldToGps(worldPosition, out lat, out lng))
            {
                Debug.Log(string.Format("[ParcelSelectionHandler] ✓ Conversion RÉUSSIE: ({0:F6}, {1:F6})", lat, lng));
                Debug.Log("[ParcelSelectionHandler] Appel de SelectParcelAtCoordinates...");
                SelectParcelAtCoordinates(lat, lng);
            }
            else
            {
                Debug.LogError("[ParcelSelectionHandler] ✗ ÉCHEC de conversion World -> GPS");
                Debug.LogError("[ParcelSelectionHandler] La sélection de parcelle est IMPOSSIBLE sans coordonnées GPS valides");
            }

            Debug.Log("=== [ParcelSelectionHandler] FIN SelectParcelAtWorldPosition ===");
        }

        /// <summary>
        /// Sélectionne une parcelle à partir de coordonnées GPS
        /// </summary>
        /// <param name="latitude">Latitude</param>
        /// <param name="longitude">Longitude</param>
        public void SelectParcelAtCoordinates(double latitude, double longitude)
        {
            Debug.Log("=== [ParcelSelectionHandler.SelectParcelAtCoordinates] DÉBUT ===");
            Debug.Log(string.Format("[ParcelSelectionHandler.SelectParcelAtCoordinates] Coordonnées GPS: ({0:F6}, {1:F6})", latitude, longitude));

            if (_parcelService == null)
            {
                Debug.LogError("[ParcelSelectionHandler.SelectParcelAtCoordinates] ERREUR: ParcelDataService non assigné!");
                return;
            }

            Debug.Log("[ParcelSelectionHandler.SelectParcelAtCoordinates] ParcelDataService OK");

            _isSelecting = true;
            Debug.Log(string.Format("[ParcelSelectionHandler.SelectParcelAtCoordinates] État _isSelecting = true"));
            Debug.Log(string.Format("[ParcelSelectionHandler.SelectParcelAtCoordinates] Appel de _parcelService.FetchParcelAtCoordinates({0:F6}, {1:F6})...", latitude, longitude));

            _parcelService.FetchParcelAtCoordinates(latitude, longitude);

            Debug.Log("=== [ParcelSelectionHandler.SelectParcelAtCoordinates] FIN ===");
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

            Debug.Log("=== [ParcelSelectionHandler] PARCELLE CHARGÉE - DÉBUT TRAITEMENT ===");
            Debug.Log(string.Format("[ParcelSelectionHandler] Parcelle sélectionnée: {0}", parcel));
            Debug.Log(string.Format("[ParcelSelectionHandler] Géométrie: {0} points", parcel.Geometry != null ? parcel.Geometry.Length : 0));
            Debug.Log(string.Format("[ParcelSelectionHandler] Centroid: {0}", parcel.Centroid));
            Debug.Log(string.Format("[ParcelSelectionHandler] Surface: {0} m²", parcel.Surface));

            // Surligner la parcelle
            if (_highlighter != null)
            {
                Debug.Log("[ParcelSelectionHandler] Appel de _highlighter.HighlightParcel()...");
                _highlighter.HighlightParcel(parcel);
                Debug.Log("[ParcelSelectionHandler] _highlighter.HighlightParcel() terminé");
            }
            else
            {
                Debug.LogError("[ParcelSelectionHandler] _highlighter est NULL - IMPOSSIBLE DE COLORIER LA PARCELLE !");
            }

            // Centrer la carte sur la parcelle (pattern Geoscale)
            if (_mapManager != null)
            {
                Debug.Log("[ParcelSelectionHandler] Centrage de la carte sur la parcelle...");
                _mapManager.CenterOnParcel(parcel, 0.8f);
            }
            else
            {
                Debug.LogWarning("[ParcelSelectionHandler] _mapManager est NULL - pas de centrage");
            }

            // Notifier les listeners
            if (OnParcelSelected != null)
            {
                Debug.Log(string.Format("[ParcelSelectionHandler] Notification de {0} listeners...", OnParcelSelected.GetInvocationList().Length));
                OnParcelSelected(parcel);
            }

            Debug.Log("=== [ParcelSelectionHandler] PARCELLE CHARGÉE - FIN TRAITEMENT ===");
        }

        private void HandleParcelError(string error)
        {
            _isSelecting = false;
            Debug.LogWarning(string.Format("[ParcelSelectionHandler] Erreur: {0}", error));
        }

        /// <summary>
        /// Convertit une position monde Unity en coordonnées GPS
        /// Délègue à MapManager qui utilise la méthode WorldToGeoPosition du SDK Mapbox
        /// </summary>
        private bool TryConvertWorldToGps(Vector3 worldPos, out double latitude, out double longitude)
        {
            Debug.Log("[ParcelSelectionHandler.TryConvertWorldToGps] >>> Entrée dans la méthode");
            Debug.Log(string.Format("[ParcelSelectionHandler.TryConvertWorldToGps] worldPos: {0}", worldPos));

            latitude = 0;
            longitude = 0;

            if (_mapManager == null)
            {
                Debug.LogError("[ParcelSelectionHandler.TryConvertWorldToGps] ERREUR: MapManager non assigné!");
                return false;
            }

            Debug.Log("[ParcelSelectionHandler.TryConvertWorldToGps] MapManager OK, appel de TryWorldToGeoPosition...");
            bool result = _mapManager.TryWorldToGeoPosition(worldPos, out latitude, out longitude);

            Debug.Log(string.Format("[ParcelSelectionHandler.TryConvertWorldToGps] Résultat: {0}, lat: {1:F6}, lng: {2:F6}",
                result, latitude, longitude));

            return result;
        }

        #region MRTK Integration

        /// <summary>
        /// Appelé par MRTK IMixedRealityPointerHandler.OnPointerClicked
        /// </summary>
        public void OnMRTKPointerClicked(Vector3 hitPoint)
        {
            Debug.Log(string.Format("[ParcelSelectionHandler] >>> OnMRTKPointerClicked - hitPoint: {0}", hitPoint));
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
