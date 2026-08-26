# Guide de style Blender — collection Médiéval

> Cible : créer un nouvel asset qui se mélange visuellement avec la collection CC0 **Médiéval** de Quaternius sans rupture de style. Ce guide décrit le kit réel inspecté, pas une cible photoréaliste hypothétique.

## 1. Signature visuelle

La collection emploie un **stylisé PBR lisible**, à mi-chemin entre illustration fantasy et matériaux physiques simplifiés :

- silhouettes nettes, immédiatement reconnaissables à distance ;
- formes épaisses et robustes, avec détails structurels exagérés juste assez pour rester lisibles ;
- géométrie économe mais non primitive : les courbes importantes, bords et volumes principaux sont réellement modelés ;
- arêtes adoucies, chanfreins ou normales lissées ; les objets ne doivent pas ressembler à des blocs CAO parfaitement tranchants ;
- irrégularité contrôlée dans la pierre, le bois, les tuiles et les troncs ;
- textures peintes et PBR qui portent le grain, les variations de teinte, l’usure et les petits détails ;
- palettes chaudes et saturées : bois brun miel, bois sombre, pierre gris-beige, plâtre crème, métal froid, tuiles terre cuite, végétation verte ou rouge saisonnière ;
- contraste suffisant entre matériaux voisins et séparation claire des masses.

Les éclairages très chauds, le bloom et les ambiances cinématiques des aperçus officiels sont des choix de présentation. Ils ne doivent pas être cuits comme une lumière directionnelle dans les textures.

## 2. Échelle et grille

- Travailler en métrique : **1 unité Blender = 1 mètre**.
- Architecture principale sur une grille de **2 m**.
- Sol standard : 2 × 2 m.
- Mur standard : 2 m de large, environ 3,12 m de haut et 0,41 m d’épaisseur visuelle.
- Escalier extérieur : environ 2 × 2 × 1 m.
- Clôture : segment proche de 2 m.
- Porte : environ 1,12 m de large ; 2,10 m plate ou environ 2,32–2,36 m ronde.
- Les tailles de toit sont nominales : les débords et pentes dépassent la grille.
- Comparer tout nouvel asset à un modèle de la même famille dans `MEDIEVAL_ASSET_INVENTORY.csv`, jamais à une valeur générique isolée.

## 3. Pivots et assemblage

Le kit historique n’a pas un pivot universel : l’analyse trouve plusieurs familles de pivots et de nombreux offsets spécialisés. Les bornes du CSV sont l’autorité pratique.

Pour un nouvel asset :

- prop posé : origine au centre de l’empreinte, au niveau du sol ;
- végétation et roche : origine au centre de la base, légèrement enterrable ;
- porte ou volet mobile : origine sur l’axe de charnière, au niveau du seuil ;
- mur, sol, toit ou escalier modulaire : origine correspondant exactement au point de snap du module voisin ;
- vigne/décor mural : origine sur le point d’attache de la surface ;
- appliquer rotation et échelle avant export, sans déplacer arbitrairement le pivot d’une variante L/R/Mirror.

## 4. Géométrie mesurée

Sur les 362 modèles statiques :

- médiane : **362 triangles** ;
- 75e percentile : **1690 triangles** ;
- 90e percentile : **4480 triangles** ;
- maximum observé : **26982 triangles** ;
- architecture : médiane de 125 triangles ;
- props : médiane de 763 triangles ;
- nature : médiane de 698 triangles ;
- tenues : médiane de 4213 triangles.

Ces chiffres décrivent la bibliothèque, mais ne sont pas des plafonds aveugles. Utiliser le budget d’un voisin comparable. Un petit gobelet ne doit pas recevoir le budget d’un personnage.

Règles de modelage :

- consacrer les polygones à la silhouette, aux courbes visibles et aux chanfreins ;
- éviter la microgéométrie qui disparaît à la distance de jeu ;
- conserver des surfaces propres et des normales cohérentes ;
- produire UV0 et normales explicites ; les sources ne stockent pas de tangentes, elles sont dérivées à l’import ;
- ne pas ajouter de bruit aléatoire uniforme : l’irrégularité doit raconter le matériau et la fabrication.

## 5. Matériaux et textures

Mesures du kit :

- 362/362 assets statiques ont une texture de base color ;
- 306 utilisent au moins une normal map ;
- 288 utilisent au moins une texture metallic/roughness ;
- 74 fichiers texture PNG partagés : 52 en 2048², 13 en 4096², 5 en 1024², 3 en 512² et une exception ;
- 569/569 matériaux sources sont double face ;
- 508 matériaux opaques, 54 en `MASK` et 7 en `BLEND` ;
- aucune texture d’occlusion ou d’émission n’est embarquée dans les modèles statiques inspectés.

Conventions visibles :

- **bois** : veinage peint lisible, alternance brun miel/brun sombre, bordures et assemblages renforcés ;
- **pierre** : blocs irréguliers, joints clairs ou sombres, facettes larges et petite teinte de mousse/verdure ;
- **plâtre** : crème chaud, mottling discret, jamais blanc clinique ;
- **tuiles** : terre cuite rouge-orangé, rangées épaisses, variations légères et accents verts ponctuels ;
- **métal** : gris froid ou bronze, highlights de bord et valeurs suffisamment séparées du bois ;
- **tissu** : couleurs franches, plis larges, symboles simples et contrastés ;
- **peau/vêtements** : atlas stylisé, détails anatomiques simplifiés, volumes et coutures plus importants que le grain microscopique ;
- **végétation** : aplats et gradients de couleur sur cartes alpha ou volumes regroupés, sans texture photographique.

Ne pas copier une photo brute. Repeindre ou générer une texture cohérente avec les atlas existants : formes larges, gradients modérés, détails orientés par le matériau, pas de lumière directionnelle cuite.

## 6. Architecture

- Construction à colombages, plâtre, brique irrégulière, bois sombre/clair et tuile ronde terre cuite.
- Les structures sont modulaires : murs, coins, planchers, overhangs, frontons, couvertures, dormers, fenêtres, cadres et vantaux restent séparés.
- Préserver les variantes `Flat/Round`, `Thin/Wide`, `L/R`, `Half`, `Corner`, `Center`, `Mirror`, `Open/Closed`.
- Les bois porteurs et encadrements sont visuellement plus épais qu’en photoréalisme afin de rester lisibles.
- Une maison ne doit pas être un simple cube texturé : pignons, débords, supports, cheminées, volets, balcons et fondations produisent la silhouette.

## 7. Props

- Silhouettes compactes et fonction immédiatement identifiable.
- Planches, cerclages, ferrures, poignées et attaches légèrement surdimensionnés.
- Répéter les matériaux et atlases du kit plutôt que créer une texture unique par petit objet.
- Prévoir les variantes utiles au dressing : vide/rempli, ouvert/fermé, simple/groupe, propre/cassé.
- Les pièces fines — couverts, chaînes, cordes — doivent rester lisibles à la caméra de jeu.

## 8. Nature

- Arbres en familles de cinq silhouettes : communs, morts, pins et arbres torsadés.
- Troncs effilés et légèrement exagérés ; masses de feuilles regroupées au lieu d’un feuillage photoréaliste feuille par feuille.
- Rochers à facettes larges, bords adoucis, dessus clair et base plus sombre/moussue.
- Fleurs et plantes aux couleurs franches, souvent sur cartes alpha ; conserver `MASK`, double face et un contour propre.
- Utiliser les variantes de hauteur et de groupe pour éviter les répétitions ; ne pas compenser un manque de variété par une rotation/échelle extrême.
- Les chemins de pierre existent en familles rondes/carrées, petite/thin/wide ; les assembler sans superpositions visibles.

## 9. Personnages et animations

- Tenues paysan et ranger, homme/femme, fournies en ensembles et pièces séparées.
- Les ensembles sont en T-pose et ne constituent pas toujours un personnage complet : tête, visage, cheveux ou corps de base peuvent manquer.
- Les bras et mains sont volontairement lisibles ; les proportions restent humaines stylisées, pas chibi.
- UAL2 emploie un rig de **65 articulations** et offre **43 clips** dans les variantes Standard et Standard Root Motion.
- Toute nouvelle tenue doit être testée sur le rig cible, en animation extrême, et pas seulement en T-pose.

## 10. Collisions, navigation et LOD

Aucun helper source nommé collision/collider/LOD n’a été détecté dans les 362 glTF statiques. Pour un asset jouable :

- créer une collision simple séparée de la géométrie visuelle ;
- éviter une collision par triangle pour les bâtiments complets ;
- garder les ouvertures de porte et d’escalier réellement franchissables ;
- déclarer navigation, collision et LOD dans les manifestes gouvernés du module ;
- vérifier en runtime, car une belle silhouette ne prouve ni passage ni navigation.

## 11. Checklist Blender → OpenRPG Forge

1. Choisir trois voisins de référence dans le CSV : même famille, taille proche, fonction proche.
2. Bloquer la silhouette à l’échelle métrique et vérifier le snap avant les détails.
3. Ajouter seulement les chanfreins et irrégularités qui améliorent la lecture.
4. Appliquer les transforms et fixer le pivot fonctionnel.
5. Créer UV0, normales et matériaux PBR ; réutiliser un atlas gouverné quand cela convient.
6. Vérifier les cartes alpha sur les deux faces et à distance.
7. Produire une collision simple et, si nécessaire, des LOD gouvernés.
8. Exporter en glTF/GLB sans chemins absolus ni dépendance cachée.
9. Importer par la voie gouvernée, avec provenance, auteur, licence et SHA-256.
10. Valider dans Forge Studio puis en Forward+ dans le runtime, à côté d’assets existants.

## 12. Ruptures de style à éviter

- photogrammétrie ou textures photographiques non retraitées ;
- flat low-poly sans textures ni réponse PBR ;
- matériaux hyperréalistes sur une silhouette simplifiée ;
- bords parfaitement coupants ou, inversement, bevels gonflés partout ;
- bois, pierres et tuiles parfaitement réguliers ;
- détails minuscules illisibles et textures uniques 4K pour chaque petit prop ;
- proportions de porte, étage ou mobilier incompatibles avec la grille ;
- pivot centré sur une porte ou un volet censé tourner sur charnière ;
- plantes alpha en simple face ou avec franges opaques ;
- personnage livré comme « complet » alors que tête, corps, rig ou animations manquent ;
- mélange silencieux avec une cible semi-réaliste différente. Si Solaren change volontairement de direction artistique, créer un guide séparé au lieu de déformer ce kit.

## Références

- `docs/assets/MEDIEVAL_ASSET_INVENTORY.md`
- `docs/assets/MEDIEVAL_ASSET_INVENTORY.csv`
- `asset_manifests/packs/medieval.json`
- planches locales : `artifacts/asset-inventory/medieval/contact-sheets/`
