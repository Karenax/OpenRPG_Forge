# R1-040 — réutilisation des maillages de berge protégés

11 septembre 2026. **Code et tests validés ; cycle fonctionnel acquis, gain de fluidité non démontré.**

Un changement de profil reconstruisait tous les chunks terrain, y compris ceux
protégés par une berge. `prepare_lod_payloads` produit pour ces derniers trois
copies de la topologie canonique pleine résolution, sans morph target : cette
géométrie ne dépend pas du niveau demandé. À l'inverse, les LOD ordinaires
possèdent des morph targets dépendant de la topologie suivante et restent reconstruits.

Le correctif réutilise le payload seulement si le chunk est toujours protégé
et si son hash canonique est inchangé. Première projection, retouche de géométrie,
arrivée ou retrait de protection conservent la reconstruction. La publication
actualise aussi les plages de visibilité, les transforms/bornes et les bandes
de morph des bindings retenus ; ne pas abandonner la mise à jour au seul motif
qu'aucun nouveau payload n'est nécessaire. Aucun cache supplémentaire, asset,
document de carte, réglage graphique ou dépendance ajouté.

## Vérifications exécutées

- 18 tests terrain ciblés PASS après le premier correctif, 26,93 s hors compilation.
- Contrôle supplémentaire d'invalidation par retouche, arrivée/retrait de berge
  et première projection ajouté ensuite.
- 711 tests workspace/all-targets/all-features PASS, deux sondes ignorées.
- Clippy workspace/all-targets/all-features strict PASS, 37,91 s.
- Clippy moteur Web/WASM strict PASS, 31,07 s ; fmt et diff-check PASS.
- Le cycle synthétique Low/Ultra/Medium reconstruit 63 chunks sur 64 avec un
  chunk protégé, conserve ses trois meshes pleins sans morph, suit exactement
  les nouvelles plages et garde les handles ; retirer la protection reconstruit
  le chunk. Ce comptage structurel n'est pas une mesure GPU.

Première invocation du test refusée à la compilation pour un nom de helper
incorrect (`lod_ranges` au lieu de `ranges`) ; corrigé avant les succès ci-dessus.
Aucun résultat rouge transformé en PASS. Commandes offline/locked, cache et
temporaires sur E:, deux jobs maximum. Scripts et logs persistants :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-bank-reuse-20260911/`.

## Build et essai interrompu

Build frontend release PASS, 2 min 54 s, puis shell Studio produit. Avertissement
future-incompatibilité proc-macro-error2 2.0.1 conservé dans build-web.log, pas
présenté comme corrigé. Les 283 fichiers du snapshot ont été revérifiés identiques
avant l'actualisation documentaire ci-présente. Seul le déplacement d'un paragraphe
du README moteur diffère ensuite ; les pins historiques ne sont pas réécrits.

| Artefact | SHA-256 |
| --- | --- |
| Studio EXE | `aae61b18688531cc12c0fa67e3be13a43e935c49f66d2dd5250fe383a1b6f455` |
| WASM | `b8370fa0f6e7c3db7b7220e8829b3ac185c1c835a3653ad38a89257fd5e2756b` |
| workspace-tests.log | `f0c57ded11a1281c7ae57c6e0615cc298d927583a5c0e4d141f0fcb46fac873e` |
| cleanup.json | `acc50417560befd5bae43c7350a3ecdc5cfec5e99f871d9f05bb4210a6de5841` |

Le Studio a été lancé : interface observée Ready 5/5, cycle High/Low/High PASS
en déplacement, parcours completed, sol 518/518 ticks, 434 contacts, 42,530 m.
La vue forestière était affichée. **Observation d'interface seulement** : aucun
diagnostic JSON ni PNG n'a été exporté pour cette session. L'utilisateur a
interrompu Computer Use par Échap avant l'export ; aucun nouveau contrôle UI
n'a été tenté ensuite. Arrêt du seul Studio possédé, sortie forcée -1, puis
neuf processus suivis fermés et zéro survivant confirmé : 16992, 19668, 21792,
30460, 6692, 10768, 29612, 31960, 17612. Ce n'est pas une sortie normale qualifiée.

`Compare.py` est préparé mais **non exécuté**, ses entrées studio-motion.json/png
étant absentes. La comparaison de timings reste à acquérir après reprise des
essais graphiques par le propriétaire ; ne pas relancer automatiquement l'UI
pour contourner l'interruption. Aucun gain de fluidité ou de FPS attesté. Les
anciens reçus Medium/Ultra restent des preuves des anciens binaires, pas du
nouveau moteur. Aucun nouveau verdict de Gate 1, aucune dérogation au tramage
déduite de la question en attente, aucune publication Git.

## Reprise autorisée — diagnostic acquis

Après « reprend », acquisition du même binaire dans `resume-2/`, sans rebuild.
La tentative `resume-1/` ne possède que deux logs vides et son script : ancien
handle d'exécution inconnu et PID 29392 absent à la reprise ; aucun nettoyage
historique inventé. Ces traces sont préservées.

`resume-2/studio-motion.json` exporté depuis le Studio, SHA-256
`0c45e8555f7f85d6db006740f08968ed510b55b25d5f4d5160611f059eadb3ff`.
Cycle High/Low/High PASS, reçus GPU prêts et identités cohérentes ; plans,
sources, substitutions et contraintes identiques à la référence préparation
bornée. Préchauffage 5/5 en 56,8178 s, 134 pipelines. Parcours observé terminé,
518/518 ticks au sol, 434 contacts, 42,530 m.

| Diagnostic | Référence | Reprise |
| --- | ---: | ---: |
| Pic frame passage Low | 147,2 ms | 143,5 ms |
| Pic frame retour High | 271,0 ms | 264,5 ms |
| Retour High GPU prêt depuis début cycle | 8,4438 s | 8,2253 s |
| Appel préparation terrain maximal, retour High | 17,2 ms | 14,1 ms |
| Appel commit terrain maximal, retour High | 2,3 ms | 1,6 ms |

Une paire diagnostique, pas un benchmark contrôlé ni une preuve causale :
écarts modestes, activité externe non isolée, préparation végétale maximale
24,3 → 27,4 ms. Aucun gain sensible de fluidité attesté et aucun PASS FPS.
Ne pas prolonger cette piste par un nouveau cache ou une campagne identique.

Vue forestière inspectée dans l'interface ; tramage encore visible sur certains
troncs. PNG non exporté : fenêtre déplacée puis réduite pendant les actions,
erreurs de coordonnées et d'état conservées comme limites. Fermeture forcée
du seul processus racine possédé, sortie -1 ; quatorze PIDs suivis fermés,
zéro survivant dans `resume-2/cleanup.json` (SHA-256
`02a2116711fc851dde050021919f3d6cbd41207678066e7b15f6642dec1cbd32`).
Pas de sortie normale ni de capture finale qualifiée déduite.

`Compare.py` exécuté avec succès, résultat `resume-2/comparison.json` :
comparaison des reçus et pins binaires stricte ; unique dérive documentaire
README explicitée avec ses deux hashes, snapshot historique conservé. Première
exécution rejetée pour une faute de transcription du hash README, corrigée
depuis le hash réellement relu ; aucun contrôle de production assoupli.
Gate 1 demeure ouverte, notamment fluidité, revues humaines kit/provenance et
dossier final sur révision exacte. Aucun nouveau changement moteur dans cette reprise.
