# ADR 0044 — Lanceur de jeu configurable et vérifiable

Date : 14 août 2026
Statut : accepté

## Contexte

Un jeu créé avec OpenRPG Forge doit pouvoir être livré comme un produit identifiable, sans demander
au joueur d'ouvrir le Creator ni de manipuler un `.orpmod`. Le lanceur doit exposer les réglages
usuels d'un RPG PC, préparer le multijoueur et rester compatible avec la règle de sécurité qui
interdit à un module communautaire de remplacer le runtime ou de charger du code natif.

## Décision

- La configuration du lanceur est un profil versionné v1 de la distribution du module. Elle porte
  l'identité visible, le nom de l'exécutable, la couleur d'accent, une image optionnelle, les liens,
  les réglages disponibles, le niveau multijoueur et le port local. Un ancien projet sans profil
  reçoit un profil déterministe désactivé.
- Le code du lanceur et le runtime proviennent exclusivement d'un modèle Windows x64 construit par
  OpenRPG Forge. Le module ne fournit que ses données et son branding PNG/JPEG gouverné.
- La couche de présentation Windows utilise WPF/XAML. Elle fournit composition plein écran,
  transitions, parallaxe et effets atmosphériques tout en conservant le backend .NET déjà validé.
  WinForms est écarté pour cette expérience visuelle ; Electron/WebView n'est pas retenu afin de ne
  pas ajouter un second runtime, et un lanceur Godot séparé n'est pas retenu afin de ne pas dupliquer
  le moteur du jeu dans la distribution. Aucune dépendance de production supplémentaire n'est ajoutée.
- Le décor générique embarqué est un actif original généré dont la provenance et le hash sont
  documentés. Le branding PNG/JPEG du jeu reste prioritaire. Parallaxe, brumes, braises et transitions
  sont désactivées lorsque le joueur active la réduction des mouvements.
- Une distribution de jeu est produite atomiquement à partir d'un paquet `Client` ou `Shipping`.
  Une destination existante n'est jamais écrasée. `game-launcher.json` décrit le produit et
  `game-distribution-manifest.json` enregistre taille et SHA-256 de chaque fichier redistribué.
  L'application vérifie le manifeste complet avant d'afficher le bouton Jouer.
- Les préférences du joueur restent hors du module, sous `%LOCALAPPDATA%/OpenRPG Forge/Games/<slug>`.
  Elles couvrent résolution, mode fenêtré/sans bordure/plein écran, qualité, VSync, volumes,
  réduction des mouvements, mode de session, joueur et port.
- Le mode solo et l'hébergement local autoritaire sont fonctionnels dans le runtime. La connexion
  directe distante peut être présentée comme aperçu désactivé, mais ne devient jouable qu'après
  branchement d'un transport inter-processus. Le lanceur ne doit jamais laisser entendre qu'une
  connexion Internet est disponible avant cette étape.
- Le Creator fournit un formulaire direct dans les profils de distribution et une action
  **Créer le lanceur du jeu**. Le même flux est automatisable avec `forge launcher build/verify` et
  les scripts de distribution.

## Conséquences

- Chaque futur jeu peut posséder son titre, son ambiance et son exécutable tout en partageant une
  base de lanceur maintenable et sécurisée.
- La présentation peut évoluer indépendamment des manifestes, réglages et règles de lancement. WPF
  reste une décision propre à la première cible Windows ; une future cible Linux/macOS devra choisir
  et qualifier sa propre couche de présentation.
- Le runtime reçoit uniquement des arguments issus d'un fichier de réglages validé ; le mode demandé
  doit correspondre au fichier persistant.
- L'hébergement local prouve déjà le chemin de session autoritaire, mais l'écoute réseau, la
  découverte LAN et la connexion distante restent un chantier distinct et explicitement visible.
- Windows x64 est la première cible. Les modèles Linux/macOS demanderont une décision et une preuve
  de distribution propres à chaque plateforme.
