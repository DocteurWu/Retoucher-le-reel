# Authored by Louai HAMLAT - All rights reserved
"""
Extraction d'images rectilinéaires depuis une vidéo 360 équirectangulaire stabilisée (Insta360 Studio).
Découpe optimisée pour COLMAP et Gaussian Splatting (Splatfacto).
"""

import os
import sys
import argparse
import subprocess
import shutil

FFMPEG_PATH = r"C:\ffmpeg\bin\ffmpeg.exe"
if not os.path.exists(FFMPEG_PATH):
    FFMPEG_PATH = "ffmpeg"

# [LH] Découpage cubemap / rectilinear optimisé pour reconstruction SAGA (Louai H.)
def extract_views(video_path, output_dir, intervals, fps=0.5, resolution=1600, fov=100, clean_output=True):
    """
    Extrait les vues rectilinéaires pour chaque intervalle spécifié.
    
    :param video_path: Chemin du fichier MP4 équirectangulaire
    :param output_dir: Dossier de destination des images
    :param intervals: Liste de tuples [(start_sec, end_sec), ...]
    :param fps: Fréquence d'échantillonnage (ex: 0.5 = 1 image toutes les 2s)
    :param resolution: Largeur et hauteur de sortie en pixels (carré)
    :param fov: Champ de vision en degrés (horizontal et vertical)
    :param clean_output: Supprime les anciennes images si True
    """
    if clean_output and os.path.exists(output_dir):
        print(f"Nettoyage de l'ancien dossier d'images : {output_dir}")
        for item in os.listdir(output_dir):
            item_path = os.path.join(output_dir, item)
            if os.path.isfile(item_path):
                os.remove(item_path)
            elif os.path.isdir(item_path):
                shutil.rmtree(item_path)
    
    os.makedirs(output_dir, exist_ok=True)

    # 6 orientations horizontales réparties tous les 60° (recouvrement de 40° parfait pour COLMAP)
    yaw_angles = [0, 60, 120, 180, 240, 300]

    total_extracted = 0

    for idx, (start_t, end_t) in enumerate(intervals):
        duration = end_t - start_t
        print(f"\n--- Traitement de l'intervalle {idx+1}/{len(intervals)}: {start_t}s -> {end_t}s (durée: {duration}s) ---")

        for yaw in yaw_angles:
            prefix = f"pass{idx+1}_yaw{yaw:03d}"
            output_pattern = os.path.join(output_dir, f"{prefix}_%04d.png")

            # Filtre FFmpeg v360 : projection équirectangulaire vers perspective rectilinéaire
            v360_filter = (
                f"v360=input=equirect:output=rectilinear:"
                f"yaw={yaw}:pitch=0:roll=0:"
                f"h_fov={fov}:v_fov={fov}:w={resolution}:h={resolution}"
            )

            cmd = [
                FFMPEG_PATH,
                "-y",
                "-ss", str(start_t),
                "-t", str(duration),
                "-i", video_path,
                "-vf", f"fps={fps},{v360_filter}",
                "-q:v", "2",
                output_pattern
            ]

            print(f"Extraction Yaw {yaw}°...")
            res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            if res.returncode != 0:
                print(f"Erreur FFmpeg (Yaw {yaw}): {res.stderr[-300:]}")

    created_images = [f for f in os.listdir(output_dir) if f.lower().endswith(('.png', '.jpg'))]
    print(f"\n Extraction terminée avec succès !")
    print(f"Total d'images générées : {len(created_images)} dans {output_dir}")
    return len(created_images)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Extraction FFmpeg de vues rectilinéaires depuis vidéo 360")
    parser.add_argument("--video", type=str, default=r"C:\Users\Louaï\Desktop\Projet Ingé\Vidéo 360\Vidéo FabLAB 1210.mp4")
    parser.add_argument("--output", type=str, default=r"C:\Users\Louaï\Desktop\Retoucher-le-réel\Traitement_Gaussian_SAGA\Donnees_Brutes\FabLAB 1210\images")
    parser.add_argument("--fps", type=float, default=0.5, help="Images par seconde par angle (défaut 0.5)")
    parser.add_argument("--res", type=int, default=1600, help="Résolution en pixels carrés (défaut 1600)")
    parser.add_argument("--fov", type=int, default=100, help="Field of view en degrés (défaut 100)")
    
    args = parser.parse_args()
    
    # Par défaut, intervalles fictifs si non spécifiés
    print("Script prêt. En attente des timecodes exacts de l'utilisateur.")
