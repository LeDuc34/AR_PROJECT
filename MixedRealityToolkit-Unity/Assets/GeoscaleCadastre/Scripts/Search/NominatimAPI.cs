using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using GeoscaleCadastre.Models;

namespace GeoscaleCadastre.Search
{
    /// <summary>
    /// Client API pour Nominatim (OpenStreetMap)
    /// Utilisé comme fallback si Mapbox échoue (pattern Geoscale)
    /// </summary>
    public class NominatimAPI
    {
        private readonly MonoBehaviour _coroutineRunner;

        // Configuration Nominatim
        private const string BASE_URL = "https://nominatim.openstreetmap.org/search";
        private const string USER_AGENT = "Unity-AR-Cadastre/1.0";

        public NominatimAPI(MonoBehaviour coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }

        /// <summary>
        /// Lance une recherche d'adresse
        /// </summary>
        /// <param name="query">Texte de recherche</param>
        /// <param name="onSuccess">Callback avec les résultats</param>
        /// <param name="onError">Callback en cas d'erreur</param>
        public void Search(string query, Action<List<AddressResult>> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 3)
            {
                onSuccess(new List<AddressResult>());
                return;
            }

            _coroutineRunner.StartCoroutine(SearchCoroutine(query, onSuccess, onError));
        }

        private IEnumerator SearchCoroutine(string query, Action<List<AddressResult>> onSuccess, Action<string> onError)
        {
            string encodedQuery = UnityWebRequest.EscapeURL(query);
            string url = string.Format("{0}?q={1}&countrycodes=fr&limit=5&format=json&accept-language=fr&addressdetails=1",
                BASE_URL, encodedQuery);

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // Nominatim requiert un User-Agent
                request.SetRequestHeader("User-Agent", USER_AGENT);

                yield return request.SendWebRequest();

                #if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
                #else
                if (request.isNetworkError || request.isHttpError)
                #endif
                {
                    onError(string.Format("Nominatim API error: {0}", request.error));
                    yield break;
                }

                try
                {
                    var results = ParseResponse(request.downloadHandler.text);
                    onSuccess(results);
                }
                catch (Exception e)
                {
                    onError(string.Format("Nominatim parsing error: {0}", e.Message));
                }
            }
        }

        private List<AddressResult> ParseResponse(string json)
        {
            var results = new List<AddressResult>();

            // Nominatim retourne un array directement
            // Format: [ { "display_name": "...", "lat": "...", "lon": "...", "type": "..." } ]

            // Wrapper pour parser l'array JSON
            string wrappedJson = "{\"items\":" + json + "}";
            var response = JsonUtility.FromJson<NominatimResponseWrapper>(wrappedJson);

            if (response != null && response.items != null)
            {
                foreach (var item in response.items)
                {
                    double lat, lon;
                    if (double.TryParse(item.lat, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out lat) &&
                        double.TryParse(item.lon, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out lon))
                    {
                        var result = new AddressResult
                        {
                            Text = ExtractMainText(item.display_name),
                            Context = ExtractContext(item.display_name),
                            Latitude = lat,
                            Longitude = lon,
                            PlaceType = item.type ?? "unknown",
                            Source = "nominatim"
                        };
                        results.Add(result);
                    }
                }
            }

            return results;
        }

        private string ExtractMainText(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "";
            int commaIndex = displayName.IndexOf(',');
            return commaIndex > 0 ? displayName.Substring(0, commaIndex) : displayName;
        }

        private string ExtractContext(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "";
            int commaIndex = displayName.IndexOf(',');
            if (commaIndex > 0 && commaIndex < displayName.Length - 2)
            {
                string context = displayName.Substring(commaIndex + 2);
                // Limiter la longueur du contexte
                if (context.Length > 50)
                {
                    int lastComma = context.Substring(0, 50).LastIndexOf(',');
                    if (lastComma > 0)
                        return context.Substring(0, lastComma);
                }
                return context;
            }
            return "";
        }

        // Classes pour le parsing JSON Nominatim
        [Serializable]
        private class NominatimResponseWrapper
        {
            public NominatimItem[] items;
        }

        [Serializable]
        private class NominatimItem
        {
            public string display_name;
            public string lat;
            public string lon;
            public string type;
        }
    }
}
