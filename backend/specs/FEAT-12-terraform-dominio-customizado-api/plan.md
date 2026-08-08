# Plan — FEAT-12: Domínio customizado da API sob Terraform

## Camadas afetadas

Só a camada de **infraestrutura** (`backend/infra/terraform/`). Nenhuma
mudança em `Api`/`Application`/`Domain`/`Infrastructure` (código .NET),
nenhum novo endpoint, nenhuma mudança de contrato — a API continua
respondendo exatamente igual, só passa a ter seu domínio customizado
descrito em código em vez de existir apenas manualmente na conta AWS.

Arquivos novos, dentro da configuração principal já existente
(`backend/infra/terraform/`, mesmo state, sem nova config/bootstrap):

- `backend/infra/terraform/acm.tf` — certificado ACM de
  `api.jrnexpenses.com` (import)
- `backend/infra/terraform/api-gateway-domain.tf` — domínio customizado
  do HTTP API (`aws_apigatewayv2_domain_name` + `aws_apigatewayv2_api_mapping`,
  import)
- `backend/infra/terraform/dns.tf` — os 2 records Route 53 de
  `api.jrnexpenses.com` (A alias + CNAME de validação ACM, import)

Arquivos existentes com pequenas adições:

- `backend/infra/terraform/outputs.tf` — novo output com a URL do
  domínio customizado
- `backend/infra/CLAUDE.md` e `backend/infra/terraform/README.md` —
  atualizados ao final, documentando que o domínio customizado também
  está sob Terraform (mesmo padrão de atualização feito na FEAT-09)

Nenhum arquivo de `api-gateway.tf`, `lambda.tf`, `cognito.tf`,
`dynamodb.tf` ou `parameter-store.tf` é alterado — apenas referenciados
(`aws_apigatewayv2_api.main`, `aws_apigatewayv2_stage.default`).

## Decisão técnica: como referenciar a hosted zone do frontend

A spec deixou em aberto para o `plan.md` como o backend referencia a
zona `jrnexpenses.com.` (gerenciada por
`frontend/infra/terraform/dns/`, não pelo backend). Duas opções:

1. **`terraform_remote_state`** (mesmo padrão que `frontend/infra/terraform/dns/`
   já usa para ler `environments/prod/`) — exigiria adicionar um
   `outputs.tf` em `frontend/infra/terraform/dns/` expondo `zone_id`
   (hoje essa config não tem nenhum output), ou seja, tocar em infra do
   **outro contexto** para viabilizar esta feature.
2. **`data "aws_route53_zone"`** (data source, consulta a zona pelo
   nome `jrnexpenses.com.` diretamente na API da AWS, sem depender do
   arquivo de state do frontend) — não exige nenhuma mudança em
   `frontend/infra/`, e ainda assim não gerencia/duplica a zona (é só
   leitura).

**Decisão proposta: opção 2** (`data "aws_route53_zone"`) — evita
acoplar o backend ao layout interno do state do frontend e não exige
tocar em arquivos do contexto frontend para uma feature de escopo
backend, mantendo a separação de contextos do `/CLAUDE.md` raiz. A
spec permite explicitamente "outra alternativa equivalente" ao
`terraform_remote_state`, então isso está dentro do escopo já
acordado — mas como é uma escolha de acoplamento entre contextos,
**peço confirmação explícita antes do `/tasks`**.

```hcl
data "aws_route53_zone" "jrnexpenses" {
  name         = "jrnexpenses.com."
  private_zone = false
}
```

## Contratos técnicos (recursos Terraform)

### `acm.tf` — certificado ACM (import)

```hcl
resource "aws_acm_certificate" "api" {
  domain_name       = "api.jrnexpenses.com"
  validation_method = "DNS"

  lifecycle {
    create_before_destroy = true
  }
}
```

- Import: `terraform import aws_acm_certificate.api <arn-completo>`
  (ARN observado parcialmente na investigação:
  `arn:aws:acm:us-east-1:648443184523:certificate/1b64dbcd-...` — a
  execução precisa confirmar o ARN completo via
  `aws acm list-certificates` antes do `import`)
- Depois do import, `key_algorithm`, SANs e demais atributos
  computados devem bater automaticamente (certificado já `ISSUED`,
  nenhuma alteração é feita nele)

### `dns.tf` — CNAME de validação do certificado (import)

```hcl
resource "aws_route53_record" "api_acm_validation" {
  for_each = {
    for dvo in aws_acm_certificate.api.domain_validation_options :
    dvo.domain_name => dvo
  }

  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = each.value.resource_record_name
  type    = each.value.resource_record_type
  ttl     = 300
  records = [each.value.resource_record_value]
}
```

- Import: `terraform import 'aws_route53_record.api_acm_validation["api.jrnexpenses.com"]' <zone_id>_<record_name>_<record_type>`
  (formato de import ID do provider AWS para `aws_route53_record`;
  `record_name` aqui é o CNAME de validação
  `_f581ceffb919246b3f9f8e25a5c2b084.api.jrnexpenses.com`, já
  identificado na investigação da spec)
- Só há 1 domain validation option (certificado sem SAN adicional,
  diferente do certificado do frontend que cobre `jrnexpenses.com` +
  `www.jrnexpenses.com`), mas o `for_each` segue o mesmo padrão usado
  em `frontend/infra/terraform/dns/route53.tf` por consistência e para
  não quebrar se um SAN for adicionado no futuro

### `api-gateway-domain.tf` — domínio customizado do API Gateway (import)

```hcl
resource "aws_apigatewayv2_domain_name" "api" {
  domain_name = "api.jrnexpenses.com"

  domain_name_configuration {
    certificate_arn = aws_acm_certificate.api.arn
    endpoint_type   = "REGIONAL"
    security_policy = "TLS_1_2"
  }
}

resource "aws_apigatewayv2_api_mapping" "api" {
  api_id      = aws_apigatewayv2_api.main.id
  domain_name = aws_apigatewayv2_domain_name.api.id
  stage       = aws_apigatewayv2_stage.default.id
}
```

- Import: `terraform import aws_apigatewayv2_domain_name.api api.jrnexpenses.com`
  e `terraform import aws_apigatewayv2_api_mapping.api <domain_name>/<api_mapping_id>`
  (o `api_mapping_id` precisa ser obtido via
  `aws apigatewayv2 get-api-mappings --domain-name api.jrnexpenses.com`
  antes do import, não é adivinhado)
- **Ponto a confirmar na execução**: `endpoint_type` (assumido
  `REGIONAL`, padrão para HTTP API) e `security_policy` (assumido
  `TLS_1_2`, único valor suportado hoje) precisam bater exatamente com
  o recurso real — conferir com
  `aws apigatewayv2 get-domain-name --domain-name api.jrnexpenses.com`
  antes de finalizar o bloco, para o `terraform plan` pós-import dar
  "No changes"

### `dns.tf` — record A do domínio da API (import)

```hcl
resource "aws_route53_record" "api_a" {
  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = "api.jrnexpenses.com"
  type    = "A"

  alias {
    name                   = aws_apigatewayv2_domain_name.api.domain_name_configuration[0].target_domain_name
    zone_id                = aws_apigatewayv2_domain_name.api.domain_name_configuration[0].hosted_zone_id
    evaluate_target_health = false
  }
}
```

- Import: `terraform import aws_route53_record.api_a <zone_id>_api.jrnexpenses.com_A`
- O alias aponta para o domínio regional gerado pelo próprio
  `aws_apigatewayv2_domain_name.api` (computado, não hardcoded) —
  mesmo princípio do frontend de nunca fixar um valor que possa mudar
  se o recurso for recriado no futuro

### `outputs.tf` — adição

```hcl
output "api_custom_domain_url" {
  description = "URL pública da API através do domínio customizado."
  value       = "https://${aws_apigatewayv2_domain_name.api.domain_name}"
}
```

## Ordem de execução (cada comando aprovado individualmente)

1. Confirmar ARN completo do certificado (`aws acm list-certificates`)
   e `api_mapping_id` (`aws apigatewayv2 get-api-mappings`)
2. Escrever os 3 arquivos `.tf` acima com os valores confirmados
3. `terraform plan` — deve mostrar apenas os recursos a serem
   **importados** (sem plano ainda, só validação de sintaxe/config)
4. `terraform import` de cada um dos 5 recursos, um por vez, com
   aprovação explícita antes de cada comando:
   `aws_acm_certificate.api` → `aws_route53_record.api_acm_validation[...]`
   → `aws_apigatewayv2_domain_name.api` → `aws_apigatewayv2_api_mapping.api`
   → `aws_route53_record.api_a`
5. `terraform plan` final — deve retornar **"No changes"**; qualquer
   diferença exige ajustar o `.tf` (nunca o `apply` para "empurrar" o
   código por cima do recurso real nesta feature, já que é import puro)
6. Validação manual: `curl https://api.jrnexpenses.com/expenses` sem
   token (espera 401) e um fluxo completo de login + `/expenses`
   continuam funcionando, igual a antes
7. Atualizar `backend/infra/CLAUDE.md` e
   `backend/infra/terraform/README.md`

## Recursos AWS usados/afetados

Nenhum recurso **novo** é criado — todos os 4 recursos abaixo (+ a
leitura da zona) já existem manualmente na conta AWS e passam a ser
geridos pelo Terraform do backend via `import`:

| Recurso | Tipo Terraform | Ação |
|---|---|---|
| Certificado ACM `api.jrnexpenses.com` | `aws_acm_certificate` | import |
| CNAME de validação do certificado | `aws_route53_record` | import |
| Domínio customizado do API Gateway | `aws_apigatewayv2_domain_name` | import |
| Mapeamento domínio → stage | `aws_apigatewayv2_api_mapping` | import |
| Record A de `api.jrnexpenses.com` | `aws_route53_record` | import |
| Hosted zone `jrnexpenses.com.` | `data.aws_route53_zone` | leitura (não gerenciado aqui) |

Nenhum custo adicional: certificados públicos ACM são gratuitos, o
domínio customizado do API Gateway não tem cobrança própria, e os
records adicionais na hosted zone já existente não geram custo
incremental (Route 53 cobra por zona/volume de query, não por record).

## Mapeamento de erros de negócio

Não aplicável — esta feature não introduz nem altera nenhum
Command/Query/Handler, endpoint ou regra de negócio. Nenhum `Error`/
`ErrorType`/status HTTP é afetado.

## Pontos que precisam de confirmação antes do `/tasks`

1. **Decisão de acoplamento**: usar `data "aws_route53_zone"` (leitura
   direta, sem tocar em `frontend/infra/`) em vez de
   `terraform_remote_state` apontando para o state do frontend —
   confirmar que essa é a abordagem desejada.
2. **ARN completo do certificado** `api.jrnexpenses.com` — só temos o
   prefixo (`1b64dbcd-...`) da investigação da spec; precisa ser
   obtido via AWS CLI/console antes de qualquer `import`.
3. **`api_mapping_id`** do mapeamento existente entre o domínio
   customizado e o stage `$default` — precisa ser obtido via
   `aws apigatewayv2 get-api-mappings --domain-name api.jrnexpenses.com`
   antes do `import`.
4. **`endpoint_type`/`security_policy`** do `aws_apigatewayv2_domain_name`
   real — assumidos como `REGIONAL`/`TLS_1_2` (valores mais comuns),
   mas precisam ser confirmados contra o recurso real antes de fechar
   o `.tf`, para o `terraform plan` pós-import dar "No changes".