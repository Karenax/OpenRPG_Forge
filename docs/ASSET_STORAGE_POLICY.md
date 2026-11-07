# Assets : stockage, publication et clôture

Règle propriétaire du 27 octobre 2026. Ce document fixe le fonctionnement
opératoire ; [ADR-0058](ADR/ADR-0058-shared-vault-storage.md) fixe le format.
Il complète les consignes du 20 octobre sans modifier les verdicts historiques.

## Une destination par rôle

| Données | Destination | Conservation |
| --- | --- | --- |
| Sources, code, outils et compilations | E: ; cache Cargo commun `.build/cargo-target` | Selon leur usage courant |
| Travail de conversion | E:/OpenRPG_Forge/.build/asset-conversion | Lot stable, reprise minimale, clôture obligatoire |
| Modèles livrés, sources conservées, licences et fiches | F:/OpenRPG_AssetVault | Référence fonctionnelle, avec hashes et droits |
| Blocs partagés des modèles | Vault/.forge-storage | Données fonctionnelles indispensables |
| Preuves de développement et historique | Point d'entrée Preuves-et-analyses ; données physiques F: | ZIP64 vérifiés pour les campagnes closes |
| Gestion du stockage et reçus de retrait | Preuves-et-analyses/rapports sur E: | Petits rapports, listes et empreintes ; aucune copie de modèles |
| Sauvegarde | G: | E: ; conserver les liens F: sans parcourir leurs cibles |

Une conversion close ne produit pas une nouvelle archive de ses diagnostics.
Captures RenderDoc, dumps, rendus intermédiaires et copies de travail devenus
inutiles sont retirés après vérification des dépendances. Sources originales,
licences, provenance, IDs, hashes, profils, pivots, dépendances, retouches,
miniatures fonctionnelles et révisions encore référencées restent conservés.
Un fichier rempli de zéros n'est pas un fichier vide.

## Choisir ce qui mérite d'être conservé

**Pas de conservation durable des données inutiles ou reconstructibles.**
Un intermédiaire reconstructible peut rester temporairement disponible pour un
travail ou une reprise effective ; il doit être retiré lorsque cet usage cesse.
Ne pas remplacer sa suppression par une archive systématique. Conserver les
sources, outils et paramètres nécessaires à sa reconstruction, ainsi que les
données uniques et les métadonnées fonctionnelles. Le bilan final de nettoyage
doit distinguer les retraits effectués et les éléments encore conservés avec
leur usage concret ; une opération en cours ne doit pas être annoncée terminée.

La règle est l'utilité réelle, pas un nombre maximal de fichiers, un quota ou
la seule ancienneté. Ne pas conserver un essai simplement parce que ses octets
diffèrent de ceux de la version retenue. Un lien présent dans un ancien rapport
ne constitue pas, à lui seul, une dépendance fonctionnelle actuelle.

| Nature | À conserver | À retirer après clôture et contrôle des usages |
| --- | --- | --- |
| Asset et variantes utiles | Modèle retenu et révisions effectivement utilisées, sur F: | Copies de travail E:, essais rejetés ou explicitement remplacés sans dépendance actuelle |
| Sources et droits | Une source de référence, licences, provenance et retouches uniques | Réextractions et copies de packs faites uniquement pour un lot terminé |
| Données embarquées dans le GLB | Le GLB vérifié contenant géométrie, textures, shaders et autres ressources utiles | Caches de décodage et copies externes prouvées identiques aux données embarquées |
| Qualification d'une conversion | Résultat synthétique utile et métadonnées courantes nécessaires à l'usage | Captures RenderDoc, dumps GPU, images de comparaison, journaux détaillés et étapes d'essai devenus inutiles ; aucune nouvelle archive de ces traces |
| Travail non terminé | Entrées et dernier état nécessaires à sa reprise effective | Essais antérieurs remplacés qui ne participent plus à cette reprise |
| Historique technique | Révisions encore référencées et décisions utiles | Sauvegardes systématiques de chaque tentative, stages et révisions sans consommateur actuel |

Les fichiers mélangés dans un même dossier sont traités séparément : une
retouche unique à conserver ne justifie pas de garder tous les caches voisins.
Un élément ambigu reste conservé avec la raison précise ; cette raison doit
être un usage ou une dépendance identifiée, pas une conservation « au cas où ».

Exemple vérifié le 27 octobre : `reopen/verified-decoded-payload-store/*.bin`
contient les payloads extraits des GLB pour leur réouverture indépendante. Les
résultats lient chaque payload à son SHA-256, sa taille et son GLB. Après succès
du lot et contrôle du GLB conservé, ces fichiers sont des caches régénérables,
pas des sources uniques. Ils doivent être retirés, sans ZIP supplémentaire.

Pour les nouveaux adaptateurs, utiliser un répertoire temporaire dont la durée
de vie est celle de la réouverture, puis le libérer après succès. Les échecs ne
conservent que les éléments nécessaires au diagnostic encore ouvert. Ne pas
créer un nouveau dossier par relance si ses entrées et sorties réutilisables
sont identiques. À la clôture, le lecteur utilise le modèle final et ses
métadonnées ; il ne doit pas exiger la conservation perpétuelle des buffers de
chacune de ses étapes de conversion. Les anciens reçus restent historiques,
sans être réécrits pour simuler une nouvelle vérification.

Implémentation commune : `scripts/assets/verified_payload_store.py`. Une même
ressource est matérialisée une seule fois pendant son utilisation, puis les
copies créées par ce stockage sont retirées à la fermeture du consommateur ou
à la sortie normale du worker, y compris après une exception Python. Un fichier
modifié ou inconnu est préservé. Un arrêt forcé peut laisser un cache à examiner.

Le pilote partagé `deterministic_hivemind.py run` sélectionne ce stockage pour
les nouveaux plans E: de réouverture non exécutés et non scellés. Les anciens
plans restent inchangés. Un autre préparateur peut faire explicitement, avant
scellement et première exécution :

```powershell
python -B scripts/assets/prepare_reopen_storage.py --manifest NOUVEAU-MANIFEST.json
```

Cette correction traite le cache de décodage ; elle ne prétend pas avoir déjà
adapté tous les anciens producteurs de captures et tampons GPU. Appliquer la
même analyse de durée de vie à leurs sorties, sans supprimer leurs dépendances
encore nécessaires ni substituer une limite numérique à ce tri.

## Cycle obligatoire d'un lot

1. **Préparer** : réutiliser le même lot et les entrées immuables. Identifier le
   rôle et le dernier consommateur de chaque grosse sortie avant production.
   La conservation dépend de l'utilité des données, sans quota arbitraire de
   volume ou de nombre de fichiers.
2. **Convertir sur E:** : passer explicitement `--work-root
   E:/OpenRPG_Forge/.build/asset-conversion` au pilote déterministe. Vérifier
   ses variables héritées. Ne pas recopier les anciens lots F: pour les reprendre.
3. **Livrer sur F:** : vérifier intégralement le transfert avant tout retrait
   E:. Les chemins utilisés par Studio/catalogues doivent viser le Vault, jamais
   un dossier temporaire E:. Publier les métadonnées avec
   `scripts/assets/publish_assets.py`, qui conserve les contrôles de l'importeur
   canonique et traite la mutualisation ou la met dans sa file SQLite.
   Le petit stage de métadonnées reste à l'intérieur du Vault, exigence de
   confinement de l'importeur ; il ne doit pas contenir une seconde bibliothèque.
4. **Qualifier et fermer** : vérifier les assets et références fonctionnelles.
   Un stage terminé ne signifie pas que tout le lot est clos. Garder le minimum
   de reprise des travaux en cours, interrompus ou suspendus. Une variation de
   matériau, de licence ou d'ID ne constitue pas un doublon à fusionner.
5. **Retirer les copies inutiles** : figer les chemins exacts et leur rôle,
   comparer le contenu SHA-256 à la copie conservée, contrôler les processus,
   références et changements depuis l'inventaire. Retirer uniquement les fichiers
   justifiés puis les dossiers réellement vides. Consigner le gain libre observé
   séparément du volume logique ; pas de ZIP supplémentaire pour ces traces.

Un lot dont les sorties ont été retirées est **retiré**, pas réutilisable comme
cache de reprise. Son reçu de retrait fournit l'emplacement conservé et le hash.
Ne pas relancer aveuglément son ancien manifeste : préparer un nouveau travail
borné depuis la bibliothèque ou depuis les sources conservées si une conversion
différente est nécessaire. Les reçus historiques restent inchangés.

## Mutualisation et compatibilité

La bibliothèque conserve ses identités et chemins logiques. Un modèle physique
peut être une enveloppe `ORPVLT1`, dont les segments identiques sont partagés et
compressés sans perte. Les lecteurs Forge restituent exactement le GLB initial.
Les catalogues conservent le SHA-256 **logique**, jamais le hash de l'enveloppe.
Ne jamais copier une enveloppe hors du Vault pour la traiter comme un GLB autonome.

Employer `forge_delivery::vault_storage` ou le lecteur Python compatible ; pour
Blender ou un autre outil externe, exporter explicitement un vrai GLB temporaire
sur E:, puis le retirer après son usage. Les distributions restent autonomes.
Ne pas dédupliquer des variantes métier ni remplacer une copie modifiable par
un lien physique partagé. Les packs, l'index et les manifests de `.forge-storage`
ne sont jamais purgés comme un cache. La migration progressive ne vaut pas
qualification visuelle, indexation Studio ou achèvement de toute la bibliothèque.

## Outil de retrait contrôlé des GLB de travail

`scripts/assets/retire_work_duplicates.py` exploite un inventaire/hachage E:
réutilisable ; il ne parcourt pas F:. Deux phases distinctes produisent un plan
figé puis appliquent ses retraits. Il refuse les jonctions, les worktrees Git,
les sources, les états incomplets et les références
courantes dans le code ou les manifestes de jobs incomplets. Une copie sur E:
est relue intégralement avant retrait, sous verrou interdisant les écritures
concurrentes sur Windows. La copie conservée reste verrouillée contre écriture
et remplacement jusqu'au retrait de son doublon. Les chemins conservés dans
le plan sont protégés.

Pour F:, le plan peut réutiliser le reçu d'une reconstruction intégrale Python
**et** Rust déjà effectuée par le migrateur. L'application contrôle l'enveloppe
exacte épinglée par ce reçu et le hash logique ; elle ne prétend pas effectuer
une seconde reconstruction intégrale. Cela évite un second balayage du HDD.
Un ancien reçu seul, sans représentation conservée inchangée, ne suffit pas.
Un fichier ambigu ou modifié est exclu, jamais forcé.

```powershell
python -B scripts/assets/retire_work_duplicates.py plan --hashes HASHED.jsonl --receipts VERIFIED-F-RECEIPTS.json --output PLAN.json
python -B scripts/assets/retire_work_duplicates.py apply --plan PLAN.json --output RESULT.json
```

Les entrées SHA-256 du hachage E: comportent `path`, `bytes`, `mtime_ns`,
`job`, `sha256`, `state=hashed`. Le fichier des reçus regroupe les lignes
`state=complete` du journal de migration par taille. Les petits résultats JSON
et le journal durable de retrait vont sous le point d'entrée central. La
conservation repose sur l'usage, pas sur l'âge ou le nom d'un fichier de suivi.
Pour une sortie ponctuelle sans STATE ou reçu de livraison local, la preuve
indépendante que le modèle exact est conservé sur F: suffit au contrôle de la
réplique, sous réserve d'absence d'usage courant et de toute la vérification
d'enveloppe/contenu. Les arbres de sources originales restent exclus. Un état
explicitement incomplet n'est pas contourné. Retirer une copie transférée ne
clôture pas les qualifications métier encore ouvertes.

Pour la campagne du 27 octobre, `scripts/assets/finish_work_retirement.py`
complète une seule fois le hachage du même inventaire E:, puis retire les copies
éligibles au fil des reçus complets de la migration déjà lancée. Il n'attend plus
la migration de toute la bibliothèque pour décharger E:. Chaque passe recontrôle
références et processus. Il ne lance ni nouvelle migration ni scan F:, ne tue
aucun processus et s'arrête sur interruption/changement de plan. Le fichier
`finish-state.json` central donne sa phase ; `incremental-plan.json`,
`incremental-retirement.json`, puis `final-plan.json`, `final-retirement.json`
et le journal donnent les retraits et exclusions. Un chemin déjà retiré n'est
pas retraité s'il a été restauré volontairement. Ce traitement ponctuel porte
sur l'inventaire figé ; il ne remplace pas les contrôles des nouveaux lots.

## Contrôle de croissance à chaque clôture

La clôture doit indiquer le volume de travail restant, les copies retirées ou
leurs motifs de conservation, le nombre de fichiers et les chemins fonctionnels
conservés. Reprendre le même cache Cargo, les binaires identiques et les ressources
immuables. Ne pas garder les anciens symboles complets par défaut : ils sont
recréables ; réserver les symboles utiles au débogage courant.

Il n'existe pas de purge périodique par âge. Le pilote de publication mutualise
les nouveaux modèles ; le retrait des sorties de conversion nécessite toujours
ses contrôles de clôture et son plan. Les outils ne garantissent pas qu'un script
externe qui contourne ce parcours ne recréera jamais de copies. Toute nouvelle
voie de conversion/publication doit appliquer ce contrat avant son utilisation.
