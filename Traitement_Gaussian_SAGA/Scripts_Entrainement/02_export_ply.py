# Authored by Louai HAMLAT - All rights reserved
import os
import sys
from pathlib import Path
import yaml
import torch

# [L. Hamlat] Normalisation et export du nuage de gaussiennes 3D
def export_ply(workspace_dir, output_ply_path):
    print("Application du patch de sécurité torch.load (PyTorch 2.6+ compatibilité)...")
    _orig_load = torch.load
    torch.load = lambda *args, **kwargs: _orig_load(*args, **{**kwargs, 'weights_only': False})

    from nerfstudio.scripts.exporter import GaussianSplattingExporter
    from nerfstudio.utils.eval_utils import eval_setup

    workspace = Path(workspace_dir)
    outputs_dir = workspace / "outputs"

    # Chercher la dernière config.yml générée
    configs = list(outputs_dir.glob("**/config.yml"))
    if not configs:
        raise FileNotFoundError(f"Aucun fichier config.yml trouvé dans {outputs_dir}")

    # Prendre la plus récente
    latest_config = max(configs, key=lambda p: p.stat().st_mtime)
    print(f"Configuration trouvée : {latest_config}")

    output_ply = Path(output_ply_path)
    output_ply.parent.mkdir(parents=True, exist_ok=True)

    print("Initialisation de l'exportation du Gaussian Splatting...")
    config, pipeline, checkpoint_path, _ = eval_setup(latest_config)

    exporter = GaussianSplattingExporter(
        load_config=latest_config,
        output_dir=output_ply.parent
    )
    exporter.main()

    # Nerfstudio exporte généralement sous le nom 'splat.ply' dans output_dir
    exported_splat = output_ply.parent / "splat.ply"
    if exported_splat.exists():
        if exported_splat.resolve() != output_ply.resolve():
            if output_ply.exists():
                output_ply.unlink()
            exported_splat.rename(output_ply)
        print(f"Modèle .ply exporté avec succès vers : {output_ply}")
        size_mb = output_ply.stat().st_size / (1024 * 1024)
        print(f"Taille finale : {size_mb:.2f} Mo")
    else:
        # Si le fichier s'appelle déjà directement comme prévu ou autre nom .ply
        plys = list(output_ply.parent.glob("*.ply"))
        if plys:
            chosen_ply = max(plys, key=lambda p: p.stat().st_mtime)
            if chosen_ply.resolve() != output_ply.resolve():
                chosen_ply.rename(output_ply)
            size_mb = output_ply.stat().st_size / (1024 * 1024)
            print(f"Modèle .ply exporté : {output_ply} ({size_mb:.2f} Mo)")
        else:
            raise RuntimeError(f"Fichier .ply introuvable après export dans {output_ply.parent}")

if __name__ == "__main__":
    workspace = sys.argv[1] if len(sys.argv) > 1 else "/workspace/3dgs_workspace"
    out_ply = sys.argv[2] if len(sys.argv) > 2 else "/workspace/output_ply/scene_finale.ply"
    export_ply(workspace, out_ply)
