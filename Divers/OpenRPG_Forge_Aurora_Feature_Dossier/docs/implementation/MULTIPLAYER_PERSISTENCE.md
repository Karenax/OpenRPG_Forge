# Multijoueur et persistance

## Première cible

Deux à quatre clients sur un serveur headless chargent la tranche verticale, se déplacent, interagissent, combattent et partagent une quête selon une policy explicite.

## Autorité

Le serveur possède : entités, inventaires, quêtes, RNG, économie, rencontres, scripts et transitions. Le client possède seulement l’intention d’entrée, l’UI et la prédiction autorisée.

## Persistance

Le domaine utilise des repositories et transactions. ForgeScript ne reçoit pas de SQL brut par défaut. Les providers peuvent cibler SQLite pour local et PostgreSQL/MariaDB pour serveur, sans changer le contrat métier.

## Contenu

Le serveur annonce un fingerprint de packages. Le client ne rejoint qu’après vérification/synchronisation. Les packages non fiables sont isolés et signés facultativement.
