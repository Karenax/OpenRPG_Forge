# Migration vers OpenRPG Forge Studio autonome

Ce document spécialisé détaille l’exécution de l’ADR 0045. Il ne remplace ni le contexte canonique,
ni le tracker Aurora, ni les contrats métier existants.

## Contrat d’expérience

- Une seule fenêtre produit au démarrage.
- La retouche d’un module existant est l’action principale du Module Hub.
- Le dernier module, la dernière zone, la page, la caméra et la sélection sont restaurés.
- Une seule rubrique est ouverte. Une seule action spatiale est armée. `Échap` annule l'outil actif.
- Le centre occupe au minimum 55 % de la largeur utile sur une page spatiale.
- L'inspecteur montre soit la sélection, soit l'outil actif, jamais deux formulaires concurrents.
- Le clic gauche sélectionne dans la 3D ; le gizmo X/Y/Z déplace directement l'objet et le clic droit
  ouvre les catégories de réglages de cette même instance, sans créer de fenêtre secondaire.
- Le même menu contextuel permet de dupliquer à côté, d'ajouter à la position visée et de supprimer
  après confirmation ; ces trois mutations partagent dry-run, undo/redo et sauvegarde explicite.
- UUID, hashes, schémas, chemins et logs restent repliés par défaut.
- Une suppression montre ses dépendances et reste annulable ; un formulaire rapide préserve les
  données qu’il ne montre pas.
- Une erreur ouvre la bonne rubrique, le bon document, l’objet et la propriété concernés.
- L’assistant utilise le même bus de commandes que l’édition manuelle et reste facultatif.

## Gabarits de page

### Page spatiale

- barre globale : 48 px ;
- rail de rubriques : 72 px ;
- liste/palette : 188 px par défaut, repliable aux faibles largeurs ;
- vue 3D centrale : largeur restante, jamais sous 55 % à 1920×1080 ;
- inspecteur contextuel : 280 px en résumé, 360 px pour l'édition complète ;
- statut : 24 px ; historique et diagnostics dans un tiroir fermé ;
- typographie courante : 12 px natifs à 100 %, sans mise à l’échelle d'une toile de référence.

La qualification responsive couvre uniquement 1920×1080, 2560×1440 et 3840×2160. Le Full HD est
le minimum supporté, le QHD la référence de conception et la 4K la borne haute. Sous 1920×1080, le
Studio peut s’ouvrir mais aucune garantie de disposition n’est exigée. En 4K, les panneaux conservent
des largeurs maximales et l’espace supplémentaire revient à la 3D ou au document central.

### Page spécialisée

- liste de ressources à gauche ;
- éditeur direct au centre ;
- contexte, dépendances et validation à droite ;
- aucune fausse vue 3D conservée pour remplir l’espace.

### Page hybride

La surface centrale reste 3D mais change de contexte : plateau tournant d’un blueprint, cadrage de
caméra, comparaison d’asset ou simulation visuelle. La zone active reste disponible sans devenir
une seconde fenêtre.

## Rubriques et pages

| Rubrique | Pages |
|---|---|
| Monde | Zones & scène ; Terrain & biomes ; Objets & interactions ; Environnement & audio ; Navigation & transitions ; Monde vivant |
| Contenu | Bibliothèque & blueprints ; Studio créature ; Studio objet ; Assets & import ; Factions, marchands & compagnons |
| Histoire | Dialogues ; Quêtes & journal ; Cinématiques & caméras ; Lore & références ; Localisation |
| Gameplay | Règles & difficulté ; Capacités & progression ; ForgeScript ; Runtime, contrôles & extensions ; Réseau & Live Director |
| Tester | Playtest ; Validation & corrections ; Laboratoire qualité ; Historique & récupération |
| Publier | Build & impact ; Packages & dépendances ; Profils & export ; Lanceur du jeu |
| Projet | Identité & configuration ; Espace de travail & raccourcis ; Sécurité & workers ; Production & plateforme |
| Assistant | Assistant contextuel ; Plans & approbation ; Création 3D assistée |

## Services de présentation

- `StudioSessionController` : projet, révision, zone active et cycle de vie.
- `StudioNavigationRouter` : rubrique, page, document et historique de navigation.
- `StudioSelectionService` : sélection simple/multiple, objet actif, verrouillage et contexte propre à
  chaque zone, partagés entre 3D, listes et diagnostics.
- `StudioCameraService` : commandes de navigation, six vues, bookmarks, profil et historique séparé.
- `StudioCommandCoordinator` : dry-run, diff, application atomique sur une sélection, undo/redo et
  sauvegarde explicite.
- `StudioDocumentService` : documents ouverts et état local par page.
- `StudioValidationNavigator` : diagnostic vers ressource/propriété.
- `StudioBackgroundJobService` : import, build, validation et annulation sans bloquer l’interface.
- `StudioPlaytestBridge` : snapshot, processus runtime, journal et retour au contexte exact.
- `StudioPreferenceStore` : préférences utilisateur hors du module et reprise après crash.

## Ordre de migration

1. ADR, contrat UX, catalogue des pages et matrice des 66 fenêtres.
2. Coque autonome, Module Hub, ouverture sûre et navigation sans mutation.
3. Tranche Monde : 3D, zone active, sélection, placement, transformation, terrain, navigation,
   sauvegarde, undo/redo et Tester ici.
4. Contenu : blueprints, créatures, objets, assets et contenu social.
5. Histoire : dialogue ligne par ligne, quêtes, cinématiques, lore et localisation.
6. Gameplay : règles, capacités, ForgeScript, runtime, réseau et Live Director.
7. Tester : playtest isolé, diagnostics navigables, qualité et récupération.
8. Publier et Projet : builds, packages, export, lanceur, configuration, sécurité et production.
9. Assistant : tiroir contextuel, plans approuvables et création 3D gouvernée.
10. Distributions Windows/Linux, bascule du raccourci Creator, parité puis retrait du plugin.

Chaque étape doit compiler, passer ses tests ciblés, fournir un smoke Godot et être relue sur des
captures 1920×1080, 2560×1440 et 3840×2160 aux échelles système applicables entre 100 % et 200 %.
Le build seul ne qualifie jamais l’ergonomie.
