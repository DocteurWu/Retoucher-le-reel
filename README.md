# Retoucher le réel — Projet MOVIE 2026 (Mines Paris - PSL)

Bienvenue sur le dépôt de notre projet d'option MOVIE (Mines Paris - PSL).

L'objectif du projet est d'explorer les interactions en Réalité Virtuelle avec des environnements réels numérisés. Pour cela, on combine deux briques :
1. **Un pipeline de capture et reconstruction 3D** basé sur :
   - [3D Gaussian Splatting](https://github.com/graphdeco-inria/gaussian-splatting) (*Inria / GraphDeco*) pour la reconstruction 3D temps réel à partir de photos/vidéos.
   - [SAGA : Segment Any 3D Gaussians](https://github.com/Jumpat/SegAnyGAussians) pour la segmentation interactive d'objets 3D dans les scènes gaussiennes.
2. **Une application VR sous Unity** (ciblant le casque Meta Quest) pour manipuler, déplacer ou modifier ces éléments directement en immersion (rendu des splats basé sur [UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting)).

---

## Comment est découpé le projet ?

On a séparé le dépôt en deux grands dossiers :

```text
retoucher-le-reel/
│
├── Application_Unity/              # Tout le projet Unity (jeu, VR, scènes, scripts)
│   ├── Assets/
│   │   └── Projet/                 # Nos assets rangés (Art_Graphismes, Scripts, Scenes_Niveaux...)
│   ├── Packages/
│   └── ProjectSettings/
│
├── Traitement_Gaussian_SAGA/       # Toute la partie IA, vision par ordinateur et reconstruction
│   ├── Donnees_Brutes/             # Les vidéos 360/ou pas brutes et photos capturées 
│   ├── Scripts_Entrainement/       # Les scripts ?
│   ├── Modeles_Exportes/           # Les fichiers .ply finaux générés, prêts pour Unity
│   └── requirements.txt            # Les librairies Python nécessaires
│
├── Documentation/                  # Nos notes, comptes-rendus de réunion et rapports
└── .gitlab/                        # Modèles de Merge Request pour GitLab
```

---

## Démarrage rapide

### 1. Cloner le projet (avec Git LFS obligatoirement !)
Comme on partage les captures vidéo et les modèles 3D directement sur le dépôt, **Git LFS est indispensable** pour ne pas corrompre les fichiers :
```bash
git lfs install
git clone https://git.sophia.minesparis.psl.eu/projets-movie-2026/retoucher-le-reel.git
cd retoucher-le-reel
git checkout develop
git pull
```

### 2. Vous travaillez sur Unity (VR)
- Ouvrez **Unity Hub**.
- Cliquez sur **Add** (Ajouter) et sélectionnez le dossier **`Application_Unity`** (pas la racine du repo car ya l'autre partie aussi ... ).
- Version requise : **Unity 6000.6.2f1** (avec le module Android (pas XR) si vous voulez build sur le Quest).
- Ouvrez la scène de test dans `Assets/Projet/Scenes_Niveaux/Sandbox_VR_Basique.unity` et appuyez sur Play ! (PC avec un bon GPU)

### 3. Vous travaillez sur le Gaussian Splatting & SAGA (Python)
- Allez dans `Traitement_Gaussian_SAGA/`.
- Installez l'environnement Python (idéalement avec un GPU Nvidia / CUDA) :
  ```bash
  pip install -r requirements.txt
  ```
- Les vidéos brutes pour entraîner les splats sont dans `Donnees_Brutes/`.
- Une fois votre scène ou objet reconstruit et segmenté, exportez le `.ply` dans `Modeles_Exportes/`, puis copiez-le dans le projet Unity (`Assets/Projet/Art_Graphismes/GaussianSplats/`).

---

- **Fichiers ignorés** : les dossiers de cache générés par Unity (`Library/`, `Temp/`, `.csproj`) et par Python (`__pycache__/`, `venv/`) sont automatiquement ignorés par le `.gitignore`.
