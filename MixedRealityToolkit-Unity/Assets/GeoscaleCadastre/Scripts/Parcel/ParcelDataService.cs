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
            Debug.Log("=== [ParcelDataService.FetchParcelAtCoordinates] DÉBUT ===");
            Debug.Log(string.Format("[ParcelDataService.FetchParcelAtCoordinates] Coordonnées GPS: ({0:F6}, {1:F6})", latitude, longitude));

            if (_isLoading)
            {
                Debug.LogWarning("[ParcelDataService.FetchParcelAtCoordinates] Chargement déjà en cours, ignoré");
                return;
            }

            Debug.Log("[ParcelDataService.FetchParcelAtCoordinates] Démarrage de la coroutine FetchParcelCoroutine...");
            StartCoroutine(FetchParcelCoroutine(latitude, longitude));
            Debug.Log("=== [ParcelDataService.FetchParcelAtCoordinates] FIN (coroutine lancée) ===");
        }

        private IEnumerator FetchParcelCoroutine(double lat, double lng)
        {
            Debug.Log("=== [ParcelDataService.FetchParcelCoroutine] DÉBUT COROUTINE ===");
            Debug.Log(string.Format("[ParcelDataService.FetchParcelCoroutine] Coordonnées: lat={0:F6}, lng={1:F6}", lat, lng));

            _isLoading = true;
            if (OnLoadingStarted != null)
                OnLoadingStarted();

            // Créer le point GeoJSON pour la requête
            string geomPoint = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{{\"type\":\"Point\",\"coordinates\":[{0},{1}]}}",
                lng, lat
            );

            Debug.Log(string.Format("[ParcelDataService.FetchParcelCoroutine] GeoJSON Point: {0}", geomPoint));

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

            // Attendre que les deux requêtes soient terminées (avec timeout de sécurité)
            float timeout = 30f; // 30 secondes max
            float elapsed = 0f;
            while (!parcelDone || !communeDone)
            {
                elapsed += Time.deltaTime;
                if (elapsed > timeout)
                {
                    Debug.LogWarning("[ParcelDataService.FetchParcelCoroutine] Timeout atteint, abandon des requêtes en cours");
                    break;
                }
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

            Debug.Log(string.Format("[ParcelDataService.FetchParcelDataCoroutine] >>> Requête API: {0}", url));

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                Debug.Log("[ParcelDataService.FetchParcelDataCoroutine] Envoi de la requête...");
                yield return request.SendWebRequest();

                Debug.Log(string.Format("[ParcelDataService.FetchParcelDataCoroutine] Requête terminée - Code: {0}",
                    request.responseCode));

                #if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
                #else
                if (request.isNetworkError || request.isHttpError)
                #endif
                {
                    Debug.LogError(string.Format("[ParcelDataService.FetchParcelDataCoroutine] ERREUR API - Code: {0}, Erreur: {1}",
                        request.responseCode, request.error));
                    onError(string.Format("API Carto error: {0}", request.error));
                    yield break;
                }

                Debug.Log(string.Format("[ParcelDataService.FetchParcelDataCoroutine] Réponse reçue - Taille: {0} caractères",
                    request.downloadHandler.text.Length));
                Debug.Log(string.Format("[ParcelDataService.FetchParcelDataCoroutine] JSON brut (200 premiers caractères): {0}",
                    request.downloadHandler.text.Substring(0, Mathf.Min(200, request.downloadHandler.text.Length))));

                ParcelModel parcel = null;
                try
                {
                    parcel = ParseParcelResponse(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    Debug.LogError(string.Format("[ParcelDataService.FetchParcelDataCoroutine] Exception lors du parsing: {0}\nStackTrace: {1}",
                        e.Message, e.StackTrace));
                    onError(string.Format("Parsing error: {0}", e.Message));
                    yield break;
                }

                if (parcel != null)
                {
                    Debug.Log("[ParcelDataService.FetchParcelDataCoroutine] Parcelle parsée avec succès!");
                    onSuccess(parcel);
                }
                else
                {
                    // Pas d'erreur technique, simplement pas de parcelle à cet endroit
                    Debug.Log("[ParcelDataService.FetchParcelDataCoroutine] Aucune parcelle à ces coordonnées");
                    onError("Aucune parcelle trouvée à ces coordonnées");
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
            Debug.Log("[ParcelDataService.ParseParcelResponse] Début du parsing...");
            Debug.Log(string.Format("[ParcelDataService.ParseParcelResponse] JSON length: {0} caractères", json.Length));

            // Parse GeoJSON FeatureCollection de l'API Carto
            // Note: On ne peut pas utiliser JsonUtility pour parser la géométrie directement
            // car Polygon et MultiPolygon ont des structures différentes (3D vs 4D)

            GeoJsonFeatureCollectionWrapper response = null;

            // Parser le JSON de base sans la géométrie - avec protection contre les exceptions
            try
            {
                response = JsonUtility.FromJson<GeoJsonFeatureCollectionWrapper>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning(string.Format("[ParcelDataService.ParseParcelResponse] Erreur parsing JSON: {0}", e.Message));
                return null;
            }

            if (response == null || response.features == null || response.features.Length == 0)
            {
                Debug.Log("[ParcelDataService.ParseParcelResponse] Aucune parcelle trouvée à ces coordonnées (réponse API vide)");
                return null;
            }

            Debug.Log(string.Format("[ParcelDataService.ParseParcelResponse] {0} feature(s) trouvée(s)", response.features.Length));

            // Extraire la première feature du JSON manuellement pour parser la géométrie
            int firstFeatureStart = json.IndexOf("\"features\":[{");
            if (firstFeatureStart == -1)
            {
                Debug.LogError("[ParcelDataService.ParseParcelResponse] Impossible de trouver les features dans le JSON");
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

            Debug.Log(string.Format("[ParcelDataService.ParseParcelResponse] Parcelle: {0} {1} {2}",
                parcel.CodeInsee, parcel.Section, parcel.Numero));

            // Parser la surface
            float surface;
            if (float.TryParse(props.contenance,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out surface))
            {
                parcel.Surface = surface;
                Debug.Log(string.Format("[ParcelDataService.ParseParcelResponse] Surface: {0} m²", surface));
            }

            // Parser la géométrie manuellement depuis le JSON brut
            if (feature.geometry != null && !string.IsNullOrEmpty(feature.geometry.type))
            {
                Debug.Log(string.Format("[ParcelDataService.ParseParcelResponse] Géométrie type: {0}", feature.geometry.type));
                ParseGeometryFromJson(json, feature.geometry.type, parcel);
                Debug.Log(string.Format("[ParcelDataService.ParseParcelResponse] Géométrie parsée: {0} points",
                    parcel.Geometry != null ? parcel.Geometry.Length : 0));
            }
            else
            {
                Debug.LogError("[ParcelDataService.ParseParcelResponse] Pas de géométrie dans la réponse !");
            }

            return parcel;
        }

        /// <summary>
        /// Parse la géométrie directement depuis le JSON brut
        /// Car JsonUtility ne peut pas gérer Polygon (3D) et MultiPolygon (4D) avec le même champ
        /// </summary>
        private void ParseGeometryFromJson(string json, string geometryType, ParcelModel parcel)
        {
            try
            {
                Debug.Log(string.Format("[ParcelDataService.ParseGeometryFromJson] Type: {0}", geometryType));

                // Trouver le début de "coordinates"
                int coordStart = json.IndexOf("\"coordinates\":");
                if (coordStart == -1)
                {
                    Debug.LogError("[ParcelDataService.ParseGeometryFromJson] Champ 'coordinates' introuvable");
                    return;
                }

                // Extraire la partie coordinates du JSON
                coordStart += "\"coordinates\":".Length;
                int depth = 0;
                int coordEnd = coordStart;
                bool inArray = false;

                // Trouver la fin du tableau de coordinates (en comptant les crochets)
                for (int i = coordStart; i < json.Length; i++)
                {
                    if (json[i] == '[')
                    {
                        depth++;
                        inArray = true;
                    }
                    else if (json[i] == ']')
                    {
                        depth--;
                        if (inArray && depth == 0)
                        {
                            coordEnd = i + 1;
                            break;
                        }
                    }
                }

                if (coordEnd <= coordStart)
                {
                    Debug.LogError("[ParcelDataService.ParseGeometryFromJson] Impossible de trouver la fin des coordinates");
                    return;
                }

                string coordsJson = json.Substring(coordStart, coordEnd - coordStart);
                Debug.Log(string.Format("[ParcelDataService.ParseGeometryFromJson] Coordinates JSON (premiers 200 chars): {0}",
                    coordsJson.Substring(0, Mathf.Min(200, coordsJson.Length))));

                // Parser les coordonnées selon le type
                if (geometryType == "Polygon")
                {
                    ParsePolygonCoordinates(coordsJson, parcel);
                }
                else if (geometryType == "MultiPolygon")
                {
                    ParseMultiPolygonCoordinates(coordsJson, parcel);
                }
                else
                {
                    Debug.LogWarning(string.Format("[ParcelDataService.ParseGeometryFromJson] Type de géométrie non supporté: {0}", geometryType));
                }
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[ParcelDataService.ParseGeometryFromJson] Exception: {0}\nStackTrace: {1}",
                    e.Message, e.StackTrace));
            }
        }

        /// <summary>
        /// Parse les coordonnées d'un Polygon: [[[lng,lat],[lng,lat],...]]
        /// </summary>
        private void ParsePolygonCoordinates(string coordsJson, ParcelModel parcel)
        {
            Debug.Log("[ParcelDataService.ParsePolygonCoordinates] Parsing Polygon...");

            // Pour un Polygon, on veut le premier ring (extérieur)
            // Format: [[[lng,lat],[lng,lat],...]]

            List<Vector2> points = new List<Vector2>();
            double minLng = double.MaxValue, maxLng = double.MinValue;
            double minLat = double.MaxValue, maxLat = double.MinValue;
            double sumLng = 0, sumLat = 0;

            // Parser manuellement les paires [lng,lat]
            int index = 0;
            int depth = 0;

            while (index < coordsJson.Length)
            {
                char c = coordsJson[index];

                if (c == '[')
                {
                    depth++;
                    if (depth == 3) // On entre dans une coordonnée [lng,lat]
                    {
                        // Extraire la coordonnée jusqu'au prochain ]
                        int endCoord = coordsJson.IndexOf(']', index);
                        if (endCoord > index)
                        {
                            string coordPair = coordsJson.Substring(index + 1, endCoord - index - 1);
                            string[] parts = coordPair.Split(',');
                            if (parts.Length >= 2)
                            {
                                double lng, lat;
                                if (double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out lng) &&
                                    double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out lat))
                                {
                                    points.Add(new Vector2((float)lng, (float)lat));
                                    minLng = Math.Min(minLng, lng);
                                    maxLng = Math.Max(maxLng, lng);
                                    minLat = Math.Min(minLat, lat);
                                    maxLat = Math.Max(maxLat, lat);
                                    sumLng += lng;
                                    sumLat += lat;
                                }
                            }
                            // Sauter jusqu'après le ] de cette coordonnée
                            // Le prochain caractère sera traité et décrementera depth
                            index = endCoord;
                            continue; // Ne pas faire index++ à la fin
                        }
                    }
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 1)
                    {
                        // Fin du premier ring (on a fermé le 2ème niveau de crochets)
                        break;
                    }
                }

                index++;
            }

            if (points.Count > 0)
            {
                Debug.Log(string.Format("[ParcelDataService.ParsePolygonCoordinates] {0} points extraits", points.Count));
                FinalizeParcelGeometry(parcel, points, minLng, maxLng, minLat, maxLat, sumLng, sumLat);
            }
            else
            {
                Debug.LogError("[ParcelDataService.ParsePolygonCoordinates] Aucun point extrait !");
            }
        }

        /// <summary>
        /// Parse les coordonnées d'un MultiPolygon: [[[[lng,lat],[lng,lat],...]]]
        /// </summary>
        private void ParseMultiPolygonCoordinates(string coordsJson, ParcelModel parcel)
        {
            Debug.Log("[ParcelDataService.ParseMultiPolygonCoordinates] Parsing MultiPolygon...");

            // Pour un MultiPolygon, on veut le premier ring du premier polygone
            // Format: [[[[lng,lat],[lng,lat],...]]]

            List<Vector2> points = new List<Vector2>();
            double minLng = double.MaxValue, maxLng = double.MinValue;
            double minLat = double.MaxValue, maxLat = double.MinValue;
            double sumLng = 0, sumLat = 0;

            int index = 0;
            int depth = 0;

            while (index < coordsJson.Length)
            {
                char c = coordsJson[index];

                if (c == '[')
                {
                    depth++;
                    if (depth == 4) // On entre dans une coordonnée [lng,lat] dans un MultiPolygon
                    {
                        // Extraire la coordonnée jusqu'au prochain ]
                        int endCoord = coordsJson.IndexOf(']', index);
                        if (endCoord > index)
                        {
                            string coordPair = coordsJson.Substring(index + 1, endCoord - index - 1);
                            string[] parts = coordPair.Split(',');
                            if (parts.Length >= 2)
                            {
                                double lng, lat;
                                if (double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out lng) &&
                                    double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out lat))
                                {
                                    points.Add(new Vector2((float)lng, (float)lat));
                                    minLng = Math.Min(minLng, lng);
                                    maxLng = Math.Max(maxLng, lng);
                                    minLat = Math.Min(minLat, lat);
                                    maxLat = Math.Max(maxLat, lat);
                                    sumLng += lng;
                                    sumLat += lat;
                                }
                            }
                            // Sauter jusqu'après le ] de cette coordonnée
                            index = endCoord;
                            continue; // Ne pas faire index++ à la fin
                        }
                    }
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 2)
                    {
                        // Fin du premier ring du premier polygone
                        break;
                    }
                }

                index++;
            }

            if (points.Count > 0)
            {
                Debug.Log(string.Format("[ParcelDataService.ParseMultiPolygonCoordinates] {0} points extraits", points.Count));
                FinalizeParcelGeometry(parcel, points, minLng, maxLng, minLat, maxLat, sumLng, sumLat);
            }
            else
            {
                Debug.LogError("[ParcelDataService.ParseMultiPolygonCoordinates] Aucun point extrait !");
            }
        }

        /// <summary>
        /// Finalise la géométrie de la parcelle avec les points extraits
        /// </summary>
        private void FinalizeParcelGeometry(ParcelModel parcel, List<Vector2> points,
            double minLng, double maxLng, double minLat, double maxLat, double sumLng, double sumLat)
        {
            parcel.Geometry = points.ToArray();

            // Centroïde (moyenne des points)
            parcel.Centroid = new Vector2(
                (float)(sumLng / points.Count),
                (float)(sumLat / points.Count)
            );

            Debug.Log(string.Format("[ParcelDataService.FinalizeParcelGeometry] Centroid: ({0:F6}, {1:F6})",
                parcel.Centroid.x, parcel.Centroid.y));

            // Bounding box
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

            Debug.Log(string.Format("[ParcelDataService.FinalizeParcelGeometry] BBox: center=({0:F6}, {1:F6}), size=({2:F6}, {3:F6})",
                center.x, center.z, size.x, size.z));
        }

        // Ancienne méthode (gardée pour référence, mais non utilisée)
        private void ParseGeometry(GeoJsonGeometry geometry, ParcelModel parcel)
        {
            try
            {
                // Cette méthode n'est plus utilisée - remplacée par ParseGeometryFromJson
                // car JsonUtility ne peut pas gérer Polygon (3D) et MultiPolygon (4D)
                Debug.LogWarning("[ParcelDataService.ParseGeometry] Méthode obsolète appelée");
            }
            catch (Exception e)
            {
                Debug.LogWarning(string.Format("[ParcelDataService] Erreur parsing géométrie: {0}", e.Message));
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
        private class GeoJsonFeatureCollectionWrapper
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
            // Note: On ne parse pas les coordonnées avec JsonUtility car la structure
            // varie entre Polygon (3D) et MultiPolygon (4D)
            // Les coordonnées sont parsées manuellement depuis le JSON brut
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
