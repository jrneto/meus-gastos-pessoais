# FEAT-34 — Plano técnico: extração da infra Terraform do frontend para `infra-jrnexpenses`

Spec: [`spec.md`](spec.md). Feature irmã: `backend/specs/FEAT-40-extracao-infra-repo-apartado/`.

## 1. O que muda, e onde

Não há camada de aplicação envolvida (`frontend/app/` não é tocado). As
"camadas" aqui são configurações Terraform raiz, em dois repositórios:

| Configuração hoje (monorepo) | Depois | State |
|---|---|---|
| `frontend/infra/terraform/cicd/` | **movida como arquivos** → `infra-jrnexpenses/terraform/cicd/frontend/` | continua fora de state (guardrail IAM); o objeto `gastosapp-frontend/cicd/terraform.tfstate` (vazio) fica órfão |
| `frontend/infra/terraform/dns/` | **movida com state** → `infra-jrnexpenses/terraform/dns/` | `gastosapp-frontend/dns/…` → `infra-jrnexpenses/dns/terraform.tfstate` |
| `frontend/infra/terraform/environments/hom/` | **dividida**: OAC + distribuição + ACM + WAF → `infra-jrnexpenses/terraform/environments/hom/`; bucket + PAB + SSE + policy **ficam** | monorepo mantém `gastosapp-frontend/hom/…`; infra ganha `infra-jrnexpenses/hom/terraform.tfstate` (**um state por ambiente** — a FEAT-40 move os recursos do backend de hom para este mesmo state) |
| `frontend/infra/terraform/environments/prod/` | idem → `infra-jrnexpenses/terraform/environments/prod/` | monorepo mantém `gastosapp-frontend/prod/…`; infra ganha `infra-jrnexpenses/prod/terraform.tfstate` (idem) |

Bucket de state: o mesmo de sempre (`gastosapp-terraform-state-648443184523`,
`us-east-1`, `use_lockfile = true`), init parcial via `-backend-config`
como hoje.

### 1.1 Layout do repositório `infra-jrnexpenses` ao final desta feature

```
infra-jrnexpenses/
├── CLAUDE.md                     # governança + mapa de states + ordem de dependência
├── README.md                     # init/plan/apply por config; histórico da migração
├── .gitignore                    # cópia de frontend/infra/terraform/.gitignore
├── scripts/
│   └── migration/                # scripts de `state mv` por etapa (registro histórico)
│       ├── wave-2-dns.sh
│       ├── wave-3-frontend-hom.sh
│       └── wave-4-frontend-prod.sh
└── terraform/
    ├── cicd/
    │   └── frontend/             # oidc.tf, iam-role.tf, iam-policy.tf, variables.tf, versions.tf, outputs.tf (referência)
    ├── dns/                      # route53.tf, remote_state.tf, variables.tf, versions.tf
    └── environments/
        ├── hom/                  # versions.tf, variables.tf, data.tf, acm.tf, cloudfront.tf, waf.tf, outputs.tf (+ .tf do backend, FEAT-40)
        └── prod/                 # idem
```

**Um state por ambiente**: `terraform/environments/{hom,prod}/` é a
plataforma inteira do ambiente (frontend + backend). Esta feature
popula só a parte do frontend; `terraform/cicd/backend/`,
`terraform/bootstrap/` e os `.tf` do backend dentro de
`environments/{hom,prod}/` são escopo da FEAT-40. Para não colidir na
fusão, os arquivos do frontend usam prefixo no nome (`frontend-acm.tf`,
`frontend-cloudfront.tf`, `frontend-waf.tf`, `frontend-data.tf`,
`frontend-outputs.tf`); `versions.tf` e `variables.tf` são
compartilhados (a FEAT-40 só acrescenta variáveis).

## 2. Contratos técnicos entre states

### 2.1 Outputs que a infra expõe (por ambiente, `terraform/environments/{hom,prod}/frontend-outputs.tf`)

| Output | Valor | Consumidor |
|---|---|---|
| `cloudfront_domain_name` | `aws_cloudfront_distribution.main.domain_name` | `dns/` (records alias) — **já existe hoje**, mantido com o mesmo nome |
| `cloudfront_hosted_zone_id` | `aws_cloudfront_distribution.main.hosted_zone_id` | `dns/` — já existe, mantido |
| `acm_domain_validation_options` | `aws_acm_certificate.<nome>.domain_validation_options` | `dns/` — já existe, mantido |
| `cloudfront_distribution_arn` | `aws_cloudfront_distribution.main.arn` | **novo** — `aws_s3_bucket_policy.frontend` do monorepo (`AWS:SourceArn`) |
| `cloudfront_distribution_id` | `aws_cloudfront_distribution.main.id` | **novo** — informativo (bate com `DISTRIBUTION_ID` do GitHub Environment e com `cicd/variables.tf`) |

### 2.2 O que o monorepo passa a ler (`frontend/infra/terraform/environments/{hom,prod}/`)

```hcl
# remote_state.tf (novo) — mesmo padrão de frontend/infra/terraform/dns/remote_state.tf
data "terraform_remote_state" "infra" {
  backend = "s3"
  config = {
    bucket = var.state_bucket        # gastosapp-terraform-state-648443184523
    key    = var.infra_state_key     # infra-jrnexpenses/<env>/terraform.tfstate
    region = var.aws_region
  }
}
```

`s3.tf` muda **uma linha**:
`"AWS:SourceArn" = data.terraform_remote_state.infra.outputs.cloudfront_distribution_arn`.
O JSON resultante é byte a byte o mesmo (mesmo ARN) ⇒ sem diff.

### 2.3 O que a infra lê do lado do monorepo: **nada de state**

A distribuição precisa do domínio regional do bucket (hoje
`aws_s3_bucket.frontend.bucket_regional_domain_name`). Decisão: **`data
"aws_s3_bucket"` por nome**, não string literal:

```hcl
# data.tf — leitura por nome, mesmo padrão de data "aws_route53_zone" no backend
data "aws_s3_bucket" "frontend" {
  bucket = var.frontend_bucket_name
}
# cloudfront.tf: origin.domain_name = data.aws_s3_bucket.frontend.bucket_regional_domain_name
```

Por quê data source e não `"${var.frontend_bucket_name}.s3.${var.aws_region}.amazonaws.com"`:
o formato de `bucket_regional_domain_name` para `us-east-1` mudou entre
versões do provider (`s3.amazonaws.com` × `s3.us-east-1.amazonaws.com`);
o data source devolve exatamente o que o resource devolvia com o
provider em uso, eliminando o risco de um `update in-place` na origem
da distribuição. Continua sem `terraform_remote_state` apontando para o
monorepo (invariante da spec). Só exige `s3:ListBucket`/`GetBucketLocation`
— perfil `agent-toolkit` já tem.

### 2.4 `dns/` na infra

`variables.tf` mantém `prod_state_key`/`hom_state_key`, com defaults
repontados **por etapa**:

| Etapa | `hom_state_key` | `prod_state_key` |
|---|---|---|
| 2 (move `dns/`) | `gastosapp-frontend/hom/terraform.tfstate` (monorepo, inalterado) | `gastosapp-frontend/prod/terraform.tfstate` |
| 3 (move hom) | `infra-jrnexpenses/hom/terraform.tfstate` | inalterado |
| 4 (move prod) | inalterado | `infra-jrnexpenses/prod/terraform.tfstate` |

`route53.tf` e `remote_state.tf` não mudam (mesmos nomes de output).

### 2.5 Endereços de state movidos (mv `addr → addr`, nomes lógicos preservados)

| Etapa | State de origem | Endereços |
|---|---|---|
| 2 | `gastosapp-frontend/dns/` | `aws_route53_zone.main`, `aws_route53_record.apex_a`, `.apex_aaaa`, `.www_a`, `.www_aaaa`, `.acm_validation` (move as 2 instâncias `for_each`), `.hom_a`, `.hom_aaaa`, `.acm_validation_hom` (1 instância) |
| 3 | `gastosapp-frontend/hom/` | `aws_cloudfront_origin_access_control.frontend`, `aws_cloudfront_distribution.main`, `aws_acm_certificate.hom`, `aws_wafv2_web_acl.hom` |
| 4 | `gastosapp-frontend/prod/` | `aws_cloudfront_origin_access_control.frontend`, `aws_cloudfront_distribution.main`, `aws_acm_certificate.frontend`, `aws_wafv2_web_acl.frontend` |

`data.*` (remote_state, `aws_s3_bucket`) não são movidos — são
re-lidos no próximo `plan`. A lista real é sempre conferida contra
`terraform state list` no momento da execução, nunca de memória.

## 3. Mecanismo de movimentação (igual nas etapas 2, 3 e 4)

`terraform state mv` com `-state`/`-state-out` **não funciona com backend
S3 configurado** — roda de uma pasta scratch com backend local:

```bash
# 0) scratch (fora de qualquer repo; só provider, sem backend)
mkdir -p /tmp/tf-mv && cd /tmp/tf-mv
cat > versions.tf <<'EOF'
terraform {
  required_version = ">= 1.10"
  required_providers { aws = { source = "hashicorp/aws", version = "~> 5.0" } }
}
EOF
terraform init

# 1) baseline + backup (leitura)
(cd <monorepo>/frontend/infra/terraform/<config> && terraform plan)          # tem que ser "No changes" ANTES de começar
(cd <monorepo>/frontend/infra/terraform/<config> && terraform state pull) > /tmp/tf-mv/mono.tfstate
cp /tmp/tf-mv/mono.tfstate /tmp/tf-mv/backup-<config>-$(date +%Y%m%d%H%M).tfstate

# 2) state vazio do destino (init com a key nova)
(cd <infra>/terraform/<dest> && terraform init -backend-config=bucket=… -backend-config=region=us-east-1)
(cd <infra>/terraform/<dest> && terraform state pull) > /tmp/tf-mv/infra.tfstate   # pode vir vazio — state mv cria o arquivo

# 3) mv offline — script da etapa, revisado pelo usuário antes
bash <infra>/scripts/migration/wave-N-….sh    # loop: terraform state mv -state=mono.tfstate -state-out=infra.tfstate "$addr" "$addr"
terraform state list -state=infra.tfstate     # conferir
terraform state list -state=mono.tfstate      # conferir o que sobrou

# 4) push destino (APROVAÇÃO) → plan
(cd <infra>/terraform/<dest> && terraform state push /tmp/tf-mv/infra.tfstate)   # -force só se reclamar de lineage (state novo)
(cd <infra>/terraform/<dest> && terraform plan)   # esperado: nenhum recurso muda; só "Changes to Outputs" (ver 3.1)

# 5) push origem (APROVAÇÃO) → plan  (depois de editar os .tf do monorepo)
(cd <monorepo>/… && terraform state push /tmp/tf-mv/mono.tfstate)              # -force só se o serial não tiver subido
(cd <monorepo>/… && terraform plan)               # esperado: nenhum recurso muda; só outputs removidos (ver 3.1)
```

### 3.1 Gotcha: outputs só entram no state via `apply`

`state push` grava recursos, **não outputs**. Logo, após o push:

- na infra, `terraform plan` mostra `Changes to Outputs: + cloudfront_domain_name …`
  (e nenhum recurso) → um **`apply` só de outputs** (aprovado) é
  obrigatório antes de qualquer consumidor (`dns/`, monorepo) conseguir
  ler `outputs.*` via `terraform_remote_state` — sem isso, o
  `terraform_remote_state` devolve `null` e a bucket policy do monorepo
  planejaria um `update`;
- no monorepo, o `plan` mostra `Changes to Outputs: - …` para os outputs
  removidos → `apply` só de outputs (aprovado) para o state ficar limpo.

Ordem dentro de cada etapa, portanto: **push infra → apply(outputs) infra
→ plan infra = No changes → editar/push monorepo → apply(outputs)
monorepo → plan monorepo = No changes → repontar `dns/` → plan `dns/` =
No changes.** Um `apply` cujo plan lista qualquer recurso é bloqueio.

### 3.2 Rollback

Antes de cada push: backup local em `/tmp/tf-mv/backup-*.tfstate` (fora
de git) **e** o bucket é versionado. Reverter = `terraform state push
-force backup-….tfstate` no lado afetado (ou restaurar a versão anterior
do objeto no S3) + `git revert` dos `.tf`. Entre o push da infra e o
push do monorepo o recurso está nos dois states — nenhum `apply` nesse
intervalo, exceto o `apply` de outputs da infra (que não toca recurso).

### 3.3 Guardrail IAM

Nenhuma config desta feature tem IAM gerenciado (`cicd/` continua
referência). Todos os `plan`/`apply` rodam com `agent-toolkit`. O
`state mv` offline não chama a AWS.

## 4. Etapas (o `tasks.md` detalha cada uma)

### Etapa 0 — repositório + esqueleto (compartilhada com a FEAT-40)
**Parte manual já feita em 2026-09-12**: repositório
`github.com/jrneto/infra-jrnexpenses` criado (commit `a69eee0`, só
`README.md` de uma linha + `.gitignore` do template Terraform do
GitHub), clonado em `D:\git_jrneto\infra-jrnexpenses`, branches `main`
(default) e `develop` já existentes no remoto.

Falta o esqueleto (primeiro commit de conteúdo): `CLAUDE.md`,
`README.md` real, `scripts/migration/.gitkeep`, `terraform/.gitkeep` e
ajuste do `.gitignore` — o template do GitHub **não** ignora
`.terraform.lock.hcl` (o monorepo ignora; decisão §7.5 é manter a
prática, então acrescentar a linha) e ignora `override.tf`/
`*_override.tf` (manter, inofensivo). Sem Terraform executado.
`CLAUDE.md` do repo novo cobre: apply manual/local com aprovação por
execução; bucket/keys de state e ordem de dependência
(`environments/{hom,prod}` → `dns/`; monorepo → `environments/{hom,prod}`);
guardrail IAM; plano Free do CloudFront manual; princípio "infra nunca
lê state do monorepo"; fluxo de branches do repo (§7.7).

### Etapa 1 — `cicd/` do frontend (só arquivos)
Copiar `frontend/infra/terraform/cicd/*.tf` → `terraform/cicd/frontend/`
(sem alteração de conteúdo; `versions.tf` mantém a key
`gastosapp-frontend/cicd/terraform.tfstate` vazia — trocar a key de um
state vazio não tem valor). Remover a pasta do monorepo; mover a seção
"cicd/" do `frontend/infra/terraform/README.md` para o README da infra e
deixar ponteiro. Nenhum comando Terraform.

### Etapa 2 — `dns/`
1. Infra: criar `terraform/dns/` com `route53.tf`, `remote_state.tf`
   copiados; `versions.tf` com key `infra-jrnexpenses/dns/terraform.tfstate`;
   `variables.tf` com as keys **atuais** do monorepo (2.4). `init`.
2. Mecanismo §3 com `scripts/migration/wave-2-dns.sh` (9 endereços).
3. `plan` na infra `dns/` = No changes (dns não tem outputs → sem apply).
4. Monorepo: `mono.tfstate` fica só com `data.*` → push; remover
   `frontend/infra/terraform/dns/` inteira; o objeto
   `gastosapp-frontend/dns/terraform.tfstate` fica órfão (vazio, versionado)
   — remoção do objeto S3 é limpeza opcional, com aprovação, ao final da
   FEAT-40.
5. Commit infra + PR monorepo (só remoção + README/CLAUDE.md de infra).

### Etapa 3 — frontend hom
1. Infra: `terraform/environments/hom/` — `versions.tf` (key
   `infra-jrnexpenses/hom/terraform.tfstate`), `variables.tf`
   (`aws_region`, `hom_domain_name`, `frontend_bucket_name`),
   `frontend-data.tf` (2.3), `frontend-acm.tf`/`frontend-waf.tf`
   copiados sem alteração de conteúdo, `frontend-cloudfront.tf` com a
   única mudança da linha `origin.domain_name`, `frontend-outputs.tf`
   (2.1). `init`.
2. Mecanismo §3 com `wave-3-frontend-hom.sh` (4 endereços) → push infra
   → `apply` de outputs → `plan` = No changes.
3. Monorepo `environments/hom/`: apagar `acm.tf`, `cloudfront.tf`,
   `waf.tf`; `variables.tf` remove `hom_domain_name`, adiciona
   `state_bucket` + `infra_state_key`; novo `remote_state.tf`; `s3.tf`
   (uma linha); `outputs.tf` passa a expor só `bucket_name`/`bucket_arn`
   (informativo). Push `mono.tfstate` → `apply` de outputs → `plan` = No
   changes.
4. `dns/` (infra): `hom_state_key` → key nova → `plan` = No changes.
5. Validação: smoke em `https://hom.jrnexpenses.com` (inclusive F5 em
   rota interna); deploy de hom verde no próximo push em `develop` que
   toque `frontend/app/**` (ver ponto a confirmar em §7).
6. Commit infra + PR monorepo.

### Etapa 4 — frontend prod
Idem etapa 3 com `wave-4-frontend-prod.sh`, nomes lógicos de prod
(`aws_acm_certificate.frontend`, `aws_wafv2_web_acl.frontend`),
`variables.tf` de prod remove `domain_name`; `dns/` reponta
`prod_state_key`. **Janela combinada com o usuário**; `plan` da
distribuição de prod revisado linha a linha antes do push (um `replace`
aqui derruba o site).

### Etapa 7 (parte do frontend) — docs
`frontend/infra/CLAUDE.md` e `frontend/infra/terraform/README.md`
reescritos para o estado final (monorepo = só bucket/policy por
ambiente; tudo o mais aponta para `infra-jrnexpenses`);
`frontend/docs/backlog.md` marca FEAT-34. `/CLAUDE.md` raiz e
`/docs/architecture.md` são atualizados uma vez só, pela FEAT-40 (última
a terminar) — esta feature só deixa nota no PR.

## 5. Decisões técnicas

- **Um state por ambiente (frontend + backend), não por contexto** —
  decisão do usuário em 2026-09-12. Racional: "a plataforma de hom" é
  uma unidade operacional; o state resultante não tem IAM (as roles
  ficam no monorepo), então não há motivo de guardrail para separar; a
  migração não muda (cada etapa continua movendo um state de origem —
  as etapas 5/6 da FEAT-40 só fazem `state pull` de um destino já
  populado em vez de vazio); e Cognito/records de `api*` passam a poder
  referenciar recursos do frontend no mesmo state no futuro. Custo:
  raio de impacto maior por `apply` (um `plan` de CORS em prod também
  faz refresh de CloudFront/WAF) — aceitável com apply manual e revisão
  linha a linha. Colisão de nomes verificada em 2026-09-12: nenhuma
  entre os recursos/outputs que saem dos dois contextos; só
  `variable "aws_region"` é comum (declarada uma vez). Continuam
  separados: `dns/` (persistente), `cicd/{frontend,backend}`
  (referência) e `bootstrap/` (state local).
- **Nomes lógicos preservados** (`aws_acm_certificate.hom` × `.frontend`,
  `aws_wafv2_web_acl.hom` × `.frontend`): `state mv addr → addr` mantém o
  diff de revisão trivial. Unificar nomes entre hom/prod é refactor
  futuro (via `moved {}` dentro do mesmo state, sem risco).
- **Configs paralelas hom/prod, sem módulo** — mesma decisão da FEAT-07/08;
  módulo é abstração que a spec exclui.
- **`data "aws_s3_bucket"` em vez de literal** (2.3) — trade-off:
  dependência de leitura em runtime (o bucket precisa existir; sempre
  existirá enquanto houver site) × robustez a formato de domínio.
- **Keys novas para a infra, keys antigas no monorepo** — menos objetos
  tocados; o monorepo não precisa de `init -migrate-state`.
- **`state mv` offline, não `import`/`removed`** — `import` exigiria
  reconstruir IDs (WebACL `id/name/scope`, records `ZONEID_name_type`) e
  não cobre tudo no backend (FEAT-40); um único mecanismo para as duas
  features.
- **Scripts de migração versionados na infra** (`scripts/migration/`) —
  registro auditável do que foi movido; não são reutilizáveis nem
  idempotentes por desenho (rodam uma vez).
- **`.terraform.lock.hcl` continua gitignored** (prática atual) — manter
  consistência; versionar lockfile é decisão separada.

## 6. Recursos AWS usados/afetados

**Nenhum recurso AWS é criado, alterado ou destruído.** Únicos efeitos
na conta: 3 objetos novos no bucket de state (`infra-jrnexpenses/dns/`,
`…/hom/`, `…/prod/` + seus `.tflock` transitórios), 3
objetos existentes reescritos com menos recursos, e chamadas de leitura
(`plan`, `data "aws_s3_bucket"`). Custo: zero. IAM: nada. Workflows e
GitHub Environments: intocados.

Mapeamento de erros de negócio: não se aplica.

## 7. Decisões confirmadas com o usuário (2026-09-12)

1. **Origem da distribuição via `data "aws_s3_bucket"`** por nome (2.3),
   não string literal.
2. **Validação do deploy de hom (etapa 3)**: no `/tasks`, verificar se
   `frontend-deploy-hom.yml` aceita `workflow_dispatch`; se sim,
   disparar manualmente após a etapa; se não, aguardar o próximo push
   real em `frontend/app/**`. Smoke manual no site é feito de qualquer
   jeito.
3. **States órfãos** (`gastosapp-frontend/dns/…` e `…/cicd/…`): deixar no
   bucket; limpeza única dos órfãos dos dois contextos ao final da
   FEAT-40, com aprovação.
4. **`terraform/cicd/frontend/` + `terraform/cicd/backend/`** separados
   (referência, fora de state); fusão fora do escopo.
5. **Repositório privado, sem CI**; `.terraform.lock.hcl` continua
   gitignored (prática atual mantida — acrescentar ao `.gitignore` do
   template, que não a cobre).
6. **Um state por ambiente** (`terraform/environments/{hom,prod}/`, keys
   `infra-jrnexpenses/{hom,prod}/terraform.tfstate`) — ver §5.
7. **Fluxo de branches no `infra-jrnexpenses`** (repo já nasceu com
   `develop` + `main`): commits **direto em `develop`**, um por etapa
   (esqueleto, `dns/`, hom, prod); ao fechar cada FEAT (34 e depois 40)
   abre-se PR `develop → main` manual — `main` reflete o que está
   aplicado e validado, mesmo princípio do monorepo, sem workflow que
   abra o PR sozinho. Clone local: `D:\git_jrneto\infra-jrnexpenses`.
