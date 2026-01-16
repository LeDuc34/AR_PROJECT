using System;
using System.Collections;
using UnityEngine;
using GeoscaleCadastre.Models;

namespace GeoscaleCadastre.Map
{
    /// <summary>
    /// Gestionnaire de carte pour Mapbox Unity SDK
    /// Implémente les animations et comportements de Geoscale (flyTo, auto-zoom)
    /// </summary>
    public class MapManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Référence au composant AbstractMap de Mapbox")]
        private Component _mapboxMap; // AbstractMap - utiliser Component pour éviter la dépendance directe

        [SerializeField]
        [Tooltip("Latitude par défaut (Paris)")]
        private double _defaultLatitude = 48.8566;

        [SerializeField]
        [Tooltip("Longitude par défaut (Paris)")]
        private double _defaultLongitude = 2.3522;

        [SerializeField]
        [Tooltip("Zoom par défaut")]
        private float _defaultZoom = 15f;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Durée par défaut des animations flyTo (secondes)")]
        private float _defaultFlyDuration = 1.2f;

        [SerializeField]
        [Tooltip("Courbe d'animation")]
        private AnimationCurve _flyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // État actuel
        private double _currentLatitude;
        private double _currentLongitude;
        private float _currentZoom;
        private Coroutine _flyCoroutine;

        // Events
        public event Action OnMapInitialized;
        public event Action<double, double, float> OnMapMoved;
        public event Action OnFlyToStarted;
        public event Action OnFlyToCompleted;

        /// <summary>Position actuelle (latitude)</summary>
        public double CurrentLatitude { get { return _currentLatitude; } }

        /// <summary>Position actuelle (longitude)</summary>
        public double CurrentLongitude { get { return _currentLongitude; } }

        /// <summary>Zoom actuel</summary>
        public float CurrentZoom { get { return _currentZoom; } }

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Initialise la carte avec les coordonnées par défaut
        /// </summary>
        public void Initialize()
        {
            Initialize(_defaultLatitude, _defaultLongitude, _defaultZoom);
        }

        /// <summary>
        /// Initialise la carte avec des coordonnées spécifiques
        /// </summary>
        public void Initialize(double latitude, double longitude, float zoom)
        {
            _currentLatitude = latitude;
            _currentLongitude = longitude;
            _currentZoom = zoom;

            // Initialiser Mapbox si disponible
            if (_mapboxMap != null)
            {
                // Appeler la méthode Initialize de Mapbox via reflection ou interface
                // Note: Dépend de la version du SDK Mapbox installé
                InitializeMapboxMap(latitude, longitude, (int)zoom);
            }

            if (OnMapInitialized != null)
                OnMapInitialized();

            Debug.Log(string.Format("[MapManager] Carte initialisée: ({0}, {1}) zoom {2}",
                latitude, longitude, zoom));
        }

        /// <summary>
        /// Animation fluide vers une position (pattern Geoscale flyTo)
        /// </summary>
        /// <param name="latitude">Latitude cible</param>
        /// <param name="longitude">Longitude cible</param>
        /// <param name="zoom">Zoom cible</param>
        /// <param name="duration">Durée de l'animation en secondes</param>
        public void FlyTo(double latitude, double longitude, float zoom, float duration = -1)
        {
            if (duration < 0) duration = _defaultFlyDuration;

            // Arrêter l'animation précédente si elle existe
            if (_flyCoroutine != null)
            {
                StopCoroutine(_flyCoroutine);
            }

            _flyCoroutine = StartCoroutine(FlyToCoroutine(latitude, longitude, zoom, duration));
        }

        /// <summary>
        /// Centre la carte sur une parcelle avec zoom automatique (pattern Geoscale)
        /// </summary>
        /// <param name="parcel">Parcelle cible</param>
        /// <param name="duration">Durée de l'animation</param>
        public void CenterOnParcel(ParcelModel parcel, float duration = 0.8f)
        {
            if (parcel == null)
            {
                Debug.LogWarning("[MapManager] Parcelle null");
                return;
            }

            // Calculer le zoom optimal basé sur la taille de la parcelle
            float targetZoom = CalculateOptimalZoom(parcel);

            // Utiliser le centroïde de la parcelle
            double lat = parcel.Centroid.y;
            double lng = parcel.Centroid.x;

            Debug.Log(string.Format("[MapManager] Centrage sur parcelle: ({0}, {1}) zoom {2}",
                lat, lng, targetZoom));

            FlyTo(lat, lng, targetZoom, duration);
        }

        /// <summary>
        /// Centre la carte sur une adresse
        /// </summary>
        /// <param name="address">Résultat de recherche d'adresse</param>
        /// <param name="zoom">Zoom cible (défaut: 18)</param>
        /// <param name="duration">Durée de l'animation</param>
        public void CenterOnAddress(AddressResult address, float zoom = 18f, float duration = 2f)
        {
            if (address == null)
            {
                Debug.LogWarning("[MapManager] Adresse null");
                return;
            }

            Debug.Log(string.Format("[MapManager] Navigation vers: {0}", address.Text));
            FlyTo(address.Latitude, address.Longitude, zoom, duration);
        }

        /// <summary>
        /// Déplace la carte instantanément (sans animation)
        /// </summary>
        public void SetPosition(double latitude, double longitude, float zoom)
        {
            _currentLatitude = latitude;
            _currentLongitude = longitude;
            _currentZoom = zoom;

            UpdateMapboxPosition();

            if (OnMapMoved != null)
                OnMapMoved(latitude, longitude, zoom);
        }

        /// <summary>
        /// Calcule le niveau de zoom optimal basé sur la taille de la parcelle
        /// Pattern identique à Geoscale
        /// </summary>
        private float CalculateOptimalZoom(ParcelModel parcel)
        {
            float maxDimension = parcel.GetMaxDimension();

            // Conversion approximative degrés -> mètres pour la France
            // 1 degré latitude ≈ 111km, 1 degré longitude ≈ 75km (à ~46° latitude)
            float dimensionMeters = maxDimension * 90000f; // Approximation

            // Échelle de zoom (identique à Geoscale)
            if (dimensionMeters > 1000) return 15f;      // Très grande parcelle
            if (dimensionMeters > 500) return 16f;       // Grande
            if (dimensionMeters > 200) return 17f;       // Moyenne
            if (dimensionMeters > 100) return 18f;       // Petite
            return 19f;                                   // Très petite
        }

        private IEnumerator FlyToCoroutine(double targetLat, double targetLng, float targetZoom, float duration)
        {
            if (OnFlyToStarted != null)
                OnFlyToStarted();

            double startLat = _currentLatitude;
            double startLng = _currentLongitude;
            float startZoom = _currentZoom;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Appliquer la courbe d'animation
                float curveT = _flyCurve.Evaluate(t);

                // Interpoler les valeurs
                _currentLatitude = Lerp(startLat, targetLat, curveT);
                _currentLongitude = Lerp(startLng, targetLng, curveT);
                _currentZoom = Mathf.Lerp(startZoom, targetZoom, curveT);

                // Mettre à jour la carte Mapbox
                UpdateMapboxPosition();

                yield return null;
            }

            // S'assurer d'atteindre exactement la cible
            _currentLatitude = targetLat;
            _currentLongitude = targetLng;
            _currentZoom = targetZoom;
            UpdateMapboxPosition();

            _flyCoroutine = null;

            if (OnFlyToCompleted != null)
                OnFlyToCompleted();

            if (OnMapMoved != null)
                OnMapMoved(_currentLatitude, _currentLongitude, _currentZoom);
        }

        private double Lerp(double a, double b, float t)
        {
            return a + (b - a) * t;
        }

        private void InitializeMapboxMap(double lat, double lng, int zoom)
        {
            // Initialisation Mapbox via reflection (pour compatibilité sans dépendance directe)
            if (_mapboxMap == null) return;

            try
            {
                var mapType = _mapboxMap.GetType();

                // Chercher la méthode Initialize(Vector2d, int)
                var initMethod = mapType.GetMethod("Initialize",
                    new Type[] { typeof(object), typeof(int) });

                if (initMethod != null)
                {
                    // Créer Vector2d (lat, lng)
                    var vector2dType = Type.GetType("Mapbox.Utils.Vector2d, Mapbox.Unity");
                    if (vector2dType != null)
                    {
                        var center = Activator.CreateInstance(vector2dType, lat, lng);
                        initMethod.Invoke(_mapboxMap, new object[] { center, zoom });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(string.Format("[MapManager] Impossible d'initialiser Mapbox: {0}", e.Message));
            }
        }

        private void UpdateMapboxPosition()
        {
            // Mise à jour de la position Mapbox via reflection
            if (_mapboxMap == null) return;

            try
            {
                var mapType = _mapboxMap.GetType();

                // Option 1: Chercher la méthode UpdateMap(Vector2d, float)
                var updateMethod = mapType.GetMethod("UpdateMap");
                if (updateMethod != null)
                {
                    var vector2dType = Type.GetType("Mapbox.Utils.Vector2d, Mapbox.Unity");
                    if (vector2dType != null)
                    {
                        var center = Activator.CreateInstance(vector2dType, _currentLatitude, _currentLongitude);
                        updateMethod.Invoke(_mapboxMap, new object[] { center, _currentZoom });
                        return;
                    }
                }

                // Option 2: Modifier les propriétés directement
                var centerProp = mapType.GetProperty("CenterLatitudeLongitude");
                var zoomProp = mapType.GetProperty("Zoom");

                if (centerProp != null)
                {
                    var vector2dType = Type.GetType("Mapbox.Utils.Vector2d, Mapbox.Unity");
                    if (vector2dType != null)
                    {
                        var center = Activator.CreateInstance(vector2dType, _currentLatitude, _currentLongitude);
                        centerProp.SetValue(_mapboxMap, center);
                    }
                }

                if (zoomProp != null)
                {
                    zoomProp.SetValue(_mapboxMap, _currentZoom);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(string.Format("[MapManager] Erreur mise à jour Mapbox: {0}", e.Message));
            }
        }
    }
}
