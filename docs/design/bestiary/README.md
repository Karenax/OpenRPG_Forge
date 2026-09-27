# Bestiaire OpenRPG Forge — volume I

Statut : proposition éditoriale et mécanique, non implémentée dans le runtime Rust.

Ce dossier porte la source structurée du premier volume du bestiaire. Le PDF, puis la visionneuse
interactive, sont générés à partir des mêmes entrées afin d’éviter une divergence entre le livre et
le jeu.

## Autorités épinglées

- ruleset : `openrpg-forge.systemic-rpg.v1`
  (`7C5A3089D4013932FEF9A6A22F38E3CA3165DEA7E8590FC9273DE1242802D37E`)
- manuel : `openrpg-forge.player-handbook.fr.v0.1`
  (`1C3D1D588A825DEAFFC80130EE3E33E42479E0A5ACE9BD5485B91C0EBE538566`)
- direction artistique : `openrpg-forge.design-system.fr.v0.1`
  (`875651CDE234E24B02BF0D533CA396078F215EF9F50C558E775E83FB6D5762BE`)
- bestiaire : `openrpg-forge.bestiary.fr.v0.2`

## Contrat éditorial

Chaque créature ordinaire occupe une double page A4 : planche illustrée à gauche, fiche à droite.
Les créatures majeures disposent de deux doubles pages. Une fiche distingue les observations
établies, les hypothèses de terrain et les inconnues ; elle expose aussi les télégraphes, coûts,
portées et contre-jeux de chaque capacité.

Les créatures utilisent le même système que les personnages. Le livre ne crée donc ni niveau de
monstre, ni classe d’armure abstraite, ni immunité sans justification anatomique, matérielle ou
magique. Les valeurs proposées servent à la conception et aux futurs tests ; elles ne décrivent pas
encore une mécanique livrée dans le runtime Rust.

La vague `humanoid-families.v0.2` ajoute trois profils gobelins, trois profils gnolls et trois profils
orcs. Ces familles sont décrites comme des peuples sapients : métier, équipement, apprentissage,
responsabilité locale et circonstances expliquent leurs capacités comme leurs conflits. Aucune
ascendance n’impose un alignement ou une hostilité, et chaque fiche conserve des issues non violentes.

## Données et sorties

- `bestiary.json` : source canonique commune au PDF et à la visionneuse ;
- `entries/` : brouillons éditoriaux lisibles et historiques de conception ;
- `../../assets/bestiary/` : illustrations, prompts, textes alternatifs et provenance ;
- `../../../scripts/Build-Bestiary.py` : génération du PDF et de la visionneuse ;
- `../../../scripts/Test-Bestiary.py` : validation éditoriale, mécanique et de pagination.
