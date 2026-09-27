# Gate 1 — clôture locale du 12 septembre 2026

**PASS AUTOMATISÉ LOCAL SOUS ADR-0006, AVEC DÉROGATIONS ACCEPTÉES. Gate 1 clôturée.**
Cette tâche s'arrête à la clôture ; elle ne réalise pas Gate 2 et ne publie rien.

## Révision qualifiée et isolation

- Commit : `7386e00a1d9e8531470b57bcd8da190636a9e390` ; arbre : `f73099c6372f0e10f4cd759acc6e0082aada2367` ; branche locale `Rust`.
- Copie propre : `E:/OpenRPG_Forge/.gate1-closure-20260912` ; état Git vide lors de la qualification finale.
- Le commit documentaire de clôture est un descendant qui ne change ni code, ni données,
  ni politiques qualifiées. Son identité finale est dans le reçu externe `final-revision.json`.
- Workspace original conservé, basé sur `f786a38aca2023f08d708687f99a47d14f34e6d4`.
  Divergence initiale avec son cache `origin/Rust` : `0 / 103`, sans fetch distant.
  La copie de preuve a pour origin le dépôt local ; ce n'est pas une mesure du serveur distant.
- 495 fichiers sélectionnés au gel initial ; 826 fichiers non liés/temporaires exclus.
  Manuel, bestiaire et travail Gate 2 parallèle ne sont pas intégrés implicitement.
  Aucun reset, stash, merge, commit global du workspace original ou push.

## Décisions et critères soldés

| Point | Verdict final |
|---|---|
| Profils forêt/eau et adaptation | Capacité fonctionnelle acquise sur les quatre niveaux ; preuves antérieures conservées. |
| Saccades pendant les changements de qualité | WAIVED / ACCEPTED par le propriétaire, ADR-0013 ; aucune mesure de transition exigée. |
| Performance | Dérogation du point 2 maintenue ; aucun PASS GPU cible ou 60 FPS constant inventé. |
| Mouvement / LOD | Parcours natif et Studio vérifiés ; fondu stabilisé caméra arrêtée. Pas de garantie globale de fluidité déduite. |
| Natif / Studio | Builds et lancements réels de la révision qualifiée ; parité fonctionnelle antérieure des profils conservée. |
| Mesures / préchauffage | Preuves natives et préchauffage acquis ; mesures GPU Studio dérogées sous ADR-0012. |
| Kit et retouche humaine | WAIVED / DEFERRED sous ADR-0013, sans retouche fictive ni droit de distribution nouveau. |
| Comparatif Godot / Cadle | ACCEPTED BY OWNER le 12 septembre, ADR-0013 ; conditions de cadrage et métriques inchangées. |
| Révision propre et dossier | PASS local, commit/arbre exacts, commandes et artefacts épinglés. |

Les anciennes attentes de validation dans les comptes rendus historiques sont remplacées
par ce tableau ; elles ne sont pas des travaux à recommencer. Le kit sera réexaminé avant
sa promotion ou la qualification du parcours de retouche concerné.

## Vérifications exécutées

- Formatage et Clippy workspace/all-targets/all-features strict : PASS.
- Tests workspace/all-targets : **738 PASS, 0 échec, 2 ignorés préexistants**.
  Les deux ignorés sont les sondes explicites de timing CPU `quality_preparation_cpu_probe`
  et `quality_commit_cpu_probe` ; aucun test ignoré ajouté pour cette clôture.
- Check moteur WASM et Clippy frontend WASM strict : PASS.
- Build runtime natif Windows : PASS ; Gate 0 avec redimensionnement, capture et sortie 0.
  Traversée Gate 1 avec forêt/kit/eau/nuages : PASS ; 291 images Full HD,
  fondu observé stabilisé à 0,200 s. Capture native inspectée. Vidéo à horloge imposée,
  **pas un benchmark temps réel** ; aucune campagne de saccades au changement de qualité.
- Build WASM release de preuve (wasm-opt désactivé comme les candidats précédents),
  paquet Studio Tauri debug : PASS. Parcours Studio temporel PASS, capture inspectée.
- Régression Gate 0 dans la vraie WebView Tauri : WebGPU ready, un démarrage viewport,
  interaction de hiérarchie avec canvas stable, commande typée autorisée et commande
  `plugin:window|set_title` explicitement refusée. Ledger validé contre le schéma.
- `cargo deny check licenses bans sources advisories` : PASS.
  RustSec : zéro vulnérabilité signalée, dix avertissements exacts, un blocage de
  distribution glib conservé. Voir `R1_RUSTSEC_REFRESH_20260912.md`.
- Self-test du protocole Gate 0 et tests des collecteurs vidéo, dossier final, parité,
  mesures, caméras, captures et inventaire textuel : PASS. Les fixtures graphiques
  historiques testent leurs vérificateurs ; elles ne deviennent pas des acquisitions nouvelles.
- Sources de la preuve indépendantes du clone Cadle ; 280 sources de l'ancien inventaire
  reliées au candidat, huit sorties générées exclues des sources canoniques.
- Tous les processus possédés des essais Studio sont arrêtés ; aucun survivant dans les reçus.

Les binaires natifs proviennent de `9aa22011721fe80e065924de3dd710e765c624c2`.
Leur fermeture de dépendances (crates, runtime, données, Cargo et toolchain) est inchangée
au commit qualifié ; seules politique documentaire/supply-chain et télémétrie Studio ont
évolué ensuite. Le Studio final est reconstruit après ces corrections. Aucun ancien binaire
n'est relabellisé comme recompilé ; les liens d'entrées sont contrôlés séparément.

## Échecs diagnostiqués et corrigés

Deux lints WASM corrigés : référence directe de méthode et balisage d'un nom de type.
Le replay Gate 0 a ensuite révélé que le refus ACL réel était classé `probe_failed` :
le parseur reconnaît désormais le message complet observé, en plus de l'ancien format.
Un test positif et sept altérations négatives protègent ce cas ; aucune permission changée.
La politique RustSec retire dix avis officiellement retirés, sans nouvelle exemption.
Une invocation native sans le mode borné exigé et une invocation de vérificateur sans
sa fixture ont été refusées ; commandes corrigées et replays PASS, premiers journaux conservés.

## Exception CI distante ADR-0006

Origine : [run 33335683606](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33335683606),
commit `7286740c18fe9afa6cc207e388d5f335495fc650` ; confirmation historique : run 33339077298.
Les quatre jobs Windows, Linux, WASM et supply-chain avaient une conclusion d'interface
`failure`, zéro étape exécutée (`steps = []`), refus avant runner pour facturation/plafond.
La CI distante n'a **pas testé le commit de cette Gate 1**. Aucun run distant nouveau lancé.

| Environnement | État |
|---|---|
| Tests locaux Windows automatisés | PASS |
| CI distante Windows | NOT RUN — infrastructure billing |
| CI distante Linux | NOT RUN — infrastructure billing |
| CI distante WASM | NOT RUN — infrastructure billing |
| CI distante supply-chain | NOT RUN — infrastructure billing |

Le risque Linux et celui des runners WASM/distants restent ouverts. L'exception expire
selon ADR-0006 au retour des runners, à changement pertinent de workflow, après Gate 4
ou avant distribution. Les dérogations de Gate 1 ne se prolongent pas tacitement.

## Artefacts et limites

Ledger versionné : [GATE1_QUALIFICATION_20260912.json](GATE1_QUALIFICATION_20260912.json).
Journaux, programmes de replay, hashes et captures restent dans les deux racines externes
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-clean-closure-20260912` et `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-clean-closure-studio2-20260912`. Les références Cadle restent lues sur place,
sans copie de médias dans Git. Runtime dev optimisé et Studio debug : aucune distribution
ou qualification de performances release revendiquée. Le produit complet reste non qualifié.
