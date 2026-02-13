# Post-Mortem : GeoscaleCadastre AR

## Application cadastrale en réalité augmentée pour HoloLens 2

| | |
|---|---|
| **Période** | 16 janvier 2026 – 13 février 2026 (~4 semaines) |
| **Plateforme cible** | Microsoft HoloLens 2 (UWP) |
| **Stack technique** | Unity 2020 · MRTK v2 · Mapbox SDK |
| **Code applicatif** | 17 fichiers C#, ~5 100 lignes |
| **Versionnement** | Git, 9 commits |

---

## 1. Contexte et objectif

Le projet **GeoscaleCadastre AR** s'inscrit dans le cadre d'une adaptation d'une application web existante — **Geoscale** — vers un environnement de réalité augmentée. Geoscale permet la consultation du cadastre français (recherche d'adresse, visualisation de parcelles, affichage d'informations foncières). L'objectif était de transposer ces fonctionnalités sur le casque HoloLens 2 en exploitant les interactions naturelles offertes par la réalité augmentée :

- Recherche d'adresse via une barre de saisie en réalité augmentée
- Navigation sur une carte Mapbox interactive positionnée dans l'espace
- Sélection d'une parcelle cadastrale par geste (tap) ou commande vocale
- Consultation des informations associées (section, numéro, surface, commune)
- Manipulation de la carte (déplacement, zoom, rotation) par hand tracking

---

## 2. Architecture logicielle

L'application suit une architecture **événementielle (event-driven)** organisée en modules faiblement couplés. Un orchestrateur central (`CadastralMapController`) coordonne les interactions entre les services sans porter de logique métier.

```
GeoscaleCadastre/
├── Scripts/
│   ├── CadastralMapController.cs    # Orchestrateur principal
│   ├── Map/
│   │   ├── MapManager.cs            # Contrôle Mapbox (flyTo, zoom, conversions GPS)
│   │   ├── MapInteractionHandler.cs # Interactions MRTK (mains, voix, gaze)
│   │   └── ParcelHighlighter.cs     # Surbrillance parcelle (projector + texture)
│   ├── Models/
│   │   ├── AddressResult.cs         # Modèle résultat géocodage
│   │   └── ParcelModel.cs           # Modèle parcelle cadastrale
│   ├── Search/
│   │   ├── AddressSearchService.cs  # Service unifié avec fallback
│   │   ├── MapboxGeocodingAPI.cs    # Géocodage Mapbox v5
│   │   ├── NominatimAPI.cs          # Fallback OSM Nominatim
│   │   └── SearchDebouncer.cs       # Anti-rebond 300 ms
│   ├── Parcel/
│   │   ├── ParcelDataService.cs     # Client APIcarto IGN
│   │   └── ParcelSelectionHandler.cs# Logique de sélection
│   ├── UI/
│   │   ├── SearchBarUI.cs           # Barre de recherche MRTK
│   │   ├── SearchResultsPanel.cs    # Panneau de résultats
│   │   ├── SearchResultItem.cs      # Élément individuel
│   │   └── ParcelInfoPanel.cs       # Fiche parcelle (construite au runtime)
│   └── Debug/
│       └── MRTKInputDebugger.cs     # Traceur d'événements MRTK
└── Shaders/
    └── ProjectorHighlight.shader    # Shader de projection parcelle
```

### APIs externes intégrées

| API | Rôle | Fallback |
|-----|------|----------|
| **Mapbox Geocoding v5** | Recherche d'adresse (autocomplétion, France) | Nominatim |
| **Nominatim (OSM)** | Géocodage de secours | Aucun |
| **APIcarto IGN** | Données cadastrales GeoJSON | Aucun |
| **IGN WMTS** | Tuiles cartographiques cadastrales | Aucun |

---

## 3. Réussites techniques

### Architecture événementielle

Le découpage en services faiblement couplés via des événements C# (`Action<T>`) s'est révélé efficace. Chaque module (Search, Parcel, Map, UI) peut évoluer indépendamment. Le `CadastralMapController` joue un rôle clair d'orchestrateur sans concentrer la logique métier, conformément au principe de responsabilité unique.

### Stratégie de fallback sur le géocodage

La chaîne Mapbox → Nominatim fonctionne de manière transparente. En cas d'échec ou d'absence de résultat de l'API Mapbox, le basculement vers Nominatim est automatique et invisible pour l'utilisateur, garantissant une disponibilité accrue du service de recherche.

### Discrimination tap / drag

Le hand tracking en réalité augmentée rend difficile la distinction entre un geste de sélection (tap) et un geste de déplacement (drag). Le système implémenté utilise un double seuil — temporel (500 ms) et spatial (15 cm) — pour classifier l'intention de l'utilisateur. Cette solution s'est avérée simple et robuste.

### Surbrillance procédurale des parcelles

Le `ParcelHighlighter` génère dynamiquement une texture de surbrillance à partir du polygone GPS de la parcelle, projetée via un composant `Projector` de Unity. Un algorithme de simplification Douglas-Peucker réduit le nombre de points pour les grandes parcelles. Cette approche procédurale évite de dépendre d'un système de rendu géographique supplémentaire.

### Commandes vocales

L'intégration des commandes vocales MRTK (« Zoom avant », « Zoom arrière », « Sélectionner ici », « Effacer sélection », « Retour à Paris ») offre une interaction mains-libres complète, particulièrement adaptée à un usage terrain.

### Requêtes parallèles

Le `ParcelDataService` lance les requêtes parcelle et commune en parallèle via `Task.WhenAll()`, réduisant significativement le temps de réponse perçu par l'utilisateur.

---

## 4. Difficultés rencontrées et leçons tirées

### Parsing GeoJSON manuel

`JsonUtility` de Unity ne gère pas les tableaux à profondeur variable (Polygon = 3 niveaux, MultiPolygon = 4 niveaux). Le parsing manuel par comptage de crochets dans `ParcelDataService` représente environ 150 lignes fragiles et difficiles à maintenir. Un JSON malformé peut provoquer un échec silencieux.

**Leçon apprise** : L'utilisation d'un parser JSON robuste (Newtonsoft Json.NET, disponible pour Unity) dès le début du projet aurait évité cette complexité accidentelle.

### Intégration Mapbox par réflexion .NET

Le `MapManager` utilise la réflexion .NET pour invoquer les méthodes du SDK Mapbox (recherche de types dans les assemblies, invocation dynamique). Cela représente plus de 150 lignes de code verbeux, fragile face aux mises à jour du SDK, et introduisant un surcoût à l'exécution.

**Leçon apprise** : Ajouter une référence directe à l'assembly Mapbox dans le fichier `.asmdef` aurait simplifié considérablement le code. La réflexion n'était pas justifiée dans ce contexte.

### Instabilité du simulateur MRTK en éditeur

Le simulateur de mains MRTK introduit du bruit dans les positions lors du drag, provoquant des sauts de carte. La solution adoptée (seuil de magnitude > 0.0005f combiné à un multiplicateur ×5) est un correctif empirique fonctionnel mais sensible aux conditions d'utilisation.

**Leçon apprise** : Tester régulièrement sur le dispositif réel. Le simulateur de l'éditeur Unity ne reflète pas fidèlement le comportement du tracking du HoloLens 2.

### Absence de cache réseau

Chaque recherche d'adresse et chaque sélection de parcelle déclenchent des requêtes réseau. Aucun mécanisme de cache en mémoire ni de persistance locale n'a été implémenté. Sur un réseau à faible débit (contexte terrain), l'expérience utilisateur se dégrade sensiblement.

**Leçon apprise** : Implémenter au minimum un cache en mémoire avec durée de vie (TTL) pour les résultats récents.

### Interface utilisateur construite entièrement au runtime

Le `ParcelInfoPanel` crée un Canvas complet, des RectTransforms et des composants TextMeshPro par code (~295 lignes). Cette approche est fonctionnelle mais pénible à maintenir et à déboguer : tout changement visuel nécessite de modifier du code C# plutôt que de manipuler un prefab dans l'éditeur.

**Leçon apprise** : Privilégier les prefabs pour l'interface utilisateur, même si leur création initiale dans l'éditeur Unity demande plus de temps. Le gain en maintenabilité est significatif.

---

## 5. Dette technique identifiée

| Problème | Sévérité | Remédiation envisagée |
|----------|----------|----------------------|
| Parsing JSON manuel (comptage de crochets) | Haute | Remplacement par Newtonsoft Json.NET |
| Intégration Mapbox par réflexion | Haute | Refactoring vers une référence d'assembly directe |
| Absence de cache réseau | Moyenne | Cache `Dictionary<string, T>` avec TTL |
| ~100 appels `Debug.Log` en production | Faible | Conditionnement via `#if UNITY_EDITOR` ou suppression |
| Valeurs seuils codées en dur (timeouts, sensibilité) | Faible | Extraction en `[SerializeField]` ou `ScriptableObject` |
| Absence de validation des entrées utilisateur | Moyenne | Sanitisation avant les appels API |
| Absence de gestion du mode hors-ligne | Moyenne | Détection réseau + messages d'erreur adaptés |
| ParcelInfoPanel construit en code pur | Faible | Migration vers un prefab Unity |

---

## 6. Chronologie du développement

```
16 jan    Initialisation du projet
          → Setup MRTK + Mapbox, structure de base, première carte fonctionnelle

16 jan    Itérations rapides (3 commits)
          → Carte interactive, améliorations de navigation

22 jan    Correction : freeze lorsque l'APIcarto ne retourne aucune parcelle
          → Premier cas limite critique résolu

23 jan    Highlighting des parcelles opérationnel
          → Surbrillance visuelle + interface de sélection

23 jan    Réglages de qualité graphique
          → Optimisation des Quality Settings Unity

12 fév    Refactoring majeur
          → Rework du système de drag (discrimination tap/drag)
          → Construction du ParcelInfoPanel au runtime

13 fév    Nettoyage et finalisation
          → Suppression du composant SelectedParcelOverlay (redondant)
          → Suppression des assets MRTK Examples et GoogleARCore inutilisés
          → Simplification de la scène
```

---

## 7. Métriques du projet

| Métrique | Valeur |
|----------|--------|
| Fichiers C# | 17 |
| Lignes de code | ~5 100 |
| Shaders custom | 1 |
| APIs externes intégrées | 4 |
| Commandes vocales | 5 |
| Événements custom (`Action<T>`) | 20+ |
| Commits | 9 |
| Durée de développement | ~4 semaines |

### Répartition du code par module

| Module | Lignes | Part |
|--------|--------|------|
| Map (Manager, Interaction, Highlighter) | 1 847 | 36 % |
| Parcel (DataService, Selection) | 962 | 19 % |
| Search (Service, APIs, Debouncer) | 590 | 12 % |
| UI (SearchBar, Results, InfoPanel) | 1 097 | 21 % |
| Models | 135 | 3 % |
| Controller (orchestrateur) | 328 | 6 % |
| Debug | 135 | 3 % |
| **Total** | **5 094** | **100 %** |

Le module **Map** représente 36 % du code, reflétant la complexité de l'intégration entre le SDK Mapbox et le système d'interaction MRTK.

---

## 8. Flux de données principal

```
Utilisateur saisit une adresse
        │
        ▼
   SearchBarUI (TMP_InputField)
        │
        ▼
   SearchDebouncer (300 ms)
        │
        ▼
   MapboxGeocodingAPI ──échec──► NominatimAPI
        │                              │
        ▼                              ▼
   List<AddressResult> ◄──────────────┘
        │
        ▼
   SearchResultsPanel (affiche 5 résultats)
        │
        ▼ (sélection utilisateur)
   MapManager.CenterOnAddress() ──► Animation FlyTo
        │
        ▼ (tap sur la carte)
   MapInteractionHandler
        │
        ▼
   ParcelSelectionHandler
        │
        ▼
   ParcelDataService ──► APIcarto IGN (parcelle + commune en parallèle)
        │
        ▼
   ParcelModel
        │
        ├──► ParcelHighlighter (projection visuelle sur la carte)
        ├──► ParcelInfoPanel (fiche détaillée)
        └──► MapManager.CenterOnParcel() (recadrage automatique)
```

---

## 9. Pistes d'amélioration

### Court terme

1. **Remplacer le parsing JSON manuel** par Newtonsoft Json.NET (package Unity officiel disponible)
2. **Supprimer la réflexion Mapbox** en ajoutant la référence d'assembly directe dans le fichier `.asmdef`
3. **Ajouter un cache mémoire** sur les résultats de recherche et les parcelles déjà consultées
4. **Nettoyer les appels `Debug.Log`** ou les conditionner derrière une directive de compilation

### Moyen terme

5. **Migrer le ParcelInfoPanel** vers un prefab éditable dans l'éditeur Unity
6. **Ajouter une gestion hors-ligne** (détection réseau, messages d'erreur, cache persistant)
7. **Écrire des tests unitaires** pour le parsing GeoJSON et les conversions de coordonnées
8. **Externaliser la configuration** (timeouts, sensibilité du drag, niveaux de zoom) dans un `ScriptableObject`

### Long terme

9. **Tester et optimiser sur HoloLens 2 réel** (performances, ergonomie, lisibilité de la carte en conditions réelles)
10. **Envisager la migration vers MRTK 3** si le projet se poursuit (architecture modernisée, support OpenXR natif)

---

## 10. Conclusion

Le projet a atteint son objectif principal : transposer une application de consultation cadastrale web vers un environnement de réalité augmentée HoloLens 2 en quatre semaines de développement. L'architecture événementielle et le découpage modulaire assurent une bonne évolutivité du code. Les principaux défis techniques — interaction en réalité augmentée, conversion de coordonnées GPS vers l'espace Unity, intégration de SDK tiers — ont été résolus avec des solutions fonctionnelles, parfois au prix de compromis techniques (réflexion .NET, parsing manuel) qui constituent l'essentiel de la dette technique identifiée. Les fondations architecturales sont solides et permettraient une itération future sur la base du travail réalisé.
