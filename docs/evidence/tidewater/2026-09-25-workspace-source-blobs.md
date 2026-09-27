# Tidewater — conservation des sources binaires dans le workspace

`workspace::source_blobs::SourceBlobStore` conserve les sources dans
`source-blobs/<sha256>.bin` sous l'autorité de workspace fournie par l'application.
Le stockage limite chaque objet à 64 MiB, vérifie taille/hash, refuse les liens,
écrit un staging exclusif, le synchronise puis publie par hard link sans remplacer
un objet existant. Une corruption existante est signalée et jamais écrasée.
Comme le magasin d'archives existant, cela suppose des processus locaux coopérants ;
ce n'est pas un sandbox contre le remplacement hostile concurrent des répertoires.

`ZoneEditSession::required_source_blobs` inventorie les références de relief et
d'instances dans le checkpoint, le head et les deux bases de chaque régénération.
Le magasin d'objets de zone vérifie ces sources avant publication et lecture :
un ancien bloc nécessaire à l'historique ne peut pas manquer silencieusement.
Les archives JSON existantes et leurs hashes ne changent pas.

## Preuves

`E:/OpenRPG_Forge/.tidewater-integration/source-store-verification-001.json` :
- destination neuve `workspace-source-store-001` ;
- 11 blocs, **26715432 octets** : raster et dix couches de végétation ;
- **65383 instances** décodées après réouverture depuis la destination seulement ;
- archive de zone `3b37a4c9e5203193e7655084a8b4c0ee8bed5f467ad7751e24d2eee2d27edba4`.

Sept tests passent : conservation des sources actuelles/historiques, absence ou
corruption bloquante, republication identique, absence d'écrasement ; deux tests
du magasin d'objets existant ; adoption (deux), création et récupération du workspace.
Clippy projet/exemple passe avec `-D warnings`.

## Limites

Le magasin du workspace est raccordé aux objets de zone, mais l'import Studio,
le transport des blobs lors d'une adoption provenant d'un autre workspace,
l'export portable des modules et le chemin historique `FileZoneSession` restent
à raccorder. Aucune interface ni parité visuelle n'est qualifiée ici. La vérification
de stockage porte sur les octets référencés ; les décodeurs métier restent
responsables de la validité géométrique au chargement. Le nettoyage des objets
orphelins et les performances de vérification de longs historiques restent ouverts.
