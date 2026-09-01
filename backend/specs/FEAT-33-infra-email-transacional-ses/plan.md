# Plan — FEAT-33: Infraestrutura de e-mail transacional (SES)

## Camadas afetadas

Só a camada de **infraestrutura** (`backend/infra/terraform/`), igual à
FEAT-12. Nenhuma mudança em `Api`/`Application`/`Domain`/`Infrastructure`
(código .NET) — chamar o SES pra disparar um e-mail específico é escopo
de quem consome esta infra (FEAT-34, FEAT-36, FEAT-37), não desta
feature. O único efeito observável fora do Terraform é o Cognito passar
a enviar os e-mails nativos dele (cadastro/recuperação de senha) a
partir de um remetente do domínio verificado.

Arquivos novos, um por ambiente (`environments/{prod,hom}/`):

- `ses.tf` — identidade de domínio SES + DKIM + verificação (mesmo
  padrão de arquivo dedicado por recurso já usado em
  `lambda-account-trigger.tf`)

Arquivos existentes com adições:

- `dns.tf` — records de verificação de domínio (TXT) e DKIM (3× CNAME)
  na hosted zone `jrnexpenses.com.`, mesmo mecanismo de leitura
  cross-contexto (`data.aws_route53_zone.jrnexpenses`, já existe) usado
  pela FEAT-12
- `cognito.tf` — bloco `email_configuration` no `aws_cognito_user_pool`
  já existente
- `lambda.tf` — nova statement SES na policy IAM já existente da Lambda
  da API principal
- `lambda-account-trigger.tf` — nova statement SES na policy IAM já
  existente da Lambda de trigger de conta
- `outputs.tf` — outputs novos com o ARN da identidade e o remetente
- `backend/infra/CLAUDE.md` — nova seção documentando identidades por
  ambiente, DKIM, `email_configuration` e status do sandbox (ao final,
  depois do apply)

Nenhum arquivo de `dynamodb.tf`, `api-gateway.tf`, `acm.tf`,
`api-gateway-domain.tf` ou `parameter-store.tf` é alterado.

## Decisão técnica: remetente (From) por ambiente

A spec não fechou o endereço/nome de remetente exato — só que cada
ambiente usa sua própria identidade verificada. Os templates do design
system (`frontend/design-system/emails/`) chamam o produto de
**jrn.expenses** (ex.: assunto "Sua senha do jrn.expenses foi
alterada"), não "GastosApp".

**Proposta**:
- Prod: `jrn.expenses <no-reply@jrnexpenses.com>`
- Hom: `jrn.expenses (homologação) <no-reply@hom.jrnexpenses.com>` —
  prefixo "(homologação)" no nome de exibição pra nunca ser confundido
  com um e-mail de prod durante testes manuais/`IntegrationTests`

`no-reply@` porque nenhum dos 4 templates do design system pede resposta
(README: "e-mails transacionais dispensam link de descadastro"). O
endereço de suporte que aparece no rodapé de alguns templates
(`suporte@jrnexpenses.com.br`, hoje placeholder `.com.br`) é só exibido
como texto, não é usado como remetente/reply-to — segue fora do escopo
desta feature (mesmo tratamento das outras URLs placeholder).

**Peço confirmação antes do `/tasks`** — endereço, nome de exibição e o
sufixo "(homologação)" são convenção nova, sem precedente direto no
projeto.

## Contratos técnicos (recursos Terraform)

### `ses.tf` — identidade de domínio, DKIM e verificação

Prod (`environments/prod/ses.tf`):

```hcl
resource "aws_ses_domain_identity" "main" {
  domain = "jrnexpenses.com"
}

resource "aws_ses_domain_dkim" "main" {
  domain = aws_ses_domain_identity.main.domain
}

# Só fica "success" depois que o record TXT de verificação (dns.tf)
# propagar — mesmo princípio do aws_acm_certificate_validation já usado
# no certificado de hom (FEAT-12).
resource "aws_ses_domain_identity_verification" "main" {
  domain     = aws_ses_domain_identity.main.id
  depends_on = [aws_route53_record.ses_verification]
}
```

Hom (`environments/hom/ses.tf`) — idêntico, trocando o domínio:

```hcl
resource "aws_ses_domain_identity" "main" {
  domain = "hom.jrnexpenses.com"
}

resource "aws_ses_domain_dkim" "main" {
  domain = aws_ses_domain_identity.main.domain
}

resource "aws_ses_domain_identity_verification" "main" {
  domain     = aws_ses_domain_identity.main.id
  depends_on = [aws_route53_record.ses_verification]
}
```

Cada ambiente usa o nome local `main` (sem colisão — tipo de recurso já
diferencia o endereço Terraform, mesmo padrão de `aws_cognito_user_pool.main`
já usado em `cognito.tf`).

### `dns.tf` — adição (records de verificação e DKIM)

Prod, dentro do `dns.tf` já existente:

```hcl
resource "aws_route53_record" "ses_verification" {
  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = "_amazonses.jrnexpenses.com"
  type    = "TXT"
  ttl     = 300
  records = [aws_ses_domain_identity.main.verification_token]
}

resource "aws_route53_record" "ses_dkim" {
  count   = 3
  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = "${aws_ses_domain_dkim.main.dkim_tokens[count.index]}._domainkey.jrnexpenses.com"
  type    = "CNAME"
  ttl     = 300
  records = ["${aws_ses_domain_dkim.main.dkim_tokens[count.index]}.dkim.amazonses.com"]
}
```

Hom, dentro do `dns.tf` já existente — mesmo formato, trocando
`jrnexpenses.com` por `hom.jrnexpenses.com` nos `name` (o SES sempre
publica o TXT de verificação em `_amazonses.<domínio-verificado>`,
mesmo quando o domínio verificado já é um subdomínio).

### `cognito.tf` — adição (`email_configuration`)

Prod:

```hcl
resource "aws_cognito_user_pool" "main" {
  # ...bloco existente sem mudança...

  email_configuration {
    email_sending_account = "DEVELOPER"
    source_arn            = aws_ses_domain_identity.main.arn
    from_email_address    = "jrn.expenses <no-reply@jrnexpenses.com>"
  }

  depends_on = [aws_ses_domain_identity_verification.main]
}
```

Hom: mesmo bloco, `from_email_address = "jrn.expenses (homologação) <no-reply@hom.jrnexpenses.com>"`.

- `depends_on` explícito porque `source_arn`/`from_email_address` por si
  só não criam uma dependência implícita forte o bastante pra garantir
  que o Cognito só é reconfigurado depois da identidade **verificada**
  (não só criada) — evita o `apply` terminar com o User Pool apontando
  pra uma identidade ainda "pending".

### `lambda.tf` — adição (IAM da Lambda da API principal)

Nova `Sid` dentro do `aws_iam_role_policy.lambda_exec` já existente:

```hcl
{
  Sid      = "SesSendEmail"
  Effect   = "Allow"
  Action   = ["ses:SendEmail", "ses:SendRawEmail"]
  Resource = aws_ses_domain_identity.main.arn
}
```

### `lambda-account-trigger.tf` — adição (IAM da Lambda de trigger)

Mesma `Sid`/Action, dentro do `aws_iam_role_policy.account_trigger_lambda_exec`
já existente — `Resource` aponta pra mesma `aws_ses_domain_identity.main.arn`
do próprio ambiente (`ses.tf` está na mesma pasta).

### `outputs.tf` — adição

```hcl
output "ses_domain_identity_arn" {
  description = "ARN da identidade de domínio SES verificada."
  value       = aws_ses_domain_identity.main.arn
}

output "ses_sender_email" {
  description = "Remetente usado pelo Cognito e pelas Lambdas para envio via SES."
  value       = aws_cognito_user_pool.main.email_configuration[0].from_email_address
}
```

## Investigação do sandbox do SES (fora do Terraform)

Não é recurso gerenciado por `.tf` — é um estado de conta consultado e,
se preciso, alterado via AWS CLI/console, documentado como parte dos
critérios de aceite:

1. **Investigar** (somente leitura, sem aprovação prévia necessária —
   não cria/altera nada):
   `aws sesv2 get-account --region us-east-1` → campo
   `ProductionAccessEnabled` (`true` = fora do sandbox).
2. **Se `false`** (ainda no sandbox): solicitar saída via
   `aws sesv2 put-account-details` (ou console, "Request production
   access") — precisa de um caso de uso descrito (texto livre) e fica
   sujeito a análise da AWS (pode levar até 24h). **Esta chamada exige
   aprovação explícita do usuário antes de ser executada**, mesmo sendo
   gratuita — é uma mudança de estado da conta, não uma leitura.
3. Resultado (dentro ou fora do sandbox, e se solicitado, status da
   solicitação) documentado em `backend/infra/CLAUDE.md` ao final —
   critério de aceite já previsto na spec.

Enquanto a conta estiver no sandbox, `POST /auth/register` com um
e-mail real não pré-verificado manualmente no SES continua falhando no
envio (ainda que o restante da infra esteja correta) — por isso é
critério de aceite, não follow-up.

## Ordem de execução (cada comando aprovado individualmente)

1. `aws sesv2 get-account --region us-east-1` em cada ambiente (leitura,
   sem aprovação necessária) — define se o passo 6 é preciso
2. Escrever `ses.tf` (prod e hom)
3. Escrever as adições em `dns.tf` (records de verificação + DKIM)
4. `terraform plan` em cada ambiente — deve mostrar só recursos novos
   sendo **criados** (identidade, DKIM, verificação, 4 records DNS),
   nada existente sendo destruído/recriado
5. `terraform apply` da identidade/DKIM/DNS em cada ambiente (aprovação
   explícita antes de cada apply) — sozinho, antes de tocar Cognito/IAM,
   pra isolar qualquer problema de verificação de domínio
6. Se algum ambiente ainda estiver no sandbox (passo 1): solicitar saída
   (aprovação explícita antes do comando)
7. Escrever as adições em `cognito.tf` (`email_configuration`), `lambda.tf`
   e `lambda-account-trigger.tf` (statements IAM), `outputs.tf`
8. `terraform plan` em cada ambiente — deve mostrar só os campos novos
   sendo adicionados (Cognito `email_configuration`, policy IAM), sem
   recriar o User Pool nem as Lambdas
9. `terraform apply` (aprovação explícita antes de cada apply)
10. Validação manual em hom: `POST /auth/register` com e-mail próprio
    verificável (ou já dentro do escopo permitido pelo sandbox/produção
    liberada) seguido de `POST /auth/login`, conferindo que o e-mail de
    confirmação chega com remetente `no-reply@hom.jrnexpenses.com`
11. Atualizar `backend/infra/CLAUDE.md` (nova seção SES) e
    `backend/docs/backlog.md` (FEAT-33 concluída)

## Recursos AWS usados/afetados

| Recurso | Tipo Terraform | Ambiente | Ação |
|---|---|---|---|
| Identidade de domínio SES | `aws_ses_domain_identity` | prod + hom | criação |
| DKIM da identidade | `aws_ses_domain_dkim` | prod + hom | criação |
| Verificação da identidade | `aws_ses_domain_identity_verification` | prod + hom | criação (espera DNS) |
| Record TXT de verificação | `aws_route53_record` | prod + hom | criação (na zona do frontend, via `data` já existente) |
| Records CNAME de DKIM (×3) | `aws_route53_record` | prod + hom | criação (idem) |
| `email_configuration` do User Pool | atributo em `aws_cognito_user_pool` já existente | prod + hom | alteração |
| Policy IAM da Lambda da API | atributo em `aws_iam_role_policy` já existente | prod + hom | alteração (nova `Sid`) |
| Policy IAM da Lambda de trigger | atributo em `aws_iam_role_policy` já existente | prod + hom | alteração (nova `Sid`) |
| Status de sandbox da conta | fora do Terraform (`sesv2 put-account-details`) | conta AWS (única, `648443184523`) | leitura sempre; alteração só se necessário |

Nenhum custo fixo: SES cobra por e-mail enviado (primeiros 62 mil/mês
gratuitos quando enviados de dentro da AWS — bem acima do volume
esperado de uma conta pessoal), identidade de domínio/DKIM/verificação
não têm custo próprio, e os records adicionais na hosted zone já
existente não geram custo incremental.

## Mapeamento de erros de negócio

Não aplicável — esta feature não introduz nem altera nenhum
Command/Query/Handler, endpoint ou regra de negócio. Nenhum `Error`/
`ErrorType`/status HTTP é afetado.

## Pontos que precisam de confirmação antes do `/tasks`

1. **Remetente por ambiente**: `jrn.expenses <no-reply@jrnexpenses.com>`
   (prod) e `jrn.expenses (homologação) <no-reply@hom.jrnexpenses.com>`
   (hom) — confirmar endereço, nome de exibição e o sufixo
   "(homologação)".
2. **Nomes dos recursos Terraform propostos** (`aws_ses_domain_identity.main`,
   `aws_ses_domain_dkim.main`, `aws_ses_domain_identity_verification.main`,
   `aws_route53_record.ses_verification`/`ses_dkim`) — sem precedente
   direto no projeto pra SES, confirmar que fazem sentido antes de virar
   `tasks.md`.
3. **Ordem em duas fases do apply** (identidade/DNS primeiro, Cognito/IAM
   depois) — proposta pra isolar problema de verificação de domínio
   antes de reconfigurar o Cognito; confirmar que faz sentido manter
   assim no `tasks.md` em vez de um único apply.
4. **Investigação do sandbox é leitura livre, mas a solicitação de saída
   (se necessária) exige aprovação explícita no momento** — confirmar
   que esse nível de granularidade (leitura sem perguntar, mutação
   sempre perguntando) está de acordo com a expectativa de aprovação da
   spec (US6).
