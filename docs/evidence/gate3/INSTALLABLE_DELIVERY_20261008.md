# Preview installable — preuves locales du 8 octobre 2026

Demande propriétaire : launcher, éditeur, jeu, assets séparés et préparation du futur client.
Livraison finale : `E:/OpenRPG_Forge/Forge-Installable-Preview-20261008-r2`.
Version : `0.1.0-preview.20261008.2`. Aucun commit, push ni envoi distant effectué.

## Résultats

| Contrôle | Résultat et limite |
|---|---|
| Moteur installer Rust | 8 tests PASS : corruption, chemins, inventaire, verrou, plans périmés, retry, versions, rollback, données utilisateur, dépendances et arguments natifs |
| Builds | CLI et hub Tauri optimisés, offline ; Clippy strict sur les deux workspaces PASS |
| Installation réelle | PASS, trois archives indexées, vérification complète, Prepare sans écriture, Apply idempotent |
| Sources éditables | 27 fichiers copiés dans un workspace utilisateur isolé ; originaux préservés |
| Launcher installé | Processus natif vivant après 8 secondes ; pas d'acceptation visuelle déduite |
| Studio installé | Processus natif vivant après 8 secondes ; candidat 075 et preuve de build épinglés |
| Jeu installé | Scène et contrôles fonctionnels PASS, backend stable, ombres PASS ; test global FAIL sur cadence |
| Prérequis | Microsoft WebView2 x64 hors ligne fourni, signature Microsoft `Valid`, SHA-256 épinglé ; détection sur le poste actuel PASS, installation sur Windows propre non testée |
| Bibliothèque | 108 233 fichiers / 485 634 632 475 octets ; 57 lots planifiés, pas tous exportés |
| Catalogue séparé | Export privé PASS, 2 249 fichiers / 1 993 010 950 octets ; aucun modèle normalisé inclus |

Jeu : reçu `game.walk.json`, `functional_pass=true`, `backend_stable=true`,
`shadow_cadence_pass=true`, `cadence_pass=false`, `pass=false`, exit 1.
1920×1080 : moyenne 39,13 FPS, 1 % bas 19,60 FPS, p95 29,18 ms ; le critère
non throttlé exige au moins 60 FPS. Aucun seuil abaissé, mode throttlé substitué
ou qualification transférée depuis un autre exécutable. Deux captures de vent
sont produites et la première a été inspectée ; elles ne qualifient pas le RPG
ni les entrées clavier/souris ou la validation humaine.

La confirmation humaine du propriétaire déjà documentée dans
[GATE3_OWNER_HUMAN_VALIDATION_20261008.md](GATE3_OWNER_HUMAN_VALIDATION_20261008.md)
concerne le correctif Tidewater 005 et son ancien launcher. Elle reste acquise dans
sa portée ; elle n'est pas transférée au nouveau hub ni au jeu repackagé dont le
hash est différent. Les parcours RPG Studio source075 acquis restent distincts de
la promenade native incluse ici. Aucun nouveau verdict global de Gate 3.

## Preuves conservées

- `E:/OpenRPG_Forge/.delivery-evidence/20261008/installation-test-002/verification.json`
- `E:/OpenRPG_Forge/.delivery-evidence/20261008/entrypoint-test-002/entrypoints.json`
- `E:/OpenRPG_Forge/.delivery-evidence/20261008/game-smoke-002/entrypoints.json`
- `E:/OpenRPG_Forge/.delivery-evidence/20261008/installation-test-002/user-data/captures/game.walk.json`
- `E:/OpenRPG_Forge/.delivery-evidence/20261008/prerequisites/download-proof.json`
- `E:/OpenRPG_Forge/.delivery-evidence/20261008/asset-library/asset-library-transfer.json`
- `E:/OpenRPG_Forge/.delivery-evidence/20261008/asset-library/catalog/asset-library-catalog.json`
- Dossier de livraison : index, manifestes, provenance des exécutables, README et copies sources d'assemblage.

## Suite de qualification

1. Traiter la cadence du runtime actuel et requalifier un candidat exact, sans modifier
   les travaux moteur/Studio concurrentiels depuis ce chantier de packaging.
2. Valider le nouveau parcours installer/launcher et les trois points d'entrée sur
   Windows propre ; ne pas demander à nouveau la validation acquise pour 005.
3. Terminer les contrôles CI exacts, la politique supply-chain et les critères RPG
   restants selon le suivi Gate 3. Le blocage Linux/GLib existant n'est pas levé.
4. Dans le futur client : composition des lots en racine Vault, activation, catalogue
   authentifié, signatures et transport réseau. Les hashes ne remplacent pas la confiance.

Le statut livré est **preview privée locale installable**, avec jeu natif non qualifié.
Gate 3 et release restent ouvertes. La bibliothèque complète n'a pas été copiée dans
une archive de près de 486 Go, ni envoyée ; les sources originales sont inventoriées
à part, sans cache/quarantaine et sans modification du Vault.
