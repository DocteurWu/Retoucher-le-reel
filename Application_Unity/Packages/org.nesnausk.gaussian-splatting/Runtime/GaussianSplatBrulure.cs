// =====================================================================================
//  GaussianSplatBrulure.cs — AJOUT « Retoucher le réel » AU PLUGIN D'ARAS-P
//
//  Paramètres GLOBAUX de l'effet « brûlure », lus par le noyau CSCalcViewData de
//  SplatUtilities.compute pour TOUS les GaussianSplatRenderer (décor et objets).
//  Le plugin ne fait que les transmettre au shader ; c'est le jeu
//  (RemodelerLeReel.VR.VRBrulure) qui les anime.
// =====================================================================================
using UnityEngine;

namespace GaussianSplatting.Runtime
{
    public static class GaussianSplatBrulure
    {
        /// <summary>xyz : foyer (monde) ; w : secondes depuis le départ (&lt; 0 : effet coupé).</summary>
        public static Vector4 Foyer = new Vector4(0, 0, 0, -1);

        /// <summary>x : portée (m) ; y : constante de temps de la brûlure (s) ;
        /// z : délai avant la reconstruction (s, &lt; 0 : jamais) ; w : constante de temps de la reconstruction (s).</summary>
        public static Vector4 Forme = new Vector4(14f, 1f, -1f, 0.6f);

        /// <summary>Taille d'un point (m) par mètre de distance à la caméra.</summary>
        public static float TaillePoint = 0.004f;

        internal static readonly int PropFoyer = Shader.PropertyToID("_BrulureFoyer");
        internal static readonly int PropForme = Shader.PropertyToID("_BrulureForme");
        internal static readonly int PropTaillePoint = Shader.PropertyToID("_BrulureTaillePoint");
    }
}
