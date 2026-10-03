# Bestiaire OpenRPG Forge — volume I

Statut : proposition éditoriale et mécanique, non implémentée dans le runtime Rust.

Ce dossier porte la source structurée du premier volume du bestiaire. Le codex
dynamique et les exports illustrés partagent ces entrées. L'édition textuelle
v0.3 précède volontairement ses illustrations et son export livre ; voir les
limites de sorties ci-dessous.

## Autorités épinglées

- ruleset : `openrpg-forge.systemic-rpg.v1`
  (`7C5A3089D4013932FEF9A6A22F38E3CA3165DEA7E8590FC9273DE1242802D37E`)
- manuel : `openrpg-forge.player-handbook.fr.v0.1`
  (`1C3D1D588A825DEAFFC80130EE3E33E42479E0A5ACE9BD5485B91C0EBE538566`)
- direction artistique : `openrpg-forge.design-system.fr.v0.1`
  (`875651CDE234E24B02BF0D533CA396078F215EF9F50C558E775E83FB6D5762BE`)
- bestiaire : `openrpg-forge.bestiary.fr.v0.3`

## Contrat éditorial

Dans l'export livre illustré, chaque créature ordinaire occupe une double page A4 : planche illustrée à gauche, fiche à droite.
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

L'extension **Horizons sauvages** ajoute 30 créations originales inspirées de
grands archétypes de fantasy : le corpus compte désormais **51 créatures**.
Voir [la recherche et le parti pris](RESEARCH_HORIZONS_SAUVAGES_20260927.md).
Les 21 fiches antérieures et leurs 26 planches sont préservées. Le premier lot
illustré d'Horizons sauvages ajoute quatre planches originales (Chouette-cerf,
Ronce-marcheuse, Dame des lucioles et Dragon des sources closes). Le deuxième lot
ajoute les 26 illustrations restantes : **51 créatures illustrées, 56 planches**.
Toutes les nouvelles fiches portent `illustration_status: illustrated` et une
planche originale distincte. `expansion_wave.illustrated_entry_ids` énumère les
30 créations illustrées ; l'état `complete` signifie une couverture visuelle
complète, pas une qualification humaine, éditoriale finale ou runtime.

Le codex dynamique courant est `E:/OpenRPG_Forge/launcher-aube`. Les anciens
PDF et exports dans `output/bestiary` ne sont pas régénérés pour v0.3 et ne
doivent pas être présentés comme un export complet des 51 entrées. Les sources
illustrées sont désormais complètes ; l'export et sa validation restent distincts.

Les prompts et empreintes du lot sont conservés dans
`../assets/bestiary/provenance/provenance-horizons-art-01.json` et
`../assets/bestiary/provenance/provenance-horizons-art-02.json`. La revue visuelle
est une revue agent, pas une validation humaine ni une qualification runtime.

Validation des sources et illustrations, sans qualification des anciennes sorties :

```powershell
python -B scripts/Test-Bestiary.py --source-only
```

Sans `--source-only`, les exigences PDF, doubles pages et ancienne visionneuse
restent actives : elles ne sont pas déclarées satisfaites par cette livraison.

- `bestiary.json` : source canonique commune au PDF et à la visionneuse ;
- `entries/` : brouillons éditoriaux lisibles et historiques de conception ;
- `../../assets/bestiary/` : illustrations, prompts, textes alternatifs et provenance ;
- `../../../scripts/Build-Bestiary.py` : génération du PDF et de la visionneuse ;
- `../../../scripts/Test-Bestiary.py` : validation éditoriale, mécanique et de pagination.
