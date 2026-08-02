# FEAT-12: Domínio customizado da API sob Terraform

## Objetivo

Eliminar o descasamento entre a infraestrutura real do domínio
customizado da API (`api.jrnexpenses.com`, criado manualmente via
console AWS depois da FEAT-10, hoje em produção) e o Terraform,
trazendo os recursos que faltam para dentro de
`backend/infra/terraform/` via `terraform import`, sem criar, recriar,
duplicar ou destruir nenhum recurso, e sem gerar custo adicional.

## Contexto

A FEAT-10 (`backend/specs/FEAT-10-deploy-lambda-aot-api-gateway/spec.md`)
publicou a API atrás da URL padrão do API Gateway
(`https://dhb1xc3bsi.execute-api.us-east-1.amazonaws.com/`) e listou
explicitamente "domínio customizado / certificado ACM / Route53" como
**fora do escopo** naquele momento. Depois disso, o domínio
`api.jrnexpenses.com` foi configurado manualmente e está em produção,
mas nunca ganhou uma spec própria nem entrou no Terraform do backend.

A FEAT-07 do frontend
(`frontend/specs/FEAT-07-terraform-import-infra/spec.md`) trouxe a
hosted zone `jrnexpenses.com.` e os records/recursos do frontend para o
Terraform, mas **excluiu explicitamente** (linhas 66-74 e 290-297
daquela spec) tudo que pertence a `api.jrnexpenses.com`, por ser escopo
do contexto backend. Levantamento feito na conta AWS
(`648443184523`, `us-east-1`), a partir dessa spec do frontend e de
`backend/infra/terraform/`:

- **ACM**: certificado `api.jrnexpenses.com`
  (`arn:aws:acm:us-east-1:648443184523:certificate/1b64dbcd-...`,
  status `ISSUED`), distinto do certificado `jrnexpenses.com` do
  frontend (esse sim já sob Terraform desde a FEAT-07 do frontend).
  Não há arquivo `.tf` correspondente em `backend/infra/terraform/`.
- **API Gateway — domínio customizado**: o HTTP API `api-gateway.tf`
  (recurso `aws_apigatewayv2_api.main`, da FEAT-10) hoje só expõe a URL
  padrão. O mapeamento de `api.jrnexpenses.com` para esse API
  (`aws_apigatewayv2_domain_name` + `aws_apigatewayv2_api_mapping`) foi
  criado manualmente e não existe no Terraform.
- **Route 53**: na hosted zone `jrnexpenses.com.`
  (`Z053098817OJTJ5LWHAZW`, gerenciada pelo Terraform do frontend desde
  a FEAT-07, config `frontend/infra/terraform/dns/`), existem hoje
  (criados manualmente, fora do Terraform de qualquer contexto):
  - `api.jrnexpenses.com` A (alias) → domínio regional do API Gateway
    gerado pelo `aws_apigatewayv2_domain_name` acima
  - CNAME de validação DNS do certificado ACM `api.jrnexpenses.com`
    (record `_f581ceffb919246b3f9f8e25a5c2b084.api.jrnexpenses.com`)
- **DynamoDB, Cognito, Parameter Store, Lambda e a URL padrão do API
  Gateway**: já 100% cobertos pelo Terraform do backend (FEAT-09 e
  FEAT-10), sem drift — não fazem parte do escopo desta feature.
- Precedentes diretos de estilo/abordagem: `backend/specs/FEAT-09-terraform-cognito-parameter-store/`
  (import de recurso manual → Terraform no backend) e
  `frontend/specs/FEAT-07-terraform-import-infra/spec.md` (mesmo padrão
  para o frontend, incluindo a forma como a hosted zone é referenciada
  entre configurações Terraform distintas).

### Sobre a hosted zone (recurso do frontend, referenciado aqui)

A hosted zone `jrnexpenses.com.` já é um recurso gerenciado
(`aws_route53_zone`, com `prevent_destroy = true`) na configuração
`frontend/infra/terraform/dns/`, não no backend — consistente com a
regra do `/CLAUDE.md` raiz de não misturar infraestrutura entre
contextos. O record `api.jrnexpenses.com` e seu CNAME de validação
vivem *dentro* dessa zona, mas pertencem semanticamente ao backend (é a
API do backend que o record aponta). Por isso, a forma de referenciar o
`zone_id` da zona a partir do Terraform do backend (leitura via
`terraform_remote_state` apontando para o state de
`frontend/infra/terraform/dns/`, ou outra alternativa equivalente) é
uma decisão técnica do `plan.md`, não desta spec — aqui só se registra
que os records de `api.jrnexpenses.com` passam a ser geridos pelo
Terraform do **backend**, mesmo estando fisicamente numa zona cujo
recurso raiz é gerenciado pelo frontend.

## Requisitos de negócio / restrições

- **Import apenas**: nenhum recurso é criado, recriado ou destruído
  nesta feature. O certificado ACM, o domínio customizado do API
  Gateway e os 2 records DNS (`A` + CNAME de validação) devem ser
  trazidos via `terraform import` a partir do estado real atual.
- **Custo zero adicional**: nenhuma configuração nova pode gerar
  cobrança. ACM (certificados públicos são gratuitos) e os records
  Route 53 (a zona já existe e já é cobrada pelo frontend; records
  adicionais não têm custo incremental) continuam sem custo.
- **Nenhuma ação na conta AWS sem autorização prévia explícita do
  usuário** — vale tanto para o desenho da estratégia (`plan.md`)
  quanto para qualquer execução futura (`terraform import`,
  `terraform plan`, `terraform apply`). Nenhum comando que possa
  alterar estado real roda de forma autônoma; cada `import` é
  confirmado individualmente no momento da execução.
- **Nenhuma mudança de comportamento observável da API** — o domínio
  `api.jrnexpenses.com` continua respondendo exatamente como hoje
  (mesmos endpoints, mesmo certificado, mesmo mapeamento); a URL padrão
  do API Gateway (`*.execute-api.us-east-1.amazonaws.com`) continua
  funcionando em paralelo, sem alteração.
- **Sem duplicar a hosted zone**: a zona `jrnexpenses.com.` não é
  importada nem recriada aqui — já está sob Terraform do frontend
  (FEAT-07). Esta feature só referencia esse recurso (leitura, não
  gerência) para poder gerenciar os 2 records de `api.jrnexpenses.com`
  dentro dela.
- **Records `NS`/`SOA` e demais records do frontend continuam fora do
  escopo** — inalterados, permanecem geridos (ou não) exatamente como a
  FEAT-07 do frontend definiu.

## User Stories

**US1 — Certificado ACM da API gerenciado pelo Terraform**
- Given o certificado `api.jrnexpenses.com` já existe e está `ISSUED`
- When a estratégia de import é aplicada
- Then o Terraform do backend (`backend/infra/terraform/`) passa a
  gerenciar esse certificado, sem alterar seu status ou validação

**US2 — Domínio customizado do API Gateway gerenciado pelo Terraform**
- Given o mapeamento de `api.jrnexpenses.com` para o HTTP API
  (`aws_apigatewayv2_api.main`) já existe, criado manualmente
- When a estratégia de import é aplicada
- Then o Terraform passa a gerenciar o `aws_apigatewayv2_domain_name` e
  o `aws_apigatewayv2_api_mapping` correspondentes, com a mesma
  configuração observável de hoje (mesmo domínio, mesmo certificado,
  mesmo API de destino)

**US3 — Records DNS de `api.jrnexpenses.com` gerenciados pelo Terraform**
- Given o record `A` (alias) de `api.jrnexpenses.com` e o CNAME de
  validação do certificado ACM já existem na hosted zone
  `jrnexpenses.com.`
- When a estratégia de import é aplicada
- Then o Terraform do backend passa a gerenciar esses 2 records,
  referenciando a hosted zone gerenciada pelo Terraform do frontend
  (sem duplicá-la ou recriá-la)

**US4 — Nenhuma diferença após a reconciliação**
- Given todos os recursos acima já foram trazidos para o Terraform
- When se roda `terraform plan` no backend (e, se aplicável, no
  frontend)
- Then o resultado é "No changes" em ambos — o código Terraform
  reflete exatamente o que existe na conta AWS

**US5 — Nenhuma execução sem aprovação explícita**
- Given qualquer comando que possa criar, alterar ou destruir um
  recurso AWS (`terraform import`, `terraform apply`)
- When esse comando está prestes a ser executado
- Then o usuário é consultado e precisa aprovar explicitamente antes da
  execução — nenhum comando desse tipo roda de forma autônoma

**US6 — API continua respondendo sem regressão**
- Given a API publicada hoje em `https://api.jrnexpenses.com`
- When a reconciliação é concluída
- Then requisições a `https://api.jrnexpenses.com/*` (incluindo
  `/expenses/*` com JWT válido) continuam respondendo exatamente como
  antes, e a URL padrão do API Gateway continua funcionando em
  paralelo

## Critérios de aceite

- [x] `terraform state list` de `backend/infra/terraform/` passa a
      incluir o certificado ACM `api.jrnexpenses.com`, o
      `aws_apigatewayv2_domain_name` e o `aws_apigatewayv2_api_mapping`
- [x] Os 2 records DNS de `api.jrnexpenses.com` (A alias + CNAME de
      validação ACM) passam a ser geridos por Terraform, referenciando
      a hosted zone do frontend sem duplicá-la
- [x] `terraform plan` após a reconciliação retorna "No changes" contra
      a conta AWS real
- [x] `https://api.jrnexpenses.com` continua respondendo exatamente
      como antes (mesmo comportamento de autenticação — 401 sem
      token), validado manualmente, assim como a URL padrão do API
      Gateway em paralelo, sem regressão
- [x] Nenhum comando de `import`/`apply` executado sem aprovação
      explícita do usuário no momento da execução
- [x] `backend/infra/CLAUDE.md` e `backend/infra/terraform/README.md`
      atualizados para refletir que o domínio customizado da API
      (certificado, mapeamento e records) agora é gerido por Terraform

## Status

Implementado conforme `plan.md`/`tasks.md`. Todos os 5 recursos já
existentes na conta AWS (`648443184523`, `us-east-1`) foram trazidos
via `terraform import` para `backend/infra/terraform/`, sem criar,
recriar ou alterar nenhum deles:

- `aws_acm_certificate.api` —
  `arn:aws:acm:us-east-1:648443184523:certificate/1b64dbcd-776f-4008-8a3a-2683ceb34fab`
- `aws_route53_record.api_acm_validation["api.jrnexpenses.com"]` — CNAME
  de validação (`_f581ceffb919246b3f9f8e25a5c2b084.api.jrnexpenses.com`)
- `aws_apigatewayv2_domain_name.api` — `api.jrnexpenses.com`
  (`endpoint_type = REGIONAL`, `security_policy = TLS_1_2`, confirmados
  contra o recurso real antes do import)
- `aws_apigatewayv2_api_mapping.api` — mapping id `oqn3qo`, stage
  `$default` do HTTP API `dhb1xc3bsi`
- `aws_route53_record.api_a` — record A (alias), na hosted zone
  `jrnexpenses.com.` (`Z053098817OJTJ5LWHAZW`), lida via
  `data "aws_route53_zone"` (sem depender do state do frontend)

`terraform plan` final: **"No changes. Your infrastructure matches the
configuration."** Validação manual: `https://api.jrnexpenses.com/expenses`
e `https://dhb1xc3bsi.execute-api.us-east-1.amazonaws.com/expenses`
(URL padrão) respondem `401` sem token, igual antes da feature — sem
regressão em nenhum dos dois domínios. Fluxo completo autenticado
validado através do domínio customizado com um usuário de teste
temporário (`e2e-feat12-review@jrnexpenses.com`, confirmado via
`admin-confirm-sign-up` e excluído via `admin-delete-user` ao final,
sem dados de despesa criados): `POST /auth/register` (201) →
`POST /auth/login` (200) → `GET /auth/me` (200) → `GET /expenses`
(200, `{"items":[],"nextCursor":null}`).

`backend/infra/CLAUDE.md` e `backend/infra/terraform/README.md`
atualizados com a nova seção do domínio customizado.

## Fora do escopo

- Qualquer mudança na hosted zone `jrnexpenses.com.` em si (criação,
  exclusão, `prevent_destroy`) — recurso já gerido pelo Terraform do
  frontend (FEAT-07), fora do alcance desta spec
- Novo certificado, novo domínio ou novo subdomínio (ex.: ambiente de
  homologação `api-hom.jrnexpenses.com`) — feature futura separada,
  mencionada apenas como preparo de baixo custo na FEAT-07 do frontend
- Records `NS`/`SOA` da zona e os demais records do frontend
  (`jrnexpenses.com`, `www.jrnexpenses.com` e seus CNAMEs de validação)
  — inalterados, continuam sob responsabilidade do contexto frontend
- Pipeline de CI/CD para aplicar Terraform automaticamente — execução
  continua manual, a partir da máquina do usuário, com aprovação
  passo a passo
- Qualquer mudança de comportamento observável da API além de expor o
  domínio customizado (nenhuma mudança de contrato, autenticação ou
  CORS é escopo desta feature)
