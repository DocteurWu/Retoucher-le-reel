#!/usr/bin/env bash
# Lance les tests EditMode purs sans Unity (Mono de l'éditeur + réflexion).
# Usage (depuis Application_Unity, Git Bash) :  Outils/TestsHorsUnity/lancer.sh <ed.dll> <ac.dll-dossier> [Classe ...]
#   <ed.dll>  : Assembly-CSharp-Editor compilée avec csc (voir CLAUDE.md, « Vérifier la compilation sans Unity »)
#   <dossier> : dossier contenant l'Assembly-CSharp compilée (ac.dll)
#   Classe    : suffixe du nom de classe de test (défaut : les classes pures connues)
set -e
UNITY="C:/Program Files/Unity/Hub/Editor/6000.0.84f1/Editor/Data"
ICI="$(cd "$(dirname "$0")" && pwd -W)"
PROJET="$(pwd -W)"
ED="$1"; AC_DIR="$2"; shift 2
CLASSES=("${@:-EpreuveMemoireTests VRScaleGrabTests VRHandGesturesTests NarrationVoixTests}")
NUNIT="$(ls -d "$PROJET"/Library/PackageCache/com.unity.ext.nunit@*/net40/unity-custom | head -1)"
MSYS_NO_PATHCONV=1 "$UNITY/NetCoreRuntime/dotnet.exe" "$UNITY/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -target:exe \
  -out:"$ICI/Harness.exe" -r:"$UNITY/MonoBleedingEdge/lib/mono/4.7.1-api/mscorlib.dll" \
  -r:"$UNITY/MonoBleedingEdge/lib/mono/4.7.1-api/System.dll" -r:"$UNITY/MonoBleedingEdge/lib/mono/4.7.1-api/System.Core.dll" "$ICI/Harness.cs"
for c in ${CLASSES[@]}; do
  echo "== $c"
  MSYS_NO_PATHCONV=1 "$UNITY/MonoBleedingEdge/bin/mono.exe" "$ICI/Harness.exe" "$ED" "$c" "$AC_DIR" "$UNITY/Managed/UnityEngine" \
    "$UNITY/Managed" "$NUNIT" "$PROJET/Library/ScriptAssemblies" 2>&1 | grep -E "^(OK|FAIL)|reussis"
done
