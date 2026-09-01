# Backlog — Backend

Sequência combinada em 2026-08-22 para o backend alcançar tudo que o
design system (`frontend/design-system/screenshots/`) já assume, com
todas as rotas prontas pro frontend consumir. Cada linha da seção
**Features** vira uma `spec.md` própria em
`backend/specs/{FEAT-XX-nome}/` via `/specify`, seguindo o fluxo normal
(`/specify` → `/plan` → `/tasks` → implementar → `/review`). Ver
`backend/docs/README.md` para o processo.

**Como usar este arquivo:** o backlog é dividido em 4 seções —
**Features**, **Bugs**, **Débitos técnicos e melhorias futuras** e
**Compliance (LGPD)**. Todo item leva um checkbox: `- [x]` quando já
implementado/testado/revisado, `- [ ]` enquanto pendente. Ao terminar um
item, marcar o checkbox antes de seguir pro próximo. Na seção
**Features**, não pular a ordem — cada uma depende da anterior conforme
a coluna "Depende de". Ao priorizar um item de Bug/Débito/Compliance,
ele sai da lista de pendências e vira trabalho normal (spec nova via
`/specify` ou Modo Leve, conforme o caso — ver critério no `/CLAUDE.md`
raiz).

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

## Features

- [x] **FEAT-19 — Conta (fundação multi-tenant)**
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

- [x] **FEAT-20 — Membros da conta, convites e permissões**
  `GET/POST/DELETE /members`, convite por e-mail (`Status=ConvitePendente`
  → aceite no login), níveis de acesso `Leitura`/`Lançar`/`Total`.
  Aplica autorização por role em todos os endpoints já existentes
  (despesas, categorias).
  Depende de: FEAT-19.

- [x] **FEAT-21 — Categoria: tipo e orçamento**
  Adiciona `tipo` (`despesa`|`receita`, obrigatório) e
  `orcamentoMensalCents` (opcional) a `Category`. `GET /categories`
  ganha filtro `?tipo=`. Editar orçamento exige role `Total`.
  Depende de: FEAT-19 (e idealmente FEAT-20, pra já nascer com a role certa).

- [x] **FEAT-22 — Transações: generalizar Despesa para Receita/Despesa**
  Generaliza `Expense` → `Transação`: renomeia `/expenses` → `/transactions`
  (rota única, filtro `?tipo=`), adiciona `tipo` (`despesa`|`receita`,
  validado contra a categoria referenciada), expõe `createdByUserId`/
  `createdByLabel` (pra "Lançado por: Você"). Papel `Lancar` passa a
  poder editar/excluir só o que criou. Reaproveita a mecânica de chave
  já existente (`TXN#`, GSI1 por categoria, GSI2 por id).
  Depende de: FEAT-19, FEAT-21.

- [x] **FEAT-23 — Resumo mensal (dashboard)**
  `GET /summary?month=YYYY-MM`: saldo, receitas, gastos, orçamento
  total, restante, gasto por categoria, últimos lançamentos — calculado
  via `Query` + agregação em memória, sem tabela agregada.
  Depende de: FEAT-22, FEAT-21.

- [x] **FEAT-24 — Relatórios por período**
  `GET /reports?period=week|month|year`: gasto por categoria, total do
  período, variação vs período anterior, maior gasto. Mesma estratégia
  de cálculo do FEAT-23.
  Depende de: FEAT-22, FEAT-21.

- [x] **FEAT-25 — Exportação CSV de transações** *(menor, pode ficar por
  último ou fora desta leva)*
  `GET /transactions/export` gerando CSV a partir da mesma `Query` de
  transações do período — cobre o botão "Exportar CSV" de Ajustes.
  Depende de: FEAT-22.

- [x] **FEAT-26 — Perfil do usuário no cadastro (nome, telefone, CPF)**
  *(inserida fora da ordem original desta lista, a pedido do usuário,
  empurrando as duas linhas seguintes uma posição adiante)*
  `POST /auth/register` passa a exigir também `name`, `phoneNumber` e
  `cpf`, armazenados num novo item de perfil no DynamoDB (não em
  atributos do Cognito — CPF não é atributo padrão e um atributo
  customizado só pode ser definido na criação do User Pool). CPF único
  e validado por dígito verificador. `GET /auth/me` passa a expor os
  três campos.
  Depende de: FEAT-01 (auth).

- [ ] **FEAT-27 — E-mail de boas-vindas**
  Envia e-mail de boas-vindas quando a conta é criada (mesmo trigger
  `Post Confirmation` da FEAT-19). Exige decidir/provisionar
  infraestrutura de e-mail (SES ou similar) — inexistente no projeto
  hoje. Escopo deixado de fora da FEAT-19 de propósito.
  Depende de: FEAT-19.

- [x] **FEAT-28 — Seed de categorias padrão**
  Cria automaticamente um conjunto de categorias padrão para toda conta
  nova. Já tinha sido adiado na FEAT-16; retomado aqui porque a criação
  de conta (FEAT-19) é o gatilho natural. Implementada fora da ordem
  original desta lista, a pedido do usuário, antes da FEAT-27.
  Depende de: FEAT-19, FEAT-16.

- [x] **FEAT-30 — Categoria: escopar busca por ID (GSI2) por conta**
  *(inserida fora da ordem original desta lista — nasceu de um bug
  encontrado em 2026-08-31 e registrado no backlog, corrigido pelo
  schema em vez de correção rápida)*
  Corrige `GSI2PK` de `Category`, que usava só `ID#<categoryId>` e
  colidia entre contas nas 13 categorias padrão (mesmos ids literais
  hardcoded pela FEAT-28 em toda conta nova). Passa a
  `ID#<accountId>#<categoryId>`, alinhado ao mesmo padrão de escopo por
  conta já usado no resto do modelo. Ver
  `backend/specs/FEAT-30-categoria-gsi2-escopo-conta/`.
  Depende de: FEAT-19, FEAT-28.

## Bugs

- [x] **BUG — Login não exige perfil completo quando o usuário é criado
  diretamente no Cognito** (levantado em 2026-08-31, fora do escopo de
  qualquer FEAT em andamento) *(resolvido, ver
  `backend/specs/FEAT-31-login-perfil-incompleto/`)*: o fluxo normal
  (`POST /auth/register`, FEAT-26) exige `name`, `phoneNumber` e `cpf`
  antes de criar o perfil no DynamoDB. Mas se um administrador cadastra
  o usuário proativamente no Cognito (fora do `/auth/register`) e já
  confirma o acesso, `LoginUserCommandHandler` autenticava via
  `IAuthService.LoginAsync` sem checar se existe perfil com os campos
  obrigatórios preenchidos — o usuário logava normalmente mesmo sem
  nome/CPF/telefone cadastrados. FEAT-31 bloqueou o login (403
  `profile-incomplete`) nesse caso.

## Débitos técnicos e melhorias futuras

Itens levantados durante specify/plan/tasks/implementação/review ou
Modo Leve, fora do escopo do que estava sendo feito no momento — ver
"Débitos técnicos e oportunidades de melhoria" no `/CLAUDE.md` raiz do
monorepo.

- [x] **DÉBITO — Módulos sem teste integrado ainda** (levantado na
  FEAT-29 — `backend/specs/FEAT-29-testes-integrados/`) *(resolvido,
  ver `backend/specs/FEAT-32-testes-integrados-modulos-pendentes/`)*:
  a infraestrutura de testes integrados (suíte multiambiente, execução
  local via Docker/Native AOT/Runtime Interface Emulator, gates de
  CI/CD em hom/prod) foi entregue cobrindo só o módulo Auth como prova
  de conceito — os demais módulos existentes continuavam sem teste
  integrado, cobertos só por teste de componente (mocks). FEAT-32
  preencheu os 7 módulos, seguindo o padrão já estabelecido
  (`TestAccountFixture` + `<Modulo>/<Modulo>FlowTests.cs`): Categorias
  (`FEAT-16`/`FEAT-21`), Transações (`FEAT-22`), Membros/convites
  (`FEAT-20`), Resumo mensal (`FEAT-23`), Relatórios por período
  (`FEAT-24`), Exportação CSV (`FEAT-25`), Perfil do usuário
  (`FEAT-26`).

- [ ] **DÉBITO — `DELETE /members` remove o membro em vez de
  inativá-lo** (confirmado com o usuário durante a FEAT-22): deveria
  bloquear a remoção de um membro que já lançou transações,
  transformando-o em `Inativo` (novo `Status` de `Membership`) em vez
  de removê-lo de fato — um membro `Inativo` continuaria aparecendo
  como `createdByLabel` nas transações que já criou. Hoje
  (FEAT-20/FEAT-22) `DELETE /members` remove o `Membership`
  incondicionalmente; transações de um membro removido caem no
  fallback `createdByLabel="Ex-membro"` (ver
  `backend/specs/FEAT-22-transacoes-receita-despesa/`).

- [ ] **DÉBITO — `backend-feature-pr.yml` não dispara para mudanças só
  em `backend/infra/terraform/**`** (percebido num fix pontual pós
  FEAT-32, PR #86): o filtro `paths` do workflow cobre
  `backend/src/**`, `backend/tests/**`, `backend/infra/lambda/**`,
  `backend/GastosApp.sln` e `.github/workflows/backend-*.yml`, mas não
  `backend/infra/terraform/**` — uma branch `fix/*`/`FEAT-*` que só
  altera Terraform (ex.: ajuste de IAM policy da role de CI/CD) nunca
  abre PR automático pra `develop`, exigindo `gh pr create` manual.
  Decidir se `backend/infra/terraform/**` deve entrar nesse filtro
  (ou se mudanças de Terraform devem mesmo ficar fora do PR
  automático, por não passarem pelo gate de build/teste de código) —
  ver `backend/infra/CLAUDE.md`.

## Compliance (LGPD)

Levantado durante o `/specify` da FEAT-26 (coleta de CPF no cadastro).
Sem timeline — só entram na seção **Features** quando o usuário decidir
priorizar, ex.: se o projeto deixar de ser uso pessoal.

- [ ] **LGPD — Direito de exclusão/anonimização** (`Art. 16` LGPD): hoje
  não existe fluxo de encerramento de conta; ao existir, precisa apagar
  ou anonimizar nome/telefone/CPF, não reter indefinidamente.

- [ ] **LGPD — Direito de retificação** (`Art. 18` LGPD): edição do
  próprio perfil (nome/telefone/CPF) pelo usuário — hoje fora do escopo
  da FEAT-26 de propósito.

- [ ] **LGPD — Base legal e consentimento explícito** (`Art. 7º`/`Art.
  9º` LGPD): tela de cadastro sem Termos de Uso/Política de Privacidade
  hoje — necessário formalizar a finalidade da coleta de CPF antes de
  qualquer uso além de identificação da conta.

- [ ] **LGPD — Transferência internacional de dados** (`Art. 33` LGPD):
  infra de produção roda em `us-east-1`
  (`backend/infra/terraform/environments/prod/variables.tf`) —
  reavaliar migração para `sa-east-1` (São Paulo) se o volume de dados
  pessoais justificar simplificar essa exigência.

- [ ] **LGPD — Encarregado (DPO) e plano de resposta a incidente**: não
  obrigatório no porte atual, mas necessário antes de qualquer escala
  maior, dado que CPF é alvo preferencial de fraude.

## Fora desta leva, de propósito

Itens deliberadamente fora de escopo — não são pendências, não entram
como checkbox. Só migram pra uma das seções acima se houver decisão
explícita de priorizar.

- Comprovante/anexo real (upload S3)
- Agregação materializada / DynamoDB Streams
- Ajustes → notificações push/e-mail reais e seletor de moeda (hoje sem
  infra nenhuma por trás — ficam só de UI no front até haver decisão
  explícita de criar essa infra)
