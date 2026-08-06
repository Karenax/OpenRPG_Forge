# Gouvernance des sources Asset Vault

Dernière revue : 5 août 2026.

Ce registre décrit la décision **OpenRPG Forge**, pas une déclaration générale sur une plateforme.
Une source reste refusée tant que licence, provenance, redistribution et transport ne sont pas
prouvés pour les fichiers exacts visés.

## Sources acceptées et automatisées

| Source | Périmètre accepté | Licence du noyau | Transport | Preuve locale |
|---|---|---|---|---|
| Poly Haven | modèles fournis par l’API officielle | CC0-1.0 | API et URLs officielles | `asset_manifests/sources/polyhaven.json` |
| ambientCG | modèles 3D et archives officielles | CC0-1.0 | API v3 et URLs officielles | `asset_manifests/sources/ambientcg.json` |
| Kenney | packs 3D dont la page exacte déclare CC0 | CC0-1.0 | page et ZIP officiel | `asset_manifests/sources/kenney.json` |

L’acceptation est bornée à ces adaptateurs, variantes et URLs. Un miroir tiers portant le même nom
de source n’est pas accepté automatiquement.

## Sources exigeant une action manuelle

| Source | Packs actuellement identifiés | Motif du mode manuel | Conditions avant catalogue |
|---|---|---|---|
| Quaternius | Stylized Nature MegaKit, Medieval Village Pack | Itch.io ou dossier partagé sans URL d’archive stable autorisée par l’adaptateur | téléchargement depuis la page officielle, archive intacte, SHA-256, preuve CC0 exacte, extraction sûre et revue visuelle |

La procédure détaillée et les pages épinglées sont dans `MANUAL_DOWNLOADS_REQUIRED.md`. Aucun outil
ne simule une validation humaine, ne contourne une page ou ne devine une URL interne.

## Sources refusées ou différées

| Catégorie | État | Motif de refus actuel |
|---|---|---|
| marketplaces et stores sous licence propriétaire | refusée pour le noyau | redistribution brute, usage commercial ou modification non prouvés sous CC0 |
| agrégateurs à licences mixtes | différée | une licence de plateforme ne suffit pas ; chaque fichier, auteur et texte d’attribution doivent être vérifiés |
| miroirs, reuploads et liens communautaires | refusée | chaîne de provenance et intégrité non vérifiables |
| génération IA sans provenance/licence explicites | quarantaine | auteur, données, droits de redistribution ou fournisseur non gouvernés |
| packs CC-BY, copyleft ou non commerciaux | pack optionnel différé | la chaîne d’attribution et de redistribution par module n’est pas encore validée de bout en bout |
| téléchargements nécessitant contournement, scraping de session ou secret embarqué | refusée | transport non reproductible ou contraire aux règles de sécurité du projet |

« Différée » signifie qu’un futur adaptateur et une revue juridique peuvent changer la décision.
« Refusée » signifie qu’aucun téléchargement ni import automatique ne doit avoir lieu dans l’état
actuel du projet.

## Gate d’admission d’une nouvelle source

Avant d’ajouter un adaptateur ou un pack manuel :

1. épingler page officielle, licence SPDX, URL de licence et date de vérification ;
2. prouver redistribution, usage commercial et modification pour les octets exacts ;
3. séparer les conditions d’API de la licence des assets ;
4. utiliser uniquement URLs officielles, User-Agent identifiable, reprise et limites ;
5. conserver l’original immuable, SHA-256, taille, provenance et reçu ;
6. extraire en confinement, mettre en quarantaine tout contenu suspect ;
7. normaliser sans modifier l’original, mesurer puis inspecter visuellement ;
8. produire crédits et notices depuis les assets réellement installés/utilisés ;
9. ajouter tests, documentation et manifeste `asset_manifests/sources/<source>.json` ;
10. n’élargir le catalogue qu’après validation d’un petit pack cohérent.
