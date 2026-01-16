using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using GeoscaleCadastre.Models;
using GeoscaleCadastre.Map;

namespace GeoscaleCadastre.UI
{
    /// <summary>
    /// Panel d'affichage des résultats de recherche d'adresse
    /// Conçu pour MRTK/HoloLens 2 avec scroll virtuel
    /// </summary>
    public class SearchResultsPanel : MonoBehaviour
    {
        [Header("Références UI")]
        [SerializeField]
        [Tooltip("Prefab pour les items de résultat")]
        private GameObject _resultItemPrefab;

        [SerializeField]
        [Tooltip("Container pour les items")]
        private Transform _resultsContainer;

        [SerializeField]
        [Tooltip("Texte affiché quand aucun résultat")]
        private TMP_Text _noResultsText;

        [SerializeField]
        [Tooltip("Texte d'erreur")]
        private TMP_Text _errorText;

        [Header("Services")]
        [SerializeField]
        private MapManager _mapManager;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Nombre maximum de résultats affichés")]
        private int _maxDisplayedResults = 5;

        [SerializeField]
        [Tooltip("Message quand aucun résultat")]
        private string _noResultsMessage = "Aucun résultat trouvé";

        // Items instanciés
        private List<GameObject> _resultItems = new List<GameObject>();
        private List<AddressResult> _currentResults;

        // Events
        public event Action<AddressResult> OnAddressSelected;

        private void Awake()
        {
            // S'assurer que le panel est masqué au départ
            gameObject.SetActive(false);

            // Masquer les textes d'erreur/no results
            if (_noResultsText != null)
                _noResultsText.gameObject.SetActive(false);

            if (_errorText != null)
                _errorText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Affiche les résultats de recherche
        /// </summary>
        /// <param name="results">Liste des résultats</param>
        public void DisplayResults(List<AddressResult> results)
        {
            // Nettoyer les anciens résultats
            ClearResults();

            // Masquer les messages d'erreur
            if (_errorText != null)
                _errorText.gameObject.SetActive(false);

            _currentResults = results;

            if (results == null || results.Count == 0)
            {
                // Aucun résultat
                if (_noResultsText != null)
                {
                    _noResultsText.text = _noResultsMessage;
                    _noResultsText.gameObject.SetActive(true);
                }

                gameObject.SetActive(true);
                return;
            }

            // Masquer le message "aucun résultat"
            if (_noResultsText != null)
                _noResultsText.gameObject.SetActive(false);

            // Créer les items de résultat
            int count = Mathf.Min(results.Count, _maxDisplayedResults);
            for (int i = 0; i < count; i++)
            {
                CreateResultItem(results[i], i);
            }

            // Afficher le panel
            gameObject.SetActive(true);

            Debug.Log(string.Format("[SearchResultsPanel] Affichage de {0} résultats", count));
        }

        /// <summary>
        /// Affiche un message d'erreur
        /// </summary>
        /// <param name="error">Message d'erreur</param>
        public void ShowError(string error)
        {
            ClearResults();

            if (_noResultsText != null)
                _noResultsText.gameObject.SetActive(false);

            if (_errorText != null)
            {
                _errorText.text = error;
                _errorText.gameObject.SetActive(true);
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Masque le panel
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Efface tous les résultats
        /// </summary>
        public void ClearResults()
        {
            foreach (var item in _resultItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            _resultItems.Clear();
            _currentResults = null;
        }

        private void CreateResultItem(AddressResult result, int index)
        {
            if (_resultItemPrefab == null || _resultsContainer == null)
            {
                Debug.LogError("[SearchResultsPanel] Prefab ou container non assigné");
                return;
            }

            // Instancier le prefab
            var itemObj = Instantiate(_resultItemPrefab, _resultsContainer);
            itemObj.name = string.Format("Result_{0}", index);

            // Configurer l'item
            var resultItem = itemObj.GetComponent<SearchResultItem>();
            if (resultItem != null)
            {
                resultItem.Setup(result, OnResultSelected);
            }
            else
            {
                // Fallback: configurer manuellement si pas de SearchResultItem
                SetupResultItemManually(itemObj, result);
            }

            _resultItems.Add(itemObj);
        }

        private void SetupResultItemManually(GameObject itemObj, AddressResult result)
        {
            // Chercher les composants texte
            var texts = itemObj.GetComponentsInChildren<TMP_Text>();

            if (texts.Length > 0)
            {
                texts[0].text = result.Text;
            }

            if (texts.Length > 1)
            {
                texts[1].text = result.Context;
            }

            // Ajouter un handler de clic
            var button = itemObj.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => OnResultSelected(result));
            }
        }

        private void OnResultSelected(AddressResult result)
        {
            Debug.Log(string.Format("[SearchResultsPanel] Adresse sélectionnée: {0}", result.Text));

            // Naviguer vers l'adresse (pattern Geoscale handleAddressSelection)
            if (_mapManager != null)
            {
                _mapManager.CenterOnAddress(result, 18f, 2f);
            }

            // Notifier les listeners
            if (OnAddressSelected != null)
                OnAddressSelected(result);

            // Masquer le panel
            Hide();
        }

        private void OnDestroy()
        {
            ClearResults();
        }
    }
}
