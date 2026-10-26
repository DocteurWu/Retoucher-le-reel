# Créer ses propres gestes de main

Ce guide explique comment le projet reconnaît un geste, et comment en ajouter un
nouveau sans casser les autres. Exemple suivi du début à la fin : le **pouce levé**.

---

## 1. Ce que le casque nous donne

Le package **XR Hands** fournit, pour chaque main suivie, la pose (position +
rotation) de **26 articulations** : `Wrist`, `Palm`, puis pour chaque doigt
`…Metacarpal`, `…Proximal`, `…Intermediate`, `…Distal`, `…Tip`
(le pouce n'a pas d'`Intermediate`).

```
            IndexTip
               │  IndexDistal
               │  IndexIntermediate
   ThumbTip    │  IndexProximal   ← jointure
      \        │
  ThumbDistal  └──── Palm ──── Wrist
```

⚠️ Ces positions sont dans l'**espace de tracking** (repère du Camera Offset),
pas dans le monde : pour placer quelque chose dans la scène, convertir avec
`VRHandGestures.ToWorld(position)` / `DirToWorld(direction)`.

⚠️ Avec ALVR / SteamVR, **l'orientation** des articulations n'est pas fiable
(axes de la paume inversés selon le runtime). On ne se sert donc que des
**positions**, et on en déduit des distances et des directions.

---

## 2. Les trois briques de base

Tout geste du projet est une combinaison de ces mesures (toutes dans
`VRHandGestures.cs`) :

| Mesure | Fonction | Valeur | Exemple d'usage |
|---|---|---|---|
| **Flexion** d'un doigt | `GetCurl(hand, XRHandFingerID.Index)` | 0 = tendu, 1 = replié dans la paume | poing, pistolet |
| **Pincement** pouce ↔ bout d'un doigt | `GetPinch(hand, XRHandFingerID.Index)` | 0 = écartés (≥ 5 cm), 1 = collés (≤ 1,5 cm) | saisie, redimensionnement |
| **Distance** entre deux articulations | `Vector3.Distance(a.position, b.position)` ou `DistanceToSegment(point, a, b)` | en mètres | tap du pouce sur le côté de l'index |

Et pour l'orientation : une **direction** entre deux articulations, comparée à
un axe du monde ou de la caméra avec `Vector3.Dot` (1 = même sens, 0 =
perpendiculaire, -1 = opposé).

---

## 3. La méthode, en 6 étapes

### Étape 1 — Mesurer avec l'atelier de gestes
1. Ajoute le composant **Atelier de gestes (debug)** (`VRGestureDebug`) sur
   n'importe quel objet de la scène (par ex. le XR Origin).
2. Lance la scène en casque : au-dessus de chaque main s'affichent les flexions
   des 5 doigts, les pincements, la distance pouce ↔ côté de l'index et les
   gestes déjà reconnus (en vert).
3. **Fais ton geste**, note les valeurs. Puis fais **les gestes voisins** (main
   ouverte, poing, pistolet…) et note aussi leurs valeurs : c'est ce qui
   distingue ton geste des autres qui compte.
4. Coche `logToConsole` pour retrouver les valeurs dans
   `%LOCALAPPDATA%\Unity\Editor\Editor.log` (lignes `[Gestes]`).

Pour le pouce levé, on observe par exemple :

| | Pouce | Index | Majeur | Annulaire |
|---|---|---|---|---|
| pouce levé | 0,10 | 0,85 | 0,90 | 0,88 |
| poing | 0,60 | 0,85 | 0,90 | 0,88 |
| main ouverte | 0,10 | 0,05 | 0,05 | 0,05 |

→ Ce qui le caractérise : **pouce tendu ET les autres repliés**. Et pour le
différencier d'un pouce tendu vers le bas, la **direction** du pouce : vers le haut.

### Étape 2 — Écrire une fonction PURE avec les seuils
Une fonction qui ne prend que des nombres (pas de casque) : elle est testable.
Choisis des seuils **au milieu** entre ton geste et les gestes voisins.

```csharp
// Dans VRHandGestures.cs, section « Calculs purs »
/// <summary>Pouce levé : pouce tendu, index / majeur / annulaire repliés.</summary>
public static bool IsThumbsUpPose(float thumbCurl, float indexCurl, float middleCurl, float ringCurl)
{
    return thumbCurl < 0.35f && indexCurl > 0.6f && middleCurl > 0.6f && ringCurl > 0.55f;
}
```

### Étape 3 — La version « casque »
Elle lit les articulations, calcule les valeurs et appelle la fonction pure.

```csharp
/// <summary>Pouce levé vers le haut, main suivie.</summary>
public static bool IsThumbsUp(XRNode node)
{
    if (!TryGetHand(node, out XRHand hand)) return false;

    if (!IsThumbsUpPose(GetCurl(hand, XRHandFingerID.Thumb), GetCurl(hand, XRHandFingerID.Index),
                        GetCurl(hand, XRHandFingerID.Middle), GetCurl(hand, XRHandFingerID.Ring)))
        return false;

    // Direction du pouce (base → bout), convertie dans le monde, comparée au haut.
    if (!TryGetJointPose(hand, XRHandJointID.ThumbProximal, out Pose basePouce)) return false;
    if (!TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose boutPouce)) return false;
    Vector3 dir = DirToWorld((boutPouce.position - basePouce.position).normalized);
    return Vector3.Dot(dir, Vector3.up) > 0.7f; // ≈ moins de 45° de la verticale
}
```

### Étape 4 — Stabiliser : hystérésis et tolérance
Le suivi tremble : une valeur juste au seuil fait clignoter le geste
(reconnu / perdu / reconnu…). Deux remèdes, déjà utilisés partout :

- **Hystérésis** : un seuil pour *entrer* dans le geste, un autre, plus
  tolérant, pour en *sortir*. Modèle : `IsThumbTapPressed(distance, wasPressed)`
  (appui sous 2,2 cm, relâché seulement au-delà de 3,5 cm).
- **Grâce** : on garde le geste quelques dixièmes de seconde après l'avoir
  perdu (main cachée par l'autre main, etc.). Modèle : `microPoseGrace` dans
  `VRTeleportation`.

Pour un **événement** (un « clic »), on ne réagit qu'au **front montant** :
`actif && !etaitActif`, sinon l'action se répète à chaque frame.

### Étape 5 — Tester sans casque
Ajoute un test dans `Editor/Tests/VRHandGesturesTests.cs` avec les valeurs
notées à l'étape 1 :

```csharp
[Test]
public void PouceLeve_Detecte_EtPasLePoing()
{
    Assert.IsTrue(VRHandGestures.IsThumbsUpPose(0.1f, 0.85f, 0.9f, 0.88f));   // pouce levé
    Assert.IsFalse(VRHandGestures.IsThumbsUpPose(0.6f, 0.85f, 0.9f, 0.88f));  // poing
    Assert.IsFalse(VRHandGestures.IsThumbsUpPose(0.1f, 0.05f, 0.05f, 0.05f)); // main ouverte
}
```
Lancer : *Window > General > Test Runner > EditMode > Run All*.
Si plus tard tu changes un seuil, le test te dit tout de suite si un geste
voisin devient reconnu par erreur.

### Étape 6 — S'en servir dans un script

```csharp
private bool _etaitLeve;

private void Update()
{
    if (!VRModalityManager.IsHandMode) return;      // les gestes n'existent qu'en mode mains
    bool leve = VRHandGestures.IsThumbsUp(XRNode.RightHand);
    if (leve && !_etaitLeve) Valider();              // front montant = une seule fois
    _etaitLeve = leve;
}
```

---

## 4. Gestes déjà pris (éviter les conflits)

Un nouveau geste ne doit ressembler à aucun de ceux-ci, sinon deux actions
partent en même temps.

| Geste | Main | Action | Défini dans |
|---|---|---|---|
| Poing fermé ou pincement pouce-index | les deux | saisir un objet ; escalade (main vide, mode Escalade) | `IsGrabGesture` |
| Pistolet (index tendu, majeur + annulaire repliés) | les deux | laser du menu | `TryGetFingerGun` |
| Pistolet + pouce rabattu (« tir ») | les deux | valider un bouton du menu | `TryGetFingerGun` (`isShootPressed`) |
| Poing lâche + tap du pouce sur le côté de l'index | droite | téléportation (1er tap = arc, 2e = départ) | `VRThumbMicrogestures` |
| Poing lâche + pouce qui glisse à gauche / droite sur l'index | droite | rotation par crans | `VRThumbMicrogestures` |
| Pincement des deux mains sur un objet | les deux | redimensionner | `VRScaleGrab` |
| Paume gauche vers le visage (~2 s) | gauche | menu poignet | `VRHandMenu` |
| Bout de l'index droit sur un doigt gauche | les deux | dupliquer / supprimer / récupérer (bulle) | `VRHandMenu` |

**Libres et faciles à reconnaître** : pouce levé / baissé, main ouverte paume
vers le haut, « V » (index + majeur tendus), « OK » (pincement pouce-index
avec les autres doigts tendus), poing gauche + tap du pouce (les microgestes
de la main gauche ne sont pas utilisés).

---

## 5. Pièges

- **Main cachée** : quand une main passe devant l'autre, celle de derrière n'est
  plus suivie (souvent la gauche). Prévoir une grâce, ne jamais déclencher une
  action sur la *perte* d'un geste.
- **Faux boutons d'ALVR** : en mode mains, ALVR simule des boutons de manette
  (X/Y, gâchette) à partir des gestes. Ne pas lire les boutons physiques en
  mode mains (`VRModalityManager.IsHandMode`).
- **Taille des mains** : préférer des rapports (la flexion en est un) ou des
  distances petites et nettes (un contact < 2 cm) à des distances absolues longues.
- **Gestes trop proches** : si deux gestes ne diffèrent que d'un doigt à
  mi-flexion, l'un sera reconnu à la place de l'autre. Exagérer la différence.
- **Valeurs modifiées en Play mode** : perdues à l'arrêt. Noter les bons
  seuils, puis les recopier hors Play mode.
