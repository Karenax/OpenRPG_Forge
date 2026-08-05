# G9-E — Stockage, événements et observabilité

## Exigences

`ORF-MP-023`, `ORF-MP-027`, `ORF-MP-028`, `ORF-MP-029`, `ORF-MP-030`.

## Livré

- stockage derrière `ISecureDataStore`, opérations nommées et paramètres exacts typés;
- bus serveur séquencé, corrélé, historisé et protégé contre le rejeu;
- compteurs, jauges et health checks composés;
- logs JSON structurés, corrélation et redaction automatique des clés sensibles;
- profilage par scopes avec agrégats de durée et échecs, sans contenu gameplay.

## Invariants testés

Les chaînes SQL libres sont refusées. Les paramètres manquants, surnuméraires ou de mauvais type ne s'exécutent pas. Un événement ne peut être publié deux fois. Les tokens ne figurent jamais dans le JSON. Le profiler ne conserve que des agrégats bornés.
