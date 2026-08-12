# G9-C — Réplication, intérêt, contenu et identité

## Exigences

`ORF-MP-011`, `ORF-MP-012`, `ORF-MP-013`, `ORF-MP-015`, `ORF-MP-016`.

## Livré

- snapshots de composants et deltas révisionnés sous autorité serveur;
- filtrage d'intérêt par instance, rayon, pertinence permanente ou abonnement explicite;
- manifestes de contenu canoniques avec SHA-256 et liste des différences;
- verrouillage strict du protocole, de la version du module et du fingerprint;
- comptes normalisés, sel aléatoire, PBKDF2-SHA256 et comparaison en temps constant.

## Sécurité vérifiée

Une écriture de réplication sans autorité lève `NET_REPLICATION_AUTHORITY_REQUIRED`. Une ressource altérée produit `NET_CONTENT_SYNC_REQUIRED`. Un login inconnu et un mauvais mot de passe exposent le même code public `NET_AUTH_INVALID_CREDENTIALS`.

R102 complète la réplication par politiques de composants, budget de delta, ownership,
prédiction/réconciliation et intérêt combinant zone, distance, visibilité, groupe, propriété et
abonnement explicite. Les contrôles restent sous autorité serveur et sont exposés par le même profil
canonique au Toolset, runtime, ForgeScript et MCP.

R103 ferme la distribution de contenu : manifeste signé, chunks bornés, hash, cache adressé par
contenu, reprise, delta, licences, quota et politique de confiance partagent maintenant le profil
`NetworkOperationsProfile`. Le lockfile strict négocie module, protocole et fingerprint avant join
et retourne un rapport de mismatch lisible. Le Toolset, le runtime, ForgeScript, MCP et le serveur
utilisent les mêmes commandes et diagnostics.
