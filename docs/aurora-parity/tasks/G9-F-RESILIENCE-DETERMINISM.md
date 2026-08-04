# G9-F — Résilience et déterminisme

## Exigences

`ORF-MP-031`, `ORF-MP-032`, `ORF-MP-033`, `ORF-MP-034`, `ORF-MP-036`.

## Livré

- garde serveur de révision, séquence, cadence et vitesse avec score de violation;
- tickets cryptographiques de reconnexion, liés à la session et consommables une fois;
- preview et rollback compensatoire d'un checkpoint canonique;
- capacité bornée, file FIFO et réservations expirables;
- harnais déterministe de latence, perte, duplication et réordonnancement à deux clients.

## Invariants testés

Un client ne téléporte pas son personnage et ne rejoue pas une séquence. Un ticket volé ou expiré échoue. Un rollback ne réécrit pas l'histoire : il produit une nouvelle révision. La première entrée en file est la première promue. Deux exécutions du même profil de fautes ont le même digest.

