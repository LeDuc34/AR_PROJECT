using System;
using System.Collections;
using UnityEngine;

namespace GeoscaleCadastre.Search
{
    /// <summary>
    /// Implémente le pattern debounce pour la recherche d'adresse
    /// Évite les requêtes API excessives pendant la frappe
    /// Pattern identique à Geoscale (300ms debounce)
    /// </summary>
    public class SearchDebouncer
    {
        private readonly MonoBehaviour _coroutineRunner;
        private readonly float _delaySeconds;
        private Coroutine _currentCoroutine;
        private Action _pendingAction;

        /// <summary>
        /// Crée un nouveau debouncer
        /// </summary>
        /// <param name="coroutineRunner">MonoBehaviour pour exécuter les coroutines</param>
        /// <param name="delayMs">Délai en millisecondes (défaut: 300ms comme Geoscale)</param>
        public SearchDebouncer(MonoBehaviour coroutineRunner, int delayMs = 300)
        {
            _coroutineRunner = coroutineRunner;
            _delaySeconds = delayMs / 1000f;
        }

        /// <summary>
        /// Exécute une action après le délai de debounce
        /// Annule toute action précédente en attente
        /// </summary>
        /// <param name="action">Action à exécuter</param>
        public void Debounce(Action action)
        {
            // Annuler la recherche précédente (équivalent clearTimeout en JS)
            Cancel();

            _pendingAction = action;
            _currentCoroutine = _coroutineRunner.StartCoroutine(DebounceCoroutine());
        }

        /// <summary>
        /// Annule toute action en attente
        /// </summary>
        public void Cancel()
        {
            if (_currentCoroutine != null)
            {
                _coroutineRunner.StopCoroutine(_currentCoroutine);
                _currentCoroutine = null;
            }
            _pendingAction = null;
        }

        private IEnumerator DebounceCoroutine()
        {
            // Attendre le délai (300ms par défaut)
            yield return new WaitForSeconds(_delaySeconds);

            // Exécuter l'action si elle n'a pas été annulée
            if (_pendingAction != null)
            {
                var action = _pendingAction;
                _pendingAction = null;
                _currentCoroutine = null;
                action.Invoke();
            }
        }

        /// <summary>
        /// Indique si une action est en attente
        /// </summary>
        public bool IsPending
        {
            get { return _pendingAction != null; }
        }
    }
}
