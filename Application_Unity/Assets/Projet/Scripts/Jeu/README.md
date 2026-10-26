# Le jeu — pas à pas

Ce dossier contient **le jeu** (la couche de narration, les objectifs, les
chapitres), par opposition à `Assets/Projet/Scripts/` qui contient **le module
VR** (saisir, dupliquer, supprimer, redimensionner, se déplacer).

## La règle du projet

> **1 étape = 1 fichier = 1 responsabilité.**
> Chaque étape est *jouable seule*, sans dépendre des étapes suivantes.

On construit l'escalier dans cet ordre. Chaque étape finit par une **fiche**
(`.md`) qui explique : ce que ça fait, où le poser, comment tester, ce qui peut
casser, et comment l'expliquer en 30 secondes.

## L'escalier

| # | Fichier | Ce que ça ajoute | Fiche |
|---|---|---|---|
| 1-2 | `NarrationSequence.cs` + `NarrationBulle.cs` | des répliques qui s'écrivent lettre par lettre, s'enchaînent ou se déclenchent sur un événement (saisie, regard, zone…), dans une bulle jamais cachée par le décor | en-têtes des deux fichiers |
| 3 | `VRUIPanel.cs` / `VRUIButton.cs` | un **bouton 3D cliquable au laser** (comme « Jouer ») | *à venir* |
| 4 | `NarrationSequence.cs` | le contenu de narration devient une **fiche éditable** (`.asset`) | *à venir* |
| 5 | `GameState.cs` (+ `VRInspectable.cs`) | la **mémoire** du jeu : les « clues » trouvées, sauvegardées | *à venir* |
| 6 | `ObjectiveDefinition.cs` (+ HUD) | les **objectifs** du puzzle | *à venir* |
| 7 | `GameFlow.cs` + `ChapterDefinition.cs` | le **passage d'une scène à l'autre** (fondu + narration) | *à venir* |
| 8 | `VRHistory.cs` | **annuler** une suppression / duplication | *à venir* |

> Arrêter après l'étape 4 donne déjà un jeu qui parle.

## Vocabulaire

| Mot | Sens dans le projet |
|---|---|
| **narration** | tout ce que le jeu dit au joueur (texte à l'écran, voix enregistrée) |
| **clue** | une information que le joueur *découv* en manipulant le réel (un objet examiné, une pièce retirée) |
| **objectif** | une chose à faire pour avancer (`Retrouver le cylindre`, `Examiner la machine`) |
| **chapitre** | une portion de jeu = **une scène Unity** + ses objectifs + sa narration d'entrée |

## Où vont les choses

| Quoi | Où |
|---|---|
| Scripts du jeu | `Assets/Projet/Scripts/Jeu/` |
| Fiches d'étape | `Assets/Projet/Scripts/Jeu/` (`.md` à côté des scripts) |
| Outils d'éditeur | `Assets/Projet/Scripts/Editor/` |
| Contenu (fiches de narration, objectifs, chapitres) | `Assets/Projet/Donnees_Config/` (dès l'étape 4) |
