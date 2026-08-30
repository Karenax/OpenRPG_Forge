# Gate 0 — Validation navigateur et Studio

Date : 30 août 2026
Branche : `Rust`
Commit de code visé : `528a909738bde92a8831e5b41ba185ab35ffef7f`
Verdict : **FONCTIONNEL LOCALEMENT, PREUVE DE GATE PARTIELLE**

## Portée et règle de preuve

Cette preuve distingue Chrome, Edge et Tauri/WebView2. Elle enregistre les observations
fonctionnelles sans transformer des valeurs ponctuelles en benchmark.

La validation automatisée ne remplace pas la revue humaine. Toute enveloppe
`forge.visual-proof` produite dans ce lot conserve :

```text
review.status = uninspected
review.method = automated_then_human
```

## Contrat partagé

Toutes les surfaces utilisent le document :

```text
schema       forge.scene-proof
version      1
entities     4
SHA-256      0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df
```

La scène contient le sol, le cube PBR, la lumière directionnelle et la caméra. La hiérarchie,
l'inspecteur et le viewport dérivent des mêmes types et données Rust.

Le frontend appelle réellement `navigator.gpu.requestAdapter()` puis `requestDevice()`. Le chemin
d'échec affiche un message utilisateur détaillé dans le code, mais il n'a pas été exécuté avec
WebGPU désactivé.

## Bundle 528 qualifié

Le bundle navigateur produit par `scripts/Test-Rust.ps1` puis servi localement porte :

| Fichier | Taille | SHA-256 |
|---|---:|---|
| `forge_studio_bg.wasm` | 34 879 512 octets | `d307e835f6aede2fb8cdc5b0939d2219a54354b79def6fb09f65805779086a28` |
| `forge_studio.js` | 181 150 octets | `4751935be971a12ee33840bb93f9e39a1e92bce10d964a0e6b5c23d864399ecb` |
| `index.html` | 1 025 octets | `bc671f45656df66424ecede7cbb8cbac122e02be2ecf6933e26ef81535070b60` |
| `styles.css` | 3 103 octets | `c33688b71bb4cde7cbf52ee8cad9f6e0a7453c4326ff914590464aef5b72cc2d` |

Le JavaScript est la glue générée par `wasm-bindgen`. Aucun fichier JavaScript/TypeScript métier n'est
suivi dans les sources produit.

## Chrome desktop — commit final

Une nouvelle URL cache-bustée `?build=528a909` a provoqué des requêtes HTTP pour l'HTML, le CSS, le
JavaScript et le WASM courants. Le snapshot DOM final a confirmé :

- `WebGPU prêt` ;
- `Monde initialisé 1 fois` ;
- hiérarchie, inspecteur et viewport présents ;
- même scène et même hash source ;
- canvas Bevy chargé.

Le contrôle de l'onglet est ensuite devenu instable : la capture et le clic final ont expiré. La
sélection complète Chrome — cube vers EntityId, rerender 1 et monde toujours initialisé une fois —
avait été prouvée sur le commit de fondation `7d529a86`, pas rejouée sur `528a9097`. La validation
Chrome 528 prouve donc le chargement/WebGPU/document, pas une nouvelle interaction commit-bound.

Une mesure antérieure de session avait observé `loadEventEnd = 627,3 ms` et un heap JS utilisé de
24 030 675 octets. La collecte `requestAnimationFrame` du navigateur piloté avait été throttled à
environ une frame par seconde ; elle est non représentative et n'est pas retenue comme métrique Gate 0.

## Edge desktop — commit final

Edge a chargé `?build=528a909-edge` et a fourni une validation complète du bundle final :

- état `WebGPU prêt` ;
- scène visible avec sol vert et cube bleu ;
- hash source et monde initialisé une fois ;
- sélection du bouton `Procedural cube` ;
- EntityId passé de la valeur du sol à
  `ae91d914-98e9-52d4-80d1-4fddc482988d` ;
- `Rerenders UI` passé de 0 à 1 ;
- monde resté initialisé une fois ;
- mode navigateur autonome correctement signalé, backend Tauri absent ;
- journal contenant uniquement des entrées `INFO`, aucun warning et aucune erreur.

La capture automatisée finale montre la fenêtre Studio complète, ses trois colonnes, le viewport et
l'overlay. Elle a été visualisée pendant le test, mais n'a pas été écrite comme média suivi. Les
captures brutes restent volontairement locales/ignorées.

`R0-031` est donc qualifié localement pour Chrome et Edge desktop. Cette conclusion ne constitue pas
un benchmark navigateur.

## Tauri / WebView2

La session interactive complète du socle avait été exécutée sur `7d529a86` :

- WebGPU prêt et même hash source ;
- sélection depuis la hiérarchie ;
- picking Bevy du cube vers un `CustomEvent` ;
- inspecteur Leptos puis backend Tauri Rust typé ;
- EntityId cohérent ;
- trois rerenders sans reconstruction du monde ;
- rejet d'un EntityId inconnu couvert par un test backend.

La capture locale ignorée de cette session est informative seulement :

| Fichier | Dimensions | Taille | SHA-256 |
|---|---:|---:|---|
| `tauri-studio.png` | 1602 × 932 | 92 973 octets | `c310653df1564661f38eca431d82fc7f5493efe84ee91014f969b580fab1c573` |

Le commit `528a9097` ne modifie pas le flux de sélection ou l'IPC. Il durcit le hook de build et le
harness natif. Son shell exact a été reconstruit depuis le clone propre avec `NO_COLOR=1`, sans
correction externe :

| Fichier | Taille | SHA-256 |
|---|---:|---|
| `forge-studio.exe` | 16 304 640 octets | `4bb12eef7841b9bc4247b6cc12ea4012aa241d75c31f75dea1d43edc2a77efb0` |

La fenêtre finale n'a pas reçu une nouvelle capture commit-bound. Les valeurs IPC ponctuelles de la
session antérieure, environ 2 à 10 ms, prouvent l'aller-retour mais pas une distribution statistique.

La capability suivie contient seulement `core:default`, sans plugin filesystem ou shell, et la CSP
est bornée. Aucun test d'exécution n'a cependant tenté une permission non accordée ; `R0-042` reste
ouvert.

## Ce qui est prouvé

- même code Rust/Bevy et même document pour le natif, le navigateur et le Studio ;
- adapter/device WebGPU obtenus dans Chrome, Edge et la session WebView2 ;
- Chrome 528 charge le bundle, la scène et le monde unique ;
- Edge 528 passe le contrôle visuel et la sélection complète sans erreur console ;
- build Tauri 528 reproductible sous l'environnement régressif `NO_COLOR=1` ;
- IPC Tauri typé et non-reconstruction du monde sur le socle fonctionnel.

## Ce qui n'est pas prouvé

- une nouvelle capture Tauri liée à l'arbre propre 528 ;
- une revue visuelle humaine ;
- le chemin WebGPU indisponible ;
- une distribution statistique frame time, mémoire ou latence navigateur/Studio ;
- les vrais draw calls GPU ;
- le refus effectif d'une permission Tauri non accordée ;
- une CI distante Windows/Linux/WASM.

Verdict : **validation fonctionnelle locale partielle**. Elle ne ferme pas Gate 0 et n'autorise pas
le démarrage des Gates 1–4.
