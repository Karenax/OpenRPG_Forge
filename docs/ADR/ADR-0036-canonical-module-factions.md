# ADR-0036 — Catalogue de factions canonique au niveau du module

Date : 5 août 2026
Statut : accepté

## Décision

Le catalogue de factions appartient à `Forge.Domain` et à `ModuleDefinition`. Il n’est plus défini
dans `Forge.RpgKit.Domain`, car sérialisation, commandes Creator, ForgeScript, MCP, runtime et réseau
doivent partager exactement la même identité. Les services RPG Kit de réputation consomment ce
contrat canonique sans le redéfinir.

Le profil avancé reste optionnel. Lorsqu’il existe, il porte UUID, version de schéma, provenance et
autorité serveur. La hiérarchie autorise séparément l’héritage des tags, politiques et relations.
Les témoins et affiliations sont des données persistantes ; création dynamique, diplomatie et
expiration sont des transitions runtime révisionnées que seul le serveur peut produire.

Le format agrégé `0.6.28` et le workspace `1.22.0` sérialisent le catalogue dans le module. Les
lecteurs acceptent les versions immédiatement antérieures et injectent un catalogue vide, de sorte
que les modules sans factions avancées restent valides et jouables.

## Conséquences

- aucune couche d’interface ou d’adaptation moteur ne devient source de vérité des factions ;
- le RPG Kit dépend du domaine canonique, sans dépendance inverse du Core vers ses services ;
- Toolset et MCP passent par la même commande transactionnelle et ne mutent pas directement le JSON ;
- le runtime solo utilise la même règle d’autorité que le serveur, ce qui prépare la réplication G9 ;
- une future matrice visuelle ou synchronisation réseau peut évoluer sans migration du modèle auteur.
