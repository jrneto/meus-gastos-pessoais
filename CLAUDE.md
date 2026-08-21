# GastosApp — Monorepo

Monorepo com dois contextos independentes:
- **`/backend`** — API .NET (Clean Architecture), foco atual do projeto.
- **`/frontend`** — React (ainda não iniciado).

**Não existe infraestrutura compartilhada entre contextos.** Cada contexto
tem sua própria pasta `infra/` (`backend/infra/`, `frontend/infra/`), com
seu próprio `CLAUDE.md`, provisionada de forma independente (futuramente
via Terraform).

O foco atual do projeto é **exclusivamente o backend**. Frontend (e sua
infra) ainda não foram iniciados.

## Roteamento por contexto

- Ao trabalhar em algo dentro de `/backend` (incluindo `/backend/infra`),
  sempre consulte `/backend/CLAUDE.md`, `/backend/infra/CLAUDE.md` e os
  documentos em `/backend/docs/` antes de gerar código.
- Ao trabalhar em algo dentro de `/frontend` (incluindo `/frontend/infra`),
  consulte `/frontend/CLAUDE.md` e `/frontend/docs/` (a serem criados
  quando o frontend for iniciado) e `/frontend/infra/CLAUDE.md`.
- **Nunca aplicar decisões arquiteturais ou de infraestrutura de um
  contexto ao outro por padrão** (ex.: Clean Architecture é uma decisão do
  backend — não impor a outros contextos sem que faça sentido para eles).

## Organização de specs

Toda spec vive em sua própria subpasta: `{contexto}/specs/{FEAT-XX-nome}/`,
contendo `spec.md`, `plan.md` e `tasks.md`. **Nunca criar arquivos soltos
direto em `specs/`.** Hoje só existe `backend/specs/`.

## Fluxo de Git (branches e PRs)

Desde a FEAT-10 (frontend), toda feature em **Fluxo Completo** segue:

- A branch nasce **no `/specify`, antes de qualquer código** — a
  partir de `develop`, nomeada exatamente igual à pasta criada em
  `{contexto}/specs/` (ex.: `FEAT-10-nome-feature`). `spec.md`,
  `plan.md`, `tasks.md` e todo o código da feature vivem nessa branch;
  `develop` só recebe tudo de uma vez quando o PR é mergeado.
- Ao final da implementação, um PR da branch para `develop` é aberto
  automaticamente pelo CI/CD de cada contexto (backend e frontend têm
  workflow próprio — `{contexto}-feature-pr.yml` — disparado a cada
  push verde na branch).
- Depois de validado em homologação, uma **GitHub Release** publicada
  manualmente dispara o deploy de produção; se esse deploy for
  bem-sucedido, um PR `develop → main` é aberto automaticamente —
  mantém `main` sempre refletindo o que está de fato em produção
  (antes disso, `main` podia ficar desatualizada indefinidamente).
- **Merge dos PRs (feature→develop e develop→main) continua sempre
  manual** — os workflows só abrem o PR, nunca mergeiam sozinhos.
- **Modo Leve também usa branch própria + PR automático**, mas sem
  passar por `/specify`: a branch nasce a partir de `develop` com
  prefixo `fix/` (ex.: `fix/nome-do-bug`), pois não há pasta em
  `specs/` para nomeá-la. O workflow `{contexto}-feature-pr.yml`
  dispara tanto para `FEAT-*` quanto para `fix/*`. Ainda não é
  necessário criar pasta em `specs/` nem passar por `/specify` —
  ver regras completas em "Modo Leve vs Fluxo Completo" abaixo.

## Infraestrutura

Toda infraestrutura, em qualquer contexto, é **AWS**. O plano futuro é
provisionar cada `infra/` via **Terraform**. Não gerar código Terraform
até que seja solicitado explicitamente. Regras específicas de cada
contexto (ex.: backend conecta-se diretamente aos recursos AWS reais em
desenvolvimento local, sem LocalStack/Kong) vivem no `CLAUDE.md` de
`{contexto}/infra/`, não aqui.

## Modo Leve vs Fluxo Completo

Nem toda mudança precisa do ciclo completo spec → plan → tasks → review.
Antes de iniciar qualquer trabalho, classifique a demanda.

### Segue o FLUXO COMPLETO (`/specify` → `/plan` → `/tasks` → `/review`) quando:
- Nova feature que introduz um novo domínio/entidade de negócio
- Mudança que toca 2 ou mais camadas (ex: Api + Application + Infrastructure)
- Mudança que introduz ou altera um novo recurso AWS (nova tabela, novo
  pool Cognito, novo endpoint público)
- Mudança que altera contrato de API já publicado (breaking change)
- Qualquer coisa com mais de ~1 dia de trabalho estimado

### Pode ir para MODO LEVE (implementação direta, sem spec/plan/tasks) quando:
- Bugfix pontual sem mudança de contrato
- Ajuste de configuração, log, validação simples
- Refatoração interna que não muda comportamento externo
- CRUD simples que já segue padrão 100% estabelecido em feature anterior
- Correção de teste, ajuste de nomenclatura, formatação

### Regra em caso de dúvida:
Se não tiver certeza se é modo leve ou fluxo completo, pergunte ao usuário
antes de decidir. Nunca decida sozinho pular o fluxo completo para algo
que pareça tocar arquitetura, contrato de API ou infraestrutura.

### No modo leve:
- Ainda é obrigatório consultar `backend/docs/constitution.md` e
  `backend/CLAUDE.md` antes de implementar
- Não é necessário criar pasta em `specs/`
- Ao final, resuma em 2-3 linhas o que foi feito e por quê (pode ir direto
  na mensagem de commit ou no chat, não precisa de arquivo)

Aplique essa mesma lógica de Modo Leve vs Fluxo Completo para o contexto
frontend quando ele for iniciado no futuro.
