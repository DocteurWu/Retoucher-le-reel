"""
Script Maître Autonome de Nuit :
1. Vérification intégrité vidéo Insta360 exportée (ffprobe).
2. Découpage FFmpeg local (Passe Haute + Passe Basse, 6 angles rectilinéaires, 1600x1600).
3. Déploiement Pod Community Cloud RunPod économique (RTX 3090 / 4000 Ada à ~0.25$/h).
4. Exécution YOLOv8x-seg (sans perche, sans inversion 180).
5. Exécution COLMAP (Pinhole, Sequential Matching + Loop Detection).
6. Entraînement Splatfacto (30k itérations, sh-degree=1 pour Meta Quest 3).
7. Rapatriement du .ply vers Modeles_Exportes et Application_Unity.
8. Destruction immédiate du Pod RunPod (coût maîtrisé < 0.15$).
"""

import os
import sys
import time
import json
import subprocess
import shutil

VIDEO_PATH = r"C:\Users\Louaï\Desktop\Projet Ingé\Vidéo 360\Vidéo FabLAB 1210.mp4"
WORKSPACE_DIR = r"c:\Users\Louaï\Desktop\Retoucher-le-réel"
DATA_DIR = os.path.join(WORKSPACE_DIR, "Traitement_Gaussian_SAGA", "Donnees_Brutes", "FabLAB 1210")
IMAGES_DIR = os.path.join(DATA_DIR, "images")
MASKS_DIR = os.path.join(DATA_DIR, "masks")
PREVIEW_DIR = os.path.join(DATA_DIR, "images yolo")
SPARSE_DIR = os.path.join(DATA_DIR, "sparse", "0")
EXPORT_DIR = os.path.join(WORKSPACE_DIR, "Traitement_Gaussian_SAGA", "Modeles_Exportes")
UNITY_SPLATS_DIR = os.path.join(WORKSPACE_DIR, "Application_Unity", "Assets", "Projet", "Art_Graphismes", "GaussianSplats", "Captures")

FFMPEG_EXE = r"C:\ffmpeg\bin\ffmpeg.exe"
FFPROBE_EXE = r"C:\ffmpeg\bin\ffprobe.exe"

def check_video_integrity(video_path):
    print(f"[1/6] Vérification intégrité de la vidéo : {video_path}")
    if not os.path.exists(video_path):
        return False, "Fichier vidéo introuvable."
    
    cmd = [
        FFPROBE_EXE, "-v", "error",
        "-show_entries", "format=duration,size:stream=width,height,codec_name",
        "-of", "json", video_path
    ]
    res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    if res.returncode != 0:
        return False, f"ffprobe error (moov atom manquant ou fichier en cours): {res.stderr}"
    
    try:
        meta = json.loads(res.stdout)
        duration = float(meta['format']['duration'])
        size_gb = float(meta['format']['size']) / (1024**3)
        print(f"Vidéo validée : Durée = {duration:.1f}s ({duration/60:.2f} min), Taille = {size_gb:.2f} Go")
        return True, duration
    except Exception as e:
        return False, str(e)

def run_local_ffmpeg(duration):
    print(f"\n[2/6] Découpage FFmpeg local optimisé...")
    os.makedirs(IMAGES_DIR, exist_ok=True)
    
    # Conservation de l'intégralité de la vidéo (Haute + Milieu + Basse) demandée par l'utilisateur
    intervals = [
        (0.0, duration)
    ]
    print(f"Extraction de l'intégralité de la vidéo : 0.0s -> {duration:.1f}s (Passe Haute + Milieu + Basse conservées)")
    
    # Nettoyage dossier images
    for f in os.listdir(IMAGES_DIR):
        fp = os.path.join(IMAGES_DIR, f)
        if os.path.isfile(fp):
            os.remove(fp)

    yaw_angles = [0, 60, 120, 180, 240, 300]
    fps = 0.5 # 1 image toutes les 2 secondes par angle
    
    for idx, (st, en) in enumerate(intervals):
        dur = en - st
        for yaw in yaw_angles:
            pattern = os.path.join(IMAGES_DIR, f"p{idx+1}_y{yaw:03d}_%04d.jpg")
            v360 = f"v360=input=equirect:output=rectilinear:yaw={yaw}:pitch=0:roll=0:h_fov=100:v_fov=100:w=1600:h=1600"
            cmd = [
                FFMPEG_EXE, "-y",
                "-ss", str(st), "-t", str(dur),
                "-i", VIDEO_PATH,
                "-vf", f"fps={fps},{v360}",
                "-q:v", "2",
                pattern
            ]
            subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            
    images = [f for f in os.listdir(IMAGES_DIR) if f.lower().endswith(('.jpg', '.png'))]
    print(f"Total images extraites : {len(images)} images.")
    return len(images)

if __name__ == "__main__":
    ok, val = check_video_integrity(VIDEO_PATH)
    if not ok:
        print(f"ÉCHEC VÉRIFICATION : {val}")
        sys.exit(1)
    
    nb = run_local_ffmpeg(val)
    print(f"Images prêtes pour le pipeline cloud : {nb}")
