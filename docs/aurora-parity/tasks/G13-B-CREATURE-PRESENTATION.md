# G13-B — Identité et présentation des créatures

## Périmètre

- `ORF-CRE-006` — sexe, genre et pronoms indépendants des règles mécaniques ;
- `ORF-CRE-007` — portraits multi-résolution et expressions avec licence, hash et fallback 3D ;
- `ORF-CRE-009` — parties du corps modulaires avec rig, ordre, masquage et clipping ;
- `ORF-CRE-010` — paramètres de couleur et attachments génériques, dont ailes et queue ;
- `ORF-CRE-029` — profil voix avec banques, émotions, langue, lipsync, variantes et droits.

## Contrat livré

`CreaturePresentationProfile` est une donnée canonique v1 portée par le blueprint de créature. Elle
est indépendante de Godot, possède un UUID et une provenance, et traverse directement les formats
projet `0.6.23` et workspace `1.17.0`. Les anciennes versions `0.6.22` et `1.16.0` migrent avec une
présentation absente et un fallback 3D sûr.

`SetCreaturePresentationHandler` impose révision, type de blueprint, lecture seule, validation,
UUID unique, dry-run, patch, inverse, audit et historique projet. Le validateur produit des codes et
chemins stables pour identité, portraits, rig, slots, couleurs, attachments et voix.

Le Studio de créature expose ces données sans édition JSON. Il charge un portrait réel, calcule son
SHA-256 lorsque le fichier est local, affiche un avatar 3D éclairé et matérialise couleur, ailes et
queue. Le viewport Creator et le runtime utilisent le même profil pour leurs représentations de
secours. Une présentation optionnelle absente reste jouable ; une présentation déclarée requise
mais absente produit un diagnostic.

L’export `.orpmod` vérifie avant écriture que chaque portrait reste sous le workspace, existe et
correspond au SHA-256 déclaré. Le portrait et le profil sont ensuite couverts par le hash du package.

## Preuves

- domaine : `CreaturePresentationProfileTests` — 3 tests ;
- commande : `SetCreaturePresentationHandlerTests` — 2 tests ;
- sérialisation et package : 4 tests ciblés, dont migration, round-trip, inclusion et refus d’un
  portrait absent ;
- build `OpenRPGForge.sln` — zéro warning, zéro erreur ;
- smoke Godot : `OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_PASS` ;
- non-régression jouable : le même smoke atteint `OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS` et conserve
  le hash du module source.

## Limites explicites

- Les IDs de banques audio, pièces de corps et attachments sont des références canoniques ; leur
  résolution par un catalogue spécialisé sera enrichie avec les lots équipement/audio suivants.
- Les proxies procéduraux rendent ailes et queue lorsque l’asset final n’est pas résolu. Ils ne
  remplacent pas un GLB riggé et skinné produit par le pipeline d’assets.
