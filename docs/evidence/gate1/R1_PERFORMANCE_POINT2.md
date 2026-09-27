# Gate 1 — point 2, performances sur matériel cible

9 septembre 2026, branche Rust, worktree préservé, sans publication.

## Verdict courant

**ACCEPTÉ PAR DÉROGATION DU PROPRIÉTAIRE — 10 septembre 2026.**
Après présentation des échecs mesurés et de l'absence du GPU cible, le propriétaire
demande : « non considère ces test réussit avec ma 7800XT ».
Cette décision clôt le point 2 dans le suivi de Gate 1 sur la base de sa RX 7800 XT,
sans exiger de machine RTX 3060/RX 6600 ni une nouvelle campagne comme préalable.
Statut : **WAIVED / ACCEPTED**, pas benchmark **PASS**.

Les mesures ci-dessous restent inchangées, les seuils historiques non atteints
restent des échecs mesurés et le benchmark cible reste NOT RUN. Le candidat combiné
actuel n'a pas été remesuré. Aucune garantie de performance sur GPU milieu de gamme
ou de 60 FPS constants n'est acquise. Les objectifs chiffrés restent des objectifs
d'amélioration ; ils ne bloquent plus ce point sous cette dérogation explicite.
Gate 1 et ses autres critères restent ouverts. Aucun push autorisé par cette décision.

## Verdict historique du 9 septembre — remplacé pour le suivi, preuves conservées

**NON QUALIFIÉ — benchmark cible NOT RUN.** La machine accessible expose une
AMD Radeon RX 7800 XT (pilote 32.0.31041.1004) et un GPU intégré AMD Radeon Graphics,
pas une RTX 3060 12 Go ni une RX 6600 8 Go. Aucun résultat RX 7800 XT n'est converti
par extrapolation en résultat du GPU cible ; aucun seuil ni matériel requis changé.
Le point 2 et R1-041 restent ouverts. Gate 1 reste ouverte.

## Vérifié dans cette reprise

- Inventaire matériel Windows réel ; aucun GPU cible détecté.
- SHA-256 du rapport release historique contrôlé :
  `83DB1553636418DA7DCC492899EBDE728D2A94D37507FB950C0B5DDE54EEBB6D`.
- Recalcul indépendant depuis les échantillons et les ticks du trajet ; accord
  avec les statistiques globales enregistrées à 1e-8.

| Mesure historique | Échantillons | FPS moyens | p95 ms | 1 % low FPS | Seuils |
|---|---:|---:|---:|---:|---|
| Trajet entier | 2173 | 72,47 | 21,31 | 43,01 | Échec |
| Tiers forestier | 517 | 51,72 | 22,28 | 39,90 | Échec |
| Exigence | — | ≥60 | ≤16,7 | ≥45 | Tous requis |

Source : `R1_RELEASE_PREPARATION.md`, release historique
`4C4BB888936653294D420A86CDEBE7E69BC2AE0E86E7203B8D5E0EE43BFC25EC`.
Ce trajet utilise des ombres candidates et n'inclut pas les nuages. Il précède
le dernier raccord des profils. Il démontre un déficit historique local, pas
les performances du candidat combiné actuel. Une moyenne globale supérieure
à 60 FPS ne compense pas un p95 ou un 1 % low insuffisant.

Preuve locale :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-performance-point2-20260909/preflight.json`.
Script de recontrôle exécuté :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/Check-Point2-20260909.ps1`.
Inventaire, contrôles de validité des échantillons, SHA et recalcul PASS.
Ces contrôles ne sont pas une nouvelle exécution du moteur.

## Qualification technique différée — non bloquante pour le point 2 dérogé

1. Disposer d'une machine cible accessible et relever le GPU réellement sélectionné
   par wgpu, le CPU, la RAM, le pilote et la résolution effective.
2. Construire et épingler le release du candidat combiné courant, avec le kit et
   tous ses inputs. Le runner historique `Run-FoliageAtlasCandidate.ps1` ne constitue
   pas un protocole cible prêt à utiliser tel quel : template externe, anciennes
   ombres candidates et absence de nuages. Ne pas réutiliser son verdict sans adaptation.
3. Exécuter des passages répétés en 1920×1080, High effectif fixe, sans adaptation,
   sans capture ni autre charge de test concurrente ; conserver tous les échecs.
   Vérifier préchauffage, reçus GPU, sources, paramètres effectifs et parcours.
4. Comparer moyenne, p95 et 1 % low sur chaque passage et la section forestière ;
   ne pas masquer cette dernière dans la moyenne globale. Si nécessaire, mesurer
   le coût dominant avant une correction ciblée puis comparer à conditions identiques.

Aucune nouvelle compilation, nouvelle mesure graphique ou optimisation engagée
dans cette reprise : absence du matériel qualifiant établie avant de lancer une
longue série non concluante. Aucun processus de test ouvert. Les pistes de
micro-optimisation de l'eau et de simplification du bois déjà rejetées ne sont pas
rejouées. Un benchmark local actuel peut préparer l'optimisation, mais ne lève
pas le besoin de matériel cible. Toute modification de cette exigence requiert
une décision explicite distincte, non présumée ici.
