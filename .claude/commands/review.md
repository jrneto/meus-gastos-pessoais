---
description: Compara a implementação atual de uma feature com os critérios de aceite da sua spec.md e aponta divergências
---

Feature: $ARGUMENTS

Contexto padrão: **backend** (`/backend`), salvo indicação explícita de `/frontend` ou `/infra`.

1. Resolva a pasta da feature: `{contexto}/specs/{FEAT-XX-nome-feature}/`. Se não existir, avise e pare.
2. Leia `spec.md` (critérios de aceite e contratos), `plan.md` (se existir) e `tasks.md` (se existir) dessa pasta.
3. Explore o código atual do contexto (`{contexto}/src` ou equivalente) e os testes relevantes para localizar a implementação de cada critério de aceite.
4. Para cada critério de aceite em `spec.md`, reporte um de:
   - **Atendido** — implementado conforme especificado, com referência a arquivo(s)/linha(s)
   - **Divergente** — implementado, mas de forma diferente do especificado (explique a diferença e o risco)
   - **Não implementado** — sem código correspondente encontrado
5. Verifique também se os contratos de API documentados em `spec.md` (paths, request/response, status codes, corpos de erro) batem exatamente com o código.
6. Não altere código neste comando — é uma revisão somente leitura. Ao final, apresente um resumo objetivo (lista) das divergências encontradas, priorizadas pelo impacto, e pergunte ao usuário se deseja que você corrija alguma delas.
