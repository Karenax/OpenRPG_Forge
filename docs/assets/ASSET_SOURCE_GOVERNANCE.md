# Gouvernance des sources Asset Vault

Dernière revue : 24 août 2026.

Ce registre décrit la décision **OpenRPG Forge**, pas une déclaration générale sur une plateforme.
Une source reste refusée tant que licence, provenance, redistribution et transport ne sont pas
prouvés pour les fichiers exacts visés.

## Sources acceptées et automatisées

| Source | Périmètre accepté | Licence du noyau | Transport | Preuve locale |
|---|---|---|---|---|
| Poly Haven | modèles fournis par l’API officielle | CC0-1.0 | API et URLs officielles | `asset_manifests/sources/polyhaven.json` |
| ambientCG | modèles 3D et archives officielles | CC0-1.0 | API v3 et URLs officielles | `asset_manifests/sources/ambientcg.json` |
| Kenney | packs 3D dont la page exacte déclare CC0 | CC0-1.0 | page et ZIP officiel | `asset_manifests/sources/kenney.json` |
| KayKit | dépôts officiels épinglés dont la licence du commit déclare CC0 | CC0-1.0 | dépôt et archive du commit officiel | `asset_manifests/sources/kaykit.json` |

L’acceptation est bornée à ces adaptateurs, variantes et URLs. Un miroir tiers portant le même nom
de source n’est pas accepté automatiquement.

## Sources acceptées via import manuel gouverné

| Source | Périmètre admis | Transport | Preuve locale |
|---|---|---|---|
| Quaternius | Universal Base Characters Standard, LowPoly Animated Easy Enemies et collection Médiéval : Fantasy Props MegaKit Standard, Medieval Village MegaKit Standard, Modular Character Outfits Fantasy Standard, Stylized Nature MegaKit Standard, Universal Animation Library 2 Standard | téléchargement humain depuis Itch.io, puis import local épinglé par SHA-256 ; les octets déjà présents sont réutilisés | `asset_manifests/catalogs/manual_itch_cc0_packs.json`, `asset_manifests/catalogs/quaternius_standard_packs.json`, `asset_manifests/packs/medieval.json` |
| KayKit | Forest Nature Pack FREE, Character Animations FREE, Fantasy Weapons Bits FREE, RPG Tools Bits FREE, Resource Bits FREE | téléchargement humain depuis Itch.io, licence CC0 interne, puis import local épinglé par SHA-256 | `asset_manifests/catalogs/manual_itch_cc0_packs.json` |

Les admissions KayKit restent bornées aux dépôts épinglés et aux cinq archives FREE exactes du
catalogue manuel. Elles n'admettent pas automatiquement les autres packs ou niveaux payants.

Il ne reste aucun pack identifié en attente d'une action manuelle. L'historique et les hashes sont
dans `ASSETS_MANUAL_DOWNLOAD.md` et le catalogue. Aucun outil ne simule une validation humaine,
ne contourne une page ou ne devine une URL interne.

## Sources refusées ou différées

| Catégorie | État | Motif de refus actuel |
|---|---|---|
| marketplaces et stores sous licence propriétaire | refusée pour le noyau | redistribution brute, usage commercial ou modification non prouvés sous CC0 |
| agrégateurs à licences mixtes | différée | une licence de plateforme ne suffit pas ; chaque fichier, auteur et texte d’attribution doivent être vérifiés |
| miroirs, reuploads et liens communautaires | refusée | chaîne de provenance et intégrité non vérifiables |
| génération IA sans provenance/licence explicites | quarantaine | auteur, données, droits de redistribution ou fournisseur non gouvernés |
| synthèse vocale locale reproductible | admise au cas par cas | texte autorisé, divulgation synthétique, outil/version/paramètres, preuve des droits de sortie, hash et licence de chaque sortie obligatoires ; le générateur n’est pas redistribué implicitement |
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

Pour une voix de dialogue, le registre doit aussi identifier le locuteur fictif, la locale, le texte,
la prise, la nature humaine ou synthétique et le consentement applicable. Une URL de service TTS ou
la licence d’un outil ne suffit jamais à autoriser la redistribution des octets produits. Le parcours
opérationnel est défini dans [`../DIALOGUE_VOICE.md`](../DIALOGUE_VOICE.md).

La procédure d’implémentation, de revue visuelle et d’admission est détaillée dans
[`ASSET_SOURCE_AND_PACK_EXTENSION.md`](ASSET_SOURCE_AND_PACK_EXTENSION.md). Les extensions admises
restent soumises aux preuves décrites dans [`AV5_DELIVERY_AND_EXTENSION.md`](AV5_DELIVERY_AND_EXTENSION.md).
