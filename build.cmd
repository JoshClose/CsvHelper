:; set -eo pipefail
:; ./build.sh "$@"
:; exit $?

@ECHO OFF
powershell -ExecutionPolicy ByPass -NoProfile %~dp0build.ps1 %*
