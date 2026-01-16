using UnityEngine;
using GeoscaleCadastre.Parcel;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.UI.BoundsControl;
using Microsoft.MixedReality.Toolkit.UI.BoundsControlTypes;

namespace GeoscaleCadastre.Map
{
    /// <summary>
    /// Gère les interactions MRTK avec la carte
    /// Implémente les handlers de pointeur pour HoloLens 2
    /// Compatible avec les interactions main, regard et voix
    /// Supporte la navigation par pincement direct de la carte
    /// </summary>
    public class MapInteractionHandler : MonoBehaviour,
        IMixedRealityPointerHandler,
        IMixedRealityFocusHandler,
        IMixedRealityTouchHandler
    {
        [Header("Références")]
        [SerializeField]
        private ParcelSelectionHandler _parcelSelectionHandler;

        [SerializeField]
        private MapManager _mapManager;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Active les interactions de sélection")]
        private bool _enableSelection = true;

        [SerializeField]
        [Tooltip("Active le zoom par geste")]
        private bool _enableZoom = true;

        [SerializeField]
        [Tooltip("Active le pan géographique par geste (glisser sur le globe)")]
        private bool _enablePan = true;

        [SerializeField]
        [Tooltip("Sensibilité du zoom")]
        private float _zoomSensitivity = 1f;

        [SerializeField]
        [Tooltip("Sensibilité du pan")]
        private float _panSensitivity = 0.001f;

        // État pour les interactions pointeur
        private bool _isManipulating;
        private bool _isFocused;
        private Vector3 _lastPointerPosition;
        private Vector3 _pointerDownPosition;
        private float _pointerDownTime;
        private const float TAP_TIME_THRESHOLD = 0.3f; // 300ms max pour un tap
        private const float TAP_DISTANCE_THRESHOLD = 0.05f; // 5cm max de mouvement

        // État pour les interactions tactiles (pincement)
        private Vector3 _lastTouchPosition;
        private bool _isTouching;

        /// <summary>Indique si la carte a le focus</summary>
        public bool IsFocused { get { return _isFocused; } }

        [Header("Manipulation Physique (AR)")]
        [SerializeField]
        [Tooltip("Active ObjectManipulator pour déplacer/tourner/scaler la carte comme objet 3D")]
        private bool _enableObjectManipulator = false;

        [SerializeField]
        [Tooltip("Active BoundsControl pour afficher les poignées de manipulation")]
        private bool _enableBoundsControl = false;

        [SerializeField]
        [Tooltip("Échelle minimum lors du pinch-to-scale")]
        private float _minScale = 0.1f;

        [SerializeField]
        [Tooltip("Échelle maximum lors du pinch-to-scale")]
        private float _maxScale = 3f;

        private ObjectManipulator _objectManipulator;
        private BoundsControl _boundsControl;

        [Header("Collider Auto-Configuration")]
        [SerializeField]
        [Tooltip("GameObject contenant la carte (pour calculer la taille du collider)")]
        private GameObject _mapRoot;

        [SerializeField]
        [Tooltip("Marge supplémentaire autour du collider (en unités Unity)")]
        private float _colliderPadding = 0.02f;

        [SerializeField]
        [Tooltip("Épaisseur minimale du collider pour les interactions")]
        private float _colliderMinHeight = 0.05f;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Utiliser le mode handler global (contournement si les événements ne sont pas reçus)")]
        private bool _useGlobalHandler = true;

        [SerializeField]
        [Tooltip("Désactiver la vérification IsPointOnMap pour debug (accepter tous les clics)")]
        private bool _bypassMapCheck = false;

        [SerializeField]
        [Tooltip("Tolérance en mètres pour IsPointOnMap")]
        private float _mapCheckTolerance = 0.5f;

        #region Unity Lifecycle

        private void Awake()
        {
            SetupCollider();
            SetupNearInteraction();
            SetupObjectManipulator();
        }

        private void OnEnable()
        {
            if (_useGlobalHandler)
            {
                // S'enregistrer comme handler global pour recevoir TOUS les événements
                CoreServices.InputSystem?.RegisterHandler<IMixedRealityPointerHandler>(this);
                Debug.Log("[MapInteractionHandler] Enregistré comme HANDLER GLOBAL pour les pointeurs");
            }
        }

        private void OnDisable()
        {
            if (_useGlobalHandler)
            {
                CoreServices.InputSystem?.UnregisterHandler<IMixedRealityPointerHandler>(this);
                Debug.Log("[MapInteractionHandler] Désenregistré du handler global");
            }
        }

        private float _debugLogTimer = 0f;
        private const float DEBUG_LOG_INTERVAL = 5f; // Log toutes les 5 secondes

        private void Start()
        {
            // Recalculer le collider après l'initialisation de la carte
            // (les tiles Mapbox peuvent ne pas être chargées dans Awake)
            Invoke("RefreshColliderSize", 0.5f);

            // === DEBUG: Vérification des références au démarrage ===
            Debug.Log("=== [MapInteractionHandler] DEBUG START ===");
            Debug.Log(string.Format("[MapInteractionHandler] GameObject: {0}", gameObject.name));
            Debug.Log(string.Format("[MapInteractionHandler] _parcelSelectionHandler: {0}", _parcelSelectionHandler != null ? "OK" : "NULL"));
            Debug.Log(string.Format("[MapInteractionHandler] _mapManager: {0}", _mapManager != null ? "OK" : "NULL"));
            Debug.Log(string.Format("[MapInteractionHandler] _enableSelection: {0}", _enableSelection));
            Debug.Log(string.Format("[MapInteractionHandler] _enablePan: {0}", _enablePan));
            Debug.Log(string.Format("[MapInteractionHandler] _enableZoom: {0}", _enableZoom));

            // Vérifier le collider
            var collider = GetComponent<Collider>();
            Debug.Log(string.Format("[MapInteractionHandler] Collider: {0}", collider != null ? collider.GetType().Name + " enabled=" + collider.enabled : "NULL"));

            // Vérifier NearInteractionGrabbable
            var nearGrab = GetComponent<NearInteractionGrabbable>();
            Debug.Log(string.Format("[MapInteractionHandler] NearInteractionGrabbable: {0}", nearGrab != null ? "OK" : "NULL"));

            // Vérifier si MRTK Input System est actif
            var inputSystem = CoreServices.InputSystem;
            Debug.Log(string.Format("[MapInteractionHandler] MRTK InputSystem: {0}", inputSystem != null ? "OK" : "NULL"));

            // Vérifier le GazeProvider
            var gazeProvider = inputSystem?.GazeProvider;
            Debug.Log(string.Format("[MapInteractionHandler] GazeProvider: {0}", gazeProvider != null ? "OK" : "NULL"));

            // Vérifier le layer
            Debug.Log(string.Format("[MapInteractionHandler] Layer: {0} ({1})", gameObject.layer, LayerMask.LayerToName(gameObject.layer)));

            // Vérifier si on est bien enregistré pour les événements
            Debug.Log(string.Format("[MapInteractionHandler] Interfaces implémentées: IMixedRealityPointerHandler={0}, IMixedRealityFocusHandler={1}",
                this is IMixedRealityPointerHandler,
                this is IMixedRealityFocusHandler));

            // Debug settings
            Debug.Log(string.Format("[MapInteractionHandler] DEBUG SETTINGS: _useGlobalHandler={0}, _bypassMapCheck={1}, _mapCheckTolerance={2}m",
                _useGlobalHandler, _bypassMapCheck, _mapCheckTolerance));

            // Log collider bounds in world space
            var col = GetComponent<Collider>();
            if (col != null)
            {
                Bounds worldBounds = col.bounds;
                Debug.Log(string.Format("[MapInteractionHandler] Collider WORLD bounds: center={0}, size={1}",
                    worldBounds.center, worldBounds.size));
                Debug.Log(string.Format("[MapInteractionHandler] Collider WORLD bounds: min={0}, max={1}",
                    worldBounds.min, worldBounds.max));
            }

            Debug.Log("=== [MapInteractionHandler] DEBUG END ===");
        }

        private void Update()
        {
            // Log périodique de l'état MRTK pour debug
            _debugLogTimer += Time.deltaTime;
            if (_debugLogTimer >= DEBUG_LOG_INTERVAL)
            {
                _debugLogTimer = 0f;

                var gazeProvider = CoreServices.InputSystem?.GazeProvider;
                string gazeTarget = "NULL";
                string gazePosition = "N/A";

                if (gazeProvider != null)
                {
                    gazeTarget = gazeProvider.GazeTarget != null ? gazeProvider.GazeTarget.name : "None";
                    gazePosition = gazeProvider.HitPosition.ToString();
                }

                Debug.Log(string.Format("[MapInteractionHandler] ÉTAT PÉRIODIQUE - isFocused: {0}, isManipulating: {1}, isTouching: {2}, GazeTarget: {3}, GazePos: {4}",
                    _isFocused, _isManipulating, _isTouching, gazeTarget, gazePosition));
            }
        }

        /// <summary>
        /// Configure le BoxCollider avec une taille adaptée à la carte
        /// </summary>
        private void SetupCollider()
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();
                Debug.Log("[MapInteractionHandler] BoxCollider ajouté automatiquement");
            }

            // Calculer la taille basée sur les renderers
            Bounds bounds = CalculateMapBounds();

            if (bounds.size != Vector3.zero)
            {
                // Convertir les bounds du monde vers l'espace local
                boxCollider.center = transform.InverseTransformPoint(bounds.center);
                Vector3 localSize = transform.InverseTransformVector(bounds.size);

                // S'assurer que les dimensions sont positives
                localSize = new Vector3(
                    Mathf.Abs(localSize.x) + _colliderPadding * 2,
                    Mathf.Max(Mathf.Abs(localSize.y), _colliderMinHeight),
                    Mathf.Abs(localSize.z) + _colliderPadding * 2
                );

                boxCollider.size = localSize;

                Debug.Log(string.Format("[MapInteractionHandler] Collider adapté à la carte - Center: {0}, Size: {1}",
                    boxCollider.center, boxCollider.size));
            }
            else
            {
                // Taille par défaut si pas de renderers trouvés
                boxCollider.size = new Vector3(1f, _colliderMinHeight, 1f);
                Debug.LogWarning("[MapInteractionHandler] Aucun renderer trouvé, taille par défaut utilisée");
            }
        }

        /// <summary>
        /// Ajoute NearInteractionGrabbable pour le pincement direct
        /// </summary>
        private void SetupNearInteraction()
        {
            if (GetComponent<NearInteractionGrabbable>() == null)
            {
                gameObject.AddComponent<NearInteractionGrabbable>();
                Debug.Log("[MapInteractionHandler] NearInteractionGrabbable ajouté pour le pincement");
            }
        }

        /// <summary>
        /// Configure ObjectManipulator et BoundsControl pour manipulation physique de la carte
        /// Permet de déplacer, tourner et scaler la carte comme un objet 3D dans l'espace AR
        /// </summary>
        private void SetupObjectManipulator()
        {
            if (!_enableObjectManipulator) return;

            // Ajouter ObjectManipulator s'il n'existe pas
            _objectManipulator = GetComponent<ObjectManipulator>();
            if (_objectManipulator == null)
            {
                _objectManipulator = gameObject.AddComponent<ObjectManipulator>();
                Debug.Log("[MapInteractionHandler] ObjectManipulator ajouté pour manipulation AR");
            }

            // Configurer les contraintes
            _objectManipulator.TwoHandedManipulationType =
                TransformFlags.Move | TransformFlags.Rotate | TransformFlags.Scale;
            _objectManipulator.OneHandRotationModeNear = ObjectManipulator.RotateInOneHandType.RotateAboutGrabPoint;
            _objectManipulator.OneHandRotationModeFar = ObjectManipulator.RotateInOneHandType.RotateAboutGrabPoint;

            // Ajouter MinMaxScaleConstraint pour limiter le scale
            var scaleConstraint = GetComponent<MinMaxScaleConstraint>();
            if (scaleConstraint == null)
            {
                scaleConstraint = gameObject.AddComponent<MinMaxScaleConstraint>();
            }
            scaleConstraint.ScaleMinimum = _minScale;
            scaleConstraint.ScaleMaximum = _maxScale;

            // Ajouter BoundsControl pour les poignées visuelles
            if (_enableBoundsControl)
            {
                _boundsControl = GetComponent<BoundsControl>();
                if (_boundsControl == null)
                {
                    _boundsControl = gameObject.AddComponent<BoundsControl>();
                    Debug.Log("[MapInteractionHandler] BoundsControl ajouté pour poignées de manipulation");
                }

                // Configuration du BoundsControl pour une carte (principalement rotation Y et scale uniforme)
                _boundsControl.BoundsControlActivation = BoundsControlActivationType.ActivateOnStart;
                _boundsControl.ScaleHandlesConfig.ShowScaleHandles = true;
                _boundsControl.RotationHandlesConfig.ShowHandleForX = false;
                _boundsControl.RotationHandlesConfig.ShowHandleForY = true;
                _boundsControl.RotationHandlesConfig.ShowHandleForZ = false;
            }
        }

        /// <summary>
        /// Calcule les bounds combinés de tous les renderers de la carte
        /// </summary>
        private Bounds CalculateMapBounds()
        {
            // Utiliser le mapRoot s'il est défini, sinon chercher dans les enfants
            GameObject root = _mapRoot != null ? _mapRoot : gameObject;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                Debug.LogWarning("[MapInteractionHandler] Aucun Renderer trouvé dans la hiérarchie");
                return new Bounds(transform.position, Vector3.zero);
            }

            // Combiner les bounds de tous les renderers
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            Debug.Log(string.Format("[MapInteractionHandler] Bounds calculés: center={0}, size={1} ({2} renderers)",
                combinedBounds.center, combinedBounds.size, renderers.Length));

            return combinedBounds;
        }

        /// <summary>
        /// Recalcule la taille du collider (utile après chargement des tiles)
        /// Appelable depuis l'Inspector ou par script
        /// </summary>
        public void RefreshColliderSize()
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null) return;

            Bounds bounds = CalculateMapBounds();

            if (bounds.size != Vector3.zero)
            {
                boxCollider.center = transform.InverseTransformPoint(bounds.center);
                Vector3 localSize = transform.InverseTransformVector(bounds.size);

                localSize = new Vector3(
                    Mathf.Abs(localSize.x) + _colliderPadding * 2,
                    Mathf.Max(Mathf.Abs(localSize.y), _colliderMinHeight),
                    Mathf.Abs(localSize.z) + _colliderPadding * 2
                );

                boxCollider.size = localSize;

                Debug.Log(string.Format("[MapInteractionHandler] Collider mis à jour - Size: {0}", localSize));
            }
        }

        #endregion

        #region IMixedRealityPointerHandler Implementation

        /// <summary>
        /// Vérifie si le point touche notre carte (via raycast sur notre collider)
        /// </summary>
        private bool IsPointOnMap(Vector3 point, out Vector3 hitPoint)
        {
            hitPoint = point;

            // Vérifier via le collider
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                // Debug: afficher les bounds en world space
                Bounds worldBounds = col.bounds;
                Debug.Log(string.Format("[MapInteractionHandler] IsPointOnMap DEBUG - InputPoint: {0}", point));
                Debug.Log(string.Format("[MapInteractionHandler] IsPointOnMap DEBUG - Collider WorldBounds: center={0}, size={1}, min={2}, max={3}",
                    worldBounds.center, worldBounds.size, worldBounds.min, worldBounds.max));

                // Le point est-il proche de notre collider ?
                Vector3 closestPoint = col.ClosestPoint(point);
                float distance = Vector3.Distance(point, closestPoint);

                Debug.Log(string.Format("[MapInteractionHandler] IsPointOnMap DEBUG - ClosestPoint: {0}, Distance: {1:F4}m",
                    closestPoint, distance));

                // Vérifier aussi si le point est dans les bounds (alternative)
                bool isInsideBounds = worldBounds.Contains(point);
                Debug.Log(string.Format("[MapInteractionHandler] IsPointOnMap DEBUG - IsInsideBounds: {0}", isInsideBounds));

                // Utiliser la tolérance configurable
                if (distance < _mapCheckTolerance)
                {
                    hitPoint = closestPoint;
                    Debug.Log(string.Format("[MapInteractionHandler] IsPointOnMap RESULT: TRUE (distance {0:F4} < tolerance {1})", distance, _mapCheckTolerance));
                    return true;
                }
                else
                {
                    Debug.Log(string.Format("[MapInteractionHandler] IsPointOnMap RESULT: FALSE (distance {0:F4} >= tolerance {1})", distance, _mapCheckTolerance));
                }
            }
            else
            {
                Debug.LogWarning("[MapInteractionHandler] IsPointOnMap - No collider found!");
            }

            return false;
        }

        /// <summary>
        /// Vérifie si le pointer cible notre objet (directement ou via hiérarchie)
        /// </summary>
        private bool IsPointerTargetingUs(MixedRealityPointerEventData eventData)
        {
            var target = eventData.Pointer.Result?.CurrentPointerTarget;

            // Cible directe
            if (target == gameObject)
            {
                Debug.Log("[MapInteractionHandler] IsPointerTargetingUs: TRUE (target == this)");
                return true;
            }

            // Cible est un enfant
            if (target != null && target.transform.IsChildOf(transform))
            {
                Debug.Log(string.Format("[MapInteractionHandler] IsPointerTargetingUs: TRUE (target {0} is child)", target.name));
                return true;
            }

            // Mode bypass: accepter tous les clics si le pointer a un résultat valide
            if (_bypassMapCheck && eventData.Pointer.Result != null)
            {
                Debug.Log("[MapInteractionHandler] IsPointerTargetingUs: TRUE (bypass mode enabled)");
                return true;
            }

            // En mode global, vérifier si le point est sur notre collider
            if (_useGlobalHandler && eventData.Pointer.Result != null)
            {
                Vector3 hitPoint;
                bool isOnMap = IsPointOnMap(eventData.Pointer.Result.Details.Point, out hitPoint);
                Debug.Log(string.Format("[MapInteractionHandler] IsPointerTargetingUs via IsPointOnMap: {0}", isOnMap));
                return isOnMap;
            }

            Debug.Log("[MapInteractionHandler] IsPointerTargetingUs: FALSE (no match)");
            return false;
        }

        void IMixedRealityPointerHandler.OnPointerDown(MixedRealityPointerEventData eventData)
        {
            string targetName = eventData.Pointer.Result?.CurrentPointerTarget != null
                ? eventData.Pointer.Result.CurrentPointerTarget.name
                : "NULL";

            Debug.Log(string.Format("[MapInteractionHandler] >>> OnPointerDown - Pointer: {0}, Target: {1}, ThisObject: {2}",
                eventData.Pointer.PointerName, targetName, gameObject.name));

            if (IsPointerTargetingUs(eventData))
            {
                _lastPointerPosition = eventData.Pointer.Result.Details.Point;
                _pointerDownPosition = _lastPointerPosition; // Sauvegarder la position initiale
                _pointerDownTime = Time.time; // Sauvegarder le temps
                _isManipulating = true;
                Debug.Log(string.Format("[MapInteractionHandler] OnPointerDown ACCEPTED - Position: {0}, Time: {1}, isManipulating: {2}",
                    _lastPointerPosition, _pointerDownTime, _isManipulating));
            }
            else
            {
                Debug.Log("[MapInteractionHandler] OnPointerDown IGNORED - pas sur la carte");
            }
        }

        void IMixedRealityPointerHandler.OnPointerUp(MixedRealityPointerEventData eventData)
        {
            Debug.Log(string.Format("[MapInteractionHandler] >>> OnPointerUp - wasManipulating: {0}", _isManipulating));

            // Vérifier si c'était un TAP (clic rapide sans mouvement) plutôt qu'un DRAG
            if (_isManipulating && eventData.Pointer.Result != null)
            {
                Vector3 upPosition = eventData.Pointer.Result.Details.Point;
                float distance = Vector3.Distance(_pointerDownPosition, upPosition);
                float duration = Time.time - _pointerDownTime;

                Debug.Log(string.Format("[MapInteractionHandler] OnPointerUp - Distance: {0:F4}m, Duration: {1:F3}s", distance, duration));
                Debug.Log(string.Format("[MapInteractionHandler] OnPointerUp - DownPos: {0}, UpPos: {1}", _pointerDownPosition, upPosition));

                // Si le mouvement est très faible et rapide, c'est un TAP -> sélectionner la parcelle
                if (distance < TAP_DISTANCE_THRESHOLD && duration < TAP_TIME_THRESHOLD)
                {
                    Debug.Log("[MapInteractionHandler] OnPointerUp - DÉTECTÉ COMME TAP -> Sélection de parcelle");

                    if (_enableSelection && _parcelSelectionHandler != null)
                    {
                        Debug.Log(string.Format("[MapInteractionHandler] OnPointerUp - Appel SelectParcelAtWorldPosition({0})", upPosition));
                        _parcelSelectionHandler.OnMRTKPointerClicked(upPosition);
                    }
                    else
                    {
                        if (!_enableSelection)
                            Debug.LogWarning("[MapInteractionHandler] OnPointerUp - Sélection désactivée");
                        if (_parcelSelectionHandler == null)
                            Debug.LogError("[MapInteractionHandler] OnPointerUp - _parcelSelectionHandler est NULL");
                    }
                }
                else
                {
                    Debug.Log(string.Format("[MapInteractionHandler] OnPointerUp - DÉTECTÉ COMME DRAG (distance: {0:F4} >= {1} OU duration: {2:F3} >= {3})",
                        distance, TAP_DISTANCE_THRESHOLD, duration, TAP_TIME_THRESHOLD));
                }
            }

            _isManipulating = false;
        }

        void IMixedRealityPointerHandler.OnPointerClicked(MixedRealityPointerEventData eventData)
        {
            string targetName = eventData.Pointer.Result?.CurrentPointerTarget != null
                ? eventData.Pointer.Result.CurrentPointerTarget.name
                : "NULL";

            Debug.Log(string.Format("[MapInteractionHandler] >>> OnPointerClicked - enableSelection: {0}, Pointer: {1}, Target: {2}",
                _enableSelection, eventData.Pointer.PointerName, targetName));

            if (!_enableSelection)
            {
                Debug.Log("[MapInteractionHandler] OnPointerClicked IGNORED - selection disabled");
                return;
            }

            // Vérifier si on clique sur la carte
            if (!IsPointerTargetingUs(eventData))
            {
                Debug.Log("[MapInteractionHandler] OnPointerClicked IGNORED - pas sur la carte");
                return;
            }

            Vector3 hitPoint = eventData.Pointer.Result.Details.Point;
            Debug.Log(string.Format("[MapInteractionHandler] OnPointerClicked ACCEPTED - HitPoint: {0}", hitPoint));

            if (_parcelSelectionHandler != null)
            {
                Debug.Log("[MapInteractionHandler] Calling _parcelSelectionHandler.OnMRTKPointerClicked...");
                _parcelSelectionHandler.OnMRTKPointerClicked(hitPoint);
            }
            else
            {
                Debug.LogError("[MapInteractionHandler] _parcelSelectionHandler is NULL!");
            }

            // Marquer l'événement comme utilisé
            eventData.Use();
        }

        void IMixedRealityPointerHandler.OnPointerDragged(MixedRealityPointerEventData eventData)
        {
            if (_isManipulating && _enablePan)
            {
                Vector3 position = eventData.Pointer.Result.Details.Point;
                Vector3 delta = position - _lastPointerPosition;

                if (delta.magnitude > 0.001f)
                {
                    Debug.Log(string.Format("[MapInteractionHandler] >>> OnPointerDragged - Delta: {0}, Magnitude: {1:F4}",
                        delta, delta.magnitude));
                }

                _lastPointerPosition = position;

                if (_mapManager != null)
                {
                    double latDelta = delta.z * _panSensitivity;
                    double lngDelta = delta.x * _panSensitivity;

                    Debug.Log(string.Format("[MapInteractionHandler] Pan - latDelta: {0:F6}, lngDelta: {1:F6}", latDelta, lngDelta));

                    _mapManager.SetPosition(
                        _mapManager.CurrentLatitude - latDelta,
                        _mapManager.CurrentLongitude - lngDelta,
                        _mapManager.CurrentZoom
                    );
                }
                else
                {
                    Debug.LogError("[MapInteractionHandler] OnPointerDragged - _mapManager is NULL!");
                }
            }
        }

        #endregion

        #region IMixedRealityFocusHandler Implementation

        void IMixedRealityFocusHandler.OnFocusEnter(FocusEventData eventData)
        {
            _isFocused = true;
            Debug.Log(string.Format("[MapInteractionHandler] === FOCUS ENTER === Pointer: {0}, OldTarget: {1}, NewTarget: {2}",
                eventData.Pointer != null ? eventData.Pointer.PointerName : "NULL",
                eventData.OldFocusedObject != null ? eventData.OldFocusedObject.name : "NULL",
                eventData.NewFocusedObject != null ? eventData.NewFocusedObject.name : "NULL"));
        }

        void IMixedRealityFocusHandler.OnFocusExit(FocusEventData eventData)
        {
            _isFocused = false;
            _isManipulating = false;
            _isTouching = false;
            Debug.Log(string.Format("[MapInteractionHandler] === FOCUS EXIT === Pointer: {0}, OldTarget: {1}, NewTarget: {2}",
                eventData.Pointer != null ? eventData.Pointer.PointerName : "NULL",
                eventData.OldFocusedObject != null ? eventData.OldFocusedObject.name : "NULL",
                eventData.NewFocusedObject != null ? eventData.NewFocusedObject.name : "NULL"));
        }

        #endregion

        #region IMixedRealityTouchHandler Implementation

        void IMixedRealityTouchHandler.OnTouchStarted(HandTrackingInputEventData eventData)
        {
            _lastTouchPosition = eventData.InputData;
            _isTouching = true;
            Debug.Log(string.Format("[MapInteractionHandler] === TOUCH STARTED === Position: {0}, Handedness: {1}",
                _lastTouchPosition, eventData.Handedness));
        }

        void IMixedRealityTouchHandler.OnTouchUpdated(HandTrackingInputEventData eventData)
        {
            if (!_enablePan || !_isTouching || _mapManager == null)
            {
                if (!_enablePan) Debug.Log("[MapInteractionHandler] TouchUpdated IGNORED - pan disabled");
                if (!_isTouching) Debug.Log("[MapInteractionHandler] TouchUpdated IGNORED - not touching");
                if (_mapManager == null) Debug.LogError("[MapInteractionHandler] TouchUpdated IGNORED - _mapManager NULL");
                return;
            }

            Vector3 currentPosition = eventData.InputData;
            Vector3 delta = currentPosition - _lastTouchPosition;
            _lastTouchPosition = currentPosition;

            if (delta.magnitude > 0.001f)
            {
                Debug.Log(string.Format("[MapInteractionHandler] === TOUCH UPDATED === Delta: {0}, Magnitude: {1:F4}",
                    delta, delta.magnitude));

                // Navigation inverse - même logique que OnPointerDragged
                double latDelta = delta.z * _panSensitivity;
                double lngDelta = delta.x * _panSensitivity;

                Debug.Log(string.Format("[MapInteractionHandler] Touch Pan - latDelta: {0:F6}, lngDelta: {1:F6}", latDelta, lngDelta));

                _mapManager.SetPosition(
                    _mapManager.CurrentLatitude - latDelta,
                    _mapManager.CurrentLongitude - lngDelta,
                    _mapManager.CurrentZoom
                );
            }
        }

        void IMixedRealityTouchHandler.OnTouchCompleted(HandTrackingInputEventData eventData)
        {
            _isTouching = false;
            Debug.Log(string.Format("[MapInteractionHandler] === TOUCH COMPLETED === Handedness: {0}", eventData.Handedness));
        }

        #endregion

        #region MRTK Manipulation Handlers

        /// <summary>
        /// Appelé par MRTK lors d'un pinch zoom
        /// </summary>
        public void OnPinchZoom(float zoomDelta)
        {
            if (!_enableZoom || _mapManager == null) return;

            float newZoom = _mapManager.CurrentZoom + (zoomDelta * _zoomSensitivity);
            newZoom = Mathf.Clamp(newZoom, 10f, 20f);

            _mapManager.SetPosition(
                _mapManager.CurrentLatitude,
                _mapManager.CurrentLongitude,
                newZoom
            );
        }

        #endregion

        #region Voice Commands

        /// <summary>
        /// Commande vocale: "Zoom avant"
        /// </summary>
        public void OnVoiceZoomIn()
        {
            Debug.Log("[MapInteractionHandler] === VOICE COMMAND: Zoom avant ===");

            if (_mapManager == null)
            {
                Debug.LogError("[MapInteractionHandler] OnVoiceZoomIn - _mapManager is NULL");
                return;
            }

            float newZoom = Mathf.Min(_mapManager.CurrentZoom + 1f, 20f);
            Debug.Log(string.Format("[MapInteractionHandler] Zoom avant: {0} -> {1}", _mapManager.CurrentZoom, newZoom));

            _mapManager.FlyTo(
                _mapManager.CurrentLatitude,
                _mapManager.CurrentLongitude,
                newZoom,
                0.5f
            );
        }

        /// <summary>
        /// Commande vocale: "Zoom arrière"
        /// </summary>
        public void OnVoiceZoomOut()
        {
            Debug.Log("[MapInteractionHandler] === VOICE COMMAND: Zoom arrière ===");

            if (_mapManager == null)
            {
                Debug.LogError("[MapInteractionHandler] OnVoiceZoomOut - _mapManager is NULL");
                return;
            }

            float newZoom = Mathf.Max(_mapManager.CurrentZoom - 1f, 10f);
            Debug.Log(string.Format("[MapInteractionHandler] Zoom arrière: {0} -> {1}", _mapManager.CurrentZoom, newZoom));

            _mapManager.FlyTo(
                _mapManager.CurrentLatitude,
                _mapManager.CurrentLongitude,
                newZoom,
                0.5f
            );
        }

        /// <summary>
        /// Commande vocale: "Sélectionner ici"
        /// Sélectionne la parcelle au point de regard actuel
        /// </summary>
        public void OnVoiceSelectHere()
        {
            Debug.Log("[MapInteractionHandler] === VOICE COMMAND: Sélectionner ici ===");

            // Obtenir le point de regard actuel via MRTK
            var gazeProvider = CoreServices.InputSystem?.GazeProvider;
            if (gazeProvider != null && gazeProvider.GazeTarget != null)
            {
                Vector3 hitPosition = gazeProvider.HitPosition;
                Debug.Log(string.Format("[MapInteractionHandler] Sélection vocale - GazeTarget: {0}, HitPosition: {1}",
                    gazeProvider.GazeTarget.name, hitPosition));

                if (_parcelSelectionHandler != null)
                {
                    _parcelSelectionHandler.OnMRTKPointerClicked(hitPosition);
                }
                else
                {
                    Debug.LogError("[MapInteractionHandler] OnVoiceSelectHere - _parcelSelectionHandler is NULL");
                }
            }
            else
            {
                Debug.LogWarning(string.Format("[MapInteractionHandler] OnVoiceSelectHere - GazeProvider: {0}, GazeTarget: {1}",
                    gazeProvider != null ? "OK" : "NULL",
                    gazeProvider?.GazeTarget != null ? gazeProvider.GazeTarget.name : "NULL"));
            }
        }

        /// <summary>
        /// Commande vocale: "Effacer sélection"
        /// </summary>
        public void OnVoiceClearSelection()
        {
            Debug.Log("[MapInteractionHandler] === VOICE COMMAND: Effacer sélection ===");

            if (_parcelSelectionHandler != null)
            {
                _parcelSelectionHandler.ClearSelection();
            }
            else
            {
                Debug.LogError("[MapInteractionHandler] OnVoiceClearSelection - _parcelSelectionHandler is NULL");
            }
        }

        /// <summary>
        /// Commande vocale: "Retour à Paris" (ou autre ville)
        /// </summary>
        public void OnVoiceGoToDefault()
        {
            Debug.Log("[MapInteractionHandler] === VOICE COMMAND: Retour à Paris ===");

            if (_mapManager != null)
            {
                _mapManager.FlyTo(48.8566, 2.3522, 15f, 2f);
            }
            else
            {
                Debug.LogError("[MapInteractionHandler] OnVoiceGoToDefault - _mapManager is NULL");
            }
        }

        #endregion
    }
}
