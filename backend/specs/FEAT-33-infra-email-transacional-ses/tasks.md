# Tasks — FEAT-33: Infraestrutura de e-mail transacional (SES)

- [x] 1. Investigar o status do sandbox do SES na conta
      (`aws sesv2 get-account --region us-east-1`, leitura, sem
      aprovação necessária) — registrar `ProductionAccessEnabled` no
      commit/anotação, define se as tasks 15-16 são necessárias
      (`ProductionAccessEnabled: false`, `Max24HourSend: 200`,
      `MaxSendRate: 1` — conta segue no sandbox, saída necessária)

## Prod — identidade de domínio, DKIM e verificação

- [x] 2. Criar `backend/infra/terraform/environments/prod/ses.tf` com
      `aws_ses_domain_identity.main` (`jrnexpenses.com`),
      `aws_ses_domain_dkim.main` e `aws_ses_domain_identity_verification.main`
- [x] 3. Adicionar em `backend/infra/terraform/environments/prod/dns.tf`
      o record `aws_route53_record.ses_verification` (TXT,
      `_amazonses.jrnexpenses.com`) e os 3 records
      `aws_route53_record.ses_dkim` (CNAME, via `count`)
- [x] 4. Rodar `terraform validate`/`terraform plan` em
      `environments/prod/` (sem aplicar) — confirmar que só os recursos
      novos de SES/DNS aparecem, nada existente é destruído/recriado
      (`terraform plan` completo falha em `iam:GetRole` com o perfil
      `agent-toolkit` — mesmo guardrail de IAM já documentado em
      `backend/infra/CLAUDE.md`; `terraform plan -refresh=false`
      confirma "Plan: 7 to add, 1 to change, 0 to destroy" — os 7 são
      só os recursos novos de SES/DNS, o 1 é drift pré-existente e não
      relacionado em `aws_iam_role_policy.lambda_exec`, sem nada
      destruído)
- [x] 5. Pedir aprovação explícita e executar `terraform apply` só dos
      recursos de SES/DNS em `environments/prod/` (identidade, DKIM,
      records, verificação) — aprovado pelo usuário; `terraform apply
      -refresh=false -target=...` (5 targets, evitando o guardrail de
      IAM da task 4) criou os 7 recursos, `aws_ses_domain_identity_
      verification.main` completou em 38s (`id=jrnexpenses.com`) —
      identidade verificada

## Hom — identidade de domínio, DKIM e verificação

- [x] 6. Criar `backend/infra/terraform/environments/hom/ses.tf` com
      `aws_ses_domain_identity.main` (`hom.jrnexpenses.com`),
      `aws_ses_domain_dkim.main` e `aws_ses_domain_identity_verification.main`
- [x] 7. Adicionar em `backend/infra/terraform/environments/hom/dns.tf`
      o record `aws_route53_record.ses_verification` (TXT,
      `_amazonses.hom.jrnexpenses.com`) e os 3 records
      `aws_route53_record.ses_dkim` (CNAME, via `count`)
- [x] 8. Rodar `terraform validate`/`terraform plan` em
      `environments/hom/` (sem aplicar) — confirmar que só os recursos
      novos de SES/DNS aparecem, nada existente é destruído/recriado
      (mesmo guardrail de `iam:GetRole` do item 4;
      `terraform plan -refresh=false` confirma "Plan: 7 to add, 1 to
      change, 0 to destroy", mesmo padrão: 7 recursos novos de SES/DNS,
      1 drift pré-existente não relacionado)
- [x] 9. Pedir aprovação explícita e executar `terraform apply` só dos
      recursos de SES/DNS em `environments/hom/` (identidade, DKIM,
      records, verificação) — aprovado pelo usuário; mesmo padrão da
      task 5, `aws_ses_domain_identity_verification.main` completou em
      37s (`id=hom.jrnexpenses.com`) — identidade verificada

## Cognito + IAM (prod e hom)

- [x] 10. Adicionar o bloco `email_configuration` (com `depends_on =
      [aws_ses_domain_identity_verification.main]`) no
      `aws_cognito_user_pool.main` de
      `environments/prod/cognito.tf` (`from_email_address = "jrn.expenses <no-reply@jrnexpenses.com>"`)
- [x] 11. Adicionar o bloco `email_configuration` equivalente em
      `environments/hom/cognito.tf`
      (`from_email_address = "jrn.expenses (homologação) <no-reply@hom.jrnexpenses.com>"`)
- [x] 12. Adicionar a statement `SesSendEmail`
      (`ses:SendEmail`/`ses:SendRawEmail`, `Resource =
      aws_ses_domain_identity.main.arn`) na
      `aws_iam_role_policy.lambda_exec` de `environments/prod/lambda.tf`
      e na equivalente de `environments/hom/lambda.tf`
- [x] 13. Adicionar a mesma statement `SesSendEmail` na
      `aws_iam_role_policy.account_trigger_lambda_exec` de
      `environments/prod/lambda-account-trigger.tf` e na equivalente de
      `environments/hom/lambda-account-trigger.tf`
- [x] 14. Adicionar os outputs `ses_domain_identity_arn` e
      `ses_sender_email` em `environments/prod/outputs.tf` e
      `environments/hom/outputs.tf`
- [x] 15. Rodar `terraform validate`/`terraform plan` em
      `environments/prod/` e `environments/hom/` — confirmar que só os
      campos novos aparecem (Cognito `email_configuration`, policies
      IAM), sem recriar User Pool nem Lambdas (`terraform plan
      -refresh=false` em ambos: "0 to add, 3 to change, 0 to destroy",
      todos `update in-place` — Cognito `email_configuration` e as 2
      policies IAM ganhando `SesSendEmail`, nada recriado)
- [x] 16. Pedir aprovação explícita e executar `terraform apply` em
      `environments/prod/` e `environments/hom/` (Cognito + IAM) —
      ambos rodados localmente pelo usuário (fora do bloqueio do
      classificador do harness). Hom: `email_configuration` confirmado
      via `aws cognito-idp describe-user-pool` (`EmailSendingAccount:
      DEVELOPER`, `SourceArn` → `hom.jrnexpenses.com`). Prod:
      `email_configuration` confirmado do mesmo jeito (`SourceArn` →
      `jrnexpenses.com`) e a policy IAM da Lambda da API confirmada via
      `aws iam get-role-policy`; a policy da Lambda de trigger não deu
      pra confirmar via API (mesmo guardrail de IAM da task 4,
      `AccessDenied` em `iam:GetRolePolicy` só nessa role específica),
      confirmada em vez disso via `terraform state show` — `SesSendEmail`
      presente

## Sandbox (condicional, só se a task 1 indicar `ProductionAccessEnabled = false`)

- [x] 17. Pedir aprovação explícita e solicitar a saída do sandbox do
      SES (`aws sesv2 put-account-details` ou console — mesma conta
      única, cobre hom e prod) — aprovado pelo usuário; enviado via
      `aws sesv2 put-account-details` (`mail-type=TRANSACTIONAL`,
      `website-url=https://jrnexpenses.com`, contato adicional
      `reato.neto@gmail.com`)
- [x] 18. Registrar o status da solicitação (aprovada/pendente) para
      referência posterior — `aws sesv2 get-account` confirma
      `ReviewDetails.Status = "PENDING"`, `ProductionAccessEnabled`
      ainda `false` (aguardando análise da AWS)

## Validação e documentação

- [x] 19. Validar manualmente em hom: `POST /auth/register` (e-mail
      próprio verificável dentro do escopo permitido pelo
      sandbox/produção liberada) seguido de `POST /auth/login`,
      confirmando que o e-mail de confirmação chega com remetente
      `no-reply@hom.jrnexpenses.com` — identidade individual
      `reato.neto@gmail.com` verificada no SES (necessário por ainda
      estar no sandbox, task 17); conta de teste pré-existente com esse
      e-mail excluída e recriada para forçar um código novo;
      `POST /auth/register` (201) → e-mail chegou (remetente
      confirmado pelo usuário, "jrn.expenses (...)") → confirmado com o
      código real via `aws cognito-idp confirm-sign-up` →
      `POST /auth/login` (200, token emitido) — fluxo sem regressão.
      Dados de teste limpos ao final (itens DynamoDB
      Account/Membership/Categorias + `admin-delete-user`). **Achado**:
      o e-mail caiu na caixa de spam do Gmail — provável causa é
      ausência de SPF/MAIL FROM customizado e DMARC (só DKIM foi
      configurado, conforme escopo da spec); oferecido ao usuário como
      item de backlog
- [x] 20. Atualizar `backend/infra/CLAUDE.md` com a nova seção de SES
      (identidades por ambiente, DKIM, `email_configuration` do
      Cognito, permissões IAM concedidas, status do sandbox) — também
      documentado o guardrail de IAM mais amplo achado na task 4/16
      (`iam:GetRole`/`GetRolePolicy` bloqueado pro `agent-toolkit` numa
      role já existente, não só OIDC/criação)
- [x] 21. Atualizar `backend/docs/backlog.md` marcando a FEAT-33 como
      concluída (`- [x]`) — e adicionado o débito técnico de
      deliverability (spam, achado na task 19)
- [x] 22. Atualizar
      `backend/specs/FEAT-33-infra-email-transacional-ses/spec.md`
      marcando todos os critérios de aceite (`- [ ]` → `- [x]`) e
      preenchendo a seção "Status" com o resultado final
