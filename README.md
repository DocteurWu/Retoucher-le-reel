# Remodeler le réel — Jeu 3D Unity (VR Quest visée)

Projet Unity **Universal 3D (Unity 6000.6.2f1, URP 17.6.0)**. Cible finale : **Quest / VR standalone**.
Stratégie : on valide d'abord le gameplay en **Flat (écran clavier/souris)**, puis on active la VR
(XR) sans changer de template. Un seul membre a un casque pour l'instant : le mode Flat permet
aux 3-5 personnes de travailler et tester au quotidien.

## 1. Prérequis (toute l'équipe, même version)

- **Unity Hub + Unity 6000.6.2f1 exactement** (voir `ProjectSettings/ProjectVersion.txt` — toute l'équipe installe la même mineure).
- **Git + Git LFS** : `git lfs install` une fois par machine.
- Modules Unity : `Android Build Support (pour Quest)` + `Windows/Mac` selon votre OS.
- Recommandé débutants : **GitHub Desktop**.

Ne jamais activer `Unity Version Control (Plastic)` : le versionning se fait avec Git + GitHub.

## 2. Premier setup

```powershell
git clone https://github.com/DocteurWu/Remodeler-le-reel.git
cd Remodeler-le-reel
git lfs install
git checkout develop
git pull
```

Ouvrir ensuite le dossier avec Unity Hub (version LTS figée). Laissez Unity régénérer `Library/`.

Réglage déjà en place (ne pas changer) :
`Edit > Project Settings > Editor > Version Control Mode = Visible Meta Files`,
`Asset Serialization Mode = Force Text`.

## 3. Organisation de l'espace de travail

```
Assets/_Project/
  Art/ (Models, Textures, Materials, Animations)
  Audio/ (Music, SFX)
  Code/ (Scripts, Shaders, Editor)
  Prefabs/
  Scenes/ (Main + Sandbox_Prenom pour tests perso)
  ScriptableObjects/
  UI/
```

Règles :
- Jamais d'asset en vrac à la racine de `Assets/`.
- Une scène de test par personne : `Assets/_Project/Scenes/Sandbox_Prenom.unity`.
- On ne committe que `Assets/`, `Packages/`, `ProjectSettings/`. Jamais `Library/ Temp/ Logs/ UserSettings/`.

## 4. Workflow Git (niveau mixte)

Branches : `main` (toujours jouable, protégée) / `develop` (intégration) / `feature/xxx-nom-clair`.

```powershell
git checkout develop
git pull
git checkout -b feature/ma-tache
# ... travail ...
git add Assets Packages ProjectSettings
git commit -m "feat: description claire"
git push -u origin feature/ma-tache
```

Puis **Pull Request vers `develop`** + 1 review obligatoire. Règle anti-conflit n°1 :
**jamais 2 personnes sur la même `.unity` en même temps**. Préfabriquez au maximum.

Voir `CONTRIBUTING.md` pour le pas-à-pas débutant.

## 5. Route VR Quest

Packages à ajouter quand le MVP Flat tourne (2 clics, pas de re-template) :
`XR Plugin Management + Oculus XR Plugin + XR Interaction Toolkit + XR Device Simulator`.

Contraintes Quest dès maintenant : URP, `Single Pass Instanced`, 72 fps, low-poly,
pas de post-process lourd. Test VR hebdo avec l'unique casque ; le reste du temps : Flat + simulateur.

## 6. Liens

- Repo : `https://github.com/DocteurWu/Remodeler-le-reel`
- `CONTRIBUTING.md` : guide quotidien. `Assets/_Project/README_STRUCTURE.md` : où ranger quoi.
