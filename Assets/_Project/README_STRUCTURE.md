# Où ranger quoi

## Dans Unity : `Assets/_Project/`

- `Art/Models3D/` : modèles classiques (FBX, GLB, OBJ).
  - `Characters/` : personnages. `Environment/` : décors. `Props/` : objets.
  - `Scans/` : scans 3D / photogrammétrie (maillages).
- `Art/GaussianSplats/` : **splatting gaussien** (fichiers `.ply`, `.splat` — en LFS).
  - `Captures/` : splats bruts issus du traitement (un sous-dossier par lieu/objet).
  - `Processed/` : splats nettoyés et optimisés, prêts pour les scènes.
- `Art/Textures` : PNG, JPG, TGA, EXR partagés.
- `Art/Materials` : matériaux URP uniquement.
- `Art/Animations` : clips + controllers.
- `Audio/Music` : boucles musique. `Audio/SFX` : effets courts.
- `Audio/Ambience` : nappes et ambiances de lieu. `Audio/Voice` : voix et dialogues.
- `Code/Scripts` : C# gameplay. `Code/Shaders` : Shader Graph. `Code/Editor` : outils éditeur.
- `Prefabs/` : tout objet réutilisé — `Characters`, `Environment`, `Gaussian` (splats),
  `XR` (rig, mains), `UI`. La scène ne contient que de l'assemblage.
- `Scenes/` : `Main.unity` (assemblage officiel) + `Sandbox_Prenom.unity` (tests perso,
  1 par personne) + `Gaussian/` (scènes de test des splats).
- `ScriptableObjects/` : réglages, équilibrage, config (+ `Gaussian/` : réglages de splats).
- `UI` : sprites et éléments d'interface (les prefabs UI vont dans `Prefabs/UI`).

## Hors Unity : `Captures/` (racine du dépôt)

Vidéos brutes tournées au téléphone pour fabriquer les splats : **ne jamais les committer**
(ignorées par Git). Gardez-les en local, ne partagez que les `.ply` traités dans
`Art/GaussianSplats/`. Petits fichiers texte (notes de tournage) acceptés.

## Règles

- Ne créez jamais de dossier à la racine de `Assets/` : tout passe par `_Project/`.
- Un asset = son `.meta` committé avec lui (Unity les génère à l'ouverture).
- Gros fichiers (`FBX`, `PLY`, `MP3`, `MP4`...) : vérifiez qu'ils partent en LFS
  (`git lfs ls-files` doit les lister après le commit).
