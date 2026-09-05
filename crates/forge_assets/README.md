# forge_assets

Contrats de catalogue, provenance et intégrité d'assets. Gate 0 utilise uniquement des recettes
procédurales déterministes ; le validateur `validate_procedural_only` refuse toute référence externe.

Le module public `browser` expose des DTO natif/WASM pour le snapshot
`forge.asset-browser-snapshot` v1 ; son scanner filesystem reste exclusivement natif. Il dérive le
snapshot à la demande, ne persiste rien, ne télécharge rien et ne suit aucun lien : seul un fichier
nommé exactement `subset.manifest.json` est désérialisé. Le scanner vérifie l'inventaire
byte-for-byte, les tailles, les SHA-256, la preuve de licence et la provenance avant d'exposer des
entrées triées par `AssetId`. Le sous-ensemble doit aussi correspondre au registre immuable compilé
par Forge : identifiants, manifeste brut, archive, métadonnées normalisées, licence et preuve de
licence sont épinglés indépendamment des affirmations du manifeste scanné.
Un répertoire sans manifeste est une erreur explicite. Les identités stables sont dérivées de
`source.pack_id/model.id` normalisés en minuscules, indépendamment du nom local du sous-ensemble.

Le manifeste de preuve actuel contient 9 modèles, 26 fichiers de contenu et une preuve de licence
distincte (27 déclarations `files` vérifiées au total), sous un unique manifeste.

La traversée est bornée à 6 niveaux, 256 répertoires, 32 manifestes, 512 modèles et 4 096 fichiers.
Un manifeste est limité à 256 Kio (4 Mio agrégés), un fichier à 128 Mio (512 Mio agrégés), et les
chemins portables à 256 octets. Les chemins absolus, `..`, `.`, antislashs, liens/reparse points et
composants contenant `Cadle` sont refusés. Chaque scène JSON glTF est parsée sous budget ; ses URI de
buffers et d'images doivent être locales et présentes dans l'inventaire vérifié. GLB est refusé en v1.

Le scanner R2-026 reçoit uniquement la racine compilée du checkout et suppose cette racine fiable et
stable pendant l'appel ; il ne constitue pas une frontière contre une mutation concurrente hostile.
Le raccordement à une racine d'installation autonome appartient à Gate 4. Ces contrats n'altèrent pas
le catalogue procédural Gate 0.
