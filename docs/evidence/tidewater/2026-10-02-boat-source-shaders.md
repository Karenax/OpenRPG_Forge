# Tidewater — acquisition des shaders source du bateau

Suite de la [projection Engine](2026-10-02-boat-engine-instance.md).
Le raccordement au rendu Rust et la parité visuelle restent ouverts.
Suite : [lecteur Rust et packing source vérifiés](2026-10-02-boat-shader-loader.md).

## Résultat source

La scène upstream au commit `1438b1abfcaee3267092b75573014f4d9b4a983c`
est initialisée par son véritable `App.init()`, avec ses ressources et les huit
hooks réels de `SceneLighting`. La boucle d'animation `App.start()` n'est pas
lancée. Les API GPU, les hooks et les fichiers upstream ne sont pas remplacés.

La capture conserve sept matériaux et vingt variantes WGSL : passes main,
couleur et profondeur des six matériaux opaques, puis main late et couleur du
verre. Le verre ne projette pas d'ombre. Elle inclut les layouts de vertex,
les bindings, les modules, les uniformes et les politiques de rendu.

Les vingt modules sont soumis au vrai WebGPU du navigateur (adaptateur AMD,
architecture RDNA-3), sans message de compilation ni erreur dans les scopes
observées. La readiness des pipelines async n'est pas attestée : voir l'erratum
ci-dessous. Les vingt empreintes WGSL concordent. Les 344 fichiers épinglés
d'entrée restent identiques après acquisition ; le Git upstream reste propre.

Preuve locale :
`F:/OpenRPG_Forge/.build/boat-shader-acquisition-344/capture.json`,
1 957 057 octets, SHA-256
`d5924cb51c76fd3323c14e81cce3d448d6ea0dd3bd4bf3e70a4096d2765458a9`.
Le reçu adjacent épingle le helper, la sonde et les entrées source.

## Limites et suite

Cette capture conserve la composition et les diagnostics des modules source, sans dessin de
comparaison ni acquisition des contenus des textures et buffers GPU. Les valeurs
d'uniformes sont celles de la scène initialisée ; elles ne remplacent pas les
valeurs canoniques du paquet géométrique 319. Le paquet de provenance 340 et ses
anciens verdicts restent inchangés.

Le paquet shader 350 et son lecteur Rust passent maintenant les contrôles CPU
et l'ABI source décrits dans la suite. Il reste à
raccorder ses ressources réelles, puis tester le bateau dans la scène jouable et
Studio. Ce résultat ne qualifie ni le rendu Rust, ni l'édition, ni la fidélité
visuelle, ni une distribution Windows. L'objectif complet reste actif.

## Erratum du raccordement Bevy

La sonde 344 choisissait `late` selon la transparence du matériau. Or `App.js`
place aussi les pièges opaques dans la couche TRANSPARENT : la vraie variante
main est `boatTrap/main/late=true`. Le paquet 350 conserve la variante early
demandée par l'ancienne sonde ; il n'est pas l'inventaire complet de l'App.
Ses métadonnées ne conservaient pas non plus les biais 2/1,5 de `Shadows.render()`.

`GPU.pipelinesReady()` attend les promesses mais absorbe leurs rejets ; le reçu
344 ne vérifiait pas `handle.failed` ni `handle.pipeline`. L'absence d'erreur
observée n'est donc pas une preuve suffisante de readiness de ces pipelines.
La sonde v2 observe maintenant le descriptor SDK avant readiness, vérifie les
handles et ferme sa scope dans `finally`. L'empaqueteur v2 exige ces contrôles.
Cette nouvelle acquisition n'a pas été exécutée : aucun navigateur n'est
disponible dans les outils de la session. Le helper 352 a été fermé et son
reçu `UI-UNAVAILABLE.json` est conservé ; aucun nouveau shader source n'est inventé.

Une preuve native indépendante crée les vingt programmes existants sur le
RenderDevice Bevy avec politiques explicitement dérivées du SDK. Son
[rapport et ses limites](2026-10-02-boat-bevy-pipelines.md) distinguent cette
preuve de création GPU de l'inventaire source, du dessin et de la parité.
