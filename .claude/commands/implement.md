---
description: Executa o tasks.md de uma feature, com checkpoints de comunicação e paradas obrigatórias conforme o Safe Feature Criterion
---

Feature: $ARGUMENTS

Contexto padrão: **backend** (`/backend`), salvo indicação explícita de `/frontend` ou `/infra`.

## Passo 0 — Pré-condições

1. Resolva a pasta da feature: `{contexto}/specs/{FEAT-XX-nome-feature}/`. Se `tasks.md` não existir nessa pasta, avise que é preciso rodar `/tasks` antes e pare.
2. Leia `tasks.md`, `plan.md` e `spec.md` da feature, além de `{contexto}/docs/constitution.md` e `{contexto}/CLAUDE.md`.
3. **Sempre pergunte ao usuário qual o Level (1, 2 ou 3) da feature antes de iniciar a implementação**, referenciando `criterio-feature-segura-autopilot.md` como critério — mesmo que a feature pareça óbvia, mesmo que já tenha sido discutida antes. Nunca assuma ou infira o Level sozinho, e nunca reaproveite o Level de uma execução anterior sem perguntar de novo.
4. Com o Level confirmado, anuncie ao usuário: quantas tasks existem em `tasks.md` e qual vai ser a cadência de parada correspondente (ver Passo 3) antes de começar a implementar.

## Passo 1 — Gatilhos de parada obrigatória

Independente do Level, **pare e pergunte ao usuário** antes de continuar sempre que, durante a execução de uma task:

- For necessário tocar em Terraform/IAM, modelagem DynamoDB (PK/SK/GSI novo), Cognito/auth, cálculo monetário, pipeline de CI/CD, ou qualquer recurso AWS novo — mesmo que a task pareça isolada — e isso não estava explícito em `plan.md`.
- A task exigir uma decisão de produto ou técnica não coberta em `plan.md`/`spec.md`.
- Um teste falhar de um jeito não esperado pelo plano (não é falha "task ainda não implementada", é falha de comportamento).
- O escopo real da task parecer maior do que o estimado em `tasks.md` (ex.: uma task de 1 commit virando várias mudanças em cascata).
- For a última task de um Level 3, ou qualquer task de Level 3 (ver Passo 3).

Nesses casos, **não prossiga silenciosamente nem decida sozinho** — descreva o que encontrou, o que `plan.md` previa, e aguarde confirmação.

## Passo 2 — Formato de report por task

Ao concluir cada task de `tasks.md`, reporte no chat usando este formato (curto, escaneável):

```
✅ Task N concluída — <descrição curta>
Arquivos: <lista>
Testes: passou / falhou (<detalhe se falhou>)
Desvios do plano: nenhum / <descrição>
Próxima: Task N+1 — <descrição curta>
```

Não narre linha a linha o que está sendo codado — o report acontece só na conclusão (ou interrupção) de cada task.

## Passo 3 — Cadência por Level

- **Level 1**: execute as tasks em sequência sem pausar entre elas, mas ainda emitindo o report da task a cada conclusão. Pare no final para um resumo consolidado.
- **Level 2**: pare para confirmação do usuário a cada 3–5 tasks (ou no fim de cada "bloco lógico" identificável em `tasks.md`, o que vier primeiro).
- **Level 3**: pare para confirmação do usuário após **cada task individual**, antes de seguir para a próxima.

Em qualquer Level, os gatilhos do Passo 1 têm prioridade sobre a cadência — eles podem forçar uma parada mesmo em Level 1.

## Passo 4 — Critério de "done" por task

Antes de marcar uma task como concluída em `tasks.md`, confirme:

1. Código compila (`dotnet build` no contexto backend, `npm run build`/`vite build` no frontend, conforme aplicável).
2. Testes relevantes à task passam (não a suíte inteira a cada task — isso fica para o final).
3. Lint/formatação sem erros novos introduzidos pela task.

Se qualquer um desses falhar, a task **não** é marcada como concluída — reporte a falha usando o formato do Passo 2 (`Testes: falhou`) e pare para orientação do usuário, a menos que a correção seja trivial e óbvia dentro do escopo da própria task.

## Passo 5 — Divergência plano vs. realidade

Se a implementação real de uma task divergir do que `plan.md` previa (mesmo que a divergência pareça pequena):

1. Não implemente silenciosamente diferente do plano.
2. Registre a divergência no report da task (`Desvios do plano: <descrição>`).
3. Pergunte ao usuário se a divergência deve ser aceita como está, ou se `plan.md`/`tasks.md` precisam ser atualizados para refletir a nova decisão.
4. Só depois de confirmado, atualize `plan.md` (se a mudança for relevante o suficiente para valer como decisão técnica documentada) e siga para a próxima task.

## Passo 6 — Fechamento

Ao concluir todas as tasks de `tasks.md`:

1. Rode a suíte de testes completa do contexto (não só as tasks-específicas).
2. Marque em `tasks.md` todos os itens concluídos.
3. Marque em `spec.md` os critérios de aceite atendidos (isso normalmente já é uma task explícita gerada pelo `/tasks`).
4. Apresente um resumo final: quantas tasks foram concluídas, quantos desvios do plano ocorreram (se algum), estado da suíte de testes, e sugira rodar `/review` em seguida para validar contra os critérios de aceite.
5. Não faça deploy nem abra PR automaticamente — isso segue as regras já definidas em `CLAUDE.md` (merge sempre manual).
