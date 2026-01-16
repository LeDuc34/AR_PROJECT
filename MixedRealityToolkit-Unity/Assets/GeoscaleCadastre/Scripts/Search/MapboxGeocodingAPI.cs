using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using GeoscaleCadastre.Models;

namespace GeoscaleCadastre.Search
{
    /// <summary>
    /// Client API pour Mapbox Geocoding
    /// Configuration identique à Geoscale (France, autocomplétion, fuzzy match)
    /// </summary>
    public class MapboxGeocodingAPI
    {
        private readonly string _accessToken;
        private readonly MonoBehaviour _coroutineRunner;

        // Configuration Mapbox (identique à Geoscale)
        private const string BASE_URL = "https://api.mapbox.com/geocoding/v5/mapbox.places/";
        private const string COUNTRY = "fr";
        private const string TYPES = "address,poi,place,postcode,district,locality,neighborhood";
        private const int LIMIT = 5;
        private const string LANGUAGE = "fr";
        private const string BBOX = "-5.2,41.3,9.6,51.1"; // France bounding box
        private const string PROXIMITY = "2.3522,48.8566"; // Paris (biais de proximité)

        public MapboxGeocodingAPI(string accessToken, MonoBehaviour coroutineRunner)
        {
            _accessToken = accessToken;
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
            string url = string.Format("{0}{1}.json?access_token={2}&country={3}&types={4}&limit={5}&language={6}&bbox={7}&proximity={8}&autocomplete=true&fuzzyMatch=true",
                BASE_URL, encodedQuery, _accessToken, COUNTRY, TYPES, LIMIT, LANGUAGE, BBOX, PROXIMITY);

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                #if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
                #else
                if (request.isNetworkError || request.isHttpError)
                #endif
                {
                    onError(string.Format("Mapbox API error: {0}", request.error));
                    yield break;
                }

                try
                {
                    var results = ParseResponse(request.downloadHandler.text);
                    onSuccess(results);
                }
                catch (Exception e)
                {
                    onError(string.Format("Mapbox parsing error: {0}", e.Message));
                }
            }
        }

        private List<AddressResult> ParseResponse(string json)
        {
            var results = new List<AddressResult>();

            // Parse JSON manuellement (compatible Unity 2018)
            // Format Mapbox: { "features": [ { "place_name": "...", "center": [lng, lat], ... } ] }

            var response = JsonUtility.FromJson<MapboxResponse>(json);
            if (response != null && response.features != null)
            {
                foreach (var feature in response.features)
                {
                    var result = new AddressResult
                    {
                        Text = ExtractMainText(feature.place_name),
                        Context = ExtractContext(feature.place_name),
                        Longitude = feature.center[0],
                        Latitude = feature.center[1],
                        PlaceType = feature.place_type != null && feature.place_type.Length > 0
                            ? feature.place_type[0] : "unknown",
                        Source = "mapbox"
                    };
                    results.Add(result);
                }
            }

            return results;
        }

        private string ExtractMainText(string placeName)
        {
            if (string.IsNullOrEmpty(placeName)) return "";
            int commaIndex = placeName.IndexOf(',');
            return commaIndex > 0 ? placeName.Substring(0, commaIndex) : placeName;
        }

        private string ExtractContext(string placeName)
        {
            if (string.IsNullOrEmpty(placeName)) return "";
            int commaIndex = placeName.IndexOf(',');
            if (commaIndex > 0 && commaIndex < placeName.Length - 2)
                return placeName.Substring(commaIndex + 2);
            return "";
        }

        // Classes pour le parsing JSON Mapbox
        [Serializable]
        private class MapboxResponse
        {
            public MapboxFeature[] features;
        }

        [Serializable]
        private class MapboxFeature
        {
            public string place_name;
            public double[] center;
            public string[] place_type;
        }
    }
}
