# Guide de contribution au projet

Ce guide résume la méthode de collaboration sur GitLab pour l'ensemble des membres du groupe.

---

## 1. Démarrer une session de travail

1. Mettre à jour la branche d'intégration locale :
   ```bash
   git checkout develop
   git pull
   ```
2. Créer une branche dédiée à l'objectif du jour :
   ```bash
   git checkout -b feature/description-courte
   ```

---

## 2. Travail dans l'éditeur Unity

- **Scènes de test** : Travaillez dans votre scène personnelle située dans `Assets/Projet/Scenes_Niveaux/` (ex. `Sandbox_VR_Basique.unity` ou `BacASable_Prenom.unity`). Évitez de modifier les scènes partagées en même temps qu'un autre membre de l'équipe pour prévenir tout conflit sur les fichiers YAML.
- **Fichiers `.meta`** : Dès qu'un fichier ou dossier est créé, renommé ou déplacé dans Unity, le fichier `.meta` correspondant doit systématiquement être committé en même temps.

---

## 3. Préparer et pousser ses commits

Effectuez des commits ciblés et fréquents :
```bash
git status
git add Assets/ Packages/ ProjectSettings/
git commit -m "feat(vr): ajout de la téléportation et rotation par crans"
git push -u origin feature/description-courte
```

> **Important** : Ne committez jamais les dossiers générés en local par le moteur (`Library/`, `Temp/`, `Logs/`, `UserSettings/`). Ils sont d'ailleurs ignorés par le fichier `.gitignore`.

---

## 4. Soumettre une Merge Request (MR) sur GitLab

1. Rendez-vous sur la page GitLab du projet : https://git.sophia.minesparis.psl.eu/projets-movie-2026/retoucher-le-reel
2. Créez une **Merge Request** ciblant la branche `develop`.
3. Remplissez le modèle de description proposé (description des ajouts et tests réalisés).
4. Demandez une relecture (*Reviewer*) à au moins un membre du projet avant fusion.
