#!/bin/bash
set -e
export CUDA_HOME=/usr/local/cuda-12.1
export PATH=/usr/local/cuda-12.1/bin:/usr/local/bin:/usr/bin:$PATH
export LD_LIBRARY_PATH=/usr/local/cuda-12.1/lib64:$LD_LIBRARY_PATH

echo "=== Lancement entraînement Splatfacto 30 000 itérations (sh-degree=1) ==="
ns-train splatfacto \
    --pipeline.model.sh-degree 1 \
    --pipeline.datamanager.cache-images-type uint8 \
    --pipeline.model.densify-grad-thresh 0.0002 \
    --pipeline.model.warmup-length 1000 \
    --pipeline.model.refine-every 100 \
    --max-num-iterations 30000 \
    --viewer.quit-on-train-completion True \
    --logging.local-writer.enable False \
    --output-dir /workspace/3dgs_workspace/outputs \
    colmap \
    --data /workspace/3dgs_workspace \
    --colmap-path colmap/sparse/0 \
    --images-path images \
    --masks-path masks \
    --downscale-factor 1

echo "=== Entraînement terminé, début de l'exportation PLY ==="
LATEST_CONFIG=$(find /workspace/3dgs_workspace/outputs -name "config.yml" | sort | tail -n 1)
echo "Config trouvée : ${LATEST_CONFIG}"

ns-export gaussian-splat --load-config "${LATEST_CONFIG}" --output-dir /workspace/output_ply

if [ -f /workspace/output_ply/splat.ply ]; then
    mv -f /workspace/output_ply/splat.ply /workspace/output_ply/scene_finale.ply
fi

echo "=== PLY EXPORTÉ AVEC SUCCÈS ==="
ls -lh /workspace/output_ply/scene_finale.ply
