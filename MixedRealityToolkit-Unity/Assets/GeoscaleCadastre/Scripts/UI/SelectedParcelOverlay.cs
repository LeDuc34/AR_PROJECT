using UnityEngine;
using TMPro;
using GeoscaleCadastre.Models;
using GeoscaleCadastre.Parcel;

namespace GeoscaleCadastre.UI
{
    /// <summary>
    /// Overlay compact affichant les infos de la parcelle sélectionnée
    /// Positionné en coin inférieur gauche, se masque automatiquement si aucune sélection
    /// </summary>
    public class SelectedParcelOverlay : MonoBehaviour
    {
        [Header("Références UI")]
        [SerializeField]
        [Tooltip("Conteneur principal de l'overlay (pour show/hide)")]
        private GameObject _overlayContainer;

        [SerializeField]
        [Tooltip("Texte de la référence cadastrale (Section + Numéro)")]
        private TMP_Text _referenceText;

        [SerializeField]
        [Tooltip("Texte de la surface")]
        private TMP_Text _surfaceText;

        [SerializeField]
        [Tooltip("Texte de la commune")]
        private TMP_Text _communeText;

        [Header("Services")]
        [SerializeField]
        private ParcelSelectionHandler _selectionHandler;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Masquer l'overlay au démarrage")]
        private bool _hideOnStart = true;

        private void Awake()
        {
            if (_hideOnStart && _overlayContainer != null)
            {
                _overlayContainer.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_selectionHandler != null)
            {
                _selectionHandler.OnParcelSelected += OnParcelSelected;
                _selectionHandler.OnSelectionCleared += OnSelectionCleared;

                // Si une parcelle est déjà sélectionnée, l'afficher
                if (_selectionHandler.SelectedParcel != null)
                {
                    DisplayParcel(_selectionHandler.SelectedParcel);
                }
            }
        }

        private void OnDisable()
        {
            if (_selectionHandler != null)
            {
                _selectionHandler.OnParcelSelected -= OnParcelSelected;
                _selectionHandler.OnSelectionCleared -= OnSelectionCleared;
            }
        }

        private void OnParcelSelected(ParcelModel parcel)
        {
            DisplayParcel(parcel);
        }

        private void OnSelectionCleared()
        {
            Hide();
        }

        /// <summary>
        /// Affiche les informations de la parcelle dans l'overlay
        /// </summary>
        public void DisplayParcel(ParcelModel parcel)
        {
            if (parcel == null)
            {
                Hide();
                return;
            }

            // Mettre à jour les textes
            if (_referenceText != null)
            {
                _referenceText.text = parcel.GetFormattedId();
            }

            if (_surfaceText != null)
            {
                _surfaceText.text = parcel.GetFormattedSurface();
            }

            if (_communeText != null)
            {
                _communeText.text = string.IsNullOrEmpty(parcel.NomCommune) ? "-" : parcel.NomCommune;
            }

            // Afficher l'overlay
            if (_overlayContainer != null)
            {
                _overlayContainer.SetActive(true);
            }

            Debug.Log(string.Format("[SelectedParcelOverlay] Affichage: {0} - {1} - {2}",
                parcel.GetFormattedId(), parcel.GetFormattedSurface(), parcel.NomCommune));
        }

        /// <summary>
        /// Masque l'overlay
        /// </summary>
        public void Hide()
        {
            if (_overlayContainer != null)
            {
                _overlayContainer.SetActive(false);
            }
        }

        /// <summary>
        /// Affiche ou masque l'overlay
        /// </summary>
        public void Toggle()
        {
            if (_overlayContainer != null)
            {
                _overlayContainer.SetActive(!_overlayContainer.activeSelf);
            }
        }
    }
}
