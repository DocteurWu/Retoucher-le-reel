# Authored by Louai HAMLAT - All rights reserved
"""
Génération optimisée des masques de segmentation pour la reconstruction 3D.
- Détecte et masque UNIQUEMENT la silhouette de l'opérateur (classe 0 'person') via YOLOv8x-seg.
- Supprime tout masque statique de perche (la perche étant déjà gommée par Insta360 Studio).
- Fonctionne sur GPU CUDA ou CPU avec dilatation morphologique pour éviter les artefacts de bordure.
"""

import os
import sys
import glob
import argparse
import shutil
import cv2
import numpy as np
import torch
from ultralytics import YOLO

# [Louai Hamlat] Génération automatique de masques SAM / SAGA multi-angles
def process_masks(images_dir, masks_dir, preview_dir=None, conf=0.25, batch_size=8, clean_output=False):
    if clean_output:
        for folder in [masks_dir, preview_dir]:
            if folder and os.path.exists(folder):
                print(f"Nettoyage du dossier existant : {folder}")
                shutil.rmtree(folder)

    os.makedirs(masks_dir, exist_ok=True)
    if preview_dir:
        os.makedirs(preview_dir, exist_ok=True)

    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(f"=== Accélération Détectée : {device.upper()} ===")
    if device == "cuda":
        print(f"GPU Actif : {torch.cuda.get_device_name(0)}")
        torch.backends.cudnn.benchmark = True

    print("Chargement de YOLOv8x-seg...")
    model = YOLO("yolov8x-seg.pt")
    model.to(device)

    # Noyau de dilatation morphologique (35x35) pour couvrir les mains, vêtements et bords flous
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (35, 35))

    image_paths = sorted(glob.glob(os.path.join(images_dir, "*.*")))
    image_paths = [p for p in image_paths if p.lower().endswith(('.jpg', '.jpeg', '.png'))]
    total_imgs = len(image_paths)
    print(f"Total d'images à traiter : {total_imgs}")

    if total_imgs == 0:
        print("Aucune image trouvée dans le répertoire spécifié.")
        return

    # Reprise idempotente si des masques existent déjà
    pending_paths = []
    for p in image_paths:
        fn = os.path.basename(p)
        stem, _ = os.path.splitext(fn)
        tc = os.path.join(masks_dir, f"{fn}.png")
        tn = os.path.join(masks_dir, f"{stem}.png")
        prev = os.path.join(preview_dir, fn) if preview_dir else None

        if os.path.exists(tc) and os.path.exists(tn) and (prev is None or os.path.exists(prev)):
            continue
        pending_paths.append(p)

    already_done = total_imgs - len(pending_paths)
    if already_done > 0:
        print(f"Reprise automatique : {already_done}/{total_imgs} déjà traitées. Restantes : {len(pending_paths)}")

    if not pending_paths:
        print("Toutes les images ont déjà été masquées !")
        return

    for i in range(0, len(pending_paths), batch_size):
        batch_paths = pending_paths[i:i + batch_size]
        batch_imgs = [cv2.imread(p) for p in batch_paths]

        valid_items = [(p, img) for p, img in zip(batch_paths, batch_imgs) if img is not None]
        if not valid_items:
            continue

        imgs = [it[1] for it in valid_items]
        paths = [it[0] for it in valid_items]

        # Inférence YOLO (personnes uniquement, classe 0)
        results = model(imgs, classes=[0], conf=conf, device=device, verbose=False)

        for idx_in_batch, (p, img) in enumerate(zip(paths, imgs)):
            fn = os.path.basename(p)
            stem, _ = os.path.splitext(fn)
            h, w = img.shape[:2]
            pmask = np.zeros((h, w), dtype=np.uint8)

            r = results[idx_in_batch]
            if r.masks is not None:
                for m in r.masks.data:
                    m_np = cv2.resize(m.cpu().numpy().astype(np.uint8), (w, h), interpolation=cv2.INTER_NEAREST)
                    pmask = np.maximum(pmask, m_np)

            # Masque COLMAP / Nerfstudio : 255 = décor conservé, 0 = exclu de l'apprentissage
            mask = np.full((h, w), 255, dtype=np.uint8)

            # Dilatation morphologique de la silhouette humaine
            if np.any(pmask > 0):
                dilated = cv2.dilate(pmask, kernel, iterations=2)
                mask[dilated > 0] = 0

            # Sauvegarde double extension pour compatibilité COLMAP (.png et sans double .png)
            tc = os.path.join(masks_dir, f"{fn}.png")
            tn = os.path.join(masks_dir, f"{stem}.png")
            cv2.imwrite(tc, mask)
            if tc != tn:
                cv2.imwrite(tn, mask)

            # Sauvegarde dans "images yolo" avec surimpression rouge translucide pour contrôle visuel
            if preview_dir:
                preview = img.copy()
                preview[mask == 0] = (0, 0, 255)
                overlay = cv2.addWeighted(img, 0.6, preview, 0.4, 0)
                cv2.imwrite(os.path.join(preview_dir, fn), overlay)

        current_total = already_done + i + len(paths)
        print(f"[{current_total}/{total_imgs}] Traitement terminé pour ce lot.")

    print("=== Masquage terminé avec succès ! ===")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Génération des masques YOLOv8x-seg")
    parser.add_argument("--images", type=str, default=r"C:\Users\Louaï\Desktop\Retoucher-le-réel\Traitement_Gaussian_SAGA\Donnees_Brutes\FabLAB 1210\images")
    parser.add_argument("--masks", type=str, default=r"C:\Users\Louaï\Desktop\Retoucher-le-réel\Traitement_Gaussian_SAGA\Donnees_Brutes\FabLAB 1210\masks")
    parser.add_argument("--preview", type=str, default=r"C:\Users\Louaï\Desktop\Retoucher-le-réel\Traitement_Gaussian_SAGA\Donnees_Brutes\FabLAB 1210\images yolo")
    parser.add_argument("--batch-size", type=int, default=8)
    parser.add_argument("--clean", action="store_true", help="Écrase complètement les anciens masques")
    
    args = parser.parse_args()
    process_masks(args.images, args.masks, args.preview, batch_size=args.batch_size, clean_output=args.clean)
