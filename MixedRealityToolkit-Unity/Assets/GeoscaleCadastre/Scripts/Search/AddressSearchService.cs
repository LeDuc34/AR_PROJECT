using System;
using System.Collections.Generic;
using UnityEngine;
using GeoscaleCadastre.Models;

namespace GeoscaleCadastre.Search
{
    /// <summary>
    /// Service unifié de recherche d'adresse
    /// Implémente le pattern Geoscale: Mapbox en priorité, Nominatim en fallback
    /// Debounce 300ms pour éviter les requêtes excessives
    /// </summary>
    public class AddressSearchService : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Token d'accès Mapbox")]
        private string _mapboxAccessToken;

        [SerializeField]
        [Tooltip("Nombre minimum de caractères pour déclencher la recherche")]
        private int _minQueryLength = 3;

        [SerializeField]
        [Tooltip("Délai de debounce en millisecondes")]
        private int _debounceDelayMs = 300;

        // APIs
        private MapboxGeocodingAPI _mapboxAPI;
        private NominatimAPI _nominatimAPI;
        private SearchDebouncer _debouncer;

        // État
        private bool _isSearching;
        private string _lastQuery;

        // Events
        public event Action<List<AddressResult>> OnSearchResults;
        public event Action<string> OnSearchError;
        public event Action OnSearchStarted;
        public event Action OnSearchCompleted;

        /// <summary>
        /// Indique si une recherche est en cours
        /// </summary>
        public bool IsSearching { get { return _isSearching; } }

        private void Awake()
        {
            InitializeServices();
        }

        private void InitializeServices()
        {
            // Initialiser les APIs
            if (!string.IsNullOrEmpty(_mapboxAccessToken))
            {
                _mapboxAPI = new MapboxGeocodingAPI(_mapboxAccessToken, this);
            }
            else
            {
                Debug.LogWarning("[AddressSearchService] Token Mapbox manquant - utilisation de Nominatim uniquement");
            }

            _nominatimAPI = new NominatimAPI(this);

            // Initialiser le debouncer (300ms comme Geoscale)
            _debouncer = new SearchDebouncer(this, _debounceDelayMs);
        }

        /// <summary>
        /// Configure le token Mapbox à runtime
        /// </summary>
        public void SetMapboxToken(string token)
        {
            _mapboxAccessToken = token;
            if (!string.IsNullOrEmpty(token))
            {
                _mapboxAPI = new MapboxGeocodingAPI(token, this);
            }
        }

        /// <summary>
        /// Lance une recherche d'adresse avec debounce automatique
        /// </summary>
        /// <param name="query">Texte de recherche</param>
        public void Search(string query)
        {
            _lastQuery = query;

            // Vérifier la longueur minimale
            if (string.IsNullOrEmpty(query) || query.Length < _minQueryLength)
            {
                _debouncer.Cancel();
                if (OnSearchResults != null)
                    OnSearchResults(new List<AddressResult>());
                return;
            }

            // Debounce: attend 300ms d'inactivité avant de lancer la recherche
            _debouncer.Debounce(() => ExecuteSearch(query));
        }

        /// <summary>
        /// Annule la recherche en cours
        /// </summary>
        public void CancelSearch()
        {
            _debouncer.Cancel();
            _isSearching = false;
        }

        /// <summary>
        /// Vide les résultats
        /// </summary>
        public void ClearResults()
        {
            if (OnSearchResults != null)
                OnSearchResults(new List<AddressResult>());
        }

        private void ExecuteSearch(string query)
        {
            _isSearching = true;
            if (OnSearchStarted != null)
                OnSearchStarted();

            // Pattern Geoscale: Mapbox en priorité, Nominatim en fallback
            if (_mapboxAPI != null)
            {
                SearchWithMapbox(query);
            }
            else
            {
                SearchWithNominatim(query);
            }
        }

        private void SearchWithMapbox(string query)
        {
            _mapboxAPI.Search(query,
                // Success callback
                (results) =>
                {
                    if (results.Count > 0)
                    {
                        // Mapbox a trouvé des résultats
                        CompleteSearch(results);
                    }
                    else
                    {
                        // Aucun résultat Mapbox, fallback vers Nominatim
                        Debug.Log("[AddressSearchService] Aucun résultat Mapbox, fallback Nominatim");
                        SearchWithNominatim(query);
                    }
                },
                // Error callback: fallback vers Nominatim
                (error) =>
                {
                    Debug.LogWarning(string.Format("[AddressSearchService] Mapbox échoué: {0}, fallback Nominatim", error));
                    SearchWithNominatim(query);
                }
            );
        }

        private void SearchWithNominatim(string query)
        {
            _nominatimAPI.Search(query,
                // Success callback
                (results) =>
                {
                    CompleteSearch(results);
                },
                // Error callback: toutes les APIs ont échoué
                (error) =>
                {
                    Debug.LogError(string.Format("[AddressSearchService] Toutes les APIs ont échoué: {0}", error));
                    _isSearching = false;

                    if (OnSearchError != null)
                        OnSearchError("Impossible de rechercher l'adresse");

                    if (OnSearchCompleted != null)
                        OnSearchCompleted();
                }
            );
        }

        private void CompleteSearch(List<AddressResult> results)
        {
            _isSearching = false;

            if (OnSearchResults != null)
                OnSearchResults(results);

            if (OnSearchCompleted != null)
                OnSearchCompleted();

            Debug.Log(string.Format("[AddressSearchService] Recherche terminée: {0} résultats", results.Count));
        }

        private void OnDestroy()
        {
            if (_debouncer != null)
                _debouncer.Cancel();
        }
    }
}
