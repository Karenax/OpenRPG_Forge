# Multijoueur Internet direct

Dernière mise à jour : 25 août 2026.

Ce document décrit le transport réellement utilisable depuis le lanceur de jeu, ses prérequis et
ses limites. Il ne faut pas confondre cette première base Internet avec l'ensemble des contrats
multijoueur configurables dans Studio ni avec une campagne RPG coopérative entièrement synchronisée.

## État fonctionnel

| Parcours | Écoute ou destination | État actuel |
| --- | --- | --- |
| Solo | aucun socket | fonctionnel |
| Hôte local | `127.0.0.1:<port>` | fonctionnel, limité à l'ordinateur hôte |
| Hôte Internet du lanceur | écoute `0.0.0.0:<port TCP>` ; affiche `IPv4 publique:<port>` | fonctionnel, capacité actuelle de 8 joueurs |
| Rejoindre par Internet | IPv4 publique ou nom DNS et port TCP | fonctionnel en connexion directe |
| `Forge.Server` headless | adresse et capacité du fichier de configuration | listener TCP fonctionnel, capacité configurable de 1 à 64 |

Le profil **Internet direct · héberger et rejoindre** active les deux parcours Internet dans le
lanceur. L'hôte et les clients utilisent le même transport de `Forge.Network`; le serveur headless
ne duplique pas les règles d'admission.

## Configurer le lanceur du jeu

Dans **Distribution et conformité** :

1. activer **Lanceur du jeu · Windows x64** ;
2. choisir **Internet direct · héberger et rejoindre** comme mode multijoueur ;
3. choisir un port TCP entre 1024 et 65535, 5121 étant la valeur par défaut ;
4. enregistrer le profil puis choisir **Créer le lanceur du jeu** ;
5. vérifier le dossier produit avec `forge launcher verify` ou le script de distribution.

Le paquet distribué contient le lanceur WPF, le runtime, le module Client et un manifeste SHA-256.
Les préférences du joueur restent sous `%LOCALAPPDATA%\OpenRPG Forge\Games\<slug>` et ne doivent pas
être partagées avec les invités.

## Adresse affichée par le lanceur

Quand **Héberger** est sélectionné avec le profil Internet, le lanceur demande facultativement
l'IPv4 publique à `https://api.ipify.org/` par HTTPS. La réponse est bornée à 64 octets et n'est
acceptée que si elle contient une IPv4 publique. La requête expire après cinq secondes, ne demande
aucun compte et ne transmet que les métadonnées HTTP usuelles ; comme tout service de ce type,
l'opérateur du point d'accès voit nécessairement l'adresse source de la requête.

L'adresse détectée n'est pas stockée dans les préférences. Le champ reste modifiable afin d'utiliser
un nom DNS ou de corriger une adresse fournie par un VPN ou un proxy. Si la détection échoue,
l'hébergement reste disponible et le lanceur demande une saisie manuelle. `127.0.0.1` n'est jamais
proposé comme adresse Internet à partager : cette adresse de boucle locale ne désigne que
l'ordinateur sur lequel elle est saisie.

Cette aide ne teste pas si le port est joignable, ne crée pas de règle de pare-feu et ne configure
pas le routeur. Elle ne peut pas non plus rendre redirigeable une connexion placée derrière un CGNAT.

## Héberger une partie Internet

1. ouvrir **Multijoueur**, puis choisir **Héberger** ;
2. attendre l'IPv4 publique détectée ou saisir une IPv4 publique/un nom DNS, puis confirmer le port ;
3. utiliser **Copier** pour obtenir l'adresse complète `adresse:port` à transmettre aux invités ;
4. autoriser l'exécutable du runtime ou ce port entrant dans le pare-feu Windows ;
5. rediriger le même port TCP du routeur vers l'IPv4 locale de l'ordinateur hôte ;
6. conserver le runtime hôte ouvert pendant toute la partie.

L'hôte écoute sur toutes les interfaces IPv4 avec `0.0.0.0`, mais le runtime de l'hôte se connecte
lui-même au serveur par `127.0.0.1`. Cette adresse interne n'est pas celle affichée aux invités. Le
mode n'ouvre pas automatiquement le routeur et ne crée aucune règle de pare-feu.

## Rejoindre une partie

1. installer ou copier exactement la même distribution du jeu ;
2. ouvrir **Multijoueur**, puis choisir **Rejoindre** ;
3. saisir l'IPv4 publique ou le nom DNS de l'hôte et le même port TCP ;
4. lancer la partie ; le module et le protocole sont contrôlés avant l'entrée dans le monde.

Un test depuis le même réseau local ne prouve pas la redirection Internet. La qualification WAN doit
être réalisée depuis une seconde connexion, par exemple un autre accès fixe ou un partage mobile.
Certains routeurs ne prennent pas en charge le retour NAT vers leur propre adresse publique.

Si l'accès Internet de l'hôte utilise un double NAT ou un CGNAT sans IPv4 publique redirigeable, ce
mode direct peut être inaccessible. Le projet ne fournit pas encore de relais ni d'ouverture NAT
automatique ; un nom DNS ne contourne pas cette limite.

## Contrat du transport v1

- TCP avec trames JSON UTF-8 préfixées par une longueur 32 bits big-endian ;
- schéma de trame `1`, protocole de jeu `1` et taille maximale de 64 Kio par trame ;
- admission par UUID de connexion et de compte éphémères, nom borné, empreinte du contenu du module
  et version de protocole ;
- refus avant publication du joueur si le contenu, le protocole, l'identité ou la capacité diffère ;
- positions séquencées, coordonnées bornées, pas de déplacement aberrant refusé et snapshots portant
  une révision globale ;
- snapshots plus anciens ignorés par le client ; départ d'un joueur rediffusé aux autres clients.

L'empreinte du module garantit que les deux runtimes ont le même contenu. Elle ne constitue ni un
mot de passe, ni une identité du joueur, ni une preuve cryptographique de l'hôte.

## Réplication runtime actuelle

Le runtime transmet actuellement :

- l'admission et le départ des joueurs ;
- le nom visible ;
- la zone active ;
- la position et l'orientation ;
- les avatars distants interpolés et le nombre de joueurs connectés.

Il ne route pas encore par ce transport :

- combats, dégâts, soins, pouvoirs et RNG autoritaire ;
- inventaires, butin, marchands et économie ;
- quêtes, dialogues, variables, cinématiques et choix de groupe ;
- transitions de zone et vérification spatiale collective ;
- personnages persistants, sauvegardes et reprise de session ;
- chat, voix, Live Director distant et administration publique.

Ces contrats existent pour partie dans `Forge.Network`, mais leur présence dans Domain, Studio ou les
scénarios mémoire ne signifie pas qu'ils sont raccordés au transport TCP du lanceur.

## Serveur headless

Le lanceur hôte n'exige pas un processus `Forge.Server` séparé. Pour une machine dédiée, le serveur
headless utilise un fichier JSON conforme à `DedicatedServerConfiguration` :

```json
{
  "serverId": "11111111-2222-4333-8444-555555555555",
  "schemaVersion": 1,
  "name": "Mon jeu - serveur direct",
  "bindAddress": "0.0.0.0",
  "port": 5121,
  "capacity": 8,
  "discovery": "direct",
  "moduleFingerprint": "<contentSha256 du manifest.json du module>",
  "protocolVersion": "1"
}
```

`moduleFingerprint` correspond à `contentSha256` dans `manifest.json` à l'intérieur du `.orpmod`, et
non au hash du fichier `.orpmod` complet. Le manifeste peut être inspecté sans l'extraire :

```powershell
tar -xOf E:\Jeux\MonJeu\game\mon-jeu.orpmod manifest.json
```

Depuis le dépôt de développement :

```powershell
dotnet run --project apps/Forge.Server --configuration Release -- --config=E:\Serveur\openrpg-server.json
```

Le marqueur `OPENRPG_FORGE_SERVER_READY` confirme l'écoute. Le processus reste actif jusqu'à son
arrêt par `Ctrl+C`. L'exploitation comme service Windows, le déploiement automatisé, TLS et la
supervision distante restent des qualifications séparées.

## Diagnostics principaux

| Code | Signification |
| --- | --- |
| `NET_PUBLIC_ADDRESS_RESOLVED` | IPv4 publique détectée et validée dans le lanceur |
| `NET_PUBLIC_ADDRESS_LOOKUP_FAILED` | service indisponible, délai dépassé ou réponse HTTP refusée ; saisie manuelle possible |
| `NET_PUBLIC_ADDRESS_RESPONSE_INVALID` | réponse reçue mais non partageable sur Internet |
| `NET_CONNECT_FAILED` / `NET_CONNECT_TIMEOUT` | adresse, port, pare-feu, routeur ou serveur indisponible |
| `NET_CONTENT_MISMATCH` | contenu de module différent entre client et hôte |
| `NET_PROTOCOL_MISMATCH` | version de protocole incompatible |
| `NET_SERVER_FULL` | capacité de la session atteinte |
| `NET_CONNECTION_EXISTS` | identifiant de connexion déjà présent |
| `NET_POSE_STEP_REJECTED` | déplacement reçu trop important dans une même zone |
| `NET_SERVER_DISCONNECTED` | connexion TCP interrompue |

## Sécurité et exploitation

Le protocole v1 est destiné à des joueurs de confiance :

- aucun mot de passe ou jeton de compte n'est envoyé ;
- le trafic n'est pas chiffré et l'identité de l'hôte n'est pas prouvée ;
- il n'existe pas encore d'authentification de joueur, bannissement réseau ou limitation par IP sur
  ce chemin direct ;
- l'adresse publique et le port exposés peuvent être scannés tant que l'hôte écoute ;
- la règle de pare-feu et la redirection doivent être retirées ou désactivées lorsqu'elles ne sont
  plus nécessaires.

Avant une exposition à des joueurs non fiables, il faut au minimum ajouter un transport sécurisé,
une identité d'hôte vérifiable, une authentification bornée, la reconnexion, des limites de débit et
une qualification offensive proportionnée.

## Preuves locales au 25 août 2026

- 521/521 tests `Forge.Domain.Tests` en Release ;
- 386/386 tests `Forge.ModuleFormat.Tests` en Release ;
- 112/112 tests `Forge.Network.Tests` en Release, dont 10 cas ciblés pour la détection et le rejet
  des adresses locales, privées ou invalides ;
- appel HTTPS réel à `api.ipify.org` validé comme IPv4, valeur volontairement masquée dans la preuve ;
- builds Release de `Forge.Server`, `Forge.GameLauncher`, `Forge.Runtime` et `Forge.Toolset` sans
  avertissement ni erreur ;
- smoke TCP réel à deux clients avec deux positions répliquées et refus `NET_CONTENT_MISMATCH` ;
- distribution Solaren de 228 fichiers au total, dont 227 contrôlés par manifeste ;
- état visuel **Héberger** avec adresse et port partageables inspecté en 1440 × 860 ;
- portail Full vert avec 2 162 tests .NET et 67 tests Python, puis graphe Roslyn complet frais,
  déterministe et sans diagnostic.

Ces preuves utilisent de vrais sockets TCP sur loopback. Elles ne remplacent pas encore un essai WAN
entre deux accès Internet, un soak multijoueur, une matrice de perte/latence réelle ni une revue de
sécurité du protocole exposé.

## Décisions et suivi

- `docs/ADR/ADR-0021-network-kit-authority.md` : autorité indépendante du moteur ;
- `docs/ADR/ADR-0044-configurable-game-launcher.md` : produit lanceur et condition d'activation ;
- `docs/ADR/ADR-0048-direct-internet-multiplayer-transport.md` : protocole TCP direct retenu ;
- `TASKS_MVP.md`, section C9 : raccords gameplay, sécurité, reconnexion, NAT/relais et qualification
  WAN encore ouverts.
