# Roadmap — Backend para o Design System (Modernist)

Sequência combinada em 2026-08-22 para o backend alcançar tudo que o
design system (`frontend/design-system/screenshots/`) já assume, com
todas as rotas prontas pro frontend consumir. Cada linha vira uma
`spec.md` própria em `backend/specs/{FEAT-XX-nome}/` via `/specify`,
seguindo o fluxo normal (`/specify` → `/plan` → `/tasks` → implementar →
`/review`). Ver `backend/docs/README.md` para o processo.

**Como usar este arquivo:** ao terminar uma FEAT (implementada, testada
100% e revisada), marcar o checkbox correspondente antes de seguir pra
próxima. Não pular a ordem — cada uma depende da anterior conforme a
coluna "Depende de".

## Decisões de modelagem já fechadas (contexto para o `/plan` de cada FEAT)

- **DynamoDB single-table, sem migração de dados** — tabela pode ser
  recriada do zero, sem compatibilidade retroativa com o que existe hoje
- **Novo tenant: `Conta`** substitui `USER#<userId>` como partição
  principal de `Category`/`Expense`/futura `Transaction`. Um usuário
  pode pertencer a múltiplas contas (`GSI1 PK=USER#<userId>` já modelado
  pra isso desde já, mesmo que o front hoje só use uma)
- **Sem tabela agregada / sem DynamoDB Streams** — `Resumo` e
  `Relatórios` são sempre `Query` do período + agregação em memória na
  própria request. Ponto de reavaliação: só se algum dia uma conta
  acumular milhares de transações/ano (não é o caso hoje)
- **Comprovante de despesa/receita fica só de UI por enquanto** — sem
  bucket S3, sem `ReceiptS3Key` no modelo, adiado pra feature própria
  futura
- **Orçamento por categoria é um valor mensal recorrente** (atributo
  `OrcamentoMensalCents` na própria `Category`), não versionado por mês
- Modelo completo (item types, PK/SK/GSI) detalhado na conversa do dia
  2026-08-22; será formalizado em `backend/docs/data-model.md` conforme
  cada FEAT abaixo for implementada (mesmo padrão já usado pra FEAT-16)

## Sequência

- [ ] **FEAT-19 — Conta (fundação multi-tenant)**
  Cria `Account` + `Membership` (titular) via trigger `Post
  Confirmation` do Cognito (novo Lambda), assim que o usuário confirma o
  cadastro — com resolução idempotente também no primeiro login como
  rede de segurança (falha do trigger, usuário criado fora do fluxo
  padrão, limitação do `cognito-local`). Migra `Category` e `Expense` de
  `PK=USER#<userId>` para `PK=ACCOUNT#<accountId>`, resolvendo o
  `accountId` a partir do `userId` do JWT em todo request. Contrato das
  rotas existentes não muda — é troca de chave interna, transparente pro
  usuário único de hoje.
  Depende de: FEAT-01 (auth), FEAT-16/17 (categorias/despesas) — já prontas.

- [ ] **FEAT-20 — Membros da conta, convites e permissões**
  `GET/POST/DELETE /members`, convite por e-mail (`Status=ConvitePendente`
  → aceite no login), níveis de acesso `Leitura`/`Lançar`/`Total`.
  Aplica autorização por role em todos os endpoints já existentes
  (despesas, categorias).
  Depende de: FEAT-19.

- [ ] **FEAT-21 — Categoria: tipo e orçamento**
  Adiciona `tipo` (`despesa`|`receita`, obrigatório) e
  `orcamentoMensalCents` (opcional) a `Category`. `GET /categories`
  ganha filtro `?tipo=`. Editar orçamento exige role `Total`.
  Depende de: FEAT-19 (e idealmente FEAT-20, pra já nascer com a role certa).

- [ ] **FEAT-22 — Transações: generalizar Despesa para Receita/Despesa**
  Generaliza `Expense` → `Transação`: renomeia `/expenses` → `/transactions`,
  adiciona `tipo` (`despesa`|`receita`), expõe `createdByUserId`/
  `createdByLabel` (pra "Lançado por: Você"). Reaproveita a mecânica de
  chave já existente (`TXN#`, GSI1 por categoria, GSI2 por id).
  **Decisão em aberto para o `/plan` desta FEAT:** rota única
  `/transactions?tipo=` vs. `/expenses` + `/incomes` espelhados —
  confirmar antes de detalhar o contrato.
  Depende de: FEAT-19, FEAT-21.

- [ ] **FEAT-23 — Resumo mensal (dashboard)**
  `GET /summary?month=YYYY-MM`: saldo, receitas, gastos, orçamento
  total, restante, gasto por categoria, últimos lançamentos — calculado
  via `Query` + agregação em memória, sem tabela agregada.
  Depende de: FEAT-22, FEAT-21.

- [ ] **FEAT-24 — Relatórios por período**
  `GET /reports?period=week|month|year`: gasto por categoria, total do
  período, variação vs período anterior, maior gasto. Mesma estratégia
  de cálculo do FEAT-23.
  Depende de: FEAT-22, FEAT-21.

- [ ] **FEAT-25 — Exportação CSV de transações** *(menor, pode ficar por
  último ou fora desta leva)*
  `GET /transactions/export` gerando CSV a partir da mesma `Query` de
  transações do período — cobre o botão "Exportar CSV" de Ajustes.
  Depende de: FEAT-22.

- [ ] **FEAT-26 — E-mail de boas-vindas**
  Envia e-mail de boas-vindas quando a conta é criada (mesmo trigger
  `Post Confirmation` da FEAT-19). Exige decidir/provisionar
  infraestrutura de e-mail (SES ou similar) — inexistente no projeto
  hoje. Escopo deixado de fora da FEAT-19 de propósito.
  Depende de: FEAT-19.

- [ ] **FEAT-27 — Seed de categorias padrão**
  Cria automaticamente um conjunto de categorias padrão (nome, cor,
  ícone) para toda conta nova. Já tinha sido adiado na FEAT-16;
  retomado aqui porque a criação de conta (FEAT-19) é o gatilho natural.
  Depende de: FEAT-19, FEAT-16.

## Fora desta leva, de propósito

- Comprovante/anexo real (upload S3)
- Agregação materializada / DynamoDB Streams
- Ajustes → notificações push/e-mail reais e seletor de moeda (hoje sem
  infra nenhuma por trás — ficam só de UI no front até haver decisão
  explícita de criar essa infra)