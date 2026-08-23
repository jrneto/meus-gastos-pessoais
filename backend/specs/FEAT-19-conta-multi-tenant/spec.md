# FEAT-19: Conta (fundação multi-tenant)

## Objetivo

Introduzir `Account` (conta) como o tenant real de `Category` e
`Expense`, no lugar do `userId` isolado usado até hoje. Todo usuário
passa a ter automaticamente uma conta própria, criada de forma
assíncrona no momento em que confirma o cadastro no Cognito, sem que o
contrato observável de nenhuma rota hoje existente mude.

## Contexto

Prepara terreno pra FEAT-20 (membros/convites/permissões) e as demais
features do roadmap do design system (`backend/docs/roadmap.md`), que
assumem que uma conta pode ter mais de um usuário vinculado no futuro.
Hoje `Category`/`Expense` são particionadas por `USER#<userId>` (ver
`backend/docs/data-model.md`); esta feature troca essa partição por
`ACCOUNT#<accountId>`, resolvido a partir do `userId` do JWT em todo
request — transparente pro usuário único de hoje.

Depende de FEAT-01 (auth), FEAT-16 (categorias) e FEAT-17 (despesas com
categoria dinâmica) — já implementadas.

Decisão de modelagem já fechada pelo roadmap: a tabela DynamoDB pode ser
recriada do zero, sem migração de dado existente (ver
`backend/docs/roadmap.md`).

**Mecanismo de criação (decidido nesta spec):** `Account` + `Membership`
são criadas de forma assíncrona por um trigger `Post Confirmation` do
Cognito — um novo componente Lambda, novo recurso AWS aprovado
explicitamente para esta feature — disparado assim que o usuário
confirma o cadastro, antes de qualquer login. Como rede de segurança —
cobrindo falha transitória do trigger, usuários criados fora do fluxo
padrão de confirmação (ex.: console do Cognito, seed scripts) ou
eventual limitação do emulador local (`cognito-local`, ver
`backend/infra/CLAUDE.md`) em suportar o trigger — a mesma resolução
(buscar ou criar, idempotente) também roda no primeiro login
bem-sucedido. Login e requests a `Category`/`Expense` nunca falham por
causa desse mecanismo: se a conta ainda não existir por qualquer razão,
ela é criada ali mesmo, na hora.

Esta feature **não expõe nenhum endpoint novo** (gerenciamento de
membros é escopo da FEAT-20) e **não muda o contrato de nenhuma rota
existente** (`/auth/*`, `/categories`, `/expenses`) — troca só a chave
interna de particionamento.

E-mail de boas-vindas e seed de categorias padrão — cogitados como
outras ações a rodar nesse mesmo momento — foram conscientemente
deixados fora desta feature (ver "Fora do escopo") e viram itens novos
no roadmap (`backend/docs/roadmap.md`), dependentes desta.

## Requisitos de negócio

- Toda `Account` nasce com exatamente um `Membership` inicial, vinculado
  ao `userId` que confirmou o cadastro, com papel `Titular` (papel
  fixo, distinto dos níveis de acesso `Leitura`/`Lançar`/`Total` que a
  FEAT-20 vai introduzir pra membros convidados)
- A criação é idempotente: nem o trigger de confirmação nem a resolução
  no login duplicam `Account`/`Membership` para o mesmo `userId` —
  inclusive sob concorrência (ex.: confirmação e um login quase
  simultâneos, ou múltiplas tentativas de login em paralelo)
- Falha transitória do trigger de confirmação (ex.: DynamoDB
  indisponível) nunca impede a confirmação do cadastro no Cognito nem
  bloqueia o usuário — na pior hipótese, a conta é criada no primeiro
  login bem-sucedido
- `userId` continua sempre extraído do JWT (claim `sub`), nunca do
  body — o que muda é que toda operação sobre `Category`/`Expense`
  passa a resolver o `accountId` correspondente a esse `userId` antes
  de tocar o banco
- Isolamento entre contas é equivalente ao isolamento por usuário que
  já existia: nenhuma operação sobre `Category`/`Expense` pode nunca
  ler, alterar ou expor dado de uma `Account` diferente da resolvida
  para o `userId` autenticado
- Um usuário sem `Account` resolvível ao chamar uma rota autenticada de
  `Category`/`Expense` (situação que só ocorreria por dado
  corrompido/manual, já que login sempre resolve ou cria) é tratado
  como erro do usuário (401), nunca como 500

## User Stories

**US1 — Confirmação de cadastro cria a conta automaticamente**
- Given um usuário que acabou de se cadastrar (`POST /auth/register`) e
  ainda não confirmou o cadastro no Cognito
- When ele confirma o cadastro (fluxo de confirmação do próprio
  Cognito)
- Then uma `Account` nova é criada com um `Membership` vinculando esse
  `userId` como `Titular`, antes mesmo do primeiro login

**US2 — Login reaproveita a conta já existente**
- Given um usuário cuja `Account`/`Membership` já foram criadas (via
  confirmação ou login anterior)
- When ele faz `POST /auth/login` com sucesso
- Then nenhuma `Account`/`Membership` nova é criada — a mesma conta de
  antes é resolvida, e a resposta de login continua no mesmo formato de
  hoje

**US3 — Login cria a conta quando o trigger de confirmação não rodou**
- Given um usuário confirmado no Cognito mas sem `Account`/`Membership`
  (ex.: trigger falhou, ou usuário criado fora do fluxo padrão de
  confirmação)
- When ele faz `POST /auth/login` com sucesso pela primeira vez
- Then uma `Account`/`Membership` são criadas nesse momento, e o login
  retorna 200 normalmente

**US4 — Login com credenciais inválidas não cria conta**
- Given uma tentativa de login com credenciais inválidas
- When a autenticação falha
- Then nenhuma `Account`/`Membership` é criada, e a API retorna 401 no
  mesmo formato de hoje

**US5 — Categorias e despesas passam a viver na conta**
- Given um usuário autenticado com `Account` já resolvida
- When ele cria, consulta, edita ou exclui uma categoria ou despesa
- Then o dado é gravado/lido vinculado à `Account` do usuário — mas
  toda resposta observável da API (`GET`/`POST`/`PUT`/`DELETE
  /categories`, `/expenses`) permanece exatamente no formato de hoje

**US6 — Isolamento entre contas**
- Given dois usuários diferentes, cada um com sua própria `Account`
  criada automaticamente
- When qualquer um consulta, cria, edita ou exclui categorias ou
  despesas
- Then a operação nunca afeta nem expõe dado da `Account` do outro
  usuário

**US7 — Concorrência na criação da conta**
- Given um usuário sem `Account` ainda, com o trigger de confirmação e
  uma tentativa de login dele disparando quase ao mesmo tempo (ou
  múltiplas tentativas de login em paralelo)
- When mais de um desses caminhos tenta criar a `Account`/`Membership`
  concorrentemente
- Then só uma `Account`/`Membership` existe ao final, sem duplicidade
  nem erro visível pro usuário

## Contratos da API

Esta feature não introduz nem altera nenhum endpoint. Todos os
contratos abaixo continuam idênticos aos já documentados em
`backend/docs/openapi.json` e nas specs originais:

- `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`,
  `POST /auth/logout`, `GET /auth/me` — sem mudança
  (`backend/specs/FEAT-01-auth/`, `backend/specs/FEAT-15-refresh-token/`)
- `GET`/`POST`/`PUT`/`DELETE /categories` — sem mudança
  (`backend/specs/FEAT-16-crud-categorias/`)
- `GET`/`POST`/`PUT`/`DELETE /expenses` — sem mudança
  (`backend/specs/FEAT-04-registro-despesa/`, `FEAT-06`, `FEAT-07`,
  `FEAT-08`, `FEAT-17`)

Nenhum campo novo (ex.: `accountId`) é exposto em request/response
nesta feature — expor `accountId` ao cliente fica para a FEAT-20, se
necessário para gerenciar membros.

## Critérios de aceite

- [ ] Confirmar o cadastro no Cognito cria automaticamente `Account` +
      `Membership` (`Titular`) para o `userId` confirmado — lógica
      coberta por `ComponentTest` (`AccountTriggerHandlerTests`,
      handler invocado diretamente); o *wiring* real Cognito → Lambda
      ainda depende de `terraform apply` em hom/prod, não aplicado
      nesta sessão (ver "Status")
- [x] Login bem-sucedido de um usuário sem `Account` ainda (trigger não
      rodou) cria `Account` + `Membership` na hora, sem alterar o
      formato da resposta de login
- [x] Login bem-sucedido de um usuário com `Account` já existente não
      cria duplicata
- [x] Login com credenciais inválidas não cria `Account`/`Membership`
- [x] Falha transitória do trigger de confirmação não impede a
      confirmação do cadastro nem bloqueia o usuário
- [x] Criação concorrente (confirmação + login, ou múltiplos logins em
      paralelo) nunca resulta em mais de uma `Account`/`Membership`
      para o mesmo `userId`
- [x] `GET`/`POST`/`PUT`/`DELETE /categories` e `/expenses` continuam
      funcionando exatamente como antes (mesmo request/response/status
      codes), agora particionados por `accountId` internamente
- [x] Nenhuma operação sobre categoria/despesa expõe ou altera dado de
      outra `Account`
- [x] Nenhum contrato de API existente muda —
      `backend/docs/openapi.json` regenerado sem diffs de contrato

## Status

Implementado conforme `plan.md`/`tasks.md`. `Account`/`Membership`
(Domain), `IAccountRepository`/`EnsureAccountCommand`/
`ResolveAccountIdQuery`/`AccountErrors` (Application),
`DynamoDbAccountRepository` (Infrastructure),
`ResolveAccountEndpointFilter`/`CurrentAccountContext` (Api) e o novo
projeto `GastosApp.CognitoTriggers` (handler `AccountTriggerHandler` +
`Function.cs`) implementados e adicionados à `GastosApp.sln`.
`Category`/`Expense` migrados de `PK=USER#<userId>` para
`PK=ACCOUNT#<accountId>` em todo o Domain/Application/Infrastructure.
`LoginUserCommandHandler` despacha `EnsureAccountCommand` como
fallback, capturando qualquer falha sem propagar (login nunca quebra
por causa disso).

Terraform novo (`lambda-account-trigger.tf` em `environments/{hom,prod}`,
`lambda_config.post_confirmation` em `cognito.tf`, IAM da role de
CI/CD ampliada) e os dois workflows de deploy
(`backend-deploy-account-trigger-{hom,prod}.yml`) foram escritos, mas
**`terraform apply` não foi executado nesta sessão** — segue a mesma
regra de aprovação explícita já aplicada às demais mudanças de infra
do projeto. Antes do primeiro deploy real, faltam dois passos manuais
fora do escopo do código: `terraform apply` em hom (depois prod) e
criar a variável `ACCOUNT_TRIGGER_FUNCTION_NAME` nos GitHub
Environments `backend-hom`/`backend-prod` (mesmo padrão de
`FUNCTION_NAME` já existente).

`backend/docs/openapi.json` regenerado localmente (API rodando contra
`local-init.sh`/LocalStack/cognito-local) — `git diff` confirma zero
diferença de contrato, exatamente como a spec previa.

Suíte completa (`dotnet test` na solução) passa: 316/316 (1
IntegrationTests placeholder + 94 ComponentTests + 221 UnitTests).

## Fora do escopo

- Qualquer endpoint de gerenciamento de membros/convites
  (`GET/POST/DELETE /members`) — escopo da FEAT-20
- Papéis de acesso `Leitura`/`Lançar`/`Total` e autorização por role —
  escopo da FEAT-20; o único papel criado aqui é `Titular`
- E-mail de boas-vindas — depende de infraestrutura de e-mail (SES ou
  similar) ainda inexistente no projeto; vira item novo no roadmap,
  dependente desta feature
- Seed de categorias padrão para conta nova — já tinha sido adiado na
  FEAT-16 por ter regras próprias (quais categorias, cor/ícone padrão);
  vira item novo no roadmap, dependente desta feature
- Migração de dado existente na tabela `GastosApp` — decisão já fechada
  no roadmap de que a tabela pode ser recriada do zero
- Expor `accountId` em qualquer resposta ao cliente
- Múltiplos usuários por conta (convite de outros membros) — só a
  estrutura de dados (`Membership` já modelado para permitir N membros
  por conta) nasce pronta pra isso, mas nenhum fluxo de convite existe
  ainda
