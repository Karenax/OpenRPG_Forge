# Multijoueur Internet direct sécurisé

Dernière mise à jour : 28 août 2026.

Ce guide décrit le transport réellement utilisé par le lanceur et `Forge.Server`. Le protocole v2
chiffre et authentifie les nouvelles sessions Internet, réplique les déplacements et contient une
tranche d'autorité gameplay. Il ne constitue pas encore une campagne coopérative complète ni un
service Internet public administré.

## État fonctionnel

| Parcours | Transport | État vérifié |
| --- | --- | --- |
| Solo | aucun socket | fonctionnel |
| Hôte local | TCP en clair sur `127.0.0.1` sans jeton | développement local uniquement |
| Hôte Internet du lanceur | TLS 1.2/1.3 sur `0.0.0.0:<port>` | certificat auto-signé épinglé, invitation privée, capacité 8 |
| Rejoindre par le lanceur | invitation `openrpg2:` | pin et jeton obligatoires, contenu et protocole vérifiés |
| `Forge.Server` loopback | TCP local | autorisé sans TLS pour test isolé |
| `Forge.Server` non loopback | TLS et registre de comptes | refus de démarrer si un élément de sécurité manque |

La présence, la zone, la position et l'orientation sont diffusées par snapshots révisionnés. Le
serveur v2 accepte aussi une allowlist d'intentions pour une tranche verticale : attaque spatiale,
usage d'une potion, progression d'une quête de preuve et checkpoint canonique. Il calcule l'état ;
le client ne peut envoyer ni dégâts, ni quantité d'inventaire, ni payload de sauvegarde.

## Héberger depuis le lanceur

1. Ouvrir **Multijoueur**, choisir **Héberger**, puis confirmer l'adresse publique ou le DNS et le
   port TCP.
2. Cliquer **Copier l'invitation**. Le lanceur génère au besoin un certificat, un jeton aléatoire et
   une invitation commençant par `openrpg2:`.
3. Transmettre cette invitation par un canal privé. Elle est un secret porteur : toute personne qui
   la possède peut rejoindre tant qu'elle reste valable.
4. Autoriser le runtime dans le pare-feu Windows et rediriger le port TCP du routeur vers la machine
   hôte.
5. Garder le runtime hôte actif pendant la partie.

Le certificat PFX, son mot de passe, le jeton et l'identifiant de compte sont conservés sous
`%LOCALAPPDATA%\OpenRPG Forge\Games\<slug>`, dans ou à côté des préférences du lanceur. Ils ne font
pas partie de la distribution et ne doivent pas être publiés. Supprimer/régénérer ce profil révoque
de fait les anciennes invitations de cet hôte.

La détection d'IPv4 publique utilise facultativement `https://api.ipify.org/`, avec réponse bornée et
délai de cinq secondes. Elle ne teste ni le pare-feu, ni la redirection, ni le CGNAT. Un nom DNS ne
contourne pas l'absence d'une adresse joignable.

## Rejoindre depuis le lanceur

1. Installer exactement la même distribution du jeu.
2. Ouvrir **Multijoueur**, choisir **Rejoindre** et coller l'invitation `openrpg2:` complète dans le
   champ d'adresse.
3. Lancer. Le lanceur extrait adresse, port, pin du certificat et jeton ; Runtime refuse un certificat
   différent, un jeton invalide, un module différent ou un protocole incompatible avant publication
   du joueur.

Une ancienne préférence v1 de connexion directe est migrée vers Solo. Elle ne peut donc pas se
reconnecter silencieusement en clair. Une adresse nue `hôte:port` ne remplace pas une invitation v2.

## Contrat du transport v2

- TCP avec trames JSON UTF-8 préfixées par une longueur 32 bits big-endian ; 64 Kio maximum ;
- schéma de trame `2` et protocole de jeu `2` ;
- TLS 1.2 ou 1.3 et épinglage exact du SHA-256 du certificat pour Internet ;
- jeton d'invitation envoyé seulement après établissement de TLS, comparé en temps constant à son
  SHA-256 côté serveur ;
- admission par UUID de connexion et de compte, nom borné, empreinte du module et protocole ;
- positions séquencées, coordonnées bornées, pas aberrant refusé et snapshot global révisionné ;
- intentions gameplay avec UUID anti-rejeu, révision attendue, payload borné et allowlist fermée ;
- état privé d'inventaire et de quête envoyé seulement au compte concerné ; santé et présence
  publiques ;
- pompes socket indépendantes du thread principal Godot, y compris pendant un chargement de scène.

## Autorité gameplay actuellement raccordée au transport

| Intention | Contrôles serveur | Résultat serveur |
| --- | --- | --- |
| `combat.attack` | attaquant/cible valides, même zone, distance ≤ 6 m, vivants | 30 dégâts fixes et compteur d'attaque |
| `inventory.use` | seul `healing-potion`, stock disponible, soin applicable | stock décrémenté, +25 PV plafonné à 100 |
| `quest.advance` | transition `available → active → completed`, attaque requise | état privé de `network-trial` |
| `save.checkpoint` | payload client interdit | révision et SHA-256 calculés sur l'état canonique |

Cette tranche est exécutée par deux vrais runtimes Godot dans
`scripts/Invoke-SecureInternetRuntimeSmoke.ps1`. Les actions normales de tous les modules, dialogues,
butin, économie, transitions collectives, cinématiques et sauvegardes `.orpsave` complètes ne sont
pas encore toutes projetées vers ces quatre intentions. La preuve garantit la frontière serveur,
pas une campagne coopérative exhaustive.

## Serveur headless

Le fichier de configuration conserve le schéma serveur 1, mais son `protocolVersion` doit être `2` :

```json
{
  "serverId": "11111111-2222-4333-8444-555555555555",
  "schemaVersion": 1,
  "name": "Mon jeu - serveur direct",
  "bindAddress": "0.0.0.0",
  "port": 5121,
  "capacity": 8,
  "discovery": "direct",
  "moduleFingerprint": "<contentSha256 du manifest.json>",
  "protocolVersion": "2"
}
```

Le registre d'authentification ne contient jamais les jetons en clair :

```json
{
  "schemaVersion": 1,
  "accounts": [
    {
      "accountId": "22222222-3333-4444-8555-666666666666",
      "tokenSha256": "<64 caractères hexadécimaux>"
    }
  ]
}
```

Le PFX et le registre doivent être des chemins absolus, des fichiers ordinaires non-reparse et de
taille bornée. Le mot de passe du PFX, long de 12 à 256 caractères, vient d'une variable
d'environnement dont le nom est passé en argument :

```powershell
$env:OPENRPG_SERVER_PFX_PASSWORD = '<secret local>'
dotnet run --project apps/Forge.Server -c Release --no-build -- `
  --config=E:\Serveur\openrpg-server.json `
  --tls-certificate=E:\Serveur\server.pfx `
  --tls-password-env=OPENRPG_SERVER_PFX_PASSWORD `
  --auth-registry=E:\Serveur\accounts.json
```

Une écoute non loopback sans les trois arguments échoue avec
`NET_PUBLIC_TRANSPORT_SECURITY_REQUIRED`. Le marqueur `OPENRPG_FORGE_SERVER_SECURITY_PASS` publie
le pin du certificat, jamais le secret.

## Diagnostics principaux

| Code | Signification |
| --- | --- |
| `NET_SERVER_CERTIFICATE_MISMATCH` | le certificat reçu ne correspond pas au pin de l'invitation |
| `NET_TLS_HANDSHAKE_FAILED` | négociation TLS impossible |
| `NET_AUTH_INVALID_CREDENTIALS` | compte ou jeton refusé |
| `NET_AUTH_TOKEN_REQUIRES_TLS` | tentative locale d'envoyer un jeton en clair, bloquée avant socket |
| `NET_PUBLIC_TRANSPORT_SECURITY_REQUIRED` | serveur public lancé sans TLS et registre complets |
| `NET_CONTENT_MISMATCH` | contenu du module différent |
| `NET_PROTOCOL_MISMATCH` | protocole différent de v2 |
| `NET_GAMEPLAY_REVISION_STALE` | intention fondée sur un état ancien |
| `NET_GAMEPLAY_INTENT_REPLAYED` | UUID d'intention déjà consommé |
| `NET_GAMEPLAY_INTENT_FORBIDDEN` | type d'intention hors allowlist |
| `NET_POSE_STEP_REJECTED` | déplacement trop important dans une même zone |
| `NET_CONNECT_FAILED` / `NET_CONNECT_TIMEOUT` | serveur, réseau, pare-feu ou routeur indisponible |

## Limites de sécurité et d'exploitation

- l'invitation est partageable et ne porte pas de date d'expiration ni de révocation individuelle ;
- le lanceur stocke localement les secrets de l'hôte, sans coffre-fort matériel ;
- pas encore de limitation par IP, bannissement transport, détection DDoS ou audit offensif WAN ;
- pas de reconnexion authentifiée, reprise durable de session, rotation en ligne ou base serveur ;
- pas de relais, STUN/TURN, ouverture NAT automatique, annuaire ni preuve de joignabilité ;
- le certificat auto-signé est fiable par pin, pas par une autorité PKI ;
- l'essai local ne remplace pas un test WAN, un soak, une matrice perte/latence ou une revue sécurité.

## Preuves locales du 28 août 2026

- 118/118 tests `Forge.Network.Tests` en Release ;
- scénario serveur TLS avec deux clients, mauvais jeton et mauvais pin refusés ;
- deux processus Godot 4.7.1 réels, TLS 1.3, deux joueurs, présence et poses ;
- attaque calculée à 70 PV, potion décrémentée et soin à 95 PV, quête terminée ;
- checkpoint serveur révision 1 et SHA-256 canonique ;
- builds Release de `Forge.Server` et `Forge.GameLauncher`, build Debug du Runtime, sans warning ;
- journaux sous `artifacts/network/secure-runtime/`.

Voir [`ADR-0056`](ADR/ADR-0056-secure-authoritative-internet-transport.md). La signature des
exécutables, la CI distante, la qualification WAN et l'exploitation publique restent des gates
séparées.
