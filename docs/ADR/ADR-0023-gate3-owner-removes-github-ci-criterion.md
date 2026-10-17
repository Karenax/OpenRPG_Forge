# ADR-0023 — Retrait de la CI GitHub des critères de clôture Gate 3

Date : 15 octobre 2026
Statut : **accepté par instruction explicite du propriétaire**
Portée : clôture Gate 3 actuelle, branche `Rust`, candidat Source013 publié au commit
`3e0044d5f63b26c678d9db19380a66db832d9de3`
Remplace partiellement : [l'actualisation d'ADR-0006](ADR-0006-remote-ci-exception-gates-0-4.md),
pour la seule condition d'attente de CI de cette clôture.

## Décision propriétaire

Après présentation du refus de démarrage de la CI et demande de confirmation
pour une relance, le propriétaire répond dans la conversation de clôture :

> on arret d'attendre les validation de git, c'est pas un critère a retenir

La CI GitHub n'est donc plus un critère bloquant de la clôture Gate 3 actuelle.
Il n'est plus nécessaire d'attendre le déblocage de la facturation, une relance
du run ou un résultat PASS GitHub pour établir ce verdict. Cette instruction
s'applique immédiatement ; elle ne demande pas de nouvelle approbation.

Les autres critères de Gate 3 restent exigés : boucle RPG séparée acceptée par
le propriétaire, comportement environnemental, parcours créés depuis Studio,
persistance, contrôles techniques et preuves identifiées sur la version commune.
Le retrait de la condition CI ne clôture pas automatiquement ces critères ;
leur audit et le rapport de clôture portent le verdict final.

## État distant conservé

Le [run GitHub 38762140900](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38762140900)
porte sur `3e0044d5f63b26c678d9db19380a66db832d9de3`, arbre
`861a739f743d35a763fb80bb6978221f1094ffba`. Le reçu collecté le 15 octobre à
15:40 UTC conserve quatre jobs terminaux en `failure`, sans runner affecté
(`runner_id = 0`) et sans étape exécutée (`steps = []`) : Windows, Linux,
WASM et supply-chain. Les quatre annotations indiquent un paiement échoué ou
un plafond de dépenses à relever ; la condition précise du compte n'est pas
établie par ce message.

Le verdict de cette acquisition reste **NOT RUN — infrastructure billing**.
Aucune compilation, aucun test, aucune qualification Linux ou indépendante
distante n'est attribué à ces jobs. L'acceptation propriétaire ne transforme
pas ces absences d'exécution en PASS. Le reçu historique conserve
`remote_ci_qualified = false` et son `gate_qualified = false` daté ; le nouveau
verdict de gate est documenté séparément, sans réécriture de ce reçu.

## Frontières

- Cette décision vise uniquement la clôture Gate 3 actuelle et ses preuves
  locales reliées au candidat publié ; elle ne supprime pas la CI du projet.
- ADR-0006 reste historique et n'est pas réactivée, reconduite ou étendue.
- Les règles des autres gates, de publication, de distribution, de release
  et de qualification multiplateforme restent distinctes et inchangées.
- Aucun échec produit, test local, règle de sécurité ou de licence n'est
  accepté par cette décision. La réserve de distribution GLib demeure consignée.
- L'absence de preuve Linux et de CI distante reste une limite explicite du
  verdict local, sans se substituer aux contrôles réellement exécutés.
- Aucun nouveau commit, push, changement de workflow, relancement payant ou
  intervention dans la facturation n'est autorisé implicitement.

## Preuves et suivi

- [Matrice des preuves Gate 3](../evidence/gate3/GATE3_CLOSURE_READINESS_20261014.md).
- [Acceptation propriétaire de la boucle RPG séparée](../evidence/gate3/GATE3_OWNER_RPG_ACCEPTANCE_20261013.md).
- Reçu local de la CI :
  `E:\OpenRPG_Forge\.gate3-evidence\20261015\closure-ci-published-013-001\result.json`,
  SHA-256 `e4deff981d57485348dee9ef6bac34110290eab48a6ca4651db106b49f55fe7f`.
- L'acceptation et le verdict de Gate 3 sont consignés dans le contexte canonique,
  la roadmap et le rapport de clôture ; les preuves brutes gardent leurs dates,
  révisions, résultats et réserves propres.
