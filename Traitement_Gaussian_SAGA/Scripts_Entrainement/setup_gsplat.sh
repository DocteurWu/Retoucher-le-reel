#!/bin/bash
set -e
export CUDA_HOME=/usr/local/cuda-12.1
export PATH=/usr/local/cuda-12.1/bin:/usr/local/bin:/usr/bin:$PATH
export LD_LIBRARY_PATH=/usr/local/cuda-12.1/lib64:$LD_LIBRARY_PATH

echo "Installation de ninja et gsplat précompilé..."
pip install ninja
pip install --force-reinstall --no-deps gsplat --extra-index-url https://docs.gsplat.studio/whl/pt22cu121

python3 -c "import gsplat; print('GSPLAT SUCCESS:', gsplat.__file__)"
