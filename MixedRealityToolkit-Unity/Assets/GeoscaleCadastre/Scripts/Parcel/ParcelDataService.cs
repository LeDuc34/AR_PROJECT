using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using GeoscaleCadastre.Models;

namespace GeoscaleCadastre.Parcel
{
    /// <summary>
    /// Service de récupération des données cadastrales
    /// Utilise l'API Carto IGN (même source que Geoscale)
    /// Effectue des requêtes parallèles parcelle + commune
    /// </summary>
    public class ParcelDataService : MonoBehaviour
    {
        // URLs API Carto IGN (identique à Geoscale)
        private const string APICARTO_PARCELLE = "https://apicarto.ign.fr/api/cadastre/parcelle";
        private const string APICARTO_COMMUNE = "https://apicarto.ign.fr/api/cadastre/commune";

        // État
        private bool _isLoading;

        // Events
        public event Action<ParcelModel> OnParcelLoaded;
        public event Action<string> OnError;
        public event Action OnLoadingStarted;
        public event Action OnLoadingCompleted;

        /// <summary>
        /// Indique si un chargement est en cours
        /// </summary>
        public bool IsLoading { get { return _isLoading; } }

        /// <summary>
        /// Récupère les données d'une parcelle à partir de coordonnées GPS
        /// </summary>
        /// <param name="latitude">Latitude</param>
        /// <param name="longitude">Longitude</param>
        public void FetchParcelAtCoordinates(double latitude, double longitude)
        {
            if (_isLoading)
            {
                Debug.LogWarning("[ParcelDataService] Chargement déjà en cours");
                return;
            }

            StartCoroutine(FetchParcelCoroutine(latitude, longitude));
        }

        private IEnumerator FetchParcelCoroutine(double lat, double lng)
        {
            _isLoading = true;
            if (OnLoadingStarted != null)
                OnLoadingStarted();

            // Créer le point GeoJSON pour la requête
            string geomPoint = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{{\"type\":\"Point\",\"coordinates\":[{0},{1}]}}",
                lng, lat
            );

            // Lancer les requêtes en parallèle (pattern Geoscale)
            ParcelModel parcel = null;
            string communeName = null;
            string parcelError = null;
            string communeError = null;
            bool parcelDone = false;
            bool communeDone = false;

            // Requête parcelle
            StartCoroutine(FetchParcelDataCoroutine(geomPoint,
                (p) => { parcel = p; parcelDone = true; },
                (e) => { parcelError = e; parcelDone = true; }
            ));

            // Requête commune
            StartCoroutine(FetchCommuneCoroutine(geomPoint,
                (name) => { communeName = name; communeDone = true; },
                (e) => { communeError = e; communeDone = true; }
            ));

            // Attendre que les deux requêtes soient terminées
            while (!parcelDone || !communeDone)
            {
                yield return null;
            }

            _isLoading = false;

            // Traiter les résultats
            if (parcel != null)
            {
                // Enrichir avec le nom de commune
                if (!string.IsNullOrEmpty(communeName))
                {
                    parcel.NomCommune = communeName;
                }

                if (OnParcelLoaded != null)
                    OnParcelLoaded(parcel);

                Debug.Log(string.Format("[ParcelDataService] Parcelle chargée: {0}", parcel));
            }
            else
            {
                string error = parcelError ?? "Aucune parcelle trouvée à ces coordonnées";
                Debug.LogWarning(string.Format("[ParcelDataService] Erreur: {0}", error));

                if (OnError != null)
                    OnError(error);
            }

            if (OnLoadingCompleted != null)
                OnLoadingCompleted();
        }

        private IEnumerator FetchParcelDataCoroutine(string geomPoint, Action<ParcelModel> onSuccess, Action<string> onError)
        {
            string url = string.Format("{0}?geom={1}",
                APICARTO_PARCELLE, UnityWebRequest.EscapeURL(geomPoint));

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                #if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
                #else
                if (request.isNetworkError || request.isHttpError)
                #endif
                {
                    onError(string.Format("API Carto error: {0}", request.error));
                    yield break;
                }

                try
                {
                    var parcel = ParseParcelResponse(request.downloadHandler.text);
                    onSuccess(parcel);
                }
                catch (Exception e)
                {
                    onError(string.Format("Parsing error: {0}", e.Message));
                }
            }
        }

        private IEnumerator FetchCommuneCoroutine(string geomPoint, Action<string> onSuccess, Action<string> onError)
        {
            string url = string.Format("{0}?geom={1}",
                APICARTO_COMMUNE, UnityWebRequest.EscapeURL(geomPoint));

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                #if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
                #else
                if (request.isNetworkError || request.isHttpError)
                #endif
                {
                    onError(string.Format("API Commune error: {0}", request.error));
                    yield break;
                }

                try
                {
                    string communeName = ParseCommuneResponse(request.downloadHandler.text);
                    onSuccess(communeName);
                }
                catch (Exception e)
                {
                    onError(string.Format("Commune parsing error: {0}", e.Message));
                }
            }
        }

        private ParcelModel ParseParcelResponse(string json)
        {
            // Parse GeoJSON FeatureCollection de l'API Carto
            var response = JsonUtility.FromJson<GeoJsonFeatureCollection>(json);

            if (response == null || response.features == null || response.features.Length == 0)
            {
                return null;
            }

            var feature = response.features[0];
            var props = feature.properties;

            var parcel = new ParcelModel
            {
                Idu = props.id ?? "",
                CodeInsee = props.commune ?? "",
                Section = props.section ?? "",
                Numero = props.numero ?? "",
                Prefixe = props.prefixe ?? "",
                TypeSource = "cadastre_officiel",
                DateMaj = DateTime.Now
            };

            // Parser la surface
            float surface;
            if (float.TryParse(props.contenance,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out surface))
            {
                parcel.Surface = surface;
            }

            // Parser la géométrie et calculer centroïde/bbox
            if (feature.geometry != null && feature.geometry.coordinates != null)
            {
                ParseGeometry(feature.geometry, parcel);
            }

            return parcel;
        }

        private void ParseGeometry(GeoJsonGeometry geometry, ParcelModel parcel)
        {
            try
            {
                // Pour un Polygon, coordinates est [[[lng, lat], [lng, lat], ...]]
                // Pour un MultiPolygon, c'est [[[[lng, lat], ...]]]
                // On prend le premier anneau du premier polygone

                if (geometry.type == "Polygon" && geometry.coordinates != null)
                {
                    var ring = geometry.coordinates;
                    if (ring.Length > 0)
                    {
                        ParsePolygonRing(ring[0], parcel);
                    }
                }
                else if (geometry.type == "MultiPolygon" && geometry.coordinatesMulti != null)
                {
                    var polygons = geometry.coordinatesMulti;
                    if (polygons.Length > 0 && polygons[0].Length > 0)
                    {
                        ParsePolygonRing(polygons[0][0], parcel);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(string.Format("[ParcelDataService] Erreur parsing géométrie: {0}", e.Message));
            }
        }

        private void ParsePolygonRing(double[][] ring, ParcelModel parcel)
        {
            if (ring == null || ring.Length == 0) return;

            var points = new List<Vector2>();
            double minLng = double.MaxValue, maxLng = double.MinValue;
            double minLat = double.MaxValue, maxLat = double.MinValue;
            double sumLng = 0, sumLat = 0;

            foreach (var coord in ring)
            {
                if (coord.Length >= 2)
                {
                    double lng = coord[0];
                    double lat = coord[1];

                    points.Add(new Vector2((float)lng, (float)lat));

                    minLng = Math.Min(minLng, lng);
                    maxLng = Math.Max(maxLng, lng);
                    minLat = Math.Min(minLat, lat);
                    maxLat = Math.Max(maxLat, lat);
                    sumLng += lng;
                    sumLat += lat;
                }
            }

            if (points.Count > 0)
            {
                parcel.Geometry = points.ToArray();

                // Centroïde (moyenne des points)
                parcel.Centroid = new Vector2(
                    (float)(sumLng / ring.Length),
                    (float)(sumLat / ring.Length)
                );

                // Bounding box (en coordonnées locales, sera converti par MapManager)
                Vector3 center = new Vector3(
                    (float)((minLng + maxLng) / 2),
                    0,
                    (float)((minLat + maxLat) / 2)
                );
                Vector3 size = new Vector3(
                    (float)(maxLng - minLng),
                    0,
                    (float)(maxLat - minLat)
                );
                parcel.BoundingBox = new Bounds(center, size);
            }
        }

        private string ParseCommuneResponse(string json)
        {
            // Parse GeoJSON pour extraire le nom de la commune
            var response = JsonUtility.FromJson<CommuneFeatureCollection>(json);

            if (response != null && response.features != null && response.features.Length > 0)
            {
                return response.features[0].properties.nom_com ?? "";
            }

            return "";
        }

        // Classes pour le parsing JSON API Carto
        [Serializable]
        private class GeoJsonFeatureCollection
        {
            public GeoJsonFeature[] features;
        }

        [Serializable]
        private class GeoJsonFeature
        {
            public ParcelProperties properties;
            public GeoJsonGeometry geometry;
        }

        [Serializable]
        private class ParcelProperties
        {
            public string id;
            public string commune;
            public string section;
            public string numero;
            public string prefixe;
            public string contenance;
        }

        [Serializable]
        private class GeoJsonGeometry
        {
            public string type;
            public double[][][] coordinates;
            public double[][][][] coordinatesMulti;
        }

        [Serializable]
        private class CommuneFeatureCollection
        {
            public CommuneFeature[] features;
        }

        [Serializable]
        private class CommuneFeature
        {
            public CommuneProperties properties;
        }

        [Serializable]
        private class CommuneProperties
        {
            public string nom_com;
        }
    }
}
