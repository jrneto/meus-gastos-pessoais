#!/usr/bin/env bash
# Empacota o Lambda trigger CustomMessage do Cognito (FEAT-34) em Native
# AOT. Mesmo padrão de build-account-trigger.sh — ver aquele arquivo/
# Dockerfile.build para os achados completos sobre glibc/AOT.
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/build-custom-message-trigger.sh

set -euo pipefail

cd "$(dirname "$0")/../.."  # backend/

OUT_DIR="infra/lambda/out-custom-message-trigger"

rm -rf "$OUT_DIR"

docker build \
  -f infra/lambda/Dockerfile.build-custom-message-trigger \
  --target export \
  --output "type=local,dest=$OUT_DIR" \
  .

mv "$OUT_DIR/custom-message-trigger-function.zip" infra/lambda/custom-message-trigger-function.zip
rm -rf "$OUT_DIR"

echo "Gerado: infra/lambda/custom-message-trigger-function.zip"
