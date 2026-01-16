using System.Collections.Generic;
using UnityEngine;
using GeoscaleCadastre.Models;

namespace GeoscaleCadastre.Map
{
    /// <summary>
    /// Gère le surlignage visuel des parcelles sélectionnées sur la carte
    /// </summary>
    public class ParcelHighlighter : MonoBehaviour
    {
        [Header("Configuration visuelle")]
        [SerializeField]
        [Tooltip("Matériau pour le remplissage de la parcelle")]
        private Material _fillMaterial;

        [SerializeField]
        [Tooltip("Matériau pour le contour de la parcelle")]
        private Material _outlineMaterial;

        [SerializeField]
        [Tooltip("Couleur de remplissage")]
        private Color _fillColor = new Color(0, 0.9f, 0.75f, 0.3f); // #00E5BE avec transparence

        [SerializeField]
        [Tooltip("Couleur du contour")]
        private Color _outlineColor = new Color(0, 0.9f, 0.75f, 1f); // #00E5BE

        [SerializeField]
        [Tooltip("Épaisseur du contour")]
        private float _outlineWidth = 0.002f;

        [SerializeField]
        [Tooltip("Hauteur du surlignage au-dessus de la carte")]
        private float _highlightHeight = 0.01f;

        [Header("Références")]
        [SerializeField]
        [Tooltip("Parent des objets de surlignage")]
        private Transform _highlightParent;

        // Objets créés pour le surlignage
        private GameObject _currentFillObject;
        private GameObject _currentOutlineObject;
        private ParcelModel _currentParcel;

        private void Awake()
        {
            // Créer le parent si nécessaire
            if (_highlightParent == null)
            {
                var parentObj = new GameObject("ParcelHighlights");
                parentObj.transform.SetParent(transform);
                _highlightParent = parentObj.transform;
            }

            // Créer les matériaux par défaut si non assignés
            if (_fillMaterial == null)
            {
                _fillMaterial = CreateDefaultFillMaterial();
            }

            if (_outlineMaterial == null)
            {
                _outlineMaterial = CreateDefaultOutlineMaterial();
            }
        }

        /// <summary>
        /// Surligne une parcelle
        /// </summary>
        /// <param name="parcel">Parcelle à surligner</param>
        public void HighlightParcel(ParcelModel parcel)
        {
            Debug.Log("=== [ParcelHighlighter] DÉBUT SURLIGNAGE PARCELLE ===");

            if (parcel == null)
            {
                Debug.LogError("[ParcelHighlighter] ERREUR: Parcelle est NULL");
                return;
            }

            if (parcel.Geometry == null)
            {
                Debug.LogError("[ParcelHighlighter] ERREUR: Parcelle.Geometry est NULL");
                return;
            }

            if (parcel.Geometry.Length < 3)
            {
                Debug.LogError(string.Format("[ParcelHighlighter] ERREUR: Géométrie invalide - seulement {0} points (minimum 3)", parcel.Geometry.Length));
                return;
            }

            Debug.Log(string.Format("[ParcelHighlighter] Parcelle valide: {0}", parcel.GetFormattedId()));
            Debug.Log(string.Format("[ParcelHighlighter] Géométrie: {0} points", parcel.Geometry.Length));
            Debug.Log(string.Format("[ParcelHighlighter] Premier point: {0}", parcel.Geometry[0]));
            Debug.Log(string.Format("[ParcelHighlighter] Centroid: {0}", parcel.Centroid));

            // Nettoyer le surlignage précédent
            Debug.Log("[ParcelHighlighter] Nettoyage du surlignage précédent...");
            ClearHighlight();

            _currentParcel = parcel;

            // Créer le mesh de remplissage
            Debug.Log("[ParcelHighlighter] Création du mesh de remplissage...");
            CreateFillMesh(parcel);
            Debug.Log(string.Format("[ParcelHighlighter] Mesh de remplissage créé: {0}", _currentFillObject != null ? "OK" : "ÉCHEC"));

            // Créer le contour
            Debug.Log("[ParcelHighlighter] Création du contour...");
            CreateOutline(parcel);
            Debug.Log(string.Format("[ParcelHighlighter] Contour créé: {0}", _currentOutlineObject != null ? "OK" : "ÉCHEC"));

            Debug.Log("=== [ParcelHighlighter] FIN SURLIGNAGE PARCELLE ===");
        }

        /// <summary>
        /// Efface le surlignage actuel
        /// </summary>
        public void ClearHighlight()
        {
            if (_currentFillObject != null)
            {
                Destroy(_currentFillObject);
                _currentFillObject = null;
            }

            if (_currentOutlineObject != null)
            {
                Destroy(_currentOutlineObject);
                _currentOutlineObject = null;
            }

            _currentParcel = null;
        }

        /// <summary>
        /// Définit la couleur de surlignage
        /// </summary>
        public void SetHighlightColor(Color fillColor, Color outlineColor)
        {
            _fillColor = fillColor;
            _outlineColor = outlineColor;

            if (_fillMaterial != null)
                _fillMaterial.color = _fillColor;

            if (_outlineMaterial != null)
                _outlineMaterial.color = _outlineColor;
        }

        private void CreateFillMesh(ParcelModel parcel)
        {
            Debug.Log("[ParcelHighlighter.CreateFillMesh] Création du GameObject...");
            _currentFillObject = new GameObject("ParcelFill");
            _currentFillObject.transform.SetParent(_highlightParent);
            Debug.Log(string.Format("[ParcelHighlighter.CreateFillMesh] GameObject créé, parent: {0}", _highlightParent != null ? _highlightParent.name : "NULL"));

            var meshFilter = _currentFillObject.AddComponent<MeshFilter>();
            var meshRenderer = _currentFillObject.AddComponent<MeshRenderer>();
            Debug.Log("[ParcelHighlighter.CreateFillMesh] MeshFilter et MeshRenderer ajoutés");

            // Créer le mesh à partir de la géométrie
            Debug.Log("[ParcelHighlighter.CreateFillMesh] Appel de CreatePolygonMesh...");
            var mesh = CreatePolygonMesh(parcel.Geometry);
            Debug.Log(string.Format("[ParcelHighlighter.CreateFillMesh] Mesh créé - Vertices: {0}, Triangles: {1}",
                mesh.vertexCount, mesh.triangles.Length / 3));
            meshFilter.mesh = mesh;

            // Appliquer le matériau
            var mat = new Material(_fillMaterial);
            mat.color = _fillColor;
            meshRenderer.material = mat;
            Debug.Log(string.Format("[ParcelHighlighter.CreateFillMesh] Matériau appliqué - Couleur: {0}, Shader: {1}",
                _fillColor, mat.shader.name));

            // Positionner au-dessus de la carte
            _currentFillObject.transform.localPosition = new Vector3(0, _highlightHeight, 0);
            Debug.Log(string.Format("[ParcelHighlighter.CreateFillMesh] Position locale: {0}", _currentFillObject.transform.localPosition));
            Debug.Log(string.Format("[ParcelHighlighter.CreateFillMesh] Position mondiale: {0}", _currentFillObject.transform.position));
            Debug.Log(string.Format("[ParcelHighlighter.CreateFillMesh] Active: {0}", _currentFillObject.activeSelf));
        }

        private void CreateOutline(ParcelModel parcel)
        {
            Debug.Log("[ParcelHighlighter.CreateOutline] Création du GameObject...");
            _currentOutlineObject = new GameObject("ParcelOutline");
            _currentOutlineObject.transform.SetParent(_highlightParent);

            var lineRenderer = _currentOutlineObject.AddComponent<LineRenderer>();
            Debug.Log("[ParcelHighlighter.CreateOutline] LineRenderer ajouté");

            // Configurer le LineRenderer
            lineRenderer.material = _outlineMaterial;
            lineRenderer.startColor = _outlineColor;
            lineRenderer.endColor = _outlineColor;
            lineRenderer.startWidth = _outlineWidth;
            lineRenderer.endWidth = _outlineWidth;
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            Debug.Log(string.Format("[ParcelHighlighter.CreateOutline] LineRenderer configuré - Couleur: {0}, Width: {1}",
                _outlineColor, _outlineWidth));

            // Définir les points du contour
            var points = ConvertToWorldPositions(parcel.Geometry);
            lineRenderer.positionCount = points.Length;
            lineRenderer.SetPositions(points);
            Debug.Log(string.Format("[ParcelHighlighter.CreateOutline] Points du contour: {0}", points.Length));
            if (points.Length > 0)
            {
                Debug.Log(string.Format("[ParcelHighlighter.CreateOutline] Premier point: {0}, Dernier point: {1}",
                    points[0], points[points.Length - 1]));
            }

            // Positionner au-dessus de la carte
            _currentOutlineObject.transform.localPosition = new Vector3(0, _highlightHeight + 0.001f, 0);
            Debug.Log(string.Format("[ParcelHighlighter.CreateOutline] Position locale: {0}", _currentOutlineObject.transform.localPosition));
            Debug.Log(string.Format("[ParcelHighlighter.CreateOutline] Position mondiale: {0}", _currentOutlineObject.transform.position));
            Debug.Log(string.Format("[ParcelHighlighter.CreateOutline] Active: {0}", _currentOutlineObject.activeSelf));
        }

        private Mesh CreatePolygonMesh(Vector2[] geometry)
        {
            var mesh = new Mesh();

            // Convertir les coordonnées 2D en positions 3D
            var vertices = new Vector3[geometry.Length];
            for (int i = 0; i < geometry.Length; i++)
            {
                // Note: Les coordonnées GPS doivent être converties en coordonnées locales
                // Cette conversion dépend du système de coordonnées de Mapbox
                vertices[i] = ConvertGpsToLocal(geometry[i]);
            }

            // Triangulation simple (fan triangulation - fonctionne pour les polygones convexes)
            // Pour les polygones complexes, utiliser une librairie de triangulation
            var triangles = new List<int>();
            for (int i = 1; i < vertices.Length - 1; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private Vector3[] ConvertToWorldPositions(Vector2[] geometry)
        {
            var positions = new Vector3[geometry.Length];
            for (int i = 0; i < geometry.Length; i++)
            {
                positions[i] = ConvertGpsToLocal(geometry[i]);
            }
            return positions;
        }

        private Vector3 ConvertGpsToLocal(Vector2 gpsCoord)
        {
            // Conversion basique GPS -> coordonnées locales
            // Cette conversion devrait être faite via le système Mapbox pour être précise
            // Ici on utilise une approximation simple centrée sur le centroïde

            if (_currentParcel != null)
            {
                // Coordonnées relatives au centroïde
                float offsetLng = gpsCoord.x - _currentParcel.Centroid.x;
                float offsetLat = gpsCoord.y - _currentParcel.Centroid.y;

                // Conversion approximative en mètres (à ajuster selon l'échelle de la carte)
                // 1 degré lat ≈ 111km, 1 degré lng ≈ 75km (à ~46° latitude France)
                float x = offsetLng * 75000f * 0.00001f; // Mise à l'échelle pour Unity
                float z = offsetLat * 111000f * 0.00001f;

                Vector3 result = new Vector3(x, 0, z);

                // Log seulement pour le premier point pour éviter trop de logs
                if (gpsCoord == _currentParcel.Geometry[0])
                {
                    Debug.Log(string.Format("[ParcelHighlighter.ConvertGpsToLocal] GPS: {0} -> Local: {1} (offset lng={2:F6}, lat={3:F6})",
                        gpsCoord, result, offsetLng, offsetLat));
                }

                return result;
            }

            Debug.LogWarning("[ParcelHighlighter.ConvertGpsToLocal] _currentParcel est NULL - conversion par défaut");
            return new Vector3(gpsCoord.x, 0, gpsCoord.y);
        }

        private Material CreateDefaultFillMaterial()
        {
            // Créer un matériau transparent basique
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.color = _fillColor;

            // Configurer pour la transparence
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            return mat;
        }

        private Material CreateDefaultOutlineMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.color = _outlineColor;

            return mat;
        }

        private void OnDestroy()
        {
            ClearHighlight();
        }
    }
}
