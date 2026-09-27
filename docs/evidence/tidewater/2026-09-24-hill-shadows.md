# Tidewater — ombres du relief calculées en natif

Source épinglée intacte `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Le prototype calcule désormais les ombres lointaines du relief sur le GPU
Bevy, puis les applique au terrain, au village et à la végétation. Il ne lit
pas une carte d'ombres précalculée par Node à l'exécution.

## Calcul et contrôle indépendant

Rust reconstruit depuis les hauteurs 2048² finales les moyennes de cellules
4×4, puis les max-mips 512² à 1². Accumulation f64 comme JavaScript, stockage
f32 et conversion f16 tronquée comme DataUtils source. Les 699 050 octets de
cette pyramide sont identiques à l'export original :
`9dd52cf258819846d97e8a1234066c90dfd17de0ef860f242c660d6a8a2fd18d`.

Le kernel WGSL original effectue les 24 pas du rayon vers le soleil. Il produit
512² RGBA16F : hauteur limite de visibilité, distance à l'occulteur, 0, 1.
La direction du soleil est une uniform native. Le calcul précède les caméras,
et est invalidé lorsque ses ressources d'image ou ses paramètres extraits
changent. Le parcours interactif de modification et la synchronisation avec
le soleil de tous les matériaux restent à qualifier ; ce contrôle utilise
un soleil fixe à 16,2 h.

Le prélèvement manuel bilinéaire et la largeur de pénombre viennent du source.
Seule la lumière directe est modulée. L'oracle indépendant exécute TerrainGPU
et le shadeSurface originaux, avec le même facteur d'ombre du relief. Ombres
des objets, des nuages et auto-ombrage des prairies restent exclus.

## Résultats numériques et visuels

La première version à soleil constant dans le WGSL était identique octet par
octet à la carte source. La version finale à uniform conserve 22 texels
différents sur 262 144 : erreur maximale de hauteur 0,03125 m, moyenne
3,95613824e-7 m ; aucune différence de distance d'occulteur ni des deux autres
canaux. Valeurs finies partout. Ce changement de résultat est constaté ;
aucune cause détaillée d'optimisation du compilateur n'est démontrée.
La capture du village est identique entre ces deux versions natives.

| Comparaison sRGB de la scène assemblée | Village | Île |
| --- | ---: | ---: |
| Erreur moyenne par canal /255 | 0.462401 | 0.161890 |
| P99 du maximum par canal /255 | 8 | 3 |
| Maximum /255 | 182 | 135 |
| Désaccord noir/non-noir | 0 | 0 |

Les ombres sont visibles dans les vallées et sur les flancs de l'île. Le fond
marin reste exposé : l'océan n'est pas raccordé. Pas de fidélité complète
ni d'identité pixel à pixel revendiquée pour les images finales.

## Artefacts et régressions

Racine `E:\OpenRPG_Forge\.tidewater-integration` :

- Source finale : `hill-shadow-source-002` (001 : première variante constante).
- Fixtures : `hill-shadow-scene-002`, `hill-shadow-island-001`.
- Sorties GPU natives : `native-hill-shadow-002.shadow.bin` et
  `native-hill-island-001.shadow.bin`, 2 097 152 octets, SHA-256
  `a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288`.
  Source : `210d9e16527f5b74ccbaacfeca04790af39420a6a1c38594372218d9a07c54dc`.
  Rapport : `hill-numeric-comparison-final.json`.
- Village : `source-hill-shadow-001.png`, `native-hill-shadow-002.png`,
  `hill-shadow-comparison-002.json`. Image native SHA-256
  `e77d7022ab9c7eec41b36d443906f5e3fce55380b85e041c7bbbe4fdcd2ef61f`.
- Île : `source-hill-island-001.png`, `native-hill-island-001.png`,
  `hill-island-comparison-001.json`. Image native SHA-256
  `044f4e600ff3a4a59eb3bf520c427a5333bdf017403e0d3864fe8bbed92232fb`.
- Build et Clippy strict réussis : `hill-shadow-build-002.log`,
  `hill-shadow-clippy-003.log`. Binaire final :
  `15373015ae6438167e229872cc51cd9890b9c264f024d5b7fbe44a8db2d47e62`.
- Ancienne scène sans ombres inchangée : `hill-disabled-regression.png`,
  `42a003b53791264ed4d01f50db0e7e16ffa57f210f4942095b44260128fc621c`.
- Empreinte attendue de pyramide corrompue, hash de fixture recalculé : refus
  sémantique avant GPU, code 1, `hill-invalid.log`.

Les images assemblées ne qualifient pas les ombres proches des arbres/maisons,
le ciel dynamique, l'eau, les autres effets, Studio, WebGPU navigateur ou la
performance. Aucun changement de statut de gate ni publication.
