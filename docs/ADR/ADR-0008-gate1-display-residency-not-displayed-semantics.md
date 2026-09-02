# ADR-0008 — Résidence d'affichage et frames non affichées de Gate 1

Date : 2026-09-02
Statut : **accepté — clarification sémantique de la qualification stutter Gate 1**
Portée : branche `Rust`, preuve native PresentMon de `visual_valley`, Windows réel
Complète : `ADR-0004-gate0-draw-call-telemetry-exception.md`

## Contexte

La preuve native Gate 1 mesure une fenêtre de gameplay réelle avec PresentMon, sur le PID exact du
runtime. Les captures propres à PresentMon `2.4.1` et les diagnostics A/B avec `2.5.1` contiennent un
petit nombre de lignes dont `DisplayedTime` vaut `NA`, alors que les intervalles effectivement
affichés couvrent toute la fenêtre, qu'aucun événement ou buffer ETW n'est perdu et que les seuils de
stutter demandés par la roadmap passent.

La documentation officielle de l'[application console PresentMon
2.5.1](https://github.com/GameTechDev/PresentMon/blob/v2.5.1/README-ConsoleApplication.md)
définit `DisplayedTime` comme la durée pendant laquelle une frame est affichée et `NA` lorsque cette
frame n'a pas été affichée. Elle décrit aussi `--exclude_dropped` comme l'option qui retire ces frames
du CSV. La capture Forge n'utilise pas cette option : les lignes non affichées doivent rester dans la
preuve brute.

Une ligne non affichée n'est donc ni une valeur temporelle manquante à convertir en zéro, ni à elle
seule la preuve d'un trou visible. Pendant une présentation composée, l'écran peut continuer à
résider sur une frame affichée pendant qu'une autre présentation de l'application n'atteint pas
l'écran. La présence d'une ligne `NA` et l'existence d'un intervalle sans frame résidente sont deux
observations différentes et doivent être qualifiées séparément.

## Décision

Cette décision est une **clarification du sens des mesures**, pas une dérogation aux seuils de Gate 1.

### Conservation des frames non affichées

Chaque capture qualifiante conserve et hache le CSV PresentMon complet, sans `--exclude_dropped`.
Elle publie séparément au minimum :

- le nombre de lignes dont le début CPU appartient à l'enveloppe de mesure ;
- le nombre de lignes avec un `DisplayedTime` numérique ;
- le nombre de lignes `DisplayedTime=NA` ;
- les pertes d'événements et de buffers ETW ;
- la signature de présentation, l'état de la fenêtre et les bornes QPC ;
- le hash SHA-256 du CSV, du ledger runtime et du résultat d'analyse.

Le compteur `DisplayedTime=NA` reste un diagnostic de présentation obligatoire. Il ne doit être ni
supprimé, ni renommé en mesure affichée, ni masqué par un filtre. Une variation inexpliquée de ce
compteur entre deux builds comparables demande une investigation, même lorsque le verdict stutter
reste positif.

### Résidence dérivée

Pour chaque ligne affichée, Forge dérive l'intervalle de résidence à partir de `CPUStartQPC`, de la
latence jusqu'à l'affichage et de `DisplayedTime`. L'union ordonnée de ces intervalles doit :

- couvrir le début et la fin de la fenêtre QPC du contrôleur ;
- ne contenir aucun trou mesurable dans cette fenêtre ;
- publier le trou maximal, le trou total, la fréquence QPC et la tolérance de quantification ;
- utiliser une fenêtre semi-ouverte, début inclus et fin exclue.

La tolérance ne couvre que l'arrondi QPC explicitement enregistré. Elle ne permet pas de masquer un
intervalle temporel réellement découvert. Les deux captures A/B ci-dessous observent un trou maximal
et un trou total de zéro.

### Verdict stutter

Un nombre non nul de lignes `DisplayedTime=NA` ne bloque pas **à lui seul** le verdict stutter si, et
seulement si, toutes les conditions suivantes passent simultanément sur la même capture :

1. la résidence dérivée couvre sans trou mesurable toute la fenêtre du contrôleur ;
2. `EtwEventsLost=0` et `EtwBuffersLost=0` ;
3. le handle, le PID propriétaire, la visibilité, l'absence de minimisation et le premier plan de la
   fenêtre restent stables pendant toute la fenêtre ;
4. la signature de présentation reste stable ;
5. la moyenne d'affichage est au moins de 60 FPS ;
6. le p95 de `DisplayedTime` est au plus de 16,7 ms ;
7. le 1 % low est au moins de 45 FPS ;
8. aucun `DisplayedTime` numérique ne dépasse 33,3 ms ;
9. la longueur maximale d'une séquence de `DisplayedTime` supérieurs à 16,7 ms est au plus égale à
   un.

Les métriques temporelles sont calculées uniquement sur les valeurs numériques de `DisplayedTime`,
puisqu'une ligne `NA` ne porte aucune durée d'affichage. La ligne elle-même reste néanmoins conservée
et comptée dans la preuve. L'échec d'une seule des neuf conditions bloque le verdict stutter ; le
compteur non nul ne peut compenser aucun seuil, aucune perte ETW ou aucun trou de résidence.

## Preuve A/B PresentMon 2.4.1 et 2.5.1

Les deux observations utilisent le même binaire Release normal issu de
`9db35a8e125c6c60cde9de66a7f6aa7585215bfa`, SHA-256
`C3790DA4A7A9F46B36F0837144A75F5AFDD43A63101093D25F402D09E0E80C4A`, sur Windows 11
`10.0.26200`, Radeon RX 7800 XT, Vulkan, profil High 1920 × 1080 et route
`AutoNoVsync`/`Composed: Flip`.

| Mesure | PresentMon 2.4.1 gouverné | PresentMon 2.5.1 diagnostic A4 |
|---|---:|---:|
| lignes CPU dans la fenêtre | 3 877 | 3 908 |
| lignes affichées | 3 860 | 3 893 |
| lignes `DisplayedTime=NA` | 17 | 16 |
| FPS moyens affichés | 128,7260 | 129,7334 |
| p95 | 13,9112 ms | 13,9102 ms |
| p99 | 13,9657 ms | 13,9572 ms |
| 1 % low | 58,3489 FPS | 61,4409 FPS |
| valeurs affichées > 33,3 ms | 0 | 0 |
| maximum consécutif > 16,7 ms | 1 | 1 |
| événements / buffers ETW perdus | 0 / 0 | 0 / 0 |
| résidence couvre début et fin | oui | oui |
| trou maximal / total | 0 / 0 QPC | 0 / 0 QPC |

Les lignes CPU et `NA` sont sélectionnées par leur `CPUStartQPC`, tandis que les lignes affichées sont
sélectionnées par le chevauchement de leur intervalle de résidence avec la fenêtre. Ces compteurs
documentent donc deux projections complémentaires et ne sont pas supposés former une partition par
simple addition.

### Preuve gouvernée PresentMon 2.4.1

- outil : 927 304 octets, SHA-256
  `D74183E7AE630F72CD3690BE0373ECBFD6CBB86578148AAB8FA2A7166068F34` ;
- dossier local ignoré :
  `vulkan-governed-normal-9db35a8-20260902T163730202Z` ;
- validation : SHA-256
  `61223F1362C85CC7F0990279DF2DD77C9CA6A71B8A9B9EB9C834D697D76FD571` ;
- CSV : SHA-256
  `12052E2EFB218837B1985528B06B68F6E44FE22AF1E25F91608C102BA31C9F7B` ;
- ledger runtime : SHA-256
  `ACF0A7BD21034335EB090F52D8F9E9C7702724664D0B9F24E71E708C62B1ECA1`.

Cette capture est le run gouverné construit depuis un target Cargo propre. Son ancien résultat
`runtime_qpc_display_overlap_thresholds_failed` provient du prédicat supplémentaire « zéro frame non
affichée », alors que les neuf conditions de stutter ci-dessus sont vraies.

### Diagnostic A4 PresentMon 2.5.1

- release officielle : [PresentMon
  v2.5.1](https://github.com/GameTechDev/PresentMon/releases/tag/v2.5.1) ;
- outil : 956 768 octets, SHA-256
  `9BEC3083069F58F911E6A512F4806DB51A27BD096103087BC1D05EF54C80A191`, identique au digest de
  l'asset GitHub, signature Authenticode Intel valide lors de l'acquisition ;
- dossier local ignoré :
  `vulkan-normal-presentmon-2.5.1-a4-9db35a8-20260902T173500000Z` ;
- résumé diagnostique : SHA-256
  `AAC662F7177FAEEF2B26E923061517BF6CF4B80A80543E8DE31862FE3DE17B28` ;
- CSV : SHA-256
  `5053DAE9491C5665077D430741258D2CADF1EBCCE627506820E137EA8C5DB2D2` ;
- ledger runtime : SHA-256
  `718270959D9CCB1BD6711CC6BCAE6E1CC3DF70B4F987EBA5A9AE16A6E646E95F`.

A4 est volontairement **non qualifiant** : il réutilise le target Cargo gouverné au lieu de refaire
un build propre et emploie un shim d'identité `2.4.1` dans le signal runtime pendant que le collecteur
réel est `2.5.1`. Il sert seulement à comparer le collecteur sur le même binaire. La proximité des
résultats et la persistance de 16 lignes `NA` ne remplacent pas la preuve gouvernée.

## Limites des notifications DDI récentes

L'[issue officielle PresentMon
#571](https://github.com/GameTechDev/PresentMon/issues/571) indique que PresentMon ne suivait pas de
nouveaux événements DDI donnant le timestamp de chaque flip et les notifications de flips terminés ou
abandonnés. Elle est fermée sous le jalon `v2.6.0`, sans release `2.6.0` publiée ni relation de
développement visible au moment de cette décision. Les notes de release `2.5.1` ne revendiquent pas
ce support.

Le commentaire technique de l'issue précise que ces événements étaient alors disponibles sur du
matériel Intel LNL/BMG ou plus récent avec un build Windows supérieur à 26200. La preuve Forge
courante utilise exactement le build 26200 et un GPU AMD. L'issue #571 est donc une limite amont à
surveiller, pas la preuve que les lignes `NA` observées ici sont erronées, ni qu'une version non
publiée les corrigerait.

Cette clarification devra être réévaluée si une release stable PresentMon documente ce support, si le
matériel ou l'OS change, ou si une capture ETL indépendante contredit la résidence dérivée.

## Frontières avec Gate 1 et la roadmap

Cette décision ne ferme pas Gate 1. Elle qualifie uniquement la sémantique du verdict automatisé de
stutter sur le matériel local observé. Restent notamment ouverts :

- la qualification sur le GPU cible ;
- la revue humaine visuelle et de jouabilité ;
- `R1-037`, tant que la télémétrie exhaustive des draw calls ou une capture GPU conforme à ADR-0004
  n'existe pas ;
- la CI distante, toujours `NOT RUN` selon ADR-0006 ;
- toute autre condition ouverte du rapport et de la roadmap Gate 1.

ADR-0008 n'autorise pas le démarrage de Gate 2. Gate 2 reste interdite tant qu'un verdict Gate 1
complet, honnête et documenté n'a pas été prononcé selon la roadmap.

## Conséquences

Le protocole ne confond plus une présentation applicative non affichée avec un trou de résidence
visible. Il conserve l'intégralité de la preuve et applique les seuils de stutter à ce qui a réellement
été affiché, tout en rendant les frames non affichées auditables.

Le coût assumé est un verdict composé de plusieurs invariants plutôt qu'un unique compteur nul. Les
ledgers, scripts et rapports qui appliquent cette décision doivent donc exposer séparément la
résidence, les pertes ETW, l'état de fenêtre, les seuils temporels et le compteur `NA`. Aucun résultat
ne peut être résumé par « FPS moyens suffisants » seul.
