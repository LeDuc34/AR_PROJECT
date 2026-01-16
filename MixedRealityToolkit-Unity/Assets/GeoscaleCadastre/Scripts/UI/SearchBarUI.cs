using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GeoscaleCadastre.Models;
using GeoscaleCadastre.Search;
using Microsoft.MixedReality.Toolkit.Experimental.UI;

namespace GeoscaleCadastre.UI
{
    /// <summary>
    /// Interface utilisateur pour la barre de recherche d'adresse
    /// Conçu pour MRTK et HoloLens 2 (Near Menu ou Hand Menu)
    /// </summary>
    public class SearchBarUI : MonoBehaviour
    {
        [Header("Références UI")]
        [SerializeField]
        [Tooltip("Champ de saisie de texte (TMP ou MRTK InputField)")]
        private TMP_InputField _inputField;

        [SerializeField]
        [Tooltip("Bouton de recherche (optionnel)")]
        private Button _searchButton;

        [SerializeField]
        [Tooltip("Bouton d'effacement")]
        private Button _clearButton;

        [SerializeField]
        [Tooltip("Indicateur de chargement")]
        private GameObject _loadingIndicator;

        [Header("MRTK Keyboard")]
        [SerializeField]
        [Tooltip("Composant MixedRealityKeyboard pour HoloLens 2")]
        private MixedRealityKeyboard _mrtkKeyboard;

        [SerializeField]
        [Tooltip("Utiliser le clavier MRTK au lieu du clavier standard")]
        private bool _useMRTKKeyboard = true;

        [Header("Services")]
        [SerializeField]
        private AddressSearchService _searchService;

        [SerializeField]
        private SearchResultsPanel _resultsPanel;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Placeholder du champ de recherche")]
        private string _placeholder = "Rechercher une adresse...";

        // État du clavier MRTK
        private bool _isKeyboardOpen;

        private void Awake()
        {
            SetupUI();
        }

        private void OnEnable()
        {
            // S'abonner aux événements du champ de saisie
            if (_inputField != null)
            {
                _inputField.onValueChanged.AddListener(OnInputChanged);
                _inputField.onSubmit.AddListener(OnInputSubmit);

                // Si on utilise le clavier MRTK, intercepter le focus
                if (_useMRTKKeyboard)
                {
                    _inputField.onSelect.AddListener(OnInputFieldSelected);
                }
            }

            if (_searchButton != null)
            {
                _searchButton.onClick.AddListener(OnSearchButtonClicked);
            }

            if (_clearButton != null)
            {
                _clearButton.onClick.AddListener(OnClearButtonClicked);
            }

            // S'abonner aux événements du service de recherche
            if (_searchService != null)
            {
                _searchService.OnSearchStarted += OnSearchStarted;
                _searchService.OnSearchCompleted += OnSearchCompleted;
                _searchService.OnSearchResults += OnResultsReceived;
                _searchService.OnSearchError += OnSearchError;
            }

            // S'abonner aux événements du clavier MRTK
            if (_mrtkKeyboard != null)
            {
                _mrtkKeyboard.OnShowKeyboard.AddListener(OnMRTKKeyboardShown);
                _mrtkKeyboard.OnHideKeyboard.AddListener(OnMRTKKeyboardHidden);
                _mrtkKeyboard.OnCommitText.AddListener(OnMRTKKeyboardCommit);
            }
        }

        private void OnDisable()
        {
            if (_inputField != null)
            {
                _inputField.onValueChanged.RemoveListener(OnInputChanged);
                _inputField.onSubmit.RemoveListener(OnInputSubmit);

                if (_useMRTKKeyboard)
                {
                    _inputField.onSelect.RemoveListener(OnInputFieldSelected);
                }
            }

            if (_searchButton != null)
            {
                _searchButton.onClick.RemoveListener(OnSearchButtonClicked);
            }

            if (_clearButton != null)
            {
                _clearButton.onClick.RemoveListener(OnClearButtonClicked);
            }

            if (_searchService != null)
            {
                _searchService.OnSearchStarted -= OnSearchStarted;
                _searchService.OnSearchCompleted -= OnSearchCompleted;
                _searchService.OnSearchResults -= OnResultsReceived;
                _searchService.OnSearchError -= OnSearchError;
            }

            if (_mrtkKeyboard != null)
            {
                _mrtkKeyboard.OnShowKeyboard.RemoveListener(OnMRTKKeyboardShown);
                _mrtkKeyboard.OnHideKeyboard.RemoveListener(OnMRTKKeyboardHidden);
                _mrtkKeyboard.OnCommitText.RemoveListener(OnMRTKKeyboardCommit);
            }
        }

        private void Update()
        {
            // Synchroniser le texte pendant la saisie sur le clavier MRTK
            if (_isKeyboardOpen && _mrtkKeyboard != null && _inputField != null)
            {
                string keyboardText = _mrtkKeyboard.Text;
                if (_inputField.text != keyboardText)
                {
                    _inputField.text = keyboardText;
                    // Ne pas déclencher la recherche ici, attendre OnCommit
                }
            }
        }

        private void SetupUI()
        {
            // Configurer le placeholder
            if (_inputField != null && !string.IsNullOrEmpty(_placeholder))
            {
                var placeholderText = _inputField.placeholder as TMP_Text;
                if (placeholderText != null)
                {
                    placeholderText.text = _placeholder;
                }
            }

            // Masquer l'indicateur de chargement
            if (_loadingIndicator != null)
            {
                _loadingIndicator.SetActive(false);
            }

            // Mettre à jour la visibilité du bouton clear
            UpdateClearButtonVisibility();
        }

        /// <summary>
        /// Ouvre le clavier MRTK pour la saisie
        /// </summary>
        public void OpenMRTKKeyboard()
        {
            if (_mrtkKeyboard != null && !_isKeyboardOpen)
            {
                string initialText = _inputField != null ? _inputField.text : "";
                _mrtkKeyboard.ShowKeyboard(initialText, false);
            }
        }

        /// <summary>
        /// Ferme le clavier MRTK
        /// </summary>
        public void CloseMRTKKeyboard()
        {
            if (_mrtkKeyboard != null && _isKeyboardOpen)
            {
                _mrtkKeyboard.HideKeyboard();
            }
        }

        /// <summary>
        /// Définit le focus sur le champ de recherche
        /// </summary>
        public void Focus()
        {
            if (_useMRTKKeyboard)
            {
                OpenMRTKKeyboard();
            }
            else if (_inputField != null)
            {
                _inputField.Select();
                _inputField.ActivateInputField();
            }
        }

        /// <summary>
        /// Efface le champ de recherche
        /// </summary>
        public void Clear()
        {
            if (_inputField != null)
            {
                _inputField.text = "";
            }

            if (_searchService != null)
            {
                _searchService.ClearResults();
            }

            UpdateClearButtonVisibility();
        }

        /// <summary>
        /// Définit le texte de recherche programmatiquement
        /// </summary>
        public void SetSearchText(string text)
        {
            if (_inputField != null)
            {
                _inputField.text = text;
            }
        }

        private void OnInputFieldSelected(string text)
        {
            // Quand l'InputField est sélectionné et qu'on utilise MRTK Keyboard
            if (_useMRTKKeyboard)
            {
                // Désactiver le clavier Unity standard
                if (_inputField != null)
                {
                    _inputField.DeactivateInputField();
                }

                // Ouvrir le clavier MRTK
                OpenMRTKKeyboard();
            }
        }

        private void OnInputChanged(string query)
        {
            // Déclencher la recherche avec debounce (seulement si pas en mode clavier MRTK)
            if (!_isKeyboardOpen && _searchService != null)
            {
                _searchService.Search(query);
            }

            UpdateClearButtonVisibility();
        }

        private void OnInputSubmit(string query)
        {
            // Recherche immédiate sur Enter/Submit
            if (_searchService != null && !string.IsNullOrEmpty(query))
            {
                _searchService.Search(query);
            }
        }

        private void OnSearchButtonClicked()
        {
            if (_inputField != null && _searchService != null)
            {
                _searchService.Search(_inputField.text);
            }
        }

        private void OnClearButtonClicked()
        {
            Clear();
        }

        private void OnSearchStarted()
        {
            if (_loadingIndicator != null)
            {
                _loadingIndicator.SetActive(true);
            }
        }

        private void OnSearchCompleted()
        {
            if (_loadingIndicator != null)
            {
                _loadingIndicator.SetActive(false);
            }
        }

        private void OnResultsReceived(List<AddressResult> results)
        {
            // Transmettre les résultats au panel de résultats
            if (_resultsPanel != null)
            {
                _resultsPanel.DisplayResults(results);
            }
        }

        private void OnSearchError(string error)
        {
            Debug.LogWarning(string.Format("[SearchBarUI] Erreur de recherche: {0}", error));

            // Afficher l'erreur dans le panel de résultats
            if (_resultsPanel != null)
            {
                _resultsPanel.ShowError(error);
            }
        }

        private void UpdateClearButtonVisibility()
        {
            if (_clearButton != null && _inputField != null)
            {
                _clearButton.gameObject.SetActive(!string.IsNullOrEmpty(_inputField.text));
            }
        }

        #region MRTK Keyboard Events

        private void OnMRTKKeyboardShown()
        {
            _isKeyboardOpen = true;
            Debug.Log("[SearchBarUI] Clavier MRTK ouvert");
        }

        private void OnMRTKKeyboardHidden()
        {
            _isKeyboardOpen = false;
            Debug.Log("[SearchBarUI] Clavier MRTK fermé");
        }

        private void OnMRTKKeyboardCommit()
        {
            // Le texte a été validé par l'utilisateur
            if (_mrtkKeyboard != null)
            {
                string text = _mrtkKeyboard.Text;

                if (_inputField != null)
                {
                    _inputField.text = text;
                }

                // Déclencher la recherche
                if (_searchService != null && !string.IsNullOrEmpty(text))
                {
                    _searchService.Search(text);
                }

                Debug.Log(string.Format("[SearchBarUI] Texte validé: {0}", text));
            }
        }

        #endregion
    }
}
