# Retoucher le réel — Module VR (Unity 6 / OpenXR / Quest 3)

Ce document est le **point d'entrée** pour comprendre le fonctionnement du module VR
de ce projet : il décrit l'architecture, les commandes, les choix de conception
(**et pourquoi**), les pièges déjà payés, et la procédure de test.

> **Pour une IA ou un nouveau développeur** : lire ce fichier EN PREMIER, puis le
> résumé de chaque script (en-tête du fichier `.cs`), puis le code.

---

## 1. Ce que fait le projet

Application VR de **retouche/réparation de « vrais » objets** : on saisit un objet
réel (scanné en Gaussian Splatting), on peut **le dupliquer**, **le supprimer**,
**le redimensionner à deux mains**, et **le lancer**. Le joueur se déplace dans
la scène (locomotion continue ou téléportation), grimpe, saute, se retourne.

- **Moteur** : Unity `6000.0.84f1` (Unity 6)
- **XR** : OpenXR (`com.unity.xr.openxr` 1.12) — Quest 3 (Touch Plus) en
  autonome **et** en PCVR (Quest Link / Air Link)
- **Input** : `com.unity.inputsystem` 1.11.2 (Input System Package **seul**,
  `activeInputHandler = 1` : l'ancien Input Manager est désactivé)
- **Rendu** : URP 17.0.3
- **S Scenes** : `Assets/Projet/Scenes_Niveaux/`
  - `Menu.unity` — menu d'entrée (canvas world-space, laser de visée)
  - `Scene.de.base1.unity` — scène de jeu (celle que lance le bouton *Jouer*)

---

## 2. Architecture du « rig » VR

```
XR Origin (VR)                     ← VRSmoothLocomotion, VRSnapTurn, VRTeleportation
└── Camera Offset                  ← VRHeadTracker est sur la Main Camera
    ├── Main Camera                ← VRHeadTracker (pose de la tête)
    ├── Left Controller            ← VRGrabHand + VRDuplicateDelete
    └── Right Controller           ← VRGrabHand + VRScaleGrab
```

**Aucun composant XRI n'est utilisé** (ni `XROrigin`, ni `ActionBasedController`,
ni `TrackedPoseDriver`) : tout est écrit à la main dans `Assets/Projet/Scripts/`.
C'est un choix délibéré (contrôle total, pas de dépendance au Input System
« Action »), mais c'est aussi la source de tous les pièges de la section 6.

### Suivi de pose

| Script | Rôle |
|---|---|
| `VRHeadTracker` | Écrit la pose réelle du casque dans le Transform de la **Main Camera** (local). Demande le mode d'origine **Floor** au sous-système XR. |
| `VRGrabHand` | Écrit la pose réelle de la manette dans son Transform (local), puis gère la **saisie** et le **lancer**. |

La pose est écrite en **local** (et non en monde) parce que la caméra et les
manettes sont enfants de l'XR Origin, que la locomotion/téléportation/snap turn
déplacent ou font tourner : l'écriture en local garantit que la tête et les
mains restent correctement placées dans le repère du joueur.

> ⚠️ Conséquence **assumée** (comportement standard XRI) : au snap turn, l'XR
> Origin tourne de 45°, donc **les manettes virtuelles tournent avec le monde**
> (elles s'écartent de la manette physique). C'est le comportement de tous les
> jeux VR ; si un jour on veut des mains « collées » au physique, il faudra
> introduire un *tracking origin* explicite (compensation de rotation).

---

## 3. Commandes

### En VR (Quest 3, OpenXR)

| Commande | Manette | Effet |
|---|---|---|
| **Stick gauche** (avant/arrière/gauche/droite) | gauche | **Locomotion continue** (2 m/s avant, 1,5 m/s latéral) |
| **Stick gauche** (poussé puis relâché) | gauche | **Téléportation** balistique (uniquement si le mode « Téléportation » est sélectionné dans le menu) |
| **Stick droit** (gauche/droite) | droite | **Snap turn** 45° (désactivé tant qu'un objet est tenu) |
| **Stick droit** (haut/bas) | droite | **Échelle** de l'objet tenu (1,5×/s) |
| **Gâchette (trigger)** | les deux | **Saisir / relâcher** l'objet (lâcher = lancer avec la vitesse de la main) |
| **Poignée (grip)** | les deux | **Saisir** aussi ; maintenue pendant un objet tenu = **pincement à deux mains** pour l'échelle |
| **Bouton X** | gauche | **Dupliquer** l'objet tenu |
| **Bouton Y** | gauche | **Supprimer** l'objet tenu |
| **Bouton A** | droite | **Sauter** (3,8 m/s, gravité 9,81 m/s²) |
| **Bouton B** | droite | *(libre, réservé)* |

> Les boutons X/Y sont **sur la manette gauche** : pour dupliquer, il faut donc
> tenir l'objet (souvent de la main droite) et appuyer sur X de la main gauche.
> `VRDuplicateDelete` lit le bouton sur la main configurée, **quelle que soit la
> main qui tient l'objet**.

### Dans l'éditeur (sans casque)

| Touche | Effet |
|---|---|
| `W A S D` | stick gauche (déplacement) |
| `Flèches` | stick gauche (déplacement secondaire) |
| `Espace` | bouton A (saut) |
| `X` / `Y` | dupliquer / supprimer |
| `Clic droit` | main droite (saisir) |
| `Clic gauche` | main gauche (saisir) |
| `Q` / `D` ou `←` / `→` | snap turn |

> Les raccourcis clavier ne sont actifs **que s'il n'y a aucun casque détecté**
> (`VRDeviceState.EditorFallbackAllowed`). Avec un casque branché, le clavier
> est totalement ignoré (sinon « A » sautait *et* strafrait en même temps).

---

## 4. Les scripts, un par un

| Script | Rôle | Points d'attention |
|---|---|---|
| `VRInput.cs` | Couche d'entrée : `VRInputLock` (verrou d'échelle), `VRDeviceState` (casque connecté ?), `VRInputControls` (**lecteur de contrôles sûr**), `VRButtonReader` (X/Y/A/B), `VRStickReader` (sticks) | ⚠️ Ne **jamais** utiliser `GetChildControl` : voir section 6.1 |
| `VRHeadTracker.cs` | Pose de la tête + mode d'origine Floor | Écrit en local |
| `VRGrabHand.cs` | Suivi 6-DOF de la manette, **saisie** (parentage + cinématique), **lancer** (moyenne de 8 frames) | `Physics.SyncTransforms()` obligatoire (section 6.4) |
| `VRScaleGrab.cs` | Échelle de l'objet tenu (stick **et** pincement à deux mains) | Un seul geste à la fois ; arme le verrou d'entrée |
| `VRDuplicateDelete.cs` | Dupliquer / supprimer l'objet tenu | S'exécute **après** la saisie (`DefaultExecutionOrder(10)`) |
| `VRSmoothLocomotion.cs` | Locomotion continue + **module vertical** (saut, gravité, sol) | Section 6.2 |
| `VRSnapTurn.cs` | Rotation par crans (45°) autour de la tête | Désactivé si un objet est tenu |
| `VRTeleportation.cs` | Téléportation balistique (arc parabolique + réticule) | Masque de layer : **layer 6 (Ground) uniquement** |
| `VRThrowableRespawn.cs` | Remet un objet sur son socle s'il tombe sous le monde | |
| `VRScalable.cs` | Marquage d'un objet : limites d'échelle, masse | |
| `VRDuplicable.cs` | Marquage d'un objet : droits de duplication/suppression | |
| `VRCloneOrigin.cs` | Lie un clone à sa source (compte des exemplaires) | |
| `VRLocomotionSettings.cs` | Mode de locomotion (Continu / Téléportation), persisté en `PlayerPrefs` | Lu par le menu **et** par la scène de jeu |
| `MainMenu.cs` | Menu world-space : laser de visée, bouton *Jouer*, bascule du mode | |
| `VRInputDiagnostics.cs` | **Outil de diagnostic** (voir section 7) | Aucun coût au runtime (classe statique) |

### Ordre d'exécution (`DefaultExecutionOrder`)

| Ordre | Script | Pourquoi |
|---|---|---|
| **−50** | `VRScaleGrab` | Arme le verrou d'entrée **avant** que la saisie ne le lise |
| **0** | `VRGrabHand` | La saisie doit voir le verrou du scale dans la **même frame** |
| **0** | `VRSmoothLocomotion`, `VRSnapTurn`, `VRTeleportation`, `VRHeadTracker` | |
| **+10** | `VRDuplicateDelete` | Doit connaître l'objet tenu **à l'instant** (même frame que la saisie) |

---

## 5. Le verrou d'entrée (`VRInputLock`)

Problème résolu : le « mode échelle » doit interdire à la main libre de saisir un
objet pendant que le joueur redimensionne. Un verrou statique était purgé par un
`GameObject` créé au chargement (`RuntimeInitializeOnLoadMethod`) — **ce purgeur
mourait au changement de scène Menu → Jeu**, et le verrou restait armé **pour
toujours** : plus aucune saisie, plus de snap turn, plus de locomotion.

**Solution actuelle** : le verrou est armé **pour une seule frame**
(`_armedFrame == Time.frameCount`). Il expire tout seul, rien à maintenir en
vie, et aucun scénario ne peut le laisser bloqué. Le geste qui l'arme doit le
**ré-armer à chaque frame** (`VRScaleGrab.ContinuePinch` / `UpdateThumbstick`).

---

## 6. Les pièges déjà payés (ne pas les rejouer)

### 6.1 `GetChildControl` LÈVE une exception ⚠️⚠️⚠️

`InputControl.GetChildControl<T>("xButton")` ne renvoie **pas** `null` quand le
contrôle n'existe pas : il **lève une `ArgumentException`**.

Nos périphériques n'ont pas tous les mêmes contrôles : le profil *Meta Quest
Touch Plus* n'a pas de contrôle `xButton` (il a un **alias** `X`), le casque n'a
pas de `grip`, etc. Chaque appel raté **interrompait la frame en cours** :

- `VRSmoothLocomotion.HandleJumpAndGravity` s'arrêtait net → **ni gravité, ni
  saut**, et `HandleHorizontalMovement` (appelé juste après) **n'était jamais
  exécuté** → aucune locomotion continue ;
- `VRDuplicateDelete.Update` s'arrêtait net → duplication et suppression mortes ;
- la console était saturée d'exceptions.

**Règle absolue :** toujours passer par `VRInputControls.Find<T>(device, nom)`
ou `FindAny<T>(device, nom1, nom2…)`. Ces helpers cherchent par **nom ou alias**,
ne lèvent **jamais**, et rendent un `null` si le contrôle est absent.

En plus, `VRButtonReader.Read/WasPressed` et `VRStickReader.Read` sont enveloppés
dans un `try/catch` (filet de sécurité) : une exception est signalée **une seule
fois** (`VRInputLog`) et n'interrompt plus la frame.

### 6.2 Le module vertical : distinguer « sol » et « posé » ⚠️

Deux notions **différentes**, à ne plus confondre :

| Notion | Test | Usage |
|---|---|---|
| **Un sol existe sous les pieds** | le rayon de 4 m touche une surface marchable | le joueur doit-il tomber ? |
| **Le joueur est posé dessus** | cette surface est à moins de `groundedDistance` (0,35 m) de ses pieds | le joueur peut-il sauter ? |

Si on confond les deux : le joueur **flotte** au bord d'une ledge (le sol est
encore trouvé à 4 m ⇒ il se croit au sol) ou il **tombe** alors qu'il ne
devrait pas tomber.

Autres règles du module :

- la sonde de sol part des **PIEDS** (le rig), **pas de la tête** : la tête peut
  être en avant du sol (le sol de la scène ne fait que 20 × 20 m) ;
- le joueur ne **descend** sur le sol que de quelques centimètres à la fois
  (`maxStepHeight` = 0,35 m) : il ne peut jamais être aspiré sur un meuble ;
- la gravité ne s'applique **que s'il existe un sol, ou si le joueur a déjà été
  posé** : dans une scène **sans sol** (le **menu** n'a aucun layer 6), le
  joueur ne tombe pas — sinon il tombait à l'infini, le filet de sécurité le
  ramenait à y=0, et il recommençait (**boucle de chute**) ;
- le filet de sécurité ramène le joueur à sa **dernière position valide**, pas à
  y=0 (sinon : rebouclage hors de la zone marchable) ;
- **coyote time** : on peut sauter juste après avoir quitté le sol (0,12 s).

### 6.3 `Physics.autoSyncTransforms = 0`

Le projet a `autoSyncTransforms = 0` (ProjectSettings → Dynamics Manager).
Conséquences sur la saisie :

- le **collider** de l'objet tenu reste en retard sur le visuel → l'objet
  « colle » mais ne pousse rien et se fait pousser ;
- au relâchement, la physique **réimpose sa propre pose** → l'objet « repart »
  à l'endroit d'où il a été pris.

**Règle** : `Physics.SyncTransforms()` après chaque parentage/déparentage, et
écrire la pose sur le **Rigidbody** (`body.position` / `body.rotation`) avant de
le repasser en dynamique.

### 6.4 `XRSettings.isDeviceActive` est obsolète

En Unity 6, `UnityEngine.XR.XRSettings.isDeviceActive` ne reflète pas l'état
réel du runtime OpenXR → les raccourcis clavier de l'éditeur restaient actifs
**casque branché**. Utiliser `VRDeviceState.HmdConnected` (présence d'un
`XRController`/`XRHMD` dans l'Input System).

### 6.5 La duplication doit venir **après** la saisie

`VRDuplicateDelete` était en `DefaultExecutionOrder(-40)`, donc **avant**
`VRGrabHand` (0) : une duplication demandée dans la même frame que la saisie
était ignorée. Il est maintenant en **+10**.

### 6.6 Un objet ne peut pas être dans deux mains

`VRGrabHand.FindClosestGrabbable` **exclut** les objets déjà tenus (au lieu
d'annuler toute la saisie). Avant, si l'objet de l'autre main entrait dans la
sphère de préhension (18 cm), la main ne pouvait plus rien attraper du tout.

---

## 7. Diagnostiquer un problème

`VRInputDiagnostics` est une **classe statique** : aucun composant en scène, aucun
coût au runtime. Pour obtenir le rapport complet :

- dans le débogueur de l'Unity (fenêtre **Console**, bouton **Evaluate / Inspect
  > *Expression Evaluator***), évaluer :
  ```csharp
  RemodelerLeReel.VR.VRInputDiagnostics.BuildReport()
  ```
- ou appeler `Debug.Log(VRInputDiagnostics.BuildReport())` temporairement dans un
  `Update`.

Le rapport contient : le loader XR actif et le mode d'origine du suivi, **tous**
les périphériques de l'Input System (layout, usages, **tous** leurs boutons/axes et
leurs valeurs), les périphériques de l'ancienne API XR, la lecture effective de
X/Y/A/B (avec le contrôle qui a réellement répondu), l'état du jeu (verrou,
locomotion, objet tenu, position de l'XR Origin, position des mains) et le
nombre d'exceptions neutralisées.

---

## 8. Procédure de test

1. **Lancer par le menu** (`Menu.unity` → bouton *Jouer*) : certains bugs
   n'apparaissent qu'au changement de scène.
2. Le mode de locomotion par défaut est **Mouvement Continu** ; le menu permet
   de basculer en Téléportation (persisté en `PlayerPrefs`).
3. Checklist :
   - le stick gauche déplace le joueur ;
   - X duplique (le compteur de clones du rapport monte), Y supprime ;
   - A fait sauter **et le joueur redescend** ;
   - saisir le cylindre : l'objet suit la main et **ne revient pas en arrière**
     au relâchement ;
   - tenir un objet + stick droit : l'échelle fonctionne, et **au relâcher la
     saisie repart** (c'était le verrou définitif) ;
   - marcher hors du sol (le plan fait 20 × 20 m) : **le joueur tombe** ;
   - dans le menu : **aucune chute**.
4. Pour un diagnostic complet : section 7.

---

## 9. Limites connues / travail futur

- **Pas de collisions** avec le décor : le joueur traverse le socle, la machine
  et les objets (choix assumé ; un `CharacterController` sur l'XR Origin
  demanderait de coupler les modules vertical et horizontal).
- **Pas de rampe ni de marches** automatiques au-delà de `maxStepHeight`.
- Le snap turn fait tourner les manettes virtuelles avec le monde (standard XRI).
- Les objets *Gaussian Splatting* n'ont pas de vraie géométrie : la collision
  repose sur des `BoxCollider` approximatifs.
- `Physics.autoSyncTransforms` est laissé à `0` (performance) : chaque
  manipulation de transform par le code doit être suivie d'un
  `Physics.SyncTransforms()`.

---

## 10. Historique des correctifs (pour ne pas réintroduire les bugs)

| Commit | Sujet |
|---|---|
| `ddfce8f` | Lecture directe `XRController` pour les boutons et les sticks, `forceSmooth`, HUD de diagnostic |
| `34d1ed6` | Verrou d'entrée à durée de frame, module vertical réécrit, sync physique à la saisie, ordre d'exécution de la duplication, `VRDeviceState`, HUD world-space |
| `3749572` | Ancres du HUD (hauteur nulle ⇒ HUD invisible dans le casque) |
| `6286c0b` | **Plus aucune exception `GetChildControl`** (le bug qui cassait gravité, saut, locomotion, duplication et suppression) + `VRInputDiagnostics` |
| `4e88611` | Plus de boucle de chute dans le menu (scène sans sol) |
