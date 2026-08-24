# Audit initial — Asset Vault et carte de référence

Date : 2 août 2026.

## État existant

- Le domaine, le bus de commandes, la reconstruction de zone, le runtime, le format `.orpmod`, le
  dialogue, les quêtes, le combat et la sauvegarde sont fonctionnels.
- Le kit Crypte utilise des primitives originales et applique déjà les conventions métriques,
  d’orientation, de pivot, de collisions simples et de provenance définies dans
  `docs/ASSET_CONVENTIONS.md`.
- Le toolset ne possède pas encore de catalogue d’assets, d’installateur, de normalisation Blender,
  de terrain extérieur ni de dispersion de végétation.
- Les espaces Assets et les outils Creator complets sont encore annoncés comme limites dans
  `STATUS.md`.

## Stratégie d’intégration retenue

1. Conserver les fichiers 3D volumineux dans un Vault externe configurable et versionner dans Git
   seulement l’outil, les manifestes, les preuves de licence et les inventaires.
2. Commencer avec Poly Haven et ambientCG, dont les catalogues et téléchargements officiels sont
   accessibles sans compte et dont les assets sont annoncés CC0.
3. Télécharger les modèles glTF 1K Poly Haven et les archives `LQ-1K-JPG` ambientCG afin de maximiser
   la variété avant d’engager un budget 2K/4K.
4. Conserver chaque original immuable, calculer SHA-256, vérifier les MD5 fournis par Poly Haven et
   extraire les ZIP ambientCG avec protection contre la traversée de chemins.
5. Publier un catalogue JSON reproductible qui servira ensuite au dock Assets et au pipeline de
   normalisation Godot/Blender.

## Extension plutôt que remplacement

- Étendre la reconstruction et le placement existants ; ne pas créer une deuxième source de vérité
  de scène.
- Réutiliser le format de module et son extraction sûre pour les futurs packs, sans confondre un pack
  d’assets avec un module jouable.
- Faire passer les futures mutations de scène et commandes MCP par le bus transactionnel existant.

## Risques

- Les axes, pivots, échelles et matériaux des fichiers sources doivent être contrôlés avant emploi en
  scène ; le statut `downloaded-original` ne signifie pas `production-ready`.
- Le catalogue complet peut occuper plusieurs gigaoctets malgré la résolution 1K.
- Les APIs peuvent évoluer ; les URLs, licences et métadonnées sont donc enregistrées avec une date
  de vérification.
- Une grande quantité d’assets ne garantit pas une direction artistique cohérente. Asset Vault
  utilisera une sélection revue visuellement et non le catalogue entier.
