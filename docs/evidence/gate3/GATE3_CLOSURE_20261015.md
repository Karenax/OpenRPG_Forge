# Gate 3 — clôture du 15 octobre 2026

**Gate 3 est clôturée sur les preuves locales de la version commune et les
validations du propriétaire.** Les 21 critères R3-A, R3-B et R3-P01 à R3-P05
ont été rapprochés des résultats réels. Aucun critère fonctionnel requis ne
reste ouvert dans ce périmètre.

Le propriétaire a accepté le 13 octobre la boucle RPG du jeu séparé et le
parcours complet créé depuis Studio, puis demandé de mener le travail jusqu’à
clôture. Le 15 octobre, il retire explicitement l’attente des validations
GitHub des critères à retenir. [ADR-0023](../../ADR/ADR-0023-gate3-owner-removes-github-ci-criterion.md)
consigne cette décision. Elle permet ce verdict sans modifier les résultats
distants et ne réactive pas l’ancienne exception ADR-0006.

## Version et méthode de qualification

- Checkout commun : `E:\OpenRPG_Forge\OpenRPG_Forge-Rust`, branche `Rust`.
- Candidat publié : `3e0044d5f63b26c678d9db19380a66db832d9de3`.
- Arbre publié : `861a739f743d35a763fb80bb6978221f1094ffba`.
- Empreinte des 2 630 entrées consommées Source013 :
  `4178ac3b3f7d028cb5b94ca0d870d67c245fc0418a595f375e6a8eb75027b163`.
- Les preuves012 gardent leurs révisions et identités originales. Leur
  réutilisation est justifiée par la comparaison des composants inchangés ;
  elles ne sont pas rebaptisées en acquisitions013.

Le [correctif013](../../../../.gate3-evidence/20261015/closure-installed-game-environment-013/source-change.json)
ne modifie que le raccord de configuration du Studio installé et sa recette
de livraison. Le [mapping Release](../../../../.gate3-evidence/20261015/closure-impact-012-to-013-001/release-addendum-001/release-impact.json)
confirme Game et Runtime bit-identiques à012. Le
[mapping WASM](../../../../.gate3-evidence/20261015/closure-impact-012-to-013-001/wasm-addendum-001/wasm-impact.json)
retrouve 3 023 fichiers identiques sur 3 025 ; les deux différences sont des
reçus diagnostiques régénérés. Le code et les assets du frontend sont identiques.
Le Studio natif013 est contrôlé séparément dans le parcours installé réel.

L’[audit indépendant après publication](../../../../.gate3-evidence/20261015/closure-publication-post-audit-013-001/result.json)
vérifie les 176 chemins publiés, la correspondance des 2 630 entrées et quinze
normalisations de fins de ligne Git seulement. Les 1 346 enregistrements de
statut hors lot sont identiques avant/après publication ; l’index est sans
changement préparé et les références Git ont une divergence `0/0`.
Les présents documents de clôture sont postérieurs à ce commit et restent
locaux ; aucune nouvelle publication n’est effectuée implicitement.

## Matrice des critères officiels

Les liens A–E désignent les lecteurs d’observations réelles, avec gardes des
sources/contenus et rapports des processus fermés normalement. Les contrôles
non requis par un scénario ne sont pas interprétés comme des échecs.

| R3-A — fondations | Verdict | Preuves déterminantes |
|---|---|---|
| Joueur, statistiques minimales et inventaire | PASS | A012, B012 ; état, loot et persistance |
| PNJ et navigation | PASS | A–E012 ; tests Native013 des KCC, portes, obstacles, NoPath et danger |
| Interaction ciblée | PASS | A012 ; commandes validées et contrôles de portée |
| Dialogue | PASS | A012 et P02 public ; réplique retouchée et conservée |
| Quête | PASS | A012 et agrégat ; objectif, loot et conséquence persistante |
| Ennemi | PASS | A012, D012, E012 ; état de l’acteur et réactions causales |
| Combat simple | PASS | A012 et contrats Native013 ; contacts et conséquences de gameplay |
| Sauvegarde/reprise | PASS | B012, P02/P04 publics et parcours installé013 |

| R3-B — monde comme arme | Verdict | Preuves déterminantes |
|---|---|---|
| Objet poussable | PASS | D012, observation requise acquise |
| Objet lançable | PASS | D012, observation requise acquise |
| Dégâts d’impact | PASS | D012, observation requise acquise |
| Chute ou piège | PASS | D012, observation requise acquise |
| Feu | PASS | D012 et E012 |
| Eau/extinction | PASS | D012, observation requise acquise |
| Objet destructible | PASS | D012, observation requise acquise |
| Réaction IA au danger | PASS | D012/E012 ; lien feu → danger → retraite et ACK ≤ 1 000 ms |

| Parcours Studio/jeu | Verdict | Preuves déterminantes |
|---|---|---|
| R3-P01 — trois lieux, deux approches, conséquence persistante | PASS | A–E012 et agrégat `p01_two_approaches=true` |
| R3-P02 — retoucher réplique/porte, reprendre sans perte | PASS | Deux retouches publiques Studio, Save/export, reprise du Save A exact, état préservé et physique revalidée |
| R3-P03 — diagnostiquer et corriger un objectif invalide | PASS | Sceau sélectionné, refus visible de X2048 hors terrain, correction X18.05, preview/apply/save |
| R3-P04 — état de test choisi et isolation des documents | PASS | B012 pour l’état technique exact ; trois sessions publiques fresh/reprise/fresh, demande stricte consommée et bilans Studio lisibles |
| R3-P05 — revue du jeu séparé et origine Studio | ACCEPTÉ PROPRIÉTAIRE | [Acceptation explicite du 13 octobre](GATE3_OWNER_RPG_ACCEPTANCE_20261013.md), conservée par la décision de qualifier la version commune actuelle |

### Références des acquisitions

| Acquisition | Résultat conservé | SHA-256 du reçu |
|---|---|---|
| [A012](../../../../.gate3-evidence/20261013/closure-content-guard-A-015012/assertions.json) | boucle complète, PID29312 | `a72d594ff783bbccc0614db557abcb695261c035290350cbe995143e86d5f38e` |
| [B012](../../../../.gate3-evidence/20261013/closure-content-guard-B-015012/assertions.json) | reprise exacte, PID21952 | `ee0e5afcb3b42b73d875808c45106e6a269273c7643b871f3466016873103611` |
| [C012](../../../../.gate3-evidence/20261013/closure-content-guard-C-015012/assertions.json) | retouche SDK compatible, PID35176 | `b0cdaf819cdddfdbeaa292630ca24f16b885158587a5ab428a66569752dc45c6` |
| [D012](../../../../.gate3-evidence/20261013/closure-content-guard-D-015012/assertions.json) | huit capacités, PID33412 puis22336 | `df2bc234a3d8db342bd23370c37b27fa2b39bdb50f5b0e3f963f2853dcacd6b9` |
| [E012](../../../../.gate3-evidence/20261013/closure-content-guard-E-015012/assertions.json) | feu/danger/retraite, PID3820 | `fe09d5ac569cda27f639035a0938e239c12b84aa852795b45605d53b84cb696d` |
| [Agrégat012](../../../../.gate3-evidence/20261013/closure-content-guard-aggregate-015012/assertions.json) | deux approches acquises | `7aff0029424aff005382a415104429a17192263814d5b84f58295f3f164cc49a` |
| [P02 public](../../../../.gate3-evidence/20261015/closure-public-p02-provenance-012.json) | deux retouches visibles, Studio12344 | `df2d9373729fc6df53266d0416e91249d3f6e3169b692f1f54cc794f2b4c61f3` |
| [Reprise P02](../../../../.gate3-evidence/20261015/closure-public-p02-runtime-012-001/result.json) | PID21512 fermé code0 | `775e846240c1ab7ab826f9f50eaf4901a977402f453526f1ca9aac0eb3a6d122` |
| [P03 public](../../../../.gate3-evidence/20261015/closure-public-p03-ui-012/provenance.json) | refus/correction visibles | `418a1e24e33f95e6d6de08972f133de6f5ab054a610c73afe2ae04aaeabe1663` |
| [P04 public](../../../../.gate3-evidence/20261015/closure-public-p04-ui-012/local-result.json) | trois sessions et bilans visibles | `6c8c34746f7206a4c44a4e5771eb7e5693bade09eccab625a1d95e90d76ea71a` |

La généricité RPG est vérifiée sur des modules/aires/identifiants indépendants,
notamment `navigation-product/*`, `gameplay-danger-motion/*` et
`gameplay-physics/*`. Le domaine, les commandes et la persistance ne sont pas
définis par le slug de Tidewater. Cette preuve ne clôture pas le chantier
global de renommage des modules moteur `tidewater_*` ou de parité graphique.

## Contrôles techniques et applications installées

| Contrôle | Résultat réel | SHA-256 du reçu |
|---|---|---|
| [Native013](../../../../.gate3-evidence/20261015/closure-common-013/native-result.json) | format/diff/Clippy et **2 201 tests PASS**, 56 ignorés, 232 suites | `794f43438cd1f2f62bc6ad769e5a94131319635064ea281bfd2300dbda48aa7b` |
| [Supply013](../../../../.gate3-evidence/20261015/closure-common-013/supply-result.json) | deny/audit code0 ; 0 vulnérabilité, 10 avertissements, réserve GLib conservée | `ef0b5d53ceb9b330856b7188c62cb777a323dbd29f7afcad641fce0836f9966a` |
| [WASM013](../../../../.gate3-evidence/20261015/closure-common-013/wasm-result.json) | engine WebGPU et bundle Studio code0 | `36938c6022673d33572b5833c05d54c9601c2024b325cf56a68cc6930f1b9296` |
| [Release013](../../../../.gate3-evidence/20261015/closure-common-013/release-result.json) | Runtime, Game et Studio code0 | `1bcf86f3af67d974f3d37e4c12a846dfbce4168e3272acb14dd9fd8ed45b64bf` |
| [Studio installé013](../../../../.gate3-evidence/20261015/closure-installed-studio-ui-013/relaunch-002/result.json) | Studio25832 et trois jeux14108→4160→19448, quatre sorties0 sans arrêt forcé ; 350 fichiers utilisateur préservés | `3266bce04fb771abe93b75482da27dc32933c1d5b6b3a287cc5a23c94e53fa5c` |
| [Audit UI installé](../../../../.gate3-evidence/20261015/closure-installed-studio-ui-013-audit-001/audit.json) | 12 captures et 12 états Sky ; fresh → F5/F9 → reprise → fresh | `d3dae63b5eebf0ba4f0963387ea68ac2b68310c84eb2307200ed4f9db42f892a` |

Les 34 replays privés012, 65 contrats headless et 13 contrats supplémentaires,
les identités SDK et le smoke navigateur sont détaillés avec leurs limites
dans le [journal de qualification](GATE3_CLOSURE_READINESS_20261014.md).
Les gardes, rapports et captures ont été recontrôlés lors de l’audit final ;
la préparation d’un collecteur ou un simple build n’est pas compté comme
une nouvelle exécution runtime.

Les EXE Release013 vérifiés sont : Game
`1d4858213e74e64ead31332a65a633e7baa1c951645616137de04b3733271d27`,
Runtime `a2f53b22c6a5747bfbd3b95f82faa5af26302ce9efe553cbea17c53ff4e081c4`
et Studio `22347277bc6f999adfd11d85e93211d84d1d08f5c2527d004a2ed22c53683fce`.

L’installateur principal reste `E:\OpenRPG_Forge\OpenRPG-Forge-Setup.exe`,
version `0.1.0-preview.20261015.013`, 1 170 618 151 octets, SHA
`0eca18e3e632ca108b841feb2d540f1ee9bded5fa01dc6cd1686561b6ea17ad0`.
Les deux packs séparés assets/module et leurs manifestes sont dans
`E:\OpenRPG_Forge\Forge-Contenus-20261015`. Leurs octets vérifiés sont conservés.

## Limites conservées et suite

La [CI du commit](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38762140900)
reste **NOT RUN — infrastructure billing** : quatre jobs sans étape exécutée.
Elle n’est pas verte et ne fournit aucune validation Linux ou distante.
Son attente n’est plus un critère de cette clôture, par décision du propriétaire.

La réserve GLib `RUSTSEC-2024-0429` reste un blocage de qualification de
distribution ; aucune acceptation de risque ou dérogation Windows n’est ajoutée.
Windows vierge, installation initiale WebView2, registre/raccourcis et release
multiplateforme ne sont pas qualifiés par ce bilan. Le parcours installé
vérifie lancement/sauvegarde/reprise ; la preuve de boucle RPG complète reste
dans les campagnes et l’acceptation propriétaire qui la couvrent.

Les mesures caméra012 sont conservées dans leur périmètre déclaré. Aucun
benchmark général, p95 GPU, performance de toutes les campagnes ou nouvelle
acceptation visuelle humaine n’est déduit de ces captures.

La prochaine gate est **Gate 4 — assets gouvernés et `.orpmod` v2**. Ses critères
ne sont pas cochés par cette clôture. Les documents de qualification gardent
les résultats bruts et les réserves historiques ; seul le présent verdict
consolidé porte la clôture Gate 3.
