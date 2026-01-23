using System.Collections.Generic;
using UnityEngine;
using GeoscaleCadastre.Models;

namespace GeoscaleCadastre.Map
{
    /// <summary>
    /// Gère le surlignage visuel des parcelles sélectionnées sur la carte
    /// Utilise un Projector pour "peindre" la couleur sur la carte et les bâtiments
    /// </summary>
    public class ParcelHighlighter : MonoBehaviour
    {
        [Header("Configuration visuelle")]
        [SerializeField]
        [Tooltip("Couleur de projection")]
        private Color _highlightColor = new Color(0, 0.9f, 0.75f, 0.5f); // #00E5BE avec transparence

        [SerializeField]
        [Tooltip("Résolution de la texture de projection (plus élevé = plus précis)")]
        private int _textureResolution = 512;

        [SerializeField]
        [Tooltip("Hauteur du Projector au-dessus de la carte")]
        private float _projectorHeight = 10f;

        [SerializeField]
        [Tooltip("Profondeur de projection (doit couvrir les bâtiments)")]
        private float _projectorDepth = 20f;

        [SerializeField]
        [Tooltip("Marge autour de la parcelle (en pourcentage)")]
        [Range(0.05f, 0.5f)]
        private float _boundsPadding = 0.1f;

        [Header("Simplification de géométrie")]
        [SerializeField]
        [Tooltip("Activer la simplification Douglas-Peucker")]
        private bool _simplifyGeometry = true;

        [SerializeField]
        [Tooltip("Tolérance de simplification (plus élevé = moins de points)")]
        [Range(0.001f, 0.1f)]
        private float _simplificationTolerance = 0.01f;

        [Header("Références")]
        [SerializeField]
        [Tooltip("Référence au MapManager pour la conversion de coordonnées")]
        private MapManager _mapManager;

        [SerializeField]
        [Tooltip("Layers à ignorer par le Projector")]
        private LayerMask _ignoreLayers;

        // Composants du Projector
        private GameObject _projectorObject;
        private Projector _projector;
        private Material _projectorMaterial;
        private Texture2D _parcelTexture;
        private ParcelModel _currentParcel;
        private bool _isXYPlane; // True si la carte est dans le plan XY, false si XZ

        private void Awake()
        {
            // Vérifier que MapManager est assigné
            if (_mapManager == null)
            {
                Debug.LogWarning("[ParcelHighlighter] MapManager non assigné! Le surlignage ne fonctionnera pas correctement.");
            }

            // Créer le Projector
            CreateProjector();
        }

        private void CreateProjector()
        {
            _projectorObject = new GameObject("ParcelProjector");
            _projectorObject.transform.SetParent(transform);

            _projector = _projectorObject.AddComponent<Projector>();

            // Créer le material pour le Projector
            _projectorMaterial = CreateProjectorMaterial();
            _projector.material = _projectorMaterial;

            // Configuration du Projector
            _projector.orthographic = true;
            _projector.orthographicSize = 1f; // Sera ajusté dynamiquement
            _projector.nearClipPlane = 0.1f;
            _projector.farClipPlane = _projectorDepth;
            _projector.ignoreLayers = _ignoreLayers;

            // Orienter vers le bas
            _projectorObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Désactiver par défaut
            _projector.enabled = false;

            Debug.Log("[ParcelHighlighter] Projector créé et configuré");
        }

        private Material CreateProjectorMaterial()
        {
            // Utiliser notre shader custom
            var shader = Shader.Find("GeoscaleCadastre/ProjectorHighlight");
            if (shader == null)
            {
                Debug.LogError("[ParcelHighlighter] Shader 'GeoscaleCadastre/ProjectorHighlight' non trouvé! Assurez-vous que le fichier ProjectorHighlight.shader est dans le projet.");
                // Fallback
                shader = Shader.Find("Projector/Light");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
            }

            var mat = new Material(shader);
            mat.SetColor("_Color", _highlightColor);

            Debug.Log(string.Format("[ParcelHighlighter] Material créé avec shader: {0}", shader.name));

            return mat;
        }

        /// <summary>
        /// Surligne une parcelle en projetant une couleur dessus
        /// </summary>
        public void HighlightParcel(ParcelModel parcel)
        {
            Debug.Log("=== [ParcelHighlighter] DÉBUT SURLIGNAGE PARCELLE (PROJECTOR) ===");

            if (parcel == null)
            {
                Debug.LogError("[ParcelHighlighter] ERREUR: Parcelle est NULL");
                return;
            }

            if (parcel.Geometry == null || parcel.Geometry.Length < 3)
            {
                Debug.LogError("[ParcelHighlighter] ERREUR: Géométrie invalide");
                return;
            }

            Debug.Log(string.Format("[ParcelHighlighter] Parcelle: {0}, {1} points",
                parcel.GetFormattedId(), parcel.Geometry.Length));

            // Nettoyer le surlignage précédent
            ClearHighlight();

            _currentParcel = parcel;

            // Convertir les coordonnées GPS en positions monde
            var worldPositions = ConvertToWorldPositions(parcel.Geometry);
            if (worldPositions.Length == 0 || worldPositions[0] == Vector3.zero)
            {
                Debug.LogError("[ParcelHighlighter] Échec de conversion des coordonnées GPS!");
                return;
            }

            // Calculer la bounding box de la parcelle
            var bounds = CalculateBounds(worldPositions);
            Debug.Log(string.Format("[ParcelHighlighter] Bounds: center={0}, size={1}",
                bounds.center, bounds.size));

            // Détecter l'orientation de la carte (nécessaire pour la simplification)
            _isXYPlane = bounds.size.z < bounds.size.y;

            // Simplifier la géométrie si activé
            if (_simplifyGeometry && worldPositions.Length > 4)
            {
                int originalCount = worldPositions.Length;
                worldPositions = SimplifyPolygon(worldPositions, _simplificationTolerance);
                Debug.Log(string.Format("[ParcelHighlighter] Géométrie simplifiée: {0} → {1} points",
                    originalCount, worldPositions.Length));

                // Recalculer les bounds après simplification
                bounds = CalculateBounds(worldPositions);
            }

            // Générer la texture de la parcelle
            GenerateParcelTexture(worldPositions, bounds);

            // Positionner et configurer le Projector
            ConfigureProjector(bounds);

            // Activer le Projector
            _projector.enabled = true;

            Debug.Log("=== [ParcelHighlighter] FIN SURLIGNAGE PARCELLE ===");
        }

        /// <summary>
        /// Efface le surlignage actuel
        /// </summary>
        public void ClearHighlight()
        {
            if (_projector != null)
            {
                _projector.enabled = false;
            }

            if (_parcelTexture != null)
            {
                Destroy(_parcelTexture);
                _parcelTexture = null;
            }

            _currentParcel = null;
        }

        /// <summary>
        /// Définit la couleur de surlignage
        /// </summary>
        public void SetHighlightColor(Color color)
        {
            _highlightColor = color;

            if (_projectorMaterial != null)
            {
                _projectorMaterial.SetColor("_Color", _highlightColor);
            }
        }

        private void GenerateParcelTexture(Vector3[] worldPositions, Bounds bounds)
        {
            // Utiliser une taille de bounds carrée pour éviter la déformation
            float sizeA = bounds.size.x;
            float sizeB = _isXYPlane ? bounds.size.y : bounds.size.z;
            float maxSize = Mathf.Max(sizeA, sizeB);
            Vector3 squareCenter = bounds.center;

            Debug.Log(string.Format("[ParcelHighlighter] Orientation: {0}, sizeA={1}, sizeB={2}, maxSize={3}",
                _isXYPlane ? "XY" : "XZ", sizeA, sizeB, maxSize));

            // Créer une nouvelle texture
            _parcelTexture = new Texture2D(_textureResolution, _textureResolution, TextureFormat.ARGB32, false);
            _parcelTexture.wrapMode = TextureWrapMode.Clamp;
            _parcelTexture.filterMode = FilterMode.Bilinear;

            // Remplir avec du transparent
            var clearColor = new Color(0, 0, 0, 0);
            var fillColor = Color.white; // Blanc pour le masque, la couleur vient du shader
            var pixels = new Color[_textureResolution * _textureResolution];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clearColor;
            }

            // Convertir les positions monde en coordonnées UV (0-1) dans un carré centré
            var uvPoints = new Vector2[worldPositions.Length];
            for (int i = 0; i < worldPositions.Length; i++)
            {
                // Centrer sur le milieu du carré et normaliser
                float u = (worldPositions[i].x - squareCenter.x) / maxSize + 0.5f;
                float v;
                if (_isXYPlane)
                {
                    // Carte dans le plan XY
                    v = (worldPositions[i].y - squareCenter.y) / maxSize + 0.5f;
                }
                else
                {
                    // Carte dans le plan XZ
                    v = (worldPositions[i].z - squareCenter.z) / maxSize + 0.5f;
                }
                uvPoints[i] = new Vector2(u, v);
            }

            Debug.Log(string.Format("[ParcelHighlighter] UV Points: premier={0}, dernier={1}",
                uvPoints[0], uvPoints[uvPoints.Length - 1]));

            // Dessiner le polygone rempli sur la texture
            FillPolygon(pixels, uvPoints, fillColor);

            // Appliquer les pixels à la texture
            _parcelTexture.SetPixels(pixels);
            _parcelTexture.Apply();

            // Assigner la texture au material du Projector
            _projectorMaterial.SetTexture("_ShadowTex", _parcelTexture);

            Debug.Log(string.Format("[ParcelHighlighter] Texture générée: {0}x{0}, maxSize={1}",
                _textureResolution, maxSize));
        }

        private void FillPolygon(Color[] pixels, Vector2[] uvPoints, Color fillColor)
        {
            int width = _textureResolution;
            int height = _textureResolution;

            // Utiliser un algorithme point-in-polygon plus robuste
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Convertir pixel en coordonnées UV
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;

                    if (IsPointInPolygon(u, v, uvPoints))
                    {
                        pixels[y * width + x] = fillColor;
                    }
                }
            }
        }

        private bool IsPointInPolygon(float x, float y, Vector2[] polygon)
        {
            // Algorithme ray casting (plus robuste que scanline)
            bool inside = false;
            int n = polygon.Length;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = polygon[i].x, yi = polygon[i].y;
                float xj = polygon[j].x, yj = polygon[j].y;

                // Vérifier si le rayon horizontal vers la droite croise ce segment
                if (((yi > y) != (yj > y)) &&
                    (x < (xj - xi) * (y - yi) / (yj - yi) + xi))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        #region Douglas-Peucker Simplification

        /// <summary>
        /// Simplifie un polygone avec l'algorithme Douglas-Peucker
        /// </summary>
        private Vector3[] SimplifyPolygon(Vector3[] points, float tolerance)
        {
            if (points.Length < 3)
                return points;

            // Douglas-Peucker pour une ligne ouverte
            var simplified = DouglasPeucker(new List<Vector3>(points), tolerance);

            // S'assurer qu'on a au moins 3 points
            if (simplified.Count < 3)
                return points;

            return simplified.ToArray();
        }

        private List<Vector3> DouglasPeucker(List<Vector3> points, float tolerance)
        {
            if (points.Count < 3)
                return points;

            // Trouver le point le plus éloigné de la ligne entre le premier et le dernier
            float maxDistance = 0f;
            int maxIndex = 0;

            Vector3 first = points[0];
            Vector3 last = points[points.Count - 1];

            for (int i = 1; i < points.Count - 1; i++)
            {
                float distance = PerpendicularDistance(points[i], first, last);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxIndex = i;
                }
            }

            // Si la distance max est supérieure à la tolérance, on subdivise
            if (maxDistance > tolerance)
            {
                // Récursion sur les deux parties
                var leftPart = DouglasPeucker(points.GetRange(0, maxIndex + 1), tolerance);
                var rightPart = DouglasPeucker(points.GetRange(maxIndex, points.Count - maxIndex), tolerance);

                // Combiner (sans dupliquer le point de jonction)
                var result = new List<Vector3>(leftPart);
                result.RemoveAt(result.Count - 1);
                result.AddRange(rightPart);
                return result;
            }
            else
            {
                // Simplifier à une ligne droite
                return new List<Vector3> { first, last };
            }
        }

        private float PerpendicularDistance(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            // Distance perpendiculaire d'un point à une ligne (en 2D sur le plan de la carte)
            float dx = lineEnd.x - lineStart.x;
            float dy = _isXYPlane ? (lineEnd.y - lineStart.y) : (lineEnd.z - lineStart.z);

            float px = point.x - lineStart.x;
            float py = _isXYPlane ? (point.y - lineStart.y) : (point.z - lineStart.z);

            float lineLengthSq = dx * dx + dy * dy;

            if (lineLengthSq == 0f)
            {
                // lineStart == lineEnd
                return Mathf.Sqrt(px * px + py * py);
            }

            // Projection du point sur la ligne
            float t = Mathf.Clamp01((px * dx + py * dy) / lineLengthSq);
            float projX = lineStart.x + t * dx;
            float projY = (_isXYPlane ? lineStart.y : lineStart.z) + t * dy;

            float distX = point.x - projX;
            float distY = (_isXYPlane ? point.y : point.z) - projY;

            return Mathf.Sqrt(distX * distX + distY * distY);
        }

        #endregion

        private void ConfigureProjector(Bounds bounds)
        {
            // Utiliser une taille carrée (même logique que la texture)
            float sizeA = bounds.size.x;
            float sizeB = _isXYPlane ? bounds.size.y : bounds.size.z;
            float maxSize = Mathf.Max(sizeA, sizeB);
            float padding = maxSize * _boundsPadding;
            float halfSize = maxSize / 2f + padding;

            // Configurer la taille du Projector (carré, aspect ratio 1:1)
            _projector.orthographicSize = halfSize;
            _projector.aspectRatio = 1f;

            // Positionner et orienter le Projector selon l'orientation de la carte
            Vector3 projectorPos = bounds.center;

            if (_isXYPlane)
            {
                // Carte dans le plan XY → Projector pointe vers Z négatif
                projectorPos.z = bounds.min.z - _projectorHeight;
                _projectorObject.transform.position = projectorPos;
                _projectorObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
            else
            {
                // Carte dans le plan XZ → Projector pointe vers Y négatif
                projectorPos.y = bounds.max.y + _projectorHeight;
                _projectorObject.transform.position = projectorPos;
                _projectorObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            Debug.Log(string.Format("[ParcelHighlighter] Projector configuré: pos={0}, halfSize={1}, farClip={2}, isXY={3}",
                projectorPos, halfSize, _projector.farClipPlane, _isXYPlane));
        }

        private Bounds CalculateBounds(Vector3[] positions)
        {
            if (positions.Length == 0)
                return new Bounds();

            var bounds = new Bounds(positions[0], Vector3.zero);
            for (int i = 1; i < positions.Length; i++)
            {
                bounds.Encapsulate(positions[i]);
            }
            return bounds;
        }

        private Vector3[] ConvertToWorldPositions(Vector2[] geometry)
        {
            var positions = new Vector3[geometry.Length];
            for (int i = 0; i < geometry.Length; i++)
            {
                positions[i] = ConvertGpsToWorld(geometry[i]);
            }
            return positions;
        }

        private Vector3 ConvertGpsToWorld(Vector2 gpsCoord)
        {
            if (_mapManager == null)
            {
                Debug.LogError("[ParcelHighlighter] MapManager non assigné!");
                return Vector3.zero;
            }

            Vector3 worldPos;
            double lat = gpsCoord.y;
            double lng = gpsCoord.x;

            if (_mapManager.TryGeoToWorldPosition(lat, lng, out worldPos))
            {
                return worldPos;
            }
            else
            {
                Debug.LogError(string.Format("[ParcelHighlighter] Échec conversion GPS ({0:F6}, {1:F6})",
                    lat, lng));
                return Vector3.zero;
            }
        }

        private void OnDestroy()
        {
            ClearHighlight();

            if (_projectorMaterial != null)
            {
                Destroy(_projectorMaterial);
            }

            if (_projectorObject != null)
            {
                Destroy(_projectorObject);
            }
        }
    }
}
