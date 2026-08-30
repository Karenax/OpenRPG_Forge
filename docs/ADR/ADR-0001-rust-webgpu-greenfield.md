# ADR-0001 — Réécriture greenfield Rust/WebGPU

Date : 2026-08-30  
Statut : **accepté pour la branche `Rust`**  
Portée : branche orpheline `Rust` uniquement  
Référence historique : `main@a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`

## Contexte

OpenRPG Forge possède sur `main` une implémentation C#/Godot très étendue : Creator, runtime,
formats, assets, génération de monde, scripting, MCP, sauvegardes, réseau et couverture fonctionnelle
inspirée des possibilités d’Aurora.

Cette implémentation a permis de formaliser le produit, mais elle est devenue coûteuse à faire évoluer
vers une architecture Rust/WebGPU :

- la majorité des adapters, scènes, ressources et preuves est liée à Godot ;
- une conversion incrémentale maintiendrait longtemps deux moteurs et deux interfaces ;
- les agents IA obtiennent plus facilement un résultat visuel cohérent dans une pile WebGPU et web ;
- la taille et la complexité historiques réduisent la lisibilité du dépôt ;
- le nouveau projet doit d’abord prouver sa qualité visuelle avant de reproduire l’exhaustivité.

Le propriétaire a créé `Rust` comme branche orpheline avec un commit racine vide
`2741ec5938dd6de8556a1bf9c3c589bc477a0f3f` et le message
`chore: initialize independent Rust rewrite`.

## Décision

La branche `Rust` est une réécriture indépendante. Elle ne traduit pas mécaniquement le code C# et
ne conserve pas Godot comme couche temporaire.

### Socle moteur

- Rust stable, édition 2024 ;
- Bevy 0.19.x comme socle initial de moteur, ECS, assets, rendu et outils ;
- `wgpu`/WebGPU comme abstraction graphique commune native et WASM ;
- WGSL pour les shaders spécifiques Forge ;
- Rapier 3D comme physique initiale.

Bevy est utilisé avant d’envisager un renderer raw `wgpu`. Une extension bas niveau exige une limite
mesurée. Un fork de Bevy n’est pas autorisé dans les premières gates.

### Studio

La cible initiale du Studio est :

```text
Tauri 2 (backend Rust)
└── Leptos/WASM (interface)
    ├── HTML/CSS
    └── Bevy/WASM/WebGPU canvas
```

Cette pile préserve Rust comme langage d’autorité tout en permettant une interface de qualité web.
Le JavaScript est limité au bootstrap ou aux fonctions navigateur impossibles à exposer autrement.
Aucune règle métier ne vit en JavaScript.

L’intégration doit être validée dans la Gate 0. Si la WebView ou le partage Bevy/WASM est insuffisant,
un Studio natif Bevy UI/Feathers peut le remplacer après benchmark et nouvel ADR.

### Runtime

Le runtime de jeu est un exécutable Bevy natif utilisant les backends `wgpu` adaptés à la plateforme.
Il partage les documents, commandes, règles et plugins moteur avec le viewport Studio, sans dépendre de
Tauri ou Leptos.

### Source de vérité

Les sources de vérité sont des documents Forge JSON versionnés. L’ECS Bevy, les buffers GPU,
collisions, navmeshes et scènes rendues sont des projections ou caches dérivés.

### Migration

La migration depuis `main` porte sur :

- exigences et comportements ;
- formats conceptuels ;
- données moteur-neutres ;
- assets légalement réutilisables ;
- provenance ;
- scénarios de test.

Elle ne porte pas sur :

- code C# ;
- scènes ou ressources Godot ;
- WPF ;
- DLL et exécutables ;
- hiérarchie de classes ;
- adapters de compatibilité sans besoin démontré.

### Ordre de livraison

Le projet commence par :

1. fondation native/WASM/Studio ;
2. vallée visuelle de référence ;
3. éditeur de monde ;
4. première boucle RPG ;
5. modules, narration, scripting, MCP et distribution.

Le catalogue historique de 717 capacités reste une source d’exigences, mais n’est pas reconstruit
avant la preuve visuelle et l’éditeur de monde.

## Alternatives évaluées

### A. Continuer uniquement avec C#/Godot

Avantages : code, fonctionnalités et tests déjà présents.  
Rejet : ne résout pas l’objectif de renderer WebGPU, la boucle visuelle par IA ni la simplification du
dépôt.

### B. Migrer progressivement C#/Godot vers Rust

Avantages : réutilisation partielle et continuité apparente.  
Rejet : double runtime, adapters temporaires, formats hybrides, tests dupliqués et dette de migration
prolongée.

### C. Écrire directement un moteur raw `wgpu`

Avantages : contrôle maximal.  
Rejet initial : coût élevé pour ECS, assets, animation, PBR, picking, UI, outils et multiplateforme.
Cette option reste possible pour un sous-système isolé après mesure.

### D. Three.js/TypeScript pour tout le moteur

Avantages : productivité visuelle élevée et interface web naturelle.  
Rejet comme cœur : le propriétaire choisit Rust pour la simulation, la performance, les outils natifs
et le partage avec le runtime. Les qualités du web sont conservées dans le Studio.

### E. Bevy UI uniquement, sans coque web

Avantages : une seule pile native, accès direct au renderer.  
Non retenu comme première option : le motif du reboot inclut la qualité et la productivité de
l’interface web. Cette option reste le fallback officiel de la Gate 0.

## Preuves attendues

Cette décision n’est considérée comme techniquement validée qu’après :

- build natif Bevy ;
- build WASM WebGPU ;
- intégration du canvas dans Tauri/Leptos ;
- scène partagée ;
- CI reproductible ;
- vallée 512 × 512 m ;
- cible 60 FPS 1080p sur PC milieu de gamme ;
- capture et revue visuelle ;
- sauvegarde/rechargement dans l’éditeur de monde.

Les preuves seront référencées depuis la roadmap et les futurs rapports de gate.

## Conséquences

### Bénéfices

- base de code propre et cohérente ;
- Rust commun au domaine, runtime, outils et frontend WASM ;
- renderer moderne natif et navigateur ;
- séparation nette des données et du moteur ;
- dépôt plus lisible pour les agents ;
- absence de dette de compatibilité implicite ;
- possibilité de conserver une interface HTML/CSS ;
- réutilisation des connaissances du projet historique.

### Coûts

- aucune fonctionnalité historique n’existe automatiquement ;
- convertisseurs à écrire ;
- stabilité de Bevy et du chemin WASM à surveiller ;
- temps de compilation Rust ;
- nécessité de reconstruire les outils ;
- risque de sous-estimer la complexité d’un moteur de monde ;
- impossibilité de fusion Git classique avec `main`.

### Risques contrôlés

- Gate 0 tranche la viabilité Studio web ;
- Gate 1 tranche visuel et performance ;
- Gate 2 tranche la productivité d’édition ;
- aucun portage massif avant ces gates ;
- Bevy peut être étendu ou remplacé par sous-système, pas par réécriture réflexe ;
- la branche peut devenir un dépôt autonome si la séparation Git gêne les opérations.

## Relation avec les ADR de `main`

Cet ADR ne modifie pas les décisions de la branche `main`. Il remplace le choix Godot uniquement dans
la portée de la branche orpheline `Rust`. Il ne doit pas être interprété comme une validation de
publication ou comme une décision de supprimer la ligne C#/Godot.

## Remplacement

Toute modification du socle moteur, du shell Studio ou de la stratégie greenfield nécessite un nouvel
ADR accepté, lié à celui-ci. Ne pas réécrire cette décision silencieusement.
