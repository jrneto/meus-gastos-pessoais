# Spec-Driven Development (SDD) — Frontend GastosApp

Este frontend utiliza o mesmo processo de **Desenvolvimento Orientado por
Especificação (SDD)** do backend. Veja também o [`/CLAUDE.md`](../../CLAUDE.md)
raiz do monorepo para o critério de **Modo Leve vs Fluxo Completo** — nem
toda mudança exige o ciclo completo abaixo.

## Fluxo de Desenvolvimento (Fluxo Completo)

1. **`/specify frontend: ...`** — cria `frontend/specs/{FEAT-XX-nome-feature}/spec.md`
   com o requisito de negócio (user stories + Given/When/Then), sem
   detalhes de implementação. A branch nasce aqui, a partir de `develop`
   (ver "Fluxo de Git" no `/CLAUDE.md` raiz).
2. **`/plan`** — lê `spec.md` + `frontend/docs/constitution.md` +
   `frontend/CLAUDE.md` e gera `plan.md` na mesma subpasta: componentes/
   rotas/features afetados, contratos com a API do backend, decisões
   técnicas.
3. **`/tasks`** — lê `plan.md` e gera `tasks.md`: checklist sequencial e
   granular (tamanho de commit).
4. **Implementar** — o código é desenvolvido seguindo estritamente
   `tasks.md`.
5. **`/review`** — compara a implementação com os critérios de aceite de
   `spec.md` e aponta divergências.
6. **Testar** — Vitest + React Testing Library + MSW; nenhuma feature é
   considerada concluída com testes falhando (ver `docs/constitution.md`).

A numeração `FEAT-XX` do frontend é independente da do backend (ver
`/CLAUDE.md` raiz).

## Estrutura de uma feature

```
frontend/specs/{FEAT-XX-nome-feature}/
├── spec.md   # o quê e por quê (user stories, Given/When/Then, critérios de aceite)
├── plan.md   # como (componentes, rotas, contratos com a API, decisões técnicas)
└── tasks.md  # checklist de implementação
```

**Nunca criar `.md` de feature solto direto em `frontend/specs/`.**

## Fonte do contrato da API

O contrato de wire (endpoints, request/response, status codes) vem de
`backend/docs/openapi.json`, gerado pelo contexto backend — o frontend
nunca o edita. Regras de negócio/validação que o schema não expressa
continuam em `backend/specs/{FEAT-XX}/spec.md`. Detalhes em
`frontend/docs/constitution.md` ("Fonte do contrato da API").
