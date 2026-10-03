# Bibliothèque privée Studio — validation locale du 27 septembre 2026

Le bouton **Bibliothèque d’assets**, dans les ateliers principal et secondaire,
ouvre un écran dédié sans navigation de la carte. Le frontend accepte également
une entrée autonome `?asset_library=1`. Le lecteur natif reçoit seulement des IDs
et filtres ; il lit le Vault configuré par `OPENRPG_ASSET_VAULT` et ne le modifie pas.

Recherche sans accents, filtres pack/famille/type/sous-type/qualification, tri,
pagination, vignettes PNG contrôlées, fiche dimensions/pivot/ancres/confiance/licence
et accès aux métadonnées brutes sont présents. Les images et modèles restent hors
bundle dans le Vault privé.

## Preuves

- Suite native Studio : 116 tests réussis, 2 ignorés (dont le test live opt-in).
- Test live distinct : parcours intégral de 1 877 fiches et vignettes, 19 packs,
  unicité des IDs, pagination, cohérence cartes/fiches et empreintes PNG : PASS.
- Clippy natif strict, rustfmt des nouveaux fichiers et diff whitespace : PASS.
- Compilation WASM release puis candidat Tauri Windows : PASS.
- Clippy WASM strict reste bloqué par le `too_many_lines` préexistant de
  `atelier_ui::bind_main` (103/100), hors fonction modifiée pour ce raccordement.
- UI Windows inspectée via Computer Use, contenu 1280 × 720 et fenêtre agrandie :
  1 877 résultats avec images ; `epee` donne 4 résultats ; avec le pack Dark Fantasy
  Weapons, 3 ; recherche absente, 0 ; réinitialisation, 1 877 ; page 2, 25–48.
- Sélection, fiche détaillée, retour Échap et Actualiser vérifiés. Contraste du
  titre/survol/sélection corrigé et revérifié. Coordonnées et confiance lisibles.

Candidat : `output/studio-gold-20260924/candidate/forge-studio-candidate.exe`.
SHA-256 : `F4140A39756568A1DC4D4DC8B193A6684A705462EB610C7A07031EB3E695749C`.
Rapport et captures privés :
`F:/OpenRPG_ImportWork/review/asset-library-studio-validation.v1.json`,
`asset-library-studio-overview.png`, `asset-library-studio-metadata.png`.

## Actualisation native après extension du catalogue

Le lecteur accepte désormais le filtre de disponibilité `converted_model` ou
`source_support`. Les sources conservées restent recherchables avec leur provenance
et leur chemin FBX, mais n'exposent aucun chemin GLB et portent explicitement le
statut non plaçable. Le registre compagnon immuable contient actuellement quatre
cas : deux maillages source sans géométrie visible et deux dépendances de scène.

Après cette extension, les contrôles ciblés donnent 8 tests unitaires réussis,
2 tests live ignorés par défaut, Clippy ciblé strict réussi et compilation de la
bibliothèque frontale `wasm32-unknown-unknown` réussie. Le test live exact du
registre compagnon a relu les quatre sources et compte 5 651 entrées recherchables :
5 647 modèles convertis et 4 sources conservées. Journal :
`F:/OpenRPG_ImportWork/review/native-library-source-support-5651.20260927.log`,
SHA-256 `EA26944DE32D4919B8378ABB140A0F6ABE2B8F9FE0EE024691969EB826E3B7DA`.
Cette actualisation est une validation native ; aucune nouvelle preuve graphique
n'est revendiquée après l'arrêt de la fenêtre par Échap.

## Parcours intégral après les imports Hospital, Medieval et Military URP

Le registre contient désormais 6 648 modèles dans 28 packs. Les quatre fiches de
support source portent le total recherché par le lecteur à 6 652 entrées. Les 521
vignettes du catalogue Medieval sûr ont été rematérialisées sous son répertoire
`input/previews` depuis leurs deux sorties de conversion épinglées (173 basic et
348 expanded). Chaque copie a subi deux relectures SHA-256 dans le répertoire
temporaire, une troisième après renommage atomique, ainsi que deux décodages PNG.
Le reçu est
`F:/OpenRPG_AssetVault/normalized/hivemind/medieval-kingdom-source-authored-frame-safe-v1/input/preview-restoration.receipt.v1.json`,
SHA-256 `F168B5CF35E48CD49F3A7FEAA689F5A1D147D3CA1EC501ACCF1E774B68D8D8ED`.

Le test live du registre compagnon passe avec quatre sources et 6 652 entrées.
Le parcours natif intégral passe en 413,27 s : 6 652 IDs uniques, 28 packs,
pagination et facettes cohérentes, lecture de chaque fiche, puis lecture et
signature PNG de chaque vignette annoncée. Rapport :
`F:/OpenRPG_ImportWork/review/native-library-6652-verification.v1.json`, SHA-256
`DA342BD1C24B181715E73B1FA9F0576638B69C3307DF5CC0D6AC0E588672A7C6`.
Il s'agit d'une preuve native du lecteur actuel ; la preuve graphique reste celle
du candidat précédent et n'est pas étendue implicitement au nouveau total.

Un candidat local distinct a ensuite été reconstruit avec le frontend courant et
le même Vault F:, sans remplacer le candidat Gold. La revue automatisée du vrai
WebView Studio puis l'inspection des captures 1 600 × 900 confirment : 6 652
résultats, sept filtres/tri, 24 vignettes chargées sur la première page, recherche
sans accent `epee` à 12 résultats, réduction à 3 par le pack Dark Fantasy Weapons,
fiche détaillée avec dimensions/pivot/ancre/confiance/licence/provenance, et filtre
Source conservée à 4 fiches explicitement non plaçables et sans faux GLB.
Rapport visuel :
`output/asset-library-6652-20260927/asset-library-6652-ui-review.v1.json`, SHA-256
`965FCB8F5E013469B7884D45C4C7A5D64A12B153359AD6F8B33928F427EB384D`.
Le processus de revue et son port CDP ont été fermés après capture. La suite
frontend donne 117 PASS et 3 tests opt-in/bench ignorés ; la suite binaire 13 PASS.
Clippy strict ciblé `forge_studio` sans dépendances et rustfmt ciblé passent. Le
formatage global reste non exécutable à cause du module Tidewater
`tidewater_plant_probe/terrain_ao.rs` absent, hors de cette page.

## Parcours natif après l'ajout Dungeon URP

Le pack privé Dungeon URP ajoute 1 534 modèles et porte le registre à 8 182
modèles uniques dans 29 packs. Avec les quatre fiches de support source, le lecteur
expose 8 186 entrées. Le test live du registre compagnon passe à ce total. Le
parcours natif intégral passe ensuite en 239,84 s : pagination stable, 8 186 IDs
uniques, 29 packs, égalité entre chaque carte et sa fiche détaillée, chemin modèle
pour chaque entrée convertie, chemin source sans faux modèle pour les quatre
supports, et lecture PNG de chaque vignette annoncée. Rapport :
`F:/OpenRPG_ImportWork/review/native-library-8186-verification.v1.json`, SHA-256
`08CD43B1032A35DEF4E36530BF20EC056AC393E641B2D317E927BDB9FC444052` ;
journal SHA-256
`BEDA49E66D8D6DE3415CC334369855516F3D4BDC7B400E55A6083EFADB1F7570`.
Le registre contrôlé porte le SHA-256
`B2EA0C7C302D149C927364ECF84D72C8F4DB066FA741322BB8606B6A7068FF2D`.
Cette étape étend la preuve native au nouveau total ; la dernière preuve graphique
WebView reste celle des 6 652 entrées tant qu'une nouvelle capture n'est pas scellée.

## Limites

Il s’agit des assets déjà indexés, pas de toute la collection source convertie.
Cette page ne les place pas dans une carte et ne qualifie pas leur runtime.
Le retour à l’atelier a été testé sans zone ouverte ; aucune nouvelle qualification
de carte/GPU ni fermeture de gate n’est déduite. Les métadonnées historiques du
Vault n’ont pas été réécrites pour promouvoir leur statut.
