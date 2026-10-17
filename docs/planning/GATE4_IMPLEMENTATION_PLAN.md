# Gate 4 — réalisation dans le checkout commun

Ouverture : 15 octobre 2026 ; aucun critère n'était qualifié à cette date.
État au 16 octobre 2026 : **clôturée localement**, lot `0.1.0-preview.20261016.019`,
selon le [rapport final](../evidence/gate4/GATE4_CLOSURE_20261016.md).
Les lots et la matrice de départ ci-dessous gardent leur portée historique ;
les verdicts courants sont ceux du rapport et de ses preuves.
Demande propriétaire : « ok lancons le travail gate 4 ».

## Baseline et méthode

Gate 3 est clôturée selon son rapport du 15 octobre. Sa baseline publiée est
`3e0044d5f63b26c678d9db19380a66db832d9de3`. Gate 4 modifie la version commune
`E:\OpenRPG_Forge\OpenRPG_Forge-Rust` sans copie de code par conversation.
Les changements préexistants restent conservés. Les tests négatifs précèdent
les implémentations ; les contrôles lourds sont séquentiels sur le cache commun.

Les contrôles locaux, les parcours installés, la revue humaine et la qualification
de distribution restent distincts. La consigne propriétaire de ne plus attendre
les validations GitHub s'applique à la progression : une exécution distante
absente reste absente, sans être annoncée PASS. Aucune nouvelle publication Git
n'est comprise dans cette ouverture.

## Lots de réalisation

1. **Admission et gouvernance des assets.** Catalogue commun versionné pour
   maillages, textures et audio : identité stable, fichiers et dépendances épinglés,
   provenance, droits explicites, filiation et retouches conservées dans une source
   authorée ou une recette. Refus des chemins non portables, contenus périmés,
   dérivés orphelins et droits insuffisants à la publication.
2. **Contrat sémantique `.orpmod` v2.** Module et zones identifiés, documents
   canoniques existants et blobs référencés, catalogue d'assets, compatibilité et
   inventaire fermé. Ce contrat décrit les documents actuels ; il ne remplace pas
   les archives auteur. Le transport `.forgepack` v1 d'ADR-0021 reste distinct.
3. **Assemblage et admission réels.** Raccorder les validations sémantiques aux
   commandes de livraison, contrôler les fichiers réels et les types admis, puis
   l'activation atomique et la conservation des données utilisateur. Réutiliser
   l'installation existante et les packs séparés.
4. **Parcours Studio et jeu.** Importer et inspecter GLB/texture/audio depuis le
   produit ; remplacer un visuel lié au gameplay en conservant identités et
   retouches ; exporter et charger le module depuis la livraison installée.
5. **Qualification des seize critères.** Installer et jouer sans accès aux sources,
   au Vault ou à l'IA ; installer un second module indépendant dans le même client ;
   qualifier un import expérimental de données Solaren sélectionnées avec provenance.

Les lots organisent la réalisation et ne créent pas de demandes d'approbation
intermédiaires. Un test de contrat ne remplace pas un parcours produit.

## Matrice de départ

| Critère officiel | Acquis réutilisable | Travail Gate 4 |
|---|---|---|
| Catalogue | Navigateur Studio et fiches privées | Catalogue commun et admission dans le produit |
| GLB | Admission GLB autonome et placement gouverné | Import générique et parcours produit |
| Textures | Lecture de textures intégrées/source | Import autonome gouverné |
| Audio | Aucun import commun identifié | Admission, import et restitution |
| Provenance/licence | Plusieurs contrats spécialisés | Contrat commun et contrôle des droits |
| Hash/dérivés | SHA, historique immuable | Dépendances et filiation communes |
| Vignettes | Lecture PNG bornée et épinglée | Génération/import et lien au catalogue |
| Publication | Gardes spécialisés | Admission commune, refus des droits inconnus |
| `.orpmod` v2 | Aucun manifeste sémantique Rust identifié | Contrat, assemblage et admission |
| Protections archive | Transport borné, chemins, hashes, liens refusés | Vérification sémantique et contenus déguisés |
| Installation | Setup013, staging et activation atomique | Raccord `.orpmod` et qualification |
| Runtime | Jeu séparé consommant les archives auteur | Chargement du package validé |
| Second module | Fixtures de domaine indépendantes | Module empaqueté/installé dans le même client |
| Données Solaren | Besoin délimité dans la roadmap | Sélection, provenance et import sans code historique |
| R4-P01 | Parcours installés sur la machine de développement | Machine isolée et boucle RPG complète |
| R4-P02 | Remplacement de placements avec Undo/Redo | Véritable visuel gameplay et retouches durables |

## Livrables et limites

Le [protocole de qualification](../testing/GATE4_PACKAGE_ACCEPTANCE.md) décrit
les parcours finaux. Le [suivi des résultats](../evidence/gate4/GATE4_PROGRESS_20261015.md)
consigne uniquement ce qui a réellement été exécuté.

Le Setup013 et les packs du 15 octobre constituent la baseline historique Gate 3.
Le livrable courant `0.1.0-preview.20261016.019`, les contenus détachés et leur ZIP
sont épinglés dans le [rapport final](../evidence/gate4/GATE4_CLOSURE_20261016.md).
Les packs de la baseline sont `private_local_only` : leur assemblage ne leur
confère aucun droit de redistribution. La réserve GLib et la qualification de
release restent distinctes. L'absence de Windows Sandbox ne justifie aucune
modification des ACL, du Vault ou des chemins du checkout partagé.
