; OpenRPG Forge — one-file offline Windows installer.
;
; The build orchestrator supplies a frozen PAYLOAD_DIR containing only the
; client payload. This file intentionally does not rebuild, copy, or inspect
; repository sources. Native package ownership and rollback stay in
; forge-delivery.exe; this script owns only the user-facing bootstrap files,
; shortcuts, and the uninstall registration.

Unicode true

!ifndef STUDIO_ONLY
  !error "Select the STUDIO_ONLY profile"
!endif
!ifdef LEGACY_WITH_HUB
  !error "Historical installer retired from the common checkout"
!endif

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

!ifndef PAYLOAD_DIR
  !error "PAYLOAD_DIR must point to the frozen installer payload"
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE must point to the standalone installer"
!endif
!ifndef VERSION
  !define VERSION "0.1.0-preview.20261010.1"
!endif
!ifndef STUDIO_PACKAGE
  !error "STUDIO_PACKAGE must name the verified Studio package"
!endif
!ifndef STUDIO_DELIVERY_CLI
  !error "STUDIO_DELIVERY_CLI must name the verified internal CLI"
!endif
!ifndef STUDIO_DELIVERY_INDEX
  !error "STUDIO_DELIVERY_INDEX must name the verified delivery index"
!endif
!ifndef STUDIO_WEBVIEW2
  !error "STUDIO_WEBVIEW2 must name the pinned offline prerequisite"
!endif
!define APPLICATION_PACKAGE "forge-studio.forgepack"

; The normal assembly recipe emits these stable names. They remain overrideable
; for a locally renamed payload without changing the installer logic.
!ifndef WEBVIEW2_PACKAGE
  !define WEBVIEW2_PACKAGE "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
!endif

!define PRODUCT_NAME "OpenRPG Forge Studio"
!define PRODUCT_KEY "Software\OpenRPG Forge Studio"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenRPGForgeStudio"
!define INTEGRATION_MARKER ".studio-classic-installer-integrated"
!define OWNERSHIP_MARKER ".studio-classic-installer-owned"
!define START_MENU_FOLDER "OpenRPG Forge Studio"
!define WEBVIEW2_GUID "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"

Name "${PRODUCT_NAME}"
OutFile "${OUTPUT_FILE}"
  InstallDir "$LOCALAPPDATA\OpenRPG Forge\StudioInstallation"
InstallDirRegKey HKCU "${PRODUCT_KEY}" "InstallRoot"
RequestExecutionLevel user
; zlib keeps local rebuilds quick for the already packed native content. It is
; embedded in NSIS: the client needs no separate archive utility.
SetCompressor /SOLID zlib
XPStyle on
CRCCheck on
AutoCloseWindow false
ShowInstDetails hide
ShowUninstDetails hide
BrandingText "OpenRPG Forge · livraison locale préparatoire"

; Forest / warm-gold interface, with the original Aube artwork converted only
; from PNG to BMP for the native MUI control. The conversion preserves every
; source pixel and its 1672×941 dimensions.
!define MUI_BGCOLOR "F4EEDC"
!define MUI_TEXTCOLOR "1B4A35"
!define MUI_ABORTWARNING
  !define MUI_WELCOMEPAGE_TITLE "Installer OpenRPG Forge Studio"
  !define MUI_WELCOMEPAGE_TEXT "Installez le Studio et les composants nécessaires à la création et à l'export de jeux.$\r$\n$\r$\nVos projets restent dans votre espace utilisateur."
!define MUI_FINISHPAGE_TITLE "OpenRPG Forge est installé"
!define MUI_FINISHPAGE_TEXT "Les applications et les packages ont été vérifiés puis installés. Vos projets et sauvegardes restent dans votre espace utilisateur."
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Lancer OpenRPG Forge"
!define MUI_FINISHPAGE_RUN_FUNCTION "LaunchForge"
!define MUI_UNCONFIRMPAGE_TEXT_TOP "Le programme de désinstallation va supprimer OpenRPG Forge."
!define MUI_UNCONFIRMPAGE_TEXT "Les fichiers gérés par Forge seront retirés. Vos projets, sauvegardes et données utilisateur restent conservés."
!define MUI_UNFINISHPAGE_TITLE "OpenRPG Forge est désinstallé"
!define MUI_UNFINISHPAGE_TEXT "La désinstallation des composants installés est terminée. Les données utilisateur ont été conservées."

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "French"

Var IntegrationEnabled
Var LogDirectory
Var LogFile
Var Command
Var CommandOutput
Var CommandResult
Var ErrorMessage
Var PackageCount
Var FindHandle
Var FindName
Var WebViewVersion
Var SignaturePath
Var LegacyInstallRoot

Function .onInit
  SetShellVarContext current
  SetRegView 32
  ; A separate, fixed root prevents a Studio-only uninstall from targeting
  ; the historical launcher/Hub installation, including via /D=.
  StrCpy $INSTDIR "$LOCALAPPDATA\OpenRPG Forge\StudioInstallation"
  ${IfNot} ${RunningX64}
    IfSilent +2
    MessageBox MB_ICONSTOP "OpenRPG Forge nécessite Windows 64 bits."
    SetErrorLevel 1
    Abort
  ${EndIf}
  ; The extraction directory must exist before any payload File instruction.
  ; Otherwise the first plugin call initializes a different directory later.
  InitPluginsDir
  StrCpy $IntegrationEnabled "1"
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} "$0" "/NOINTEGRATION" $1
  ${IfNot} ${Errors}
    StrCpy $IntegrationEnabled "0"
  ${EndIf}
FunctionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 32
  GetFullPathName $0 "$LOCALAPPDATA\OpenRPG Forge\StudioInstallation"
  GetFullPathName $1 "$INSTDIR"
  ${If} $0 != $1
    SetErrorLevel 1
    Abort
  ${EndIf}
  StrCpy $IntegrationEnabled "1"
FunctionEnd

; Installer and uninstaller callbacks have separate NSIS namespaces. Keep the
; logging and command runner identical while emitting both namespaces.
!macro DefineCommandFunctions PREFIX
Function ${PREFIX}EnsureLog
  StrCpy $LogDirectory "$LOCALAPPDATA\OpenRPG Forge\InstallerLogs"
  ClearErrors
  CreateDirectory "$LogDirectory"
  ${If} ${Errors}
    StrCpy $LogDirectory "$TEMP"
  ${EndIf}
  StrCpy $LogFile "$LogDirectory\classic-installer-${VERSION}.log"
  Push "--- OpenRPG Forge ${VERSION} ---"
  Call ${PREFIX}AppendLog
FunctionEnd

Function ${PREFIX}AppendLog
  Exch $0
  Push $1
  Push $2
  ClearErrors
  FileOpen $1 "$LogFile" a
  ${IfNot} ${Errors}
    FileSeek $1 0 END $2
    ${If} $2 == 0
      FileWriteUTF16LE /BOM $1 ""
    ${EndIf}
    FileWriteUTF16LE $1 "$0$\r$\n"
    FileClose $1
  ${EndIf}
  Pop $2
  Pop $1
  Pop $0
FunctionEnd

; Run one native process without a shell, preserve its stdout/stderr excerpt,
; and fail the install on every non-zero result.
Function ${PREFIX}RunCommand
  Exch $Command
  Push "COMMAND: $Command"
  Call ${PREFIX}AppendLog
  nsExec::ExecToStack /TIMEOUT=900000 $Command
  Pop $CommandResult
  Pop $CommandOutput
  Push "EXIT: $CommandResult"
  Call ${PREFIX}AppendLog
  Push "OUTPUT: $CommandOutput"
  Call ${PREFIX}AppendLog
  ${If} $CommandResult != 0
    StrCpy $ErrorMessage "La commande a échoué ($CommandResult). Consultez $LogFile."
    Push $ErrorMessage
    Call ${PREFIX}InstallerFailure
  ${EndIf}
  Pop $Command
FunctionEnd

Function ${PREFIX}InstallerFailure
  Exch $ErrorMessage
  Push "ERROR: $ErrorMessage"
  Call ${PREFIX}AppendLog
  IfSilent +2
  MessageBox MB_ICONSTOP "OpenRPG Forge: $ErrorMessage"
  SetErrorLevel 1
  Abort
FunctionEnd
!macroend

!insertmacro DefineCommandFunctions ""
!insertmacro DefineCommandFunctions "un."

Function DetectWebView2
  StrCpy $WebViewVersion ""
  ; EdgeUpdate registers the Evergreen WebView2 runtime in both views. The
  ; 32-bit view is the WOW6432Node registration on this Windows installation.
  ; EdgeUpdate can also register 0.0.0.0 for an absent runtime.
  SetRegView 32
  ReadRegStr $WebViewVersion HKLM "SOFTWARE\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_GUID}" "pv"
  ${If} $WebViewVersion == ""
  ${OrIf} $WebViewVersion == "0.0.0.0"
    ReadRegStr $WebViewVersion HKCU "Software\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_GUID}" "pv"
  ${EndIf}
  SetRegView 64
  ${If} $WebViewVersion == ""
  ${OrIf} $WebViewVersion == "0.0.0.0"
    ReadRegStr $WebViewVersion HKLM "SOFTWARE\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_GUID}" "pv"
  ${EndIf}
  ${If} $WebViewVersion == ""
  ${OrIf} $WebViewVersion == "0.0.0.0"
    ReadRegStr $WebViewVersion HKCU "Software\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_GUID}" "pv"
  ${EndIf}
  ${If} $WebViewVersion == "0.0.0.0"
    StrCpy $WebViewVersion ""
  ${EndIf}
  SetRegView 32
FunctionEnd

Function VerifyWebView2Signature
  StrCpy $SignaturePath "$PLUGINSDIR\payload\${WEBVIEW2_PACKAGE}"
  IfFileExists "$WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" verify_webview_signature_present
  Push "Windows PowerShell est absent : impossible de vérifier la signature WebView2."
  Call InstallerFailure
  verify_webview_signature_present:
  ; Keep the PowerShell script literal and pass the path through the process
  ; environment so apostrophes and spaces in the extraction path are safe.
  System::Call 'kernel32::SetEnvironmentVariableW(w "FORGE_WEBVIEW2_INSTALLER", w "$SignaturePath")i.r0'
  ${If} $0 = 0
    Push "Impossible de préparer la vérification de signature WebView2."
    Call InstallerFailure
    Return
  ${EndIf}
  StrCpy $Command `"$WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "& { $$env:PSModulePath=Join-Path $$env:WINDIR 'System32\WindowsPowerShell\v1.0\Modules'; $$s=Get-AuthenticodeSignature -LiteralPath $$env:FORGE_WEBVIEW2_INSTALLER; if ($$s.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or $$s.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { exit 1 } }"`
  Push $Command
  Call RunCommand
  System::Call 'kernel32::SetEnvironmentVariableW(w "FORGE_WEBVIEW2_INSTALLER", w "")i.r0'
FunctionEnd

Function CollectPackages
  StrCpy $PackageCount "0"
  FindFirst $FindHandle $FindName "$PLUGINSDIR\payload\*.forgepack"
  ${If} $FindName == ""
    ${If} $FindHandle != ""
      FindClose $FindHandle
    ${EndIf}
    Push "Le nombre de packages Forge est nul."
    Call InstallerFailure
  ${EndIf}
  collect_packages_loop:
    IntOp $PackageCount $PackageCount + 1
    FindNext $FindHandle $FindName
    ${If} $FindName != ""
      Goto collect_packages_loop
    ${EndIf}
  FindClose $FindHandle
  Push "Packages trouvés : $PackageCount"
  Call AppendLog
FunctionEnd

Function VerifyFixedPackages
  IfFileExists "$PLUGINSDIR\payload\${APPLICATION_PACKAGE}" verify_fixed_assets
  Push "Package applications absent : ${APPLICATION_PACKAGE}."
  Call InstallerFailure
  verify_fixed_assets:
FunctionEnd

Function InstallWebView2IfMissing
  Call DetectWebView2
  ${If} $WebViewVersion != ""
    Push "WebView2 déjà enregistré : $WebViewVersion"
    Call AppendLog
    Return
  ${EndIf}
  Push "WebView2 absent : vérification de l'installeur Microsoft hors ligne."
  Call AppendLog
  IfFileExists "$PLUGINSDIR\payload\${WEBVIEW2_PACKAGE}" install_webview_signature
  Push "Installeur WebView2 absent : ${WEBVIEW2_PACKAGE}."
  Call InstallerFailure
  install_webview_signature:
  Call VerifyWebView2Signature
  StrCpy $Command '"$PLUGINSDIR\payload\${WEBVIEW2_PACKAGE}" /silent /install'
  Push $Command
  Call RunCommand
  Call DetectWebView2
  ${If} $WebViewVersion == ""
    Push "WebView2 a retourné un succès mais reste absent du registre."
    Call InstallerFailure
  ${EndIf}
  Push "WebView2 installé : $WebViewVersion"
  Call AppendLog
FunctionEnd

Function CopyRootBootstrap
  ClearErrors
  CreateDirectory "$INSTDIR"
  ${If} ${Errors}
    Push "Impossible de créer le répertoire d'installation."
    Call InstallerFailure
  ${EndIf}
  ; Replace the pending marker with the installer version before bootstrapping.
  Call WriteOwnershipMarker
  ClearErrors
  CopyFiles /SILENT "$PLUGINSDIR\payload\forge-delivery.exe" "$INSTDIR"
  ${If} ${Errors}
    Push "Impossible de copier forge-delivery.exe vers la racine d'installation."
    Call InstallerFailure
  ${EndIf}
  ClearErrors
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${If} ${Errors}
    Push "Impossible d'écrire le désinstalleur."
    Call InstallerFailure
  ${EndIf}
FunctionEnd

Function WriteOwnershipMarker
  ; This private marker is written even for /NOINTEGRATION. It proves that
  ; this bootstrap owns the root before the uninstaller calls the CLI.
  ClearErrors
  FileOpen $0 "$INSTDIR\${OWNERSHIP_MARKER}" w
  ${If} ${Errors}
    Push "Impossible d'écrire le marqueur de propriété de l'installation."
    Call InstallerFailure
  ${EndIf}
  ClearErrors
  FileWrite $0 "${VERSION}$\r$\n"
  ${If} ${Errors}
    Push "Impossible d'écrire la version du marqueur de propriété."
    Call InstallerFailure
  ${EndIf}
  ClearErrors
  FileClose $0
  ${If} ${Errors}
    Push "Impossible de fermer le marqueur de propriété."
    Call InstallerFailure
  ${EndIf}
FunctionEnd
!macro CheckInstallerError MESSAGE
  ${If} ${Errors}
    Push "${MESSAGE}"
    Call InstallerFailure
  ${EndIf}
!macroend

Function RegisterIntegration
  ${If} $IntegrationEnabled != "1"
    Push "Mode /NOINTEGRATION : raccourcis et registre ignorés."
    Call AppendLog
    Return
  ${EndIf}

  ClearErrors
  WriteRegStr HKCU "${PRODUCT_KEY}" "InstallRoot" "$INSTDIR"
  !insertmacro CheckInstallerError "Impossible d'enregistrer le chemin d'installation."
  ClearErrors
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT_NAME}"
  !insertmacro CheckInstallerError "Impossible d'enregistrer le nom de l'application."
  ClearErrors
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  !insertmacro CheckInstallerError "Impossible d'enregistrer la version de l'application."
  ClearErrors
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "OpenRPG Forge"
  !insertmacro CheckInstallerError "Impossible d'enregistrer l'éditeur de l'application."
  ClearErrors
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  !insertmacro CheckInstallerError "Impossible d'enregistrer l'emplacement d'installation."
  ClearErrors
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\forge-delivery.exe"
  !insertmacro CheckInstallerError "Impossible d'enregistrer l'icône de l'application."
  ClearErrors
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  !insertmacro CheckInstallerError "Impossible d'enregistrer le désinstalleur."
  ClearErrors
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  !insertmacro CheckInstallerError "Impossible d'enregistrer les options de désinstallation."
  ClearErrors
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  !insertmacro CheckInstallerError "Impossible d'enregistrer les options de réparation."

  ClearErrors
  CreateDirectory "$SMPROGRAMS\${START_MENU_FOLDER}"
  !insertmacro CheckInstallerError "Impossible de créer le dossier du menu Démarrer."
  ClearErrors
  CreateShortCut "$SMPROGRAMS\${START_MENU_FOLDER}\OpenRPG Forge Studio.lnk" "$INSTDIR\forge-delivery.exe" 'launch "$INSTDIR" editor' "$INSTDIR\forge-delivery.exe" 0 SW_SHOWMINIMIZED ""
  !insertmacro CheckInstallerError "Impossible de créer le raccourci du Studio."
  ClearErrors
  CreateShortCut "$SMPROGRAMS\${START_MENU_FOLDER}\Désinstaller OpenRPG Forge.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0 SW_SHOWNORMAL ""
  !insertmacro CheckInstallerError "Impossible de créer le raccourci de désinstallation."

  ClearErrors
  FileOpen $0 "$INSTDIR\${INTEGRATION_MARKER}" w
  !insertmacro CheckInstallerError "Impossible d'écrire le marqueur d'intégration."
  ClearErrors
  FileWrite $0 "${VERSION}$\r$\n"
  !insertmacro CheckInstallerError "Impossible d'écrire le marqueur de version."
  ClearErrors
  FileClose $0
  !insertmacro CheckInstallerError "Impossible de fermer le marqueur d'intégration."
  Push "Raccourcis et désinstallation enregistrés pour $INSTDIR"
  Call AppendLog
FunctionEnd
Function LaunchForge
  Exec '"$INSTDIR\forge-delivery.exe" launch "$INSTDIR" editor'
FunctionEnd

Section "OpenRPG Forge"
  Call EnsureLog
  Push "Début de l'installation dans $INSTDIR"
  Call AppendLog

  SetOutPath "$PLUGINSDIR\payload"
  DetailPrint "Extraction des composants hors ligne..."
  File /oname=forge-delivery.exe "${STUDIO_DELIVERY_CLI}"
  File /oname=${APPLICATION_PACKAGE} "${STUDIO_PACKAGE}"
  ; Refuse foreign roots and recover an interruption after the first
  ; activation only when the active Studio package matches this installer.
  IfFileExists "$INSTDIR\${OWNERSHIP_MARKER}" studio_root_ready
  StrCpy $Command '"$PLUGINSDIR\payload\forge-delivery.exe" check-studio-reinstall-root "$INSTDIR" "$PLUGINSDIR\payload\${APPLICATION_PACKAGE}"'
  Push $Command
  Call RunCommand
  studio_root_ready:
  File /oname=delivery.json "${STUDIO_DELIVERY_INDEX}"
  File /oname=${WEBVIEW2_PACKAGE} "${STUDIO_WEBVIEW2}"

  DetailPrint "Vérification et installation de WebView2 si nécessaire..."
  Call InstallWebView2IfMissing
  DetailPrint "Vérification des packages Forge..."
  Call VerifyFixedPackages
  Call CollectPackages

  ; plan-file is deliberately written to the temporary extraction directory;
  ; Apply remains the only operation allowed to publish the installation head.
  DetailPrint "Préparation de l'installation..."
  ReadRegStr $LegacyInstallRoot HKCU "Software\OpenRPG Forge" "InstallRoot"
  ${If} $LegacyInstallRoot == ""
    System::Call 'kernel32::SetEnvironmentVariableW(w "FORGE_STUDIO_LEGACY_INSTALL_ROOT", p 0)i.r1'
  ${Else}
    System::Call 'kernel32::SetEnvironmentVariableW(w "FORGE_STUDIO_LEGACY_INSTALL_ROOT", w "$LegacyInstallRoot")i.r1'
    ${If} $1 = 0
      Push "Impossible de préparer la lecture de l'installation Forge historique."
      Call InstallerFailure
    ${EndIf}
  ${EndIf}
  StrCpy $Command '"$PLUGINSDIR\payload\forge-delivery.exe" plan-studio-installer "$INSTDIR" "$PLUGINSDIR\privateplan.json" "$PLUGINSDIR\payload\${APPLICATION_PACKAGE}"'
  Push $Command
  Call RunCommand
  System::Call 'kernel32::SetEnvironmentVariableW(w "FORGE_STUDIO_LEGACY_INSTALL_ROOT", p 0)i.r1'
  IfFileExists "$PLUGINSDIR\privateplan.json" apply_delivery_plan
  Push "Le CLI n'a pas écrit privateplan.json."
  Call InstallerFailure
  apply_delivery_plan:
  DetailPrint "Installation des applications et du contenu..."
  StrCpy $Command '"$PLUGINSDIR\payload\forge-delivery.exe" apply-studio-installer "$PLUGINSDIR\privateplan.json"'
  Push $Command
  Call RunCommand

  ${If} $LegacyInstallRoot != ""
    DetailPrint "Récupération des modules de l’installation historique..."
    StrCpy $Command '"$PLUGINSDIR\payload\forge-delivery.exe" recover-legacy-modules "$LegacyInstallRoot" "$INSTDIR"'
    Push $Command
    Call RunCommand
  ${EndIf}
  DetailPrint "Installation des composants et du désinstalleur..."
  Call CopyRootBootstrap
  Call RegisterIntegration
  Push "Installation terminée. Journal : $LogFile"
  Call AppendLog
SectionEnd

Section "Uninstall"
  Call un.EnsureLog
  DetailPrint "Suppression des fichiers de Forge..."
  Push "Désinstallation demandée pour $INSTDIR"
  Call un.AppendLog
  ; The private marker is the first domain guard. The registered root is the
  ; recovery guard for an integration that lost its marker. The CLI then owns
  ; the package-state validation before any bootstrap file is removed.
  IfFileExists "$INSTDIR\${OWNERSHIP_MARKER}" uninstall_domain_valid
  ReadRegStr $0 HKCU "${PRODUCT_KEY}" "InstallRoot"
  ${If} $0 == "$INSTDIR"
    Goto uninstall_domain_valid
  ${EndIf}
  Push "Racine d'installation non reconnue : aucune suppression effectuée."
  Call un.InstallerFailure
  uninstall_domain_valid:
  IfFileExists "$INSTDIR\forge-delivery.exe" uninstall_owned_packages
  Push "forge-delivery.exe est absent : réinstallez ou réparez OpenRPG Forge avant de désinstaller."
  Call un.InstallerFailure
  uninstall_owned_packages:
  StrCpy $Command '"$INSTDIR\forge-delivery.exe" uninstall-studio "$INSTDIR"'
  Push $Command
  Call un.RunCommand
  ; Only these files were installed directly by this NSIS wrapper. Package
  ; payload ownership remains with forge-delivery.exe and is never removed by
  ; a recursive NSIS delete.
  Delete "$INSTDIR\forge-delivery.exe"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$INSTDIR\${OWNERSHIP_MARKER}"
  Delete "$INSTDIR\${INTEGRATION_MARKER}"
  ReadRegStr $0 HKCU "${PRODUCT_KEY}" "InstallRoot"
  ${If} $0 == "$INSTDIR"
    Delete "$SMPROGRAMS\${START_MENU_FOLDER}\OpenRPG Forge Studio.lnk"
    Delete "$SMPROGRAMS\${START_MENU_FOLDER}\Désinstaller OpenRPG Forge.lnk"
    RMDir "$SMPROGRAMS\${START_MENU_FOLDER}"
    DeleteRegValue HKCU "${PRODUCT_KEY}" "InstallRoot"
    DeleteRegKey /ifempty HKCU "${PRODUCT_KEY}"
    DeleteRegKey HKCU "${UNINSTALL_KEY}"
  ${EndIf}
  ; Never recurse here: a non-empty root is evidence that package ownership
  ; cleanup failed or that a user-owned file remains and must be preserved.
  RMDir "$INSTDIR"
  Push "Désinstallation NSIS terminée ; le journal est conservé sous $LogFile"
  Call un.AppendLog
SectionEnd
