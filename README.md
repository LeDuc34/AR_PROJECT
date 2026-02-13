# GeoscaleCadastre AR

Application de consultation cadastrale francaise en realite augmentee pour HoloLens 2, adaptee de l'application web Geoscale.

## Presentation

GeoscaleCadastre permet de visualiser et interroger le cadastre francais en realite augmentee. L'utilisateur peut rechercher une adresse, naviguer sur une carte interactive, selectionner une parcelle et consulter ses informations (section, numero, surface, commune), le tout en manipulant la carte avec le hand tracking ou des commandes vocales.

## Stack technique

| Composant | Technologie |
|-----------|-------------|
| Moteur | Unity |
| Plateforme cible | HoloLens 2 (UWP) |
| Framework AR | Microsoft Mixed Reality Toolkit (MRTK) v2 |
| Cartographie | Mapbox Unity SDK |
| Donnees cadastrales | APIcarto IGN |
| Tuiles cadastrales | IGN WMTS (data.geopf.fr) |

## Ouvrir le projet

Le projet Unity se trouve dans le dossier :

```
MixedRealityToolkit-Unity/
```

## Architecture

```
Assets/GeoscaleCadastre/Scripts/
|-- CadastralMapController.cs       # Orchestrateur principal
|-- Map/
|   |-- MapManager.cs               # Controle Mapbox (flyTo, zoom, conversions GPS)
|   |-- MapInteractionHandler.cs    # Interactions MRTK (mains, voix, gaze)
|   +-- ParcelHighlighter.cs        # Surbrillance parcelle (projector + texture)
|-- Models/
|   |-- AddressResult.cs            # Modele resultat geocoding
|   +-- ParcelModel.cs              # Modele parcelle cadastrale
|-- Search/
|   |-- AddressSearchService.cs     # Service unifie avec fallback
|   |-- MapboxGeocodingAPI.cs       # Geocoding Mapbox v5
|   |-- NominatimAPI.cs             # Fallback OSM Nominatim
|   +-- SearchDebouncer.cs          # Debounce 300ms
|-- Parcel/
|   |-- ParcelDataService.cs        # Client APIcarto IGN
|   +-- ParcelSelectionHandler.cs   # Logique de selection
|-- UI/
|   |-- SearchBarUI.cs              # Barre de recherche MRTK
|   |-- SearchResultsPanel.cs       # Panel resultats
|   |-- SearchResultItem.cs         # Item individuel
|   |-- ParcelInfoPanel.cs          # Fiche parcelle (construit au runtime)
|   +-- SelectedParcelOverlay.cs    # Overlay compact coin d'ecran
+-- Debug/
    +-- MRTKInputDebugger.cs        # Traceur d'evenements MRTK
```

## APIs externes

| API | Role | Fallback |
|-----|------|----------|
| Mapbox Geocoding v5 | Recherche d'adresse (autocompletion, France) | Nominatim |
| Nominatim (OSM) | Geocoding de secours | -- |
| APIcarto IGN | Donnees cadastrales GeoJSON | -- |
| IGN WMTS | Tuiles cartographiques cadastrales | -- |

## Configuration

### Token Mapbox

Renseigner le token Mapbox dans l'Inspector sur le composant `CadastralMapController` :

```
CadastralMapController > Mapbox Access Token
```

### Hierarchie de scene

```
Scene
|-- MixedRealityToolkit
|-- MixedRealityPlayspace
|   +-- Main Camera
|-- MapContainer
|   |-- AbstractMap (Mapbox)
|   |-- MapManager
|   |-- MapInteractionHandler
|   +-- ParcelHighlighter
|-- Services
|   |-- CadastralMapController
|   |-- AddressSearchService
|   |-- ParcelDataService
|   +-- ParcelSelectionHandler
+-- UI
    |-- SearchBar (NearMenu MRTK)
    |-- SearchResultsPanel
    +-- ParcelInfoPanel
```

## Commandes vocales

| Commande | Action |
|----------|--------|
| "Zoom avant" | Augmente le niveau de zoom |
| "Zoom arriere" | Diminue le niveau de zoom |
| "Selectionner ici" | Selectionne la parcelle sous le regard |
| "Effacer selection" | Deselectionne la parcelle active |
| "Retour a Paris" | Recentre la carte sur Paris |

## Flux de donnees

```
Saisie adresse
    -> SearchBarUI
    -> SearchDebouncer (300ms)
    -> MapboxGeocodingAPI  --echec-->  NominatimAPI
    -> Liste de resultats (SearchResultsPanel)
    -> Selection d'un resultat
    -> MapManager.CenterOnAddress() + animation FlyTo

Tap sur la carte
    -> MapInteractionHandler (discrimination tap/drag : 500ms + 15cm)
    -> ParcelSelectionHandler
    -> ParcelDataService -> APIcarto IGN (parcelle + commune en parallele)
    -> ParcelModel
        -> ParcelHighlighter (projection visuelle)
        -> ParcelInfoPanel (fiche detail)
        -> SelectedParcelOverlay (overlay compact)
        -> MapManager.CenterOnParcel() (recadrage)
```

## Patterns

| Pattern | Implementation |
|---------|----------------|
| Event-driven | Evenements C# `Action<T>` entre services |
| Fallback | Mapbox -> Nominatim pour le geocoding |
| Debounce | `SearchDebouncer` avec CancellationToken |
| Requetes paralleles | `Task.WhenAll()` pour parcelle + commune |
| Discrimination tap/drag | Seuil 500ms + 15cm de deplacement |

## Build et deploiement

Build en ligne de commande (adapter le chemin Unity) :

```bash
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -batchmode -projectPath "MixedRealityToolkit-Unity" -buildTarget WSAPlayer -executeMethod BuildScript.PerformBuild -quit
```

Le build produit une solution UWP a deployer sur HoloLens 2 via Visual Studio.

Le projet peut aussi etre teste directement dans l'editeur Unity grace au simulateur d'entrees MRTK.

## Tests

Les tests MRTK sont disponibles via le Unity Test Runner (Window > General > Test Runner) :

- `Microsoft.MixedReality.Toolkit.Tests.EditModeTests`
- `Microsoft.MixedReality.Toolkit.Tests.PlayModeTests`
