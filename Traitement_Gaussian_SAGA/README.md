# Pipeline Gaussian Splatting et Segmentation SAGA

Ce module regroupe l'ensemble des scripts de traitement, de reconstruction 3D et de segmentation d'objets. Il s'appuie sur ces dépôts open-source :
- **Reconstruction 3D** : [3D Gaussian Splatting (Inria / GraphDeco)](https://github.com/graphdeco-inria/gaussian-splatting)
- **Segmentation d'objets 3D** : [SAGA - Segment Any 3D Gaussians](https://github.com/Jumpat/SegAnyGAussians)
- **Visualisation / Intégration dans Unity** : [UnityGaussianSplatting (Aras Pranckevičius)](https://github.com/aras-p/UnityGaussianSplatting)

---

## 1. Organisation du dossier

* **`Donnees_Brutes/`** : Répertoire destiné à recevoir les vidéos ou séquences d'images capturées (fichiers volumineux gérés via Git LFS).
* **`Modeles_Exportes/`** : Fichiers 3D Gaussian Splatting exportés (format `.ply`), prêts pour l'intégration dans Unity.

---  

## 2. Outils nécessaires sur la machine Linux

Pour faire tourner le pipeline de reconstruction, on installe les paquets de base et l'environnement Python :

```bash
# 1. FFmpeg (pour découper le .insv)
sudo apt install -y ffmpeg

# 2. COLMAP avec support CUDA (carte Nvidia)
# Téléchargez l'AppImage officielle ou la version compilée avec CUDA depuis le GitHub de COLMAP.

# 3. Python + PyTorch CUDA + YOLO + OpenCV (pour gommer la silhouette en accéléré sur GPU)
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121
pip install ultralytics opencv-python numpy
```

*(Et on télécharge **LichtFeld Studio** pour Linux).*

---

## 3. Workflow complet : de la vidéo brute au modèle dans Unity

(Les étapes de 1 à 4 sont nécessaires comme 360SplatPro n'est dispo que sur la machine Windows)

### Étape 1 : Enregistrement de la séquence vidéo du lieu / objet
On fait bien attention à fixer les différents paramètres (ISO, balance des blancs, obturation manuelle...) pour éviter les variations de luminosité entre les images.
> **Matériel de capture** : Nous filmons via la caméra **Insta360 X4 en 8K à 30 FPS**.

### Étape 2 : Préparer les dossiers pour LichtFeld Studio
Sur la machine de calcul, on crée la structure attendue par LichtFeld Studio (en créant directement `sparse/0/` pour que COLMAP GUI puisse y exporter les binaires sans erreur) :
```bash
mkdir -p Projet_3DGS/Data/images
mkdir -p Projet_3DGS/Data/masks
mkdir -p Projet_3DGS/Data/sparse/0
cd Projet_3DGS
```

### Étape 3 : Extraire les 6 vues plates avec FFmpeg
Le fichier `.insv` de l'Insta360 contient 2 flux vidéo séparés :
- `[0:0]` : la lentille fisheye avant.
- `[0:1]` : la lentille fisheye arrière.

On redresse chaque fisheye en images plates classiques (`rectilinear` en 1600x1600) avec 3 angles de vue par lentille (centre, gauche, droite), ce qui donne 6 photos bien recouvrantes par seconde :

```bash
ffmpeg -y -i "chemin_vers/video.insv" -filter_complex "\
[0:0]fps=1,split=3[f0_a][f0_b][f0_c];\
[f0_a]v360=input=fisheye:output=rectilinear:ih_fov=190:iv_fov=190:h_fov=100:v_fov=100:w=1600:h=1600:yaw=0:interp=cubic[front];\
[f0_b]v360=input=fisheye:output=rectilinear:ih_fov=190:iv_fov=190:h_fov=100:v_fov=100:w=1600:h=1600:yaw=-45:interp=cubic[left_front];\
[f0_c]v360=input=fisheye:output=rectilinear:ih_fov=190:iv_fov=190:h_fov=100:v_fov=100:w=1600:h=1600:yaw=45:interp=cubic[right_front];\
[0:1]fps=1,split=3[f1_a][f1_b][f1_c];\
[f1_a]v360=input=fisheye:output=rectilinear:ih_fov=190:iv_fov=190:h_fov=100:v_fov=100:w=1600:h=1600:yaw=0:interp=cubic[back];\
[f1_b]v360=input=fisheye:output=rectilinear:ih_fov=190:iv_fov=190:h_fov=100:v_fov=100:w=1600:h=1600:yaw=45:interp=cubic[left_back];\
[f1_c]v360=input=fisheye:output=rectilinear:ih_fov=190:iv_fov=190:h_fov=100:v_fov=100:w=1600:h=1600:yaw=-45:interp=cubic[right_back]" \
-map "[front]"       -q:v 2 "Data/images/frame_%04d_cam0_front.jpg" \
-map "[left_front]"  -q:v 2 "Data/images/frame_%04d_cam1_leftfront.jpg" \
-map "[right_front]" -q:v 2 "Data/images/frame_%04d_cam2_rightfront.jpg" \
-map "[back]"        -q:v 2 "Data/images/frame_%04d_cam3_back.jpg" \
-map "[left_back]"   -q:v 2 "Data/images/frame_%04d_cam4_leftback.jpg" \
-map "[right_back]"  -q:v 2 "Data/images/frame_%04d_cam5_rightback.jpg"
```

### Étape 4 : Masquer la personne qui filme avec YOLO (Endroit + Envers, avec prévisualisation en direct)
Lors du tournage, on a dû tenir la caméra à l'envers au bout de la perche (pour filmer au ras du sol ou passer sous des obstacles). 
(Ce n'est pas un problème pour la 3D, car **COLMAP et LichtFeld Studio sont totalement invariants à la rotation**. En revanche, le modèle YOLO est pré-entraîné pour reconnaître des humains debout à l'endroit. Si l'image est inversée, il risque de nous rater...)

Le script ci-dessous effectue donc **un double scan pour chaque image** (une passe normale + une passe retournée à 180° pour détecter notre corps même si la caméra est à l'envers). Il sauvegarde les masques noirs/blancs pour COLMAP et LichtFeld, tout en affichant une fenêtre en direct (800x800) avec la zone gommée surlignée en rouge :

```bash
python3 -c '
import glob, os, cv2, numpy as np
from ultralytics import YOLO

os.makedirs("Data/masks", exist_ok=True)
model = YOLO("yolov8x-seg.pt")
kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (35, 35))

for p in sorted(glob.glob("Data/images/*.jpg")):
    name = os.path.basename(p)
    img = cv2.imread(p)
    h, w = img.shape[:2]
    pmask = np.zeros((h, w), dtype=np.uint8)

    # 1. Scan normal (camera a l endroit)
    res1 = model(img, classes=[0], conf=0.25, verbose=False)[0]
    if res1.masks is not None:
        for m in res1.masks.data:
            m_np = cv2.resize(m.cpu().numpy().astype(np.uint8), (w, h), interpolation=cv2.INTER_NEAREST)
            pmask = np.maximum(pmask, m_np)

    # 2. Scan retourne a 180 degres (au cas ou la camera est a l envers)
    img_flip = cv2.rotate(img, cv2.ROTATE_180)
    res2 = model(img_flip, classes=[0], conf=0.25, verbose=False)[0]
    if res2.masks is not None:
        for m in res2.masks.data:
            m_np = cv2.resize(m.cpu().numpy().astype(np.uint8), (w, h), interpolation=cv2.INTER_NEAREST)
            m_unflip = cv2.rotate(m_np, cv2.ROTATE_180) # On remet le masque dans le sens de l image
            pmask = np.maximum(pmask, m_unflip)

    # Dilatation pour gommer aussi l ombre et la perche
    mask = np.full((h, w), 255, dtype=np.uint8)
    if np.any(pmask > 0):
        mask[cv2.dilate(pmask, kernel, iterations=2) > 0] = 0

    # Sauvegarde du masque pour COLMAP et LichtFeld Studio (COLMAP attend nom_image.jpg.png)
    cv2.imwrite(f"Data/masks/{name}.png", mask)

    # Fenetre de controle en direct (zone effacee en rouge)
    preview = img.copy()
    preview[mask == 0] = (0, 0, 255)
    vis = cv2.addWeighted(img, 0.5, preview, 0.5, 0)
    cv2.imshow("Verification YOLO (Endroit + Envers) - Q pour quitter", cv2.resize(vis, (800, 800)))
    if cv2.waitKey(1) & 0xFF == ord("q"): break

cv2.destroyAllWindows()
'
```

Une fenêtre s'ouvre sur le bureau Linux : on voit défiler les images en temps réel avec la silhouette gommée en rouge (appuyer sur `Q` pour interrompre si besoin).


### Étape 5 : Calcul de poses avec COLMAP GUI
On ouvre l'interface graphique de COLMAP :

```bash
colmap gui
```

Dans l'interface :
1. **File -> New project** : créer la base de données `Data/database.db` et sélectionner le dossier d'images `Data/images`.
2. **Processing -> Feature extraction** :
   - Dans les options **ImageReader**, renseigner le champ `mask_path` en pointant vers le dossier `Data/masks`.
   - Modèle de caméra : choisir **PINHOLE** (puisque FFmpeg a déjà redressé les vues en perspective plate rectilinéaire sans distorsion optique) avec caméra partagée (`single camera`). Si besoin de corriger de micro-distorsions, on peut aussi laisser `OPENCV`.
3. **Processing -> Feature matching** : aller dans l'onglet **Sequential**, régler `overlap` entre **10 et 15** (pour couvrir 2 secondes de déplacement fluide sans risquer de faux raccords sur des murs unis) puis cliquer sur **Run**.
4. **Reconstruction -> Start reconstruction** : lancer le calcul 3D, puis exporter le modèle sparse via **File -> Export model** dans le dossier existant `Data/sparse/0`.

### Étape 6 : Entraînement dans LichtFeld Studio
Une fois le calcul COLMAP fini, on ouvre LichtFeld Studio :
1. Glisser le dossier `Data` dans le logiciel.
2. **SH Degree** : mettre à **1** (beaucoup plus léger et fluide pour le Quest 3).
3. **Mask Mode** : mettre sur **Ignore** (pour ne pas apprendre les pixels où la personne se trouve).
4. **Option GUT** : laisser décochée (les images sont déjà redressées à plat avec ffmpeg, pas besoin du mode fisheye).
5. Lancer l'entraînement (**Start Training**).
6. Une fois le résultat propre, faire **File -> Export** pour récupérer le fichier `.ply`.

### Étape 7 : Segmentation SAGA des objets interactifs

Prochainement ... 

### Étape 8 : Export et intégration dans Unity
1. Ranger le fichier `.ply` optimisé dans `Modeles_Exportes/`.
2. Le copier ensuite dans `Application_Unity/Assets/Projet/Art_Graphismes/GaussianSplats/` pour l'afficher et le manipuler dans la scène Unity.
