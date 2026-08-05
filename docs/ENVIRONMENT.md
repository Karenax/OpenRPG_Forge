# Environnement de bootstrap

Vérification effectuée le 5 août 2026 sur la machine locale après installation explicite des
prérequis autorisés par l’utilisateur.

## Détecté

| Composant | Résultat |
|---|---|
| Système | Microsoft Windows 11 Professionnel, version 10.0.26200, build 26200, 64 bits |
| Git | `2.55.0.windows.2` dans `C:\Program Files\Git\cmd\git.exe` |
| SDK .NET | `10.0.302`, épinglé par `global.json` |
| Runtime .NET utilisé | `Microsoft.NETCore.App 10.0.10` |
| Godot .NET | `4.7.1.stable.mono.official.a13da4feb` |
| Python | `3.13.14` pour le graphe d’architecture |
| Toolchain natif portable | Zig `0.16.0`, archive et SHA-256 épinglés par `scripts/Build-ForgeScriptNative.ps1` |
| Blender LTS | `4.5.10`, installé dans `C:\Program Files\Blender Foundation\Blender 4.5\blender.exe` |

## Manquant ou non détecté

| Composant | État |
|---|---|
| CMake | Non trouvé dans le `PATH` ; non requis au premier build |
| Compilateur C/C++ système | `cl`, `clang` et `gcc` non trouvés ; Zig fournit le compilateur portable du host ForgeScript |

## État du gate

T0.1 et le MVP sont franchis. La solution cible `net10.0`, garde C# 12 comme version de langage
explicite et utilise le SDK .NET 4.7.1 du moteur pour les deux applications. Build Release, tests,
smokes application/runtime, MCP et sauvegarde/reprise sont exécutables par `scripts/Verify.ps1`.

CMake n'est pas requis par le build natif actuel, réalisé avec Zig. Blender reste optionnel pour le
runtime joueur ; lorsqu'il est présent, `scripts/Verify.ps1` exécute le vrai round-trip OBJ vers GLB.
Le chemin peut être forcé avec `OPENRPG_BLENDER` sur une autre machine.
