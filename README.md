# Retoucher le réel — Projet MOVIE 2026 (Mines Paris - PSL)

Application de **réalité virtuelle** pour retoucher des lieux **réels numérisés** : on se promène dans une pièce scannée en *3D Gaussian Splatting*, on en saisit les objets, on les agrandit, on les copie, on les fait disparaître.

Le projet combine deux briques :
1. **Capture et reconstruction 3D** : [3D Gaussian Splatting](https://github.com/graphdeco-inria/gaussian-splatting) (*Inria / GraphDeco*) reconstruit la pièce à partir de photos ou de vidéos, et [SAGA](https://github.com/Jumpat/SegAnyGAussians) en découpe les objets.
2. **Application VR sous Unity** (casque Meta Quest) : le rendu des splats repose sur [UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting).

---

## Fonctionnalités

- **Mains nues ou manettes** : saisir et lancer un objet, le redimensionner, se déplacer (téléportation, escalade ou déplacement continu).
- **Menu au poignet** : on range un objet dans une bulle, puis on le duplique, le supprime ou le récupère d'un simple contact entre les doigts.
- **Jeu de base** (salle « céramique ») : cinq épreuves à réussir pour ouvrir la porte. Il faut remplir un bac de 4 fraises, agrandir puis accrocher un tableau, mettre la plante dans son pot, faire disparaître la statue et trouver la clé. La porte ouverte, la pièce brûle jusqu'à ses Gaussiennes et la salle suivante se reconstruit autour du joueur.
- **Jeu de mémoire** (bibliothèque « La Grange ») : on observe la pièce, les objets s'envolent en tas au milieu, puis il faut remettre chacun à sa place.
- **Accompagnement** :
  - un tutoriel des gestes, montrés par des mains fantômes ;
  - une narration vocale ;
  - une main guide façon « La Chose » qui montre où aller ;
  - des sons générés par le code.

---

## Mini manuel d'utilisation

**Lancer** : ouvrir le dossier `Application_Unity` dans Unity Hub (Unity **6000.0.84f1**). Ouvrir la scène `Assets/Projet/Scenes_Niveaux/Menu.unity` et appuyer sur Play, casque branché.

Le casque Meta Quest est relié au PC par ALVR et SteamVR. Le runtime OpenXR actif doit être **SteamVR**.

**Dans le menu** : viser un bouton avec le signe du pistolet (index tendu), puis valider en rabattant le pouce. Avec les manettes, on valide à la gâchette.
- *Tutoriel des gestes* : apprendre les gestes pas à pas.
- *Mode* : choisir le mode de déplacement.
- *Jeu de base* ou *Jeu de mémoire* : lancer une partie.

| Action | Mains nues | Manettes |
| :--- | :--- | :--- |
| Saisir / lâcher | fermer le poing (ou pincer) près de l'objet | gâchette ou poignée |
| Redimensionner | pincer avec les deux mains, la main libre touchant l'objet | stick droit |
| Se déplacer | main droite en poing lâche : un tap du pouce sur l'index pour viser, un 2ᵉ tap pour se téléporter | stick gauche |
| Menu poignet | tourner la paume gauche vers soi, lâcher l'objet dans la bulle | idem |
| Dupliquer / supprimer / récupérer | index / majeur / annulaire droit sur l'index gauche | X / Y |

**Pour la personne au PC** : la touche **H** affiche le panneau de démonstration. Depuis ce panneau :

| Touche | Effet |
| :--- | :--- |
| 1 à 9 | valider l'étape correspondante |
| N | valider l'étape suivante |
| G | afficher ou masquer la main guide |
| V | activer ou couper la voix |
| P | jeu de mémoire : passer l'attente |
| O | charger la scène suivante |

---

## Organisation du dépôt

```text
retoucher-le-reel/
├── Application_Unity/              # Projet Unity (VR, scènes, scripts)
│   ├── Assets/Projet/              # Nos assets : Scripts, Scenes_Niveaux, Prefabs_Objets, Audio...
│   ├── Outils/                     # Générateur des voix (Node) et tests lançables sans Unity
│   ├── GUIDE_NOUVELLE_SCENE.md     # Du fichier .ply à une scène jouable, pas à pas
│   ├── Packages/
│   └── ProjectSettings/
├── Traitement_Gaussian_SAGA/       # Reconstruction et segmentation (Python)
│   ├── Donnees_Brutes/             # Vidéos et photos capturées
│   ├── Scripts_Entrainement/       # Entraînement, segmentation, décompression des .ply SuperSplat
│   ├── Modeles_Exportes/           # Fichiers .ply générés, prêts pour Unity
│   └── requirements.txt            # Dépendances Python
└── .gitlab/                        # Modèle de Merge Request
```

---

## Démarrage rapide

### 1. Cloner le projet (Git LFS obligatoire)
Les splats, sons et modèles 3D sont versionnés avec **Git LFS** :
```bash
git lfs install
git clone https://git.sophia.minesparis.psl.eu/projets-movie-2026/retoucher-le-reel.git
cd retoucher-le-reel
```
La version finale est sur la branche `main`.

### 2. Partie Unity (VR)
- Dans **Unity Hub**, cliquer sur **Add** et choisir le dossier **`Application_Unity`**, pas la racine du dépôt.
- Version requise : **Unity 6000.0.84f1**. Il faut le module Android pour un build sur le Quest.
- Lancer la scène `Menu.unity` (voir le mini manuel ci-dessus). Un PC avec un bon GPU est conseillé.

### 3. Partie Gaussian Splatting & SAGA (Python)
- Aller dans `Traitement_Gaussian_SAGA/` et installer l'environnement, idéalement avec un GPU Nvidia / CUDA :
  ```bash
  pip install -r requirements.txt
  ```
- Une fois la scène reconstruite et segmentée, l'importer dans Unity en suivant `Application_Unity/GUIDE_NOUVELLE_SCENE.md`.

---

## Bon à savoir

- **Fichiers ignorés** : les caches d'Unity (`Library/`, `Temp/`, `.csproj`) et de Python (`__pycache__/`, `venv/`) sont exclus par le `.gitignore`.
- **Git LFS** : les `.fbx`, `.blend`, `.bytes` (données des splats), `.mp3`, `.ogg` et les images passent toujours par LFS.
- **Les `.ply` bruts** sont ignorés : ils sont régénérables.
