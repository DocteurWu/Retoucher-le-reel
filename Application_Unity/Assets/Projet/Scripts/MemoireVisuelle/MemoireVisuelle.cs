// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  MemoireVisuelle.cs — LE JEU « MÉMOIRE VISUELLE » (chef d'orchestre)
//
//  LE JEU
//    1. INTRO          : une phrase de présentation.
//    2. OBSERVATION    : le joueur fait le tour de la scène et mémorise les objets. Il peut dire
//                        « Je suis prêt » (bouton) après observationMin ; fin automatique à
//                        observationMax. RIEN n'est mis en évidence : ce serait donner la réponse.
//    3. MÉLANGE        : fondu au noir ; pendant ce temps, « nombreADeplacer » objets changent
//                        d'emplacement (MelangeurMemoire) ; fondu retour.
//    4. RECONSTITUTION : le joueur repère ce qui a bougé et le remet en place avec les gestes
//                        habituels (saisir, déplacer). Bouton « Valider ».
//    5. RÉSULTAT       : boîtes vertes (bien placé) / rouges (raté) + où il devait être, score,
//                        narration. Bouton « Rejouer ».
//
//  NIVEAUX : « niveau » est le premier ; « niveauxSuivants » les suivants. Réussir un niveau
//    qui a un suivant transforme le bouton final en « Niveau suivant ». Un niveau avancé peut
//    aussi changer la TAILLE de quelques objets pendant le noir (nombreAChangerDeTaille) : le
//    joueur doit alors retrouver place ET taille (évaluées ensemble, toleranceRatioTaille).
//
//  C'EST UNE ÉPREUVE (hérite de Epreuve) : quand le score atteint le seuil du niveau,
//    SignalerReussite() lève l'événement commun que la narration écoute (« Epreuve Reussie »).
//    On n'utilise pas ProgressionEtapes (porte, tutoriel, main guide : propres au niveau céramique).
//
//  À POSER SUR : un objet vide « MemoireVisuelle ». Renseigner « Niveau » (un NiveauMemoire).
//    Les objets (ObjetMemoire) et les emplacements (EmplacementMemoire) sont trouvés tout seuls
//    dans la scène ; « Objets » permet de se limiter à une liste.
//    Le menu « Retoucher le Reel > Memoire visuelle > Creer la scene maquette » monte une scène
//    de test complète (pièce simple, 5 faux objets, 8 emplacements).
//
//  SANS CASQUE : DemoMemoire (créée au lancement) donne des touches PC : N = phase suivante,
//    R = rejouer, F = solution automatique.
//
//  LE BOUTON se matérialise devant le joueur au moment où il sert et reste là : pas de validation
//  accidentelle en cours de partie.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using RemodelerLeReel.Jeu;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Les phases du jeu.</summary>
    public enum PhaseMemoire
    {
        Attente,
        Intro,
        Observation,
        Melange,
        Reconstitution,
        Resultat
    }

    /// <summary>Jeu de mémoire visuelle : observer, mélange, replacer, résultat.</summary>
    [AddComponentMenu("Remodeler le Reel/Memoire visuelle/Jeu Memoire visuelle")]
    public class MemoireVisuelle : Epreuve
    {
        /// <summary>Levé à chaque changement de phase.</summary>
        public static event Action<PhaseMemoire> PhaseChangee;

        [Header("Niveau")]
        [Tooltip("Les réglages du niveau (Create > Retoucher le Reel > Memoire visuelle > Niveau).")]
        public NiveauMemoire niveau;

        [Tooltip("Niveaux suivants, dans l'ordre : après une réussite, le bouton devient « Niveau suivant ».")]
        public List<NiveauMemoire> niveauxSuivants = new List<NiveauMemoire>();

        [Tooltip("Objets du jeu. Vide = tous les ObjetMemoire de la scène.")]
        public List<ObjetMemoire> objets = new List<ObjetMemoire>();

        [Header("Déroulement")]
        public bool demarrerAuLancement = true;

        [Tooltip("Durée (s) de l'intro avant l'observation.")]
        public float delaiIntro = 5f;

        [Tooltip("Touches PC de test (N = phase suivante, R = rejouer, F = solution).")]
        public bool commandesDemo = true;

        [Header("Textes (narration)")]
        [TextArea(2, 4)] public string texteIntro = "Observe bien la pièce : tu vas devoir te souvenir de chaque objet. Fais le tour, prends ton temps.";
        [TextArea(2, 4)] public string textePret = "Quand tu es prêt, touche le bouton devant toi.";
        [TextArea(2, 4)] public string texteMelange = "Quelque chose a changé. Remets chaque objet à sa place.";
        [TextArea(2, 4)] public string texteMelangeTaille = "Des objets ont changé de place, et certains de taille. Retrouve leur place et leur taille d'origine.";
        [TextArea(2, 4)] public string texteValider = "Quand tu as fini, touche le bouton devant toi.";
        [TextArea(2, 4)] public string texteReussite = "Bravo ! Tu as une excellente mémoire.";
        [TextArea(2, 4)] public string texteEchec = "Pas tout à fait. Regarde où ils devaient être.";

        /// <summary>Phase en cours.</summary>
        public PhaseMemoire Phase { get; private set; } = PhaseMemoire.Attente;

        /// <summary>Verdicts de la dernière validation (un par objet).</summary>
        public IReadOnlyList<ResultatObjet> Resultats => _resultats;

        /// <summary>Score de la dernière validation (0 à 1).</summary>
        public float Score { get; private set; }

        /// <summary>Objets que le dernier mélange a déplacés.</summary>
        public IReadOnlyList<ObjetMemoire> Deplaces => _deplaces;

        /// <summary>Objets dont le dernier mélange a changé la taille.</summary>
        public IReadOnlyList<ObjetMemoire> Redimensionnes => _redimensionnes;

        /// <summary>Un niveau suivant existe-t-il ?</summary>
        public bool ASuivant => niveauxSuivants != null && _indexNiveau < niveauxSuivants.Count && niveauxSuivants[_indexNiveau] != null;

        private readonly List<ObjetMemoire> _jeu = new List<ObjetMemoire>();
        private readonly List<EmplacementMemoire> _emplacements = new List<EmplacementMemoire>();
        private readonly List<ObjetMemoire> _deplaces = new List<ObjetMemoire>();
        private readonly List<ObjetMemoire> _redimensionnes = new List<ObjetMemoire>();
        private int _indexNiveau; // nombre de niveaux suivants déjà passés
        private bool _dernierReussi;
        private readonly List<ResultatObjet> _resultats = new List<ResultatObjet>();
        private readonly List<NarrationSequence> _annonces = new List<NarrationSequence>();

        private BoutonMemoire3D _bouton;
        private MarqueursResultat _marqueurs;
        private TextMeshPro _panneau;
        private float _debutPhase;
        private int _essais;
        private bool _boutonAffiche;

        private void Start()
        {
            if (nomEtape == "Épreuve") nomEtape = "Mémoire visuelle";
            if (commandesDemo && GetComponent<DemoMemoire>() == null) gameObject.AddComponent<DemoMemoire>();
            if (demarrerAuLancement) Demarrer();
        }

        private void OnDestroy()
        {
            if (_bouton != null) _bouton.Presse -= SurBouton;
        }

        // ------------------------------------------------------------------
        // Démarrage / rejouer
        // ------------------------------------------------------------------
        /// <summary>Lance (ou relance) une partie.</summary>
        public void Demarrer()
        {
            if (Phase != PhaseMemoire.Attente && Phase != PhaseMemoire.Resultat) return;
            if (niveau == null)
            {
                Debug.LogError("[Memoire] Aucun niveau renseigné sur « " + name + " » : glisser un NiveauMemoire.", this);
                enabled = false;
                return;
            }
            if (!Rassembler()) return;

            _essais = 0;
            _deplaces.Clear();
            _redimensionnes.Clear();
            _resultats.Clear();
            _dernierReussi = false;
            if (_marqueurs != null) _marqueurs.Effacer();
            CacherBouton();

            // Le redimensionnement n'est permis que si le niveau l'exige (taille à retrouver).
            foreach (ObjetMemoire o in _jeu) o.DefinirRedimensionnement(niveau.permettreRedimensionnement);

            PasserA(PhaseMemoire.Intro);
            Dire(!string.IsNullOrWhiteSpace(niveau.introduction) ? niveau.introduction : texteIntro);
        }

        /// <summary>Après un résultat : remet tout en place et recommence le même niveau (nouveau mélange).</summary>
        public void Recommencer()
        {
            if (Phase != PhaseMemoire.Resultat) return;
            RemettreTout();
            Phase = PhaseMemoire.Attente;
            Demarrer();
        }

        /// <summary>Après un résultat : passe au niveau suivant, s'il y en a un.</summary>
        public void NiveauSuivant()
        {
            if (Phase != PhaseMemoire.Resultat || !ASuivant) return;
            niveau = niveauxSuivants[_indexNiveau];
            _indexNiveau++;
            Debug.Log("[Memoire] Niveau suivant : " + niveau.titre, this);
            RemettreTout();
            Phase = PhaseMemoire.Attente;
            Demarrer();
        }

        private void RemettreTout()
        {
            foreach (ObjetMemoire o in _jeu)
            {
                if (o == null) continue;
                Liberer(o.Corps);
                o.RemettreEnPlace(); // position, rotation ET taille d'origine
            }
        }

        private bool Rassembler()
        {
            _jeu.Clear();
            IList<ObjetMemoire> source = objets != null && objets.Count > 0 ? (IList<ObjetMemoire>)objets : ObjetMemoire.Tous;
            foreach (ObjetMemoire o in source)
            {
                if (o != null && o.isActiveAndEnabled) _jeu.Add(o);
            }
            _emplacements.Clear();
            foreach (EmplacementMemoire e in EmplacementMemoire.Tous)
            {
                if (e != null && e.isActiveAndEnabled) _emplacements.Add(e);
            }

            if (_jeu.Count == 0)
            {
                Debug.LogError("[Memoire] Aucun ObjetMemoire dans la scène : le jeu ne peut pas démarrer.", this);
                enabled = false;
                return false;
            }
            if (_emplacements.Count == 0)
            {
                Debug.LogWarning("[Memoire] Aucun EmplacementMemoire dans la scène : aucun objet ne pourra être déplacé.", this);
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Boucle
        // ------------------------------------------------------------------
        private void Update()
        {
            float t = Time.time - _debutPhase;
            switch (Phase)
            {
                case PhaseMemoire.Intro:
                    if (t >= delaiIntro) PasserA(PhaseMemoire.Observation);
                    break;

                case PhaseMemoire.Observation:
                    MettreAJourObservation(t);
                    break;
            }
        }

        private void LateUpdate()
        {
            SuivreLeJoueur(_panneau);
        }

        private void MettreAJourObservation(float t)
        {
            bool pret = t >= niveau.observationMin;
            if (pret && !_boutonAffiche)
            {
                MontrerBouton("Je suis prêt", new Color(0.2f, 0.6f, 1f, 1f));
                Dire(textePret);
            }

            int reste = Mathf.Max(0, Mathf.CeilToInt(niveau.observationMax - t));
            Ecrire(pret
                ? "Observation — " + Chrono(reste) + "\nTouche le bouton quand tu es prêt"
                : "Observe la pièce — " + Chrono(reste));

            if (t >= niveau.observationMax) LancerLeMelange();
        }

        private void PasserA(PhaseMemoire phase)
        {
            Phase = phase;
            _debutPhase = Time.time;
            Debug.Log("[Memoire] Phase : " + phase, this);

            switch (phase)
            {
                case PhaseMemoire.Intro:
                case PhaseMemoire.Melange:
                    Ecrire(null);
                    break;
                case PhaseMemoire.Observation:
                    Ecrire("Observe la pièce");
                    break;
                case PhaseMemoire.Reconstitution:
                    Ecrire(TexteReconstitution());
                    break;
            }
            PhaseChangee?.Invoke(phase);
        }

        // ------------------------------------------------------------------
        // Mélange
        // ------------------------------------------------------------------
        /// <summary>Fin de l'observation : fondu, mélange, fondu retour.</summary>
        public void LancerLeMelange()
        {
            if (Phase != PhaseMemoire.Observation) return;
            StartCoroutine(Melanger());
        }

        private IEnumerator Melanger()
        {
            PasserA(PhaseMemoire.Melange);
            CacherBouton();
            CouperLaNarration();

            FonduMemoire fondu = FonduMemoire.Obtenir();
            if (fondu != null) yield return fondu.Vers(1f, 0.8f);

            AppliquerLeMelange();
            yield return new WaitForSeconds(0.5f);

            if (fondu != null) yield return fondu.Vers(0f, 0.8f);

            Dire(_redimensionnes.Count > 0 ? texteMelangeTaille : texteMelange);
            PasserA(PhaseMemoire.Reconstitution);
            MontrerBouton("Valider", new Color(0.2f, 0.8f, 0.35f, 1f));
        }

        private void AppliquerLeMelange()
        {
            _deplaces.Clear();

            var objetsLogique = new List<ObjetMelange>(_jeu.Count);
            for (int i = 0; i < _jeu.Count; i++)
            {
                ObjetMemoire o = _jeu[i];
                objetsLogique.Add(new ObjetMelange
                {
                    id = i,
                    origine = o.CentreOrigine,
                    taille = o.Taille,
                    fixe = o.emplacementFixe != null ? _emplacements.IndexOf(o.emplacementFixe) : -1,
                    autorises = IdsAutorises(o)
                });
            }

            var emplacementsLogique = new List<EmplacementMelange>(_emplacements.Count);
            for (int i = 0; i < _emplacements.Count; i++)
            {
                emplacementsLogique.Add(new EmplacementMelange
                {
                    id = i,
                    position = _emplacements[i].transform.position,
                    tailleMax = _emplacements[i].tailleMax
                });
            }

            int voulus = Mathf.Min(niveau.nombreADeplacer, _jeu.Count);
            List<Deplacement> choix = MelangeurMemoire.Calculer(objetsLogique, emplacementsLogique, voulus,
                niveau.distanceMinDeplacement, niveau.graine);

            foreach (Deplacement d in choix)
            {
                ObjetMemoire o = _jeu[d.objet];
                Liberer(o.Corps); // au cas où une main le tiendrait encore
                o.Deplacer(_emplacements[d.emplacement].transform.position);
                _deplaces.Add(o);
            }

            if (choix.Count < voulus)
            {
                Debug.LogWarning("[Memoire] " + choix.Count + " objet(s) déplacé(s) sur " + voulus + " voulus : pas assez d'emplacements valides "
                    + "(taille, distance minimale " + niveau.distanceMinDeplacement + " m, emplacements autorisés). Ajouter des EmplacementMemoire.", this);
            }
            Debug.Log("[Memoire] Mélange : " + string.Join(", ", _deplaces.ConvertAll(o => o.NomAffiche)), this);

            AppliquerLesChangementsDeTaille();
        }

        /// <summary>Niveaux avancés : quelques objets sont agrandis ou rétrécis pendant le noir.</summary>
        private void AppliquerLesChangementsDeTaille()
        {
            _redimensionnes.Clear();
            if (niveau.nombreAChangerDeTaille <= 0) return;

            var tailles = new List<float>(_jeu.Count);
            var indicesDeplaces = new List<int>();
            for (int i = 0; i < _jeu.Count; i++)
            {
                tailles.Add(_jeu[i].Taille);
                if (_deplaces.Contains(_jeu[i])) indicesDeplaces.Add(i);
            }

            List<ChangementTaille> changements = ChangementsTailleMemoire.Calculer(tailles, indicesDeplaces,
                niveau.nombreAChangerDeTaille, niveau.tailleMinPourChanger, niveau.ratioAgrandi, niveau.ratioReduit, niveau.graine);

            foreach (ChangementTaille c in changements)
            {
                ObjetMemoire o = _jeu[c.objet];
                Liberer(o.Corps);
                o.ChangerTaille(c.ratio);
                _redimensionnes.Add(o);
            }

            if (changements.Count < niveau.nombreAChangerDeTaille)
            {
                Debug.LogWarning("[Memoire] " + changements.Count + " changement(s) de taille sur " + niveau.nombreAChangerDeTaille
                    + " voulus : pas assez d'objets de plus de " + niveau.tailleMinPourChanger + " m.", this);
            }
            if (niveau.toleranceRatioTaille <= 0f)
            {
                Debug.LogWarning("[Memoire] Des tailles changent mais « toleranceRatioTaille » vaut 0 : la taille ne sera pas évaluée.", this);
            }
            if (!niveau.permettreRedimensionnement)
            {
                Debug.LogWarning("[Memoire] Des tailles changent mais « permettreRedimensionnement » est décoché : le joueur ne pourra pas les corriger.", this);
            }
            Debug.Log("[Memoire] Tailles : " + string.Join(", ", _redimensionnes.ConvertAll(o => o.NomAffiche + " ×" + o.RatioTaille.ToString("0.0"))), this);
        }

        private int[] IdsAutorises(ObjetMemoire o)
        {
            if (o.emplacementsAutorises == null || o.emplacementsAutorises.Count == 0) return null;
            var ids = new List<int>();
            foreach (EmplacementMemoire e in o.emplacementsAutorises)
            {
                int index = _emplacements.IndexOf(e);
                if (index >= 0) ids.Add(index);
            }
            return ids.ToArray();
        }

        // ------------------------------------------------------------------
        // Validation / résultat
        // ------------------------------------------------------------------
        /// <summary>Le joueur valide : on mesure chaque objet.</summary>
        public void Valider()
        {
            if (Phase != PhaseMemoire.Reconstitution) return;
            _essais++;
            Evaluer();

            int corrects = EvaluationMemoire.Corrects(_resultats);
            bool tousBons = corrects == _resultats.Count;
            if (_essais >= niveau.nombreEssais || tousBons)
            {
                AfficherLeResultat();
                return;
            }

            int restants = niveau.nombreEssais - _essais;
            Dire(corrects + " objet(s) bien placé(s) sur " + _resultats.Count + ". Il te reste " + restants + " essai" + (restants > 1 ? "s" : "") + ".");
            Ecrire(TexteReconstitution());
        }

        private void Evaluer()
        {
            _resultats.Clear();
            for (int i = 0; i < _jeu.Count; i++)
            {
                ObjetMemoire o = _jeu[i];
                _resultats.Add(EvaluationMemoire.Evaluer(i, o.CentreOrigine, o.Centre, o.Taille,
                    niveau.toleranceMetres, niveau.toleranceTaille, o.RatioTaille, niveau.toleranceRatioTaille));
            }
            Score = EvaluationMemoire.Score(_resultats);
        }

        private void AfficherLeResultat()
        {
            PasserA(PhaseMemoire.Resultat);
            CouperLaNarration();

            int corrects = EvaluationMemoire.Corrects(_resultats);
            bool reussi = EvaluationMemoire.Reussi(Score, niveau.seuilReussite, _resultats.Count);

            _dernierReussi = reussi;
            Ecrire(niveau.titre + "\n" + corrects + " / " + _resultats.Count + (reussi ? " — Réussi !" : ""));
            if (niveau.afficherSolution) MontrerLaSolution();

            // Réussi et un niveau suivant existe : on propose de continuer ; sinon, de rejouer.
            if (reussi && ASuivant) MontrerBouton("Niveau suivant", new Color(0.3f, 0.5f, 1f, 1f));
            else MontrerBouton("Rejouer", new Color(0.95f, 0.6f, 0.15f, 1f));

            if (reussi)
            {
                Vector3 centre = transform.position;
                if (Camera.main != null) centre = Camera.main.transform.position;
                VRSons.Jouer(VRSon.Reussite, centre, 0.8f);
                Dire(!string.IsNullOrWhiteSpace(felicitation) ? felicitation : texteReussite);
                SignalerReussite();
            }
            else
            {
                Dire(texteEchec);
            }
        }

        private void MontrerLaSolution()
        {
            if (_marqueurs == null)
            {
                GameObject go = new GameObject("Marqueurs_Memoire");
                go.transform.SetParent(transform, false);
                _marqueurs = go.AddComponent<MarqueursResultat>();
            }
            _marqueurs.Effacer();

            for (int i = 0; i < _jeu.Count; i++)
            {
                ObjetMemoire o = _jeu[i];
                if (!o.CalculerBoite(out Bounds actuelle)) continue;
                ResultatObjet r = _resultats[i];
                if (r.ok)
                {
                    _marqueurs.AjouterBoite(actuelle, new Color(0.2f, 1f, 0.4f, 1f), o.NomAffiche);
                    continue;
                }

                // Bien placé mais pas à la bonne taille : orange. Raté : rouge.
                Color couleur = r.positionOk ? new Color(1f, 0.6f, 0.1f, 1f) : new Color(1f, 0.25f, 0.25f, 1f);
                _marqueurs.AjouterBoite(actuelle, couleur, o.NomAffiche + (r.tailleOk ? "" : " ×" + r.ratioTaille.ToString("0.0")));

                // La solution : la boîte à la taille d'origine, centrée sur la place d'origine (« ici »),
                // ou « taille » si seule la taille était fausse (la boîte d'origine est alors au même endroit).
                float ratio = Mathf.Max(0.01f, r.ratioTaille);
                _marqueurs.AjouterBoite(new Bounds(o.CentreOrigine, actuelle.size / ratio), Color.white, r.positionOk ? "taille d'origine" : "ici");
            }
        }

        // ------------------------------------------------------------------
        // Épreuve (classe de base)
        // ------------------------------------------------------------------
        /// <summary>
        /// Rien à montrer : mettre les objets en évidence révélerait la réponse. (Un indice, plus
        /// tard, pourra passer par la main guide.)
        /// </summary>
        protected override void AjouterObjetsAMontrer(List<Collider> liste) { }

        /// <summary>Validé depuis le PC : tout remis en place, résultat affiché.</summary>
        protected override void Forcer()
        {
            ForcerLaSolution();
        }

        /// <summary>
        /// Touche F : remet tout en place (position ET taille) et affiche le résultat, à tout moment et
        /// autant de fois qu'on veut. Passe par ici plutôt que par Epreuve.ForcerReussite, qui ne fait
        /// plus rien dès que l'épreuve a été réussie une fois (EstReussie est définitif).
        /// </summary>
        public void ForcerLaSolution()
        {
            if (niveau == null) return;
            if (_jeu.Count == 0 && !Rassembler()) return;

            StopAllCoroutines();
            FonduMemoire fondu = FonduMemoire.Obtenir();
            if (fondu != null) fondu.Appliquer(0f);

            RemettreTout();
            _essais = niveau.nombreEssais;
            Evaluer();
            AfficherLeResultat(); // même déjà au résultat : rafraîchit le score et les boîtes
        }

        // ------------------------------------------------------------------
        // Commandes de démo (DemoMemoire)
        // ------------------------------------------------------------------
        /// <summary>Passe à la phase suivante (ou valide / rejoue).</summary>
        public void PhaseSuivante()
        {
            switch (Phase)
            {
                case PhaseMemoire.Attente: Demarrer(); break;
                case PhaseMemoire.Intro: PasserA(PhaseMemoire.Observation); break;
                case PhaseMemoire.Observation: LancerLeMelange(); break;
                case PhaseMemoire.Reconstitution: Valider(); break;
                case PhaseMemoire.Resultat:
                    if (_dernierReussi && ASuivant) NiveauSuivant();
                    else Recommencer();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Bouton, panneau, narration
        // ------------------------------------------------------------------
        private void SurBouton()
        {
            switch (Phase)
            {
                case PhaseMemoire.Observation:
                    if (Time.time - _debutPhase >= niveau.observationMin) LancerLeMelange();
                    break;
                case PhaseMemoire.Reconstitution: Valider(); break;
                case PhaseMemoire.Resultat:
                    if (_dernierReussi && ASuivant) NiveauSuivant();
                    else Recommencer();
                    break;
            }
        }

        private void MontrerBouton(string texte, Color couleur)
        {
            if (_bouton == null)
            {
                GameObject go = new GameObject("Bouton_Memoire");
                go.transform.SetParent(transform, false);
                _bouton = go.AddComponent<BoutonMemoire3D>();
                _bouton.Presse += SurBouton;
            }

            // Devant le joueur, à hauteur de poitrine : à portée de main sans bouger.
            Vector3 position = transform.position;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 avant = cam.transform.forward;
                avant.y = 0f;
                avant = avant.sqrMagnitude > 1e-4f ? avant.normalized : Vector3.forward;
                position = cam.transform.position + avant * 0.7f;
                position.y = cam.transform.position.y - 0.3f;
            }
            _bouton.Montrer(position, texte, couleur);
            _boutonAffiche = true;
        }

        private void CacherBouton()
        {
            _boutonAffiche = false;
            if (_bouton != null) _bouton.Cacher();
        }

        private string TexteReconstitution()
        {
            int restants = niveau.nombreEssais - _essais;
            string consigne = _redimensionnes.Count > 0 ? "Retrouve la place et la taille de chaque objet" : "Remets chaque objet à sa place";
            return consigne + (niveau.nombreEssais > 1 ? "\nEssais restants : " + restants : "");
        }

        private static string Chrono(int secondes) => (secondes / 60) + ":" + (secondes % 60).ToString("00");

        /// <summary>Texte du panneau flottant (null ou vide : panneau caché).</summary>
        private void Ecrire(string texte)
        {
            if (string.IsNullOrEmpty(texte))
            {
                if (_panneau != null) _panneau.gameObject.SetActive(false);
                return;
            }
            if (_panneau == null)
            {
                _panneau = CreerPanneau("Panneau_Memoire", new Vector2(1.1f, 0.3f));
            }
            if (!_panneau.gameObject.activeSelf) _panneau.gameObject.SetActive(true);
            _panneau.text = texte;
        }

        /// <summary>Le panneau flotte devant le joueur, un peu plus bas que le regard, avec un suivi doux.</summary>
        private static void SuivreLeJoueur(TextMeshPro panneau)
        {
            Camera cam = Camera.main;
            if (panneau == null || !panneau.gameObject.activeSelf || cam == null) return;

            Vector3 avant = cam.transform.forward;
            avant.y = 0f;
            avant = avant.sqrMagnitude > 1e-4f ? avant.normalized : Vector3.forward;
            Vector3 cible = cam.transform.position + avant * 1.4f + Vector3.down * 0.45f;

            Transform t = panneau.transform;
            float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
            t.position = Vector3.Lerp(t.position, cible, k);

            Vector3 regard = t.position - cam.transform.position;
            regard.y = 0f;
            if (regard.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(regard, Vector3.up);
        }

        /// <summary>Dit une phrase dans la bulle de narration (si la scène en a une).</summary>
        private void Dire(string texte)
        {
            if (string.IsNullOrWhiteSpace(texte)) return;
            NarrationBulle bulle = FindFirstObjectByType<NarrationBulle>();
            if (bulle == null) return;

            // Une séquence « Manuel » par annonce : la bulle les enchaîne (file d'attente) puis chacune
            // se retire une fois dite. Même méthode que ProgressionEtapes.
            NarrationSequence annonce = bulle.gameObject.AddComponent<NarrationSequence>();
            annonce.declencheur = DeclencheurNarration.Manuel; // avant son Start : ne part pas toute seule
            annonce.nom = "Mémoire : " + texte.Substring(0, Mathf.Min(24, texte.Length));
            annonce.delai = 0.3f;
            annonce.repliques = new List<Replique> { new Replique { texte = texte } };
            annonce.Terminee += s =>
            {
                _annonces.Remove(s);
                Destroy(s);
            };
            _annonces.Add(annonce);
            annonce.Declencher();
        }

        /// <summary>Retire les annonces en attente : le joueur n'attend pas une phrase périmée.</summary>
        private void CouperLaNarration()
        {
            foreach (NarrationSequence annonce in _annonces.ToArray())
            {
                if (annonce != null) Destroy(annonce);
            }
            _annonces.Clear();
        }
    }
}
