# R1-022/023 — courant local et extinction du candidat

2026-09-08, branche Rust, suite de `R1_WATER_DEPTH_PREVIEW.md`.
**Candidat explicite uniquement, non promu ; Gate 1 ouverte.**

## Règles implémentées

- Les canaux G/B existants des sommets d'eau portent la direction XZ locale du
  tracé, déduite de l'orientation de chaque section. R conserve la profondeur,
  A reste 1. Aucun nouveau flux de sommets, texture, asset ou placement authoré.
  Les tests comparent ces directions aux tangentes échantillonnées de la rivière.
- Détail normal original à deux échelles, issu d'un bruit de valeur avec gradient
  analytique, entraîné par cette direction locale. Deux phases d'advection avec
  fondu cosinus rendent le poids et sa dérivée nuls lors des remises à zéro.
  Filtrage selon l'empreinte du pixel ; aucun coût équivalent revendiqué.
- Dans le candidat transmissif seulement, déplacement géométrique ramené à
  15 % de l'amplitude authorée pour subordonner les crêtes au détail de surface.
  Le témoin différé garde ses sinusoïdes et sa configuration antérieures.
- Correction sémantique : une couleur profonde d'apparence n'est pas un
  coefficient de transmittance. Le candidat utilise désormais `1 - opacity`
  comme transmittance neutre à la profondeur de référence ; la palette reste
  distincte. Cette convention expérimentale n'est pas un nouveau format source.

La géométrie statique, les collisions, le nombre de sommets, le temps déterministe,
les sources de carte et les retouches restent indépendants de ces règles de rendu.
SSR reste désactivé dans le candidat ; reflets d'environnement, TAA et limites
de profils décrits dans le rapport précédent restent applicables.

## Vérification

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-flow-preview-20260908/` :
`Verify.ps1`, logs fmt/tests/Clippy/check WASM/build, `Capture.ps1`.
**640 tests PASS / zéro échec / un ignoré**, fmt, Clippy workspace strict,
check WASM et build natif PASS. Validation Naga des fonctions WGSL et vérification
des tangentes incluses. Warning préexistant `proc-macro-error2` conservé.

Shader principal : `C13048B9CEEE749B9BF353FFEA70957A9DC4FCB5CABD834C36550D1AFE59DE1B`.
Shader de détail : `A2269BF49A940773FBF75ED61D2ED1BB5E5F40F015317BE6D00AB4569281D6F2`.
Binaire : `46DA2C6D2E990DCB880E3A7FA71AF78467BCD3961F472B2701037AF1EA241FF8`.

Deux captures natives figées du binaire exact, kit/forêt conservés :

- `hero_reference_match.png`, SHA256
  `7F57A70BCF11E39A97C1A11D6C2AE19D2CCC0D17AA520FC2DBEAF74B2AC30A43` ;
- `human_bridge.png`, SHA256
  `4FEF5450C684DB1268760D4C3596FD0898DCB143E714BEE221D9A2E93FA9D121`.

PIDs 32608 et 29224 terminés, exit 0, captures vérifiées, aucun ERROR.
Images inspectées. Comparaison distante avec `after-taa.png` du lot précédent :
grosses stries fortement atténuées, raccord doux conservé. **Rupture de teinte
visible à mi-rivière**, non expliquée ni acceptée. Ne pas affirmer que la
transmission est visuellement correcte parce que la compilation passe.
La vue `human_bridge` est principalement rasante et masque une partie de l'eau
derrière la berge : elle ne qualifie pas le détail rapproché sous tous les angles.

Overlays non benchmark : vue distante moyenne 19,94 ms/p95 25,87 ms ; vue pont
17,61 ms/p95 22,45 ms. Aucun gain, 60 FPS ou coût du shader isolé démontré.
Les images sont figées : pas de preuve de continuité temporelle ou de direction
du courant observée en mouvement. Pas de livraison Studio, pas de publication Git.

## Suite nécessaire

### Diagnostic ciblé des ombres — même binaire

Deux captures supplémentaires, même caméra/temps/kit/binaire, sans reconstruction
ni modification de shader : `Capture-ShadowDiagnostic.ps1 -Tier low|ultra` dans
le répertoire de preuves ci-dessus. Seul `--shadow-quality` est ajouté.

- Low (64 m, deux cascades, 1024, contacts désactivés) :
  `hero_shadow_low_diagnostic.png`, SHA256
  `A0B67E983C198480B37778A43F879306A725EC1F23FAE720400A7636E1702812`.
- Ultra (192 m, quatre cascades, 4096, contacts activés) :
  `hero_shadow_ultra_diagnostic.png`, SHA256
  `AC6A2A0FAE0B256602F23AA08CC030120639A8CE69E210B80FFCE478AB50B2AA`.

Images inspectées : la coupure initiale vers y=740 disparaît à cet endroit en
Low, et se déplace vers y=625 en Ultra. Le témoin sans override utilise la portée
de végétation 128 m. Cela relie la rupture au chemin des ombres ; ce n'est pas
une modification de la couleur ou du bruit d'eau. Ces presets modifient plusieurs
paramètres simultanément : ils ne constituent pas un test isolé de distance.

Inspection du code : l'entité d'eau ne porte pas `NotShadowCaster`, alors que
le candidat transmet le fond. Hypothèse prioritaire : la surface projette une
ombre opaque sur le lit visible à travers elle, jusqu'à la limite des cascades.
Cette cause précise reste à confirmer par une comparaison sans projection
d'ombre de l'eau seulement, en conservant les ombres reçues des arbres et du pont.
Ne pas corriger en désactivant les ombres globales ou en augmentant leur portée.

Les deux processus possédés, PIDs 23324 et 36380, sont terminés (exit 0) ; chacun
a produit une capture vérifiée et aucun ERROR. Aucun test de code relancé :
aucun code moteur n'a changé dans ce diagnostic. Aucun benchmark déduit.

Confirmer et corriger le contrat d'ombre de la surface transmissive, puis
qualifier mouvement, profils/fallbacks et Studio.
La finition artistique et la Gate 1 restent ouvertes ; aucune case promue.
