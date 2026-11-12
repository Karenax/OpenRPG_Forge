# Ressources partagées du Vault

Contrat opératoire obligatoire : [stockage et clôture des assets](../../docs/ASSET_STORAGE_POLICY.md).
Il couvre E:/F:/G:, la publication, les copies de travail et leur retrait vérifié.
Ne pas conserver un modèle livré sur E: par défaut ; justifier son usage encore
fonctionnel ou le retirer après comparaison et contrôle des références.
Les caches de décodage des nouveaux plans de réouverture utilisent
`verified_payload_store.py` : copie partagée pendant l'usage, retrait après
consommation. `prepare_reopen_storage.py` ne modifie que des plans non exécutés
et non scellés ; le pilote partagé l'applique aux nouveaux `run` concernés.
Il n'y a aucun quota arbitraire de fichiers ou de volume ajouté.

Voir [ADR-0058](../../docs/ADR/ADR-0058-shared-vault-storage.md). Les lecteurs
et le migrateur sont implémentés ; une bascule physique exige le reçu de
déploiement compatible. F: garde les données, E: les outils et temporaires.

## Format et lecture

GLB bruts et modèles compactés peuvent coexister. Un fichier compacté garde
son chemin logique mais contient `ORPVLT1`, pas un GLB physique. L'enveloppe
référence des segments par SHA-256, partagés dans des packs immuables de
256 Mio sous `.forge-storage/packs`. Les segments suivent les vues GLB et
sont découpés à 4 Mio ; le codage est brut ou zlib, sans perte.

`forge_delivery::vault_storage` restitue le GLB exact avant transport
Studio/Web et validation d'assets. IDs, variantes, provenance, licences,
pivots, profils et retouches restent distincts. Les catalogues gardent le
hash du GLB logique : ne jamais le remplacer par celui de l'enveloppe.

Les exports Studio et `forge_delivery pack*` produisent les vrais fichiers
autonomes et excluent les fichiers techniques `.forge-storage`. Pour ouvrir
un modèle dans un outil externe, exporter d'abord le GLB :

```text
vault_storage.exe export VAULT MODEL OUTPUT.glb LIMIT_BYTES
python scripts/assets/vault_storage.py verify VAULT MODEL
```

Le programme natif est l'exemple `vault_storage` du manifeste
`tools/forge_delivery/Cargo.toml`, construit dans le cache partagé. OUTPUT
doit être absent. La limite de stockage suit l'enveloppe GLB u32 ; les budgets
du produit (512 Mio, ou Desktop explicite de 1 Gio) restent inchangés.
La compaction ne qualifie jamais un asset.

## Publication des nouveaux lots

Préparer et vérifier avec l'importeur canonique, puis appliquer avec le pilote
du projet, qui conserve tous ses contrôles :

```text
python scripts/assets/publish_assets.py --vault F:/OpenRPG_AssetVault --stage STAGE
```

Il applique d'abord les métadonnées canoniques. La compaction prend ensuite
les modèles publiés et leurs pins dans une seule file SQLite. Si un migrateur
travaille, la publication reste accessible et le travail attend dans cette
file. Sinon le pilote la traite avec le lecteur natif qualifié. Un échec de
stockage est explicitement distinct d'une publication déjà appliquée.
Avant l'application, le pilote refuse une étape qui réintroduit un identifiant
historique consolidé ou qui retire ses métadonnées du modèle conservé. L'étape
doit être préparée de nouveau avec ses références préservées. L'importeur
canonique installé effectue également ce contrôle sous son verrou, y compris
lors d'un appel direct de `asset_import.py apply`.
Lors de la préparation, les entrées déjà consolidées sont ignorées et leurs
identifiants sont consignés dans `superseded_asset_ids` du plan. Pour une fiche
conservée et réimportée avec le même SHA, ses références historiques sont
reprises. Si son modèle a changé, la préparation demande une migration explicite.
Un catalogue composé uniquement d'identifiants déjà consolidés ne produit pas
de nouvelle étape. Les contrats source restent vérifiés sur le catalogue original.
Le retour arrière refuse désormais une révision qui supprimerait des références
consolidées, recréerait une ancienne identité ou désignerait un modèle absent.
Une restauration ou migration explicite est nécessaire avant un tel retour ; les
révisions et leurs données restent intactes.
Sans reçu de déploiement, aucune compaction n'est lancée.
Le reçu doit épingler les chemins et SHA-256 du Studio, du lecteur CLI et du
lecteur Python, ainsi que le SHA-256 du migrateur : leur remplacement rend le
reçu précédent périmé jusqu'à une nouvelle qualification des lecteurs.
La publication canonique reste distincte ; son optimisation est différée si
le reçu est absent ou périmé. Ne pas réécrire un reçu historique pour le faire
correspondre au nouveau binaire.
Le résultat expose `work_retirement=required` et `closure=open` même après une
publication réussie : contrôler et justifier les copies de travail avant de
clôturer le lot selon le contrat. La publication ne purgera jamais implicitement
des sources ou des répertoires mixtes.

Les modèles trop petits et les lots gagnant moins de 1 % ou 1 Mio restent
bruts. Le calcul inclut enveloppes, packs, index et manifests de secours.

## Migration et reprise

Le plan réutilise l'inventaire SQLite central et fige chemins, tailles et
dates, sans reparcourir tout F:. Sources conservées et licences sont exclues.
Un seul écrivain est admis ; les jonctions sont refusées. Sous Windows,
les fichiers déjà ouverts en écriture sont exclus et les sources préparées
sont protégées pendant la transaction.

Les packs sont publiés et relus intégralement par Python et Rust avant
retrait des originaux. `ReplaceFileW` conserve une sauvegarde transactionnelle
jusqu'à son contrôle SHA-256. Le journal de gestion est un JSONL central ;
les reçus F: ne persistent que pour les transactions inachevées.

```text
python scripts/assets/vault_storage.py apply --plan PLAN.json --verify-cli READER.exe --readers-receipt RECEIPT.json --report REPORT.json
python scripts/assets/vault_storage.py recover VAULT TRANSACTION.json --verify-cli READER.exe
python scripts/assets/vault_storage.py apply --resume --plan PLAN.json --verify-cli READER.exe --readers-receipt RECEIPT.json --report REPORT.json
```

Vérifier identité et existence du processus avant de traiter un ancien verrou.
Ne pas l'effacer sur son seul âge. Contrôler les transactions inachevées avant
reprise ; ne pas lancer une seconde migration concurrente.

L'index conserve aussi les manifests dans un même fichier SQLite. Une
restauration explicite d'une enveloppe manquante est possible sans écraser
de fichier existant et après reconstruction intégrale vérifiée :

```text
python scripts/assets/vault_storage.py restore-descriptor VAULT MODEL SHA256 --verify-cli READER.exe
```

**`.forge-storage` contient des données fonctionnelles, ce n'est pas un cache.**
Ne pas purger ses packs, son index ou ses manifests selon leur âge.

## Bornes et preuve

Le plan du 27 octobre vise 20 675 GLB d'au moins 1 Mio, environ 832 Go, dans
`normalized`, `candidates` et `animation`. Ce volume existe déjà dans le Vault ;
il ne s'agit pas d'une campagne de copies supplémentaires. Les originaux sont
renommés temporairement. Les lots sont bornés à 16 modèles, environ 2 Gio
plus le dernier modèle : environ 8 Gio de surcroît transitoire borné, hors index
fonctionnel final, avec une réserve libre de 16 Gio.

Le pilote sur dix références privées non qualifiées runtime mesure environ
89 % de réduction et dix reconstructions exactes. Ne pas extrapoler à toute
la bibliothèque. Les mesures de hachage à chaud ne sont ni un benchmark HDD
à froid ni une qualification de rendu. Les tests couvrent corruption, budgets,
chemins, source remplacée, écritures actives, interruptions, reprise, nouvelle
publication pendant migration, restauration et exports autonomes.

Rapports : `Preuves-et-analyses/rapports/mutualisation-vault-20261027`.
Journaux de gestion : `Preuves-et-analyses/rapports/mutualisation-vault-20261027`.

## Sources Unity partagées

Le registre `F:/OpenRPG_AssetVault/metadata/unity-shared-overlay-registry.v1.json`
référence les manifests épinglés des arbres Unity partagés. Les outils compatibles
utilisent `UnitySourceResolver` pour lire les chemins logiques et vérifier les SHA.
Pour un outil externe ou un projet Unity exigeant des fichiers ordinaires,
`unity_shared_overlay.py materialize` reconstruit l'arbre décrit par un manifest
dans un nouveau répertoire directement sous `.build/asset-conversion` ; `cleanup`
retire ensuite uniquement l'export inchangé. Cette reconstruction concerne
l'extraction décrite dans le manifest, pas nécessairement un projet Unity complet.

## Dépendances partagées des lots candidats

Les lots candidats enregistrés utilisent le même registre d'overlays. Le lecteur
Python `CandidateSharedReader.from_package(vault, package)` résout un chemin
logique depuis le manifeste épinglé et vérifie le SHA avant lecture. Il peut
également vérifier un catalogue `files`, un catalogue Viking et une portion
bornée de ressource binaire. Exemple de contrôle d'un catalogue enregistré :

```powershell
python -X utf8 -B scripts/assets/candidate_shared_reader.py `
  --vault F:/OpenRPG_AssetVault `
  --package candidates/hivemind/rural-town-hdrp-lantern-assembly-v1
```

Pour un outil qui exige un dossier autonome, `materialize_candidate_overlay.py`
accepte `--vault`, `--package`, `--work-root`, `--destination` et `--receipt`.
Il reconstruit les dépendances à leurs chemins logiques et exporte les modèles
en vrais GLB sur le disque de travail. La même commande avec `--cleanup` et le
reçu d'export vérifie chaque fichier avant de retirer cet export temporaire.
Lorsque le catalogue mentionne un modèle historique déjà retiré, l'exporteur
le reconstruit uniquement si le registre actif contient une identité historique
et une recette de blocs correspondant au SHA du catalogue. Chaque bloc et le
GLB reconstitué sont vérifiés ; une recette absente ou incohérente est refusée.

Ces points d'entrée sont explicites : un ancien outil qui ouvre directement un
chemin dans un lot n'utilise pas automatiquement l'overlay. L'enregistrement
d'un manifeste ne prouve pas la migration de ces consommateurs et n'autorise
pas, à lui seul, le retrait de leurs fichiers physiques.
