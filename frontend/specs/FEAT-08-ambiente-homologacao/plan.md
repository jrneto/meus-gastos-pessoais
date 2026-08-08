# Plan — FEAT-08: Ambiente de homologação do frontend

## Camadas afetadas

- **`frontend/infra/terraform/`** (única camada de código afetada):
  - Nova config `environments/hom/`, espelhando `environments/prod/`
    (bucket S3 + distribuição CloudFront + certificado ACM + **WAF
    WebACL dedicado**, ver decisão técnica abaixo).
  - `dns/` (camada persistente, já existente) ganha os records novos de
    `hom.jrnexpenses.com` e um segundo `data "terraform_remote_state"`
    apontando para o state de `environments/hom/`.
- **`frontend/app/`**: nenhuma mudança de código de aplicação/feature.
  Só um novo arquivo de env (`.env.hom.example`) e um script npm
  (`build:hom`) para gerar o build apontando para
  `VITE_API_BASE_URL=https://api-hom.jrnexpenses.com` — mesmo mecanismo
  de `mode` do Vite já usado por `.env.production`/`npm run build`,
  nenhuma lógica nova de runtime.
- **`frontend/infra/CLAUDE.md`** e `frontend/infra/terraform/README.md`:
  atualizados ao final, documentando o ambiente de homologação (critério
  de aceite da spec).

Nenhuma camada de `Api`/`Application`/`Domain`/`Infrastructure` é
afetada — não existe esse conceito no frontend; e nada no backend é
tocado por esta feature (CORS fica fora do escopo, conforme spec).

## Decisões técnicas

1. **WAF WebACL dedicado de homologação, no plano flat-rate Free
   próprio da distribuição.** Confirmado via documentação oficial
   (["CloudFront flat-rate pricing plans"](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/flat-rate-pricing-plan.html)):
   cada plano cobre **1 distribuição CloudFront** (não "1 por conta") e
   a conta pode ter **até 3 planos Free simultâneos** — produção já usa
   1, sobra espaço para hom usar o 2º. Cada plano Free inclui, a
   US$0/mês: a distribuição + um **WAF WebACL dedicado** (até 5 regras —
   os mesmos 3 AWS Managed Rule Groups de produção cabem) + DDoS
   protection + até 1M requisições/100GB de transferência por mês —
   folgado para tráfego de homologação. Ou seja: hom terá **a mesma
   postura de segurança de produção** (WAF próprio, mesmas regras), sem
   o custo de ~US$5 base + ~US$1/regra/mês apontado como risco na spec.
   - **`aws_wafv2_web_acl`, a distribuição, o certificado ACM e o
     bucket** são criados normalmente via Terraform — sem gap de
     suporte.
   - **A assinatura ao plano Free em si** (o que zera o custo da
     distribuição + WAF) é feita através de um serviço novo da AWS
     (`PricingPlanManager`). O recurso correspondente no provider
     Terraform (`aws_pricingplanmanager_subscription`) ainda **não foi
     lançado** — existe um PR aberto e não mesclado no
     `terraform-provider-aws` ([PR #49235](https://github.com/hashicorp/terraform-provider-aws/pull/49235),
     [issue #49232](https://github.com/hashicorp/terraform-provider-aws/issues/49232)),
     fora de qualquer versão publicada (o provider aqui está travado em
     `~> 5.0`, resolvendo hoje `5.100.0`). **Combinado com o usuário**:
     essa assinatura é feita **manualmente no console AWS**, depois do
     `terraform apply` criar a distribuição + WAF de hom — quando o
     recurso for lançado numa versão futura do provider, ela é trazida
     para o Terraform via `terraform import`, mesmo padrão já usado na
     FEAT-07 para reconciliar recursos criados fora do código. Até lá,
     esse passo manual fica documentado como gap conhecido em
     `frontend/infra/CLAUDE.md`/README.
   - Sem essa assinatura, a distribuição + WAF ficam em cobrança
     pay-as-you-go padrão — por isso a ordem importa: `apply` cria os
     recursos, a assinatura ao plano Free é o passo seguinte,
     aprovado e executado manualmente pelo usuário antes de considerar
     a feature concluída.
2. **Um único alias**: `hom.jrnexpenses.com`, sem variante `www` (já
   registrado como fora do escopo na spec). Certificado ACM emitido só
   para esse nome (sem SAN).
3. **Terceira `key` de state**, mesmo bucket já reaproveitado
   (`gastosapp-terraform-state-648443184523`):
   `gastosapp-frontend/hom/terraform.tfstate` — segue o padrão já usado
   por `gastosapp-frontend/prod/...` e `gastosapp-frontend/dns/...`
   (FEAT-07). Nenhum bootstrap novo.
4. **Recursos criados via `apply`, não `import`** — diferente da FEAT-07
   (que só reconciliava recursos manuais existentes), aqui não há nada
   pré-existente: os nomes/IDs internos (origin ID, nome do OAC) são
   deixados a cargo do Terraform/AWS, sem necessidade de reproduzir
   valores "criados pelo console" como em `environments/prod/`.
5. **`dns/` ganha um segundo `terraform_remote_state`** (`data
   "terraform_remote_state" "hom"`), em vez de reaproveitar o já
   existente `"prod"` — mantém os dois ambientes desacoplados (recriar
   hom não deve depender de nada do state de prod, e vice-versa).
6. **Build do frontend de homologação via `mode` do Vite**
   (`.env.hom`, não versionado — só `.env.hom.example` — mesma regra já
   aplicada a `.env.development`/`.env.production`), script
   `"build:hom": "tsc -b && vite build --mode hom"` em `package.json`.
   Upload do `dist/` gerado para o bucket de hom continua manual (fora
   do escopo desta feature, já registrado na spec).

## Recursos Terraform — `environments/hom/`

Mesmos arquivos de `environments/prod/`, adaptados:

- **`versions.tf`** — igual ao de prod, exceto
  `backend.s3.key = "gastosapp-frontend/hom/terraform.tfstate"`.
- **`variables.tf`**:
  - `aws_region` (default `"us-east-1"`, mesmo motivo de produção:
    CloudFront/ACM/WAF (scope `CLOUDFRONT`) exigem `us-east-1`)
  - `hom_domain_name` (default `"hom.jrnexpenses.com"`)
  - `frontend_bucket_name` (default `"gastosapp-frontend-hom"`)
- **`s3.tf`** — `aws_s3_bucket`, `aws_s3_bucket_public_access_block`
  (mesmos 4 blocos `true`), `aws_s3_bucket_server_side_encryption_configuration`
  (`AES256`), `aws_s3_bucket_policy` (mesmo documento JSON de
  `environments/prod/s3.tf`, `Principal = cloudfront.amazonaws.com`,
  `Condition.ArnLike.AWS:SourceArn` apontando para a distribuição de
  hom) — idêntico ao padrão de prod, só o nome do bucket muda.
- **`acm.tf`** — `aws_acm_certificate` para `var.hom_domain_name`, sem
  `subject_alternative_names` (não há `www`), `validation_method =
  "DNS"`, `lifecycle { create_before_destroy = true }` (mesmo padrão de
  prod).
- **`waf.tf`** — `aws_wafv2_web_acl` próprio de hom (`scope =
  "CLOUDFRONT"`), com os mesmos 3 AWS Managed Rule Groups de produção
  (`AWSManagedRulesAmazonIpReputationList`, `AWSManagedRulesCommonRuleSet`,
  `AWSManagedRulesKnownBadInputsRuleSet`), `default_action { allow {} }`
  — mesma estrutura de `environments/prod/waf.tf`, nome/metric name
  próprios de hom (ex.: `gastosapp-hom-web-acl`, não precisa reproduzir
  o nome "criado pelo console" de prod, ver decisão 4).
- **`cloudfront.tf`** — `aws_cloudfront_origin_access_control` +
  `aws_cloudfront_distribution` (`aliases = [var.hom_domain_name]`,
  `default_root_object = "index.html"`, `viewer_protocol_policy =
  "redirect-to-https"`, `cache_policy_id` = mesma cache policy
  gerenciada pela AWS já usada em prod — `"CachingOptimized"`,
  `658327ea-f89d-4fab-a63d-7e88639e58f6`, `web_acl_id =
  aws_wafv2_web_acl.hom.arn`, `price_class = "PriceClass_All"` — igual
  produção. Como hom está no plano flat-rate Free (decisão técnica 1),
  não há diferença de custo entre price classes (ambas cobertas pelo
  plano); `PriceClass_All` evita perda de latência para acessos do
  Brasil sem nenhum trade-off de custo.
- **`outputs.tf`** — `cloudfront_domain_name`, `cloudfront_hosted_zone_id`,
  `acm_domain_validation_options` (mesmo formato de
  `environments/prod/outputs.tf`, consumidos por `dns/`).

## Recursos Terraform — `dns/` (mudanças)

- **`remote_state.tf`**: adicionar
  ```hcl
  data "terraform_remote_state" "hom" {
    backend = "s3"
    config = {
      bucket = var.state_bucket
      key    = var.hom_state_key
      region = var.aws_region
    }
  }
  ```
- **`variables.tf`**: adicionar `hom_state_key` (default
  `"gastosapp-frontend/hom/terraform.tfstate"`).
- **`route53.tf`**: adicionar, na mesma `aws_route53_zone.main` já
  gerenciada:
  - `aws_route53_record.hom_a` / `hom_aaaa` — alias para
    `data.terraform_remote_state.hom.outputs.cloudfront_domain_name` /
    `cloudfront_hosted_zone_id`.
  - `aws_route53_record.acm_validation_hom` (ou entrada adicional no
    `for_each` existente, se os `domain_validation_options` de hom forem
    unidos ao mapa atual sem colidir com os de prod) — CNAME de
    validação do certificado ACM de hom.

## Recursos AWS novos (resumo)

| Recurso | Novo? | Observação |
|---|---|---|
| Bucket S3 `gastosapp-frontend-hom` | Sim | Sem custo fixo |
| Distribuição CloudFront (hom) | Sim | Via Terraform; custo zerado após assinatura manual ao plano Free |
| WAF WebACL dedicado (hom) | Sim | Via Terraform (`aws_wafv2_web_acl`), mesmas 3 AWS Managed Rules de prod |
| Origin Access Control (hom) | Sim | Sem custo |
| Certificado ACM `hom.jrnexpenses.com` | Sim | Gratuito |
| Records DNS (`hom` A/AAAA + CNAME validação ACM) | Sim | Zona já existe, sem custo incremental |
| Assinatura ao plano Free (distribuição + WAF de hom) | Sim, **manual** | `aws_pricingplanmanager_subscription` ainda não existe no provider Terraform — feito no console, `import` futuro quando disponível |
| Nenhum recurso de produção é alterado | — | `environments/prod/` e `dns/` (records existentes) inalterados |

## Mapeamento de erros

Não aplicável — feature de infraestrutura, sem endpoints/contratos HTTP
novos nem lógica de negócio.

## Pontos que precisam de confirmação do usuário antes do `/tasks`/implementação

1. ~~Confirmar a decisão de WAF~~ — **resolvido**: WAF WebACL dedicado
   de hom, mesmo plano Free de produção (decisão técnica 1).
2. ~~Confirmar `price_class`~~ — **resolvido**: `PriceClass_All`, igual
   produção (sem diferença de custo dentro do plano Free).
3. **Confirmar o passo manual de assinatura ao plano Free** no console
   AWS, executado por você depois do `terraform apply` (decisão técnica
   1) — sem esse passo, a distribuição + WAF de hom ficam em
   pay-as-you-go padrão em vez de US$0.
4. Nenhum comando `terraform plan`/`apply` roda sem aprovação explícita
   no momento da execução (US6) — isso vale a partir do `/tasks`, não é
   uma decisão a fechar agora.
