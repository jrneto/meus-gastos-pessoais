# FEAT-32: Testes integrados dos módulos pendentes

## Objetivo

Preencher a lacuna registrada em `backend/docs/backlog.md` ("DÉBITO —
Módulos sem teste integrado ainda", levantada na FEAT-29): adicionar
teste integrado (`GastosApp.IntegrationTests`, contra a API real —
Cognito/DynamoDB reais em hom/prod, binário Native AOT publicado via
Runtime Interface Emulator em local — nunca dublês) para os 7 módulos
que hoje só têm teste de componente (mocks), seguindo o mesmo padrão já
estabelecido pela FEAT-29 (`TestAccountFixture` +
`<Modulo>/<Modulo>FlowTests.cs`):

- Categorias (`FEAT-16-crud-categorias`, `FEAT-21-categoria-tipo-orcamento`)
- Transações (`FEAT-22-transacoes-receita-despesa`)
- Membros/convites (`FEAT-20-membros-convites-permissoes`)
- Resumo mensal (`FEAT-23-resumo-mensal-dashboard`)
- Relatórios por período (`FEAT-24-relatorios-por-periodo`)
- Exportação CSV (`FEAT-25-exportacao-csv-transacoes`)
- Perfil do usuário (`FEAT-26-perfil-usuario-cadastro`)

## Contexto

A FEAT-29 entregou toda a infraestrutura de teste integrado (suíte
multiambiente, execução local via Docker/Native AOT/RIE, gates de
CI/CD em hom/prod) cobrindo só o módulo **Auth** como prova de
conceito (`register`/`login`, ver
`backend/tests/GastosApp.IntegrationTests/Auth/AuthFlowTests.cs`) —
os demais módulos já existentes ficaram como débito técnico, a
preencher incrementalmente. Esta feature é esse preenchimento: **não
cria nenhuma infraestrutura nova** (nenhum transporte, fixture,
Dockerfile, workflow ou permissão IAM novos) — reaproveita 100% do que
a FEAT-29 já deixou pronto em `backend/tests/GastosApp.IntegrationTests/Support/`
(`IApiTransport`, `DirectHttpTransport`, `LambdaRieTransport`,
`TestAccountFixture`, `IntegrationTestEnvironment`, `AwsClientFactory`,
`CpfGenerator`, `Contracts.cs`).

**Nenhum contrato de API muda** — os 7 módulos já estão implementados e
publicados; esta feature só adiciona testes que exercitam o contrato já
existente (fonte de verdade continua `backend/docs/openapi.json` e a
`spec.md` de cada FEAT original listada acima). Como os jobs de CI/CD
já criados pela FEAT-29 (`integration-tests` em
`backend-deploy-hom.yml`, `backend-integration-tests-prod.yml`) rodam
`dotnet test tests/GastosApp.IntegrationTests` no projeto inteiro, os
testes novos desta feature passam a rodar automaticamente nesses gates
existentes, sem qualquer mudança de workflow.

**Decisão de escopo fechada com o usuário durante o `/specify`:** as 7
lacunas do backlog são cobertas por uma única spec/FEAT (não uma FEAT
por módulo), já que compartilham o mesmo objetivo, o mesmo padrão
técnico e a mesma infraestrutura — evita repetir o mesmo `/specify` →
`/plan` → `/tasks` → `/review` sete vezes para um trabalho que é, na
prática, uma única tarefa de preenchimento incremental.

### O que "cobertura mínima" significa aqui

`backend/docs/constitution.md` exige, como definição de pronto desde a
FEAT-29, teste integrado cobrindo **pelo menos o fluxo de sucesso** de
todo endpoint. Esta feature usa esse mínimo como piso, mas cada módulo
listado abaixo também inclui **isolamento entre contas** (dado real do
DynamoDB, não mock) sempre que o módulo já tiver o conceito de conta
ativa, e **pelo menos um caso de autorização por papel** (JWT real
validado contra o Cognito/cognito-local real) quando o módulo tiver
regra de papel — são justamente os dois tipos de garantia que teste de
componente (com repositório/Cognito dublês) não valida de verdade, e é
o motivo de existir uma suíte integrada separada da suíte de
componente. Não é objetivo desta feature reproduzir, em modo
integrado, **todos** os critérios de aceite já cobertos por teste de
componente em cada `spec.md` original (ex.: toda variação de erro 400
de validação de campo) — isso continua responsabilidade da suíte de
componente.

## Requisitos de negócio / restrições

- Cada módulo ganha seu próprio arquivo
  `backend/tests/GastosApp.IntegrationTests/<Modulo>/<Modulo>FlowTests.cs`,
  marcado com `[Trait("Category", "Integration")]` (mesmo padrão de
  `AuthFlowTests`), continuando fora do `dotnet test GastosApp.sln`
  padrão (filtro `Category!=Integration` já configurado pela FEAT-29)
- Toda conta de teste usada pelos novos testes é criada/limpa via
  `TestAccountFixture` (ou extensão dela), nunca deixando rastro
  permanente em hom/prod — mesma garantia já dada pela FEAT-29
- O módulo **Membros/convites** exige exercitar o fluxo real de
  convite + aceite no login (`POST /members` → login de um **segundo**
  usuário de teste com o e-mail convidado → `Membership` vira `Ativo`)
  para validar de ponta a ponta contra Cognito/DynamoDB reais — isso
  requer criar/logar uma **segunda conta de teste** dentro do mesmo
  teste, além da conta principal já criada pelo `TestAccountFixture`.
  Se `TestAccountFixture` não suportar hoje criar uma segunda conta
  vinculada, ele precisa ser estendido para isso (decisão técnica,
  detalhada no `plan.md`) — sem alterar o comportamento dos testes já
  existentes de Auth
- Nenhum teste desta feature depende de dado deixado por outro teste
  (cada `[Fact]` monta o próprio cenário a partir de uma conta nova,
  mesmo padrão de isolamento já usado por `AuthFlowTests`)
- Nenhuma mudança em `backend/docs/openapi.json`, em qualquer camada de
  produção (`Api`/`Application`/`Domain`/`Infrastructure`), em
  workflow de CI/CD ou em permissão IAM — escopo é só o projeto
  `GastosApp.IntegrationTests`
- Segue a mesma convenção de nomenclatura de teste já usada em
  `AuthFlowTests` (`MétodoOuFluxo_Cenário_ResultadoEsperado`)

## Cobertura por módulo

### Categorias (FEAT-16 + FEAT-21)
Endpoints: `GET/POST/PUT/DELETE /categories`.
- Sucesso: criar categoria (`tipo` + `orcamentoMensalCents` opcional),
  listar (com e sem filtro `?tipo=`), editar, excluir sem transações
  associadas
- Regra de negócio real: excluir categoria com transação associada
  retorna 422 (exige criar uma transação real vinculada antes, não só
  um mock de "já existe")
- Autorização: papel `Leitura` recebe 403 em `POST`/`PUT`/`DELETE`
- Isolamento: categoria de uma conta não aparece nem é acessível pela
  conta de outro usuário de teste

### Transações (FEAT-22)
Endpoints: `POST/GET/GET{id}/PUT/DELETE /transactions`.
- Sucesso: registrar despesa e receita (cada uma contra categoria do
  tipo correspondente), consultar lista e por id, editar, excluir
- Regra de negócio real: `tipo` da transação divergente do `tipo` da
  categoria retorna 400
- Autorização: papel `Lancar` edita/exclui só a própria transação
  (`createdByUserId`) e recebe 403 na de outro membro — exige uma
  segunda conta de teste (mesmo mecanismo do módulo Membros)
- Isolamento: transação de uma conta não é acessível pela conta de
  outro usuário de teste

### Membros/convites (FEAT-20)
Endpoints: `GET/POST/PUT/DELETE /members`.
- Sucesso: Titular convida (`POST`, `Status=ConvitePendente`), lista
  membros, troca papel (`PUT`), remove (`DELETE`)
- Fluxo ponta a ponta real: convite aceito automaticamente no login de
  um segundo usuário de teste com o e-mail convidado
  (`Status` vira `Ativo`, conta ativa do convidado passa a ser a conta
  do convite) — validação só possível em ambiente integrado, contra
  Cognito/DynamoDB reais
- Autorização: papel não-Titular recebe 403 em `POST`/`PUT`/`DELETE`

### Resumo mensal (FEAT-23)
Endpoint: `GET /summary?month=YYYY-MM`.
- Sucesso: mês com transações e categorias com orçamento retorna
  `saldoCents`/`receitasCents`/`gastoCents`/`orcamentoTotalCents`/
  `restanteCents`/`porCategoria`/`ultimosLancamentos` calculados a
  partir de dados reais
- Mês sem nenhuma transação retorna 200 com valores zerados (não 404)
- Autorização: papel `Leitura` recebe 200 (acesso liberado a todos)
- Isolamento: resumo de uma conta não reflete dados de outra conta

### Relatórios por período (FEAT-24)
Endpoint: `GET /reports?period=week|month|year&date=YYYY-MM-DD`.
- Sucesso: ao menos um `period` (ex.: `month`) com despesas reais
  retorna `totalCents`/`porCategoria`/`maiorGasto`/`variacaoPercentual`
  calculados corretamente
- Autorização: papel `Leitura` recebe 200
- Isolamento: relatório de uma conta não reflete dados de outra conta

### Exportação CSV (FEAT-25)
Endpoint: `GET /transactions/export`.
- Sucesso: exportação sem filtro retorna 200 com
  `Content-Type: text/csv`, cabeçalho
  `data;descricao;categoria;tipo;valor;lancadoPor` e uma linha por
  transação real da conta, com `valor` em reais/vírgula decimal (não
  centavos)
- Sem resultado (filtro sem transação correspondente) retorna 200 com
  CSV só de cabeçalho
- Autorização: papel `Leitura` recebe 200

### Perfil do usuário (FEAT-26)
Sem endpoint próprio — campos expostos por `POST /auth/register` (já
testado pela FEAT-29) e `GET /auth/me`. Hoje `AuthFlowTests` cria a
conta com `name`/`phoneNumber`/`cpf` no setup mas não afirma que esses
valores voltam corretos em `GET /auth/me`, nem cobre unicidade de CPF.
- Sucesso: `GET /auth/me`, após registro real, retorna `name`,
  `phoneNumber` e `cpf` idênticos aos enviados no `POST /auth/register`
- Regra de negócio real: registrar um segundo usuário com o mesmo CPF
  de uma conta de teste já existente retorna 409
  (`cpf-already-exists`) — exige uma segunda tentativa de registro
  real contra o Cognito

## Contratos da API

Nenhum. Esta feature não adiciona, remove nem altera nenhum endpoint
ou comportamento observável da API — os contratos exercitados já estão
publicados em `backend/docs/openapi.json` e documentados em cada
`spec.md` original (FEAT-16, FEAT-20, FEAT-21, FEAT-22, FEAT-23,
FEAT-24, FEAT-25, FEAT-26), que continuam a fonte de verdade de
request/response/status code — esta spec só define **o que passa a
ser exercitado contra a API real**.

## Critérios de aceite

- [x] `Categories/CategoriesFlowTests.cs`: fluxo de sucesso
      (criar/listar/editar/excluir), bloqueio de exclusão com
      transação associada (422), 403 para papel `Leitura` em
      escrita, isolamento entre contas — todos passando localmente
      via `run-local.sh`
- [x] `Transactions/TransactionsFlowTests.cs`: fluxo de sucesso
      (registrar despesa e receita/listar/consultar por id/
      editar/excluir), 400 para `tipo` divergente da categoria, 403
      para papel `Lancar` em transação de outro membro, isolamento
      entre contas — todos passando localmente
- [x] `Members/MembersFlowTests.cs`: fluxo de sucesso
      (convidar/listar/trocar papel/remover), convite aceito de
      verdade no login de uma segunda conta de teste
      (`Status=Ativo`, troca de conta ativa), 403 para papel
      não-Titular — todos passando localmente
- [x] `Summary/SummaryFlowTests.cs`: fluxo de sucesso com dados reais,
      mês sem dados retorna 200 zerado, 200 para papel `Leitura`,
      isolamento entre contas — todos passando localmente
- [x] `Reports/ReportsFlowTests.cs`: fluxo de sucesso com dados reais
      para ao menos um `period`, 200 para papel `Leitura`, isolamento
      entre contas — todos passando localmente
- [x] `Transactions/ExportFlowTests.cs` (ou equivalente): exportação
      com dados retorna CSV com linhas corretas, filtro sem resultado
      retorna CSV só de cabeçalho, 200 para papel `Leitura` — todos
      passando localmente
- [x] `Auth/AuthFlowTests.cs` ganha asserção de que `GET /auth/me`
      retorna `name`/`phoneNumber`/`cpf` idênticos aos enviados no
      registro, e um novo teste de CPF duplicado (409) — passando
      localmente
- [x] `TestAccountFixture` (ou uma extensão dela) suporta criar uma
      segunda conta de teste vinculada, usada pelos módulos Membros e
      Transações (autorização por papel `Lancar`)
- [x] Todos os testes novos rodam com sucesso localmente via
      `backend/infra/lambda/run-local.sh` (binário Native AOT via RIE)
- [x] `dotnet test GastosApp.sln` continua passando (unitário +
      componente), sem exigir Docker/rede, sem regressão de contagem
- [x] Nenhum arquivo fora de `backend/tests/GastosApp.IntegrationTests/`
      foi alterado, exceto `backend/docs/backlog.md` (item de débito
      marcado como concluído/apontando para esta FEAT) — ver "Status"
      abaixo sobre o fix em `LambdaRieTransport.cs` (dentro do próprio
      `GastosApp.IntegrationTests/`, portanto ainda dentro deste
      critério)
- [x] `backend/docs/backlog.md`: item "DÉBITO — Módulos sem teste
      integrado ainda" sai da seção "Débitos técnicos e melhorias
      futuras" e passa a apontar para esta FEAT

## Status

Implementado conforme `plan.md`/`tasks.md`. Sete arquivos de
`FlowTests` novos (`Categories/`, `Transactions/` — incluindo
`ExportFlowTests.cs`, `Members/`, `Summary/`, `Reports/`) mais a
extensão de `Auth/AuthFlowTests.cs` (módulo Perfil), todos exercitando
a API real via `IApiTransport` (Cognito/DynamoDB reais em hom/prod, o
binário Native AOT publicado via Runtime Interface Emulator em local),
sem referenciar código de produção. `TestAccountFixture` ganhou
`InviteAndAcceptAsync` (convida → registra/confirma/loga uma segunda
identidade real, disparando o aceite automático do convite no login) e
o novo tipo `SecondaryTestAccount` (limpeza própria via `Query
IndexName=GSI1, GSI1PK=USER#<userId>`, isolando e apagando só a conta
pessoal da segunda identidade — a `Membership` dela na conta que
convidou já é limpa pela conta principal).

Suíte local validada de ponta a ponta via `run-local.sh`: 26/26 testes
passando (os 3 já existentes de Auth + os 23 novos — inclui
`GetCategories_ComFiltroTipo_RetornaSomenteDoTipoFiltrado`, adicionado
no `/review` pra fechar uma divergência entre a prosa de "Cobertura por
módulo" acima, que já prometia testar `?tipo=`, e o que tinha sido
implementado). `dotnet test GastosApp.sln --filter
"Category!=Integration"` (unitário + componente) segue passando sem
regressão: 473 unitários + 207 de componente.

**Três achados reais durante a implementação**, todos em infraestrutura
de teste (nenhum em código de produção), documentados em detalhe no
`plan.md`:

1. **Paralelismo do xUnit derrubava o container RIE local** — primeira
   vez que a suíte tem mais de uma classe de teste; classes diferentes
   rodam em paralelo por padrão, disparando requisições concorrentes
   contra um emulador que só suporta uma invocação por vez. Corrigido
   com `[assembly: CollectionBehavior(DisableTestParallelization =
   true)]` (novo `Support/AssemblyInfo.cs`).
2. **`LambdaRieTransport` nunca separava query string do path** — bug
   pré-existente da FEAT-29, nunca exercitado porque o único módulo
   anterior (Auth) nunca usava `GET` com query string. Toda chamada
   com `?...` virava 404 em modo local. Corrigido separando `path` em
   `rawPath`/`rawQueryString` antes de montar o evento do API Gateway
   v2 (`Support/LambdaRieTransport.cs`) — sem efeito em hom/prod
   (`DirectHttpTransport` já funcionava).
3. **Nomes de categoria usados nos testes colidiam com o catálogo de
   13 categorias padrão** (FEAT-28, `DefaultCategorySeed` — inclui
   "Transporte", "Alimentação" etc., semeadas automaticamente em toda
   conta nova) — `POST /categories` com nome coincidente retorna 422
   (`name-conflict`, comportamento correto da API). Corrigido trocando
   os nomes usados em `CategoriesFlowTests.cs`/`TransactionsFlowTests.cs`
   por strings que não colidem com o seed, com comentário explicativo
   nos dois arquivos.

Os dois primeiros achados foram confirmados com o usuário antes de
alterar arquivos fora do escopo original do `plan.md`
(`Support/AssemblyInfo.cs` é novo; `LambdaRieTransport.cs` já existia
desde a FEAT-29) — ambos dentro de
`backend/tests/GastosApp.IntegrationTests/`, então não violam o
critério de aceite "nenhum arquivo fora desta pasta foi alterado".

## Fora do escopo

- Qualquer endpoint novo, campo novo ou mudança de comportamento
  observável da API — esta feature só adiciona testes
- Cobrir em modo integrado **todos** os critérios de aceite já
  cobertos por teste de componente em cada `spec.md` original (ex.:
  toda combinação de filtro de `GET /transactions`, toda mensagem de
  erro 400 de validação de campo) — só o piso descrito em "Cobertura
  por módulo" acima
- `POST /auth/login` bloqueado por perfil incompleto (`403
  profile-incomplete`, FEAT-31) — módulo Auth já tem teste integrado
  desde a FEAT-29; esse cenário específico não está na lista de débito
  desta feature (fica como oportunidade futura, se o usuário priorizar)
- Qualquer mudança nos workflows de CI/CD, no script `run-local.sh`,
  no Dockerfile local, ou em permissão IAM da role
  `gastosapp-backend-cicd` — tudo isso já existe desde a FEAT-29 e
  roda automaticamente sobre os testes novos, sem alteração
- Débito técnico "`DELETE /members` remove em vez de inativar" (ver
  `backend/docs/backlog.md`) — fora de escopo, item próprio no backlog
- Testes de carga/performance
