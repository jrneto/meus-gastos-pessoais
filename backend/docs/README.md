# Spec-Driven Development (SDD) — Backend GastosApp

Este backend utiliza o processo de **Desenvolvimento Orientado por Especificação (SDD)**.
Veja também o [`/CLAUDE.md`](../../CLAUDE.md) raiz do monorepo para o critério de
**Modo Leve vs Fluxo Completo** — nem toda mudança exige o ciclo completo abaixo.

## Fluxo de Desenvolvimento (Fluxo Completo)

1. **`/specify`** — Antes de começar a desenvolver uma nova feature, cria-se
   `backend/specs/{FEAT-XX-nome-feature}/spec.md` com o requisito de negócio
   (user stories + Given/When/Then), sem detalhes de implementação.
2. **`/plan`** — Lê `spec.md` + `backend/docs/constitution.md` + `backend/CLAUDE.md`
   e gera `backend/specs/{FEAT-XX-nome-feature}/plan.md` na mesma subpasta:
   camadas afetadas, contratos, decisões técnicas e recursos AWS envolvidos.
3. **`/tasks`** — Lê `plan.md` e gera `backend/specs/{FEAT-XX-nome-feature}/tasks.md`
   na mesma subpasta: checklist sequencial e granular (tamanho de commit).
4. **Implementar** — O código é desenvolvido seguindo estritamente `tasks.md`.
5. **`/review`** — Compara a implementação com os critérios de aceite de `spec.md`
   e aponta divergências.
6. **Testar** — Testes unitários e de componente (ver
   `backend/specs/FEAT-03-testes-componentes/spec.md`) validam as regras
   descritas na spec.

## Estrutura de uma feature

```
backend/specs/{FEAT-XX-nome-feature}/
├── spec.md   # o quê e por quê (user stories, Given/When/Then, critérios de aceite)
├── plan.md   # como (camadas, contratos, decisões técnicas, recursos AWS)
└── tasks.md  # checklist de implementação
```

**Nunca criar `.md` de feature solto direto em `backend/specs/`.** Cada feature
vive em sua própria subpasta, mesmo que hoje só tenha `spec.md` (ex.:
`FEAT-01-auth/`, `FEAT-02-mediator-result-pattern/`, `FEAT-03-testes-componentes/`
ainda não têm `plan.md`/`tasks.md` retroativos, mas seguem o mesmo padrão de pasta).

## Estrutura de uma Spec (`spec.md`)

Uma especificação típica deve conter:
- **Título e Contexto**: o que é a funcionalidade e o valor gerado.
- **Requisitos de Negócio**: regras de validação, limites, tratamentos de erro.
- **Contratos da API**: endpoints (caminho, verbo HTTP, request/response, status codes).
- **Plano de Testes**: quais cenários serão testados (sucesso, erros de validação,
  erros de infra), incluindo testes de componente conforme
  `backend/specs/FEAT-03-testes-componentes/spec.md`.
- **Critérios de aceite**.

Detalhes de implementação (camadas, PK/SK do DynamoDB, decisões técnicas) vivem em
`plan.md`, não em `spec.md`.
