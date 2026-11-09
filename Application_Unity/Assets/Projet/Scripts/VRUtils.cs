// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRUtils.cs — PETITS OUTILS PARTAGÉS (hiérarchie, matériaux, physique)
//
//  RÔLE
//    Regroupe des fonctions auparavant recopiées dans plusieurs scripts :
//      - FindDeep : recherche RÉCURSIVE d'un enfant par nom. Les os des visuels
//        de mains (L_IndexTip, L_Palm…) sont IMBRIQUÉS : un transform.Find
//        avec chemin fixe (« LeftHand/L_IndexTip ») renvoie toujours null ;
//      - CreateUnlitMaterial : matériau non éclairé pour les visuels créés par
//        code (laser, arc, réticule, bulle, pastilles) ;
//      - MoveRig : déplacement du rig + Physics.SyncTransforms (le projet a
//        autoSyncTransforms = 0, les colliders des objets tenus doivent suivre).
// =====================================================================================
using UnityEngine;
using UnityEngine.XR;

namespace RemodelerLeReel.VR
{
    public static class VRUtils
    {
        /// <summary>
        /// Cherche un descendant (ou la racine elle-même) portant exactement ce nom,
        /// à n'importe quelle profondeur. Null si introuvable.
        /// </summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Shader non éclairé disponible en build : « Sprites/Default » est toujours
        /// inclus (Always Included Shaders), URP/Unlit en secours.
        /// </summary>
        public static Shader UnlitShader
        {
            get
            {
                if (s_Unlit == null)
                    s_Unlit = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                return s_Unlit;
            }
        }
        private static Shader s_Unlit;

        /// <summary>Nouveau matériau non éclairé de la couleur donnée.</summary>
        public static Material CreateUnlitMaterial(Color color)
        {
            return new Material(UnlitShader) { color = color };
        }

        /// <summary>Main opposée (LeftHand ↔ RightHand).</summary>
        public static XRNode OtherHand(XRNode node)
        {
            return node == XRNode.LeftHand ? XRNode.RightHand : XRNode.LeftHand;
        }

        /// <summary>
        /// Déplace le rig en monde puis synchronise la physique : avec
        /// autoSyncTransforms = 0, les colliders des objets tenus (enfants des mains)
        /// resteraient sinon à l'ancienne position jusqu'au pas de physique suivant.
        /// </summary>
        public static void MoveRig(Transform rig, Vector3 newPosition)
        {
            if (rig == null) return;
            rig.position = newPosition;
            Physics.SyncTransforms();
        }
    }
}
