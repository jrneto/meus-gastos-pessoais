#!/usr/bin/env bash
# Empacota a API em Native AOT para deploy na Lambda (runtime
# provided.al2023). Roda o Dockerfile.build (container efêmero, nenhuma
# imagem é publicada) e gera infra/lambda/function.zip, já com a
# permissão de execução do "bootstrap" preservada (zipado dentro do
# container Linux, não no Windows).
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/build.sh

set -euo pipefail
# pipefail já cobre pipes usados dentro deste script; quem chama este
# script via `| tail` ou similar deve ligar pipefail no shell de fora
# também, senão o código de saída do pipe reflete só o último comando.

cd "$(dirname "$0")/../.."  # backend/

OUT_DIR="infra/lambda/out"

rm -rf "$OUT_DIR"

docker build \
  -f infra/lambda/Dockerfile.build \
  --target export \
  --output "type=local,dest=$OUT_DIR" \
  .

mv "$OUT_DIR/function.zip" infra/lambda/function.zip
rm -rf "$OUT_DIR"

echo "Gerado: infra/lambda/function.zip"
