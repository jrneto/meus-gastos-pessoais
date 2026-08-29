#!/usr/bin/env bash
# Builda a Api publicada em Native AOT dentro da mesma família de imagem
# base da Lambda real (provided.al2023), sobe num container acessível
# via Lambda Runtime Interface Emulator (RIE) e roda a suíte de testes
# integrados (FEAT-29) contra ela — sem nenhuma credencial/rede AWS
# real, reaproveitando LocalStack + cognito-local (FEAT-18) já
# existentes. É esse caminho — binário publicado, invocado como a
# Lambda real seria — que expõe erro específico de Native AOT antes de
# qualquer deploy real. Ver
# backend/specs/FEAT-29-testes-integrados/plan.md, "Container local".
#
# Ciclo completo (sobe → roda a suíte → desliga sempre). Pra debugar um
# teste específico com breakpoint no VS Code (container fica no ar
# entre execuções), use local-env-up.sh/local-env-down.sh diretamente —
# ver backend/tests/GastosApp.IntegrationTests/README.md.
#
# Uso: rodar a partir de backend/
#   ./infra/lambda/run-local.sh
set -euo pipefail

cd "$(dirname "$0")/../.."  # backend/

cleanup() {
  ./infra/lambda/local-env-down.sh
}
trap cleanup EXIT

./infra/lambda/local-env-up.sh

echo "==> Rodando testes integrados (modo local, contra o container)..."
# INTEGRATION_TESTS_TRANSPORT=rie é obrigatório aqui — sem ela, o
# default de "local" passou a ser a Api via Kestrel/dotnet run (achado
# real: Test Explorer do VS Code não injeta env var de forma confiável,
# ver README.md), o que faria este script (que builda e sobe o
# container) não testar o binário que ele acabou de subir.
INTEGRATION_TESTS_MODE=local INTEGRATION_TESTS_TRANSPORT=rie \
  dotnet test tests/GastosApp.IntegrationTests -c Release --filter "Category=Integration"
