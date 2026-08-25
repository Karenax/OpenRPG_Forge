# ADR 0044 — Lanceur de jeu configurable et vérifiable

Date : 14 août 2026
Statut : accepté, complété par l'ADR-0048 le 25 août 2026

## Contexte

Un jeu créé avec OpenRPG Forge doit pouvoir être livré comme un produit identifiable, sans demander
au joueur d'ouvrir le Creator ni de manipuler un `.orpmod`. Le lanceur doit exposer les réglages
usuels d'un RPG PC, préparer le multijoueur et rester compatible avec la règle de sécurité qui
interdit à un module communautaire de remplacer le runtime ou de charger du code natif.

## Décision

- La configuration du lanceur est un profil versionné v1 de la distribution du module. Elle porte
  l'identité visible, le nom de l'exécutable, la couleur d'accent, une image optionnelle, une musique
  WAV optionnelle, les liens, les réglages disponibles, le niveau multijoueur et le port TCP. Un
  ancien projet sans profil reçoit un profil déterministe désactivé.
- Le code du lanceur et le runtime proviennent exclusivement d'un modèle Windows x64 construit par
  OpenRPG Forge. Le module ne fournit que ses données et ses médias gouvernés PNG/JPEG/WAV.
- La couche de présentation Windows utilise WPF/XAML. Elle fournit composition plein écran,
  transitions, parallaxe et effets atmosphériques tout en conservant le backend .NET déjà validé.
  WinForms est écarté pour cette expérience visuelle ; Electron/WebView n'est pas retenu afin de ne
  pas ajouter un second runtime, et un lanceur Godot séparé n'est pas retenu afin de ne pas dupliquer
  le moteur du jeu dans la distribution. Aucune dépendance de production supplémentaire n'est ajoutée.
- Le décor générique embarqué est un actif original généré dont la provenance et le hash sont
  documentés. Le branding PNG/JPEG du jeu reste prioritaire. Parallaxe, brumes, particules et
  transitions sont désactivées lorsque le joueur active la réduction des mouvements.
- Lorsqu'une musique WAV est fournie, le lanceur la lit en boucle au chargement, expose directement
  **Play/Stop** et un curseur de volume, et synchronise ce curseur avec le réglage Musique persistant.
  Le mode de capture visuelle ne produit aucun son. WPF `MediaPlayer` reste l'unique backend audio du
  lanceur et n'ajoute aucune dépendance de production.
- Une distribution de jeu est produite atomiquement à partir d'un paquet `Client` ou `Shipping`.
  Une destination existante n'est jamais écrasée. `game-launcher.json` décrit le produit et
  `game-distribution-manifest.json` enregistre taille et SHA-256 de chaque fichier redistribué.
  L'application vérifie le manifeste complet avant d'afficher le bouton Jouer.
- Les préférences du joueur restent hors du module, sous `%LOCALAPPDATA%/OpenRPG Forge/Games/<slug>`.
  Elles couvrent résolution, mode fenêtré/sans bordure/plein écran, qualité, VSync, volumes,
  réduction des mouvements, mode de session, joueur et port.
- La décision initiale conditionne la connexion distante à un transport inter-processus réel. Cette
  condition est satisfaite par l'ADR-0048 : le profil Internet direct peut désormais exposer
  **Héberger** et **Rejoindre**. Un profil local continue d'écouter uniquement sur loopback, et le
  lanceur ne doit pas présenter la réplication de présence/déplacement comme une campagne RPG
  coopérative entièrement synchronisée.
- Le Creator fournit un formulaire direct dans les profils de distribution et une action
  **Créer le lanceur du jeu**. Le même flux est automatisable avec `forge launcher build/verify` et
  les scripts de distribution.

## Conséquences

- Chaque futur jeu peut posséder son titre, son ambiance et son exécutable tout en partageant une
  base de lanceur maintenable et sécurisée.
- Les médias propres au jeu restent dans son workspace auteur ; l'export les extrait du `.orpmod`,
  les renomme sous `branding/` et les couvre par le manifeste de distribution.
- La présentation peut évoluer indépendamment des manifestes, réglages et règles de lancement. WPF
  reste une décision propre à la première cible Windows ; une future cible Linux/macOS devra choisir
  et qualifier sa propre couche de présentation.
- Le runtime reçoit uniquement des arguments issus d'un fichier de réglages validé ; le mode demandé
  doit correspondre au fichier persistant.
- Le transport TCP direct écoute réellement depuis le runtime et le serveur headless. La découverte
  LAN, le relais/NAT automatique, la reconnexion, la sécurité de l'hôte et le raccord de tout le
  gameplay restent des chantiers distincts et explicitement visibles.
- Windows x64 est la première cible. Les modèles Linux/macOS demanderont une décision et une preuve
  de distribution propres à chaque plateforme.

## État courant

Le guide `docs/MULTIPLAYER_INTERNET.md` est l'autorité opérationnelle pour les parcours hôte/rejoint,
le pare-feu et le routeur, le serveur headless, les diagnostics, les preuves et les limites. L'ADR-0048
reste l'autorité sur le protocole TCP v1.
