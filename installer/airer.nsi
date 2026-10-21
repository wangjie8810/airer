; Per-user setup for Airer (晾衣绳). No administrator rights.
; Build with makensis /DARCH=x64|arm64 /DVERSION=1.1.0 /DSRCDIR=... /DOUTFILE=...

Unicode True
ManifestDPIAware true

!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"

!ifndef ARCH
  !error "Pass /DARCH=x64 or /DARCH=arm64"
!endif
!ifndef VERSION
  !error "Pass /DVERSION=1.1.0"
!endif
!ifndef SRCDIR
  !error "Pass /DSRCDIR=path to the published folder"
!endif
!ifndef OUTFILE
  !error "Pass /DOUTFILE=path to the setup exe"
!endif

!define APPNAME "晾衣绳"
!define REGKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Airer"

Name "${APPNAME}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\Airer"
InstallDirRegKey HKCU "Software\Airer" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "${APPNAME} ${VERSION}"
ShowInstDetails show
ShowUninstDetails show

!define MUI_ICON "..\src\Airer\Assets\airer.ico"
!define MUI_UNICON "..\src\Airer\Assets\airer.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "$(WelcomeTitle)"
!define MUI_WELCOMEPAGE_TEXT "$(WelcomeText)"
!define MUI_FINISHPAGE_TITLE "$(FinishTitle)"
!define MUI_FINISHPAGE_TEXT "$(FinishText)"
!define MUI_FINISHPAGE_RUN "$INSTDIR\Airer.exe"
!define MUI_FINISHPAGE_RUN_TEXT "$(RunText)"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

LangString WelcomeTitle ${LANG_SIMPCHINESE} "安装晾衣绳"
LangString WelcomeTitle ${LANG_ENGLISH} "Install Airer"
LangString WelcomeText ${LANG_SIMPCHINESE} "截图会挂在屏幕顶部的绳子上。$\r$\n$\r$\n这个安装包是 ${ARCH} 版，装到当前用户，不需要管理员。卸载不会删除 %AppData%\Airer 里的截图和设置。"
LangString WelcomeText ${LANG_ENGLISH} "Screenshots hang on a line at the top of the screen.$\r$\n$\r$\nThis is the ${ARCH} package. It installs for the current user and does not need an administrator. Uninstall leaves screenshots and settings in %AppData%\Airer."
LangString FinishTitle ${LANG_SIMPCHINESE} "安装完成"
LangString FinishTitle ${LANG_ENGLISH} "Installed"
LangString FinishText ${LANG_SIMPCHINESE} "晾衣绳已经装好，会留在托盘里。$\r$\n$\r$\n默认 Ctrl+Alt+S 区域截图，Ctrl+Alt+3 截取当前屏幕，Ctrl+Alt+T 显示或隐藏绳子。"
LangString FinishText ${LANG_ENGLISH} "Airer is installed and stays in the tray.$\r$\n$\r$\nCtrl+Alt+S snips a region, Ctrl+Alt+3 grabs the current screen, and Ctrl+Alt+T shows or hides the line."
LangString RunText ${LANG_SIMPCHINESE} "启动晾衣绳"
LangString RunText ${LANG_ENGLISH} "Start Airer"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "晾衣绳"
VIAddVersionKey "CompanyName" "Airer"
VIAddVersionKey "FileDescription" "晾衣绳安装程序"
VIAddVersionKey "FileVersion" "${VERSION}.0"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "Portions Copyright (c) 2026 Alejandro Buján. Airer is an independent port."

Function .onInit
  !if "${ARCH}" == "arm64"
    ${IfNot} ${IsNativeARM64}
      MessageBox MB_OK|MB_ICONSTOP "这是 ARM64 安装包。64 位电脑请安装 Airer-Setup-win-x64.exe。"
      Abort
    ${EndIf}
  !else
    ${IfNot} ${RunningX64}
      MessageBox MB_OK|MB_ICONSTOP "晾衣绳需要 64 位 Windows 10 或 Windows 11。"
      Abort
    ${EndIf}
  !endif
FunctionEnd

Section "晾衣绳"
  nsExec::ExecToLog "taskkill /IM Airer.exe /F"
  Pop $0

  SetOutPath "$INSTDIR"
  File /r "${SRCDIR}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS"
  CreateShortcut "$SMPROGRAMS\晾衣绳.lnk" "$INSTDIR\Airer.exe" "" "$INSTDIR\Airer.exe" 0

  WriteRegStr HKCU "Software\Airer" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${REGKEY}" "DisplayName" "晾衣绳"
  WriteRegStr HKCU "${REGKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${REGKEY}" "Publisher" "Airer"
  WriteRegStr HKCU "${REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${REGKEY}" "DisplayIcon" "$INSTDIR\Airer.exe"
  WriteRegStr HKCU "${REGKEY}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegDWORD HKCU "${REGKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${REGKEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegDWORD HKCU "${REGKEY}" "EstimatedSize" $0
SectionEnd

Section "Uninstall"
  nsExec::ExecToLog "taskkill /IM Airer.exe /F"
  Pop $0

  Delete "$SMPROGRAMS\晾衣绳.lnk"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${REGKEY}"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Airer"
  DeleteRegKey HKCU "Software\Airer"
SectionEnd
