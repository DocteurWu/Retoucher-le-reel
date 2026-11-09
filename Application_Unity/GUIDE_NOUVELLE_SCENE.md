# Du nuage de points à une scène jouable (guide équipe)

Ce guide explique comment on passe **d'un scan (Gaussian Splat) à une scène VR où on peut
saisir, dupliquer et redimensionner des objets**. Exemple suivi : la scène 2 (la pièce et son canapé).

```
Scan (PLY)  ──►  Segmentation SAGA  ──►  PLY standard  ──►  Asset Unity  ──►  Scène + collider  ──►  Interactions
```

---

## 1. Préparer les PLY

### 1.1 Segmenter avec SAGA (si on veut un objet séparé du décor)
On exporte **deux PLY** depuis SAGA :
- `scene_sans_<objet>.ply` : le décor sans l'objet ;
- `<objet>.ply` : l'objet seul.

Les deux sont dans **le même repère** : placés au même endroit dans Unity, ils reforment la scène
complète, et l'objet peut ensuite bouger à part.

### 1.2 Si le PLY vient de SuperSplat (« .compressed.ply »)
Le plugin Unity ne le lit pas (erreur `expected N data bytes got M`). Il faut le décompresser :

```bash
pip install numpy                       # une seule fois
python Traitement_Gaussian_SAGA/Scripts_Entrainement/03_decompresser_ply_supersplat.py chemin/scene.compressed.ply
python Traitement_Gaussian_SAGA/Scripts_Entrainement/03_decompresser_ply_supersplat.py chemin/dossier   # tout un dossier
```
Résultat : `scene.compressed.decompresse.ply` (≈ 4 fois plus gros), c'est lui qu'on importe.
Les PLY déjà standard (sortie SAGA / LichtFeld) n'ont **pas** besoin de cette étape.

> Les `.ply` ne sont pas dans git (trop gros) : on se les passe par le drive.

---

## 2. Importer dans Unity

1. Copier le PLY dans `Assets/Projet/Prefabs_Objets/Gaussian/`.
2. Menu **Tools > Gaussian Splats > Create GaussianSplatAsset**.
3. *Input PLY* = le fichier, *Output Folder* = `Assets/Projet/Scenes_Niveaux/Gaussian`, puis **Create Asset**.
4. À répéter pour chaque PLY (décor, objet…). On obtient un `.asset` + 5 fichiers `.bytes`
   (ces `.bytes` partent dans **Git LFS** : vérifier que `git lfs` est installé avant de cloner / pousser).

---

## 3. Monter la scène

### 3.1 Partir du kit
Nouvelle scène vide, puis glisser **`Assets/Projet/Prefabs_Objets/VR/Kit Scene VR.prefab`**.
Il contient tout le nécessaire : rig VR (mains + manettes), menu poignet, lumière, **sol invisible**
(collider sur le calque `Ground`, indispensable pour la téléportation et l'escalade), narration, EventSystem.
Supprimer la *Main Camera* et la *Directional Light* créées par défaut.

Ajouter la scène dans **File > Build Profiles > Scene List** (sinon on ne peut pas la charger depuis le jeu).

### 3.2 Poser le décor
1. Créer un GameObject vide « Salle », y ajouter le composant **Gaussian Splat Renderer**, glisser l'asset du décor
   dans *Asset*.
2. Un splat est souvent **à l'envers** : mettre l'échelle **Y = -1**.
3. Régler la position Y pour que **le sol du splat soit à y = 0** (là où est le collider « Sol »).
   *Scène 2 : échelle (1, -1, 1) et position Y = -0,38.*

---

## 4. Rendre un objet interactif (l'exemple du canapé)

Un splat n'a **aucune physique** : ce ne sont que des points qui s'affichent. Pour qu'un objet soit
saisissable il lui faut un **collider + un Rigidbody**, posés sur un objet vide qui *contient* le splat.

```
Canape            ← Rigidbody + BoxCollider (+ composants optionnels)
└── Canape_Splat  ← Gaussian Splat Renderer (l'asset de l'objet seul)
```

Étapes :
1. Créer l'objet vide parent. **Ajouter un Rigidbody** (masse réaliste, *Collision Detection* = Continuous Dynamic,
   *Interpolate* = Interpolate) et un **BoxCollider**.
2. Mettre le splat de l'objet en **enfant**, avec **exactement le même Transform que le décor** pour qu'il reste à sa place
   (même échelle Y = -1, même décalage Y).
3. Ajuster le BoxCollider (bouton *Edit Collider*) pour qu'il colle à l'objet. Le collider ne suit pas
   la rotation d'un objet posé en biais : pour ça, **tourner le parent** de l'angle de l'objet et
   contre-tourner le splat enfant (c'est ce qui a été fait pour le canapé : -47°).
4. Mettre l'objet sur le calque **Grabbable**.

**Ce qui rend l'objet saisissable :** n'importe quel collider relié à un Rigidbody. Rien d'autre n'est obligatoire.

Composants optionnels (à ajouter sur le parent) :

| Composant | Effet |
|---|---|
| `VRThrowableRespawn` | si l'objet tombe sous le sol, il réapparaît à son point de départ |
| `VRHapticInteractable` | vibration des manettes à la saisie / au lâcher |
| `VRScalable` | redimensionnement (pincement à deux mains) |
| `VRDuplicable` | autorise / interdit la duplication et la suppression |

> Le plus simple : **copier ces composants depuis l'objet « Canape » de la Scene2** (ou la fraise de la scène 1).

---

## 5. Les interactions disponibles (mode mains)

| Action | Geste |
|---|---|
| Saisir | poing fermé ou pincement pouce-index sur l'objet |
| Redimensionner | pincement des **deux** mains (la 2e main doit toucher l'objet) |
| Se téléporter | main droite : poing lâche, **tap du pouce sur le côté de l'index** (1er tap = viser, 2e = partir) |
| Rotation par crans | main droite : **glisser le pouce** à gauche / droite sur l'index |
| Escalade | à la place de la téléportation, selon le réglage du menu |
| Dupliquer / supprimer / récupérer (sortir de la bulle) | index droit touche index / majeur / annulaire gauche |
| Menu poignet | paume gauche tournée vers le visage ~2 s |

Dans le menu d'entrée, le réglage **Téléportation / Escalade** s'applique à tout le jeu.
Pour créer ses propres gestes : `Assets/Projet/Scripts/GESTES.md`.

---

## 6. Épreuve et narration

- **Épreuve du récipient** : composant `EpreuveRecipient` sur un objet vide. On renseigne les objets à déposer et
  leur nombre (original + copies), la scène suivante, et le bac se construit tout seul au lancement.
  Les copies comptent même si l'original est supprimé.
- **Narration** : sur l'objet « Narration », **un composant `NarrationSequence` par réplique**. Chacune a un
  déclencheur (démarrage + délai, saisir, lâcher, dupliquer, téléportation, épreuve réussie, après une autre
  séquence…). Une seule bulle partagée, toujours visible, qui suit doucement le regard.
  Détails : `Assets/Projet/Scripts/Jeu/README.md`.

---

### Porte et tableau à accrocher
- **Porte** : composant `VRPorte` sur un objet vide placé **sur la charnière** (son axe Y = axe de rotation), avec un
  `BoxCollider` qui couvre le vantail et le splat de la porte en enfant. Se tire à la main comme un objet.
  Si elle s'ouvre du mauvais côté : `Sens` = -1.
- **Épreuve du tableau** : composant `EpreuveTableau` (tableau + `Emplacement` + `Ratio Cible`). Le cadre vert à la
  taille cible est visible dans la vue Scene ; déplacer l'objet `Emplacement_Tableau` pour choisir où accrocher.
- Outil qui installe tout ça d'un coup sur la scène céramique : *Retoucher le Reel > Outils > Installer les objets segmentés*.

### Montrer quoi faire au joueur
**Désactivé pour l'instant.** Une fois activé (`ProgressionEtapes` > *Montrer Les Objets*, couleur réglable), les objets
de l'étape en cours sont entourés d'une boîte lumineuse qui pulse, avec un losange au-dessus. Une nouvelle épreuve dit quels objets montrer
en redéfinissant `AjouterObjetsAMontrer`.

### Débloquer une partie pendant une démo
Sur l'écran du PC, en haut à gauche de la vue Game : un panneau avec les étapes (clic, ou touches **1-9**), **N** pour valider
l'étape suivante, **O** pour charger la scène suivante, **V** pour couper ou remettre la voix, **H** pour masquer le panneau. Cliquer d'abord une fois dans la vue
Game pour que le clavier soit pris en compte.

## 7. Pièges à éviter

- **Rien ne bouge / on tombe dans le vide** : il manque le collider « Sol » sous le rig ou il n'est pas sur le calque `Ground`.
- **Le splat flotte ou s'enfonce** : le sol du splat n'est pas à y = 0 (régler la position Y du décor).
- **Objet impossible à saisir** : collider ou Rigidbody manquant, ou collider désactivé.
- **Objet qui traverse le sol quand on le lance** : *Collision Detection* du Rigidbody à régler sur **Continuous Dynamic**.
- **Scène modifiée par un script externe pendant qu'elle est ouverte dans Unity** : la **recharger sans sauvegarder**,
  sinon Unity écrase la modification.
- **Valeurs modifiées en mode Play** : perdues à l'arrêt. Les noter, puis les recopier hors Play.
- **PLY « compressed »** : à décompresser avant l'import (§1.2).
