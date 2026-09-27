# Gate 4 — inventaire et préparation du package, P05/P06

Inspection locale : 26 septembre 2026. [Périmètre](README.md). Aucun import, conversion, téléchargement, publication, changement de métadonnées ou test graphique effectué. Les états déclarés ci-dessous sont distingués des vérifications réalisées pendant cette tâche.

## P05 — Inventaire constaté

Vault résolu : `E:/OpenRPG_AssetVault` (résolution par OPENRPG_ASSET_VAULT si définie, sinon chemin par défaut).

### Dossiers normalisés présents

| Fournisseur/racine | Sous-dossiers immédiats | Portée de ce nombre |
|---|---:|---|
| ambientcg | 35 | Dossiers de ressources, pas 35 assets qualifiés |
| hivemind | 1 | Pack privé détaillé ci-dessous |
| kaykit | 8 | Packs et animations, intégration non vérifiée |
| kenney | 50 | Packs, intégration non vérifiée |
| openrpg | 2 | Dérivés historiques Solaren, pas autorisation automatique de reprise |
| quaternius | 13 | Packs, intégration non vérifiée |
| Total | 109 | Inventaire de conteneurs uniquement |

L'inspection n'a pas décompressé les sources, compté tous les objets logiques ou contrôlé les dépendances de chaque pack. Les fichiers présents ne sont pas tous représentés par le registre privé inspecté.

### Registre privé inspecté

`metadata/asset-import-registry.v1.json` contient un pack : `hivemind.dark-fantasy-weapons`, révision `7f17f8784df586a725925bbbc316d18fac8c7cc642fe790b9e7c57b052666f0e`.

- Catalogue : `normalized/hivemind/dark-fantasy-weapons-v1/catalog.v1.json`, 17 entrées.
- Les 17 fiches déclarent conversion, validation glTF et métadonnées prêtes.
- Les 17 fiches déclarent `studio_visible=false`, `placement_qualified=false`, `runtime_qualified=false`, `full_import=false`.
- Licence déclarée : `LicenseRef-Unity-Asset-Store-EULA`, champ `private_only=true` ; aucune autorisation de redistribution ou d'usage IA déduite de ce libellé.
- Prises/orientations restent à vérifier ; la fiche échantillonnée indique orientation non vérifiée et aucune animation/armature.
- Capacités déclarées par les métadonnées : adapter Vault disponible ; adapters Studio/runtime indisponibles. Ce statut n'est pas un audit exhaustif de tous les importeurs du produit.

Vérifications effectuées : les SHA-256 des deux fichiers de métadonnées correspondent au registre :

| Fichier dans la révision | SHA-256 calculé |
|---|---|
| details.v1.json | `36f6f0884e146c6c938214fd5dc7e4a1d823c5027445c25ee7e9f508bd3b699b` |
| profiles.v1.json | `88c56f6e74173298926b463685aa84471ee561ee657b69bd981f974f5fcbb996` |

Les GLB eux-mêmes n'ont pas été rehashés ni revalidés ici. Le statut glTF est lu dans les fiches antérieures, pas renouvelé par ces deux hashes.

### Sources internes et personnages candidats

| Source présente | Observation | Conséquence |
|---|---|---|
| [Kit Visual Valley r1-063](../../../examples/visual_valley/asset-kits/r1-063/asset-kit.v1.json) | Révision 3 ; six définitions : deux rochers, chêne, pin, mur, arche | Provenance `procedural_original`, distribution `internal_gate_proof_only`, revue publication requise ; ne pas confondre avec les candidats ultérieurs rev5 |
| Kits r2-025-wall-joint et r2-025-wall-corner | Manifestes présents dans le même dossier de kits | Les preuves portent sur leurs candidats propres ; ce comptage ne qualifie aucun raccord |
| kaykit/character-pack-adventures-1.0 et character-animations-1.1-free | Dossiers présents | Candidats à inspecter pour squelette/clips et style ; aucun clip accepté par cette tâche |
| kenney/animated-characters-protagonists | Dossier, License.txt et Preview.png présents | Licence à lire et modèle à inspecter avant sélection |
| quaternius/lowpoly-animated-knight | Dossier et aperçu présents | Candidat de prototype, adéquation artistique non vérifiée |
| openrpg/solaren-character-derivatives-1.0 | female-peasant-ual2.glb, female-ranger-ual2.glb, male-ranger-ual2.glb et aperçus présents | Dérivés historiques à auditer : provenance, transformations, rigs et droits ; aucun import Solaren exécuté |
| [Bestiaire](../../design/bestiary/README.md) | Proposition éditoriale/mécanique | Ne fournit pas par lui-même des personnages 3D animés |

Le [README forge_assets](../../../crates/forge_assets/README.md) décrit un catalogue consultatif et un kit gouverné. Le preflight GLB strict, la visibilité d'une référence et l'importeur général sont des capacités différentes. Les restrictions historiques du validateur procédural ne doivent pas être contournées en requalifiant un pack acheté comme contenu généré.

### Besoins minimaux de l'aventure

| Ressource | Quantité fonctionnelle proposée | Candidat/état | Preuve manquante avant usage final |
|---|---:|---|---|
| Joueur, donneur de quête, ennemi | 3 rôles, rig réutilisable possible | Packs personnages présents | Licence, échelle, pose, clips, matériaux, intégration |
| Animations | repos, marche/course, attaque, réaction, hors combat | Pack animations présent | Compatibilité squelette, root motion, transitions |
| Décor forêt/rochers | Petit ensemble réutilisé | Kit interne identifié | Candidat exact et droits pour module distribué |
| Intérieur et porte mobile | 1 passage jouable | Murs/arches identifiés ; porte fonctionnelle à qualifier | Charnière position/orientation, collision ouverte/fermée, navigation |
| Arme/outil | 1 attaque lisible | 17 objets privés candidats | Autorisation adaptée, prise, axe utile, équipement et collisions |
| Caisse, support, charge | 3 rôles physiques | Choix précis non établi | Mesh/masse/propriétés distincts, pivot/collider, destruction |
| Objet de quête | 1 objet reconnaissable | Choix précis non établi | Provenance, icône, visibilité et ramassage |
| Feu/eau | Source de chaleur et moyen d'extinction | Mécanique Gate 3 à construire | État simulé, rendu, arrêt et performance |
| Audio | interaction, impact, feu, extinction, notification | Aucun candidat audio qualifié dans cet audit | Droits, durée, niveau et écoute/boucle |
| UI | indicateurs et icônes minimales | Charte disponible | Lisibilité, alpha, contraste et attribution |

La quantité désigne des rôles de preuve, pas une commande d'acquisition. Les ressources existantes sont à privilégier après contrôle ; aucune sélection esthétique n'est fondée sur le seul nom d'un dossier.

## P06 — Acceptation des imports et du package

### Champs à exiger dans le futur catalogue

Identité stable, version, type et rôle ; source/transformations ; hashes source et dérivés ; licence et preuve de droits distincts pour usage local, redistribution et IA ; dépendances ; unités/repères ; caractéristiques adaptées au type ; état de préparation et lien vers ses preuves.

Une ancre fonctionnelle comporte position ET orientation. Les masses, dégâts et capacités sont des données de gameplay, pas des inférences à partir de la géométrie. Les métadonnées de texture/audio n'ont pas à contenir de pivot 3D artificiel. Un hash modifié rend les qualifications concernées périmées tout en conservant les retouches humaines.

### Paquet proposé, format encore à adopter

Le `.orpmod` doit contenir manifeste, versions compatibles, documents canoniques, clôture des dépendances, assets autorisés, hashes, crédits/licences et index. Il ne doit dépendre ni d'un chemin absolu du Vault ni du workspace source. Les scripts futurs sont soumis au contrat de sandbox ; aucun chargement implicite de code natif communautaire.

| Test futur | Entrée | Résultat attendu |
|---|---|---|
| PK01 | Aventure exportée puis installée sur emplacement propre | Installation et jeu sans workspace, Vault, modèle IA ni clone upstream : R4-P01 |
| PK02 | Variante d'un asset, rôle/ID fonctionnel conservé | Liens gameplay et retouches conservés : R4-P02 |
| PK03 | Ressource de licence/provenance inconnue | Publication refusée avec diagnostic de la ressource |
| PK04 | Archive avec chemin absolu, traversal ou lien sortant | Refus avant écriture hors destination |
| PK05 | Doublon de chemin/ID ou casse ambiguë | Refus déterministe sur plateformes concernées |
| PK06 | Fichier tronqué, hash faux, type incohérent | Aucun module partiellement activé |
| PK07 | Expansion excessive ou nombre de fichiers hors quotas | Refus borné en temps/mémoire ; pas d'extraction illimitée |
| PK08 | Installation interrompue | Ancienne version jouable ou reprise explicite |
| PK09 | Module 2 installé à côté du premier | Références isolées, mêmes outils et runtime |
| PK10 | Données Solaren sélectionnées | Source exacte, licence, conversion et limites documentées ; aucun code C#/Godot importé |
| PK11 | Réimport de contenu modifié | Retouches conservées, qualifications périmées visibles, pas d'identité perdue |
| PK12 | Textures/audio avec dépendances manquantes | Import refusé ou avertissement bloquant avant publication selon contrat |

Les seuils de quotas et politiques de versions sont à adopter avec les premiers packages mesurés. Ce dossier fournit les cas et oracles, pas un nouveau format public.

## Livraison

P05 : inventaire borné exécuté, états et manques relevés, intégrité des deux métadonnées vérifiée. P06 : contrat et suite d'acceptation préparés. Les conversions, validations graphiques et droits de publication restent à établir sur les assets sélectionnés. Aucun test runtime/package n'est annoncé exécuté.
