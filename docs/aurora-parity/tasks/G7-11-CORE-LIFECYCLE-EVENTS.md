# G7-11 — Événements de cycle de vie Core

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-PROJ-006` — événements de cycle de vie du projet et du monde ;
- `ORF-AREA-022` — événements d’entrée et de sortie de zone.

## Résultat livré

Le domaine expose un contrat d’événement indépendant du moteur avec UUID, schéma v1, séquence,
binding ForgeScript, version d’API, projet, module, ancien et nouveau contexte de zone, acteur,
transition, cause, autorité, provenance et chemin de document. Le coordinateur impose l’ordre
`ProjectLoaded → AreaEntered → WorldStarted`, puis publie une transition atomique ordonnée
`AreaExited → AreaEntered`.

Les opérations acceptent le dry-run, ne mutent l’état qu’après validation et renvoient des
diagnostics stables pour ordre invalide, projet ou zone absente, acteur inconnu, monde non démarré
et transition vers la même zone. Un acteur de transition doit appartenir à la zone qu’il quitte.

Le runtime solo publie réellement les trois événements de démarrage pour un projet source, un
snapshot ou un package `.orpmod`. Une reprise de sauvegarde reçoit la cause `SaveResume`; un
démarrage normal reçoit `Startup`. Chaque émission est observable dans le journal de playtest et
le runtime refuse de continuer si le contrat déterministe est rompu.

## Preuves

- `RuntimeLifecycleCoordinatorTests` couvre ordre, séquences, bindings, transition, contexte partagé,
  acteur absent, références invalides et dry-run sans publication ;
- le build Release du runtime valide le branchement sans dépendance du domaine au moteur ;
- `OPENRPG_FORGE_RUNTIME_LIFECYCLE_PASS` prouve le flux réel
  `ProjectLoaded>AreaEntered>WorldStarted` dans le scénario Core générique ;
- `scripts/Verify.ps1` exige désormais ce marqueur dans le gate intégré.

## Limites conservées

- le chargement complet de la scène de destination à partir d’un portail n’est pas encore branché ;
- les observateurs sont locaux au processus, sans réplication réseau ni journal persistant ;
- les événements gameplay spécialisés restent à définir dans leurs gates respectifs ;
- l’interface Playtests consomme le journal structuré existant, sans chronologie graphique dédiée.

Les deux capacités passent au statut `Implemented` partiel.
