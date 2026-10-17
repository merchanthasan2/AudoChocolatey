; Quantum Updater installer hooks.
; Uninstall removes the start-at-sign-in tasks the app created (not during an update, which
; keeps them; the app re-points them at start if needed).
!macro NSIS_HOOK_PREUNINSTALL
  ${If} $UpdateMode <> 1
    nsExec::Exec '"$INSTDIR\${MAINBINARYNAME}.exe" --unregister-autostart'
  ${EndIf}
!macroend
