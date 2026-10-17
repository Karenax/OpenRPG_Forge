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
Les modules avec des `SourceBlob` sont refusés avant la création du staging :
aucune suppression ou conversion implicite de ces sources n’est permise.

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
`read_export_origin` et `build_zone_with_origin`. Ce chemin préserve les
documents d’origine et refuse la publication d’un module d’origine privée,
même si ses médias et les nouvelles retouches sont déclarés en CC0. Ces
déclarations et contrôles techniques ne valent ni avis juridique ni validation
artistique, et ne constituent pas à eux seuls une clôture de gate.
