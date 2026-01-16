using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GeoscaleCadastre.Models;
using GeoscaleCadastre.Parcel;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;

namespace GeoscaleCadastre.UI
{
    /// <summary>
    /// Panel d'affichage des informations d'une parcelle sélectionnée
    /// Affiche les données de base: section, numéro, commune, surface
    /// Implémente IMixedRealitySpeechHandler pour les commandes vocales
    /// </summary>
    public class ParcelInfoPanel : MonoBehaviour, IMixedRealitySpeechHandler
    {
        [Header("Références UI - Titre")]
        [SerializeField]
        [Tooltip("Titre du panel (ex: 'Parcelle AB 123')")]
        private TMP_Text _titleText;

        [Header("Références UI - Informations")]
        [SerializeField]
        private TMP_Text _communeText;

        [SerializeField]
        private TMP_Text _sectionText;

        [SerializeField]
        private TMP_Text _numeroText;

        [SerializeField]
        private TMP_Text _surfaceText;

        [SerializeField]
        private TMP_Text _codeInseeText;

        [Header("Références UI - Actions")]
        [SerializeField]
        [Tooltip("Bouton pour fermer le panel")]
        private Button _closeButton;

        [SerializeField]
        [Tooltip("Bouton pour copier l'IDU")]
        private Button _copyIduButton;

        [Header("Services")]
        [SerializeField]
        private ParcelSelectionHandler _selectionHandler;

        [Header("Configuration")]
        [SerializeField]
        private bool _autoShowOnSelection = true;

        [SerializeField]
        private bool _hideOnClear = true;

        [Header("MRTK Speech")]
        [SerializeField]
        [Tooltip("Keyword pour fermer le panel")]
        private string _closeKeyword = "fermer";

        [SerializeField]
        [Tooltip("Keyword pour copier l'IDU")]
        private string _copyKeyword = "copier";

        // Données actuelles
        private ParcelModel _currentParcel;

        // Events
        public event Action OnPanelClosed;
        public event Action<string> OnIduCopied;

        private void Awake()
        {
            // Masquer le panel au départ
            gameObject.SetActive(false);
        }

        private void Start()
        {
            // S'enregistrer auprès du système d'input MRTK pour les commandes vocales
            RegisterSpeechHandler();
        }

        private void OnEnable()
        {
            // S'abonner aux événements de sélection
            if (_selectionHandler != null)
            {
                _selectionHandler.OnParcelSelected += OnParcelSelected;
                _selectionHandler.OnSelectionCleared += OnSelectionCleared;
            }

            // Configurer les boutons
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }

            if (_copyIduButton != null)
            {
                _copyIduButton.onClick.AddListener(CopyIdu);
            }

            // Re-register speech handler when enabled
            RegisterSpeechHandler();
        }

        private void OnDisable()
        {
            if (_selectionHandler != null)
            {
                _selectionHandler.OnParcelSelected -= OnParcelSelected;
                _selectionHandler.OnSelectionCleared -= OnSelectionCleared;
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Hide);
            }

            if (_copyIduButton != null)
            {
                _copyIduButton.onClick.RemoveListener(CopyIdu);
            }

            // Unregister speech handler when disabled
            UnregisterSpeechHandler();
        }

        private void OnDestroy()
        {
            UnregisterSpeechHandler();
        }

        #region Speech Handler Registration

        private void RegisterSpeechHandler()
        {
            if (CoreServices.InputSystem != null)
            {
                CoreServices.InputSystem.RegisterHandler<IMixedRealitySpeechHandler>(this);
            }
        }

        private void UnregisterSpeechHandler()
        {
            if (CoreServices.InputSystem != null)
            {
                CoreServices.InputSystem.UnregisterHandler<IMixedRealitySpeechHandler>(this);
            }
        }

        #endregion

        #region IMixedRealitySpeechHandler Implementation

        void IMixedRealitySpeechHandler.OnSpeechKeywordRecognized(SpeechEventData eventData)
        {
            // Vérifier si le panel est actif
            if (!gameObject.activeInHierarchy)
                return;

            string keyword = eventData.Command.Keyword;

            // Commande "fermer"
            if (keyword.Equals(_closeKeyword, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[ParcelInfoPanel] Commande vocale: Fermer");
                Hide();
                eventData.Use();
            }
            // Commande "copier"
            else if (keyword.Equals(_copyKeyword, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[ParcelInfoPanel] Commande vocale: Copier");
                CopyIdu();
                eventData.Use();
            }
        }

        #endregion

        /// <summary>
        /// Affiche les informations d'une parcelle
        /// </summary>
        /// <param name="parcel">Parcelle à afficher</param>
        public void DisplayParcelInfo(ParcelModel parcel)
        {
            if (parcel == null)
            {
                Debug.LogWarning("[ParcelInfoPanel] Parcelle null");
                return;
            }

            _currentParcel = parcel;

            // Mettre à jour le titre
            if (_titleText != null)
            {
                _titleText.text = string.Format("Parcelle {0}", parcel.GetFormattedId());
            }

            // Mettre à jour les informations
            if (_communeText != null)
            {
                _communeText.text = string.Format("Commune: {0}",
                    string.IsNullOrEmpty(parcel.NomCommune) ? "-" : parcel.NomCommune);
            }

            if (_sectionText != null)
            {
                _sectionText.text = string.Format("Section: {0}",
                    string.IsNullOrEmpty(parcel.Section) ? "-" : parcel.Section);
            }

            if (_numeroText != null)
            {
                _numeroText.text = string.Format("Numéro: {0}",
                    string.IsNullOrEmpty(parcel.Numero) ? "-" : parcel.Numero);
            }

            if (_surfaceText != null)
            {
                _surfaceText.text = string.Format("Surface: {0}", parcel.GetFormattedSurface());
            }

            if (_codeInseeText != null)
            {
                _codeInseeText.text = string.Format("Code INSEE: {0}",
                    string.IsNullOrEmpty(parcel.CodeInsee) ? "-" : parcel.CodeInsee);
            }

            // Afficher le panel
            gameObject.SetActive(true);

            Debug.Log(string.Format("[ParcelInfoPanel] Affichage parcelle: {0}", parcel));
        }

        /// <summary>
        /// Masque le panel
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            _currentParcel = null;

            if (OnPanelClosed != null)
                OnPanelClosed();
        }

        /// <summary>
        /// Copie l'IDU de la parcelle dans le presse-papiers
        /// </summary>
        public void CopyIdu()
        {
            if (_currentParcel != null && !string.IsNullOrEmpty(_currentParcel.Idu))
            {
                GUIUtility.systemCopyBuffer = _currentParcel.Idu;

                if (OnIduCopied != null)
                    OnIduCopied(_currentParcel.Idu);

                Debug.Log(string.Format("[ParcelInfoPanel] IDU copié: {0}", _currentParcel.Idu));
            }
            else
            {
                Debug.LogWarning("[ParcelInfoPanel] Aucun IDU à copier");
            }
        }

        private void OnParcelSelected(ParcelModel parcel)
        {
            if (_autoShowOnSelection)
            {
                DisplayParcelInfo(parcel);
            }
        }

        private void OnSelectionCleared()
        {
            if (_hideOnClear)
            {
                Hide();
            }
        }

        #region MRTK Events (public for manual/Interactable calls)

        /// <summary>
        /// Appelé par MRTK Interactable pour fermer le panel
        /// </summary>
        public void OnMRTKClose()
        {
            Hide();
        }

        /// <summary>
        /// Appelé par MRTK Interactable pour copier l'IDU
        /// </summary>
        public void OnMRTKCopyIdu()
        {
            CopyIdu();
        }

        #endregion
    }
}
