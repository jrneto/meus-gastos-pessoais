# FEAT-11: CORS para o frontend de produção

## Objetivo

Permitir que o frontend de produção do GastosApp, hospedado em
`https://jrnexpenses.com` e `https://www.jrnexpenses.com`, faça
requisições cross-origin para a API, sem quebrar o ambiente de
desenvolvimento local já configurado (`http://localhost:5173`).

## Contexto

A API já tem uma política de CORS configurável (`Cors:AllowedOrigins`,
lida em `Program.cs`), hoje alimentada só por `appsettings.json` /
`appsettings.Development.json` — não existe ainda `appsettings.Production.json`
nem leitura desse valor a partir do Parameter Store, apesar do padrão
já estabelecido no projeto de manter configuração de ambiente fora do
código-fonte (Cognito, ver FEAT-09). Além da política de CORS da
aplicação, o API Gateway (camada na frente da Lambda, ver FEAT-10)
também tem sua própria configuração de CORS, hoje um placeholder
(`http://localhost:4200`, uma única origem). As duas camadas precisam
permitir os domínios de produção para que uma requisição do browser
chegue até o handler da aplicação.

Esta feature cobre o desenho de como as origens de produção passam a
ser permitidas nas duas camadas, de forma configurável (sem exigir
novo build/deploy de código para adicionar ou remover uma origem no
futuro) — consistente com o padrão de configuração externa via AWS
Systems Manager Parameter Store já usado para outras configurações do
backend.

## Requisitos de negócio

- A API aceita requisições cross-origin de `https://jrnexpenses.com` e
  de `https://www.jrnexpenses.com`
- O ambiente de desenvolvimento local (`http://localhost:5173`)
  continua funcionando sem regressão
- Requisições vindas de origens fora da lista permitida não recebem os
  cabeçalhos CORS necessários para serem aceitas pelo navegador
- A lista de origens permitidas em produção é configurável
  externamente (sem exigir novo build/deploy de código para
  adicionar/remover uma origem no futuro)
- CORS é uma mudança de comportamento de middleware/infraestrutura, não
  de contrato de wire — nenhum endpoint, request, response ou status
  code muda; `backend/docs/openapi.json` não precisa ser regenerado por
  esta feature
- Qualquer provisionamento real de recurso AWS (novo parâmetro no
  Parameter Store, aplicação da configuração de CORS do API Gateway via
  `terraform apply`) exige aprovação explícita do usuário antes de ser
  executado — esta spec cobre o desenho; a aplicação em si é uma etapa
  separada, a critério do usuário (mesmo padrão já registrado nas specs
  de infraestrutura anteriores, FEAT-07/FEAT-09)

## User Stories

**US1 — Requisição do frontend de produção (domínio raiz)**
- Given o frontend de produção hospedado em `https://jrnexpenses.com`
- When ele faz uma requisição (preflight `OPTIONS` seguido da chamada
  real) a um endpoint existente da API (ex.: `POST /expenses`)
- Then a API responde ao preflight com
  `Access-Control-Allow-Origin: https://jrnexpenses.com` e a requisição
  real é aceita normalmente

**US2 — Requisição do frontend de produção (subdomínio www)**
- Given o frontend de produção hospedado em `https://www.jrnexpenses.com`
- When ele faz uma requisição a um endpoint existente da API
- Then a API responde ao preflight com
  `Access-Control-Allow-Origin: https://www.jrnexpenses.com` e a
  requisição real é aceita normalmente

**US3 — Ambiente de desenvolvimento local sem regressão**
- Given o frontend rodando localmente em `http://localhost:5173`
- When ele faz uma requisição à API
- Then a API continua respondendo normalmente, sem alteração de
  comportamento em relação ao que já funciona hoje

**US4 — Origem não autorizada é bloqueada**
- Given um site que não está na lista de origens permitidas
- When ele tenta fazer uma requisição cross-origin à API
- Then a resposta não inclui `Access-Control-Allow-Origin` para aquela
  origem, e o navegador bloqueia a chamada no lado do cliente

**US5 — Atualizar origens permitidas sem novo deploy de código**
- Given a lista de origens de produção configurada externamente
- When for necessário adicionar ou remover uma origem de produção no
  futuro
- Then isso é feito atualizando a configuração externa, sem exigir
  alteração de código nem novo build da aplicação

## Contratos da API observáveis

Não há novo endpoint nem mudança de request/response — o efeito é
exclusivamente nos cabeçalhos CORS de qualquer endpoint já existente.

### Preflight (`OPTIONS`) para um endpoint existente, origem permitida
Request:
```
OPTIONS /expenses
Origin: https://jrnexpenses.com
Access-Control-Request-Method: POST
```

Response 204/200:
```
Access-Control-Allow-Origin: https://jrnexpenses.com
Access-Control-Allow-Methods: ...
Access-Control-Allow-Headers: ...
```

### Requisição real, origem permitida
A resposta de qualquer endpoint já existente (ex.: `GET /expenses`)
passa a incluir `Access-Control-Allow-Origin` correspondente à origem
da requisição, quando essa origem está na lista permitida — sem
nenhuma outra mudança no corpo, status code ou demais cabeçalhos já
documentados em `backend/docs/openapi.json`.

### Requisição de origem não permitida
Nenhum cabeçalho `Access-Control-Allow-Origin` correspondente é
retornado — o navegador do cliente bloqueia o acesso à resposta.

## Critérios de aceite

- [x] Preflight e requisição real de `https://jrnexpenses.com` para um
      endpoint existente são aceitos, com os cabeçalhos CORS corretos
- [x] Preflight e requisição real de `https://www.jrnexpenses.com` para
      um endpoint existente são aceitos, com os cabeçalhos CORS
      corretos
- [x] Ambiente de desenvolvimento local (`http://localhost:5173`)
      continua funcionando sem regressão
- [x] Requisição de uma origem fora da lista permitida não recebe
      `Access-Control-Allow-Origin` correspondente
- [x] Lista de origens de produção é configurável externamente, sem
      exigir novo build/deploy de código para adicionar/remover uma
      origem
- [x] `backend/docs/openapi.json` permanece inalterado (nenhuma
      mudança de contrato de wire introduzida por esta feature)
- [x] Desenho de infraestrutura (parâmetro no Parameter Store,
      configuração de CORS do API Gateway) documentado em `plan.md`;
      qualquer `terraform apply` ou criação real do parâmetro só
      acontece após aprovação explícita do usuário

## Status

Implementado e provisionado em produção. `Program.cs` ajustado para
somar `Cors:AllowedOrigins` (dev local) + `Cors:ProductionOrigins`
(só Parameter Store) antes de configurar a policy `"Frontend"`.
`CorsTests` (componente, 4 casos: origem de cada lista, origem fora
das duas, preflight) implementado conforme `plan.md`. Terraform:
`frontend_origins` (list(string), 2 domínios), `api-gateway.tf`
atualizado, 2 novos `aws_ssm_parameter` em `parameter-store.tf`.

Suíte completa (`dotnet test` na solução) passa: 180/180 (1
IntegrationTests + 120 UnitTests + 59 ComponentTests, incluindo os 4
novos `CorsTests`).

**Infraestrutura aplicada e validada em produção**, com aprovação
explícita do usuário em cada etapa (`terraform plan` → aprovação →
`apply`, duas vezes: parâmetros SSM + CORS do API Gateway; depois
rebuild + redeploy da Lambda):
- 2 parâmetros criados: `/GastosApp/Cors/ProductionOrigins/0`
  (`https://jrnexpenses.com`) e `/1` (`https://www.jrnexpenses.com`)
- `cors_configuration` do API Gateway atualizado in-place (sem
  recriação)
- Lambda reconstruída (Native AOT, `infra/lambda/build.sh`) e
  redeployada com o `Program.cs` ajustado

Validação manual (preflight e requisição real via `curl`, contra a URL
real do API Gateway em produção e contra `dotnet run` local):
- `https://jrnexpenses.com` e `https://www.jrnexpenses.com`: preflight
  e requisição real aceitos, `Access-Control-Allow-Origin` correto nos
  dois ambientes
- `http://localhost:5173` (dev local): continua funcionando sem
  regressão
- Origem fora das listas: nenhum `Access-Control-Allow-Origin`
  retornado, em nenhum dos dois ambientes
- Efeito colateral aceito e confirmado: dev local também permite os
  dois domínios de produção (Parameter Store compartilhado entre
  ambientes, mesmo padrão do Cognito) — documentado em `plan.md`

## Fora do escopo

- Provisionamento real (`terraform apply`) do parâmetro no Parameter
  Store e da configuração de CORS do API Gateway — depende de
  aprovação explícita do usuário, tratado como etapa separada
- Qualquer domínio além de `https://jrnexpenses.com` e
  `https://www.jrnexpenses.com`
- CORS com credenciais/cookies — autenticação continua via Bearer JWT
- Rate limiting, WAF ou outras proteções de borda
