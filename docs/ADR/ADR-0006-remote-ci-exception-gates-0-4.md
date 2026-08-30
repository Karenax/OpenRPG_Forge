# ADR-0006 — Exception temporaire de CI distante pour les Gates 0 à 4

Date : 2026-08-30
Statut : **accepté par ordre explicite du propriétaire jusqu’à Gate 4**
Portée : branche orpheline `Rust`, Gates 0, 1, 2, 3 et 4 uniquement
Déclencheur probant : GitHub Actions `Rust gate`, run `33335683606`
Commit du run : `7286740c18fe9afa6cc207e388d5f335495fc650`
Confirmation au commit Gate 0 : run `33339077298`, commit
`a7c506f099b50100ad49f3ae6d01d541c9b63d49`
Complète : `ADR-0001-rust-webgpu-greenfield.md`

## Contexte

La roadmap exige une CI distante Windows, Linux et WASM avant le passage de Gate 0, puis la
préservation de ces preuves pendant les gates suivantes. Le workflow a été publié sur `origin/Rust`
et déclenché par le push du commit exact `7286740c18fe9afa6cc207e388d5f335495fc650`.

GitHub a créé le run `33335683606`, mais aucun runner ni aucune étape de workflow n’a démarré. Les
quatre jobs ont reçu la même annotation d’infrastructure : les paiements récents du compte ont échoué
ou le plafond de dépense doit être augmenté. L’interface GitHub classe le run et les jobs en
`failure`, mais chaque job contient une liste d’étapes vide. Aucun checkout, aucune installation de
toolchain, aucune compilation et aucun test du code Forge n’ont donc été exécutés à distance.

État exact du run :

| Job | ID GitHub | Conclusion affichée | Étapes exécutées | Qualification Forge |
|---|---:|---|---:|---|
| `native (windows-2025)` | `99322040255` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99322040408` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99322040420` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `supply-chain` | `99322040421` | `failure` | 0 | `NOT RUN — infrastructure billing` |

Run : `https://github.com/Karenax/OpenRPG_Forge/actions/runs/33335683606`.

Ce constat est différent d’un échec de code. Il ne prouve cependant pas non plus que la CI est verte,
que Linux compile, que le build WASM est reproductible sur un runner GitHub ou que les contrôles de
chaîne d’approvisionnement passent dans cet environnement distant.

Le run du commit final Gate 0, `33339077298`, confirme la même cause avec zéro étape exécutée :

| Job | ID GitHub | Conclusion affichée | Étapes exécutées | Qualification Forge |
|---|---:|---|---:|---|
| `native (windows-2025)` | `99331257296` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99331257267` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99331257192` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `supply-chain` | `99331257301` | `failure` | 0 | `NOT RUN — infrastructure billing` |

Run : `https://github.com/Karenax/OpenRPG_Forge/actions/runs/33339077298`.

## Décision

Le blocage d’infrastructure attesté par ces runs ne bloque pas administrativement la
progression séquentielle de Gate 0 à Gate 4. Une gate peut recevoir le verdict
**PASS AUTOMATISÉ LOCAL SOUS ADR-0006** lorsque toutes ses autres conditions sont satisfaites et que
le protocole local de substitution défini ci-dessous est entièrement passé.

Cette exception ne transforme jamais l’état distant en succès. Dans les roadmaps, rapports, ledgers
et résumés, les formulations obligatoires sont :

```text
tests locaux automatisés = PASS
CI distante Windows = NOT RUN — infrastructure billing
CI distante Linux = NOT RUN — infrastructure billing
CI distante WASM = NOT RUN — infrastructure billing
CI distante supply-chain = NOT RUN — infrastructure billing
```

Il est interdit d’écrire `CI verte`, `GitHub Actions PASS`, `tests et CI verts` ou toute formulation
équivalente tant qu’un run distant n’a pas réellement exécuté et réussi ses étapes. La conclusion
rouge affichée par GitHub doit être accompagnée du fait matériel `steps = []` et ne doit être
présentée ni comme une régression Forge, ni comme un succès.

La portée de l’exception est strictement bornée :

- sa cause reconnue est uniquement le refus de démarrage pour facturation ou plafond observé sur les
  runs documentés ci-dessus ;
- elle couvre la progression de la roadmap `Rust` jusqu’à Gate 4 incluse, sans couvrir les travaux
  postérieurs ;
- elle ne couvre aucun job qui obtient un runner et commence à exécuter ses étapes ;
- elle ne couvre aucune erreur de compilation, de test, de Clippy, de formatage, de packaging, de
  licence, d’audit ou de comportement ;
- elle ne vaut ni autorisation de publication supplémentaire, ni qualification de distribution.

## Protocole local de substitution obligatoire

Chaque Gate 0 à 4 doit être qualifiée sur un commit et un arbre Git exacts. Le rapport de gate doit
identifier le commit, l’arbre, la branche, la divergence avec `origin/Rust` et l’état du worktree. Les
preuves de qualification sont produites dans un clone ou worktree de preuve propre lorsque la copie
de travail principale contient des modifications non liées.

Le socle minimal obligatoire pour chaque gate est :

1. un replay automatisé reproductible sous Windows réel sur le matériel local qualifié ;
2. les tests ciblés de chaque capacité modifiée, avec commandes, codes de sortie et décompte exact ;
3. les contrôles transversaux locaux pertinents : formatage, Clippy strict et tests du workspace ;
4. les builds natifs Windows réellement nécessaires à la gate, suivis d’un lancement ou smoke test
   du binaire exact produit ;
5. les checks et builds WASM locaux pertinents, ainsi que le lancement navigateur ou WebView lorsque
   la gate touche le Studio ou le viewport ;
6. les contrôles locaux de licences, sources, advisories et chaîne d’approvisionnement lorsqu’ils
   sont dans la portée de la gate ;
7. des preuves machine lisibles : ledgers versionnés, timestamps UTC, environnement, hashes SHA-256,
   métriques, captures locales lorsque requises et limitations explicites ;
8. un nouveau replay des tests de non-régression de toutes les gates déjà passées que le changement
   peut affecter.

Un test ciblé ne remplace pas le contrôle workspace lorsqu’un contrat partagé, une dépendance, le
renderer, la persistance, le package ou la distribution sont modifiés. Inversement, un build réussi
ne remplace pas un lancement réel du runtime, du Studio ou du package concerné.

Le Windows local réel qualifie uniquement l’environnement observé. Il ne prouve pas Linux. Le build
WASM local qualifie la toolchain et le comportement local observé, pas l’environnement d’un runner
GitHub. Ces risques restent inscrits comme `NOT RUN` dans chaque rapport jusqu’à une vraie exécution
distante.

## Preuves à conserver pour chaque gate

Tout rapport de Gate 0 à 4 utilisant cette exception doit contenir une section `Exception CI distante
ADR-0006` avec au minimum :

- le commit et l’arbre qualifiés ;
- le lien et l’ID du run `33335683606` comme origine de l’exception ;
- les quatre jobs, leur conclusion d’interface, leur nombre nul d’étapes et la cause de facturation ;
- la mention explicite que la CI distante n’a pas testé le commit de la gate si celui-ci diffère de
  `7286740c18fe9afa6cc207e388d5f335495fc650` ;
- les commandes locales de substitution réellement exécutées et leurs résultats ;
- les hashes des binaires, bundles, ledgers et captures utilisés pour la décision ;
- les validations Windows réelles et ciblées propres à la gate ;
- les risques Linux distant et WASM distant encore ouverts ;
- le déclencheur de réouverture applicable.

Une gate ultérieure ne peut pas recopier le résultat local d’une gate antérieure si son commit ou un
sous-système pertinent a changé. Elle doit rejouer les contrôles affectés et produire de nouvelles
preuves.

## Distinction entre infrastructure et produit

L’exception s’applique seulement tant que le job est refusé avant runner et avant toute étape. Les
cas suivants sont des échecs de produit ou de portabilité normaux et bloquent la gate :

- le runner démarre puis le checkout, la toolchain ou une dépendance déclarée échoue ;
- le formatage ou Clippy échoue ;
- une compilation Windows, Linux ou WASM échoue ;
- un test, un audit, une politique de licence ou un contrôle de source échoue ;
- une capture, un ledger ou un package ne respecte pas son schéma ou son protocole ;
- un job est annulé ou désactivé pour éviter un résultat attendu.

Une nouvelle panne GitHub sans rapport avec la facturation ou le plafond n’est pas couverte
automatiquement. Elle exige une analyse et, si elle doit devenir non bloquante, une nouvelle décision
bornée.

## Réouverture et expiration automatiques

L’exception cesse automatiquement de neutraliser la condition de CI dès qu’un des événements
suivants survient :

- les GitHub-hosted runners du dépôt recommencent à accepter les jobs ;
- le problème de paiement ou de plafond est résolu ;
- un runner distant approuvé ou un service CI équivalent devient disponible ;
- un run Windows, Linux, WASM ou supply-chain commence réellement à exécuter ses étapes ;
- le workflow, les images de runner ou les commandes de qualification changent substantiellement ;
- la roadmap dépasse Gate 4 ;
- une distribution, un installateur, une release publique ou une qualification multiplateforme est
  préparé.

À la réouverture, le workflow doit être exécuté sur le commit candidat courant. Tous les jobs requis
doivent réellement passer avant de déclarer la CI verte. Un résultat distant négatif rouvre la gate
affectée au niveau de la condition correspondante et doit être corrigé ; les anciens replays locaux ne
peuvent pas l’annuler.

Avant toute distribution, cette exception expire sans prolongation tacite. Les validations distantes
Windows, Linux, WASM et supply-chain deviennent obligatoires, même si Gate 4 a été passée localement.

## Frontières avec les autres décisions

Cette décision ne modifie pas :

- l’ordre obligatoire Gate 0 → Gate 1 → Gate 2 → Gate 3 → Gate 4 ;
- les critères fonctionnels, visuels, de performance, de persistance, d’assets ou de packaging ;
- les revues humaines, qui restent gouvernées séparément par la roadmap et les décisions de gate ;
- l’exception draw calls strictement bornée d’ADR-0004 ;
- le choix du Studio enregistré par ADR-0005 ;
- l’interdiction de copier des assets Cadle dans Forge ;
- l’obligation de préserver les modifications non liées et de ne pas publier sans ordre explicite.

## Conséquences

La roadmap peut progresser malgré une incapacité externe à allouer des runners, à condition que chaque
gate fournisse un replay Windows local automatisé, reproductible, ciblé et vérifiable. La dette de
portabilité reste visible au lieu d’être maquillée en succès.

Le coût assumé est l’absence temporaire de preuve distante indépendante, notamment pour Linux et
WASM. Cette dette est obligatoire, cumulative et automatiquement bloquante avant distribution ou dès
le retour des runners.
