using System;
using UnityEngine;

namespace GeoscaleCadastre.Models
{
    /// <summary>
    /// Modèle représentant un résultat de recherche d'adresse
    /// Compatible avec les APIs Mapbox et Nominatim
    /// </summary>
    [Serializable]
    public class AddressResult
    {
        /// <summary>Texte principal de l'adresse</summary>
        public string Text;

        /// <summary>Contexte additionnel (ville, région, code postal)</summary>
        public string Context;

        /// <summary>Latitude</summary>
        public double Latitude;

        /// <summary>Longitude</summary>
        public double Longitude;

        /// <summary>Type de résultat (address, poi, place, etc.)</summary>
        public string PlaceType;

        /// <summary>Source de la donnée (mapbox, nominatim)</summary>
        public string Source;

        /// <summary>
        /// Retourne les coordonnées sous forme de Vector2d (lat, lng)
        /// </summary>
        public Vector2 GetCoordinates()
        {
            return new Vector2((float)Longitude, (float)Latitude);
        }

        /// <summary>
        /// Affichage formaté pour l'UI
        /// </summary>
        public string GetDisplayText()
        {
            if (string.IsNullOrEmpty(Context))
                return Text;
            return string.Format("{0}, {1}", Text, Context);
        }

        public override string ToString()
        {
            return string.Format("[AddressResult] {0} ({1}, {2})", Text, Latitude, Longitude);
        }
    }
}
