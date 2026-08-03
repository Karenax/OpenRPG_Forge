# Plan de fermeture G7 — Creator Core P0/P1

Date de référence : 3 août 2026.

## État de départ

G7-01 à G7-19 sont franchis. Les deux dernières capacités P0 du gate (`ORF-EE-002` et
`ORF-EE-028`) disposent d’une implémentation, de tests ciblés et d’un smoke applicatif. Les 65
capacités encore `Researched` sont toutes P1 et sont réparties ci-dessous en lots de cinq exigences
maximum conformément à `AGENTS.md`.

## Ordre exécutable

| Lot | Capacités | Résultat attendu |
|---|---|---|
| G7-20 | `ORF-SHELL-008`, `009`, `012`, `021`, `023` | Filtres d’affichage, placement répété, onglets de zones, aide contextuelle et profils d’éditeur. |
| G7-21 | `ORF-SHELL-024`, `ORF-PROJ-022` | Autosauvegarde/récupération et instructions joueur localisées. |
| G7-22 | `ORF-PAL-004`, `005`, `008`, `013`, `014` | Propagation de blueprint, catégories hiérarchiques, variantes et collections. |
| G7-23 | `ORF-PAL-018`, `ORF-AREA-034`, `ORF-AREA-033` | Budgets d’assets, LOD, occlusion et visibilité inspectables. |
| G7-24 | `ORF-AREA-006`, `007`, `009`, `010`, `011` | Groupes de tuiles, effacement/remplacement, variation, propriétés et redimensionnement. |
| G7-25 | `ORF-AREA-013`, `014`, `015`, `016`, `021` | Intérieur/extérieur, temps, éclairage, brouillard et audio de zone. |
| G7-26 | `ORF-AREA-024`, `029`, `035` | Événements utilisateur, carte/brouillard de guerre et couches d’édition. |
| G7-27 | `ORF-AUD-003`, `004`, `006`, `007` | Catalogue audio, aperçu, ordre, intervalles et variations de volume. |
| G7-28 | `ORF-AUD-009`, `011`, `012`, `013` | Spatialisation, portée, activation scriptée et ambiance jour/nuit. |
| G7-29 | `ORF-AUD-014`, `018`, `019` | Musique contextuelle, mixage/bus et sous-titres audio. |
| G7-30 | `ORF-GAME-002`, `008`, `009`, `014`, `017` | Vol libre, projectiles, physique, ciblage et temps du monde. |
| G7-31 | `ORF-GAME-021`, `025`, `026`, `027`, `029` | Sauvegardes par contexte, respawn, HUD/journal, verrous cinématiques et replay déterministe. |
| G7-32 | `ORF-PROJ-007`, `008`, `009`, `010`, `011` | Événements connexion, inventaire, utilisation, mort, équipement et niveau. |
| G7-33 | `ORF-PROJ-014`, `016` | Événements utilisateur globaux et calendrier du monde. |
| G7-34 | `ORF-WPT-002`, `003`, `004`, `007`, `008` | Apparence auteur, notes de carte et patrouilles ordonnées. |
| G7-35 | `ORF-WPT-009`, `014`, `015`, `016` | Orientation, ancres audio/VFX, groupes/requêtes et visualisation des routes. |

## Gate de fermeture

G7 ne sera fermé qu’après :

1. passage des 65 lignes au statut justifié `Implemented` ou `Tested`, ou report explicite accepté ;
2. scénario Builder depuis un projet neuf couvrant zones, placement, édition, contenu et playtest ;
3. sauvegarde/reprise et export du module sans édition manuelle du JSON ;
4. validation visuelle des surfaces nouvelles aux tailles nominale et maximisée ;
5. portail global vert, tracker frais et graphe d’architecture déterministe.

G8 ne commence qu’après ce gate, sauf travail préparatoire strictement sans mutation nécessaire à
l’audit des dépendances.
