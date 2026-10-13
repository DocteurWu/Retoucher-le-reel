# Données de Capture Vidéo — FabLAB 1210

Ce répertoire référence la capture brute réalisée pour la reconstruction 3D Gaussian Splatting et la segmentation SAGA du FabLab.

## 1. Fichier Vidéo Source (Brut)
- **Fichier** : `Vidéo FabLAB 1210 .insv`
- **Taille** : ~3.2 Go
- **Matériel** : Caméra Insta360 X4 (double optique Fisheye)
- **Résolution / Fréquence** : 8K à 30 FPS
- **Stockage** : En raison du plafond de taille HTTP (413 Request Entity Too Large) de l'instance GitLab, le fichier vidéo brut `.insv` est stocké sur la machine locale de calcul et sur le disque partagé du projet.

## 2. Données Découpées et Exploitables (Incluses dans le dépôt)
- **Dossier `images/`** : L'ensemble des **1392 images redressées** (`rectilinear` 1600x1600) extraites de la vidéo (6 vues orthogonales par seconde) est directement disponible et suivi via Git LFS.
- **Dossier `masks/`** : Masques pour YOLO / suppression de la silhouette de l'opérateur.
- **Dossier `sparse/0/`** : Structure prête pour l'export de nuage de points clairsemé COLMAP.
