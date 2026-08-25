# ADR-0048 — Transport multijoueur Internet direct

Date : 2026-08-25
Statut : accepté
UUID : `3d584584-ef77-4f22-8ac4-2fb5682744a8`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

ADR-0021 impose une autorité .NET indépendante de Godot et une frontière de transport, tandis que
ADR-0044 interdit d'activer **Rejoindre par adresse** avant l'existence d'un transport réel. Le
lanceur sait déjà persister le joueur, le mode, l'adresse et le port ; le runtime sait créer une
session autoritaire locale. Il manque le raccord inter-processus permettant à deux exécutables de
se rencontrer sans dépendance cloud obligatoire.

Les alternatives évaluées sont ENet directement dans Godot, un relais public obligatoire et un
transport .NET direct. ENet lierait le protocole au moteur et ne servirait pas le serveur headless.
Un relais public introduirait immédiatement exploitation, compte fournisseur et disponibilité
externe. Le transport .NET direct conserve l'architecture et reste utilisable hors ligne sur LAN.

## Décision

- `Forge.Network.Transport` porte un protocole TCP v1 commun au runtime et à `Forge.Server`.
- Chaque message JSON UTF-8 est précédé d'une longueur 32 bits big-endian et limité à 64 Kio. Le
  serveur refuse les trames, schémas, types et valeurs hors contrat.
- L'admission précède toute présence en jeu : UUID de connexion et de compte éphémères, nom borné,
  empreinte SHA-256 du module et version de protocole doivent correspondre à la session autoritaire.
- L'hôte local écoute uniquement sur `127.0.0.1`. Le profil Internet écoute sur `0.0.0.0` au port
  configuré ; le joueur reste responsable de l'autorisation du pare-feu et de la redirection TCP du
  routeur. Le client accepte une IPv4 ou un nom DNS.
- Le lanceur Internet demande facultativement l'IPv4 publique par HTTPS à `api.ipify.org`, valide une
  réponse bornée puis affiche et copie `adresse:port`. Ce service n'est pas une dépendance du
  transport : échec, indisponibilité ou refus laisse le champ IPv4/DNS modifiable et ne bloque pas
  l'hébergement. La boucle locale `127.0.0.1` n'est jamais proposée comme adresse d'invitation.
- Les positions portent une zone et une séquence croissante. Le serveur borne les coordonnées et le
  déplacement entre deux états, attribue une révision globale et diffuse des snapshots. Un client
  ignore tout snapshot plus ancien que celui déjà appliqué.
- Le runtime représente les autres joueurs par des avatars distants interpolés et conserve le
  serveur/client pendant les rechargements de scène. Le serveur headless utilise exactement le même
  listener au lieu de s'arrêter après l'affichage du marqueur de disponibilité.
- Aucun secret, mot de passe ou jeton de compte n'emprunte ce protocole v1. Cette première version
  directe n'offre ni chiffrement, ni preuve d'identité de l'hôte, ni relais, ni ouverture NAT
  automatique ; elle est destinée aux parties directes entre joueurs de confiance.

## Preuves

- Tests : `tests/Forge.Network.Tests/InternetTransportTests.cs`
- Tests : `tests/Forge.Network.Tests/PublicInternetAddressResolverTests.cs`
- Tests : `tests/Forge.ModuleFormat.Tests/Launcher/GameLauncherTests.cs`
- Smoke : `dotnet run --project apps/Forge.Server -- --internet-transport-smoke-test`
- Marqueur : `OPENRPG_FORGE_INTERNET_TRANSPORT_TWO_CLIENTS_PASS`
- Guide : `docs/MULTIPLAYER_INTERNET.md`

## Conséquences

Le lanceur peut réellement héberger ou rejoindre par adresse, et les runtimes voient la présence et
les déplacements distants sans service propriétaire. Les incompatibilités de module/protocole sont
refusées avant la réplication et le mode solo ne crée aucun socket.

La détection publique améliore l'adresse communiquée aux invités, mais ne prouve ni la redirection
du port ni la joignabilité WAN et peut refléter un VPN ou un proxy. Elle ne remplace donc pas un test
depuis un second accès Internet et n'introduit aucun service obligatoire pour jouer sur un LAN.

Le v1 ne synchronise pas encore l'ensemble du gameplay RPG : dégâts, inventaire, quêtes, dialogues,
sauvegardes et changements de zone doivent être routés par intentions autoritaires avant une
qualification de campagne coopérative complète. TLS ou un protocole sécurisé équivalent, la preuve
d'identité de l'hôte, la reconnexion, le relais/NAT et un annuaire optionnel restent des lots séparés.

## Remplacement

Cette décision concrétise la frontière prévue par ADR-0021 et satisfait la condition d'activation
énoncée par ADR-0044 sans remplacer ces décisions. Une évolution incompatible du protocole créera
un nouvel ADR et conservera un refus explicite des versions non compatibles.
