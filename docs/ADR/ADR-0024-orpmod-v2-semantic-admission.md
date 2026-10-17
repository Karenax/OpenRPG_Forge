# ADR-0024 — Première admission sémantique `.orpmod` v2

Date : 15 octobre 2026. Statut : première implémentation Gate 4 ; qualification ouverte.
Complète ADR-0021, à la suite de la demande propriétaire de lancer Gate 4.

## Décision

Réutiliser le conteneur streaming `FORGEPK1` et son installation transactionnelle,
avec un manifeste sémantique `module.json` fermé et versionné
`forge.module-package` v2. Les `.forgepack` v1 restent un transport de livraison
distinct. Renommer une archive ne lui confère aucune admission v2.

Le manifeste vit dans `forge_project::module_package` et référence les archives
canoniques existantes. Il contient les identités projet/module/zones, leur version,
la compatibilité exacte moteur/API, les hashes de tête/archive/gameplay/blobs,
un inventaire fermé, les assets et leurs dépendances, licences, crédits et preuves.
Il ne crée aucun second modèle de carte ou point d'entrée natif communautaire.
Le descripteur du conteneur épingle les octets de `module.json` ; ce document ne
contient pas de hash autoréférent. Hash métier canonique et hash des octets restent
distincts. Les fichiers auxiliaires sont contrôlés contre le catalogue externe.

Le catalogue `forge_assets::governed_import` (`forge.governed-import-catalog` v1)
porte les trois familles Mesh/Texture/Audio, les IDs stables, pins de fichiers,
provenance, droits explicites et filiation acyclique. Toute retouche déclarée
conserve une source authorée ou une recette épinglée. Les contrôles du domaine
restent indépendants du filesystem, du rendu et des outils de conversion.

L'adapter `forge_delivery::orpmod` lie ces contrats aux fichiers réels. Les JSON
bornés sont hashés sur le tampon interprété ; le préflight média traverse le même
lecteur que le calcul du hash. L'inspection garde l'archive ouverte, sans extraction.
Le pack est revalidé après assemblage en staging ; l'installation vérifie les
modules sémantiques avant de changer la tête active. Les anciens packs conservent
leur admission v1. Cette extension ajoute des dépendances aux crates de domaine,
sans dépendance moteur, Tauri ou GPU dans le transport.

## Portée de cette première tranche

Les chemins internes sont ASCII afin de conserver une politique d'alias portable
et vérifiable. Les destinations d'installation, labels et métadonnées gardent
Unicode. Les contrôles de format sont bornés et préliminaires : préflight GLB
autonome existant et signatures image/audio. Les lecteurs runtime et décodeurs
complets restent nécessaires à la qualification produit.

Les références aux catalogues d'espèces, prototypes importés et packages annexes
physiques/particules ne sont pas implicitement résolues par un simple hash d'archive.
Le premier contrat les refuse tant qu'elles ne sont pas embarquées et vérifiées.
Ce refus borne la capacité actuelle ; il ne qualifie pas le package Tidewater.

Les packs existants demeurent privés. Les preuves de licence doivent voyager avec
les assets ; leur présence et leur intégrité ne prouvent pas juridiquement les droits.
Les vignettes et recettes sont des auxiliaires distincts des médias du catalogue.

Les tests de contrats, CLI et installation constituent des preuves locales de cette
tranche. Les imports Studio, chargement du module dans le jeu, variante gameplay,
second module jouable, données Solaren sélectionnées et parcours en machine isolée
restent à réaliser. Aucun critère Gate 4 n'est clôturé par ce seul ADR.

## Complément du 15 octobre 2026 — validation native des médias

La restriction « signatures image/audio » décrivait la première tranche. Le
contrôle commun `forge_assets::media_validation` décode désormais les PNG/JPEG
avec image 0.25.10, les WAV PCM/OGG Vorbis Floor1/FLAC avec Symphonia 0.6.1,
et vérifie les GLB embarqués avec gltf 1.4.1 et des pré-gardes bornées. Floor0
est refusé avant probing après reproduction d'un index non vérifié dans le
décodeur. Les budgets d'entrée, tables, pixels, frames et géométrie restent des
contrôles d'admission, sans promesse de mémoire totale ou de qualification visuelle.

Le [README des imports](../../apps/forge_studio/src/asset_import_native/README.md)
décrit les versions et licences effectivement lues dans le cache Cargo, les
régressions de parsing reproduites et les éléments MPL/Symphonia à transporter
avec le futur pack applications (textes, notices, sources exactes et indication
d'accès). Le script packaging n'est pas changé par ce complément documentaire.
Les contrôles locaux, les licences déclarées, les preuves runtime et la
qualification du produit restent des éléments distincts.

## Complément — provenance d'un module réédité et réexporté

L'import d'une copie auteur conserve une ancre dans `WorkspaceHead` : le module
créé par `ImportModule` porte un `ModuleOriginPin` vers le hash et la taille du
package reçu et le hash de son reçu natif. Le champ optionnel est omis pour les
modules anciens et les créations originales ; leurs octets et hashes restent
inchangés. Undo/Redo et le rejeu des archives doivent conserver cette ancre.
Un registre latéral seul ne convient pas : sa suppression simultanée avec le
dossier importé ne doit pas rendre l'origine implicitement absente à l'export.

Les chemins des pièces sont fixes sous la racine auteur et le ModuleId ; le
frontend ne les choisit pas. Le host vérifie l'ancre, le reçu, puis les mêmes
octets du package original. Le constructeur de réexport doit admettre ce tampon
borné, vérifier l'identité du module et reprendre ses licences, crédits,
manifestes et preuves. Une origine `private_local_only` empêche un réexport
`redistributable`, même si les nouveaux modèles ont une licence permettant leur
redistribution et si le formulaire propose un nouveau texte de licence.

Le bootstrap natif fournit une copie de données editable avec les commandes
communes et les imports gouvernés. Il n'est pas un parcours d'import de module
par l'interface Studio. Les retouches, la réouverture et l'export depuis cette
interface doivent ensuite être observés séparément. Le premier profil de
bootstrap sélectionne une zone et refuse explicitement les blobs sources non
pris en charge avant toute publication de destination.

Ces raccords sont en cours de réalisation et de vérification ; cette décision
ne constitue ni leur preuve d'exécution ni la clôture de Gate 4.

## Complément — sources des décodeurs dans le pack applications

L'assemblage courant dispose maintenant d'un inventaire épinglé de neuf archives
sources Symphonia 0.6.1, avec LICENSE et notices originales, dont la notice Sun
G.711 du codec PCM. Le producteur fournit explicitement son cache local via
`--cargo-cache`. Les archives et notices sont contrôlées avant l'assemblage et
voyagent sous `licenses/native-media/` dans le pack applications. Le client
n'a pas besoin de télécharger ces sources.

Quatorze tests locaux et un véritable cycle pack/inspection/réextraction
vérifient ce raccord. Le cycle utilise un produit de test ; il ne qualifie pas
encore le nouvel installateur complet. Ce lot documente ces dépendances ajoutées,
sans constituer un audit de toutes les dépendances transitives ou un avis juridique.

## Complément — contrôle des chemins Windows sans ouverture de chaque ancêtre

Sous AppContainer sans capacités, les fichiers reçus dans le stockage privé
peuvent être lisibles alors que l'ouverture des métadonnées d'un ancêtre Windows
préexistant, notamment `Packages`, est refusée. L'ancien `safe_chain` ouvrait
explicitement chaque ancêtre et refusait ainsi ces entrées autorisées. Aucun ACL
préexistant n'est élargi et `AccessDenied` n'est jamais transformé en succès.

Le seul adapter Windows de `safe_chain` utilise désormais `NtCreateFile` avec
`OBJ_DONT_REPARSE | OBJ_CASE_INSENSITIVE`, `FILE_OPEN`, les droits
`FILE_READ_ATTRIBUTES | SYNCHRONIZE` et `FILE_SYNCHRONOUS_IO_NONALERT`. Il ne
crée rien et ne demande ni données, ni droits de sauvegarde, ni impersonation.
Le partage `READ | WRITE | DELETE` est celui d'une interrogation de métadonnées,
sans promesse d'immuabilité ; les lecteurs d'archives gardent leurs propres
partages. Le handle possédé devient un `File` par transfert d'ownership, puis
`File::metadata` interroge ce même handle. Tout attribut reparse reste refusé.

Seuls `STATUS_OBJECT_NAME_NOT_FOUND` et `STATUS_OBJECT_PATH_NOT_FOUND` permettent
de remonter au parent. Le premier chemin existant ouvert sans réanalyse termine
le contrôle ; après une remontée pour une queue absente, ce préfixe doit être
un dossier. Un fichier reste une cible existante valide mais ne peut pas porter
une destination enfant absente : ce cas échoue explicitement `NotDirectory`.
Une racine non vérifiable échoue. Les autres statuts, dont les
refus d'accès, restent des erreurs. Le statut direct fait autorité, pas un IOSB
éventuellement nul après un échec immédiat. Cette règle suit les contrats
[OBJECT_ATTRIBUTES](https://learn.microsoft.com/en-us/windows/win32/api/ntdef/ns-ntdef-_object_attributes),
[NtCreateFile](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntcreatefile)
et [IO_STATUS_BLOCK](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/ns-wdm-_io_status_block).

La FFI constitue une exception locale au lint `deny(unsafe_code)`, limitée au
module privé `manifest::windows_path_guard` : layouts C, buffers possédés à
adresses stables, contrôle des tailles et transfert unique du handle à RAII.
Le nom et les structures natives sont dans des `Vec` fixes, sans redimensionnement
ni référence à leurs éléments après câblage ; seuls `as_mut_ptr` et
`UnsafeCell::raw_get` donnent les pointeurs. Le contrat d'aliasing de
[`Vec::as_mut_ptr`](https://doc.rust-lang.org/1.98.0/std/vec/struct.Vec.html#method.as_mut_ptr)
ne matérialise pas de référence à la slice. Des `Box` déplacés après dérivation
de leurs pointeurs ne fourniraient pas cette garantie : stabilité d'adresse et
validité d'aliasing sont deux exigences distinctes. Le slot du handle n'est lu
et transféré à RAII qu'après `STATUS_SUCCESS` ; un retour d'erreur ne garantit
pas que cet output soit un handle possédé valide.
Un `STATUS_PENDING` inattendu conserve toutes les allocations, y compris IOSB,
nom et slot du handle, sans fermeture, lecture IOSB, annulation ou attente
spéculative. Le contrôle échoue explicitement et se désactive jusqu'au prochain
processus. Un verrou borne cette rétention à une seule requête par processus
(nom inférieur à 64 Kio, structures et handle). Il n'existe pas de boucle infinie
produit. Libérer après un simple timeout ou `CloseHandle` ne prouverait pas la
complétion et pourrait créer un use-after-free ; cette anomalie impose donc un
redémarrage. Les tests injectent ce retour pour vérifier la conservation et le
refus des appels suivants ; ils ne prétendent pas provoquer un pending noyau.

Les chemins externes acceptés sont resserrés aux lecteurs littéraux `Disk` et
`VerbatimDisk`, en préservant UTF-16. Pour un lecteur ordinaire non verbatim,
`/` est converti en `\` avant validation, comme les
[fonctions Win32 de fichiers](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation).
Cela conserve les suffixes portables joints par le produit, dont
`_forge-export/assets.json`. Sur un chemin verbatim reçu directement, `/`
reste refusé : cette conversion changerait sa sémantique. `PathBuf::push` sur
une base verbatim reconstruit déjà les composants d'un suffixe ordinaire avec
les séparateurs Windows ; ce chemin résultant reste accepté.
NUL, composants vides ou relatifs,
ADS, points/espaces finaux et alias périphériques (dont COM/LPT avec chiffres
exposants et espaces avant extension) sont refusés. Après la seule conversion
des séparateurs Disk ou le retrait contrôlé du préfixe verbatim,
`std::path::absolute` doit rendre exactement les mêmes unités
UTF-16, sans consultation du filesystem : aucune normalisation silencieuse ne
doit faire diverger le chemin NT contrôlé des réouvertures Win32. UNC et les
autres namespaces sont explicitement non pris en charge par cette tranche.
Les noms internes, `confined`, `no_links` et le comportement non-Windows gardent
leurs contrôles précédents.

Le prototype du 15 octobre a obtenu 12 témoins hôtes et 12 témoins AppContainer
avec refus des réanalyses, des sources/Vault externes et du réseau ; ses profils
ont été supprimés. Ces observations hors produit ne qualifient pas les octets
Rust nouvellement raccordés. Les tests Rust et une nouvelle lecture confinée
réelle restent nécessaires. Les réouvertures ultérieures par nom conservent une
fenêtre TOCTOU ; ce contrôle n'est ni un verrou d'identité, ni une qualification
des autres filesystems/filtres, ni une clôture de Gate 4.
