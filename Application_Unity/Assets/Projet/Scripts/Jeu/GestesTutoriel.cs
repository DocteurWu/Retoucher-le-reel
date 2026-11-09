// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  GestesTutoriel.cs — LES DÉMONSTRATIONS JOUÉES PAR LES MAINS FANTÔMES
//
//  RÔLE
//    Décrit chaque geste du jeu comme un petit film en boucle : des images clés pour
//    chaque main (position, orientation, pose des doigts) et pour les accessoires
//    (cube, bulle du poignet). Ce ne sont que des données : TutorielGestes les joue,
//    MainFantome les affiche.
//
//  REPÈRE DES CLÉS (m) : l'ANCRE de démonstration, devant le joueur. x = à sa droite,
//    y = vers le haut, z = droit devant lui. La rotation s'applique au POIGNET fantôme :
//    doigts vers +z, dos de la main vers +y, paume vers -y (convention XR Hands).
//    La démonstration est vue à la première personne : mêmes mains, même sens que le
//    joueur, jamais en miroir.
//
//  RETOUCHER UN GESTE : changer les valeurs ci-dessous. Ajouter un geste : une nouvelle
//    méthode, puis l'utiliser dans une étape de TutorielGestes.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Poses des doigts d'une main fantôme (calculées par MainFantome).</summary>
    public enum PoseMain
    {
        Ouverte,
        Poing,
        /// <summary>Pouce contre le bout de l'index.</summary>
        Pince,
        /// <summary>Index tendu, autres doigts repliés.</summary>
        Pointe,
        /// <summary>Poing lâche, pouce levé (microgeste, avant le tap).</summary>
        MicroLeve,
        /// <summary>Poing lâche, pouce posé sur le côté de l'index (le tap).</summary>
        MicroTap,
        /// <summary>Doigts à demi pliés, la main repose sur leurs bouts (main guide qui marche).</summary>
        Griffe,
        /// <summary>Signe du V / extraterrestre : index et majeur dressés en V, autres doigts repliés.</summary>
        SigneV
    }

    /// <summary>Gestes enseignés par le tutoriel (TutorielGestes) et demandés par les épreuves (Epreuve.GestesRequis).</summary>
    public enum GesteTutoriel
    {
        Saisir,
        Agrandir,
        Ranger,
        Dupliquer,
        Recuperer,
        Supprimer,
        Deplacer
    }

    /// <summary>Forme d'un accessoire de démonstration.</summary>
    public enum FormeAccessoire
    {
        Cube,
        Bulle,
        /// <summary>Disque posé au sol : là où la téléportation emmène.</summary>
        Cible
    }

    /// <summary>Image clé d'une main.</summary>
    public struct CleMain
    {
        public float temps;
        public Vector3 position;
        public Quaternion rotation;
        public PoseMain pose;
    }

    /// <summary>Image clé d'un accessoire. Échelle 0 = invisible.</summary>
    public struct CleAccessoire
    {
        public float temps;
        public Vector3 position;
        public float echelle;
    }

    /// <summary>Suite d'images clés, triées par temps, jouée avec un départ et une arrivée en douceur.</summary>
    public abstract class Piste<T>
    {
        public readonly List<T> cles = new List<T>();

        protected abstract float Temps(T cle);

        /// <summary>Les deux clés qui encadrent ce temps et l'avancement (lissé, 0..1) de l'une à l'autre.</summary>
        public void Encadrer(float temps, out T a, out T b, out float avancement)
        {
            a = b = cles[0];
            avancement = 0f;
            if (temps <= Temps(cles[0])) return;

            for (int i = 1; i < cles.Count; i++)
            {
                b = cles[i];
                if (temps > Temps(b))
                {
                    a = b;
                    continue;
                }
                avancement = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Temps(a), Temps(b), temps));
                return;
            }
        }
    }

    /// <summary>Le film d'une main.</summary>
    public class PisteMain : Piste<CleMain>
    {
        protected override float Temps(CleMain cle) => cle.temps;

        /// <summary>Ajoute une image clé (appels à la suite, par temps croissant).</summary>
        public PisteMain Cle(float temps, Vector3 position, Quaternion rotation, PoseMain pose)
        {
            cles.Add(new CleMain { temps = temps, position = position, rotation = rotation, pose = pose });
            return this;
        }
    }

    /// <summary>Le film d'un accessoire.</summary>
    public class PisteAccessoire : Piste<CleAccessoire>
    {
        public readonly FormeAccessoire forme;

        public PisteAccessoire(FormeAccessoire forme)
        {
            this.forme = forme;
        }

        protected override float Temps(CleAccessoire cle) => cle.temps;

        /// <summary>Ajoute une image clé (appels à la suite, par temps croissant).</summary>
        public PisteAccessoire Cle(float temps, Vector3 position, float echelle)
        {
            cles.Add(new CleAccessoire { temps = temps, position = position, echelle = echelle });
            return this;
        }
    }

    /// <summary>Une démonstration complète : une ou deux mains, des accessoires.</summary>
    public class ClipGeste
    {
        public float duree;
        public PisteMain gauche, droite;
        public readonly List<PisteAccessoire> accessoires = new List<PisteAccessoire>();
    }

    /// <summary>Le catalogue des démonstrations.</summary>
    public static class GestesTutoriel
    {
        private const float TailleCube = 0.1f;
        private const float TailleCubeRange = 0.04f;
        private const float TailleBulle = 0.11f;

        private static readonly Quaternion PaumeBas = Quaternion.identity;
        private static readonly Quaternion GaucheVersLeCentre = Quaternion.Euler(0f, 0f, 90f);
        private static readonly Quaternion DroiteVersLeCentre = Quaternion.Euler(0f, 0f, -90f);
        /// <summary>Main gauche levée, doigts vers le haut, paume vers le visage (ouvre le menu du poignet).</summary>
        private static readonly Quaternion PaumeVersVisage = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        /// <summary>Main droite qui pointe vers la gauche du joueur, paume vers le bas.</summary>
        private static readonly Quaternion PointeVersGauche = Quaternion.LookRotation(Vector3.left, Vector3.up);

        private static readonly Vector3 CubePose = new Vector3(0f, 0f, 0.12f);
        private static readonly Vector3 DroiteLoin = new Vector3(0.2f, -0.03f, -0.06f);
        private static readonly Vector3 PoignetMenu = new Vector3(-0.12f, -0.05f, 0f);
        private static readonly Vector3 Bulle = new Vector3(-0.13f, 0.2f, 0f);

        // Bouts des doigts de la main gauche en position « menu », mesurés sur le squelette du visuel.
        private static readonly Vector3 BoutIndexGauche = new Vector3(-0.155f, 0.129f, -0.005f);
        private static readonly Vector3 BoutMajeurGauche = new Vector3(-0.118f, 0.14f, -0.014f);
        private static readonly Vector3 BoutAnnulaireGauche = new Vector3(-0.1f, 0.125f, -0.01f);

        /// <summary>Position du poignet droit (pose Pointe, PointeVersGauche) pour que le bout de l'index soit sur ce point.</summary>
        private static Vector3 PoignetPourToucher(Vector3 point)
        {
            return point + new Vector3(0.179f, 0.005f, 0.035f);
        }

        /// <summary>Approcher, fermer la main, soulever, ouvrir.</summary>
        public static ClipGeste Saisir()
        {
            Vector3 main = new Vector3(0f, 0.05f, 0.03f);
            Vector3 haut = Vector3.up * 0.12f;
            ClipGeste clip = new ClipGeste { duree = 4f };
            clip.droite = new PisteMain()
                .Cle(0f, DroiteLoin, PaumeBas, PoseMain.Ouverte)
                .Cle(1f, main, PaumeBas, PoseMain.Ouverte)
                .Cle(1.5f, main, PaumeBas, PoseMain.Poing)
                .Cle(2.5f, main + haut, PaumeBas, PoseMain.Poing)
                .Cle(3f, main + haut, PaumeBas, PoseMain.Ouverte)
                .Cle(4f, DroiteLoin, PaumeBas, PoseMain.Ouverte);
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Cube)
                .Cle(1.5f, CubePose, TailleCube)
                .Cle(2.5f, CubePose + haut, TailleCube));
            return clip;
        }

        /// <summary>Une main tient, l'autre pince en touchant l'objet, puis on écarte les mains.</summary>
        public static ClipGeste Agrandir()
        {
            Vector3 gaucheContact = new Vector3(-0.08f, 0f, -0.03f);
            Vector3 droiteContact = new Vector3(0.08f, 0f, -0.03f);
            Vector3 ecart = Vector3.right * 0.1f;
            ClipGeste clip = new ClipGeste { duree = 5f };
            clip.gauche = new PisteMain()
                .Cle(0f, gaucheContact, GaucheVersLeCentre, PoseMain.Poing)
                .Cle(1.6f, gaucheContact, GaucheVersLeCentre, PoseMain.Poing)
                .Cle(3.4f, gaucheContact - ecart, GaucheVersLeCentre, PoseMain.Poing)
                .Cle(5f, gaucheContact - ecart, GaucheVersLeCentre, PoseMain.Poing);
            clip.droite = new PisteMain()
                .Cle(0f, DroiteLoin, DroiteVersLeCentre, PoseMain.Ouverte)
                .Cle(1f, droiteContact, DroiteVersLeCentre, PoseMain.Ouverte)
                .Cle(1.6f, droiteContact, DroiteVersLeCentre, PoseMain.Pince)
                .Cle(3.4f, droiteContact + ecart, DroiteVersLeCentre, PoseMain.Pince)
                .Cle(4f, droiteContact + ecart, DroiteVersLeCentre, PoseMain.Ouverte)
                .Cle(5f, DroiteLoin, DroiteVersLeCentre, PoseMain.Ouverte);
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Cube)
                .Cle(1.6f, CubePose, TailleCube)
                .Cle(3.4f, CubePose, TailleCube * 2.2f));
            return clip;
        }

        /// <summary>Tourner la paume gauche vers soi, attendre la bulle, y lâcher l'objet.</summary>
        public static ClipGeste Ranger()
        {
            Vector3 dansLaMain = new Vector3(0.1f, 0.02f, 0.2f);
            Vector3 mainDansLaMain = dansLaMain + new Vector3(0f, 0.05f, -0.09f);
            Vector3 mainALaBulle = Bulle + new Vector3(0f, 0.05f, -0.09f);
            ClipGeste clip = new ClipGeste { duree = 6f };
            clip.gauche = new PisteMain()
                .Cle(0f, new Vector3(-0.2f, -0.2f, 0.05f), PaumeBas, PoseMain.Ouverte)
                .Cle(1f, PoignetMenu, PaumeVersVisage, PoseMain.Ouverte)
                .Cle(6f, PoignetMenu, PaumeVersVisage, PoseMain.Ouverte);
            clip.droite = new PisteMain()
                .Cle(0f, mainDansLaMain, PaumeBas, PoseMain.Poing)
                .Cle(3f, mainDansLaMain, PaumeBas, PoseMain.Poing)
                .Cle(4f, mainALaBulle, PaumeBas, PoseMain.Poing)
                .Cle(4.5f, mainALaBulle, PaumeBas, PoseMain.Ouverte)
                .Cle(6f, DroiteLoin, PaumeBas, PoseMain.Ouverte);
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Cube)
                .Cle(3f, dansLaMain, TailleCube)
                .Cle(4f, Bulle, TailleCube)
                .Cle(4.5f, Bulle, TailleCubeRange));
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Bulle)
                .Cle(1.8f, Bulle, 0f)
                .Cle(2.4f, Bulle, TailleBulle));
            return clip;
        }

        /// <summary>Base commune aux gestes « toucher un doigt gauche avec l'index droit », objet rangé dans la bulle.</summary>
        private static ClipGeste ToucherUnDoigt(Vector3 boutDuDoigt, float duree, float instantContact)
        {
            Vector3 contact = PoignetPourToucher(boutDuDoigt);
            ClipGeste clip = new ClipGeste { duree = duree };
            clip.gauche = new PisteMain()
                .Cle(0f, PoignetMenu, PaumeVersVisage, PoseMain.Ouverte)
                .Cle(duree, PoignetMenu, PaumeVersVisage, PoseMain.Ouverte);
            clip.droite = new PisteMain()
                .Cle(0f, DroiteLoin, PointeVersGauche, PoseMain.Pointe)
                .Cle(instantContact, contact, PointeVersGauche, PoseMain.Pointe)
                .Cle(instantContact + 0.5f, contact, PointeVersGauche, PoseMain.Pointe)
                .Cle(duree, DroiteLoin, PointeVersGauche, PoseMain.Pointe);
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Bulle).Cle(0f, Bulle, TailleBulle));
            return clip;
        }

        /// <summary>Index droit sur index gauche : un second cube apparaît.</summary>
        public static ClipGeste Dupliquer()
        {
            ClipGeste clip = ToucherUnDoigt(BoutIndexGauche, 4f, 1.5f);
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Cube).Cle(0f, Bulle, TailleCubeRange));
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Cube)
                .Cle(1.6f, CubePose, 0f)
                .Cle(2f, CubePose, TailleCube));
            return clip;
        }

        /// <summary>Index droit sur annulaire gauche : le cube retourne dans la main droite.</summary>
        public static ClipGeste Recuperer()
        {
            ClipGeste clip = ToucherUnDoigt(BoutAnnulaireGauche, 4f, 1.5f);
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Cube)
                .Cle(1.6f, Bulle, TailleCubeRange)
                .Cle(2.2f, PoignetPourToucher(BoutAnnulaireGauche) + Vector3.left * 0.1f, TailleCube));
            return clip;
        }

        /// <summary>Index droit sur majeur gauche : le cube de la bulle disparaît.</summary>
        public static ClipGeste Supprimer()
        {
            ClipGeste clip = ToucherUnDoigt(BoutMajeurGauche, 4f, 1.5f);
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Cube)
                .Cle(1.6f, Bulle, TailleCubeRange)
                .Cle(1.9f, Bulle, 0f));
            return clip;
        }

        /// <summary>
        /// Téléportation au microgeste : poing lâche (pouce en haut), un tap du pouce sur le côté
        /// de l'index fait apparaître la cible au sol, un second tap y emmène.
        /// </summary>
        public static ClipGeste TeleporterMicrogeste()
        {
            Vector3 main = new Vector3(0.08f, 0.02f, 0.06f);
            Vector3 cible = new Vector3(0.15f, -1.3f, 1.2f); // au sol, devant : l'ancre est à 20 cm sous les yeux
            ClipGeste clip = new ClipGeste { duree = 5f };
            clip.accessoires.Add(new PisteAccessoire(FormeAccessoire.Cible)
                .Cle(1.25f, cible, 0f)
                .Cle(1.45f, cible, 0.5f)
                .Cle(3.5f, cible, 0.5f)
                .Cle(3.6f, cible, 0f));
            clip.droite = new PisteMain()
                .Cle(0f, main, DroiteVersLeCentre, PoseMain.MicroLeve)
                .Cle(1f, main, DroiteVersLeCentre, PoseMain.MicroLeve)
                .Cle(1.25f, main, DroiteVersLeCentre, PoseMain.MicroTap)
                .Cle(1.5f, main, DroiteVersLeCentre, PoseMain.MicroLeve)
                .Cle(3.25f, main, DroiteVersLeCentre, PoseMain.MicroLeve)
                .Cle(3.5f, main, DroiteVersLeCentre, PoseMain.MicroTap)
                .Cle(3.75f, main, DroiteVersLeCentre, PoseMain.MicroLeve)
                .Cle(5f, main, DroiteVersLeCentre, PoseMain.MicroLeve);
            return clip;
        }

        /// <summary>Téléportation au signe du pistolet : viser le sol avec la main gauche et attendre.</summary>
        public static ClipGeste TeleporterPistolet()
        {
            Vector3 main = new Vector3(-0.12f, 0f, 0.02f);
            Quaternion vise = Quaternion.Euler(25f, 0f, 90f);
            ClipGeste clip = new ClipGeste { duree = 4f };
            clip.gauche = new PisteMain()
                .Cle(0f, main + Vector3.down * 0.12f, GaucheVersLeCentre, PoseMain.Ouverte)
                .Cle(1f, main, vise, PoseMain.Pointe)
                .Cle(4f, main, vise, PoseMain.Pointe);
            return clip;
        }

        /// <summary>Escalade : fermer le poing main vide, tirer vers soi.</summary>
        public static ClipGeste Escalader()
        {
            Vector3 loin = new Vector3(0.12f, 0f, 0.2f);
            Vector3 pres = new Vector3(0.12f, 0f, -0.05f);
            ClipGeste clip = new ClipGeste { duree = 3f };
            clip.droite = new PisteMain()
                .Cle(0f, loin, PaumeBas, PoseMain.Ouverte)
                .Cle(0.5f, loin, PaumeBas, PoseMain.Poing)
                .Cle(2f, pres, PaumeBas, PoseMain.Poing)
                .Cle(2.4f, pres, PaumeBas, PoseMain.Ouverte)
                .Cle(3f, loin, PaumeBas, PoseMain.Ouverte);
            return clip;
        }
    }
}
