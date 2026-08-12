# G9-E — Stockage, événements et observabilité

## Exigences

`ORF-MP-023`, `ORF-MP-027`, `ORF-MP-028`, `ORF-MP-029`, `ORF-MP-030`.

## Livré

- stockage derrière `ISecureDataStore`, opérations nommées et paramètres exacts typés;
- bus serveur séquencé, corrélé, historisé, prioritaire, tracé, protégé contre le rejeu et borné par
  backpressure;
- compteurs, jauges et health checks composés;
- logs JSON structurés, corrélation et redaction automatique des clés sensibles;
- profilage par scopes avec agrégats de durée et échecs, sans contenu gameplay.

## Invariants testés

Les chaînes SQL libres sont refusées. Les paramètres manquants, surnuméraires ou de mauvais type ne s'exécutent pas. Un événement ne peut être publié deux fois. Les tokens ne figurent jamais dans le JSON. Le profiler ne conserve que des agrégats bornés.

R103 ajoute les abonnements typés configurables, l’ordre déterministe par priorité, une trace par
handler et une file réentrante bornée. Le scénario serveur prouve l’ordre gameplay avant audit et le
refus `NET_EVENT_BACKPRESSURE_REJECTED` sans accepter le second événement débordant.

R104 ferme `ORF-MP-029` avec une politique auteur canonique pour les sorties JSON et texte, les
catégories, identifiants de corrélation, filtres de confidentialité, rétention, rotation bornée et
sinks distants nommés. Le scénario serveur prouve le masquage des secrets, deux segments et deux
livraisons vers l'adaptateur distant ; UI, ForgeScript, MCP et runtime relisent le même profil.
