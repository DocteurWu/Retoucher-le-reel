# Premiers pas — Guide débutant (lire dans l'ordre)

Ce guide suppose que tu n'as jamais utilisé Git ni Unity en équipe. Suis les étapes
une par une, ça prend ~30 minutes la première fois, 2 minutes ensuite.

Tu as besoin : d'un compte GitHub (invité par Louaï), d'un PC Windows,
et — pour la partie VR seulement — du casque de l'équipe.

---

## Étape 0 — Installer les outils (une seule fois)

1. **Git** : https://git-scm.com/download/win → installe avec les options par défaut.
2. **Git LFS** : inclus dans Git récent. Vérifie en ouvrant PowerShell et en tapant :
   `git lfs version` → tu dois voir un numéro (ex. `git-lfs/3.7.0`). Si non, installe
   depuis https://git-lfs.com puis retape `git lfs install`.
3. **GitHub Desktop** (recommandé si tu débutes) : https://desktop.github.com → connecte
   ton compte GitHub dedans (`File > Options > Accounts > Sign in`).
4. **Unity Hub** : https://unity.com/download → installe-le, connecte ton compte Unity
   (gratuit, licence Personal).
5. **Unity 6000.6.2f1 EXACTEMENT** : dans Unity Hub, `Installs > Install Editor > Archive`,
   cherche `6000.6.2f1`, coche ces modules :
   - `Android Build Support` + `OpenJDK` + `Android SDK & NDK` (obligatoire pour la Quest),
   - `Windows Build Support` (ton OS).
   ⚠️ Pas une autre version : tout le monde doit avoir la même, sinon le projet casse.

## Étape 1 — Récupérer le projet (une seule fois)

**Avec GitHub Desktop (débutant) :**
1. `File > Clone Repository > URL` → colle `https://github.com/DocteurWu/Remodeler-le-reel.git`.
2. Choisis un dossier SIMPLE, sans accent ni espace, ex. `C:\Unity\remodeler-le-reel`.
   (Évite le Bureau avec des accents, ça crée des bugs.)
3. Ouvre PowerShell dans ce dossier et tape une fois : `git lfs install`, puis
   `git checkout develop` et `git pull`.

**Avec le terminal (si tu es à l'aise) :**
```powershell
git clone https://github.com/DocteurWu/Remodeler-le-reel.git C:\Unity\remodeler-le-reel
cd C:\Unity\remodeler-le-reel
git lfs install
git checkout develop
git pull
```

## Étape 2 — Ouvrir dans Unity (une seule fois)

1. Unity Hub → `Projects > Open > Open` → sélectionne le dossier cloné
   (celui qui contient `Assets`, `Packages`, `ProjectSettings`).
2. Si Hub propose une autre version d'Unity : **refuse**, installe la 6000.6.2f1 (étape 0).
3. Laisse Unity importer (barre de progression, 2-10 min la première fois — normal,
   il régénère le dossier `Library`, ne le committe jamais).
4. Ouvre `Assets/Scenes/SampleScene.unity`, clique ▶ `Play` : tu dois voir la scène
   d'exemple tourner. `Play` une 2e fois pour arrêter. 🎉 Ça marche en Flat.

Vérification : dans Unity, `Edit > Project Settings > Editor` doit afficher
`Version Control Mode = Visible Meta Files` et `Asset Serialization Mode = Force Text`.
(Ne change rien, c'est déjà réglé — c'est ce qui permet de travailler à plusieurs.)

## Étape 3 — Travailler au quotidien (à chaque session)

1. **Mets-toi à jour** (GitHub Desktop : `Fetch origin` puis `Pull`, branche `develop`).
   En terminal : `git checkout develop` puis `git pull`.
2. **Crée ta branche** : `Branch > New Branch` nommée `feature/ce-que-tu-fais`
   (ex. `feature/rebond-joueur`), basée sur `develop`.
3. **Travaille dans Unity**, de préférence dans TA scène bac à sable :
   `Assets/_Project/Scenes/Sandbox_TonPrenom.unity` (demande à Louaï de copier la scène
   d'exemple une fois pour créer la tienne). Ne touche jamais `Main.unity` sans prévenir.
4. **Sauvegarde** (`Ctrl+S`) puis **committe** souvent et petit :
   - GitHub Desktop : vérifie que seuls `Assets/`, `Packages/`, `ProjectSettings/` sont cochés
     (jamais `Library/`, `Temp/`, `Logs/`), écris un message clair (`feat: ajoute rebond`),
     `Commit to feature/...` puis `Push origin`.
   - Terminal : `git add Assets Packages ProjectSettings` puis
     `git commit -m "feat: ajoute rebond"` puis `git push -u origin feature/ma-branche`.
5. **Pull Request** : sur https://github.com/DocteurWu/Remodeler-le-reel, clique
   `Compare & pull request` vers `develop`, remplis le template (quoi / comment tester),
   demande une review à un coéquipier. Après validation : `Squash and merge`.
6. Reviens sur `develop` et `Pull` pour repartir à jour.

## Étape 4 — Utiliser le casque (quand c'est ton tour)

Le casque est partageable à tout moment : pas de créneau imposé, mais **annonce quand tu le
prends** (message équipe) et **rends-le dispo après ton test** (~15-30 min par test suffit).

**Préparer le casque Quest (une seule fois, avec le propriétaire) :**
1. Sur le téléphone (app Meta Horizon) : active le **mode développeur** du casque.
2. Branche le casque au PC en USB-C, accepte `Autoriser le débogage USB` dans le casque.
3. Deux façons de tester :
   - **Rapide (recommandé au quotidien)** : `Meta Quest Link` (câble) ou `Air Link` (Wi-Fi) →
     tu joues le projet PC directement dans le casque, sans build.
   - **Réaliste (avant une démo)** : `File > Build Profiles > Android > Build And Run` →
     ça installe l'APK sur le casque (5-15 min).

**Activer la VR dans le projet (à faire une fois, ensemble) :**
1. `Window > Package Manager > Unity Registry` → installe :
   `XR Plugin Management`, `Oculus XR Plugin`, `XR Interaction Toolkit`,
   `XR Device Simulator` (permet de simuler le casque sans le porter).
2. `Edit > Project Settings > XR Plug-in Management` → coche `Oculus` (onglets Android + Windows).
3. Ajoute un `XR Origin` (VR) dans ta scène de test via `GameObject > XR > XR Origin (VR)`.
4. Sans le casque, tu peux quand même tester avec le simulateur :
   le package `XR Device Simulator` simule casque + manettes au clavier/souris.

**Règle d'équipe avec 1 casque :**
- Tout se développe et se teste d'abord en **Flat** (tout le monde, tout le temps).
- Avant de merger une PR qui touche la VR : **1 test casque obligatoire** (par whoever l'a
  sous la main), avec une petite vidéo/capture dans la PR.
- Si le casque est pris : continue en Flat ou simulateur, ton travail n'est jamais bloqué.

## Si ça coince (débutants)

| Symptôme | Solution |
|---|---|
| Unity propose une autre version | Installe la 6000.6.2f1 (étape 0), n'ouvre jamais avec une autre |
| `Library` prend 10 min à importer | Normal la 1re fois, laisse finir |
| `git status` montre `Library/`, `Temp/` | Ne les committe pas : ils sont ignorés, décoche-les |
| Fichier `.meta` modifié après ajout d'asset | Normal : committe-le avec l'asset |
| Conflit sur une scène `.unity` | Préviens l'autre personne, accepte `develop`, refais ta modif à la main |
| Erreur LFS / fichier > 100 Mo refusé | `git lfs install` oublié ? Relance-le, puis recommite |
| Le casque n'est pas détecté | Câble USB-C données (pas charge seule), débogage USB accepté dans le casque |

En cas de doute : demande avant de forcer quoi que ce soit (`push --force` interdit sur
`develop`/`main`). Mieux vaut une question qu'un projet cassé. 🙂
