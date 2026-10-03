# Tidewater — acquisition du bateau canonique

Suite du [masque de coque dans l’eau réelle](2026-10-02-real-water-hull-probe.md)
et du [rapport d’exposition Studio](2026-09-30-studio-exposure-sweep.md).
Le bateau source est maintenant acquis, avec sa géométrie, ses matériaux
procéduraux, ses pivots et ses données d’interaction. Son importeur Rust, son
placement éditable dans Studio et son comportement jouable restent à raccorder.
Cette tranche ne qualifie pas la parité visuelle de l’île.

## Source et acquisition reçue

Source : checkout Tidewater propre à
`1438b1abfcaee3267092b75573014f4d9b4a983c`, dans
`E:/OpenRPG_Forge/.tidewater-integration/upstream`.
L’exporteur propre à Forge évalue uniquement la construction CPU du modèle et
les commandes de ses pièces dans une copie des **54 modules préalablement
hashés**. Il n’initialise aucun GPU, n’exécute aucun outil du dépôt source et
ne lance pas App, Player, BoatController ou BoatSpray. Ces quatre fichiers
d’intégration sont conservés comme références de source.

Recette : [export-boat-assets.mjs](../../../../scripts/tidewater/export-boat-assets.mjs).
Paquet final :
[manifest.json](<F:/OpenRPG_Forge/.build/boat-source-acquisition-319/manifest.json>).
Les 154 payloads représentent 7 742 667 octets, hors manifeste.
Les noms sont triés en ASCII, les chemins physiques contrôlés, et la sortie
est vérifiée dans un répertoire de staging avant renommage vers un nouveau
répertoire. Les anciens candidats sont conservés.

| Élément | SHA-256 |
|---|---|
| Manifeste 319 | `44a9110a0bda0cd319488f088fc1da32f3344f6cad8b9db0a92869df0c49351a` |
| Graphe source, 54 références triées ASCII | `4c19abeacf0343600de7926c8d4cc673b351b6bdf593b94791ba0da616c7650b` |
| boat.json, 94 519 octets | `3bec4368e04645e9fdd58e2aeadf48ff7a41df3f250f88a8364cfe7de73a8431` |
| Matériaux source, 21 272 octets | `65489a54ddb9db651594abe7ecf7e119e4ea1de2b43d98beef4b37ee89607792` |
| GLB d’inspection, 3 001 520 octets | `b7e0ad4fb628cf62c75e2205d6ac954564b1497f97e117f61fad3dcbf2d54986` |
| Licence MIT source | `bf7d406711a4cbf9af0b9eb5f2cf464ae88053e999e0e64a92b3423494dd8699` |

Le paquet conserve également `CREDITS.md`, les crédits audio, les quatre sons
du bateau déclarés CC0 dans ces crédits et les sources des contrôleurs.
La conservation de ces fichiers ne prouve pas leur lecture par le runtime Rust.

## Contenu et fidélité géométrique

Le groupe `LobsterBoat` contient **14 nœuds, 12 meshes et 46 845 triangles**.
Le commentaire historique du modèle annonçant environ 37k triangles ne
correspond pas au compte effectivement évalué sur cette révision.

Les sept buckets statiques sont hull, gelcoat, wood, fittings, trap, glow et
glass. Les cinq meshes mobiles sont wheel, throttle, radar, propeller et rudder.
Le pivot orienté de la roue reste un nœud parent distinct.
Chaque pièce conserve ses positions, normales, UV, couleurs linéaires et
attribut `aux`, ainsi que ses indices, son parent et sa transformation locale.

Le descripteur conserve les 23 collisions source, les 32 échantillons de
flottabilité, les dimensions et l’hydrostatique déclarées par le constructeur,
les points d’embarquement/sortie, le point de vue de conduite, les points
d’embruns, les pivots et les axes des commandes. Les orientations des points
fonctionnels sans orientation source restent non qualifiées.

Le repère source est conservé : mètres, +Y vertical, +Z avant, +X bâbord ;
transformation source → asset identité. La pose canonique vient de
`WORLD.boatDock` : `(64.5, 0, 36.5)`, heading 0. Elle est séparée du modèle
local et devra devenir un placement du document, pas une constante du moteur.

Les sept matériaux conservent leurs snippets WGSL, modules, uniforms, attributs,
varyings, defines et règles de transparence/profondeur. Les observations CPU
des commandes −1/0/+1, de l’hélice/radar après un pas et des feux de navigation
sont stockées avec leur méthode source. Les drivers du drapeau sont explicites.
Les animations interactives ne sont pas transformées en courbes glTF fictives.

## Proxy d’exclusion d’eau

Le proxy est un payload distinct, absent de la scène GLB visible. Son parent
est une référence explicite à un identifiant du descripteur ; le futur
adaptateur devra composer cette identité avec l’asset et l’instance de placement.
Il ne doit pas rechercher une coque par nom de mesh ou slug de module.

Le calcul topologique retrouve 952 positions, 910 sommets soudés, 5 436 indices,
1 812 triangles et **2 722 arêtes totales** : 2 714 arêtes intérieures et
8 arêtes de frontière. Les nombres satisfont `2 × 2714 + 8 = 3 × 1812`.
Le proxy n’a aucune arête non-manifold ou incohérente ni triangle d’aire nulle.
Ses huit bords ouverts restent présents ; aucune réparation n’est appliquée.

L’empreinte canonique reste
`52b35be1ea38f49f9448ce8d8984d4d0bb3f3c3c506e73cbc1232a04d86ab92e`.
Les indices source restent u16 LE. L’importeur devra vérifier leur hash brut,
les élargir vers u32 et appeler
`Geometry::from_verified_source_proxy(..., fingerprint, 8)` ; la validation
géométrique du moteur demeure obligatoire. Ces données ne justifient pas une
déclaration `NoHullsInScene`.

## Contrôles effectués

[verify-boat-assets.mjs](../../../../scripts/tidewater/verify-boat-assets.mjs)
passe sur le candidat final :
[résultat 320](<F:/OpenRPG_Forge/.build/boat-acquisition-verification-320/result.json>).
Les 154 fichiers sont conformes à leurs hashes/tailles ; les **72 accessors
GLB sont identiques octet par octet** aux payloads source. Les 14 nœuds,
leurs transformations et les sept associations de matériaux sont préservés.
Khronos glTF Validator `2.0.0-dev.3.10` rapporte 0 erreur, 0 avertissement
et 12 informations d’UV inutilisés par les matériaux d’inspection.
Les [trois contrôles de refus 325](<F:/OpenRPG_Forge/.build/boat-export-negative-checks-325/result.json>)
passent aussi : destination déjà existante, sortie dans les sources et premier
payload altéré sont refusés avec code 1. Aucun résultat interdit n’est créé,
et le manifeste final conserve son hash après le refus d’écrasement.

La [fiche FR/EN](<F:/OpenRPG_Forge/.build/boat-import-profile-324/profile.json>)
décrit le bateau, ses mesures et ses ancres avec leurs statuts distincts.
Elle est locale et n’est pas encore indexée dans le Vault.

[inspect-boat-blender.py](../../../../scripts/tidewater/inspect-boat-blender.py)
réouvre le GLB dans **Blender 5.2.1 LTS**, build `9e2066aef7ef` :
[résultat 321](<F:/OpenRPG_Forge/.build/boat-blender-inspection-321/result.json>).
Les douze meshes, 46 845 triangles, la hiérarchie et les slots matériau sont
retrouvés. Toutes les erreurs de bornes composées sont nulles ; l’erreur de
matrice maximale est environ `2.98e-8` sur la roue, sous le seuil `2e-6`.
Le changement de repère Blender est explicite : `(x,y,z) → (x,−z,y)`.

Le [rendu d’inspection des ancres](<F:/OpenRPG_Forge/.build/boat-blender-inspection-321/boat-origin-anchors.png>)
est inspecté par l’agent : bateau entier visible, annotations d’embarquement,
conduite, hélice/gouvernail et repère lisibles. Le repère dessiné est déplacé
pour la lisibilité, avec un trait vers l’origine source réelle. Cette image
utilise les paramètres PBR et couleurs de vertices d’inspection ; elle ne
reproduit pas les shaders procéduraux et n’est pas une acceptation humaine.
La [revue agent](<F:/OpenRPG_Forge/.build/boat-import-profile-324/visual-review.json>)
conserve le hash de l’image et ces limites sans modifier le reçu Blender.
Deux avertissements Blender de dépréciation `use_nodes` pour la version 6.0
sont conservés ; ils n’affectent pas les contrôles géométriques.

Les recettes exactes sont figées dans
[reçu 323](<F:/OpenRPG_Forge/.build/boat-source-receipts-323/recipes.json>).
Les revues indépendantes sont conservées dans
[308](<F:/OpenRPG_Forge/.build/boat-source-map-308/NOTES.md>),
[309](<F:/OpenRPG_Forge/.build/boat-descriptor-api-309/NOTES.md>),
[310](<F:/OpenRPG_Forge/.build/boat-validation-map-310/NOTES.md>),
[312](<F:/OpenRPG_Forge/.build/boat-export-review-312/NOTES.md>) et
[317](<F:/OpenRPG_Forge/.build/boat-acquisition-audit-317/NOTES.md>).
L’audit 317 concerne le candidat intermédiaire 314 ; il ne doit pas être
présenté comme l’audit du manifeste final 319.
L’[audit final 322](<F:/OpenRPG_Forge/.build/boat-final-export-audit-322/NOTES.md>)
confirme les corrections du candidat 319 et les limites du contrôle 320 :
snapshot hashé, containment physique, ordre ASCII, staging/renommage,
topologie mesurée et référence explicite au nœud parent.

L’échec initial Blender 316 est conservé : le premier inspecteur utilisait
`C × M` au lieu de `C × M × C⁻¹` pour les matrices de nœud. Blender change
aussi la base des vertices. La correction porte sur l’inspecteur ; elle ne
modifie aucun payload source. Le retour CLI initial 0 n’était pas une réussite
Python. Les essais suivants utilisent `--python-exit-code 1` et passent.

## Suite nécessaire et frontières de preuve

Suite validée : [lecteur Rust sur le paquet réel](2026-10-02-boat-rust-loader.md).
L'étape 1 ci-dessous possède maintenant sa preuve de transport CPU. Le placement,
les matériaux, les commandes et la qualification de scène restent ouverts.

1. Ajouter un lecteur explicite du nouveau schéma `boat-package`, avec limites
   et contrôles de toutes les références ; ne pas injecter un vingt-quatrième
   asset dans le contrat Building actuel à 23 bâtiments.
2. Résoudre un descripteur de coque générique par asset/hash et identifiant de
   part, puis attacher son repère local à l’instance éditable. Vérifier
   déplacement, rotation, suppression, sauvegarde et réouverture de la zone.
3. Porter les sept matériaux procéduraux et les contrôleurs source, puis
   inscrire le bateau canonique dans un workspace isolé par les commandes
   d’authoring réelles. Compléter le census de scène et ses epochs pour le
   traitement temporel Studio.
4. Reconstruire les crates actuels nécessaires : les rlibs forge_assets
   historiques utilisés par Engine300/probe304 ne contiennent pas ce nouveau
   lecteur. Leur PASS ne qualifie aucun nouvel importeur.
5. Qualifier le bateau dans l’eau réelle, Studio natif/WebGPU, gameplay et
   comparaisons de la scène complète. Réintégrer le contenu qualifié dans la
   distribution utilisant le launcher actuel.

État final de cette tranche : acquis et documenté, transport GLB et géométrie
réouverte validés ; index privé, visibilité Studio, placement éditable,
matériaux Rust, animations jouables et parité visuelle **non qualifiés**.
L’objectif complet reste actif.
