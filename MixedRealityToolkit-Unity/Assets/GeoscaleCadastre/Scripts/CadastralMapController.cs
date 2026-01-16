using UnityEngine;
using GeoscaleCadastre.Map;
using GeoscaleCadastre.Search;
using GeoscaleCadastre.Parcel;
using GeoscaleCadastre.Models;
using GeoscaleCadastre.UI;

namespace GeoscaleCadastre
{
    /// <summary>
    /// Contrôleur principal de la carte cadastrale
    /// Orchestre tous les composants et gère le cycle de vie
    /// Point d'entrée principal pour l'application AR cadastrale
    /// </summary>
    public class CadastralMapController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Token d'accès Mapbox")]
        private string _mapboxAccessToken;

        [SerializeField]
        [Tooltip("Position initiale - Latitude")]
        private double _initialLatitude = 48.8566;

        [SerializeField]
        [Tooltip("Position initiale - Longitude")]
        private double _initialLongitude = 2.3522;

        [SerializeField]
        [Tooltip("Zoom initial")]
        private float _initialZoom = 15f;

        [Header("Composants - Carte")]
        [SerializeField]
        private MapManager _mapManager;

        [SerializeField]
        private MapInteractionHandler _mapInteractionHandler;

        [SerializeField]
        private ParcelHighlighter _parcelHighlighter;

        [Header("Composants - Recherche")]
        [SerializeField]
        private AddressSearchService _addressSearchService;

        [Header("Composants - Parcelle")]
        [SerializeField]
        private ParcelDataService _parcelDataService;

        [SerializeField]
        private ParcelSelectionHandler _parcelSelectionHandler;

        [Header("Composants - UI")]
        [SerializeField]
        private SearchBarUI _searchBarUI;

        [SerializeField]
        private SearchResultsPanel _searchResultsPanel;

        [SerializeField]
        private ParcelInfoPanel _parcelInfoPanel;

        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogs = true;

        // État
        private bool _isInitialized;

        /// <summary>Indique si le système est initialisé</summary>
        public bool IsInitialized { get { return _isInitialized; } }

        /// <summary>Parcelle actuellement sélectionnée</summary>
        public ParcelModel SelectedParcel
        {
            get { return _parcelSelectionHandler != null ? _parcelSelectionHandler.SelectedParcel : null; }
        }

        private void Awake()
        {
            ValidateComponents();
        }

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Initialise le système cadastral
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                LogDebug("Déjà initialisé");
                return;
            }

            LogDebug("Initialisation du système cadastral...");

            // Configurer le token Mapbox
            if (_addressSearchService != null && !string.IsNullOrEmpty(_mapboxAccessToken))
            {
                _addressSearchService.SetMapboxToken(_mapboxAccessToken);
            }

            // Initialiser la carte
            if (_mapManager != null)
            {
                _mapManager.Initialize(_initialLatitude, _initialLongitude, _initialZoom);
            }

            // S'abonner aux événements
            SubscribeToEvents();

            _isInitialized = true;
            LogDebug("Système cadastral initialisé");
        }

        /// <summary>
        /// Recherche une adresse
        /// </summary>
        /// <param name="query">Texte de recherche</param>
        public void SearchAddress(string query)
        {
            if (_addressSearchService != null)
            {
                _addressSearchService.Search(query);
            }
        }

        /// <summary>
        /// Sélectionne une parcelle aux coordonnées spécifiées
        /// </summary>
        public void SelectParcelAt(double latitude, double longitude)
        {
            if (_parcelSelectionHandler != null)
            {
                _parcelSelectionHandler.SelectParcelAtCoordinates(latitude, longitude);
            }
        }

        /// <summary>
        /// Centre la carte sur une position
        /// </summary>
        public void CenterMapAt(double latitude, double longitude, float zoom = -1)
        {
            if (_mapManager != null)
            {
                if (zoom < 0) zoom = _mapManager.CurrentZoom;
                _mapManager.FlyTo(latitude, longitude, zoom);
            }
        }

        /// <summary>
        /// Efface la sélection actuelle
        /// </summary>
        public void ClearSelection()
        {
            if (_parcelSelectionHandler != null)
            {
                _parcelSelectionHandler.ClearSelection();
            }
        }

        private void ValidateComponents()
        {
            // Vérifier les composants essentiels
            if (_mapManager == null)
                LogWarning("MapManager non assigné");

            if (_addressSearchService == null)
                LogWarning("AddressSearchService non assigné");

            if (_parcelDataService == null)
                LogWarning("ParcelDataService non assigné");

            if (_parcelSelectionHandler == null)
                LogWarning("ParcelSelectionHandler non assigné");

            if (string.IsNullOrEmpty(_mapboxAccessToken))
                LogWarning("Token Mapbox non configuré - la recherche utilisera uniquement Nominatim");
        }

        private void SubscribeToEvents()
        {
            // Événements de la carte
            if (_mapManager != null)
            {
                _mapManager.OnMapInitialized += OnMapInitialized;
                _mapManager.OnMapMoved += OnMapMoved;
            }

            // Événements de recherche
            if (_addressSearchService != null)
            {
                _addressSearchService.OnSearchResults += OnSearchResults;
                _addressSearchService.OnSearchError += OnSearchError;
            }

            // Événements de sélection de parcelle
            if (_parcelSelectionHandler != null)
            {
                _parcelSelectionHandler.OnParcelSelected += OnParcelSelected;
                _parcelSelectionHandler.OnSelectionCleared += OnSelectionCleared;
            }

            // Événements UI
            if (_searchResultsPanel != null)
            {
                _searchResultsPanel.OnAddressSelected += OnAddressSelected;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_mapManager != null)
            {
                _mapManager.OnMapInitialized -= OnMapInitialized;
                _mapManager.OnMapMoved -= OnMapMoved;
            }

            if (_addressSearchService != null)
            {
                _addressSearchService.OnSearchResults -= OnSearchResults;
                _addressSearchService.OnSearchError -= OnSearchError;
            }

            if (_parcelSelectionHandler != null)
            {
                _parcelSelectionHandler.OnParcelSelected -= OnParcelSelected;
                _parcelSelectionHandler.OnSelectionCleared -= OnSelectionCleared;
            }

            if (_searchResultsPanel != null)
            {
                _searchResultsPanel.OnAddressSelected -= OnAddressSelected;
            }
        }

        #region Event Handlers

        private void OnMapInitialized()
        {
            LogDebug("Carte initialisée");
        }

        private void OnMapMoved(double lat, double lng, float zoom)
        {
            LogDebug(string.Format("Carte déplacée: ({0:F6}, {1:F6}) zoom {2:F1}", lat, lng, zoom));
        }

        private void OnSearchResults(System.Collections.Generic.List<AddressResult> results)
        {
            LogDebug(string.Format("Recherche: {0} résultats", results.Count));
        }

        private void OnSearchError(string error)
        {
            LogWarning(string.Format("Erreur recherche: {0}", error));
        }

        private void OnAddressSelected(AddressResult address)
        {
            LogDebug(string.Format("Adresse sélectionnée: {0}", address.Text));

            // Optionnel: Sélectionner automatiquement la parcelle à cette adresse
            // SelectParcelAt(address.Latitude, address.Longitude);
        }

        private void OnParcelSelected(ParcelModel parcel)
        {
            LogDebug(string.Format("Parcelle sélectionnée: {0}", parcel.GetFormattedId()));
        }

        private void OnSelectionCleared()
        {
            LogDebug("Sélection effacée");
        }

        #endregion

        #region Debug Logging

        private void LogDebug(string message)
        {
            if (_enableDebugLogs)
            {
                Debug.Log(string.Format("[CadastralMapController] {0}", message));
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning(string.Format("[CadastralMapController] {0}", message));
        }

        #endregion

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
    }
}
