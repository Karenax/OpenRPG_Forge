# ADR-0029 — Profil et surfaces communs aux zones d'un module

Date : 19 octobre 2026. Statut actualisé le 22 octobre 2026 : contrat implémenté,
R5-P01/P02 qualifiés localement, selon le
[rapport de clôture](../evidence/gate5/GATE5_CLOSURE_20261022.md).

## Problème

Le premier inspecteur utilise la configuration de la zone active. Cela ne suffit
pas à configurer un module : ses autres zones pourraient garder d'autres valeurs
ou surfaces. Une bibliothèque de blueprints contient par ailleurs des références
locales à sa zone et ne doit pas être copiée comme une configuration globale.

## Décision

`ModuleCreatorProfile` porte le profil numérique, les capacités et les surfaces
communes, avec l'identité du module. Ses capacités couvrent les données de toutes
ses zones, même masquées. Sa sérialisation canonique est épinglée par SHA-256.
Les bibliothèques de blueprints et les définitions de contenu restent locales.

Le workspace doit référencer cette définition commune comme un objet immuable.
Un changement commun prépare les nouveaux documents de zone et leurs références,
puis une seule commande du workspace remplace le profil et les références de
toutes les zones concernées. La publication utilise l'autorité CAS et l'historique
existants ; les objets préparés ne constituent pas une autre autorité mutable.
Preview ne publie rien. Undo/Redo restaure le profil et les références ensemble.

Le `RulesProfile` et la configuration embarqués dans un document de zone sont
la matérialisation vérifiée de cette définition, utilisée telle quelle par le
runtime. Une zone d'un module configuré ne peut plus changer ces valeurs seule.
Admission, lecture et sauvegarde doivent vérifier leur correspondance avec la
définition commune. Une nouvelle zone/gameplay doit hériter des mêmes valeurs.

Les archives anciennes sans configuration commune restent lisibles. L'activation
est explicite et l'aperçu doit montrer toutes les zones affectées, notamment si
leurs anciens profils diffèrent. Aucun profil ancien n'est choisi silencieusement
sur la seule création d'un blueprint. Les zones sans gameplay gardent leur contenu.

L'export/import `.orpmod` doit transporter la définition épinglée et vérifier
chaque zone avant publication. Les inspecteurs, aides et calculs utilisent les
mêmes valeurs matérialisées ; aucune interprétation par slug ou nom de module.

## Preuves nécessaires

Deux zones avec données et blueprints distincts : changement commun, contenu
masqué conservé, refus d'une capacité absente ou d'une identité étrangère, refus
d'une zone incohérente, commande atomique/idempotente, conflit sans publication
partielle, Undo/Redo, Save/Open, admission du paquet et parcours Studio réel.
Les tests d'un objet de profil seul ne prouvent pas ce parcours complet.

## Raccordement Studio

Le contexte de la carte gérée doit exposer l'identité du module et le hash courant
du workspace. L'aperçu de configuration passe les valeurs de profil, surfaces et
capacités demandées ; le backend conserve aussi les exigences des contenus de
toutes les zones, puis retourne la proposition commune et les noms des cartes
affectées. Apply accepte exactement cette proposition, en conservant l'identité
d'audit du preview. Les requêtes périmées et un playtest actif refusent la mutation.

L'historique du module utilise l'autorité workspace et rafraîchit la carte active.
Il ne doit pas annuler la dernière opération d'un autre module. Les commandes
habituelles de retouche et d'historique local gardent leur portée explicite.
L'initialisation des règles d'une future carte hérite de la définition commune.
Ces raccordements restent en cours tant que le protocole, ses tests et le
parcours dans le Studio ne sont pas qualifiés.

## Transport du profil et des cartes

Les paquets anciens restent au manifeste v2, sans définition commune ajoutée.
Le manifeste v3 porte une définition `creator` bornée, dont l'identité et les
capacités sont validées. Son empreinte fait partie du manifeste épinglé par le
transport. Chaque archive RPG doit matérialiser les mêmes règles, capacités et
surfaces ; recalculer son hash ne permet pas de contourner cette vérification.
Les cartes sans données RPG restent autorisées et héritent du profil à leur
initialisation. Les blueprints locaux et les corrections restent dans leurs archives.

L'export d'un module géré capture toutes ses cartes sous le hash du workspace,
pas seulement la carte sélectionnée. Le catalogue gouverné couvre leurs références
réunies. Le profil d'export continue à refuser explicitement les sources raster ou
blocs d'instances non transportés ; aucune carte n'est omise pour les contourner.
Le producteur de workspace réadopte toutes les archives, épingle la définition et
vérifie leur identité après réouverture, sans réécrire une matérialisation déjà
équivalente. Ce producteur reste distinct d'un parcours d'import dans le Studio.

La révision des données d'un module couvre son identité de projet/module, le pin
du profil et ses références d'archives, triées par identité de carte. Elle reste
distincte du hash global de workspace utilisé pour le CAS. Une retouche d'un autre
module ou le choix d'une autre carte principale ne doit pas invalider un export
inchangé ; une retouche d'une de ses cartes le doit. La provenance et les droits
restent épinglés séparément dans le paquet et son origine.
