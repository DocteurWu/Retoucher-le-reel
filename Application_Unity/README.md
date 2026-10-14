# Module Unity — Application Réalité Virtuelle

Application interactive en Réalité Virtuelle développée sous **Unity 6000.0.84f1** avec le pipeline **Universal Render Pipeline (URP 17.6.0)**.
Matériel ciblé : **Meta Quest 2/3**  

---

## 1. Prérequis d'installation

Pour garantir la compatibilité absolue entre tous les membres de l'équipe :

- **Unity Hub** avec la version précise **Unity 6000.0.84f1** (cf. `ProjectSettings/ProjectVersion.txt`).

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
├── Scenes_Niveaux/       # Scènes de test et scène principale
└── Scripts/              # Code source C# et Shaders
```

Consultez [Assets/Projet/ORGANISATION_DOSSIERS.md](Assets/Projet/ORGANISATION_DOSSIERS.md) pour les règles détaillées de nomenclature.

---

## 4. Statut du développement

> 🚧 **En cours de développement** :
> L'architecture de base du projet est initialisée. Nous travaillons actuellement sur l'intégration des briques fondamentales d'interaction VR :
> - **Déplacement & Locomotion** : Système de téléportation parabolique et déplacement continu adapté au confort Quest.
> - **Préhension (Grab)** : Prise en main directe et à distance d'objets interactifs (`XRGrabInteractable` / scripts de manipulation).
> - **Orientation** : Rotation par crans (*Snap Turn*) pour réduire le mal des transports.
> - **Calibration XR** : Prise en charge du tracking de la tête et des manettes Meta Quest via OpenXR / XRI.

