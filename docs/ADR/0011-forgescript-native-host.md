# ADR 0011 — Hôte natif ForgeScript

- Statut : accepté
- Date : 2 août 2026

## Contexte

ForgeScript doit exécuter des règles de module autoritaires sans exposer le moteur, le système de
fichiers, le réseau ou le processus hôte. La machine de développement ne possède pas de compilateur
C++ installé globalement, mais le spike doit rester reproductible et vérifier le véritable bytecode
AngelScript.

## Décision

- ForgeScript embarque AngelScript `2.38.0`, téléchargé depuis le site officiel et vérifié par
  SHA-256. Sa licence zlib est reproduite dans les notices.
- Le bridge C++ exporte une entrée GDExtension minimale ainsi qu’une ABI C étroite utilisée par le
  wrapper C# `IForgeScriptRuntime`.
- Le build Windows utilise Zig `0.16.0` comme toolchain locale portable lorsque Visual Studio/CMake
  ne sont pas disponibles. Zig n’est ni lié aux modules ni distribué dans leur contenu.
- Source, bytecode, version VM et version API forment un artefact indivisible. Un host refuse un
  bytecode produit par une autre version.
- L’API v1 expose seulement `EntityRef`, santé, variable, parole, capacité, spawn, quête et
  déverrouillage. Aucun objet moteur ni accès générique n’est enregistré.
- Les quotas bornent instructions, temps, appels imbriqués et spawns. Un échec retourne un
  diagnostic structuré et ne termine pas le serveur.

## Conséquences

- Le boss de la Crypte utilise deux sources ForgeScript réelles pour sa phase 2 et sa mort ; le test
  headless prouve nova, deux esprits, variable, quête et continuation.
- Le binaire natif est reconstructible par `scripts/Build-ForgeScriptNative.ps1` à partir de sources
  et hashes épinglés.
- Le nom `cast` étant réservé par la grammaire AngelScript, la fonction source est `cast_ability` ;
  elle produit l’événement sémantique `cast` pour le runtime.
- Les futures API doivent rester opt-in, versionnées et accompagnées de quotas et tests de sandbox.
