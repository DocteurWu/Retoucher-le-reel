#!/usr/bin/env bash
set -e

# ==============================================================================
# PIPELINE 3DGS HAUTE PRÉCISION (FABLAB 1210)
# OPTIMISÉ POUR VUES RECTILINÉAIRES VIRTUELLES (FFmpeg 100° FOV, 1600x1600)
# Intrinsèques connus exacts : PINHOLE fx=671.28, fy=671.28, cx=800.0, cy=800.0
# ==============================================================================

WORKSPACE="/workspace/3dgs_workspace"
IMAGES_DIR="${WORKSPACE}/images"
MASKS_DIR="${WORKSPACE}/masks"
YOLO_DIR="${WORKSPACE}/images yolo"
COLMAP_DIR="${WORKSPACE}/colmap"
OUTPUT_DIR="${WORKSPACE}/outputs"
FINAL_PLY_DIR="/workspace/output_ply"
FINAL_PLY="${FINAL_PLY_DIR}/scene_finale.ply"
SCRIPTS_DIR="/workspace/scripts"

mkdir -p "${WORKSPACE}" "${MASKS_DIR}" "${YOLO_DIR}" "${COLMAP_DIR}/sparse/0" "${OUTPUT_DIR}" "${FINAL_PLY_DIR}"

run_step1() {
    echo "=========================================================="
    echo "=== [ÉTAPE 1] VÉRIFICATION GPU & CONFIGURATION CUDA ==="
    echo "=========================================================="
    nvidia-smi

    export MAX_JOBS=4
    export MAKEFLAGS="-j4"
    export CUDA_HOME=/usr/local/cuda-12.1
    export PATH=/usr/local/cuda-12.1/bin:$PATH
    export LD_LIBRARY_PATH=/usr/local/cuda-12.1/lib64:$LD_LIBRARY_PATH
    export TORCH_CUDA_ARCH_LIST=$(python3 -c "import torch; cap=torch.cuda.get_device_capability(); print(f'{cap[0]}.{cap[1]}')" 2>/dev/null || echo "")
    echo "CUDA ARCH: ${TORCH_CUDA_ARCH_LIST}"
    echo "[OK] Étape 1 validée."
}

run_step2() {
    echo "=========================================================="
    echo "=== [ÉTAPE 2] MASQUAGE IA GPU (YOLOv8x-seg) ==="
    echo "=========================================================="
    python3 "${SCRIPTS_DIR}/01_generate_masks.py" "${IMAGES_DIR}" "${MASKS_DIR}" "${YOLO_DIR}" 16
    TOTAL_MASKS=$(ls -1 "${MASKS_DIR}"/*.png 2>/dev/null | wc -l || echo 0)
    echo "[OK] Étape 2 validée : ${TOTAL_MASKS} masques vérifiés."
}

run_step3() {
    echo "=========================================================="
    echo "=== [ÉTAPE 3] COLMAP PINHOLE HAUTE PRÉCISION ==="
    echo "=========================================================="
    
    # 1. Extraction SIFT avec modèle optique PINHOLE et focale exacte calculée (671.28 px)
    # HFOV=100° à 1600px -> 800 / tan(50°) = 671.28
    if [ ! -f "${COLMAP_DIR}/database.db" ]; then
        echo "Extraction des descripteurs SIFT avec calibration exacte PINHOLE..."
        QT_QPA_PLATFORM=offscreen colmap feature_extractor \
            --database_path "${COLMAP_DIR}/database.db" \
            --image_path "${IMAGES_DIR}" \
            --ImageReader.mask_path "${MASKS_DIR}" \
            --ImageReader.camera_model PINHOLE \
            --ImageReader.single_camera 1 \
            --ImageReader.camera_params "671.28,671.28,800.0,800.0" \
            --SiftExtraction.use_gpu 0 \
            --SiftExtraction.num_threads 12 \
            --SiftExtraction.max_num_features 8192
    else
        echo "Base COLMAP database.db déjà existante."
    fi

    # 2. Sequential matching
    echo "Matching séquentiel..."
    QT_QPA_PLATFORM=offscreen colmap sequential_matcher \
        --database_path "${COLMAP_DIR}/database.db" \
        --SequentialMatching.overlap 20 \
        --SequentialMatching.quadratic_overlap 1 \
        --SiftMatching.use_gpu 0 \
        --SiftMatching.num_threads 12 \
        --SiftMatching.max_num_matches 32768

    # 3. Mapper avec fixation des intrinsèques pour éviter toute dérive géométrique
    if [ ! -f "${COLMAP_DIR}/sparse/0/cameras.bin" ] && [ ! -f "${COLMAP_DIR}/sparse/0/cameras.txt" ]; then
        echo "Calcul de la reconstruction sparse (Mapper avec intrinsèques verrouillés)..."
        QT_QPA_PLATFORM=offscreen colmap mapper \
            --database_path "${COLMAP_DIR}/database.db" \
            --image_path "${IMAGES_DIR}" \
            --output_path "${COLMAP_DIR}/sparse" \
            --Mapper.ba_refine_focal_length 0 \
            --Mapper.ba_refine_principal_point 0 \
            --Mapper.ba_refine_extra_params 0 \
            --Mapper.num_threads 12
    else
        echo "Modèle sparse/0 déjà reconstruit !"
    fi

    if [ ! -d "${COLMAP_DIR}/sparse/0" ]; then
        echo "ERREUR : Le modèle sparse/0 n'a pas pu être généré !"
        exit 1
    fi
    echo "[OK] Étape 3 validée avec calibration PINHOLE parfaite."
}

run_step4() {
    echo "=========================================================="
    echo "=== [ÉTAPE 4] ENTRAÎNEMENT 3DGS HAUTE DÉFINITION (splatfacto) ==="
    echo "=========================================================="
    START_TIME=$(date +%s)

    export CUDA_HOME=/usr/local/cuda-12.1
    export PATH=/usr/local/cuda-12.1/bin:$PATH
    export LD_LIBRARY_PATH=/usr/local/cuda-12.1/lib64:$LD_LIBRARY_PATH

    # Hyperparamètres HD :
    # - sh-degree 1 : optimisé Quest 3 (VR)
    # - downscale-factor 1 : résolution 1600x1600 intégrale sans compression
    # - densify-grad-thresh 0.0002 : densification fine pour faire disparaître tout flou
    # - max-num-iterations 30000 : convergence complète vers 1.5M - 2.5M de Gaussiennes
    yes y 2>/dev/null | ns-train splatfacto \
        --pipeline.model.sh-degree 1 \
        --pipeline.model.densify-grad-thresh 0.0002 \
        --pipeline.model.warmup-length 1000 \
        --pipeline.model.refine-every 100 \
        --max-num-iterations 30000 \
        --viewer.quit-on-train-completion True \
        --logging.local-writer.enable False \
        --output-dir "${OUTPUT_DIR}" \
        colmap \
        --data "${WORKSPACE}" \
        --colmap-path colmap/sparse/0 \
        --images-path images \
        --masks-path masks \
        --downscale-factor 1

    END_TIME=$(date +%s)
    TRAIN_DURATION=$((END_TIME - START_TIME))
    echo "[OK] Étape 4 validée en ${TRAIN_DURATION} secondes."
}

run_step5() {
    echo "=========================================================="
    echo "=== [ÉTAPE 5] EXPORTATION DU MODÈLE .PLY HAUTE DÉFINITION ==="
    echo "=========================================================="
    
    # Trouver le dernier dossier d'entraînement
    LATEST_CONFIG=$(find "${OUTPUT_DIR}" -name "config.yml" | sort | tail -n 1)
    if [ -z "${LATEST_CONFIG}" ]; then
        echo "ERREUR: config.yml introuvable dans ${OUTPUT_DIR}"
        exit 1
    fi
    echo "Exportation à partir de : ${LATEST_CONFIG}"

    ns-export gaussian-splat --load-config "${LATEST_CONFIG}" --output-dir "${FINAL_PLY_DIR}"

    if [ -f "${FINAL_PLY_DIR}/splat.ply" ]; then
        mv -f "${FINAL_PLY_DIR}/splat.ply" "${FINAL_PLY}"
    fi

    if [ ! -f "${FINAL_PLY}" ]; then
        echo "ERREUR : Fichier ${FINAL_PLY} introuvable après export !"
        exit 1
    fi

    PLY_SIZE_BYTES=$(stat -c%s "${FINAL_PLY}")
    PLY_SIZE_MB=$(echo "scale=2; ${PLY_SIZE_BYTES} / (1024 * 1024)" | bc 2>/dev/null || awk "BEGIN {print ${PLY_SIZE_BYTES}/(1024*1024)}")

    echo "=========================================================="
    echo "=== RECONSTRUCTION 3DGS TERMINÉE AVEC SUCCÈS ==="
    echo "- Fichier PLY final HD : ${FINAL_PLY}"
    echo "- Taille du fichier    : ${PLY_SIZE_MB} Mo"
    echo "=========================================================="
}

TARGET_STEP="${1:-all}"

case "$TARGET_STEP" in
    step1) run_step1 ;;
    step2) run_step2 ;;
    step3) run_step3 ;;
    step4) run_step4 ;;
    step5) run_step5 ;;
    all)
        run_step1
        run_step2
        run_step3
        run_step4
        run_step5
        ;;
    *)
        echo "Usage: $0 {step1|step2|step3|step4|step5|all}"
        exit 1
        ;;
esac
