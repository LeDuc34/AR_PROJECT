using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GeoscaleCadastre.Models;
using Microsoft.MixedReality.Toolkit.Input;

namespace GeoscaleCadastre.UI
{
    /// <summary>
    /// Composant pour un item de résultat de recherche
    /// Affiche le texte de l'adresse et gère l'interaction
    /// Implémente IMixedRealityFocusHandler pour les interactions MRTK
    /// </summary>
    public class SearchResultItem : MonoBehaviour, IMixedRealityFocusHandler
    {
        [Header("Références UI")]
        [SerializeField]
        [Tooltip("Texte principal de l'adresse")]
        private TMP_Text _mainText;

        [SerializeField]
        [Tooltip("Texte secondaire (contexte)")]
        private TMP_Text _contextText;

        [SerializeField]
        [Tooltip("Icône de localisation")]
        private Image _locationIcon;

        [SerializeField]
        [Tooltip("Bouton de l'item")]
        private Button _button;

        [Header("Style")]
        [SerializeField]
        private Color _normalColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        [SerializeField]
        private Color _hoverColor = new Color(0, 0.9f, 0.75f, 0.1f); // #00E5BE avec transparence

        [SerializeField]
        private Color _textColor = new Color(0.93f, 0.9f, 0.89f, 1f); // #EDE6E3

        [SerializeField]
        private Color _contextColor = new Color(0.93f, 0.9f, 0.89f, 0.5f);

        // Données
        private AddressResult _addressResult;
        private Action<AddressResult> _onSelected;
        private Image _backgroundImage;

        private void Awake()
        {
            _backgroundImage = GetComponent<Image>();

            // Configurer le bouton
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button != null)
            {
                _button.onClick.AddListener(OnClicked);
            }

            // Appliquer les couleurs
            ApplyColors();
        }

        /// <summary>
        /// Configure l'item avec les données d'une adresse
        /// </summary>
        /// <param name="result">Résultat de recherche</param>
        /// <param name="onSelected">Callback de sélection</param>
        public void Setup(AddressResult result, Action<AddressResult> onSelected)
        {
            _addressResult = result;
            _onSelected = onSelected;

            // Mettre à jour les textes
            if (_mainText != null)
            {
                _mainText.text = result.Text;
            }

            if (_contextText != null)
            {
                _contextText.text = result.Context;
                _contextText.gameObject.SetActive(!string.IsNullOrEmpty(result.Context));
            }

            // Configurer l'icône selon le type
            UpdateIcon(result.PlaceType);
        }

        private void ApplyColors()
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = _normalColor;
            }

            if (_mainText != null)
            {
                _mainText.color = _textColor;
            }

            if (_contextText != null)
            {
                _contextText.color = _contextColor;
            }
        }

        private void UpdateIcon(string placeType)
        {
            if (_locationIcon == null) return;

            // On pourrait changer l'icône selon le type (address, poi, place, etc.)
            // Pour l'instant, on utilise une icône générique de localisation
            _locationIcon.color = new Color(0, 0.9f, 0.75f, 1f); // #00E5BE
        }

        private void OnClicked()
        {
            if (_addressResult != null && _onSelected != null)
            {
                _onSelected(_addressResult);
            }
        }

        #region IMixedRealityFocusHandler Implementation

        /// <summary>
        /// Appelé par MRTK quand le focus (regard/pointeur) entre sur l'item
        /// </summary>
        void IMixedRealityFocusHandler.OnFocusEnter(FocusEventData eventData)
        {
            OnFocusEnter();
        }

        /// <summary>
        /// Appelé par MRTK quand le focus (regard/pointeur) quitte l'item
        /// </summary>
        void IMixedRealityFocusHandler.OnFocusExit(FocusEventData eventData)
        {
            OnFocusExit();
        }

        #endregion

        #region Focus Handlers (public for manual calls)

        /// <summary>
        /// Appelé quand le focus entre sur l'item
        /// </summary>
        public void OnFocusEnter()
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = _hoverColor;
            }
        }

        /// <summary>
        /// Appelé quand le focus quitte l'item
        /// </summary>
        public void OnFocusExit()
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = _normalColor;
            }
        }

        /// <summary>
        /// Appelé par MRTK Interactable lors d'une sélection (pinch, tap, etc.)
        /// </summary>
        public void OnSelect()
        {
            OnClicked();
        }

        #endregion

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
            }
        }
    }
}
