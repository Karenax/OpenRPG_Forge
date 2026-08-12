# P2 R135 — Production d’environnement AREA

Les cinq critères source ont été relus. Le modèle, l’écran spécialisé, les commandes
transactionnelles, la persistance, les diagnostics et undo/redo étaient déjà présents.
Le palier ferme leurs adaptateurs d’exécution restants :

- `ORF-AREA-004` : plan runtime explicite Flat/Heightmap/Voxel/Mesh, ressources externes,
  sculpture et peinture de matériaux ;
- `ORF-AREA-005` : tessellation déterministe des routes/rivières/ponts/murs et détection
  d’intersections ;
- `ORF-AREA-017` : globals vent reliés aux matériaux de végétation, audio et force gameplay ;
- `ORF-AREA-018` : dispatch météo autoritaire vers précipitation, audio, événement script,
  surface humide et réplication ;
- `ORF-AREA-019` : transition temporelle bornée entre ciels physique, procédural ou panorama.

Test ciblé : `AreaProductionPlanBuilderTests` (1 scénario couvrant les cinq fonctions).
Preuve Toolset : `OPENRPG_FORGE_TOOLSET_R135_AREA_PRODUCTION_PASS`.

P3/P4, AV7–AV10, commit et push sont exclus.
