# FEAT-07: Import da infra do frontend para Terraform

## Objetivo

Eliminar o descasamento entre a infraestrutura real do frontend (S3 +
CloudFront + ACM + WAF + hosted zone/records DNS no Route 53, criada
manualmente via console AWS, já em produção em `jrnexpenses.com`) e o
Terraform, trazendo esses recursos para dentro do código via `terraform
import`, sem criar, recriar, duplicar ou destruir nenhum recurso, e sem
gerar custo adicional — já organizados de um jeito que suporte, no
futuro, um pipeline de CI/CD com `destroy`/recreate da infra de
aplicação sem derrubar o DNS (ver "Arquitetura de duas camadas" abaixo).

## Contexto

Levantamento feito diretamente na conta AWS (`648443184523`, perfil
`agent-toolkit`, `us-east-1`) antes de escrever esta spec:

- **S3**: bucket `gastosapp-frontend-prod` — hosting estático servido via
  origem CloudFront (não há `website configuration` no bucket, o acesso é
  só via CloudFront). Public access block totalmente bloqueado
  (`BlockPublicAcls`, `IgnorePublicAcls`, `BlockPublicPolicy`,
  `RestrictPublicBuckets` = `true`). Server-side encryption `AES256`
  (SSE-S3). Sem versionamento configurado. Bucket policy concede
  `s3:GetObject` apenas ao principal `cloudfront.amazonaws.com`,
  restrito por `AWS:SourceArn` da distribuição CloudFront abaixo.
- **CloudFront**: distribuição `E2YCZNS0F94SCU`
  (`drtfcfd20ysux.cloudfront.net`), criada em `2026-07-30`. Aliases
  `jrnexpenses.com` e `www.jrnexpenses.com`. Origem = bucket S3 acima,
  via **Origin Access Control** `E1ZY2CM7WZ1H6` (não OAI legado).
  `DefaultRootObject=index.html`, `ViewerProtocolPolicy=redirect-to-https`,
  cache policy gerenciada pela AWS (`658327ea-f89d-4fab-a63d-7e88639e58f6`),
  `PriceClass_All`, IPv6 habilitado, HTTP/2.
- **ACM**: certificado `jrnexpenses.com`
  (`arn:aws:acm:us-east-1:648443184523:certificate/a29d5ddb-d617-400f-95d1-aca8b9d3a64a`,
  status `ISSUED`), anexado à distribuição CloudFront acima. **Não
  confundir** com o certificado `api.jrnexpenses.com`
  (`arn:...certificate/1b64dbcd-...`), que pertence ao contexto
  backend/API — fora do escopo desta feature.
- **WAF WebACL**: `CreatedByCloudFront-8ee8deea`
  (`arn:aws:wafv2:us-east-1:648443184523:global/webacl/CreatedByCloudFront-8ee8deea/dad6fab1-e0cb-48e6-aa48-57459260f456`),
  criado automaticamente pelo console ao habilitar proteção de segurança
  na criação da distribuição. 3 regras gerenciadas pela AWS (IP
  Reputation List, Common Rule Set, Known Bad Inputs). **Custo
  investigado e confirmado como zero**: a conta está no plano flat-rate
  **CloudFront Free** — confirmado pela linha "CloudFront Flat-Rate
  Plans" ($0,00) e "AWS WAF" ($0,00 em todos os dias de julho/2026) no
  Cost Explorer. Esse plano inclui WAF + proteção DDoS + 100GB de
  transferência para 1 distribuição sem cobrança adicional. **Nota de
  atenção para o futuro** (fora do escopo de ação desta feature): esse
  benefício vale enquanto a conta permanecer dentro dos limites do plano
  Free (1 distribuição, 100GB/mês); se isso mudar, o WAF passa a ser
  cobrado à parte (~US$5 base + ~US$1/regra/mês).
- **Route 53**: zona hospedada `jrnexpenses.com.` (`Z053098817OJTJ5LWHAZW`,
  10 records hoje). Dela, pertencem ao **frontend**:
  - `jrnexpenses.com` A (alias) → `drtfcfd20ysux.cloudfront.net`
  - `jrnexpenses.com` AAAA (alias) → `drtfcfd20ysux.cloudfront.net`
  - `www.jrnexpenses.com` A (alias) → `drtfcfd20ysux.cloudfront.net`
  - `www.jrnexpenses.com` AAAA (alias) → `drtfcfd20ysux.cloudfront.net`
  - CNAME de validação DNS do certificado ACM `jrnexpenses.com` (record
    `_f91e552da643f7310e2ef48005c54b0d.jrnexpenses.com`)
  - CNAME de validação DNS do SAN `www.jrnexpenses.com` do mesmo
    certificado (record
    `_632a98ba4517a08bda86576acc344e22.www.jrnexpenses.com`)

  **Explicitamente excluídos** desta feature (ficam manuais/fora do
  Terraform):
  - `NS` e `SOA` da zona — records default criados automaticamente junto
    com a hosted zone; não devem ser importados/gerenciados manualmente
  - `api.jrnexpenses.com` A (alias para o API Gateway) e seu CNAME de
    validação ACM (`_f581ceffb919246b3f9f8e25a5c2b084.api.jrnexpenses.com`)
    — pertencem ao contexto **backend**, não ao frontend; se forem
    trazidos para Terraform no futuro, isso é decisão/feature do
    contexto backend, não desta
  - **O registro do domínio em si** (`jrnexpenses.com`, comprado via
    Amazon Registrar) — pertence à conta AWS onde foi registrado;
    migrá-lo para outra conta é um processo de transferência de
    registrador, fora do alcance do Terraform e fora do escopo desta
    feature
- **State Terraform**: já existe o bucket
  `gastosapp-terraform-state-648443184523`, criado pelo módulo
  `backend/infra/terraform/bootstrap/`. Esta feature **reusa esse mesmo
  bucket** para os states do frontend (duas `key`s distintas, uma por
  camada — ver abaixo), sem criar um novo bootstrap — evita duplicar
  recurso AWS.
- Identificados na conta, mas **sem relação com o frontend** (ignorar):
  bucket `elasticbeanstalk-sa-east-1-648443184523` e `jrn-bucket-curso`.
- Precedente direto de estilo/abordagem:
  `backend/specs/FEAT-09-terraform-cognito-parameter-store/`, que fez
  exatamente esse tipo de reconciliação (infra manual → Terraform) para
  Cognito e Parameter Store no backend.

## Arquitetura de duas camadas (motivada por necessidade futura)

O usuário pretende, em uma feature futura, montar um pipeline de CI/CD
com opção de `destroy` (derruba a infra de um ambiente) e reaplicação
(`destroy=false`, reconstrói do zero e a aplicação volta a funcionar).
Se DNS e infra de aplicação vivessem no mesmo state, um `destroy` levaria
o DNS junto — inaceitável. Além disso, ao recriar CloudFront/ACM do
zero, o domínio do CloudFront e o CNAME de validação do ACM mudam de
valor, então os records DNS precisam se atualizar automaticamente, sem
passo manual, para o site voltar a funcionar.

Por isso, mesmo esta feature (import puro, sem construir o pipeline
ainda) já organiza o Terraform em **duas configurações independentes**,
seguindo o mesmo princípio que o backend já usa para separar
`bootstrap/` (persistente) da config principal (recriável):

- **`frontend/infra/terraform/dns/`** — camada **persistente**, nunca
  destruída pelo pipeline futuro, compartilhada por todos os ambientes
  (a zona é uma só; prod e uma futura homologação seriam só mais
  records nela). Gerencia:
  - A hosted zone `jrnexpenses.com.` como **recurso gerenciado**
    (`aws_route53_zone`, importado — não `data source`), protegida com
    `lifecycle { prevent_destroy = true }` (mesmo padrão já usado em
    `backend/infra/terraform/bootstrap/main.tf` para o bucket de state).
    Isso permite reconstruir a zona do zero numa conta AWS nova, se
    algum dia for necessário, sem correr risco de exclusão acidental no
    dia a dia.
  - Os 6 records DNS do frontend, como `aws_route53_record`.
- **`frontend/infra/terraform/environments/prod/`** (config principal) —
  camada **efêmera**, destruível/recriável pelo pipeline futuro. Gerencia
  S3, CloudFront, ACM e WAF WebACL.

Para os records se atualizarem automaticamente quando a infra principal
for recriada, a camada `dns/` lê o domínio do CloudFront e os dados de
validação do certificado ACM via **`terraform_remote_state`** (data
source apontando para o state de `environments/prod/`), em vez de
valores fixos. Assim, depois de recriar a infra principal e rodar
`apply` na camada `dns/`, os records passam a apontar automaticamente
para os novos recursos.

**Preparo barato para uma futura homologação (`hom.jrnexpenses.com`/
`api-hom.jrnexpenses.com`, mesma conta AWS — pedido explicitamente como
"não fazer nesta feature", mas com dois ajustes de baixo custo já
adotados aqui):**
- A pasta já nasce em `environments/prod/` (em vez de
  `frontend/infra/terraform/` direto) — quando a homologação for
  especificada, ela vira `environments/hom/`, sem precisar mover state
  de uma estrutura de pasta plana para uma com ambientes.
- Nomes que hoje só têm um valor possível (domínio `jrnexpenses.com`,
  nome do bucket `gastosapp-frontend-prod`) ficam em `variables.tf`
  (`var.domain_name`, `var.frontend_bucket_name`) em vez de string
  literal direta nos `resource`, mesmo com um único valor hoje — reduz
  atrito para reaproveitar os mesmos blocos de recurso com outros
  valores depois.
- **Não** é criado nenhum módulo Terraform reutilizável, nenhuma lógica
  condicional de ambiente, nem qualquer recurso de homologação nesta
  feature — isso é decisão da feature futura, quando ela for de fato
  especificada (evita construir abstração para um requisito ainda não
  detalhado).
- Tabela DynamoDB separada por ambiente é decisão do contexto
  **backend**, fora do alcance desta spec (frontend) — não é tratada
  aqui.

**Limitação reconhecida, fora do alcance do Terraform**: o registro do
domínio (`jrnexpenses.com`) em si pertence à conta onde foi comprado —
reconstruir tudo numa conta AWS nova exige também migrar/transferir o
domínio via processo de registrador, que não é resolvido por nenhuma
configuração Terraform. Essa arquitetura resolve zona + records + infra
de aplicação, não o registro do domínio.

## Requisitos de negócio / restrições

- **Import apenas**: nenhum recurso é criado, recriado ou destruído
  nesta feature. Todos os recursos (S3, CloudFront, ACM, WAF WebACL, a
  hosted zone e os 6 records DNS do frontend) devem ser trazidos via
  `terraform import` a partir do estado real atual.
- **Custo zero adicional**: nenhuma configuração nova pode gerar
  cobrança. O WAF WebACL é importado como está, sem adicionar regras. A
  hosted zone **não é criada nem duplicada** (é importada, a que já
  existe); os records importados não têm custo adicional (Route 53
  cobra por zona e por volume de query, não por record).
- **Nenhuma ação na conta AWS sem autorização prévia explícita do
  usuário** — vale tanto para o desenho da estratégia (`plan.md`) quanto
  para qualquer execução futura (`terraform import`, `terraform plan`
  que já é seguro, `terraform apply`). Nenhum comando que possa alterar
  estado real roda de forma autônoma; cada `import` é confirmado
  individualmente no momento da execução.
- **Duas configurações Terraform separadas** (`dns/` persistente +
  principal efêmera), com states independentes, ambos no bucket de state
  já existente do backend, com `key`s distintas.
- **Hosted zone importada como recurso gerenciado, com `prevent_destroy =
  true`** — nunca como `data source` (para suportar reconstrução em conta
  nova no futuro), mas protegida contra exclusão acidental.
- **Records de `api.jrnexpenses.com` e os records default (`NS`/`SOA`)
  ficam fora do escopo** — não são importados nem referenciados.
- **Registro do domínio em si fora do escopo** — não é gerenciado por
  Terraform nesta feature (limitação reconhecida, ver seção acima).
- **Preparo de baixo custo para homologação futura**: config principal
  vive em `frontend/infra/terraform/environments/prod/`; nomes hoje
  fixos (domínio, nome do bucket) ficam em `variables.tf`. Nenhum módulo
  reutilizável, lógica de ambiente ou recurso de homologação é criado
  nesta feature.

## User Stories

**US1 — Bucket S3 gerenciado pelo Terraform**
- Given o bucket `gastosapp-frontend-prod` já existe, criado manualmente,
  servindo o build estático do frontend
- When a estratégia de import é aplicada
- Then o Terraform (config `environments/prod/`) passa a gerenciar o bucket, sua
  bucket policy, o public access block e a configuração de encryption,
  com os mesmos valores observados hoje (nenhuma mudança de
  comportamento)

**US2 — Distribuição CloudFront gerenciada pelo Terraform**
- Given a distribuição `E2YCZNS0F94SCU` já existe, servindo
  `jrnexpenses.com`/`www.jrnexpenses.com` a partir do bucket acima
- When a estratégia de import é aplicada
- Then o Terraform (config `environments/prod/`) passa a gerenciar a distribuição
  (aliases, origem via Origin Access Control, cache policy, certificado
  ACM, WAF WebACL associado), sem nenhuma mudança de configuração
  observável

**US3 — Certificado ACM gerenciado pelo Terraform**
- Given o certificado `jrnexpenses.com` já existe e está `ISSUED`,
  anexado à distribuição acima
- When a estratégia de import é aplicada
- Then o Terraform (config `environments/prod/`) passa a gerenciar esse certificado
  (não o de `api.jrnexpenses.com`, que é do backend)

**US4 — WAF WebACL gerenciado pelo Terraform**
- Given o WebACL `CreatedByCloudFront-8ee8deea` já existe, associado à
  distribuição, com 3 regras gerenciadas AWS, sem custo (plano CloudFront
  Free)
- When a estratégia de import é aplicada
- Then o Terraform (config `environments/prod/`) passa a gerenciar esse WebACL com
  as mesmas 3 regras, sem adicionar, remover ou alterar nenhuma regra

**US5 — Hosted zone gerenciada e protegida contra destroy acidental**
- Given a hosted zone `jrnexpenses.com.` já existe, criada manualmente
- When a estratégia de import é aplicada
- Then o Terraform (config `dns/`) passa a gerenciar a zona como recurso
  (`aws_route53_zone`, importado), com `lifecycle { prevent_destroy =
  true }`

**US6 — Records DNS do frontend gerenciados pelo Terraform, com
auto-atualização**
- Given os records `jrnexpenses.com` (A/AAAA), `www.jrnexpenses.com`
  (A/AAAA) e os 2 CNAMEs de validação do certificado ACM já existem na
  hosted zone
- When a estratégia de import é aplicada
- Then o Terraform (config `dns/`) passa a gerenciar esses 6 records,
  lendo o domínio do CloudFront e os dados de validação do ACM via
  `terraform_remote_state` a partir do state da config principal (não
  valores fixos) — para que, se a infra principal for recriada no
  futuro, os records se atualizem automaticamente ao rodar `apply` na
  config `dns/`; os records `NS`/`SOA` e os de `api.jrnexpenses.com`
  continuam fora do Terraform

**US7 — Nenhuma diferença após a reconciliação**
- Given todos os recursos acima já foram trazidos para o Terraform (nas
  duas configs)
- When se roda `terraform plan` em cada uma
- Then o resultado é "No changes" em ambas — o código Terraform reflete
  exatamente o que existe na conta AWS

**US8 — Nenhuma execução sem aprovação explícita**
- Given qualquer comando que possa criar, alterar ou destruir um recurso
  AWS (`terraform import`, `terraform apply`)
- When esse comando está prestes a ser executado
- Then o usuário é consultado e precisa aprovar explicitamente antes da
  execução — nenhum comando desse tipo roda de forma autônoma

## Critérios de aceite

- [x] `terraform state list` da config principal
      (`frontend/infra/terraform/environments/prod/`) inclui: bucket S3
      (+ policy + public access block + encryption), distribuição
      CloudFront, certificado ACM (`jrnexpenses.com`) e WAF WebACL
- [x] Nomes de domínio e do bucket parametrizados via `variables.tf` (não
      hardcoded), em ambas as configs onde aparecem
- [x] `terraform state list` da config `dns/`
      (`frontend/infra/terraform/dns/`) inclui: hosted zone
      `jrnexpenses.com.` e os 6 records DNS do frontend
- [x] `terraform plan` após a reconciliação retorna "No changes" nas
      duas configs, contra a conta AWS real
- [x] State remoto de ambas as configs no bucket
      `gastosapp-terraform-state-648443184523` (reaproveitado do
      backend), cada uma com sua própria `key`
- [x] Config `dns/` lê o domínio do CloudFront e os dados de validação do
      ACM via `terraform_remote_state`, não valores fixos
- [x] Hosted zone importada com `lifecycle { prevent_destroy = true }`
- [x] Nenhum comando `import`/`apply` foi executado sem aprovação
      explícita do usuário no momento da execução
- [x] `frontend/infra/CLAUDE.md` atualizado para refletir a arquitetura
      de duas camadas e que a infra de hosting do frontend (incluindo
      DNS) passa a ser gerenciada por Terraform
- [x] Records `NS`/`SOA` e os de `api.jrnexpenses.com` permanecem fora do
      Terraform

## Fora do escopo

- Records `NS`/`SOA` da zona e os records de `api.jrnexpenses.com`
  (pertencem ao contexto backend) — permanecem manuais
- Registro do domínio (`jrnexpenses.com`) em si — fora do alcance do
  Terraform, ver "Arquitetura de duas camadas"
- Qualquer alteração nas regras do WAF WebACL, nos valores dos records
  DNS ou em qualquer outra configuração dos recursos importados — só
  reconciliação, sem mudança de comportamento
- Construção do pipeline de CI/CD com `destroy`/recreate em si — esta
  feature só prepara a estrutura de Terraform (duas camadas) que o
  suportará; o pipeline é uma feature futura separada
- Pipeline de deploy automatizado do conteúdo estático (upload do build
  para o S3 continua manual/CI futuro — não é parte desta feature)
- Novo bootstrap de bucket de state — reaproveita o já existente do
  backend
