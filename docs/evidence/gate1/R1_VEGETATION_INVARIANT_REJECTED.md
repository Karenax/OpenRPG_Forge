# Gate 1 — Essai de sortie végétale invariante rejeté

2026-09-05. Branche `Rust`, travail local non publié. Aucun correctif de rendu retenu.

## Constat et hypothèse

La vallée embarquée présente des pointillés sur les arbres et l'herbe, observés dans Chrome
puis reproduits en natif avec `--visual-valley --player-camera`.
Le shader végétal Forge utilise le même calcul de position pour ses passes, mais importe
des sorties Bevy dépourvues de `@invariant`. Une différence de précision entre variantes
compilées était une hypothèse à tester, PAS une cause démontrée.

Inspection Bevy 0.19.1 : le pipeline de meshes et le prepass emploient `GreaterEqual` ;
`Equal` apparaît notamment dans le pipeline de lighting deferred. Le warning `Equal`
ne permet donc pas d'attribuer directement les pointillés au shader végétal forward.
Le dither de visibilité existe dans les deux passes ; l'alpha des poids de vent n'entraîne
pas un alpha-discard pour le matériau végétal opaque. Aucun diagnostic exclusif établi.

## Essai et refus

Une sortie de sommet propre à Forge a été essayée, avec position `@invariant` et mêmes
locations forward/prepass conditionnelles. Aucun code Bevy modifié, aucune comparaison
de profondeur assouplie, aucun dither désactivé, aucun placement ou mesh changé.

`cargo build --workspace --locked -q` réussit, mais le run natif du 13:30:24 UTC produit :

```text
ERROR failed to process shader error:
invalid field accessor `visibility_range_dither`
embedded://forge_engine/shaders/visual_valley_vegetation_deform.wgsl:290:9
```

Le processus sort néanmoins avec le code 0 et écrit une capture : cette capture est INVALIDE
pour une comparaison artistique ou une qualification de performance. L'essai ne prouve
ni ne réfute l'hypothèse de précision ; il échoue avant de fournir une variante rendue valide.

## Restauration vérifiée

La modification expérimentale a été retirée par patch. `git diff --exit-code` sur le shader
est vide ; le fichier correspond exactement à son état antérieur (il n'avait pas de diff).
Les autres changements du workspace sont conservés. Nouveau build workspace : PASS.
Run restauré du 13:32:29 UTC : code 0 et aucun enregistrement JSON de niveau ERROR.
Capture restaurée inspectée : scène présente, défaut de pointillés toujours visible.
Warnings Vulkan AMD et invariant deferred persistants, non masqués.

Dossier externe :
`E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-vegetation-invariant-20260905`.
Commandes : même exécutable target que les preuves précédentes ; options
`--visual-valley --player-camera --capture <nom>.png --capture-frame 300
--metrics <nom>-metrics.json --metrics-warmup-frames 180 --exit-after-frames 420`.

- `before.png` : `9555adff9aa77550e37e1b23e5e6c3432ebc58dca8932dceede603c9c2a123aa` ;
- `after.png`, essai REJETÉ : `738d2ae6dc697548a760f734026523544d2db0a3235723b69e545622f34fc426` ;
- `restored.png` : `4b427cba8feb0ffc19b5600b74fcdcf388096365bcee9d839945610b60e187be` ;
- `restored-run.jsonl` : logs du replay restauré, vérifiés par parsing JSON et refus d'un
  niveau ERROR, pas seulement par le code de sortie.

Les métriques before/after/restored restent locales ; aucun gain revendiqué. Les tests
précédents restent applicables au code restauré ; seul build + run affectés ont été rejoués.

## Conséquence pour la qualification

Un code processus 0 ne suffit pas : toute preuve de shader doit aussi vérifier les logs et
inspecter la capture. Ne pas promouvoir une image où des pipelines ont échoué.
Ne pas reprendre cet essai comme correctif acquis. La prochaine investigation doit isoler
le dither et les passes actives avant toute nouvelle modification du contrat de sortie.
Gate 1 reste ouverte ; aucun fork Bevy, commit, push ou changement artistique conservé.
