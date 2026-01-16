using System;
using UnityEngine;

namespace GeoscaleCadastre.Models
{
    /// <summary>
    /// Modèle représentant une parcelle cadastrale
    /// Données provenant de l'API Carto IGN
    /// </summary>
    [Serializable]
    public class ParcelModel
    {
        /// <summary>Identifiant unique de la parcelle (IDU)</summary>
        public string Idu;

        /// <summary>Code INSEE de la commune</summary>
        public string CodeInsee;

        /// <summary>Nom de la commune</summary>
        public string NomCommune;

        /// <summary>Section cadastrale</summary>
        public string Section;

        /// <summary>Numéro de la parcelle</summary>
        public string Numero;

        /// <summary>Préfixe cadastral</summary>
        public string Prefixe;

        /// <summary>Surface en m²</summary>
        public float Surface;

        /// <summary>Centroïde de la parcelle (lat, lng)</summary>
        public Vector2 Centroid;

        /// <summary>Bounding box de la parcelle</summary>
        public Bounds BoundingBox;

        /// <summary>Points du polygone de la parcelle</summary>
        public Vector2[] Geometry;

        /// <summary>Source des données</summary>
        public string TypeSource;

        /// <summary>Date de mise à jour</summary>
        public DateTime DateMaj;

        /// <summary>
        /// Retourne l'identifiant formaté (Section + Numéro)
        /// </summary>
        public string GetFormattedId()
        {
            return string.Format("{0} {1}", Section, Numero);
        }

        /// <summary>
        /// Retourne la surface formatée avec unité
        /// </summary>
        public string GetFormattedSurface()
        {
            if (Surface >= 10000)
                return string.Format("{0:N2} ha", Surface / 10000f);
            return string.Format("{0:N0} m²", Surface);
        }

        /// <summary>
        /// Calcule la dimension maximale de la parcelle (pour auto-zoom)
        /// </summary>
        public float GetMaxDimension()
        {
            return Mathf.Max(BoundingBox.size.x, BoundingBox.size.z);
        }

        public override string ToString()
        {
            return string.Format("[Parcelle] {0} {1} - {2} ({3})",
                Section, Numero, NomCommune, GetFormattedSurface());
        }
    }
}
