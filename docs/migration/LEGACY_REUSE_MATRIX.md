# Matrice de reprise historique — `main` vers `Rust`

Dernière mise à jour : 2026-08-30  
Source de référence : `Karenax/OpenRPG_Forge@a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`  
Destination : branche orpheline `Rust`

Cette matrice empêche deux erreurs opposées : perdre les connaissances utiles du projet historique ou
réintroduire sa dette technique dans le nouveau socle.

## Légende

- **Reprendre** : convertir ou reformuler dans le nouveau projet.
- **Référence** : consulter pour comprendre le besoin, sans copier l’implémentation.
- **Importer sous contrôle** : copier des données/assets après licence, hash et conversion.
- **Réécrire** : conserver le comportement attendu, produire une nouvelle implémentation Rust.
- **Écarter** : ne pas introduire dans la branche `Rust`.
- **Différer** : utile plus tard, mais interdit avant la gate correspondante.

## Règle générale

Aucun fichier de `main` n’est copié « en bloc ». Tout lot de migration doit enregistrer :

```text
source repository
source branch
source commit
source path(s)
reason for reuse
license/provenance
conversion performed
destination path
hashes when applicable
acceptance proof
known differences
```

---

# 1. Vision, exigences et gouvernance

| Élément historique | Décision | Gate | Traitement attendu |
| --- | --- | --- | --- |
| `PROJECT_CONTEXT.md` | Référence | D à 8 | Extraire vision, parcours et limites ; ne pas recopier le récit Godot comme contexte courant. |
| `STATUS.md` | Référence | selon besoin | Utiliser pour distinguer capacité prouvée et promesse ; ne pas importer les chiffres comme état Rust. |
| `TASKS_MVP.md` | Référence | 3 à 8 | Transformer les parcours utiles en critères d’acceptation, pas en backlog à recopier intégralement. |
| `docs/planning/COMPLETE_FUNCTION_BACKLOG.md` | Reprendre par index | 5+ | Conserver les 717 besoins comme catalogue externe ; mapper seulement les IDs servis par une tranche. |
| `tracking/project/AURORA_PARITY_COVERAGE.csv` | Référence | 5+ | Utiliser les identifiants et preuves historiques ; créer plus tard un tracker Rust distinct. |
| `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/` | Référence immuable | 5+ | Consulter l’exigence clean-room ; ne pas copier des données propriétaires. |
| ADR historiques | Référence | toutes | Reprendre les raisons métier encore valides ; les choix Godot/C#/WPF ne s’appliquent pas. |
| procédures de gates et preuves | Reprendre | 0+ | Adapter à Cargo, Bevy, WASM et captures WebGPU. |

## Action initiale

- créer plus tard un index compact `docs/migration/LEGACY_REQUIREMENTS_INDEX.json` ;
- ne pas copier le dossier Aurora complet tant qu’un lot n’en a pas besoin ;
- conserver un lien vers le commit historique ;
- résumer un maximum de cinq exigences par lot de migration fonctionnelle.

---

# 2. Architecture et domaine C#

| Élément | Décision | Raison |
| --- | --- | --- |
| projets C# `src/` | Écarter comme code | Dépendances et structures conçues pour .NET/Godot. |
| `Directory.Build.props` / `Directory.Packages.props` | Écarter | Sans utilité Cargo. |
| `OpenRPGForge.sln` | Écarter | Solution historique uniquement. |
| types métier C# | Référence puis réécrire | Le nom et les invariants peuvent être utiles, pas la forme de classe. |
| command bus | Reprendre le contrat, réécrire | Dry-run, diff, révisions, audit et undo restent centraux. |
| repositories/persistence | Référence | Reprendre les invariants d’atomicité et récupération. |
| validators | Réécrire | Reprendre les cas invalides et les messages utiles. |
| tests Domain/Application | Référence comportementale | Convertir les scénarios importants en tests Rust ; ne pas viser une traduction 1:1. |
| architecture graph Roslyn | Écarter | Remplacer plus tard par outils Cargo/rustdoc ou graphe propre. |

## Règle de traduction

Ne jamais créer automatiquement :

```text
ClassName.cs → class_name.rs
InterfaceX → trait X
ServiceY → struct YService
```

sans avoir d’abord formulé le besoin. Le design Rust doit employer ownership, enums, traits et types
forts de manière idiomatique.

---

# 3. Godot, scènes et renderer

| Élément | Décision | Traitement |
| --- | --- | --- |
| `apps/Forge.Runtime` code Godot | Écarter comme implémentation | Référence pour les parcours runtime seulement. |
| `apps/Forge.Studio` code Godot | Écarter comme implémentation | Référence UX et inventaire de pages. |
| `apps/Forge.Toolset` | Écarter comme implémentation | Les fonctions utilisateur peuvent être remappées plus tard. |
| `.tscn`, `.tres`, `.res` | Ne pas importer directement | Écrire des extracteurs ponctuels uniquement si les données sont indispensables. |
| shaders Godot | Référence visuelle | Réécrire en WGSL après compréhension, pas conversion syntaxique aveugle. |
| WorldEnvironment Godot | Référence de réglages | Convertir exposition, fog, ciel, météo dans des profils Forge neutres. |
| Terrain3D / Godot Flow / PCGODOT | Référence fonctionnelle | Reprendre les concepts de recette, seed et génération partielle ; aucune dépendance runtime. |
| scènes promues WorldGeneration | Import expérimental | Extraire heightmap, placements et profils si possible après Gate 2. |
| PCK et exports Godot | Écarter | Aucun rôle dans `.orpmod` v2. |
| sidecars `.import` / `.godot` | Écarter | Caches spécifiques. |

## Conversion possible d’une scène

Un convertisseur futur peut lire un sous-ensemble textuel de `.tscn` pour extraire :

- transform ;
- référence GLB ;
- lumière ;
- caméra ;
- point de spawn ;
- collisions simples ;
- métadonnées explicitement supportées.

Le résultat est un document Forge v2. Le runtime Rust ne charge jamais directement le fichier Godot.

---

# 4. Formats de projet et modules

| Élément | Décision | Gate | Traitement |
| --- | --- | --- | --- |
| concept workspace | Reprendre | 0-2 | Projet, modules, zones et dossiers `.forge`. |
| IDs stables | Reprendre | 0 | Réimplémenter avec types Rust. |
| révisions persistantes | Reprendre | 2 | Gardes de révision et conflit interprocessus. |
| transactions multi-fichiers | Reprendre | 2 | Journal et récupération atomique. |
| `.orpmod` v1 | Référence | 4 | Concevoir v2 ; importeur v1 différé. |
| manifestes | Reprendre | 4 | Version, compatibilité, hashes, licence, limites. |
| compression et protections | Reprendre | 4 | Traversal, quotas, bombes, MIME, symlinks. |
| packages historiques | Ne pas importer comme runtime | 4 | Servir de fixtures d’import dans un cache de test contrôlé. |
| save format 1.x | Référence | 3-4 | Reprendre sémantique, créer sauvegarde v2. |

## Compatibilité

`.orpmod` v2 doit annoncer explicitement son incompatibilité avec le runtime Godot. Une conversion
future est une commande offline :

```text
forge_cli migrate-v1-module source.orpmod --output workspace/
```

Cette commande n’existe pas avant Gate 4 et ne doit pas être simulée par un stub.

---

# 5. Assets 3D, textures et audio

| Type | Décision | Conditions |
| --- | --- | --- |
| GLB/glTF CC0 ou compatible | Importer sous contrôle | Licence, source, hash, unité, axes, matériaux, animations. |
| Quaternius | Importer sélectivement | Vérifier chaque pack et sa notice ; ne pas copier automatiquement les 365 modèles. |
| KayKit/équivalent | Importer sélectivement | Même procédure. |
| textures PBR | Importer sous contrôle | Vérifier canaux, espace couleur, résolution, tiling et licence. |
| WAV/OGG | Importer sous contrôle | Source, licence, durée, format et niveau. |
| MP3 | Différer ou convertir | Selon licence codecs et pipeline audio retenu. |
| portraits | Importer sous contrôle | Droits, provenance et format. |
| thumbnails générées | Régénérer | Ne pas reprendre les caches historiques. |
| blobs content-addressed | Reprendre le principe | Nouveau cache et nouveau manifeste. |
| assets sans provenance | Écarter | Même s’ils fonctionnent techniquement. |
| assets achetés/Humble/Unity | Cache utilisateur seulement | Ne redistribuer que si la licence le permet explicitement. |

## Pack de preuve Gate 1

Le pack `visual_valley` doit rester petit. Il peut contenir ou télécharger :

- quelques arbres ;
- quelques rochers ;
- végétation de sous-bois ;
- trois jeux de textures terrain ;
- un personnage de preuve ;
- un son d’ambiance facultatif.

Tous les fichiers doivent être redistribuables ou téléchargés par l’utilisateur selon leur licence.

## Pipeline de conversion

Chaque import produit :

```text
source asset
→ validation licence
→ SHA-256 source
→ inspection glTF
→ normalisation unités/axes
→ optimisation optionnelle
→ textures dérivées
→ collision/LOD/imposteur dérivés
→ manifeste Forge
→ cache local
```

Les dérivés ne remplacent pas la source ni sa provenance.

---

# 6. Solaren et Crypte des Cendres

## Solaren

Décision : **référence artistique et première candidate de migration de contenu**, après Gate 4.

À reprendre potentiellement :

- identité de la zone ;
- relief et plan général ;
- village et routes ;
- personnages ;
- portraits ;
- dialogues ;
- quête initiale ;
- audio gouverné ;
- profils météo/caméra ;
- assets CC0 vérifiés ;
- scénarios de sauvegarde et reprise.

À ne pas reprendre :

- scène Godot comme source ;
- scripts C# runtime ;
- branches de module ;
- caches ;
- exécutable ;
- résultats de capture comme preuve du runtime Rust.

Solaren v2 doit être authoré dans le Studio Rust, même si un convertisseur fournit un point de départ.

## Crypte des Cendres

Décision : **module de régression fonctionnelle secondaire**.

Utilités :

- intérieur ;
- portes ;
- pièges ;
- combat ;
- dialogue ;
- quête ;
- transition ;
- sauvegarde ;
- lumière artificielle ;
- comparaison d’import.

Ne pas l’utiliser comme première scène visuelle : la Gate 1 doit rester extérieure et ambitieuse.

---

# 7. WorldGeneration

| Capacité historique | Décision Rust |
| --- | --- |
| recette moteur-neutre | Reprendre |
| seed déterministe | Reprendre |
| étapes partielles | Reprendre |
| preview | Reprendre |
| job durable | Différer après éditeur de base |
| audit MCP | Différer Gate 6 |
| promotion explicite | Reprendre |
| providerKey Godot | Remplacer par plugins Forge |
| scène provider-free | Remplacer par documents Forge + caches |
| preuves de collision/navigation | Recréer |

Le nouveau pipeline est :

```text
WorldRecipe v2
→ validate
→ preview plan
→ execute deterministic stages
→ produce derived caches
→ inspect
→ promote selected results
→ preserve provenance and inverse
```

Le terrain, le scatter et les bâtiments restent modifiables après génération.

---

# 8. Creator et UX

| Surface historique | Décision | Priorité |
| --- | --- | --- |
| Module Hub | Reprendre le parcours | Gate 2 |
| vue 3D centrale | Reprendre | Gate 0-2 |
| hiérarchie | Reprendre | Gate 0-2 |
| inspecteur | Reprendre | Gate 0-2 |
| asset browser | Reprendre | Gate 2-4 |
| dialogues | Réécrire | Gate 3-5 |
| quêtes | Réécrire | Gate 3-5 |
| scripts | Différer | Gate 5 |
| validation | Reprendre progressivement | toutes |
| assistant | Différer | Gate 6 |
| réseau | Différer | Gate 8 |
| 717 routes Studio | Référence | Gate 5+ |
| 66 fenêtres historiques | Ne pas reproduire | Concevoir une seule coque moderne. |

## Règle UX

Ne pas chercher une parité écran par écran. Reprendre le parcours utilisateur et améliorer
l’organisation. Une ancienne fenêtre peut devenir une section, un panneau contextuel ou une commande.

La qualité attendue :

- moderne ;
- cohérente ;
- responsive ;
- accessible ;
- clavier/souris ;
- pas de fenêtres parasites ;
- détails techniques en second niveau ;
- aperçu immédiat ;
- erreurs près de l’action ;
- undo visible.

---

# 9. Gameplay et règles

| Domaine | Décision | Gate |
| --- | --- | --- |
| ruleset `openrpg-forge.systemic-rpg.v1` | Reprendre comme spécification normative | D (document) / 3-5 (implémentation) |
| contrôleur personnage | Réécrire | 1-3 |
| caméra | Réécrire | 1 |
| interaction | Réécrire | 3 |
| inventaire | Réécrire | 3 |
| combat | Réécrire | 3 |
| dialogues | Reprendre les contrats, réécrire | 3 |
| quêtes | Reprendre les contrats, réécrire | 3 |
| factions | Réécrire | 5 |
| pouvoirs/effets | Réécrire | 5 |
| pause tactique | Différer | 5+ |
| groupe | Différer | 5+ |
| environnement comme arme | Nouvelle priorité forte | 3 |
| save delta | Reprendre le principe | 3 |
| autorité serveur | Reprendre le principe | 3 et 8 |

Le ruleset historique est disponible dans `docs/design/OPENRPG_FORGE_RULESET_FR.md`. La reprise est
une copie documentaire intégrale de `archive/main@a7f4a3f5` ; elle ne copie aucune implémentation et
n’ouvre pas les gates gameplay. Les contrats Rust restent à écrire par tranche et ne doivent jamais
prendre la forme de branches spéciales liées à un module.

Provenance vérifiée de cette reprise :

- source : `docs/design/OPENRPG_FORGE_RULESET_FR.md` au snapshot
  `a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0` ;
- création : `b23fa98e0233dd6318cb6de81e4db283f675b5c3` ;
- blob Git : `f9fb40a9b4775d5f761d032a2e14b3a7f4086bea` ;
- SHA-256 : `7c5a3089d4013932fef9a6a22f38e3ca3165dea7e8590fc9273de1242802d37e` ;
- licence source : PolyForm Noncommercial 1.0.0 et notice OpenRPG Forge historique ;
- conversion : aucune ; contenu repris intégralement ;
- limite : section 25 historique non exécutable dans Rust et touches contradictoires avec le manifeste
  Crypte ; aucun profil `forge.generic`, manifeste Crypte ou code C#/Godot n’est importé.

---

# 10. ForgeScript et scripting

| Élément | Décision |
| --- | --- |
| besoin d’un langage accessible | Reprendre |
| API RPG contrôlée | Reprendre |
| quotas | Reprendre |
| interdiction fichiers/réseau/processus | Reprendre |
| AngelScript GDExtension | Écarter |
| bytecode historique | Écarter |
| sources de scripts de modules | Référence métier |
| tests de comportement | Convertir |

Le fournisseur final reste ouvert. Aucune dépendance scripting n’est ajoutée avant Gate 5. Les
candidats doivent être comparés sur sandbox, WASM, ergonomie, debug, sérialisation et maintenance.

---

# 11. IA et MCP

| Élément historique | Décision |
| --- | --- |
| outils lecture/mutation séparés | Reprendre |
| dry-run | Reprendre |
| plan_id + revision | Reprendre |
| diff et undo | Reprendre |
| outils haut niveau | Reprendre |
| audit | Reprendre |
| host C# | Réécrire en Rust |
| inventaire de 271 outils | Référence, ne pas recopier |
| shell arbitraire interdit | Reprendre |
| providers interchangeables | Reprendre |

Le premier MCP Rust ne contient que quelques opérations prouvées de l’éditeur de monde. Ajouter un
outil uniquement après stabilisation du workflow manuel correspondant.

---

# 12. Réseau et distribution

| Élément | Décision | Gate |
| --- | --- | --- |
| lanceur WPF | Écarter | — |
| profil de lanceur | Reprendre le besoin | 7 |
| transport TCP/TLS | Référence | 8 |
| invitations privées | Référence | 8 |
| serveur headless C# | Réécrire | 8 |
| protocole historique | Ne pas garantir compatibilité | 8 |
| installateur NSIS | Référence de parcours | 7 |
| manifestes/SBOM | Reprendre | 7 |
| Authenticode | Reprendre le processus | 7 |
| distributions `exe/` | Écarter | — |

Le réseau arrive après la stabilité du solo et du format v2.

---

# 13. Tests et preuves historiques

## À convertir

- parcours utilisateur ;
- cas invalides ;
- invariants de module ;
- sauvegarde/reprise ;
- transitions de zones ;
- sécurité d’archive ;
- transactions ;
- déterminisme ;
- import d’assets ;
- autorité gameplay ;
- UX sans JSON manuel.

## À ne pas convertir mécaniquement

- tests de classes ou namespaces C# ;
- tests de présence de fenêtre Godot ;
- snapshots dépendant du layout historique ;
- compte de routes comme objectif principal ;
- tests de graphe Roslyn ;
- tests de fichiers générés sans valeur comportementale.

## Méthode

Pour chaque test historique sélectionné :

1. formuler le comportement en français ou Gherkin court ;
2. décider s’il reste utile ;
3. écrire un test Rust indépendant ;
4. créer la fixture minimale ;
5. prouver le comportement sur la nouvelle architecture ;
6. lier le test à l’exigence historique si pertinent.

---

# 14. Priorité d’extraction

## Avant Gate 1

Importer uniquement :

- un petit pack d’assets de forêt juridiquement clair ;
- quelques textures PBR ;
- un personnage de preuve ;
- les concepts de profil graphique ;
- les règles de provenance nécessaires.

## Gate 2

Extraire :

- documents de zones ;
- terrain/biomes/splines ;
- commandes transactionnelles ;
- UX de placement ;
- sauvegarde projet.

## Gate 3

Extraire :

- contrats personnage, interaction, dialogue, quête, inventaire et combat ;
- règles environnementales ;
- scénarios de sauvegarde.

## Gate 4

Extraire :

- manifestes d’assets ;
- `.orpmod` ;
- sécurité package ;
- un sous-ensemble de Solaren ;
- Crypte comme second module.

## Gate 5+

Extraire à la demande :

- catalogue Aurora ;
- Creator spécialisé ;
- scripting ;
- MCP ;
- distribution ;
- réseau.

---

# 15. Checklist d’un lot de migration

```text
[ ] besoin utilisateur identifié
[ ] source main et commit cités
[ ] licence vérifiée
[ ] données nécessaires seulement
[ ] contrat Rust moteur-neutre
[ ] critères d’acceptation
[ ] implémentation indépendante
[ ] tests
[ ] capture si visuel
[ ] benchmark si performance
[ ] round-trip si données
[ ] provenance et hashes
[ ] écarts documentés
[ ] roadmap mise à jour
```

Un lot qui ne remplit pas cette checklist reste partiel et ne doit pas être annoncé comme migration
terminée.
