---
description: Cria a especificação (spec.md) de uma feature, ou orienta o Modo Leve quando o fluxo completo não é necessário
---

Descrição da demanda: $ARGUMENTS

Contexto padrão: **backend** (`/backend`), salvo se a descrição deixar claro que é sobre `/frontend` ou `/infra`.

## Passo 1 — Classificar Modo Leve vs Fluxo Completo

Antes de qualquer coisa, leia `/CLAUDE.md` (raiz) — seção "Modo Leve vs Fluxo Completo" — e aplique o critério à demanda descrita.

- Se a demanda se enquadrar claramente em **Modo Leve** (bugfix pontual sem mudança de contrato, ajuste de config/log/validação simples, refatoração interna sem mudança de comportamento externo, CRUD que já segue padrão 100% estabelecido, correção de teste/nomenclatura/formatação): **não crie spec**. Avise o usuário explicitamente que a demanda se enquadra em Modo Leve, explique por quê em 1-2 frases, e sugira pular direto para a implementação. Pare aqui e aguarde confirmação do usuário antes de implementar.
- Se houver qualquer dúvida sobre a classificação, **pergunte ao usuário** antes de decidir — nunca decida sozinho pular o fluxo completo para algo que pareça tocar arquitetura, contrato de API ou infraestrutura.
- Se for claramente **Fluxo Completo** (novo domínio/entidade, toca 2+ camadas, novo recurso AWS, breaking change de contrato, >~1 dia de trabalho), prossiga para o Passo 2.

## Passo 2 — Criar a spec (apenas em Fluxo Completo)

1. Leia `{contexto}/docs/constitution.md` e `{contexto}/CLAUDE.md` para regras e convenções vigentes.
2. Liste `{contexto}/specs/` para descobrir o próximo número `FEAT-XX` disponível (sequencial, dois dígitos) e escolha um slug curto e descritivo em kebab-case para o nome da feature.
3. Crie a subpasta `{contexto}/specs/FEAT-XX-nome-feature/` — **nunca crie um `.md` de feature solto direto em `specs/`**.
4. Dentro dela, crie `spec.md` contendo:
   - Título e contexto (o que é a funcionalidade e o valor gerado)
   - Requisitos de negócio (regras de validação, limites, tratamentos de erro)
   - User stories com critérios em formato **Given/When/Then**
   - Contratos da API observáveis externamente (endpoints, request/response, status codes) — **sem detalhes de implementação** (nada de nomes de classes, camadas, PK/SK do DynamoDB; isso é escopo de `plan.md`)
   - Critérios de aceite (checklist `- [ ]`)
   - Seção "Fora do escopo" quando fizer sentido
5. Siga o estilo/formatação já usado em specs existentes do mesmo contexto (ex.: `backend/specs/FEAT-01-auth/spec.md`) para manter consistência.
6. Ao final, apresente um resumo da spec criada e peça revisão/aprovação do usuário antes de qualquer implementação.
