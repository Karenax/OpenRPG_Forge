# Acceptation — OpenRPG Forge Studio autonome

## Coque et ouverture

- `STUDIO-001` : lancer le Studio produit exactement une fenêtre OpenRPG Forge, sans chrome de
  l’éditeur Godot ni outil secondaire visible.
- `STUDIO-002` : le Module Hub permet de continuer Forgotten Forest et d’ouvrir un workspace Forge
  par un sélecteur de dossier ; une erreur reste lisible et ne ferme pas l’application.
- `STUDIO-003` : un module valide affiche son titre, ses zones, sa zone d’entrée et son état de
  sauvegarde sans exposer de JSON.
- `STUDIO-004` : les huit rubriques et trente-six pages ont des identifiants uniques et restent
  accessibles au clavier.
- `STUDIO-005` : l’Explorateur hiérarchique reste compact, change de regroupement et partage sa
  sélection avec la vue 3D, la recherche et l’inspecteur.
- `STUDIO-006` : `Ctrl+K` trouve en français pages, contenus, ressources, objets et actions, puis
  ouvre le résultat au clavier sans nouvelle fenêtre native.
- `STUDIO-007` : les filtres d’affichage combinables ont un aperçu réel, restent annulables et ne
  sont persistés atomiquement que sur action explicite ; une vue utile peut être nommée.
- `STUDIO-008` : Trouver les références ouvre les utilisations canoniques depuis le contexte ou
  l’inspecteur, avec retour vers la zone ou la ressource concernée.
- `STUDIO-009` : l’aide contextuelle indique la tâche et l’action suivante, et ne présente que des
  réponses rattachées à des sources locales identifiées.
- `STUDIO-024` : une disposition de panneaux est prévisualisée, appliquée en mémoire, annulable et
  persistée atomiquement uniquement sur une sauvegarde explicite ; un profil utile peut être nommé.
- `STUDIO-025` : plusieurs onglets de zone conservent chacun sélection et caméra sans dupliquer
  l’état éditable, et l’activation d’un onglet ne réordonne pas les autres documents.
- `STUDIO-026` : une seconde zone peut être comparée dans une vraie vue 3D clairement marquée
  lecture seule, sans gizmo ni commande de mutation.
- `STUDIO-027` : les raccourcis sont recherchables et configurables avec presets, détection de
  conflit, dry-run, undo/redo et sauvegarde atomique explicite.
- `STUDIO-028` : noms humains et tâches dominent l’interface ; UUID, hash et chemins internes ne
  sont visibles que dans une divulgation technique demandée explicitement.
- `STUDIO-029` : les modes Édition, Simulation, Éclairage, Navigation, Collisions et Performance
  changent réellement la vue 3D ; leur configuration reste prévisualisable, annulable et enregistrée
  uniquement sur action explicite.

## Monde et continuité

- `STUDIO-010` : Forgotten Forest apparaît dans une vue 3D centrale et cadrée ; une indisponibilité
  d’asset produit un placeholder explicite, jamais un import réseau implicite.
- `STUDIO-011` : changer Monde → Dialogues → Monde conserve la zone, la pose de caméra et la
  sélection propre à chaque zone ; orbite, déplacement, zoom, focus, vues, bookmarks et pilotage
  passent par les commandes Application.
- `STUDIO-012` : cliquer un objet le sélectionne dans la 3D et met à jour l’inspecteur unique ;
  `Ctrl`/`Maj` construit une multisélection cadrable, verrouillable et éditable par propriétés communes.
- `STUDIO-013` : placement, transformation et terrain utilisent les commandes Application, montrent
  le dry-run et restent annulables avant toute sauvegarde ; aucune page encore planifiée n’expose une
  mutation de contournement.
- `STUDIO-014` : Tester ici lance un snapshot temporaire ; le retour restitue le contexte du Studio.
- `STUDIO-015` : cliquer un objet affiche les gizmos translation, rotation et échelle directement
  dans la vue 3D ; repère, pivot, snap, saisie numérique et alignement produisent une prévisualisation,
  `Maj` réduit le pas et `Échap` annule le geste en cours.
- `STUDIO-016` : le clic droit sur un objet ouvre un menu contextuel intégré à la fenêtre produit,
  puis l'inspecteur unique donne accès à sa transformation, ses tags et toutes ses propriétés locales.
- `STUDIO-017` : une retouche d'instance passe par dry-run et commande Application, alimente le même
  historique undo/redo et n'est persistée que par la sauvegarde atomique explicite.
- `STUDIO-018` : le clic droit sur un objet propose sa duplication à côté et sa suppression ; cette
  dernière exige une confirmation intégrée, ne sauvegarde rien implicitement et reste annulable.
- `STUDIO-019` : le bouton Ajouter ou le clic droit dans la zone ouvre une palette filtrable de
  blueprints ; l'objet choisi est placé à la position proposée par une commande Application validée.

## Pages spécialisées

- `STUDIO-020` : Dialogues utilise une édition ligne par ligne ; le graphe est une vue avancée.
- `STUDIO-021` : Quêtes utilise objectifs, états et transitions nommés, sans syntaxe compacte.
- `STUDIO-022` : un diagnostic ouvre la page, la ressource et la propriété exactes.
- `STUDIO-023` : les fonctions non encore migrées sont explicitement désactivées et ne simulent
  jamais un succès.

## Responsive, accessibilité et robustesse

- `STUDIO-030` : aucun contrôle principal n’est coupé à 1920×1080, 2560×1440 ou 3840×2160 ; les
  résolutions inférieures au Full HD ne font pas partie de la qualification du Studio.
- `STUDIO-031` : texte ×1,5, focus visible, contraste élevé et mouvements réduits restent utilisables.
- `STUDIO-032` : les opérations longues sont visibles, annulables et ne bloquent pas le thread UI.
- `STUDIO-033` : un crash runtime ou une fermeture forcée propose une reprise sans écrasement
  silencieux du module.
- `STUDIO-034` : le Full HD est le minimum supporté, le QHD la référence de conception et la 4K la
  borne haute qualifiée. À 100 %, le Studio utilise les pixels natifs sans étirer une toile de
  référence ; en 4K, la vue 3D et les surfaces de travail grandissent tandis que la typographie et
  les panneaux restent bornés. Les échelles système applicables sont validées séparément.

## Sortie de migration

- `STUDIO-040` : chaque classe `Window` historique possède une destination unique dans la matrice.
- `STUDIO-041` : une ligne ne passe `Parity` qu’avec tests, smoke et preuve visuelle liés.
- `STUDIO-042` : le plugin n’est retiré qu’après parité des 66 lignes et accord utilisateur.
- `STUDIO-043` : les distributions Studio Windows et Linux ouvrent le même workspace et passent le
  parcours de retouche sans installer l’éditeur Godot.
