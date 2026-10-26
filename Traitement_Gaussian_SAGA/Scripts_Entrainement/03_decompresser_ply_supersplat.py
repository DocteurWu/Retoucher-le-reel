# =====================================================================================
#  03_decompresser_ply_supersplat.py — PLY COMPRESSÉ SUPERSPLAT → PLY 3DGS STANDARD
#
#  POURQUOI
#    SuperSplat (et PlayCanvas) exporte par défaut un « .compressed.ply » : les splats
#    y sont rangés par blocs de 256 (élément « chunk » = bornes min/max du bloc) et
#    chaque splat tient sur 4 entiers 32 bits (position, rotation, échelle, couleur
#    quantifiées). Le plugin Unity d'aras-p ne lit que le PLY 3DGS « classique »
#    (un seul élément vertex, en float) : à l'import, il lit les 18 floats du chunk
#    comme si c'étaient ceux d'un splat et échoue avec
#        « PLY ... read error, expected N data bytes got M ».
#
#  CE QUE FAIT LE SCRIPT
#    Il dé-quantifie chaque splat et écrit un PLY standard avec les attributs requis
#    par le plugin : x y z, nx ny nz (à 0), f_dc_0..2, opacity, scale_0..2, rot_0..3.
#    Le format compressé ne contient pas d'harmoniques sphériques d'ordre supérieur
#    (f_rest_*) : la couleur ne change donc pas avec l'angle de vue, comme dans SuperSplat.
#
#  UTILISATION (Windows ou Linux, Python 3 + numpy)
#      pip install numpy                       (une seule fois)
#      python 03_decompresser_ply_supersplat.py  entree.compressed.ply  [sortie.ply]
#      python 03_decompresser_ply_supersplat.py  a.ply b.ply c.ply      (plusieurs fichiers)
#      python 03_decompresser_ply_supersplat.py  C:\dossier\de\plys     (tout un dossier)
#    Écrit « entree.decompresse.ply » à côté de chaque fichier. Les PLY déjà au
#    format standard sont signalés et ignorés. Ensuite, dans Unity :
#    Tools > Gaussian Splats > Create GaussianSplatAsset, et choisir ce fichier.
#
#  Alternative sans script : dans SuperSplat, File > Export > « .ply » (non compressé).
# =====================================================================================
import os
import sys

import numpy as np

SH_C0 = 0.28209479177387814  # coefficient de l'harmonique sphérique d'ordre 0
TAILLE_BLOC = 256           # splats par chunk


def lire_entete(f):
    """Renvoie la liste des éléments [(nom, nombre, [(type, propriété)…])] et la taille de l'en-tête."""
    elements = []
    if f.readline().strip() != b"ply":
        raise ValueError("ce n'est pas un fichier PLY")
    while True:
        ligne = f.readline()
        if not ligne:
            raise ValueError("en-tête PLY incomplet")
        mots = ligne.strip().split()
        if not mots:
            continue
        if mots[0] == b"format" and mots[1] != b"binary_little_endian":
            raise ValueError("format non géré : " + mots[1].decode())
        elif mots[0] == b"element":
            elements.append((mots[1].decode(), int(mots[2]), []))
        elif mots[0] == b"property":
            elements[-1][2].append((mots[1].decode(), mots[2].decode()))
        elif mots[0] == b"end_header":
            return elements, f.tell()


def type_numpy(nom):
    return {"float": "<f4", "uint": "<u4", "uchar": "u1", "int": "<i4", "double": "<f8"}[nom]


def depaqueter_11_10_11(v):
    """Trois composantes 0..1 codées sur 11, 10 et 11 bits."""
    return np.stack([((v >> 21) & 0x7FF) / 2047.0,
                     ((v >> 11) & 0x3FF) / 1023.0,
                     (v & 0x7FF) / 2047.0], axis=1)


def depaqueter_8_8_8_8(v):
    return np.stack([((v >> 24) & 0xFF) / 255.0,
                     ((v >> 16) & 0xFF) / 255.0,
                     ((v >> 8) & 0xFF) / 255.0,
                     (v & 0xFF) / 255.0], axis=1)


def depaqueter_rotation(v):
    """Quaternion « plus grande composante omise » : 2 bits d'indice + 3 × 10 bits."""
    norme = 1.0 / (np.sqrt(2.0) * 0.5)
    a = (((v >> 20) & 0x3FF) / 1023.0 - 0.5) * norme
    b = (((v >> 10) & 0x3FF) / 1023.0 - 0.5) * norme
    c = ((v & 0x3FF) / 1023.0 - 0.5) * norme
    m = np.sqrt(np.clip(1.0 - (a * a + b * b + c * c), 0.0, None))
    indice = (v >> 30).astype(np.int64)
    q = np.empty((len(v), 4), dtype=np.float64)
    q[indice == 0] = np.stack([m, a, b, c], axis=1)[indice == 0]
    q[indice == 1] = np.stack([a, m, b, c], axis=1)[indice == 1]
    q[indice == 2] = np.stack([a, b, m, c], axis=1)[indice == 2]
    q[indice == 3] = np.stack([a, b, c, m], axis=1)[indice == 3]
    return q  # ordre rot_0..rot_3 du PLY (w, x, y, z)


def decompresser(chemin_entree, chemin_sortie):
    with open(chemin_entree, "rb") as f:
        elements, debut = lire_entete(f)
        noms = [e[0] for e in elements]
        if "chunk" not in noms or "vertex" not in noms:
            raise ValueError("pas de « chunk » + « vertex » : ce n'est pas un PLY compressé SuperSplat "
                             "(peut-être déjà au format standard ?)")
        donnees = {}
        f.seek(debut)
        for nom, nombre, proprietes in elements:
            dt = np.dtype([(p, type_numpy(t)) for t, p in proprietes])
            donnees[nom] = np.frombuffer(f.read(dt.itemsize * nombre), dtype=dt, count=nombre)

    ch = donnees["chunk"]
    vx = donnees["vertex"]
    n = len(vx)
    bloc = np.arange(n) // TAILLE_BLOC
    if bloc[-1] >= len(ch):
        raise ValueError("pas assez de chunks pour le nombre de splats")

    def bornes(prefixe_min, prefixe_max, axes):
        lo = np.stack([ch[prefixe_min + a] for a in axes], axis=1)[bloc]
        hi = np.stack([ch[prefixe_max + a] for a in axes], axis=1)[bloc]
        return lo, hi

    lo, hi = bornes("min_", "max_", ("x", "y", "z"))
    position = lo + (hi - lo) * depaqueter_11_10_11(vx["packed_position"])

    lo, hi = bornes("min_scale_", "max_scale_", ("x", "y", "z"))
    echelle = lo + (hi - lo) * depaqueter_11_10_11(vx["packed_scale"])  # déjà en log, comme le PLY standard

    rgba = depaqueter_8_8_8_8(vx["packed_color"])
    couleur = rgba[:, :3]
    if "min_r" in ch.dtype.names:  # SuperSplat ≥ 1.x : couleur aussi bornée par chunk
        lo, hi = bornes("min_", "max_", ("r", "g", "b"))
        couleur = lo + (hi - lo) * couleur
    f_dc = (couleur - 0.5) / SH_C0
    alpha = np.clip(rgba[:, 3], 1e-6, 1.0 - 1e-6)
    opacite = -np.log(1.0 / alpha - 1.0)  # inverse de la sigmoïde

    rotation = depaqueter_rotation(vx["packed_rotation"])

    proprietes = ["x", "y", "z", "nx", "ny", "nz", "f_dc_0", "f_dc_1", "f_dc_2", "opacity",
                  "scale_0", "scale_1", "scale_2", "rot_0", "rot_1", "rot_2", "rot_3"]
    sortie = np.zeros((n, len(proprietes)), dtype="<f4")
    sortie[:, 0:3] = position
    sortie[:, 6:9] = f_dc
    sortie[:, 9] = opacite
    sortie[:, 10:13] = echelle
    sortie[:, 13:17] = rotation

    entete = ["ply", "format binary_little_endian 1.0",
              "comment Decompresse depuis " + os.path.basename(chemin_entree),
              "element vertex %d" % n]
    entete += ["property float " + p for p in proprietes]
    entete.append("end_header")
    with open(chemin_sortie, "wb") as f:
        f.write(("\n".join(entete) + "\n").encode("utf-8"))
        f.write(sortie.tobytes())
    return n


def sortie_par_defaut(entree):
    base = entree[:-4] if entree.lower().endswith(".ply") else entree
    return base + ".decompresse.ply"


def lister_entrees(arguments):
    """Fichiers .ply à traiter : ceux donnés, plus ceux des dossiers donnés (sans les déjà décompressés)."""
    fichiers = []
    for chemin in arguments:
        if os.path.isdir(chemin):
            for nom in sorted(os.listdir(chemin)):
                if nom.lower().endswith(".ply") and not nom.lower().endswith(".decompresse.ply"):
                    fichiers.append(os.path.join(chemin, nom))
        else:
            fichiers.append(chemin)
    return fichiers


def main():
    arguments = sys.argv[1:]
    if not arguments:
        print("usage : python 03_decompresser_ply_supersplat.py entree.ply [sortie.ply]\n"
              "        python 03_decompresser_ply_supersplat.py a.ply b.ply ... | dossier")
        sys.exit(1)

    # Forme historique « entree sortie » : deux arguments dont le 2e n'existe pas encore.
    if len(arguments) == 2 and os.path.isfile(arguments[0]) and not os.path.exists(arguments[1]):
        travaux = [(arguments[0], arguments[1])]
    else:
        travaux = [(e, sortie_par_defaut(e)) for e in lister_entrees(arguments)]

    echecs = 0
    for entree, sortie in travaux:
        try:
            n = decompresser(entree, sortie)
            print("OK      %s -> %s (%d splats, %.1f Mo)" % (entree, sortie, n, os.path.getsize(sortie) / 1e6))
        except Exception as erreur:  # un fichier raté ne doit pas arrêter le lot
            echecs += 1
            print("IGNORÉ  %s : %s" % (entree, erreur))
    if echecs:
        sys.exit(2)


if __name__ == "__main__":
    main()
