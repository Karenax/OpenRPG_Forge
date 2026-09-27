# R1-040 — Choix global initial, budgets encore partiels

2026-09-06, branche Rust, arbre hérité conservé, aucune publication. Gate 1 et R1-040 ouverts.

## Implémenté

Runtime accepte `--quality low|medium|high|ultra` dans les deux modes vallée. Le choix renseigne
les sept adapters existants : terrain, végétation, ombres, environnement, eau, pluie, post-traitement.
Aucune seconde implémentation des effets. Mélanges avec options par domaine, doublons et cycles
mono-domaine refusés quel que soit l'ordre. Omission conserve le comportement antérieur.
Test des quatre niveaux/deux modes, sept valeurs et entrées invalides exécuté.

L'événement structuré `global_quality_requested_partial` rapporte une demande de sept domaines,
**full_preset_applied=false**. Les observations par domaine restent la preuve réelle, pas ce log
de demande. Pas d'acquittement du contrôleur adaptatif, ni de changement global à chaud ou Studio.
La pluie réutilise ses budgets existants par niveau ; cette option ne charge pas une politique
personnalisée et ne prétend pas supporter un budget pluie arbitraire.

## Vérification native conjointe

Deux processus séquentiels, même binaire, sans compilation concurrente :
`9f497dff03b9deea502dbe09597dd5aef458b5a0e665cef145e0fd661a6c3161`.
RX 7800 XT/Vulkan, 1920×1080, WorldSketch rev3, kit gouverné et aperçu human_bridge-clear
du lot végétation initial, météo **rain**. Commande complémentaire :

```text
--quality low|ultra --capture <niveau>.png --capture-frame 1
--metrics <niveau>.json --metrics-warmup-frames 600 --exit-after-frames 1020
```

Les deux sorties valent 0, runtime_errors_observed=false, zéro ERROR JSON.
Les deux PNG sont inspectés : densité, pluie et ombres diffèrent ; terrain, pont et eau restent
visibles. Eau plus simple en Low ; tramage et style procédural toujours visibles. Pas de jugement
d'équivalence artistique Cadle ni d'acceptation humaine. Readbacks Low 160/164, Ultra 118/121 ;
85/92 pipelines prêts, zéro pending/failed, huit observations stables.

| Domaine | Low observé | Ultra observé |
| --- | --- | --- |
| Terrain | projected=low, 192 meshes dérivés | projected=ultra |
| Végétation | 4043 placements, 2547 entités | 9110 placements, 4212 entités |
| Ombres | 1024, 2 cascades, contact off | 4096, 4 cascades, contact on |
| Environnement | 64², texture raccordée | 512², texture raccordée |
| Eau | SSR retiré, budget projeté | SSR 64 étapes, budget projeté |
| Pluie | 2048 gouttes | 16384 gouttes |
| Post-traitement | bloom off, projected=low | bloom on, projected=ultra |

Politique liée rev3 commune aux six relevés qui la référencent :
`6ae47b0ce3d513683394749b625ac2a3dcbdfcd94173a6f84f4e3070f6c94871`.
Les relevés eau/environnement restent partiels : volumétrie, distance SSR, réfraction écran et
qualification des réflexions environnement ne deviennent pas disponibles par ce regroupement.

Smokes de 420 samples : Low 143,98 FPS moyen / p95 7,4757 ms / 1% low 127,58 FPS ;
Ultra 90,84 / 11,8819 ms / 75,67 FPS. Aucun hitch >16,7 ms dans ces samples.
Durées 2,92/4,62 s : pas un benchmark de qualification ni une mesure des transitions.

## Validation et pins

**379 tests workspace PASS**, Clippy workspace all-targets/all-features avec warnings en erreurs
PASS, format/diff PASS. Aucun code moteur changé : check WASM du lot moteur précédent applicable.
CI distante NOT RUN. Aucun asset ajouté et aucune dépendance nouvelle.

Sorties locales : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-global-quality-20260906`.
SHA-256 :

- low.json : `a2eedab9ef2bac2ca92a52e56945ab4e173b3e34f3b56e4e05a7ecaba5debe07` ;
- low.log : `4d3da3e8c4c0a8b3d46c5143b344500106805059576bbcea7b7f930f99ecf8dc` ;
- low.png : `d8e6e43f4d37d3ac89fc74b0fd691d8197115ae94f3cccb7dc17e54afe3c9aba` ;
- ultra.json : `062963a059e7a95ea7e540ccd05ca3df008c1cfa63522f7940d070c19521422a` ;
- ultra.log : `dde7dd031b950166bd504a5b793d1a72e7d9ddfa9fad4830d8d947cabf6e45d3` ;
- ultra.png : `57554c96754c91ac4585f6c8f9f6b9d54926946236a3295a1e86045e943210df`.

Suite : raccord global à chaud avec états demandés/projetés honnêtes, contrôleur et UI Studio ;
les fallbacks non qualifiés ne doivent jamais produire un acquittement complet.
