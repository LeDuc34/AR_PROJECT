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
            if (parcel == null || parcel.Geometry == null || parcel.Geometry.Length < 3)
            {
                Debug.LogWarning("[ParcelHighlighter] Parcelle invalide ou sans géométrie");
                return;
            }

            // Nettoyer le surlignage précédent
            ClearHighlight();

            _currentParcel = parcel;

            // Créer le mesh de remplissage
            CreateFillMesh(parcel);

            // Créer le contour
            CreateOutline(parcel);

            Debug.Log(string.Format("[ParcelHighlighter] Parcelle surlignée: {0}", parcel.GetFormattedId()));
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
            _currentFillObject = new GameObject("ParcelFill");
            _currentFillObject.transform.SetParent(_highlightParent);

            var meshFilter = _currentFillObject.AddComponent<MeshFilter>();
            var meshRenderer = _currentFillObject.AddComponent<MeshRenderer>();

            // Créer le mesh à partir de la géométrie
            var mesh = CreatePolygonMesh(parcel.Geometry);
            meshFilter.mesh = mesh;

            // Appliquer le matériau
            var mat = new Material(_fillMaterial);
            mat.color = _fillColor;
            meshRenderer.material = mat;

            // Positionner au-dessus de la carte
            _currentFillObject.transform.localPosition = new Vector3(0, _highlightHeight, 0);
        }

        private void CreateOutline(ParcelModel parcel)
        {
            _currentOutlineObject = new GameObject("ParcelOutline");
            _currentOutlineObject.transform.SetParent(_highlightParent);

            var lineRenderer = _currentOutlineObject.AddComponent<LineRenderer>();

            // Configurer le LineRenderer
            lineRenderer.material = _outlineMaterial;
            lineRenderer.startColor = _outlineColor;
            lineRenderer.endColor = _outlineColor;
            lineRenderer.startWidth = _outlineWidth;
            lineRenderer.endWidth = _outlineWidth;
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;

            // Définir les points du contour
            var points = ConvertToWorldPositions(parcel.Geometry);
            lineRenderer.positionCount = points.Length;
            lineRenderer.SetPositions(points);

            // Positionner au-dessus de la carte
            _currentOutlineObject.transform.localPosition = new Vector3(0, _highlightHeight + 0.001f, 0);
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

                return new Vector3(x, 0, z);
            }

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
