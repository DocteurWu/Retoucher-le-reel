# Contribuer — Guide quotidien (débutants bienvenus)

## Chaque matin

1. Ouvrez GitHub Desktop (ou terminal), `Current Branch = develop`, `Fetch origin` puis `Pull`.
2. Créez votre branche : `Branch > New Branch > feature/ce-que-je-fais` basée sur `develop`.

## Pendant le travail (Unity)

- Travaillez dans votre scène bac à sable `Assets/_Project/Scenes/Sandbox_VotrePrenom.unity`
  pour expérimenter. Ne touchez `Main.unity` que si personne d'autre ne la touche
  (annoncez-le sur le canal équipe).
- Sauvegardez la scène + projet (`Ctrl+S`) avant chaque commit.
- Si Unity propose de régénérer des `.meta`, laissez-le faire et committez-les avec.

## Committer (atomique, petit, souvent)

```powershell
git status --short
git add Assets/_Project Packages ProjectSettings
git commit -m "feat(scenes): ajoute rebond du joueur au bac a sable"
git push -u origin feature/ma-branche
```

- Préfixes : `feat:`, `fix:`, `art:`, `audio:`, `docs:`, `chore:`.
- Un commit = une idée. Pas de `Library/`, `Temp/`, `Logs/` (déjà ignorés).

## Pull Request

1. Sur GitHub : `Compare & pull request` vers `develop`.
2. Remplissez le template (quoi / comment tester / capture).
3. Demandez 1 review. Corrigez les retours dans la même branche (`commit + push`).
4. Après approbation : `Squash and merge`, puis supprimez la branche.
5. En local : `git checkout develop` + `git pull` pour repartir à jour.

## Conflit sur une scène ?

1. Ne forcez jamais (`push --force` interdit sauf votre propre branche feature, jamais sur `develop/main`).
2. Prévenez l'autre personne, décidez qui garde sa version.
3. En général : acceptez `develop`, refaites votre modif à la main dans Unity (petite perte < gros merge cassé).
4. Le merge YAML Unity (`UnityYAMLMerge`) est configuré en secours, mais la réapplication manuelle reste plus sûre pour les débutants.

## Check avant chaque PR

- [ ] Le jeu se lance en Flat (Play dans l'éditeur).
- [ ] `git status` ne montre ni `Library/`, ni `Temp/`, ni fichier > 50 Mo hors LFS.
- [ ] Les `.meta` des nouveaux assets sont inclus.
- [ ] Une capture ou courte description du test fait.
