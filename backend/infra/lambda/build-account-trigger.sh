#!/usr/bin/env bash
# Empacota o Lambda trigger PostConfirmation do Cognito (FEAT-19) em
# Native AOT. Mesmo padrão de build.sh (artefato da API) — ver aquele
# script/Dockerfile.build para os achados completos sobre glibc/AOT.
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/build-account-trigger.sh

set -euo pipefail

cd "$(dirname "$0")/../.."  # backend/

OUT_DIR="infra/lambda/out-account-trigger"

rm -rf "$OUT_DIR"

docker build \
  -f infra/lambda/Dockerfile.build-account-trigger \
  --target export \
  --output "type=local,dest=$OUT_DIR" \
  .

mv "$OUT_DIR/account-trigger-function.zip" infra/lambda/account-trigger-function.zip
rm -rf "$OUT_DIR"

echo "Gerado: infra/lambda/account-trigger-function.zip"
