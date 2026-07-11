---
description: Gera o tasks.md (checklist de implementação) de uma feature a partir do seu plan.md
---

Feature: $ARGUMENTS

Contexto padrão: **backend** (`/backend`), salvo indicação explícita de `/frontend` ou `/infra`.

1. Resolva a pasta da feature: `{contexto}/specs/{FEAT-XX-nome-feature}/`. Se `plan.md` não existir nessa pasta, avise que é preciso rodar `/plan` antes e pare.
2. Leia `{contexto}/specs/{feature}/plan.md` (e `spec.md` para os critérios de aceite).
3. Gere `tasks.md` **na mesma subpasta** da feature, como um checklist sequencial e granular, do tipo:
   ```
   - [ ] 1. <task pequena e objetiva, do tamanho de um commit>
   - [ ] 2. ...
   ```
   Cada item deve ser pequeno o suficiente para ser um commit isolado (ex.: "criar Command X", "implementar Handler Y", "adicionar teste unitário de Z", "adicionar teste de componente do endpoint W"), na ordem em que devem ser feitos (dependências antes de dependentes).
4. Inclua sempre, ao final da lista, tasks explícitas para os testes exigidos (unitários e, para todo endpoint novo, teste de componente — ver `backend/specs/FEAT-03-testes-componentes/spec.md`) e para atualizar o `spec.md` da feature marcando os critérios de aceite concluídos.
5. Não implemente código neste comando — apenas produza o checklist.
