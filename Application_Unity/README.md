# Module Unity — Application Réalité Virtuelle

Application interactive en Réalité Virtuelle développée sous **Unity 6000.6.2f1** avec le pipeline **Universal Render Pipeline (URP 17.6.0)**.
Matériel ciblé : **Meta Quest 2/3**  

---

## 1. Prérequis d'installation

Pour garantir la compatibilité absolue entre tous les membres de l'équipe :

- **Unity Hub** avec la version précise **Unity 6000.6.2f1** (cf. `ProjectSettings/ProjectVersion.txt`).

---

## 2. Ouverture du projet dans Unity Hub

1. Lancez **Unity Hub**.
2. Cliquez sur **Add** (Ajouter un projet depuis le disque).
3. Sélectionnez le sous-dossier **`Application_Unity`** (et non la racine du dépôt git).

---

## 3. Organisation interne du dossier `Assets/Projet/`

```text
Assets/Projet/
├── Art_Graphismes/       # Modèles 3D, Textures, Matériaux URP, Animations
├── Audio/                # Sons, Ambiances spatialisées et Musiques
├── Donnees_Config/       # Configurations typées (ScriptableObjects)
├── Interface_UI/         # Éléments d'interface utilisateur (Sprites, Menus)
├── Prefabs_Objets/       # Préfabriqués réutilisables (XR, Décors, Objets)
├── Scenes_Niveaux/       # Scènes de test et scène principale (Sandbox_VR_Basique.unity)
└── Scripts/              # Code source C# et Shaders
```

Consultez [Assets/Projet/ORGANISATION_DOSSIERS.md](Assets/Projet/ORGANISATION_DOSSIERS.md) pour les règles détaillées de nomenclature.
