# Hivemind — avancement par lot

Point de suivi du 7 octobre 2026. Bibliothèque définitive : `F:\OpenRPG_AssetVault`. Conversions, caches, copies privées et preuves : `F:\OpenRPG_ImportWork`.

Le registre contient 26 735 fiches (14 219 modèles déclarés et 12 516 sources support). Ce nombre n'est pas le nombre d'objets entièrement convertis. Les compteurs ci-dessous décrivent leurs étapes exactes. Une conversion n'est livrée qu'après vérification du dérivé, indexation et contrôle dans Studio puis en runtime.

| Lot | Contenu et découpage | Preuves acquises | Travail restant / état |
|---|---|---|---|
| L00 | Sources, licences, transfert vers F | Transfert initial et intégrité vérifiés, reçu L00 | Gardes SHA maintenues à chaque sous-lot |
| L01 | Moteur : textures → scène → aperçu → placement | Contrats textures/GLB contrôlés en natif et Web | Test de l'API canonique des scènes en cours ; raccordement au véritable chargement Studio restant |
| L02 | Dungeon : objets statiques et leurs dépendances | Voir preuves détaillées dans ETAT.v1.json ; aucune clôture globale déduite | Matériaux complets, dérivés finaux et contrôle Studio/runtime |
| L03 | Autres objets statiques, par pack puis prefab | 199 géométries intermédiaires réouvertes ; 43 prefabs préparés, 428 associations statiques | Capture Townsmith en cours, 893 meshes demandés ; convertir uniquement les captures validées |
| L04 | Textures, matériaux, normales, feuillage | 43 matériaux source préparés ; 41 topologies ; 28 kernels WGSL validés | 46 textures en file ; 13 matériaux à contrôler nativement dans deux projets isolés ; comparaison GPU et raccordement renderer |
| L05 | Rigs, poids, poses, animations | Rig Medieval identifié sans aplatissement ; exporteur préparé | Capture en file, réouverture indépendante, skin/animations et déformations runtime |
| L06 | Effets Blood et Dust | Captures et contrôles intermédiaires ; 46 textures / 498 mips comparés depuis leur stockage capturé | Conversion des effets, apparence, chronologie et comportement dans le runtime |
| L07 | Sprites, terrains et formats spéciaux | 4 sprites intermédiaires vérifiés, 20 buffers | Assemblage final, dépendances terrain, rendu et usage Forge |
| L08 | Bibliothèque, images, recherche, filtres, métadonnées | Recherche Axe : 7 résultats ; image diagnostique et métadonnées réellement consultées dans Studio | Audit intégral en cours ; images manquantes/génériques, aperçu, placement et sauvegarde/réouverture à qualifier |
| L09 | Réconciliation et recette finale | Non clôturé | Comparer attendu/livré/refusé par asset ; vérifier toutes les preuves et les fichiers sous F |

## Ordre opérationnel

1. **L01.4** : terminer le test déjà lancé de l'API canonique sur les 39 scènes ; enregistrer réussite ou échec réel, puis raccorder le chargement et les matériaux.
2. **L03.4** : laisser finir Townsmith ; vérifier indépendamment les identités et buffers avant toute conversion. **L03.5** : conversion par sous-lot de pack, puis assemblage par prefab.
3. **L04.1** : capturer les 46 textures. **L04.2** : capturer les bindings effectifs des 13 matériaux refusés, après les contrôleurs précédents. **L04.3** : résoudre les dépendances et qualifier normales/couleurs/passes ; conserver les refus documentés si une source manque.
4. **L05.1** : capture et contrôle du rig, puis conversion et essai de déformation.
5. **L06.1** : Blood, puis **L06.2** : Dust ; chaque effet reçoit son contrôle visuel et temporel.
6. **L07.1** : sprites ; **L07.2** : terrains et autres dépendances spéciales.
7. **L08.1** : terminer l'audit global en cours. **L08.2** : contrôler les nouvelles fiches et images après chaque livraison. **L08.3** : aperçu, placement, édition, sauvegarde/réouverture et runtime dans le véritable éditeur.
8. **L09.1** : bilan final explicite, avec les assets utilisables et les refus restants.

Les opérations Unity sont séquentielles. Les contrôles indépendants déjà lancés continuent pendant les captures. Aucun lot validé n'est refait sans raison consignée.

## Comment suivre une livraison

Chaque sous-lot a un identifiant stable, un périmètre, un compteur attendu, un reçu terminal et un compteur réellement vérifié. Ses états sont : à faire, en file, en cours, intermédiaire vérifié, livré dans Studio/runtime, ou refus documenté. « En file » et « processus vivant » ne font pas avancer le compteur vérifié.

Après chaque sous-lot clos, mettre à jour ce tableau, `ETAT.v1.json`, `PLAN_ET_SUIVI.md` et `JOURNAL.md`. Le message d'avancement indique : **lot — vérifiés/attendus — résultat — prochaine étape**, avec lien vers la preuve. Un compteur technique n'est jamais présenté comme un nombre d'assets prêts.

## Travail effectivement repris

Le contrôle natif des 13 matériaux refusés est préparé dans deux projets isolés. Contrôleur lancé : PID 45700, création UTC 2026-10-07T01:00:54.2014440Z. Il attend le contrôleur textures existant ; aucune nouvelle capture validée à cette étape. Preuves : `F:\OpenRPG_ImportWork\unity-delta-null-material-defaults-v1\JOB.v1.json` et `RUN-RESULT.v1.json`.

Le test canonique des scènes est toujours en cours. Aucun succès terminal ni nouvel asset runtime n'est revendiqué à cette actualisation.

## Dossiers de suivi

- Tableau court : `F:\OpenRPG_ImportWork\SUIVI_LOTS_HIVEMIND\TABLEAU_DE_BORD_LOTS.md`.
- État détaillé et liens des reçus : `ETAT.v1.json` dans le même dossier.
- Historique technique : `PLAN_ET_SUIVI.md` et `JOURNAL.md` dans le même dossier.
- Inventaire par pack : `INVENTAIRE_PAR_PACK.csv` dans le même dossier.
