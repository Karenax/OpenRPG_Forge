# Imports de la collection utilisateur

L’hôte fournit une racine de données utilisateur absolue. Le navigateur choisit
des fichiers et transmet leurs octets dans l’enveloppe binaire ORAI v1 ; il ne
fournit aucun chemin d’accès au système de fichiers. Le Vault privé n’est pas
modifié. Les métadonnées conservent Unicode ; les chemins internes sont ASCII.

`Prepare` valide les métadonnées et les médias, conserve les octets immuables,
la source et la preuve de licence, puis retourne un plan lié à la révision.
Ces fichiers préparatoires restent inactifs. `Apply` reprend les pins du plan,
revérifie les fichiers, enregistre un reçu et remplace atomiquement `head.json`.
Le verrou système empêche deux mutations concurrentes. Une relance identique
du dernier Apply retourne son reçu ; un plan périmé est refusé.

Les catalogues, plans, sources et reçus précédents sont conservés. `Undo`
restaure le catalogue précédent avec une nouvelle révision sans effacer les
octets. L’historique est borné à 256 catalogues/révisions ; il n’est jamais
tronqué silencieusement. Un arrêt avant la publication de la tête laisse des
objets inactifs et n’active pas un import incomplet. La sauvegarde flushée et
le remplacement de fichier ne constituent pas une preuve de résistance à une
coupure électrique de toute la machine.

Le réimport exige la même identité et l’ancien SHA attendu. Les placements et
sauvegardes conservent leurs pins ; l’adoption d’une autre référence utilise
les commandes communes de prévisualisation/application des objets de carte.
`lookup_model` retourne `None` seulement pour une identité jamais importée.
Une version connue absente, un historique corrompu ou un payload modifié
retourne une erreur et interdit un remplacement par un modèle du Vault.

« Créer une variante » crée une nouvelle identité avec un parent conservé dans
la collection. Le fichier retouché et une source de travail ou recette JSON sont
obligatoires. Un réimport de la variante conserve sa filiation ; il ne peut pas
changer de parent. La preuve du parent peut être conservée, et les droits de
redistribution de tous les ancêtres restent exigés lors d’une publication.

Les images PNG/JPEG et les sons WAV PCM, OGG Vorbis avec profil Floor1 et FLAC sont
décodés par les validateurs natifs communs. Le profil Vorbis Floor0 est refusé
avant la sonde du conteneur : un paquet hostile atteignait un accès non vérifié
dans le décodeur retenu. Les GLB passent aussi les contrôles URI, structure,
accès aux buffers, valeurs finies et types d’accessors avant lecture. Les budgets
bornent notamment les pixels, échantillons, paquets et tables de codebooks Vorbis.
Ces limites d’admission ne représentent pas une mesure de la mémoire totale du
processus. Un GLB structurellement vide reste explicitement non visuel.

L’export d’un instantané conserve les pins historiques explicitement référencés,
leur filiation, vignettes, sources durables et preuves de licence. Chaque tampon
retourné est vérifié sur son SHA et sa taille. Ce chemin retourne des octets en
mémoire et impose 512 Mio par export ; deux versions d’une même identité dans un
seul catalogue v1 sont refusées. Les anciens fichiers et la tête ne sont pas
modifiés par l’export. Les écritures immuables utilisent un fichier temporaire
flushé puis un renommage sous le verrou du magasin, sans exiger les liens durs.

L’interface propose aperçu GLB dans le canvas existant, lecture d’image/audio,
vignette choisie et conservation d’une source authorée ou recette JSON. Une
vignette choisie ne devient pas une capture du modèle. Préserver une recette
ne signifie pas que sa réexécution a été qualifiée. Les déclarations de droits
restent distinctes d’une revue juridique et d’une qualification de publication.
Les preuves de parcours Studio, placement, réouverture, runtime et installation
restent nécessaires pour qualifier les critères Gate 4 correspondants.

## Décodeurs et redistribution — complément du 15 octobre 2026

Les dépendances natives épinglées de `forge_assets/Cargo.toml` sont `image`
0.25.10 (PNG/JPEG, décodage réel et limites de décompression), `gltf` 1.4.1
(structure GLB et lecture des buffers embarqués) et Symphonia 0.6.1 (WAV PCM,
OGG Vorbis Floor1, FLAC, paquets réellement décodés). Les contrôles byte-only sont
communs aux imports et à l'admission Delivery. Les décodeurs ne sont pas exécutés
dans le navigateur ; les sources de ces dépendances n'ont pas été modifiées.

Les `Cargo.toml` et fichiers `LICENSE` du cache Cargo de ces versions déclarent :

| Composant | Licence déclarée | Textes présents dans la source |
| --- | --- | --- |
| `image` 0.25.10 | `MIT OR Apache-2.0` | `LICENSE-MIT`, `LICENSE-APACHE` |
| `gltf`, `gltf-json`, `gltf-derive` 1.4.1 | `MIT OR Apache-2.0` | `LICENSE-MIT`, `LICENSE-APACHE` |
| Les neuf crates Symphonia 0.6.1 verrouillées | `MPL-2.0` | `LICENSE` dans chaque crate, notices dans les sources |

Les neuf crates sont `symphonia`, `symphonia-core`, `symphonia-common`,
`symphonia-metadata`, `symphonia-bundle-flac`, `symphonia-codec-pcm`,
`symphonia-codec-vorbis`, `symphonia-format-ogg` et `symphonia-format-riff`.
Le texte et la notice des sources portent notamment « Copyright (c) 2019-2026
The Project Symphonia Developers ». Cet inventaire n'est pas un audit des
dépendances transitives complètes ni un avis juridique.

Le futur pack applications devra transporter un inventaire de notices avec ces
versions et auteurs, le texte intégral `LICENSE` MPL-2.0, les notices originales
des sources Symphonia et les textes MIT/Apache retenus pour image/gltf. Pour
Symphonia, prévoir aussi les sources exactes des neuf crates 0.6.1 (archives
sources conservant leurs notices) et une indication lisible de leur accès, en
référence aux sections 3.1, 3.2 et 3.4 du texte MPL livré. Ces éléments concernent
les bibliothèques des applications ; ils restent distincts des preuves de licence
des assets utilisateurs. Ce complément n'ajoute pas encore ces fichiers au
script d'empaquetage et n'atteste pas la conformité d'un installateur distribué.

Les plafonds par défaut sont 256 Mio d'entrée par média, 64 × 1024² pixels,
256 Mio d'admission des données décodées, 28,8 millions de frames audio,
8 canaux, 192 kHz, 100 000 paquets, 8 millions de sommets, 24 millions d'indices
et 65 536 primitives. Les tables de codebooks partagent un budget cumulé ;
la prélecture Floor1 utilise des tableaux bornés. Ces plafonds ne constituent
pas une borne mesurée de la mémoire totale du processus ni de son temps CPU.

Des fixtures ont reproduit des accès/paniques de parsing : référence POSITION
hors table et type d'accessor incohérent dans gltf, soustraction sur un count ou
sparse count nul, puissance u32 non vérifiée dans la préparation Vorbis, et
index de livre Floor0 hors tableau. Les pré-gardes refusent ces entrées avant
les lecteurs concernés ; les tests distinguent refus attendu et absence de
panique. Le run local `assets-combined-green.log` confirme 41 tests, dont ces cas
count nul et Floor0. Les quatre nouveaux tests d'usages morph/animation/skin ont
ensuite reproduit des admissions injustifiées sans panique. Un cinquième a
reproduit un unwrap de propriété d'animation inconnue : les targets bruts sont
maintenant validés avant leurs getters. Le run local `glb-usage-green.log`
confirme ces cinq régressions en GREEN. Les fixtures à tables Vorbis gonflées testent le refus
du cumul avant allocation. Cela ne prouve ni l'absence de toute vulnérabilité
des décodeurs, ni la qualité artistique, ni une clôture de Gate 4.
