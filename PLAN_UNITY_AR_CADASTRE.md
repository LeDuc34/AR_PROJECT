# Plan d'Implémentation - Carte Cadastrale AR Unity/MRTK pour HoloLens 2

## Objectif
Adapter la logique Geoscale (carte cadastrale, recherche d'adresse, sélection de parcelle) dans un projet Unity AR existant avec MRTK pour HoloLens 2.

---

## Architecture Proposée

```
Unity Project/
├── Scripts/
│   ├── Map/
│   │   ├── MapManager.cs              # Gestion Mapbox Unity SDK
│   │   ├── CadastreLayerController.cs # Couche cadastrale IGN
│   │   └── ParcelHighlighter.cs       # Surlignage parcelle sélectionnée
│   ├── Search/
│   │   ├── AddressSearchService.cs    # Service de recherche (async)
│   │   ├── MapboxGeocodingAPI.cs      # API Mapbox
│   │   ├── NominatimAPI.cs            # API Nominatim (fallback)
│   │   └── SearchDebouncer.cs         # Debounce 300ms
│   ├── Parcel/
│   │   ├── ParcelDataService.cs       # Fetch APIcarto IGN
│   │   ├── ParcelModel.cs             # Modèle de données parcelle
│   │   └── ParcelSelectionHandler.cs  # Gestion sélection
│   └── UI/
│       ├── SearchBarUI.cs             # UI MRTK recherche
│       ├── SearchResultsPanel.cs      # Panel résultats
│       └── ParcelInfoPanel.cs         # Infos parcelle sélectionnée
├── Prefabs/
│   ├── SearchBar.prefab               # Barre de recherche MRTK
│   ├── ResultItem.prefab              # Item résultat recherche
│   └── ParcelInfoCard.prefab          # Carte info parcelle
└── Resources/
    └── MapboxConfig.asset             # Configuration Mapbox
```

---

## Étape 1 : Configuration Mapbox Unity SDK

### 1.1 Installation
- Importer Mapbox Unity SDK depuis le Package Manager ou `.unitypackage`
- Configurer le token Mapbox dans `MapboxAccess.cs`

### 1.2 MapManager.cs - Initialisation carte
```csharp
using Mapbox.Unity.Map;
using Mapbox.Utils;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    [SerializeField] private AbstractMap _map;

    // Paris par défaut (comme Geoscale)
    private Vector2d _defaultCenter = new Vector2d(48.8566, 2.3522);
    private float _defaultZoom = 15f;

    public void Initialize()
    {
        _map.Initialize(_defaultCenter, (int)_defaultZoom);
    }

    public void FlyTo(double lat, double lng, float zoom, float duration = 1.2f)
    {
        // Animation fluide vers coordonnées (comme Geoscale flyTo)
        StartCoroutine(AnimateFlyTo(new Vector2d(lat, lng), zoom, duration));
    }

    public void CenterOnParcel(ParcelModel parcel, float duration = 0.8f)
    {
        // Calcul zoom auto basé sur taille parcelle (pattern Geoscale)
        float targetZoom = CalculateOptimalZoom(parcel.BoundingBox);
        FlyTo(parcel.Centroid.x, parcel.Centroid.y, targetZoom, duration);
    }

    private float CalculateOptimalZoom(Bounds bbox)
    {
        float maxDimension = Mathf.Max(bbox.size.x, bbox.size.z);
        if (maxDimension > 1000) return 15f;      // Très grande
        if (maxDimension > 500) return 16f;       // Grande
        if (maxDimension > 200) return 17f;       // Moyenne
        if (maxDimension > 100) return 18f;       // Petite
        return 19f;                                // Très petite
    }
}
```

### 1.3 CadastreLayerController.cs - Couche cadastrale
```csharp
using Mapbox.Unity.Map;
using UnityEngine;

public class CadastreLayerController : MonoBehaviour
{
    [SerializeField] private AbstractMap _map;

    // URL WMTS IGN cadastre (même source que Geoscale)
    private const string CADASTRE_WMTS_URL =
        "https://data.geopf.fr/wmts?" +
        "layer=CADASTRALPARCELS.PARCELLAIRE_EXPRESS&" +
        "style=PCI%20vecteur&" +
        "tilematrixset=PM&" +
        "Service=WMTS&Request=GetTile&Version=1.0.0&" +
        "Format=image/png&" +
        "TileMatrix={z}&TileCol={x}&TileRow={y}";

    private bool _isVisible = true;

    public void ToggleCadastreLayer()
    {
        _isVisible = !_isVisible;
        // Activer/désactiver la couche raster cadastre
    }
}
```

---

## Étape 2 : Système de Recherche d'Adresse

### 2.1 SearchDebouncer.cs - Debounce 300ms (pattern Geoscale)
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

public class SearchDebouncer
{
    private CancellationTokenSource _cts;
    private readonly int _delayMs;

    public SearchDebouncer(int delayMs = 300)
    {
        _delayMs = delayMs;
    }

    public async Task<bool> DebounceAsync(Func<Task> action)
    {
        // Annuler la recherche précédente (comme clearTimeout en JS)
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            await Task.Delay(_delayMs, _cts.Token);
            await action();
            return true;
        }
        catch (TaskCanceledException)
        {
            return false; // Annulé par une nouvelle frappe
        }
    }
}
```

### 2.2 MapboxGeocodingAPI.cs - API Mapbox (prioritaire)
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class MapboxGeocodingAPI
{
    private readonly string _accessToken;

    // Configuration identique à Geoscale
    private const string BASE_URL = "https://api.mapbox.com/geocoding/v5/mapbox.places/";
    private const string COUNTRY = "fr";
    private const string TYPES = "address,poi,place,postcode,district,locality,neighborhood";
    private const int LIMIT = 5;
    private const string LANGUAGE = "fr";
    private const string BBOX = "-5.2,41.3,9.6,51.1"; // France

    public MapboxGeocodingAPI(string accessToken)
    {
        _accessToken = accessToken;
    }

    public async Task<List<AddressResult>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            return new List<AddressResult>();

        string encodedQuery = UnityWebRequest.EscapeURL(query);
        string url = $"{BASE_URL}{encodedQuery}.json?" +
            $"access_token={_accessToken}&" +
            $"country={COUNTRY}&" +
            $"types={TYPES}&" +
            $"limit={LIMIT}&" +
            $"language={LANGUAGE}&" +
            $"bbox={BBOX}&" +
            $"autocomplete=true&" +
            $"fuzzyMatch=true";

        using var request = UnityWebRequest.Get(url);
        var operation = request.SendWebRequest();

        while (!operation.isDone)
            await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
            throw new Exception($"Mapbox API error: {request.error}");

        return ParseMapboxResponse(request.downloadHandler.text);
    }

    private List<AddressResult> ParseMapboxResponse(string json)
    {
        // Parser le JSON Mapbox et retourner les résultats formatés
        var results = new List<AddressResult>();
        // ... parsing logic
        return results;
    }
}
```

### 2.3 NominatimAPI.cs - Fallback (pattern Geoscale)
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class NominatimAPI
{
    private const string BASE_URL = "https://nominatim.openstreetmap.org/search";

    public async Task<List<AddressResult>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            return new List<AddressResult>();

        string url = $"{BASE_URL}?" +
            $"q={UnityWebRequest.EscapeURL(query)}&" +
            $"countrycodes=fr&" +
            $"limit=5&" +
            $"format=json&" +
            $"accept-language=fr";

        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("User-Agent", "Unity-AR-Cadastre/1.0");

        var operation = request.SendWebRequest();
        while (!operation.isDone)
            await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
            throw new Exception($"Nominatim API error: {request.error}");

        return ParseNominatimResponse(request.downloadHandler.text);
    }

    private List<AddressResult> ParseNominatimResponse(string json)
    {
        var results = new List<AddressResult>();
        // ... parsing logic
        return results;
    }
}
```

### 2.4 AddressSearchService.cs - Service unifié avec fallback
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class AddressSearchService : MonoBehaviour
{
    [SerializeField] private string _mapboxToken;

    private MapboxGeocodingAPI _mapboxAPI;
    private NominatimAPI _nominatimAPI;
    private SearchDebouncer _debouncer;

    public event Action<List<AddressResult>> OnSearchResults;
    public event Action<string> OnSearchError;

    private void Awake()
    {
        _mapboxAPI = new MapboxGeocodingAPI(_mapboxToken);
        _nominatimAPI = new NominatimAPI();
        _debouncer = new SearchDebouncer(300); // 300ms comme Geoscale
    }

    public async void Search(string query)
    {
        if (query.Length < 3)
        {
            OnSearchResults?.Invoke(new List<AddressResult>());
            return;
        }

        await _debouncer.DebounceAsync(async () =>
        {
            var results = await SearchWithFallbackAsync(query);
            OnSearchResults?.Invoke(results);
        });
    }

    private async Task<List<AddressResult>> SearchWithFallbackAsync(string query)
    {
        // Priorité 1: Mapbox (comme Geoscale)
        try
        {
            var results = await _mapboxAPI.SearchAsync(query);
            if (results.Count > 0) return results;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Mapbox failed, falling back to Nominatim: {e.Message}");
        }

        // Priorité 2: Nominatim fallback (comme Geoscale)
        try
        {
            return await _nominatimAPI.SearchAsync(query);
        }
        catch (Exception e)
        {
            Debug.LogError($"All geocoding APIs failed: {e.Message}");
            OnSearchError?.Invoke("Impossible de rechercher l'adresse");
            return new List<AddressResult>();
        }
    }
}
```

---

## Étape 3 : Sélection de Parcelle

### 3.1 ParcelModel.cs - Modèle de données
```csharp
using System;
using UnityEngine;

[Serializable]
public class ParcelModel
{
    public string Idu;           // Identifiant unique
    public string CodeInsee;     // Code commune
    public string NomCommune;
    public string Section;
    public string Numero;
    public float Surface;        // m²
    public Vector2d Centroid;    // Lat/Lng
    public Bounds BoundingBox;
    public Vector2d[] Geometry;  // Polygone
    public string TypeSource;    // "cadastre_officiel"
    public DateTime DateMaj;
}
```

### 3.2 ParcelDataService.cs - Fetch APIcarto IGN
```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ParcelDataService : MonoBehaviour
{
    // API IGN Cadastre (même que Geoscale)
    private const string APICARTO_PARCELLE = "https://apicarto.ign.fr/api/cadastre/parcelle";
    private const string APICARTO_COMMUNE = "https://apicarto.ign.fr/api/cadastre/commune";

    public event Action<ParcelModel> OnParcelLoaded;
    public event Action<string> OnError;

    public async void FetchParcelAtCoordinates(double lat, double lng)
    {
        try
        {
            // Requête parallèle parcelle + commune (comme Geoscale)
            var parcelTask = FetchParcelAsync(lat, lng);
            var communeTask = FetchCommuneAsync(lat, lng);

            await Task.WhenAll(parcelTask, communeTask);

            var parcel = parcelTask.Result;
            if (parcel != null)
            {
                parcel.NomCommune = communeTask.Result;
                OnParcelLoaded?.Invoke(parcel);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Parcel fetch error: {e.Message}");
            OnError?.Invoke("Impossible de charger la parcelle");
        }
    }

    private async Task<ParcelModel> FetchParcelAsync(double lat, double lng)
    {
        // GeoJSON Point pour la requête
        string geom = $"{{\"type\":\"Point\",\"coordinates\":[{lng},{lat}]}}";
        string url = $"{APICARTO_PARCELLE}?geom={UnityWebRequest.EscapeURL(geom)}";

        using var request = UnityWebRequest.Get(url);
        var operation = request.SendWebRequest();

        while (!operation.isDone)
            await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
            throw new Exception(request.error);

        return ParseParcelResponse(request.downloadHandler.text);
    }

    private async Task<string> FetchCommuneAsync(double lat, double lng)
    {
        string geom = $"{{\"type\":\"Point\",\"coordinates\":[{lng},{lat}]}}";
        string url = $"{APICARTO_COMMUNE}?geom={UnityWebRequest.EscapeURL(geom)}";

        using var request = UnityWebRequest.Get(url);
        var operation = request.SendWebRequest();

        while (!operation.isDone)
            await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
            return null;

        // Parser et retourner nom_com
        return ParseCommuneName(request.downloadHandler.text);
    }

    private ParcelModel ParseParcelResponse(string json)
    {
        // Parser GeoJSON et créer ParcelModel
        // ... parsing logic avec JsonUtility ou Newtonsoft.Json
        return new ParcelModel();
    }

    private string ParseCommuneName(string json)
    {
        // Extraire nom_com du JSON
        return "";
    }
}
```

### 3.3 ParcelSelectionHandler.cs - Gestion interactions
```csharp
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Input;

public class ParcelSelectionHandler : MonoBehaviour, IMixedRealityPointerHandler
{
    [SerializeField] private MapManager _mapManager;
    [SerializeField] private ParcelDataService _parcelService;
    [SerializeField] private ParcelHighlighter _highlighter;

    private ParcelModel _selectedParcel;

    public event System.Action<ParcelModel> OnParcelSelected;

    private void OnEnable()
    {
        _parcelService.OnParcelLoaded += HandleParcelLoaded;
    }

    private void OnDisable()
    {
        _parcelService.OnParcelLoaded -= HandleParcelLoaded;
    }

    // MRTK pointer handler pour HoloLens 2
    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        // Convertir position pointeur en coordonnées carte
        if (TryGetMapCoordinates(eventData.Pointer.Result.Details.Point,
            out double lat, out double lng))
        {
            _parcelService.FetchParcelAtCoordinates(lat, lng);
        }
    }

    private void HandleParcelLoaded(ParcelModel parcel)
    {
        _selectedParcel = parcel;

        // Surligner la parcelle
        _highlighter.HighlightParcel(parcel);

        // Centrer la carte avec zoom auto (pattern Geoscale)
        _mapManager.CenterOnParcel(parcel, duration: 0.8f);

        // Notifier les listeners (UI, etc.)
        OnParcelSelected?.Invoke(parcel);
    }

    private bool TryGetMapCoordinates(Vector3 worldPos, out double lat, out double lng)
    {
        // Conversion position monde Unity -> coordonnées GPS
        // Dépend de la configuration Mapbox Unity SDK
        lat = 0; lng = 0;
        return false; // À implémenter selon le setup
    }

    // Autres méthodes IMixedRealityPointerHandler
    public void OnPointerDown(MixedRealityPointerEventData e) { }
    public void OnPointerUp(MixedRealityPointerEventData e) { }
    public void OnPointerDragged(MixedRealityPointerEventData e) { }
}
```

---

## Étape 4 : Interface MRTK pour HoloLens 2

### 4.1 SearchBarUI.cs - Barre de recherche spatiale
```csharp
using TMPro;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Experimental.UI;

public class SearchBarUI : MonoBehaviour
{
    [SerializeField] private MRTKTMPInputField _inputField;
    [SerializeField] private AddressSearchService _searchService;
    [SerializeField] private SearchResultsPanel _resultsPanel;

    private void OnEnable()
    {
        _inputField.onValueChanged.AddListener(OnInputChanged);
        _searchService.OnSearchResults += OnResultsReceived;
    }

    private void OnDisable()
    {
        _inputField.onValueChanged.RemoveListener(OnInputChanged);
        _searchService.OnSearchResults -= OnResultsReceived;
    }

    private void OnInputChanged(string query)
    {
        // Déclenche recherche avec debounce automatique
        _searchService.Search(query);
    }

    private void OnResultsReceived(List<AddressResult> results)
    {
        _resultsPanel.DisplayResults(results);
    }
}
```

### 4.2 SearchResultsPanel.cs - Affichage résultats
```csharp
using System.Collections.Generic;
using UnityEngine;

public class SearchResultsPanel : MonoBehaviour
{
    [SerializeField] private GameObject _resultItemPrefab;
    [SerializeField] private Transform _resultsContainer;
    [SerializeField] private MapManager _mapManager;

    private List<GameObject> _resultItems = new List<GameObject>();

    public void DisplayResults(List<AddressResult> results)
    {
        ClearResults();

        if (results.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        foreach (var result in results)
        {
            var item = Instantiate(_resultItemPrefab, _resultsContainer);
            var resultUI = item.GetComponent<SearchResultItem>();
            resultUI.Setup(result, OnResultSelected);
            _resultItems.Add(item);
        }
    }

    private void OnResultSelected(AddressResult result)
    {
        // Naviguer vers l'adresse (comme Geoscale handleAddressSelection)
        _mapManager.FlyTo(result.Latitude, result.Longitude, 18f, 2f);

        ClearResults();
        gameObject.SetActive(false);
    }

    private void ClearResults()
    {
        foreach (var item in _resultItems)
            Destroy(item);
        _resultItems.Clear();
    }
}
```

### 4.3 ParcelInfoPanel.cs - Infos parcelle
```csharp
using TMPro;
using UnityEngine;

public class ParcelInfoPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _communeText;
    [SerializeField] private TextMeshProUGUI _sectionText;
    [SerializeField] private TextMeshProUGUI _surfaceText;
    [SerializeField] private ParcelSelectionHandler _selectionHandler;

    private void OnEnable()
    {
        _selectionHandler.OnParcelSelected += DisplayParcelInfo;
    }

    private void OnDisable()
    {
        _selectionHandler.OnParcelSelected -= DisplayParcelInfo;
    }

    private void DisplayParcelInfo(ParcelModel parcel)
    {
        gameObject.SetActive(true);

        _titleText.text = $"Parcelle {parcel.Section} {parcel.Numero}";
        _communeText.text = $"Commune: {parcel.NomCommune} ({parcel.CodeInsee})";
        _sectionText.text = $"Section: {parcel.Section}";
        _surfaceText.text = $"Surface: {parcel.Surface:N0} m²";
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
```

---

## Étape 5 : Configuration et Intégration

### 5.1 Dépendances requises
- Mapbox Unity SDK (via Package Manager ou .unitypackage)
- MRTK 2.x (déjà installé selon votre projet)
- Newtonsoft.Json (pour parsing GeoJSON)
- UniTask (optionnel, pour async/await optimisé)

### 5.2 Configuration Mapbox
1. Créer un compte Mapbox et obtenir un access token
2. Configurer dans `Resources/MapboxConfig.asset` ou via inspector
3. Ajouter `AbstractMap` à la scène

### 5.3 Hiérarchie de scène suggérée
```
Scene
├── MixedRealityToolkit
├── MixedRealityPlayspace
│   └── Main Camera
├── MapContainer
│   ├── AbstractMap (Mapbox)
│   ├── CadastreLayerController
│   └── ParcelHighlighter
├── Services (Empty GameObject)
│   ├── MapManager
│   ├── AddressSearchService
│   ├── ParcelDataService
│   └── ParcelSelectionHandler
└── UI
    ├── SearchBar (Near Menu ou Hand Menu)
    ├── SearchResultsPanel
    └── ParcelInfoPanel
```

---

## Résumé des Patterns Geoscale Adaptés

| Pattern Geoscale | Adaptation Unity |
|------------------|------------------|
| Debounce 300ms (setTimeout/clearTimeout) | `SearchDebouncer` avec CancellationToken |
| Mapbox API + Nominatim fallback | `AddressSearchService.SearchWithFallbackAsync()` |
| flyTo() animation | `MapManager.FlyTo()` avec coroutine |
| Auto-zoom basé sur taille parcelle | `CalculateOptimalZoom()` |
| APIcarto IGN pour cadastre | `ParcelDataService` |
| Requêtes parallèles (parcelle + commune) | `Task.WhenAll()` |
| Event-driven UI updates | C# events (`OnParcelLoaded`, `OnSearchResults`) |

---

## Prochaines Étapes

1. Installer Mapbox Unity SDK dans votre projet
2. Créer la structure de dossiers Scripts/
3. Implémenter les services dans l'ordre : Map → Search → Parcel → UI
4. Configurer les prefabs MRTK pour l'UI spatiale
5. Tester sur HoloLens 2 ou émulateur
