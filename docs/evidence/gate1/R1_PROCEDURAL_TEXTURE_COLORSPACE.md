# R1 — Couleurs procédurales linéaires encodées en sRGB

8 septembre 2026, branche Rust, changements locaux conservés, aucune publication.

## Défaut reproduit et correction

`Build-VisualValleyAssetKit.py` utilise les mêmes couleurs pour les entrées
linéaires Principled et la génération procédurale des textures. Le writer affectait
ces nombres directement au buffer d'une image Blender **byte**, puis déclarait
`sRGB`. Cette déclaration ne transforme pas les valeurs écrites par `foreach_set`.
Le PNG stockait ainsi une couleur linéaire comme si elle était déjà encodée sRGB.

Test réel Blender 5.2.1 LTS, hash `9e2066aef7ef`, sans autoexec :
`Test-TextureColorSpace.py` écrit trois PNG témoins, les recharge et vérifie le
décodage RGB, l'alpha et l'absence de mutation du tableau source.

- Avant correction : échec, 0,18 linéaire devenait **0,027320892** ; 0,5 devenait
  0,21586053. Preuve rouge, sortie 1, dans le dossier `red`.
- Après : trois maps PASS, sortie 0, dossier `green`. RGB couleur encodé avec
  la fonction de transfert sRGB ; alpha, normales et rugosité non encodés.
  Tolérance absolue 0,005 pour la quantification 8 bits, aucun seuil assoupli.
- `Test-FoliageAlpha.py` rejoué : MASK, BLEND et opaque exportés, textures
  embarquées, mode inconnu refusé. PASS ; avertissement de sampler du fixture
  multi-nœuds conservé, pas présenté comme validation du pack entier.

La correction est dans la règle commune du générateur, pas dans le shader ou
l'exposition, ni dans une retouche ponctuelle de la vallée. Elle s'applique aux
prochaines textures générées. Les anciens fichiers ne sont pas écrasés.

## Dérivé local et preuve visuelle

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-texture-colorspace-20260908`.
`Derive.py` répare **uniquement les huit images base-color des deux arbres** du kit
forestier épinglé. Source correspondant exactement aux pins historiques vérifiée
avant/après. Les assets authorés sont exclus ; les quatre autres GLB restent
identiques. Aucun asset externe, Cadle ou téléversement.

Les nouveaux PNG sont ajoutés au buffer GLB et leurs références réaffectées.
L'ancien bloc binaire reste identique octet pour octet : géométrie, normales,
UV, collisions, alpha, maps de données et paramètres matériaux non modifiés.
Le décodage des nouveaux pixels retrouve les anciennes valeurs procédurales
avec tolérance 0,005 ; alpha identique exactement. Les anciennes images restent
dans le buffer pour cette preuve : ce n'est pas un package de distribution optimisé.
Nouveau kit identifié `-linear-color-repair`, pins catalogues actualisés, revue
humaine pending et publication false. Le script est une réparation du snapshot
épinglé, pas un importeur arbitraire ni une conversion à appliquer aux assets achetés.

Khronos gltf-validator 2.0.0-dev.3.10 : zéro erreur pour les deux GLB ; douze
warnings de tangentes générées par GLB conservés. Infos d'objets inutilisés
(dont anciennes images conservées) non masquées. Réimport réel Blender des deux
GLB PASS, rapport `reimport.json`. L'audit source initial `audit.json` n'avait pas
forcé le chargement paresseux des images : remplacé comme preuve texture par
`r1-tree-material-audit-20260908/audit-v2.json`, premier rapport conservé.
Normales bois LOD0 non opposées aux faces dans les deux modèles ; quelques
oppositions LOD1/2 du pin sont enregistrées, pas déclarées corrigées.

Capture native après la même marche jusqu'au sous-bois, même binaire
`7C4EB10B3877D6B5FA600C838C355A696BBD2910014776D79BECF0C04E4BEFDC`, mêmes ombres
expérimentales 2048 / trois cascades / 128 m / contact, autres domaines High.
Comparaison avec `r1-sky-binding-integration-20260908/candidate.png` :
**troncs bruns lisibles au lieu de silhouettes presque noires, nuances de feuillage
retrouvées**. Soleil, exposition, densité, personnage et géométrie inchangés.
Pose finale (-132,49524 ; 2,5993133 ; -203,51639), quelques millimètres d'écart au
replay précédent, animation non synchronisée : pas d'égalité pixel à pixel prétendue.

RX 7800 XT/Vulkan, Full HD, dev, VSync par défaut. Overlay 22,2 FPS, moyenne
glissante 44,94 ms, p95 46,94 ms, 1 % low 20,3 FPS : **pas un benchmark**, aucune
amélioration de performance qualifiée. PID 8392 sorti 0, zéro ERROR Runtime,
marche terminée avant capture, processus fermé et absence confirmée. Blender
également terminé, aucun onglet ouvert.

## Portée exacte

Correctif générateur et test de non-régression intégrés ; dérivé candidat natif
validé techniquement et inspecté visuellement. **Pas encore intégré au bundle
Studio, ni promotion artistique humaine, ni régénération complète du kit Blend.**
Les captures prouvent que ce défaut contribuait fortement au sous-bois noir ; elles
ne qualifient pas chaque étape GPU de l'IBL. Ne pas poursuivre la piste d'un gain
de ciel arbitraire pour compenser cet encodage. Performance et Gate 1 restent ouvertes.
Contrôle de diff PASS ; pas de rebuild Rust/WASM inutile pour cette correction
Python seule. Les 630 tests du lot précédent ne sont pas des tests de ces textures.

| Artefact | SHA-256 |
| --- | --- |
| Build-VisualValleyAssetKit.py | `723A9BE9BB29B84AA3C13DC893070880363A1554228FE8668A5CA4B4606D5F07` |
| Test-TextureColorSpace.py | `710CE76B94E664DDF7B12D92D043C41C75F3CEB0859F5C32E2D647C00C2E9F52` |
| derivative.json | `0229CCD3BA4B8612514E287E551011AA89DD4338F61A641CCCDE9C416C83A1C3` |
| validation.json | `145377F3D0D8F1E5AA9C42D6C7860F72657D1D425B547AA3BFB48EFA52DE5D6B` |
| reimport.json | `2439D102108364D63A64B3B3D1E92D583EAA15155E6BCC012FA5F44FF6BE78B2` |
| runtime-kit/build.json | `0F1304723BAD88F4AB01D0710DC1D2B7F1B0AD073B2745EC39B90FD6943BBD48` |
| candidate.png | `8A243F70FEA267337B951E37F1EC59D482816CB4D1BC63BAE9681DDC3DFFEA7A` |
| candidate.metrics.json | `2C8D3969A37B58B79279F339BC1627A80505850353297355D249ABDEF8E3ED62` |
