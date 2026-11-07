# ADR-0039 — Sources immuables des modules et historique auteur

Date : 23 octobre 2026. Statut : adopté pour la réalisation de Gate 7.
La qualification du parcours Studio, de la distribution et des OS reste distincte.

## Besoin

Le magasin auteur impose déjà toutes les sources référencées par une archive :
checkpoint, état courant et deux côtés des régénérations conservées pour undo/redo.
L'export Studio refuse encore les blobs. L'API de module 1 déclare uniquement les
sources de la tête courante ; y ajouter silencieusement les sources historiques
ferait rejeter un nouveau module par les anciens lecteurs malgré la même API.

## Décision

L'API de module **2** définit `ModuleZone.source_blobs` comme la fermeture exacte
de l'archive auteur, calculée par `ZoneEditSession::required_source_blobs`.
L'API 1 garde sa définition historique, limitée à la tête courante. Le lecteur
actuel admet explicitement les API 1 et 2, sous la version moteur déclarée ;
une API inconnue est refusée. Les nouveaux exports annoncent l'API 2. Les schémas
de manifeste v2/v3, les documents, identités et anciennes archives ne sont pas réécrits.
La compatibilité avec un lecteur plus ancien n'est pas déduite d'un schéma JSON identique.

Le snapshot natif d'export possède les octets lus dans le magasin de l'autorité
ouverte, sans chemin fourni par le WebView. Chaque blob est vérifié par taille
et SHA-256 et partagé entre zones par hash. Les archives et leurs sources sont
bornées ensemble à 256 Mio ; le Studio contrôle aussi son budget d'entrées
d'export avec les assets. Ces octets ne sont ni sérialisés vers le WebView ni
ajoutés à une nouvelle source de vérité. Le hash du snapshot reste celui des
archives et du profil, qui épinglent déjà chaque hash de blob.

Le builder préserve les archives exactes, les liens d'origine, les conditions de
droits et les sources historiques. Les fichiers `source_blob` sont déclarés une
seule fois, sous `_forge-export/source-blobs/<sha256>.bin`, et chaque zone nomme
sa fermeture exacte. Source absente, altérée, surnuméraire, collision ou budget
dépassé : refus avant publication. Aucun fichier du magasin n'est réparé implicitement.

Le runtime borne les sources de la zone sélectionnée à 256 Mio avant leur
allocation. Il réutilise le loader, le renderer et les sources canoniques ;
l'API 2 ne rend pas jouables des catalogues ou végétations encore refusés par
les autres contrats. Le bootstrap, les entrées Studio et la livraison complète
doivent conserver leurs vérifications et leurs preuves propres.

## Vérification

Exiger fermeture courante et historique, déduplication entre zones, pins et
tailles exactes, refus sans sortie, stabilité des sources, export/relecture,
admission d'API 1 inchangée et rejet d'une API inconnue. Puis exporter et lancer
un vrai module raster par le parcours Studio, sans assimiler les tests du builder
à une qualification de l'interface, de la machine propre ou de Gate 7 entière.
