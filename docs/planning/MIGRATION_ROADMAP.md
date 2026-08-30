# Roadmap de migration — OpenRPG Forge Rust/WebGPU

Dernière mise à jour : 2026-08-30  
Branche : `Rust`  
État global : **fondation documentaire**  
Gate active : **Gate 0 — Fondation reproductible**

Cette roadmap transforme `PROJECT_CONTEXT.md` en lots exécutables. Elle n’est pas un calendrier et ne
contient aucune promesse de délai. Une gate ne passe qu’avec ses preuves.

## Règles de progression

1. Une seule gate principale est active.
2. Les travaux d’une gate ultérieure sont interdits s’ils détournent la gate active.
3. Chaque lot doit produire un résultat lançable ou vérifiable.
4. Les tâches visuelles nécessitent captures et mesures.
5. Une checkbox n’est cochée qu’après preuve dans le dépôt.
6. Une fonctionnalité historique n’est pas « migrée » parce qu’un type vide porte son nom.
7. Les échecs et limites restent visibles.
8. La roadmap est mise à jour dans le même lot que la preuve.

---

# Gate D — Fondation documentaire

Objectif : rendre la branche compréhensible avant tout code.

- [x] `D-001` Créer la branche orpheline `Rust`.
- [x] `D-002` Confirmer l’absence d’ancêtre commun et l’arbre initial vide.
- [x] `D-003` Ajouter `README.md`.
- [x] `D-004` Ajouter le contexte canonique `PROJECT_CONTEXT.md`.
- [x] `D-005` Ajouter les règles agent `AGENTS.md`.
- [x] `D-006` Accepter l’ADR greenfield Rust/WebGPU.
- [x] `D-007` Ajouter cette roadmap.
- [x] `D-008` Ajouter la matrice de reprise historique.

Preuve attendue : documents présents sur `Rust`, cohérents et sans lien cassé vers un fichier requis.

---

# Gate 0 — Fondation reproductible native + WASM + Studio

## But

Prouver que la même base Rust/Bevy peut alimenter un runtime natif et un viewport WebGPU intégré dans
une application de Studio sans architecture cachée ou non reproductible.

## R0-A — Workspace et outillage

- [ ] `R0-001` Créer le workspace Cargo minimal.
- [ ] `R0-002` Ajouter `rust-toolchain.toml` avec une version stable épinglée.
- [ ] `R0-003` Activer l’édition Rust 2024.
- [ ] `R0-004` Définir les profils `dev`, `release` et `wasm-release`.
- [ ] `R0-005` Ajouter `.gitignore` pour Cargo, Tauri, Trunk, WASM, caches et preuves locales.
- [ ] `R0-006` Ajouter `cargo fmt` et clippy avec warnings projet en erreurs.
- [ ] `R0-007` Ajouter `cargo-deny` et la politique de licences.
- [ ] `R0-008` Ajouter la journalisation structurée `tracing`.
- [ ] `R0-009` Ajouter une commande unique vérifiée pour bootstrapper le développement Windows.
- [ ] `R0-010` Documenter uniquement les commandes réellement exécutées.

Critères : clone propre, toolchain automatique, aucune étape non documentée.

## R0-B — Crates minimales

Créer uniquement :

- [ ] `R0-011` `crates/forge_core`.
- [ ] `R0-012` `crates/forge_project`.
- [ ] `R0-013` `crates/forge_world`.
- [ ] `R0-014` `crates/forge_engine`.
- [ ] `R0-015` `crates/forge_assets`.
- [ ] `R0-016` `apps/forge_runtime`.
- [ ] `R0-017` `apps/forge_studio`.
- [ ] `R0-018` `tools/forge_cli`.

Chaque crate possède un README court, une responsabilité et au moins un test pertinent. Ne pas ajouter
de crate vide de « préparation future ».

## R0-C — Runtime natif

- [ ] `R0-019` Ajouter Bevy 0.19.x avec features minimales nécessaires.
- [ ] `R0-020` Ouvrir une fenêtre native.
- [ ] `R0-021` Initialiser le backend `wgpu` haute performance.
- [ ] `R0-022` Afficher une scène partagée simple : sol, objet PBR, lumière et caméra.
- [ ] `R0-023` Ajouter redimensionnement, VSync et fermeture propre.
- [ ] `R0-024` Afficher adapter GPU, backend et limites utiles.
- [ ] `R0-025` Ajouter un overlay FPS/frame time/draw calls.
- [ ] `R0-026` Capturer une image automatisable.

## R0-D — WASM/WebGPU

- [ ] `R0-027` Ajouter la cible `wasm32-unknown-unknown`.
- [ ] `R0-028` Compiler Bevy avec la feature WebGPU appropriée.
- [ ] `R0-029` Afficher la même scène partagée dans un canvas navigateur.
- [ ] `R0-030` Détecter WebGPU et produire une erreur utilisateur claire s’il est indisponible.
- [ ] `R0-031` Vérifier Chrome/Edge desktop.
- [ ] `R0-032` Mesurer chargement, mémoire et frame time.
- [ ] `R0-033` Documenter les limites WASM observées.

Le fallback WebGL2 n’est pas requis pour la Gate 0.

## R0-E — Studio Tauri + Leptos

- [ ] `R0-034` Créer le shell Tauri 2.
- [ ] `R0-035` Créer le frontend Leptos/Trunk.
- [ ] `R0-036` Ajouter un design system CSS minimal : tokens, surfaces, typographie, focus.
- [ ] `R0-037` Intégrer le canvas Bevy/WASM dans la page Studio.
- [ ] `R0-038` Garantir que les rerenders UI ne reconstruisent pas le monde 3D.
- [ ] `R0-039` Ajouter un panneau hiérarchie factice alimenté par données Rust partagées.
- [ ] `R0-040` Ajouter un inspecteur factice lié à la sélection 3D.
- [ ] `R0-041` Ajouter un canal de commande typé frontend ↔ backend.
- [ ] `R0-042` Borner les permissions Tauri au strict nécessaire.
- [ ] `R0-043` Capturer la fenêtre complète du Studio.
- [ ] `R0-044` Mesurer latence d’interaction et stabilité du canvas.

## R0-F — Données partagées

- [ ] `R0-045` Définir `ProjectId`, `AreaId`, `EntityId`, `AssetId`, `Revision`.
- [ ] `R0-046` Définir transform moteur-neutre, mètres, Y-up, droitier.
- [ ] `R0-047` Définir un `SceneProofDocument` JSON versionné.
- [ ] `R0-048` Charger ce document en natif et en WASM.
- [ ] `R0-049` Vérifier round-trip sans perte.
- [ ] `R0-050` Vérifier que les types persistants ne dépendent pas de Bevy.

## R0-G — CI et preuves

- [ ] `R0-051` CI format.
- [ ] `R0-052` CI clippy.
- [ ] `R0-053` CI tests natifs.
- [ ] `R0-054` CI build Windows.
- [ ] `R0-055` CI build Linux.
- [ ] `R0-056` CI build WASM.
- [ ] `R0-057` Rapport de licences.
- [ ] `R0-058` Capture runtime native.
- [ ] `R0-059` Capture Studio.
- [ ] `R0-060` Rapport Gate 0.

## Conditions de passage Gate 0

Toutes sont nécessaires :

- [ ] un clone propre compile sans correction manuelle ;
- [ ] runtime natif lancé ;
- [ ] build WASM WebGPU lancée ;
- [ ] Studio Tauri/Leptos lancé avec canvas ;
- [ ] même document source visible sur les deux cibles ;
- [ ] sélection simple partagée ;
- [ ] tests et CI verts ;
- [ ] aucune règle métier en JavaScript ;
- [ ] aucune dépendance à `main` à l’exécution ;
- [ ] capture et rapport inspectés ;
- [ ] décision « Studio web confirmé » ou « fallback natif » enregistrée.

---

# Gate 1 — Visual Valley

## But

Prouver que le nouveau socle produit un environnement plus convaincant que la base historique tout en
tenant 60 FPS sur PC milieu de gamme.

## Scène de référence

Créer `examples/visual_valley` avec seed et paramètres fixés :

```text
surface                 512 × 512 m
profil                   vallée forestière naturelle
terrain                  relief, chemin, rivière et berge
matériaux                herbe, terre, roche minimum
végétation               arbres, arbustes, fougères, herbe
météo                    clair + pluie de preuve
éclairage                soleil, ciel, atmosphère, brouillard
personnage               capsule ou modèle gouverné, taille humaine
caméra                    troisième personne, option première personne
physique                  sol, rochers, arbres proches, eau bornée
qualité                   Low / Medium / High / Ultra
```

## R1-A — Terrain

- [ ] `R1-001` Document de terrain versionné.
- [ ] `R1-002` Découpage en chunks.
- [ ] `R1-003` Mesh et normales.
- [ ] `R1-004` Trois matériaux PBR minimum.
- [ ] `R1-005` Blend par masques.
- [ ] `R1-006` collisions locales.
- [ ] `R1-007` mise à jour partielle.
- [ ] `R1-008` debug chunks/LOD.

## R1-B — Végétation GPU

- [ ] `R1-009` Scatter déterministe.
- [ ] `R1-010` Instances regroupées.
- [ ] `R1-011` LOD arbres.
- [ ] `R1-012` imposteurs ou HLOD lointain.
- [ ] `R1-013` herbe dense sans entité par brin.
- [ ] `R1-014` vent shader.
- [ ] `R1-015` interaction locale avec personnage.
- [ ] `R1-016` culling par chunks.
- [ ] `R1-017` réglage des ombres par distance.
- [ ] `R1-018` métriques instances visibles/cullées.

## R1-C — Atmosphère et eau

- [ ] `R1-019` ciel et soleil cohérents.
- [ ] `R1-020` brouillard atmosphérique.
- [ ] `R1-021` brouillard volumétrique ou approximation mesurée.
- [ ] `R1-022` eau de rivière.
- [ ] `R1-023` réflexion/réfraction selon profil.
- [ ] `R1-024` pluie.
- [ ] `R1-025` wetness réversible.
- [ ] `R1-026` bloom et color grading bornés.

## R1-D — Personnage et caméra

- [ ] `R1-027` contrôleur personnage Rapier.
- [ ] `R1-028` pente et marches.
- [ ] `R1-029` caméra troisième personne.
- [ ] `R1-030` occlusion caméra.
- [ ] `R1-031` option première personne.
- [ ] `R1-032` interaction végétation.
- [ ] `R1-033` animation minimale si asset disponible.

## R1-E — Performance

- [ ] `R1-034` overlay CPU/GPU.
- [ ] `R1-035` frame histogram.
- [ ] `R1-036` moyenne, p95, 1 % low.
- [ ] `R1-037` draw calls, triangles, instances.
- [ ] `R1-038` VRAM estimée.
- [ ] `R1-039` préchauffage pipelines.
- [ ] `R1-040` profil dynamique.
- [ ] `R1-041` benchmark RTX 3060 ou RX 6600.
- [ ] `R1-042` benchmark RX 7800 XT de développement.
- [ ] `R1-043` comparaison native vs Studio WebGPU.

## R1-F — Revue visuelle

- [ ] `R1-044` captures fixes de jour.
- [ ] `R1-045` captures sous pluie.
- [ ] `R1-046` vue à hauteur humaine.
- [ ] `R1-047` vue lointaine.
- [ ] `R1-048` vidéo de déplacement courte.
- [ ] `R1-049` revue des matériaux.
- [ ] `R1-050` revue de composition et lisibilité.
- [ ] `R1-051` comparaison avec la référence WebGPU ayant motivé le reboot.
- [ ] `R1-052` rapport Gate 1 avec verdict poursuivre/réorienter/arrêter.

## Conditions de passage Gate 1

- [ ] qualité visuelle jugée clairement supérieure à la base Godot ;
- [ ] 60 FPS moyens en 1080p High sur GPU milieu de gamme ;
- [ ] p95 ≤ 16,7 ms ;
- [ ] 1 % low ≥ 45 FPS ;
- [ ] absence de stutter récurrent ;
- [ ] profils qualité fonctionnels ;
- [ ] parité de données entre natif et Studio ;
- [ ] code sans dépendance propriétaire obligatoire ;
- [ ] rapport et captures versionnés.

---

# Gate 2 — Éditeur de monde

## But

Créer une carte complète sans modifier de JSON à la main, avec une expérience proche d’un constructeur
de monde accessible.

## R2-A — Projet et zones

- [ ] `R2-001` assistant nouveau projet.
- [ ] `R2-002` nouveau module.
- [ ] `R2-003` nouvelle zone extérieure.
- [ ] `R2-004` métadonnées et point de départ.
- [ ] `R2-005` onglets de zones.
- [ ] `R2-006` hiérarchie et sélection.

## R2-B — Brosses de terrain

- [ ] `R2-007` lever/abaisser.
- [ ] `R2-008` lisser.
- [ ] `R2-009` aplatir.
- [ ] `R2-010` bruit.
- [ ] `R2-011` pente.
- [ ] `R2-012` peinture matériaux.
- [ ] `R2-013` visualisation rayon/intensité.
- [ ] `R2-014` recalcul partiel.

## R2-C — Biomes et végétation

- [ ] `R2-015` pinceau densité.
- [ ] `R2-016` catalogues d’espèces.
- [ ] `R2-017` exclusions.
- [ ] `R2-018` seed stable.
- [ ] `R2-019` aperçu temps réel.
- [ ] `R2-020` promotion et retouche.

## R2-D — Splines

- [ ] `R2-021` création/édition de chemin.
- [ ] `R2-022` route avec profil.
- [ ] `R2-023` rivière et carving.
- [ ] `R2-024` intersections.
- [ ] `R2-025` clôture ou mur de preuve.

## R2-E — Placements et bâtiments

- [ ] `R2-026` navigateur d’assets.
- [ ] `R2-027` drag/drop.
- [ ] `R2-028` picking.
- [ ] `R2-029` gizmo translation/rotation/échelle.
- [ ] `R2-030` duplication et suppression.
- [ ] `R2-031` snap surface/grille.
- [ ] `R2-032` bâtiment paramétrique simple.
- [ ] `R2-033` intérieur et porte franchissable.

## R2-F — Commandes, sauvegarde et playtest

- [ ] `R2-034` bus de commandes.
- [ ] `R2-035` dry-run/diff.
- [ ] `R2-036` undo/redo.
- [ ] `R2-037` audit.
- [ ] `R2-038` sauvegarde atomique.
- [ ] `R2-039` récupération interruption.
- [ ] `R2-040` réouverture exacte.
- [ ] `R2-041` bouton Jouer.
- [ ] `R2-042` retour diagnostics.
- [ ] `R2-043` parcours de preuve sans JSON manuel.

## Conditions de passage Gate 2

- [ ] création d’une petite vallée avec forêt, route, rivière et maison ;
- [ ] undo/redo sur chaque opération ;
- [ ] sauvegarde/réouverture exacte ;
- [ ] passage jeu immédiat ;
- [ ] performance interactive ;
- [ ] aucune source de vérité cachée dans Bevy ;
- [ ] capture et procédure reproductible.

---

# Gate 3 — Première boucle RPG environnementale

## R3-A — Fondations

- [ ] joueur, stats minimales et inventaire ;
- [ ] PNJ et navigation ;
- [ ] interaction ciblée ;
- [ ] dialogue ;
- [ ] quête ;
- [ ] ennemi ;
- [ ] combat ;
- [ ] sauvegarde/reprise.

## R3-B — Monde comme arme

- [ ] objet poussable ;
- [ ] objet lançable ;
- [ ] dégâts d’impact ;
- [ ] chute ou piège ;
- [ ] feu ;
- [ ] eau/extinction ;
- [ ] objet destructible ;
- [ ] réaction IA au danger.

## R3-C — Parcours

Le joueur reçoit une quête, parle à un PNJ, traverse la zone, utilise un élément du décor pour vaincre
ou neutraliser un ennemi, récupère un objet, termine l’objectif, sauvegarde et reprend la session.

La gate passe uniquement si le parcours est authoré depuis le Studio.

---

# Gate 4 — Assets gouvernés et `.orpmod` v2

- [ ] catalogue d’assets ;
- [ ] import GLB ;
- [ ] import textures ;
- [ ] import audio ;
- [ ] provenance et licence ;
- [ ] hash et dérivés ;
- [ ] thumbnails ;
- [ ] validation publication ;
- [ ] package `.orpmod` v2 ;
- [ ] protections archive ;
- [ ] installation ;
- [ ] chargement runtime ;
- [ ] second module de régression ;
- [ ] import expérimental de données Solaren sélectionnées.

---

# Gate 5 — Creator RPG étendu

Reprendre progressivement les besoins historiques par parcours :

- dialogues avancés ;
- quêtes et journal ;
- factions ;
- blueprints ;
- objets et conteneurs ;
- créatures ;
- capacités et effets ;
- cinématiques ;
- calendrier ;
- rencontres ;
- profils de règles ;
- scripting sandboxé ;
- validation et diagnostics.

Chaque lot référence les IDs historiques utiles, sans chercher une couverture artificielle.

---

# Gate 6 — IA et MCP gouvernés

- [ ] inspection projet ;
- [ ] recherche d’assets ;
- [ ] proposition de carte ;
- [ ] génération de plan ;
- [ ] dry-run ;
- [ ] approbation ;
- [ ] application transactionnelle ;
- [ ] undo ;
- [ ] tests ;
- [ ] captures ;
- [ ] audit ;
- [ ] quotas ;
- [ ] absence de shell arbitraire.

---

# Gate 7 — Monde étendu et distribution

- [ ] streaming de cellules ;
- [ ] HLOD ;
- [ ] sauvegardes multi-zones ;
- [ ] packaging runtime ;
- [ ] profils graphiques ;
- [ ] lanceur ;
- [ ] builds Windows/Linux ;
- [ ] licences agrégées ;
- [ ] installateur ;
- [ ] qualification sur machine propre.

---

# Gate 8 — Multijoueur autoritaire

- [ ] simulation serveur ;
- [ ] transport ;
- [ ] admission ;
- [ ] présence ;
- [ ] mouvement ;
- [ ] combat ;
- [ ] inventaire ;
- [ ] quêtes ;
- [ ] changements de zone ;
- [ ] sauvegarde campagne ;
- [ ] reconnexion ;
- [ ] sécurité WAN ;
- [ ] tests à plusieurs processus et réseaux distincts.

---

# Scorecard de décision

À remplir à Gate 1 et Gate 2 :

| Critère | Baseline historique | Rust/WebGPU | Verdict |
| --- | ---: | ---: | --- |
| Impact visuel | à mesurer | à mesurer | ouvert |
| FPS moyen 1080p | à mesurer | à mesurer | ouvert |
| p95 | à mesurer | à mesurer | ouvert |
| 1 % low | à mesurer | à mesurer | ouvert |
| VRAM | à mesurer | à mesurer | ouvert |
| Temps d’itération IA | à mesurer | à mesurer | ouvert |
| Complexité dépôt | à mesurer | à mesurer | ouvert |
| Qualité UI | à mesurer | à mesurer | ouvert |
| Stabilité éditeur | à mesurer | à mesurer | ouvert |
| Import d’assets | à mesurer | à mesurer | ouvert |

Décisions possibles :

- **Poursuivre** : les objectifs sont atteints.
- **Réorienter** : remplacer une couche précise.
- **Limiter** : conserver la technologie pour le renderer ou l’éditeur seulement.
- **Arrêter** : la nouvelle pile n’apporte pas le gain attendu.

Aucune décision ne doit être formulée uniquement sur la quantité de code déjà écrite.
