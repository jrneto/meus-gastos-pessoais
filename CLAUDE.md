# GastosApp — Monorepo

Monorepo com dois contextos independentes, ambos ativos em paralelo,
cada um com seu próprio ciclo SDD (`/specify` → `/plan` → `/tasks` →
`/review`):
- **`/backend`** — API .NET (Clean Architecture).
- **`/frontend`** — SPA React (feature-based/bulletproof-react).

**Não existe infraestrutura compartilhada entre contextos.** Cada contexto
tem sua própria pasta `infra/` (`backend/infra/`, `frontend/infra/`), com
seu próprio `CLAUDE.md`, provisionada de forma independente via Terraform.

A arquitetura do sistema como um todo (backend + frontend, modelo C4
até o nível 3) vive em [`/docs/architecture.md`](docs/architecture.md)
— nenhum dos dois contextos, isoladamente, a representa por completo.

## Roteamento por contexto

- Ao trabalhar em algo dentro de `/backend` (incluindo `/backend/infra`),
  sempre consulte `/backend/CLAUDE.md`, `/backend/infra/CLAUDE.md` e os
  documentos em `/backend/docs/` antes de gerar código.
- Ao trabalhar em algo dentro de `/frontend` (incluindo `/frontend/infra`),
  sempre consulte `/frontend/CLAUDE.md`, `/frontend/infra/CLAUDE.md` e os
  documentos em `/frontend/docs/` antes de gerar código.
- **Nunca aplicar decisões arquiteturais ou de infraestrutura de um
  contexto ao outro por padrão** (ex.: Clean Architecture é uma decisão do
  backend — não impor a outros contextos sem que faça sentido para eles).

## Organização de specs

Toda spec vive em sua própria subpasta: `{contexto}/specs/{FEAT-XX-nome}/`,
contendo `spec.md`, `plan.md` e `tasks.md`. **Nunca criar arquivos soltos
direto em `specs/`.** A numeração `FEAT-XX` é independente por contexto
(ex.: `backend/specs/FEAT-19-...` e `frontend/specs/FEAT-19-...` são
features diferentes, sem relação entre si).

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

Toda infraestrutura, em qualquer contexto, é **AWS**, provisionada via
**Terraform** (produção e homologação de ambos os contextos já estão
sob Terraform). Não gerar/alterar código Terraform para um recurso além
do já existente até que seja solicitado explicitamente. Regras
específicas de cada contexto (ex.: backend roda local contra emuladores
Docker — LocalStack + cognito-local, sem depender de credenciais AWS
reais) vivem no `CLAUDE.md` de `{contexto}/infra/`, não aqui.

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
- Ainda é obrigatório consultar `{contexto}/docs/constitution.md` e
  `{contexto}/CLAUDE.md` antes de implementar
- Não é necessário criar pasta em `specs/`
- Ao final, resuma em 2-3 linhas o que foi feito e por quê (pode ir direto
  na mensagem de commit ou no chat, não precisa de arquivo)

Vale a mesma lógica de Modo Leve vs Fluxo Completo para o contexto
frontend — troque as referências a `backend/docs/constitution.md` e
`backend/CLAUDE.md` pelas equivalentes em `frontend/`.
