# Nomenclature et organisation des dossiers du projet

Ce document présente l'organisation de l'arborescence du projet Unity afin d'assurer la cohérence de l'équipe et la traçabilité des ressources.

---

## 1. Structure principale dans `Assets/Projet/`

Tous les éléments développés pour le projet sont regroupés sous `Assets/Projet/` afin de ne pas interférer avec les packages ou réglages natifs du moteur Unity.

* **`Art_Graphismes/`** : Ressources visuelles et artistiques.
  * `Models3D/` : Modèles tridimensionnels (formats FBX, OBJ, GLTF).
    * `Characters/` : Modèles de personnages et avatars.
    * `Environment/` : Éléments de décors et environnement.
    * `Props/` : Objets interactifs et accessoires de scène.
    * `Scans/` : Maillages issus de numérisation ou photogrammétrie.
  * `Textures/` : Textures (fichiers PNG, JPG, EXR).
  * `Materials/` : Matériaux Universal Render Pipeline (URP).
  * `Animations/` : Clips d'animation et animateurs (`AnimatorController`).
* **`Audio/`** : Éléments sonores.
  * `Ambience/` : Nappes sonores spatialisées et sons d'ambiance.
  * `Music/` : Pistes musicales et boucles.
  * `SFX/` : Effets sonores courts et bruitages d'interaction.
  * `Voice/` : Enregistrements vocaux.
* **`Donnees_Config/`** : Objets de configuration typés (`ScriptableObject`).
  * Contient les réglages de gameplay, paramètres physiques ou profils spécifiques.
* **`Interface_UI/`** : Éléments de l'interface utilisateur.
  * Sprites, icônes, polices et chartes graphiques pour l'affichage écran et XR.
* **`Prefabs_Objets/`** : Préfabriqués réutilisables dans les scènes.
  * `Characters/` : Préfabriqués des entités mobiles et avatars.
  * `Environment/` : Éléments architecturaux et mobilier modulaire.
  * `XR/` : Composants matériels VR (`XR Origin`, contrôleurs et mains).
  * `UI/` : Menus interactifs en espace mondial (`World Space UI`).
* **`Scenes_Niveaux/`** : Scènes de l'application Unity.
  * `Principale.unity` : Scène d'assemblage officielle.
  * `BacASable_Louai.unity` / `Sandbox_VR_Basique.unity` : Scènes d'expérimentation isolées.
* **`Scripts/`** : Code source C# du projet.
  * Scripts de gameplay et de gestion des interactions VR (`VRGrabHand`, `VRTeleportation`, `VRSnapTurn`, etc.).
  * `Editor/` : Utilitaires et scripts d'automatisation propres à l'éditeur Unity.
  * `Shaders/` : Shaders personnalisés et graphes Shader Graph.

---

## 2. Dossiers techniques à la racine du projet

* **`Captures/`** : Dossier local destiné à recevoir les enregistrements et médias bruts volumineux (non versionné sur Git pour préserver la taille du dépôt).
* **`.gitlab/`** : Modèles de requêtes de fusion (*Merge Requests*) et configuration GitLab.

---

## 3. Bonnes pratiques de nommage et de gestion de version

1. **Intégrité des fichiers `.meta`** : Tout fichier ou dossier ajouté, renommé ou déplacé dans `Assets/` doit conserver son fichier `.meta` associé.
2. **Exclusion des répertoires générés** : Les dossiers de cache local (`Library/`, `Temp/`, `Logs/`, `UserSettings/`) ne doivent jamais être intégrés aux commits Git.
3. **Gestion des fichiers lourds** : Les formats volumineux (fichiers 3D `.fbx`, fichiers `.ply`, fichiers audio non compressés) sont automatiquement pris en charge par **Git LFS**.
