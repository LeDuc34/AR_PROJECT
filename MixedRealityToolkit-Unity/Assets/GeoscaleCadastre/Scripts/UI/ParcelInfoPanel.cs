using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GeoscaleCadastre.Models;

namespace GeoscaleCadastre.UI
{
    /// <summary>
    /// Panel d'affichage des informations d'une parcelle selectionnee.
    /// Construit son UI automatiquement au runtime (pas besoin de prefab).
    /// Se positionne devant la camera quand il s'affiche.
    /// </summary>
    public class ParcelInfoPanel : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Distance du panel devant la camera (metres)")]
        private float _distanceFromCamera = 0.6f;

        [SerializeField]
        [Tooltip("Decalage vertical (metres, negatif = plus bas)")]
        private float _verticalOffset = -0.05f;

        // References UI (creees automatiquement)
        private Canvas _canvas;
        private RectTransform _panelRect;
        private TMP_Text _titleText;
        private TMP_Text _communeText;
        private TMP_Text _sectionText;
        private TMP_Text _numeroText;
        private TMP_Text _surfaceText;
        private TMP_Text _codeInseeText;
        private GameObject _panelContainer;

        // Donnees actuelles
        private ParcelModel _currentParcel;
        private bool _isBuilt;

        // Couleurs (style MRTK / Geoscale)
        private static readonly Color BgColor = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        private static readonly Color AccentColor = new Color(0f, 0.9f, 0.75f, 1f); // Teal
        private static readonly Color TextColor = new Color(0.93f, 0.9f, 0.89f, 1f);
        private static readonly Color LabelColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color SeparatorColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Events
        public event Action OnPanelClosed;

        private void Awake()
        {
            BuildUI();
            _panelContainer.SetActive(false);
        }

        /// <summary>
        /// Affiche les informations d'une parcelle
        /// </summary>
        public void DisplayParcelInfo(ParcelModel parcel)
        {
            if (parcel == null)
            {
                Debug.LogWarning("[ParcelInfoPanel] Parcelle null");
                return;
            }

            if (!_isBuilt) BuildUI();

            _currentParcel = parcel;

            // Mettre a jour les textes
            _titleText.text = string.Format("Parcelle {0}", parcel.GetFormattedId());

            _communeText.text = string.IsNullOrEmpty(parcel.NomCommune) ? "-" : parcel.NomCommune;
            _sectionText.text = string.IsNullOrEmpty(parcel.Section) ? "-" : parcel.Section;
            _numeroText.text = string.IsNullOrEmpty(parcel.Numero) ? "-" : parcel.Numero;
            _surfaceText.text = parcel.GetFormattedSurface();
            _codeInseeText.text = string.IsNullOrEmpty(parcel.CodeInsee) ? "-" : parcel.CodeInsee;

            // Positionner devant la camera
            PositionInFrontOfCamera();

            // Afficher
            _panelContainer.SetActive(true);

            Debug.Log(string.Format("[ParcelInfoPanel] Affichage parcelle: {0}", parcel.GetFormattedId()));
        }

        /// <summary>
        /// Masque le panel
        /// </summary>
        public void Hide()
        {
            if (_panelContainer != null)
            {
                _panelContainer.SetActive(false);
            }
            _currentParcel = null;

            if (OnPanelClosed != null)
                OnPanelClosed();
        }

        private void PositionInFrontOfCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 forward = cam.transform.forward;
            Vector3 position = cam.transform.position
                + forward * _distanceFromCamera
                + Vector3.up * _verticalOffset;

            transform.position = position;
            // Face a la camera
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }

        #region UI Construction

        private void BuildUI()
        {
            if (_isBuilt) return;

            // Canvas World Space
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(280, 240);
            // 1 pixel = ~0.8mm -> panneau d'environ 22cm x 19cm
            transform.localScale = Vector3.one * 0.0008f;

            // Container (pour show/hide sans desactiver le canvas)
            _panelContainer = new GameObject("PanelContainer");
            _panelContainer.transform.SetParent(canvasRect, false);
            var containerRect = _panelContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            // Background
            var bg = _panelContainer.AddComponent<Image>();
            bg.color = BgColor;

            // Layout vertical
            var layout = _panelContainer.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 4;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // === Header (titre + bouton fermer) ===
            var header = CreateHorizontalGroup(containerRect, "Header", 32);

            _titleText = CreateText(header.transform, "Title", "Parcelle", 18, AccentColor, FontStyles.Bold);
            var titleLayout = _titleText.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1;

            CreateCloseButton(header.transform);

            // === Separateur ===
            CreateSeparator(containerRect);

            // === Lignes d'info ===
            _communeText = CreateInfoRow(containerRect, "Commune", "-");
            _sectionText = CreateInfoRow(containerRect, "Section", "-");
            _numeroText = CreateInfoRow(containerRect, "Numero", "-");
            _surfaceText = CreateInfoRow(containerRect, "Surface", "-");
            _codeInseeText = CreateInfoRow(containerRect, "Code INSEE", "-");

            _isBuilt = true;
            Debug.Log("[ParcelInfoPanel] UI construite");
        }

        private GameObject CreateHorizontalGroup(Transform parent, string name, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            var layoutElem = go.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = height;

            var hLayout = go.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 8;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = true;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childAlignment = TextAnchor.MiddleLeft;

            return go;
        }

        private TMP_Text CreateText(Transform parent, string name, string text,
            float fontSize, Color color, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            return tmp;
        }

        private void CreateCloseButton(Transform parent)
        {
            var go = new GameObject("CloseButton");
            go.transform.SetParent(parent, false);

            var layoutElem = go.AddComponent<LayoutElement>();
            layoutElem.preferredWidth = 28;
            layoutElem.preferredHeight = 28;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = image;
            btn.onClick.AddListener(Hide);

            var colors = btn.colors;
            colors.highlightedColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 0.8f);
            btn.colors = colors;

            // Texte "X"
            var xText = CreateText(go.transform, "X", "X", 16, TextColor, FontStyles.Bold);
            xText.alignment = TextAlignmentOptions.Center;
            var xRect = xText.GetComponent<RectTransform>();
            xRect.anchorMin = Vector2.zero;
            xRect.anchorMax = Vector2.one;
            xRect.offsetMin = Vector2.zero;
            xRect.offsetMax = Vector2.zero;
        }

        private void CreateSeparator(Transform parent)
        {
            var go = new GameObject("Separator");
            go.transform.SetParent(parent, false);

            var layoutElem = go.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 1;

            var image = go.AddComponent<Image>();
            image.color = SeparatorColor;
        }

        private TMP_Text CreateInfoRow(Transform parent, string label, string value)
        {
            var row = CreateHorizontalGroup(parent, "Row_" + label, 22);

            // Label
            var labelText = CreateText(row.transform, "Label", label, 13, LabelColor);
            var labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 100;

            // Value
            var valueText = CreateText(row.transform, "Value", value, 14, TextColor, FontStyles.Bold);
            var valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
            valueLayout.flexibleWidth = 1;

            return valueText;
        }

        #endregion

        #region MRTK Callbacks

        public void OnMRTKClose()
        {
            Hide();
        }

        #endregion
    }
}
