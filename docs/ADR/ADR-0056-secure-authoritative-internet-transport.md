# ADR-0056 — Transport Internet authentifié et autorité gameplay bornée

Date : 28 août 2026
Statut : accepté
UUID : `4251b7d7-c610-4ee4-862a-47638c86247e`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

Le transport Internet direct v1 répliquait présence et déplacements sur TCP en clair. L'empreinte
du module empêchait un contenu incompatible, mais ne prouvait pas l'identité de l'hôte, n'authentifiait
pas l'invité et ne protégeait pas les secrets. Les contrats de combat, inventaire, quête et sauvegarde
n'étaient pas exercés par le socket réellement utilisé par le lanceur.

## Décision

- Le schéma de transport et le protocole de jeu passent à `2`. Une écoute non loopback est refusée
  sans certificat TLS, secret de mot de passe fourni par variable d'environnement et registre de
  comptes contenant uniquement des SHA-256 de jetons.
- Le client épingle exactement le SHA-256 du certificat. Un jeton ne peut jamais être envoyé sur le
  transport en clair. TLS 1.2 et 1.3 sont autorisés ; la révocation PKI n'est pas invoquée car les
  invitations utilisent une identité auto-signée épinglée.
- Le lanceur produit une invitation privée `openrpg2:` contenant adresse, port, pin et jeton. Il
  génère le certificat de l'hôte et migre une préférence v1 de connexion directe vers Solo plutôt que
  de la reconnecter silencieusement sans sécurité.
- Le serveur accepte une intention allowlistée, contrôle UUID anti-rejeu, révision attendue, taille,
  état spatial et transition, puis calcule lui-même dégâts, soin, progression de quête et empreinte
  de checkpoint. Inventaire et quêtes restent privés ; santé et présence sont publiques.
- Les boucles de transport vivent sur le thread pool. Leur progression ne dépend jamais du contexte
  principal de Godot ni d'un chargement de scène synchrone.

## Preuves

- Tests : `tests/Forge.Network.Tests/InternetTransportTests.cs` (118 tests réseau au total)
- Serveur : `dotnet run --project apps/Forge.Server -c Release --no-build -- --internet-transport-smoke-test`
- Deux runtimes Godot : `scripts/Invoke-SecureInternetRuntimeSmoke.ps1`
- Journaux : `artifacts/network/secure-runtime/host.log` et
  `artifacts/network/secure-runtime/join.log`
- Marqueurs : TLS 1.3 épinglé, deux joueurs, dégâts serveur à 70, soin à 95, potion décrémentée,
  quête terminée et checkpoint révision 1 avec SHA-256 canonique.

## Conséquences

Une session Internet du lanceur n'expose plus un protocole anonyme en clair. `Forge.Server` refuse
par défaut une écoute publique mal configurée, tandis que le mode local loopback garde un transport
de développement sans jeton.

L'invitation reste un secret porteur : toute personne qui la possède peut rejoindre jusqu'à rotation.
Le certificat et son mot de passe sont stockés dans le profil local du lanceur. La tranche gameplay
prouve l'autorité du transport, mais ne raccorde pas encore toutes les actions, dialogues, transitions
et sauvegardes d'une campagne réelle. Reconnexion sécurisée, rotation/révocation depuis l'interface,
limitation par IP, persistance serveur durable, audit offensif, relais/NAT et essai WAN restent requis
avant une exploitation publique non supervisée.

## Remplacement

Cette décision remplace la frontière de transport non sécurisé de l'ADR-0048 pour les nouvelles
sessions Internet. L'ADR-0048 reste applicable au choix d'une connexion directe sans relais.
