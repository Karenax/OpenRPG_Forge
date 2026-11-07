# ADR-0051 — Ressources d'objets explicites dans le module, API 7

Date : 26 octobre 2026. Statut : réalisation Gate 7 en cours.

## Décision

Une correspondance de relief ne déclare pas des objets. Ajouter une liaison par
zone `ModuleObjectBinding` vers un paquet de surfaces source fermé : index, fichiers
et identités/pins du catalogue. Les poses restent dans les placements canoniques.
Les assets source ne sont pas des GLB gouvernés ; leurs identités sont disjointes.
La présence de ce nouveau champ exige API 7. Les anciennes API gardent leur JSON
sans champ vide ajouté et refusent explicitement le champ futur, même vide.

Le codec existant devient `forge_assets::source_surface_package::SurfacePackage` ;
la préparation existante de géométrie et textures devient
`forge_engine::source_surface::package`. Conserver profils, bytes, contrôles, mipmaps,
samplers et choix d'attributs. Les identités historiques du format source restent
épinglées ; elles ne constituent pas des dépendances à une identité de module.

Le résolveur exige la fermeture exacte, les tailles/hashes et la licence déclarée
dans la provenance. L'absence de liaison ne lit aucune ressource. Un réexport depuis
une origine admise conserve les pins et les droits tout en permettant les retouches
canoniques. La découverte et l'adoption par les vues restent explicites.

## Preuves et limites

Le bilan `module-source-objects-validation-001.json` rapproche tests, refus, transfert
des implémentations, aller-retour réel de 821 ressources et sondes natives reconstruites.
La zone RPG est une fixture de qualification ; le paquet source est réel. Cette preuve
ne vaut ni import/édition dans Studio ni rendu du module. Le jeu attend encore des
meshes gouvernés pour ses placements et refuse les références source ; son raccord
ainsi que celui du Studio restent à réaliser. Aucun critère général Gate 7 n'est clos.
