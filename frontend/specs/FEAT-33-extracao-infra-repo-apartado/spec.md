# FEAT-33: Extração da infra Terraform do frontend para o repositório `infra-jrnexpenses`

## Objetivo

Separar a infraestrutura de **plataforma** do frontend (CloudFront,
Origin Access Control, certificado ACM, WAF WebACL, hosted zone e
records DNS da camada `dns/`, roles de CI/CD) da infraestrutura de
**workload** (o que o deploy de fato toca: o bucket S3 do site e sua
policy), levando a plataforma para um repositório novo e apartado,
`infra-jrnexpenses`, de forma **paulatina e controlada** — um state por
vez, sem criar, destruir ou recriar nenhum recurso AWS, com `terraform
plan` = "No changes" dos dois lados ao final de cada etapa e caminho de
rollback conhecido.

O monorepo continua dono só do Terraform essencial ao deploy do site
(bucket + public access block + encryption + bucket policy); tudo o mais
passa a viver em `infra-jrnexpenses`.

## Contexto

### Situação atual (levantada em 2026-09-12)

`frontend/infra/terraform/` tem 4 configurações raiz independentes, sem
módulos nem workspaces, todas com apply manual/local (nenhum workflow de
CI executa Terraform — ver `frontend/infra/CLAUDE.md`):

| Configuração | State | Conteúdo |
|---|---|---|
| `dns/` | `gastosapp-frontend/dns/terraform.tfstate` | hosted zone `jrnexpenses.com.` (`prevent_destroy`) + 8 records (apex/www A/AAAA, hom A/AAAA, 2 `for_each` de validação ACM); lê os states de `environments/{prod,hom}` via `terraform_remote_state` |
| `environments/prod/` | `gastosapp-frontend/prod/terraform.tfstate` | bucket `gastosapp-frontend-prod` (+ PAB, SSE, policy), OAC, distribuição `E2YCZNS0F94SCU`, ACM `jrnexpenses.com`, WAF `CreatedByCloudFront-8ee8deea` |
| `environments/hom/` | `gastosapp-frontend/hom/terraform.tfstate` | idem para hom (`gastosapp-frontend-hom`, `ELE195A1APCLB`, ACM `hom.jrnexpenses.com`, WAF `gastosapp-hom-web-acl`) |
| `cicd/` | `gastosapp-frontend/cicd/terraform.tfstate` (**vazio**) | OIDC provider + role `gastosapp-frontend-cicd` — referência, fora de state por causa do guardrail de IAM |

Os workflows (`frontend-deploy-{hom,prod}.yml`) publicam só via `aws s3
sync` + `aws cloudfront create-invalidation`, com
`BUCKET_NAME`/`DISTRIBUTION_ID`/`CICD_ROLE_ARN` cadastrados à mão nos
GitHub Environments `hom`/`prod`. **Nenhum valor de `terraform output` é
consumido por CI** — a extração não muda nada nos workflows nem nos
GitHub Environments.

### Acoplamento a resolver

Dentro de cada `environments/{prod,hom}/` há referência cruzada entre o
que fica e o que sai:

- `aws_s3_bucket_policy.frontend` (fica) → `aws_cloudfront_distribution.main.arn` (sai)
- `aws_cloudfront_distribution.main.origin.domain_name` (sai) →
  `aws_s3_bucket.frontend.bucket_regional_domain_name` (fica)

E a camada `dns/` (sai) lê `cloudfront_domain_name`,
`cloudfront_hosted_zone_id` e `acm_domain_validation_options` dos states
de `environments/{prod,hom}` — que passam a estar no state da infra
depois que CloudFront/ACM saírem.

### Feature irmã no backend

A parte do backend (DynamoDB, Cognito, Parameter Store, SES, API Gateway,
domínio `api*`, `cicd/` e `bootstrap/` do backend) é a
`backend/specs/FEAT-40-extracao-infra-repo-apartado/`. As duas specs
compartilham o repositório de destino, a governança e o mecanismo de
migração, mas cada uma lista só as suas etapas e é entregue em
branch/PR próprios. Ordem combinada: **frontend antes do backend** (hom
antes de prod em ambos) — o frontend tem menos recursos por state e
serve para validar o padrão de referência cruzada antes de mexer no
state que contém Cognito/DynamoDB de produção.

### Decisões fechadas com o usuário

- **Fronteira** — vai para `infra-jrnexpenses`: `dns/` inteira (zona +
  records), CloudFront (distribuição + OAC), ACM, WAF WebACL, `cicd/`.
  **Fica no monorepo**: `aws_s3_bucket.frontend`,
  `aws_s3_bucket_public_access_block.frontend`,
  `aws_s3_bucket_server_side_encryption_configuration.frontend` e
  `aws_s3_bucket_policy.frontend`, em cada ambiente.
- **Repositório novo nasce limpo** (commit inicial, sem reescrever
  histórico) — o histórico dos `.tf` continua acessível no monorepo.
- **Governança inalterada**: apply continua manual/local, cada
  `apply`/`state push` é aprovado individualmente pelo usuário, nenhum
  Terraform passa a rodar em CI.
- **Direção única de dependência: monorepo → infra, nunca o inverso.**
  A bucket policy do monorepo lê o ARN da distribuição via
  `terraform_remote_state` (mesmo bucket, padrão já usado por `dns/`); a
  distribuição na infra referencia o bucket só pelo domínio regional
  determinístico construído a partir do nome
  (`<bucket>.s3.us-east-1.amazonaws.com`, mesmo valor que
  `bucket_regional_domain_name` retorna hoje) — sem diff no plan.
- **States da infra ganham `key`s novas** no mesmo bucket
  (`infra-jrnexpenses/frontend/{hom,prod}/terraform.tfstate`,
  `infra-jrnexpenses/dns/terraform.tfstate`); os states do monorepo
  mantêm as `key`s atuais e só perdem os recursos movidos. A camada
  `dns/` reponta seus `terraform_remote_state` para as `key`s novas à
  medida que cada ambiente migra.
- **Mecanismo de movimentação**: `terraform state pull` nos dois lados →
  `terraform state mv -state=… -state-out=…` offline (não chama AWS, não
  esbarra no guardrail de IAM, funciona para records `for_each`) →
  `terraform state push` nos dois lados → `plan` = "No changes" em
  ambos. Detalhamento e scripts por etapa são escopo do `plan.md`.

Precedente direto de estilo/abordagem:
`frontend/specs/FEAT-07-terraform-import-infra/` (reconciliação sem
mudar comportamento, camadas persistente/efêmera, `terraform_remote_state`).

## Requisitos de negócio / restrições

- **Nenhum recurso AWS criado, destruído, recriado ou alterado** por esta
  feature — só movimentação de state e reorganização de código Terraform.
  Qualquer `plan` que mostre `create`/`destroy`/`replace`/`update` em um
  recurso movido é bloqueio: investigar e corrigir o código antes de
  qualquer `apply`. Atenção especial à distribuição CloudFront e à
  hosted zone (`prevent_destroy`): um `replace` acidental derrubaria o
  site.
- **Custo zero**: nenhum recurso novo na conta. O único artefato novo na
  AWS são objetos de state (`key`s novas) no bucket já existente. A
  assinatura manual ao plano Free do CloudFront não é tocada.
- **Cada etapa é atômica e verificável**: um state por etapa; ao final,
  `terraform plan` = "No changes" tanto na configuração da infra quanto
  na do monorepo (e em `dns/`, quando afetada), e o deploy de hom
  (`frontend-deploy-hom.yml`) continua verde sem nenhuma alteração em
  workflow ou GitHub Environment.
- **Rollback conhecido antes de cada `state push`**: o bucket de state é
  versionado — restaurar a versão anterior do objeto reverte o state; o
  código reverte via git. Enquanto um recurso existir nos dois states
  (entre o push da infra e o push do monorepo), nenhum `apply` pode rodar
  em nenhum dos dois lados.
- **Nenhuma execução sem aprovação prévia explícita do usuário** — vale
  para `state push`, `apply` e qualquer comando que altere state remoto
  ou recurso real. `plan`, `state pull` e `state list` são leitura e podem
  rodar livremente. A lista de endereços a mover em cada etapa é revisada
  pelo usuário antes de executar.
- **Nenhuma mudança de comportamento observável do site** — URLs,
  certificado, regras de WAF, cache, SPA fallback (403/404 → `index.html`)
  continuam idênticos.
- **Hom antes de prod**, sempre: a etapa de prod só começa depois da de
  hom estar concluída, validada por deploy verde e smoke manual em
  `https://hom.jrnexpenses.com`. A etapa de prod é feita em janela
  combinada com o usuário.
- **Docs coerentes ao fim**: `frontend/infra/CLAUDE.md`,
  `frontend/infra/terraform/README.md`, `/CLAUDE.md` raiz (seção
  "Infraestrutura" e a afirmação "não existe infraestrutura compartilhada
  entre contextos") e `/docs/architecture.md` passam a refletir que a
  plataforma vive em `infra-jrnexpenses` e o monorepo só gerencia o
  workload.

## Impacto observável externamente

Nenhum. Não há URL, certificado, regra de WAF, comportamento de cache,
nome de bucket, ID de distribuição ou GitHub Environment alterado. O
único efeito visível é onde o código Terraform vive e em qual state cada
recurso está registrado.

## User Stories

**US1 — Repositório `infra-jrnexpenses` criado com esqueleto (compartilhada com a FEAT-40 do backend)**
- Given o repositório `infra-jrnexpenses` ainda não existe
- When o usuário cria o repositório no GitHub e a etapa 0 é concluída
- Then ele contém commit inicial com `CLAUDE.md` (governança: apply
  manual, aprovação explícita, guardrail de IAM, mapa de states e ordem
  de dependência), `README.md` (init/plan/apply por configuração),
  `.gitignore` de Terraform e a árvore `terraform/` vazia, sem nenhum
  state remoto tocado

**US2 — `cicd/` do frontend movido como arquivos (compartilhada com a FEAT-40)**
- Given `frontend/infra/terraform/cicd/` é referência (state vazio, fora
  do Terraform por causa do guardrail de IAM)
- When a etapa 1 é concluída
- Then ele vive em `infra-jrnexpenses/terraform/cicd/` ao lado do do
  backend, a pasta foi removida do monorepo e nenhum state remoto foi
  alterado

**US3 — Camada `dns/` movida para `infra-jrnexpenses/terraform/dns/`**
- Given o state `gastosapp-frontend/dns/terraform.tfstate` contém a
  hosted zone e os 8 records
- When a etapa de DNS é concluída
- Then zona e records estão em `infra-jrnexpenses/dns/terraform.tfstate`,
  a zona mantém `lifecycle { prevent_destroy = true }`, os
  `terraform_remote_state` continuam lendo os states de
  `environments/{prod,hom}` do monorepo (até as etapas seguintes
  repontarem), a pasta foi removida do monorepo e `terraform plan` =
  "No changes" em `dns/`

**US4 — Plataforma de hom movida para `infra-jrnexpenses/terraform/frontend/hom/`**
- Given o state `gastosapp-frontend/hom/terraform.tfstate` contém
  plataforma e workload juntos
- When a etapa de hom é concluída
- Then OAC, distribuição, ACM `hom.jrnexpenses.com` e WAF
  `gastosapp-hom-web-acl` estão em
  `infra-jrnexpenses/frontend/hom/terraform.tfstate`; o monorepo mantém
  só bucket, PAB, SSE e bucket policy; `dns/` lê o state novo de hom; e
  `terraform plan` = "No changes" na infra (`frontend/hom` e `dns`) e no
  monorepo (`environments/hom`)

**US5 — Bucket policy lê a distribuição via `terraform_remote_state`**
- Given `aws_s3_bucket_policy.frontend` referenciava
  `aws_cloudfront_distribution.main.arn` do mesmo state
- When a etapa do ambiente é concluída
- Then o ARN da distribuição vem do output do state da infra via
  `terraform_remote_state`, sem valor hardcoded novo, e a infra não lê
  nada do state do monorepo — a origem da distribuição usa o domínio
  regional do bucket construído a partir do nome, com valor idêntico ao
  atual

**US6 — Plataforma de prod movida, mesmo padrão de hom**
- Given a etapa de hom está concluída e validada (deploy verde + smoke
  manual em `https://hom.jrnexpenses.com`)
- When a etapa de prod é concluída, em janela combinada com o usuário
- Then OAC, distribuição `E2YCZNS0F94SCU`, ACM `jrnexpenses.com` (+ SAN
  `www`) e WAF `CreatedByCloudFront-8ee8deea` estão em
  `infra-jrnexpenses/frontend/prod/terraform.tfstate`, `dns/` lê o state
  novo de prod, e `terraform plan` = "No changes" nos dois lados e em
  `dns/`

**US7 — Nenhuma regressão no deploy e no site**
- Given os workflows de deploy não foram alterados
- When cada etapa de hom/prod é concluída
- Then `frontend-deploy-hom.yml` (e, para prod, um deploy de release
  quando houver) roda verde, o site responde normalmente no domínio do
  ambiente (incluindo F5 em rota interna — SPA fallback) e a invalidação
  de cache continua funcionando

**US8 — Nenhuma execução sem aprovação explícita**
- Given qualquer comando que altere state remoto ou recurso real
  (`terraform state push`, `terraform apply`)
- When esse comando está prestes a ser executado
- Then o usuário é consultado, revisa a lista de endereços/`plan` e
  aprova explicitamente antes da execução — nada roda de forma autônoma

**US9 — Documentação coerente ao fim (compartilhada com a FEAT-40)**
- Given `frontend/infra/CLAUDE.md`, `frontend/infra/terraform/README.md`,
  `/CLAUDE.md` raiz e `/docs/architecture.md` descrevem toda a infra como
  vivendo no monorepo
- When a última etapa é concluída
- Then esses documentos descrevem a divisão plataforma
  (`infra-jrnexpenses`) × workload (monorepo), a direção de dependência
  entre states e apontam para o repositório novo para tudo o que saiu

## Critérios de aceite

- [ ] Repositório `infra-jrnexpenses` criado pelo usuário, com commit
      inicial contendo `CLAUDE.md`, `README.md`, `.gitignore` e
      `terraform/` (etapa 0, compartilhada com a FEAT-40)
- [ ] `cicd/` do frontend vive em `infra-jrnexpenses`; pasta removida de
      `frontend/infra/terraform/`; nenhum state remoto alterado nessa
      etapa
- [ ] `terraform state list` de `infra-jrnexpenses/terraform/dns/`
      contém a hosted zone e os 8 records; `frontend/infra/terraform/dns/`
      não existe mais; `plan` = "No changes"
- [ ] `terraform state list` de `infra-jrnexpenses/terraform/frontend/hom/`
      contém exatamente OAC, distribuição, ACM e WAF de hom;
      `terraform state list` de `frontend/infra/terraform/environments/hom/`
      contém exatamente bucket, PAB, SSE e bucket policy
- [ ] `terraform plan` = "No changes" em
      `infra-jrnexpenses/terraform/frontend/hom/`,
      `infra-jrnexpenses/terraform/dns/` e
      `frontend/infra/terraform/environments/hom/` após a etapa de hom
- [ ] O mesmo para prod, após a etapa de prod
- [ ] Monorepo lê o ARN da distribuição via `terraform_remote_state`
      (output `cloudfront_distribution_arn`, ou equivalente definido no
      `plan.md`); a infra não contém `terraform_remote_state` apontando
      para state do monorepo; `dns/` lê os states novos da infra
- [ ] Nenhum recurso AWS criado, destruído, recriado ou alterado —
      confirmado pelos `plan` acima e pela ausência de `apply` com
      mudanças em qualquer etapa
- [ ] Nenhuma alteração em `.github/workflows/*` nem nos GitHub
      Environments `hom`/`prod`; `frontend-deploy-hom.yml` verde após a
      etapa de hom; `https://hom.jrnexpenses.com` respondendo, incluindo
      F5 em rota interna (smoke manual)
- [ ] Nenhum `terraform state push`/`apply` executado sem aprovação
      explícita do usuário no momento da execução
- [ ] `frontend/infra/CLAUDE.md`, `frontend/infra/terraform/README.md`,
      `/CLAUDE.md` raiz e `/docs/architecture.md` atualizados (etapa
      final, compartilhada com a FEAT-40)
- [ ] `frontend/docs/backlog.md` atualizado: FEAT-33 marcada como concluída

## Fora do escopo

- A parte do backend (DynamoDB, Cognito, Parameter Store, SES, API
  Gateway, domínio `api*`, `cicd/` e `bootstrap/` do backend) — é a
  `backend/specs/FEAT-40-extracao-infra-repo-apartado/`
- Qualquer mudança de valor/configuração dos recursos movidos (regras do
  WAF, cache policy, aliases, TTL de records etc.) — só movimentação
- Pipeline de CI para `terraform fmt`/`validate`/`plan` no repositório
  novo — apply continua manual; pode virar melhoria futura se o usuário
  quiser
- Consolidar states (ex.: juntar frontend e backend de um mesmo ambiente
  em um único state) — otimização futura, não pré-requisito
- Criar módulos Terraform reutilizáveis ou lógica condicional de
  ambiente — hom e prod continuam como configurações paralelas
- Trazer `cicd/` para dentro de state (`import` da role/OIDC) — continua
  bloqueado pelo guardrail de IAM, permanece referência
- Trazer a assinatura ao plano Free do CloudFront para Terraform —
  continua aguardando o recurso do provider
- Preservar histórico git dos `.tf` no repositório novo — decisão: nasce
  limpo
- Qualquer alteração em workflows de deploy, GitHub Environments ou no
  fluxo de release
