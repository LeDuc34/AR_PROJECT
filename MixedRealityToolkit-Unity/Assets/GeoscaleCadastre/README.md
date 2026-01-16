# GeoscaleCadastre - Module AR Unity pour HoloLens 2

Module de carte cadastrale pour applications AR Unity avec MRTK, adapté de la logique Geoscale.

## Fonctionnalités

- **Affichage carte cadastrale** : Intégration Mapbox Unity SDK avec tuiles cadastrales IGN
- **Recherche d'adresse** : Debounce 300ms, Mapbox API avec fallback Nominatim
- **Sélection de parcelle** : Tap/pinch sur carte, récupération données APIcarto IGN
- **Interface MRTK** : Optimisé pour HoloLens 2 (main, regard, voix)

## Structure

```
Scripts/
├── Models/
│   ├── AddressResult.cs      # Modèle résultat recherche
│   └── ParcelModel.cs        # Modèle parcelle cadastrale
├── Search/
│   ├── SearchDebouncer.cs    # Debounce 300ms
│   ├── MapboxGeocodingAPI.cs # API Mapbox
│   ├── NominatimAPI.cs       # API Nominatim (fallback)
│   └── AddressSearchService.cs # Service unifié
├── Parcel/
│   ├── ParcelDataService.cs      # Fetch APIcarto IGN
│   └── ParcelSelectionHandler.cs # Gestion sélection
├── Map/
│   ├── MapManager.cs             # Gestion Mapbox (flyTo, zoom)
│   ├── ParcelHighlighter.cs      # Surlignage parcelle
│   └── MapInteractionHandler.cs  # Interactions MRTK
├── UI/
│   ├── SearchBarUI.cs        # Barre de recherche
│   ├── SearchResultsPanel.cs # Panel résultats
│   ├── SearchResultItem.cs   # Item résultat
│   └── ParcelInfoPanel.cs    # Info parcelle
└── CadastralMapController.cs # Contrôleur principal
```

## Installation

1. **Mapbox Unity SDK** : Importer depuis Package Manager ou .unitypackage
2. **Configuration** : Renseigner le token Mapbox dans `CadastralMapController`
3. **Scène** : Créer une scène avec la hiérarchie recommandée

## Configuration

### Token Mapbox
```
CadastralMapController > Mapbox Access Token
```

### Hiérarchie de scène recommandée
```
Scene
├── MixedRealityToolkit
├── MixedRealityPlayspace
│   └── Main Camera
├── MapContainer
│   ├── AbstractMap (Mapbox)
│   ├── MapManager
│   ├── MapInteractionHandler
│   └── ParcelHighlighter
├── Services
│   ├── CadastralMapController
│   ├── AddressSearchService
│   ├── ParcelDataService
│   └── ParcelSelectionHandler
└── UI
    ├── SearchBar (NearMenu MRTK)
    ├── SearchResultsPanel
    └── ParcelInfoPanel
```

## APIs utilisées

- **Mapbox Geocoding** : Recherche d'adresse avec autocomplétion
- **Nominatim** : Fallback gratuit (OpenStreetMap)
- **APIcarto IGN** : Données cadastrales officielles françaises

## Patterns Geoscale

| Pattern Web | Adaptation Unity |
|-------------|------------------|
| setTimeout/clearTimeout | SearchDebouncer (coroutines) |
| Mapbox GL JS | Mapbox Unity SDK |
| fetch + Promise | UnityWebRequest + coroutines |
| React events | C# Action events |
| flyTo animation | MapManager.FlyTo (coroutines) |

## Commandes vocales MRTK

- "Zoom avant" / "Zoom arrière"
- "Sélectionner ici"
- "Effacer sélection"
- "Retour à Paris"

## License

Usage interne - Adapté de Geoscale
