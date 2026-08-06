# G13-L — Équipement, usure et conteneurs

## Périmètre borné

- `ORF-ITEM-019` — armes et attaques ;
- `ORF-ITEM-020` — armures et protection ;
- `ORF-ITEM-021` — munitions et projectiles ;
- `ORF-ITEM-022` — usure, réparation et casse ;
- `ORF-ITEM-023` — conteneurs imbriqués.

Aucune autre exigence G13 n’appartient à ce lot.

## Contrat livré

Le profil avancé d’objet décrit les attaques, canaux de dégâts, portée et animations ; couverture,
protection, encombrement, bruit, matériau et résistances ; consommation et récupération des
projectiles ; durabilité et réparation ; puis capacités, profondeur, masse, permissions et
persistance des conteneurs.

Le runtime résout munitions et usure de façon déterministe et refuse profondeur excessive ou cycles.
La fenêtre **Objets avancés** regroupe ces propriétés dans un onglet équipement avec résumé lisible,
diagnostics, dry-run, application et historique.

## Preuves de fermeture

- validation/résolution : `ItemAdvancedProfileTests` ;
- mutation/ForgeScript : `ItemAdvancedWorkflowTests` ;
- persistance : `QuestItemDepthPersistenceTests` ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G13L_ITEM_EQUIPMENT_PASS` ;
- runtime : `OPENRPG_FORGE_RUNTIME_G13L_ITEM_EQUIPMENT_PASS`.

## Limites explicites

- les profils référencent animations, projectiles et effets existants ; ils ne fabriquent pas les
  assets 3D ;
- l’affichage graphique de l’usure dépend des variantes visuelles fournies par le module ;
- l’inventaire historique reste propriétaire du stockage, le profil ne duplique pas son contenu.
