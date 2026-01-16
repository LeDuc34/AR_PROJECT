# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity AR project for HoloLens 2 using Microsoft's Mixed Reality Toolkit (MRTK) v2. The project implements a cadastral (land parcel) mapping application called **GeoscaleCadastre**, adapted from a web-based Geoscale implementation.

## Opening the Project

Open the Unity project from:
```
MixedRealityToolkit-Unity/
```
This is the main Unity project directory containing the `.sln` solution file.

## Architecture

### GeoscaleCadastre Module
Location: `Assets/GeoscaleCadastre/`

The custom module uses a namespace-based organization under `GeoscaleCadastre`:

```
Scripts/
├── CadastralMapController.cs  # Main orchestrator, entry point
├── Map/
│   ├── MapManager.cs          # Mapbox map control (flyTo, zoom)
│   ├── MapInteractionHandler.cs
│   └── ParcelHighlighter.cs   # Visual highlighting of selected parcels
├── Models/
│   ├── AddressResult.cs       # Geocoding result model
│   └── ParcelModel.cs         # Cadastral parcel data model
├── Search/
│   ├── AddressSearchService.cs    # Unified search with fallback
│   ├── MapboxGeocodingAPI.cs      # Primary geocoding
│   ├── NominatimAPI.cs            # Fallback geocoding (OSM)
│   └── SearchDebouncer.cs         # 300ms debounce for input
├── Parcel/
│   ├── ParcelDataService.cs       # Fetch from APIcarto IGN
│   └── ParcelSelectionHandler.cs  # Selection logic
└── UI/
    ├── SearchBarUI.cs
    ├── SearchResultsPanel.cs
    ├── SearchResultItem.cs
    └── ParcelInfoPanel.cs
```

Assembly definition: `GeoscaleCadastre.asmdef` references MRTK assemblies and TextMeshPro.

### Key Dependencies
- MRTK 2.x (Microsoft.MixedReality.Toolkit.*)
- Unity TextMeshPro
- AR Foundation (com.unity.xr.arfoundation)
- Windows MR XR Plugin (com.unity.xr.windowsmr.metro)
- OpenVR XR Plugin

### External APIs Used
- **Mapbox Geocoding API**: Primary address search with autocompletion (France-focused)
- **Nominatim API**: Fallback geocoding (OpenStreetMap)
- **APIcarto IGN**: French cadastral parcel data (`https://apicarto.ign.fr/api/cadastre/`)
- **IGN WMTS**: Cadastral map tiles from data.geopf.fr

## Build Commands

Unity project - use Unity Editor or command line:
```bash
# Build from command line (adjust Unity path as needed)
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -batchmode -projectPath "MixedRealityToolkit-Unity" -buildTarget WSAPlayer -executeMethod BuildScript.PerformBuild -quit
```

For HoloLens 2 deployment, build as UWP (Universal Windows Platform) then deploy via Visual Studio.

## MRTK Configuration

- MRTK profiles are in `Assets/MRTK/SDK/Profiles/`
- Input system supports hand tracking, eye tracking, and voice commands
- Voice commands defined: "Zoom avant", "Zoom arrière", "Sélectionner ici", "Effacer sélection", "Retour à Paris"

## Scene Hierarchy Pattern

```
Scene
├── MixedRealityToolkit
├── MixedRealityPlayspace
│   └── Main Camera
├── MapContainer
│   ├── AbstractMap (Mapbox)
│   ├── MapManager
│   └── ParcelHighlighter
├── Services
│   ├── CadastralMapController
│   ├── AddressSearchService
│   └── ParcelDataService
└── UI
    ├── SearchBar (NearMenu MRTK)
    └── ParcelInfoPanel
```

## Code Patterns

- **Async operations**: Use `UnityWebRequest` with coroutines or async/await
- **Event-driven**: C# events (`Action<T>`) for loose coupling between services
- **Debouncing**: `SearchDebouncer` implements 300ms delay with CancellationToken
- **Fallback pattern**: Mapbox API with Nominatim fallback for geocoding
- **Parallel requests**: `Task.WhenAll()` for fetching parcel + commune data simultaneously

## Testing

MRTK includes test assemblies:
- `Microsoft.MixedReality.Toolkit.Tests.EditModeTests`
- `Microsoft.MixedReality.Toolkit.Tests.PlayModeTests`

Run tests via Unity Test Runner (Window > General > Test Runner).

## Important Notes

- Mapbox access token must be configured in `CadastralMapController` inspector
- Default map center: Paris (48.8566, 2.3522)
- Project targets HoloLens 2 but can be tested in Unity Editor with MRTK input simulation
