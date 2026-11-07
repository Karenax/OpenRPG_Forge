# Préparation native d’un workspace à partir d’un module

`prepare_module_workspace` est un outil producteur de qualification. Son usage
hors de l’application ne prouve pas un parcours d’import `.orpmod` dans l’UI du
Studio. Les imports et réimports de médias dans le Studio sont contrôlés
séparément.

```text
prepare_module_workspace --module MODULE.orpmod --area UUID \
  --expected-package-sha256 SHA256 --destination DOSSIER_ABSOLU_INEXISTANT
```

Le producteur désigne une seule zone du module et fournit le hash de l’archive
reçue, calculé indépendamment. L’admission et la copie originale utilisent les
mêmes octets immuables. La taille de cette archive est bornée à 256 Mio ; cette
borne ne constitue pas une mesure de la mémoire totale du décodage.

Le profil est limité à 128 assets du catalogue complet, incluant les textures et
sons non placés et les parents des variantes. Il réserve ainsi au moins 128
révisions dans le magasin d’import existant avant sa limite de rétention. Cette
borne concerne cet outil de qualification et ne limite pas le lancement du jeu.
Les archives de zones et leurs sources immuables sont bornées ensemble à 256 Mio,
avec au plus 64 Mio par blob. Les versions d’API admises suivent le lecteur commun des modules.
L'API 2 conserve aussi les sources nécessaires à undo/redo ; elles sont copiées
par les magasins et commandes d'adoption existants, avec tailles et hashes
vérifiés. Une archive dont l'historique requiert une source absente reste refusée,
y compris si son ancien contrat API 1 n'imposait que les sources courantes.
Aucune suppression, réparation ou conversion implicite n'est permise.

Les sources et preuves doivent aussi satisfaire le contrat courant d’import du
Studio : preuve textuelle UTF-8 bornée à 64 Kio, recettes JSON bornées à 1 Mio,
formats sources pris en charge par l’importeur et source/retouches explicites
pour une variante. Un profil non pris en charge provoque un refus explicite ;
un staging inactif peut être conservé pour diagnostic. Aucune preuve n’est
tronquée et aucun droit n’est élevé.

La destination doit être fraîche, avec un parent existant. Les chemins sont
contrôlés physiquement et les liens/jonctions sont refusés. Un dossier frère
privé reçoit les fichiers écrits et synchronisés ; la destination n’est publiée
qu’après réouverture et vérification du workspace. Les racines et noms affichés
restent Unicode ; les chemins internes des contrats sont portables et ASCII.

L’import utilise les commandes communes du magasin d’assets et du workspace,
sans fallback Vault. IDs, pins des médias, filiation, droits et octets des
sources/recettes sont conservés. Le magasin normalise les chemins des preuves
et ajoute sa propre étape de préservation de source ; cela ne constitue pas une
copie identique des métadonnées du catalogue initial.

L’archive originale et chaque payload initial restent disponibles exactement :

```text
module-imports/<ModuleId>/original.orpmod
module-imports/<ModuleId>/receipt.json
module-imports/<ModuleId>/payloads/<chemin initial>
```

Le reçu strict porte les IDs, droits et pins de taille/hash, sans chemin fourni
par l’auteur. Le `ModuleEntry.origin` canonique épingle le reçu et l’archive ;
Undo/Redo et la sauvegarde conservent cette origine. Une origine ancrée dont le
reçu ou l’archive manque bloque la lecture d’origine et le réexport. Elle ne
devient jamais silencieusement un module sans origine.

Le réexport natif demande l’origine au host pour le head courant, puis appelle
`read_export_origin` et `build_module_with_origin`. Ce chemin préserve les
documents d’origine et refuse la publication d’un module d’origine privée,
même si ses médias et les nouvelles retouches sont déclarés en CC0. Ces
déclarations et contrôles techniques ne valent ni avis juridique ni validation
artistique, et ne constituent pas à eux seuls une clôture de gate.

Le snapshot d'export natif récupère la fermeture des sources de toutes les zones
depuis l'autorité ouverte. Les blobs partagés sont embarqués une seule fois dans
le nouvel export API 2, sans modifier le magasin ou exposer des chemins au WebView.
Voir [ADR-0039](../../../../docs/ADR/ADR-0039-module-archive-source-closure.md).


## 26 octobre 2026 — Lecture native des compagnons végétaux du module ouvert

Le Studio possède un protocole natif de description et de lecture par blocs de
64 Kio des ressources végétales de l’origine du module ouvert. La résolution
des espèces gouvernées et des prototypes importés emploie les mêmes décodeurs
`forge_delivery::module_vegetation` que le jeu installé. Le nouveau chemin
`restore_authoring` vérifie séparément le head d’origine admis et le head auteur
courant ; il produit une liaison dérivée, sans réécrire un manifeste, une archive,
une preuve ni un droit. L’admission installée garde son exigence de head exact.
Les identités et pins viennent du host canonique ; la vue ne choisit aucun chemin
natif. Chaque ressource est assemblée intégralement puis vérifiée par SHA-256.

Le bootstrap de qualification ne refuse plus globalement la végétation : il
restaure et vérifie chaque zone avant activation, et conserve l’origine et ses
payloads. Le bilan central `module-vegetation-restore-validation-001.json` rapproche
2 723 tests workspace, 121 tests de transport, 21 contrôles d’architecture,
Clippy natif/WASM et formatage. L’essai privé du module complet à deux zones
lit exactement 84 ressources (46 605 576 octets), vérifie une retouche canonique
après revue, les lectures obsolètes/refusées et la réouverture du lecteur.
Les tests synthétiques couvrent les deux familles de ressources en édition,
tout en conservant le refus des heads différents dans l’admission installée.

Le parcours qualifié est natif et son aller-retour JSON est testé. Aucun WebView
ni draw GPU n’est exécuté par cet essai. Le viewport consomme encore la
bibliothèque de référence : son raccordement à cette lecture du module, les
catalogues d’auteur et la qualification visuelle complète restent à terminer.
Ce bilan ne qualifie ni les performances, ni Linux, ni une machine propre et ne
clôture aucun des dix critères généraux Gate 7. Les essais échoués restent
conservés : revue obligatoire manquante dans le test, puis confusion initiale
entre le head installé et le head auteur, avant correction du chemin commun.


## 26 octobre 2026 — Végétation du module dans le viewport commun

La végétation du module ouvert est maintenant acquise depuis son origine
canonique par le WebView, puis confiée aux propriétaires existants du moteur :
préparation, textures, rendu, herbe, sélection et projection des espèces. Le
Studio ne réimplémente pas ces mécanismes. Le chargement vérifie head, module,
zone, origine, ressources et hashes ; un échec ne bascule pas sur la bibliothèque
de référence. Les ressources immuables sont réutilisées et les réponses d'une
ancienne sélection sont rejetées, y compris au retour d'un picking GPU.

Le bilan central `module-vegetation-viewport-validation-001.json` rapproche
2 726 tests workspace (65 ignorés), 21 contrôles d'architecture, Clippy natif/WASM,
formatage et les deux builds du Studio. L'acquisition privée lit 84 ressources
avec 765 requêtes, puis une seule description en réutilisant le paquet immuable.
L'instance réelle ouvre les deux zones enregistrées : zéro matériau végétal sur
la première, onze sur la seconde, avec herbe source annoncée et végétation visible.
Les captures et sorties DOM sont conservées ; elles ne prouvent pas seules
l'origine des octets, vérifiée séparément par le test d'autorité/acquisition.

La vérification révèle encore des objets de référence affichés sur la première
zone malgré une liste canonique vide. Le terrain, les objets et l'environnement
gardent des acquisitions de référence à raccorder aux données du module.
L'éclairage du Studio reste fixe. Les instantanés de cadence ne sont pas un
benchmark apparié. L'édition/sauvegarde/réouverture/réexport dans l'UI, la fidélité
complète, les performances et les distributions restent à qualifier. Les dix
critères généraux Gate 7 restent ouverts. L'instance de qualification a été fermée
sans demander de sauvegarde ; aucun essai historique n'est réécrit.
