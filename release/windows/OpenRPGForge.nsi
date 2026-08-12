Unicode true
SetCompressor /SOLID lzma
CRCCheck force
RequestExecutionLevel user

!include "MUI2.nsh"

!ifndef SOURCE_DIR
  !error "SOURCE_DIR is required"
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE is required"
!endif
!ifndef PRODUCT_VERSION
  !define PRODUCT_VERSION "0.1.0-dev"
!endif
!ifndef ICON_FILE
  !error "ICON_FILE is required"
!endif

!define PRODUCT_NAME "OpenRPG Forge Creator"
!define PRODUCT_PUBLISHER "OpenRPG Forge"
!define PRODUCT_REGISTRY_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenRPGForgeCreator"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\OpenRPG Forge"
InstallDirRegKey HKCU "${PRODUCT_REGISTRY_KEY}" "InstallLocation"
Icon "${ICON_FILE}"
UninstallIcon "${ICON_FILE}"
BrandingText "OpenRPG Forge"

VIProductVersion "0.1.0.0"
VIAddVersionKey /LANG=1036 "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey /LANG=1036 "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey /LANG=1036 "FileDescription" "Installateur OpenRPG Forge Creator"
VIAddVersionKey /LANG=1036 "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=1036 "ProductVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=1036 "LegalCopyright" "OpenRPG Forge contributors"

!define MUI_ABORTWARNING
!define MUI_ICON "${ICON_FILE}"
!define MUI_UNICON "${ICON_FILE}"
!define MUI_FINISHPAGE_RUN "$INSTDIR\OpenRPG_Forge_Creator.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Lancer OpenRPG Forge Creator"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "French"
!insertmacro MUI_LANGUAGE "English"

Section "OpenRPG Forge Creator" MainSection
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${SOURCE_DIR}\*.*"

  FileOpen $0 "$INSTDIR\.openrpg-forge-install" w
  FileWrite $0 "OpenRPG Forge Creator ${PRODUCT_VERSION}$\r$\n"
  FileClose $0

  WriteUninstaller "$INSTDIR\Uninstall OpenRPG Forge.exe"
  CreateDirectory "$SMPROGRAMS\OpenRPG Forge"
  CreateShortcut "$SMPROGRAMS\OpenRPG Forge\OpenRPG Forge Creator.lnk" \
    "$INSTDIR\OpenRPG_Forge_Creator.exe" "" "$INSTDIR\OpenRPG_Forge_Creator.exe"
  CreateShortcut "$SMPROGRAMS\OpenRPG Forge\Désinstaller OpenRPG Forge.lnk" \
    "$INSTDIR\Uninstall OpenRPG Forge.exe"

  WriteRegStr HKCU "${PRODUCT_REGISTRY_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKCU "${PRODUCT_REGISTRY_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr HKCU "${PRODUCT_REGISTRY_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  WriteRegStr HKCU "${PRODUCT_REGISTRY_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${PRODUCT_REGISTRY_KEY}" "DisplayIcon" "$INSTDIR\OpenRPG_Forge_Creator.exe"
  WriteRegStr HKCU "${PRODUCT_REGISTRY_KEY}" "UninstallString" '"$INSTDIR\Uninstall OpenRPG Forge.exe"'
  WriteRegStr HKCU "${PRODUCT_REGISTRY_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall OpenRPG Forge.exe" /S'
  WriteRegDWORD HKCU "${PRODUCT_REGISTRY_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${PRODUCT_REGISTRY_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  IfFileExists "$INSTDIR\.openrpg-forge-install" uninstall_allowed uninstall_refused

  uninstall_refused:
    MessageBox MB_OK|MB_ICONSTOP "Le dossier ne contient pas le marqueur OpenRPG Forge. Désinstallation annulée." /SD IDOK
    Abort

  uninstall_allowed:
    Delete "$SMPROGRAMS\OpenRPG Forge\OpenRPG Forge Creator.lnk"
    Delete "$SMPROGRAMS\OpenRPG Forge\Désinstaller OpenRPG Forge.lnk"
    RMDir "$SMPROGRAMS\OpenRPG Forge"
    DeleteRegKey HKCU "${PRODUCT_REGISTRY_KEY}"
    RMDir /r "$INSTDIR"
SectionEnd
